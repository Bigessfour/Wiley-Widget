#!/usr/bin/env python3
"""
Resource scanner with atomic summary output. This uses the same scanning logic and writes
an atomic summary JSON to --output using tools.atomic_write.
"""
import sys
import argparse
from pathlib import Path
import os
import json
from resource_scanner_enhanced import find_resources
from atomic_write import atomic_write


def main(argv):
    parser = argparse.ArgumentParser(description='Resource scanner (enhanced) + atomic output')
    parser.add_argument('--paths', type=str, help='Comma separated paths to scan', default='resources,Styles,src,tools')
    parser.add_argument('-v', '--verbose', action='store_true')
    parser.add_argument('--output', type=str, help='file path to write a summary JSON output', default='logs/resource_scan_summary.json')
    args = parser.parse_args(argv)

    input_paths = [p.strip() for p in args.paths.split(',') if p.strip()]
    if args.verbose:
        print('Scanning paths:', ', '.join(input_paths))

    found = find_resources(input_paths, verbose=args.verbose)

    summary = {
        'total': len(found),
        'sample': found[:50]
    }

    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    atomic_write(output_path, json.dumps(summary, indent=2), retries=6, delay=0.02)
    if args.verbose:
        print('Wrote summary to', output_path)

    return 0


if __name__ == '__main__':
    try:
        sys.exit(main(sys.argv[1:]))
    except Exception as e:
        print('ERROR:', e)
        sys.exit(2)
