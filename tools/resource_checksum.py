#!/usr/bin/env python3
"""Детерминированная контрольная сумма всех бандл-ассетов (для экрана «О игре»)."""
import hashlib
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIRS = ('assets/audio', 'assets/fonts')

def main() -> int:
    h = hashlib.sha256()
    paths = []
    for folder in DIRS:
        full = os.path.join(ROOT, folder)
        for name in sorted(os.listdir(full)):
            p = os.path.join(full, name)
            if os.path.isfile(p):
                paths.append((os.path.relpath(p, ROOT), p))
    for rel, p in paths:
        h.update(rel.encode('utf-8'))
        with open(p, 'rb') as f:
            h.update(f.read())
    print(f'{len(paths)} assets, sha256={h.hexdigest()}')
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
