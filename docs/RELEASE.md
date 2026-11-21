# Орбита — сборка и релиз

Версия_branch: `v2`. Релиз `v4.0` уже опубликован и не перетирается;
новые изменения версионируются отдельно.

## Локально

```bash
pnpm install --store-dir /tmp/opencode/pnpm-store
pnpm run build
pnpm run check:logic
pnpm run check:manifest
pnpm exec cap sync
python3 tools/resource_checksum.py
python3 tools/parity_report.py
```

iOS-сборка локально недоступна без macOS/Xcode — её выполняет GitHub Actions.

## CI

`gh workflow run Build --ref v2` запускает:

- `verify` — при теге проверяется совпадение тега, package.json, Android и iOS версий;
- `android` — debug APK, логический QA, манифест, проверка размера;
- `ios` — unsigned device IPA, проверка размера;
- `release` — только по тегу `vX.Y`, заметки из CHANGELOG.

Артефакты: `orbita-vX.Y-debug.apk` и `orbita-vX.Y-unsigned.ipa`.
Сертификаты, ключи и provisioning-профили в репозитории отсутствуют.
