# Орбита

Неоновая аркада про корабль на орбите планеты. Android — Kotlin + Jetpack Compose, iOS — SwiftUI + Canvas. Вся графика рисуется кодом, все звуки синтезированы, никаких сторонних ассетов.

## Геймплей

Корабль летит по орбите. Из космоса летят астероиды.

- **Тап** — сменить направление полёта
- **Астероид пролетел мимо** — очки
- **Близкий пролёт** — бонус за риск, вспышка и микро-замедление
- **Золотая сфера** — +3 очка и рост комбо
- **Бонусы** — щит, замедление времени, двойные очки
- **Комбо** — серия уклонений/сборов повышает множитель до x5
- **Уровни** — каждые 50 очков, скорость и плотность растут
- **Столкновение** — конец игры; щит спасает один раз

## Звук

Все сэмплы сгенерированы скриптом `tools/generate_sounds.py` (чистый Python, стандартная библиотека):

- 7 эффектов: тап, уклонение, сфера, бонус, щит, краш, новый уровень
- зацикленный эмбиент-пад на 12 секунд
- высота звука уклонения растёт вместе с комбо (rate у SoundPool / AVAudioPlayer)

```bash
python3 tools/generate_sounds.py   # перегенерировать в res/raw и ios/Orbit/Resources
```

## Структура

```
app/                Android (Kotlin, Compose)
  src/main/java/com/example/orbit/
    game/           движок: сущности, симуляция
    ui/             Canvas-рендер, HUD, оверлеи
    audio/          SoundPool + MediaPlayer
    data/           DataStore (рекорд, mute)
ios/                iOS (SwiftUI)
  Orbit/            GameEngine, GameRenderer, ContentView, SoundEngine
  project.yml       проект генерируется XcodeGen
tools/              генератор звуков
```

## Сборка

**Android** — APK собирается в GitHub Actions на каждый пуш: вкладка Actions → артефакт `orbita-debug-apk`. Локально: `./gradlew assembleDebug`.

**iOS** — CI на macOS гоняет `xcodegen` + `xcodebuild` для симулятора (без подписи). Локально:

```bash
cd ios
brew install xcodegen
xcodegen generate
open Orbit.xcodeproj
```

Для установки на устройство и TestFlight нужна подпись — см. [RELEASE.md](RELEASE.md).

## Стек

Android: Kotlin 2.0, AGP 8.7, Gradle 8.11, Compose BOM 2024.10, minSdk 26.
iOS: Swift 5, SwiftUI Canvas, iOS 16+, XcodeGen.
