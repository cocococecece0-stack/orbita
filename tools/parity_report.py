#!/usr/bin/env python3
"""Проверка паритета Android/iOS: обе оболочки обязаны нести байт-идентичный web-бандл.

Запуск: python3 tools/parity_report.py [repo_root]
"""
from __future__ import annotations

import hashlib
import sys
from pathlib import Path

ANDROID = Path("android/app/src/main/assets/public")
IOS = Path("ios/App/App/public")
KEY_SUFFIXES = (".js", ".css", ".html", ".m4a", ".woff2")


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def snapshot(root: Path) -> dict[str, str]:
    if not root.is_dir():
        return {}
    out: dict[str, str] = {}
    for path in root.rglob("*"):
        if not path.is_file() or not path.name.endswith(KEY_SUFFIXES):
            continue
        out[str(path.relative_to(root))] = digest(path)
    return out


def main() -> int:
    repo = Path(sys.argv[1]) if len(sys.argv) > 1 else Path.cwd()
    a = snapshot(repo / ANDROID)
    i = snapshot(repo / IOS)

    if not a or not i:
        print(f"FAIL: нет бандла в {ANDROID} или {IOS}; сначала pnpm run build && pnpm exec cap sync")
        return 1

    problems: list[str] = []
    only_a = sorted(set(a) - set(i))
    only_ios = sorted(set(i) - set(a))
    if only_a:
        problems.append(f"только в Android: {', '.join(only_a)}")
    if only_ios:
        problems.append(f"только в iOS: {', '.join(only_ios)}")
    for name in sorted(set(a) & set(i)):
        if a[name] != i[name]:
            problems.append(f"различается: {name}")

    m4a = sum(1 for n in a if n.endswith(".m4a"))
    woff = sum(1 for n in a if n.endswith(".woff2"))
    print(f"Android: {len(a)} файлов, iOS: {len(i)} файлов")
    print(f"Аудио M4A: {m4a}, шрифты WOFF2: {woff} (одинаково на обеих платформах)")
    if m4a < 9 or woff < 5:
        problems.append("ожидались 9 M4A и 5 WOFF2")
    if problems:
        for p in problems:
            print(f"FAIL: {p}")
        return 1
    print("OK: web-бандлы Android и iOS байт-идентичны")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
