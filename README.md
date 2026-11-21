# Орбита

Неоновая аркада про корабль на орбите планеты. Android — Kotlin + Jetpack Compose, iOS — SwiftUI + Canvas. Вся графика рисуется кодом, все звуки синтезированы, никаких сторонних ассетов.

## Геймплей

Корабль летит по орбите. Из космоса летят астероиды.

- **Тап** — сменить направление полёта
- **Астероид пролетел мимо** — очки
- **Близкий пролёт** — бонус за риск, вспышка и микро-замедление
- **Золотая сфера** — +3 очка и рост комбо
- **Бонусы** — щит, замедление времени, двойные очки
- **Комбо** — серия уклонений/сборов повышает множитель до x5; таймер комбо виден кольцом вокруг планеты
- **Уровни** — каждые 50 очков, скорость и плотность растут
- **Кометы** — со 2 уровня, быстрые, дают вдвое больше очков
- **Метеорный дождь** — со 2 уровня, предупреждение и залп из 8 астероидов с безопасным окном
- **Скины корабля** — 4 цвета, открываются за рекорд (150 / 400 / 800)
- **Статистика** — забеги, всего сфер, лучшее комбо, локальный топ-5
- **Столкновение** — конец игры; щит спасает один раз

## Звук

Все сэмплы сгенерированы скриптом `tools/generate_sounds.py` (чистый Python, стандартная библиотека):

- 8 эффектов: тап, уклонение, сфера, бонус, щит, краш, новый уровень, метеорный дождь
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

**iOS** — CI на macOS собирает проект для симулятора и пакует **unsigned IPA** (`orbita-unsigned-ipa`) из device-сборки без подписи. Такой IPA ставится на iPhone через AltStore или Sideloadly: они переподписывают его твоим Apple ID. Для TestFlight нужна подпись — см. [RELEASE.md](RELEASE.md). Локально:

```bash
cd ios
brew install xcodegen
xcodegen generate
open Orbit.xcodeproj
```

**Релиз** — поставь тег `vX.Y`: workflow соберёт обе платформы и сам опубликует релиз с APK и IPA.

```bash
git tag v3.0 && git push origin v3.0
```

## Стек

Android: Kotlin 2.0, AGP 8.7, Gradle 8.11, Compose BOM 2024.10, minSdk 26.
iOS: Swift 5, SwiftUI Canvas, iOS 16+, XcodeGen.
