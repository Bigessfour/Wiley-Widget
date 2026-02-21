#!/usr/bin/env python3
"""grok_pr_review.py

Fetch PR diff, ask Grok-4 (via XAI endpoint) for a concise review, and post it
as a comment on the PR.

Required environment variables (set via GitHub Actions secrets):
- XAI_API_KEY: API key for the Grok/XAI provider
- XAI_API_URL: Full HTTP(S) endpoint to send prompts to (provider-specific)
- GITHUB_TOKEN: GitHub token to post comments (from the workflow)

This script intentionally does not check out or execute untrusted PR code.
It fetches the PR metadata/diff via the GitHub API and sends the diff (truncated)
to the XAI endpoint, then posts the returned text as a PR comment.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import textwrap
from typing import Tuple

import requests


def get_pr_and_diff(
    owner: str, repo: str, pr_number: str, gh_token: str
) -> Tuple[dict, str]:
    url = f"https://api.github.com/repos/{owner}/{repo}/pulls/{pr_number}"
    headers = {
        "Authorization": f"Bearer {gh_token}",
        "Accept": "application/vnd.github.v3+json",
    }
    resp = requests.get(url, headers=headers, timeout=30)
    resp.raise_for_status()
    pr = resp.json()

    diff_url = pr.get("diff_url")
    if not diff_url:
        raise RuntimeError("PR response did not include diff_url")

    diff_resp = requests.get(diff_url, headers=headers, timeout=60)
    diff_resp.raise_for_status()
    return pr, diff_resp.text


def call_xai(prompt: str, api_url: str, api_key: str, timeout: int = 60) -> str:
    headers = {"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"}
    payload = {"input": prompt}
    resp = requests.post(api_url, headers=headers, json=payload, timeout=timeout)
    resp.raise_for_status()

    # Try to be resilient to different response shapes
    try:
        data = resp.json()
    except ValueError:
        return resp.text

    # Common providers use `choices`, `text`, `output`, or top-level string
    if isinstance(data, dict):
        if "output" in data and isinstance(data["output"], str):
            return data["output"]
        if "text" in data and isinstance(data["text"], str):
            return data["text"]
        if "choices" in data and isinstance(data["choices"], list) and data["choices"]:
            first = data["choices"][0]
            if isinstance(first, dict) and "text" in first:
                return first["text"]
            if (
                isinstance(first, dict)
                and "message" in first
                and isinstance(first["message"], str)
            ):
                return first["message"]
    # Fallback: pretty-print the JSON
    return json.dumps(data, indent=2)


def post_pr_comment(
    owner: str, repo: str, pr_number: str, gh_token: str, body: str
) -> dict:
    url = f"https://api.github.com/repos/{owner}/{repo}/issues/{pr_number}/comments"
    headers = {
        "Authorization": f"Bearer {gh_token}",
        "Accept": "application/vnd.github+json",
    }
    resp = requests.post(url, headers=headers, json={"body": body}, timeout=30)
    resp.raise_for_status()
    return resp.json()


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Request Grok PR review and post as PR comment"
    )
    parser.add_argument("--pr-number", required=True)
    parser.add_argument("--repo", required=True, help="owner/repo")
    args = parser.parse_args()

    gh_token = os.environ.get("GITHUB_TOKEN")
    xai_key = os.environ.get("XAI_API_KEY")
    xai_url = os.environ.get("XAI_API_URL")

    if not gh_token:
        print("GITHUB_TOKEN environment variable is required", file=sys.stderr)
        return 2
    if not xai_key or not xai_url:
        print(
            "XAI_API_KEY and XAI_API_URL environment variables must be set (GitHub Secrets)",
            file=sys.stderr,
        )
        return 2

    owner_repo = args.repo.strip()
    if "/" not in owner_repo:
        print("--repo must be in owner/repo format", file=sys.stderr)
        return 2
    owner, repo = owner_repo.split("/", 1)

    try:
        pr, diff_text = get_pr_and_diff(owner, repo, args.pr_number, gh_token)
    except Exception as e:
        print(f"Failed to fetch PR/diff: {e}", file=sys.stderr)
        return 3

    title = pr.get("title", "")
    body = pr.get("body", "")

    # Truncate large diffs to keep prompt reasonably sized
    max_chars = 120_000
    if len(diff_text) > max_chars:
        diff_excerpt = diff_text[:max_chars] + "\n\n---DIFF TRUNCATED---"
    else:
        diff_excerpt = diff_text

    prompt = textwrap.dedent(f"""
    You are Grok-4, a concise code reviewer. Produce:
    1) Short summary (2-3 sentences)
    2) Bullet list of potential issues or risks
    3) Actionable suggestions (short bullets)
    4) A concise comment suitable for posting to the PR (1-2 paragraphs)

    PR title:
    {title}

    PR body:
    {body}

    Diff:
    {diff_excerpt}
    """)

    try:
        review_text = call_xai(prompt, xai_url, xai_key)
    except Exception as e:
        print(f"XAI request failed: {e}", file=sys.stderr)
        return 4

    comment = (
        "**Grok-4 PR Review (automated)**\n\n"
        + review_text
        + "\n\n_This review was generated automatically._"
    )

    try:
        resp = post_pr_comment(owner, repo, args.pr_number, gh_token, comment)
        print("Posted comment:", resp.get("html_url"))
    except Exception as e:
        print(f"Failed to post PR comment: {e}", file=sys.stderr)
        return 5

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
