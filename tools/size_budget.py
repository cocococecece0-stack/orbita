#!/usr/bin/env python3
"""Сообщает размер артефактов и валит сборку только при превышении TARGET_SIZE_MB.

Лимит задаётся окружением; по умолчанию 512 МБ — размер не является ограничением
продукта, но проверка остаётся как страховка от случайного включения мусора в бандл.
"""
import os
import sys

MAX_MB = float(os.environ.get('TARGET_SIZE_MB', '512'))


def main() -> int:
    files = [p for p in sys.argv[1:] if p]
    if not files:
        print('size_budget: не переданы артефакты', file=sys.stderr)
        return 2
    failed = False
    for path in files:
        if not os.path.isfile(path):
            print(f'size_budget: файл не найден: {path}', file=sys.stderr)
            return 2
        mb = os.path.getsize(path) / (1024 * 1024)
        status = 'OK' if mb <= MAX_MB else 'FAIL'
        print(f'{status} {path}: {mb:.2f} MB / {MAX_MB:.1f} MB')
        if mb > MAX_MB:
            failed = True
    return 1 if failed else 0


if __name__ == '__main__':
    raise SystemExit(main())
