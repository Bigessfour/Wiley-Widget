#!/usr/bin/env python3
"""
Atomic write helper with retry/backoff to reduce transient EPERM/rename failures on Windows.
Use `atomic_write(path, data, retries=N, delay=0.05)` to attempt to write content to a temp
file then atomically replace the target. Retries on exceptions with exponential backoff.
"""
import os
import time
from pathlib import Path
from typing import Union


def atomic_write(
    path: Union[str, Path],
    data: Union[str, bytes],
    retries: int = 5,
    delay: float = 0.05,
    encoding: str = "utf-8",
) -> None:
    """Atomically write `data` to `path`.

    On Windows a rename/replace may fail if another process has the file open.
    This function attempts the write and os.replace up to `retries` times using
    exponential backoff on failure.

    Args:
        path: target file path
        data: str or bytes to write
        retries: number of attempts
        delay: initial delay in seconds for backoff
        encoding: encoding used when `data` is str

    Raises:
        Exception: the last raised exception if all retries fail
    """
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)

    tmp = target.with_suffix(target.suffix + ".tmp")

    attempt = 0
    while True:
        try:
            # write to temp file
            if isinstance(data, bytes):
                tmp.write_bytes(data)
            else:
                tmp.write_text(data, encoding=encoding)

            # os.replace is atomic on most platforms and will overwrite target
            os.replace(str(tmp), str(target))
            # success
            return
        except Exception:
            attempt += 1
            # remove tmp if it exists (best-effort)
            try:
                if tmp.exists():
                    tmp.unlink(missing_ok=True)
            # trunk-ignore(bandit/B110)
            except Exception:
                pass

            if attempt > retries:
                # re-raise last exception after exhausting retries
                raise

            # exponential backoff with jitter
            sleep_time = delay * (2 ** (attempt - 1))
            jitter = min(sleep_time * 0.2, 0.5)
            sleep_time = sleep_time + (jitter * (0.5 - os.urandom(1)[0] / 255.0))
            time.sleep(max(sleep_time, 0.01))


if __name__ == "__main__":
    # Small CLI to write a file — useful for quick manual tests
    import argparse

    parser = argparse.ArgumentParser(description="atomic_write helper (CLI)")
    parser.add_argument("path", help="target file path")
    parser.add_argument("--text", help="text to write (default: test)")
    parser.add_argument("--retries", type=int, default=5)
    parser.add_argument("--delay", type=float, default=0.05)
    args = parser.parse_args()

    content = args.text or f"Test write at {time.time()}\n"
    atomic_write(args.path, content, retries=args.retries, delay=args.delay)
    print("Wrote", args.path)
