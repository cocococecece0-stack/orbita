#!/usr/bin/env python3
"""Детерминированный манифест dist:для  проверки, что обе платформы получили один бандл."""
import hashlib
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIST = os.path.join(ROOT, 'dist')


def main() -> int:
    if not os.path.isdir(DIST):
        print('manifest: нет dist; сначала pnpm run build', file=sys.stderr)
        return 2
    files = []
    for base, dirs, names in os.walk(DIST):
        dirs.sort()
        for name in sorted(names):
            path = os.path.join(base, name)
            rel = os.path.relpath(path, DIST).replace(os.sep, '/')
            with open(path, 'rb') as f:
                digest = hashlib.sha256(f.read()).hexdigest()
            files.append({'path': rel, 'size': os.path.getsize(path), 'sha256': digest})
    files.sort(key=lambda x: x['path'])
    print(json.dumps({'files': files}, ensure_ascii=False, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
