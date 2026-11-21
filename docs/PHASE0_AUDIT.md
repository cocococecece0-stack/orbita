# Фаза 0 — аудит размера (зафиксировано)

- v3.0 APK: 9.80 MB (compressed). Главный вклад: ~8.6 MB dex (Kotlin + Compose + AndroidX + DataStore), 691 KB WAV, 418 KB resources.arsc, 59 KB нативных .so на 4 ABI.
- v3.0 unsigned IPA: 0.81 MB. ~87% — 9 WAV; Mach-O arm64 — 123 KB compressed. Системные фреймворки iOS в IPA не входят.
- Причина разницы в 12 раз — рантайм Compose в APK, а не ассеты и не App Thinning.
- 9 WAV в APK и IPA побайтово идентичны (SHA256 совпадает).
- Решение: единое TS/WebGL2/Capacitor-ядро, AAC/M4A, budget 8 MB, CI-валидатор размера.
