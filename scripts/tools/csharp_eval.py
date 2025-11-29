"""
Small Python wrapper to call the local C# evaluator (scripts/tools/csharp_mcp_local)
Usage examples:
  python scripts/tools/csharp_eval.py --expr "1+2"
  python scripts/tools/csharp_eval.py --file ../debug_combo.csx
  python scripts/tools/csharp_eval.py --json '{"code":"1+2"}'

Returns: prints JSON response from evaluator
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVAL_PROJ = ROOT / "scripts" / "tools" / "csharp_mcp_local"


def run_json_request(req: dict):
    # call dotnet run -- -j and send req as stdin
    cmd = ["dotnet", "run", "--project", str(EVAL_PROJ), "--", "-j"]
    proc = subprocess.run(
        cmd,
        input=json.dumps(req).encode("utf-8"),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    out = proc.stdout.decode("utf-8").strip()
    err = proc.stderr.decode("utf-8").strip()

    if err:
        print("STDERR:", err, file=sys.stderr)

    # output may be JSON lines or normal text. Try parse
    try:
        j = json.loads(out)
        print(json.dumps(j, indent=2))
        return j
    except Exception:
        print(out)
        return None


def docker_available() -> bool:
    """Return True if docker daemon responds to a quick check."""
    try:
        r = subprocess.run(
            ["docker", "info"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL
        )
        return r.returncode == 0
    except FileNotFoundError:
        return False


def start_docker_desktop_on_windows(timeout: int = 30) -> bool:
    """Try to start Docker Desktop on Windows by launching known executable paths.
    Best-effort; returns True if docker becomes available within timeout seconds.
    """
    paths = [
        r"C:\Program Files\Docker\Docker\Docker Desktop.exe",
        r"C:\Program Files\Docker\Docker Desktop.exe",
    ]
    # Attempt to start the first existing path
    import platform
    import time

    if platform.system().lower() != "windows":
        return False

    found = None
    for p in paths:
        if Path(p).exists():
            found = p
            break

    if not found:
        # nothing to start
        return False

    try:
        # launch and return quickly
        subprocess.Popen([found], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception:
        return False

    # Wait for docker to become available
    start = time.time()
    while time.time() - start < timeout:
        if docker_available():
            return True
        time.sleep(1)
    return False


def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=False)
    group.add_argument("--expr", "-e", help="C# expression to evaluate")
    group.add_argument("--file", "-f", help="Path to file to evaluate (C# script)")
    group.add_argument("--json", "-j", help="JSON payload to send")
    parser.add_argument(
        "--server",
        "-s",
        action="store_true",
        help="Start an interactive evaluator server (stdin JSON per-line)",
    )
    parser.add_argument(
        "--prefer-docker",
        action="store_true",
        help="Prefer Docker for evaluations when available",
    )
    parser.add_argument(
        "--ensure-docker",
        action="store_true",
        help="If Docker is not running, attempt to auto-start Docker Desktop on Windows (best-effort)",
    )

    args = parser.parse_args()

    # docker preference/ensure handling
    if args.prefer_docker or args.ensure_docker:
        if docker_available():
            print(
                "Docker daemon is available — container-based evaluation is possible."
            )
        else:
            print("Docker daemon is not available.")
            if args.ensure_docker:
                print(
                    "Attempting to start Docker Desktop on Windows (best-effort) — this may prompt for permissions."
                )
                ok = start_docker_desktop_on_windows(timeout=30)
                if ok:
                    print("Docker became available.")
                else:
                    print("Could not start Docker; continuing with local evaluator.")
                    print("To use Docker, please start Docker Desktop and re-run.")
            else:
                print(
                    "Run with --ensure-docker to attempt auto-start on Windows, or start Docker Desktop manually."
                )

    if args.server:
        # start long-running server and give user prompt to send JSON lines.
        cmd = ["dotnet", "run", "--project", str(EVAL_PROJ), "--", "--server"]
        proc = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )

        # read the initial server line
        first = proc.stdout.readline()
        print("SERVER:", first.strip())
        print("Enter JSON commands (one per line) or blank to exit")
        try:
            while True:
                line = input("> ")
                if not line.strip():
                    break
                proc.stdin.write(line + "\n")
                proc.stdin.flush()
                out = proc.stdout.readline()
                print(out.strip())
        finally:
            proc.terminate()
            proc.wait()
        return

    # evaluate simple expression
    if args.expr:
        req = {"command": "eval", "code": args.expr}
        run_json_request(req)
        return

    # evaluate a file
    if args.file:
        p = Path(args.file)
        if not p.exists():
            print("File not found:", p, file=sys.stderr)
            sys.exit(2)
        req = {"command": "eval", "file": str(p)}
        run_json_request(req)
        return

    # raw JSON payload
    if args.json:
        try:
            req = json.loads(args.json)
        except Exception as ex:
            print("Invalid JSON provided:", ex, file=sys.stderr)
            sys.exit(2)
        run_json_request(req)


if __name__ == "__main__":
    main()
