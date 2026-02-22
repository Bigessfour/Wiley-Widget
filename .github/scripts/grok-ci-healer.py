#!/usr/bin/env python3
"""Grok CI auto-healer.

Downloads failed GitHub Actions logs for a workflow run, asks Grok for a unified diff,
applies the patch, and leaves changes for git-auto-commit to push.
"""

from __future__ import annotations

import io
import json
import os
import re
import subprocess
import sys
import textwrap
import zipfile
from pathlib import Path
from typing import Any
from urllib import error as urlerror
from urllib import request as urlrequest

MAX_LOG_CHARS = 180_000
MAX_DOC_CHARS = 16_000


def require_env(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def split_repo(repo: str) -> tuple[str, str]:
    if "/" not in repo:
        raise RuntimeError("GITHUB_REPOSITORY must be in owner/repo format")
    owner, name = repo.split("/", 1)
    return owner, name


def http_get_bytes(url: str, headers: dict[str, str], timeout: int) -> bytes:
    request = urlrequest.Request(url=url, headers=headers, method="GET")
    try:
        with urlrequest.urlopen(request, timeout=timeout) as response:
            return response.read()
    except urlerror.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {error.code} for GET {url}: {details}") from error


def http_post_json(
    url: str, headers: dict[str, str], payload: dict[str, Any], timeout: int
) -> bytes:
    body = json.dumps(payload).encode("utf-8")
    request = urlrequest.Request(url=url, headers=headers, data=body, method="POST")
    try:
        with urlrequest.urlopen(request, timeout=timeout) as response:
            return response.read()
    except urlerror.HTTPError as error:
        details = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {error.code} for POST {url}: {details}") from error


def github_get_json(url: str, token: str) -> dict[str, Any]:
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    payload = http_get_bytes(url, headers, timeout=60)
    return json.loads(payload.decode("utf-8", errors="replace"))


def fetch_run(owner: str, repo: str, run_id: str, token: str) -> dict[str, Any]:
    url = f"https://api.github.com/repos/{owner}/{repo}/actions/runs/{run_id}"
    return github_get_json(url, token)


def resolve_pr_number(run: dict[str, Any]) -> int | None:
    value = os.environ.get("PR_NUMBER", "").strip()
    if value:
        match = re.search(r"\d+", value)
        if match:
            return int(match.group(0))

    pull_requests = run.get("pull_requests")
    if isinstance(pull_requests, list) and pull_requests:
        first = pull_requests[0]
        if isinstance(first, dict) and isinstance(first.get("number"), int):
            return first["number"]

    return None


def download_logs(owner: str, repo: str, run_id: str, token: str) -> str:
    url = f"https://api.github.com/repos/{owner}/{repo}/actions/runs/{run_id}/logs"
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    archive_bytes = http_get_bytes(url, headers, timeout=120)
    with zipfile.ZipFile(io.BytesIO(archive_bytes), "r") as archive:
        names = sorted(name for name in archive.namelist() if not name.endswith("/"))
        chunks: list[str] = []
        for name in names:
            with archive.open(name, "r") as handle:
                content = handle.read().decode("utf-8", errors="replace")
                chunks.append(f"\n===== {name} =====\n{content}")

    log_text = "\n".join(chunks)
    if len(log_text) > MAX_LOG_CHARS:
        return log_text[-MAX_LOG_CHARS:]
    return log_text


def read_doc(path: Path, max_chars: int = MAX_DOC_CHARS) -> str:
    if not path.exists():
        return ""
    content = path.read_text(encoding="utf-8", errors="replace")
    if len(content) > max_chars:
        return content[:max_chars]
    return content


def call_xai(prompt: str, api_key: str, api_url: str, model: str) -> str:
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }
    payload = {
        "model": model,
        "input": prompt,
    }
    response_bytes = http_post_json(api_url, headers, payload, timeout=120)
    response_text = response_bytes.decode("utf-8", errors="replace")

    try:
        data = json.loads(response_text)
    except ValueError:
        return response_text

    if isinstance(data, dict):
        output = data.get("output")
        if isinstance(output, str):
            return output
        if isinstance(output, list):
            text_fragments: list[str] = []
            for item in output:
                if not isinstance(item, dict):
                    continue
                content = item.get("content")
                if isinstance(content, list):
                    for segment in content:
                        if isinstance(segment, dict) and isinstance(
                            segment.get("text"), str
                        ):
                            text_fragments.append(segment["text"])
            if text_fragments:
                return "\n".join(text_fragments)

        text_value = data.get("text")
        if isinstance(text_value, str):
            return text_value

        choices = data.get("choices")
        if isinstance(choices, list) and choices:
            first = choices[0]
            if isinstance(first, dict):
                if isinstance(first.get("text"), str):
                    return first["text"]
                message = first.get("message")
                if isinstance(message, dict):
                    content = message.get("content")
                    if isinstance(content, str):
                        return content

    return json.dumps(data, indent=2)


def extract_diff(text: str) -> str:
    fenced_blocks = re.findall(
        r"```(?:diff|patch)?\s*(.*?)```", text, flags=re.DOTALL | re.IGNORECASE
    )
    for block in fenced_blocks:
        candidate = block.strip()
        if "diff --git" in candidate or ("--- " in candidate and "+++ " in candidate):
            return candidate + "\n"

    direct_start = text.find("diff --git")
    if direct_start >= 0:
        return text[direct_start:].strip() + "\n"

    if "--- " in text and "+++ " in text:
        return text.strip() + "\n"

    return ""


def run_git(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        check=False,
        capture_output=True,
        text=True,
    )


def apply_unified_diff(diff_text: str) -> bool:
    patch_path = Path(".git") / "grok-ci-healer.patch"
    patch_path.write_text(diff_text, encoding="utf-8")

    first_try = run_git("apply", "--index", "--whitespace=fix", str(patch_path))
    if first_try.returncode == 0:
        return True

    second_try = run_git("apply", "--reject", "--whitespace=fix", str(patch_path))
    if second_try.returncode == 0:
        return True

    print("Unable to apply diff from Grok response.", file=sys.stderr)
    print(first_try.stderr, file=sys.stderr)
    print(second_try.stderr, file=sys.stderr)
    return False


def has_working_tree_changes() -> bool:
    status = run_git("status", "--porcelain")
    return bool(status.stdout.strip())


def build_prompt(
    run: dict[str, Any],
    pr_number: int,
    logs: str,
    standards: str,
    checklist: str,
) -> str:
    run_name = run.get("name", "Build & WinForms Integration Check")
    head_branch = run.get("head_branch", "unknown")
    head_sha = run.get("head_sha", "unknown")

    return textwrap.dedent(f"""
        You are a senior .NET WinForms + Syncfusion expert acting as an automated CI healer.

        Objective:
        - Fix the failing CI run by producing ONE unified git diff patch.

        Hard requirements:
        - Output only a unified diff patch (no prose).
        - Keep changes minimal and focused on CI failures.
        - Preserve existing architecture and coding style.
        - Follow these standards docs:
          1) docs/WileyWidgetUIStandards.md
          2) docs/WileyWidgetUIRolloutChecklist.md
        - For panel files, preserve ScopedPanelBase lifecycle, SafeSuspendAndLayout usage,
          PanelHeader consistency, minimum-size safety, and SfSkinManager/theming rules.
        - Do not introduce secrets, test-only hacks, or disable failing tests/builds.

        GitHub context:
        - Workflow run: {run_name}
        - PR number: {pr_number}
        - Branch: {head_branch}
        - SHA: {head_sha}

        ===== docs/WileyWidgetUIStandards.md (excerpt) =====
        {standards}

        ===== docs/WileyWidgetUIRolloutChecklist.md (excerpt) =====
        {checklist}

        ===== Failing CI Logs =====
        {logs}
        """).strip()


def main() -> int:
    try:
        api_key = require_env("XAI_API_KEY")
        gh_token = require_env("GITHUB_TOKEN")
        repository = require_env("GITHUB_REPOSITORY")
        run_id = require_env("CI_RUN_ID")
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        return 2

    api_url = os.environ.get("XAI_API_URL", "https://api.x.ai/v1/responses").strip()
    model = os.environ.get("XAI_MODEL", "grok-4-1-fast-reasoning").strip()

    owner, repo = split_repo(repository)

    try:
        run = fetch_run(owner, repo, run_id, gh_token)
    except Exception as error:
        print(f"Failed to fetch workflow run metadata: {error}", file=sys.stderr)
        return 3

    pr_number = resolve_pr_number(run)
    if pr_number is None:
        print(
            "No pull request number associated with this workflow run. Skipping auto-heal."
        )
        return 0

    try:
        logs = download_logs(owner, repo, run_id, gh_token)
    except Exception as error:
        print(f"Failed to download workflow logs: {error}", file=sys.stderr)
        return 4

    if not logs.strip():
        print("No logs found in workflow archive. Skipping auto-heal.")
        return 0

    standards = read_doc(Path("docs") / "WileyWidgetUIStandards.md")
    checklist = read_doc(Path("docs") / "WileyWidgetUIRolloutChecklist.md")
    prompt = build_prompt(run, pr_number, logs, standards, checklist)

    try:
        response = call_xai(prompt, api_key, api_url, model)
    except Exception as error:
        print(f"XAI request failed: {error}", file=sys.stderr)
        return 5

    diff_text = extract_diff(response)
    if not diff_text:
        print("Grok did not return a unified diff. Skipping commit.")
        return 0

    if not apply_unified_diff(diff_text):
        return 6

    if not has_working_tree_changes():
        print("Patch applied cleanly but produced no changes. Nothing to commit.")
        return 0

    changed_files = run_git("diff", "--name-only").stdout.strip()
    if changed_files:
        print("Changed files:")
        print(changed_files)

    print("Auto-heal patch applied successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
