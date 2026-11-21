# Как выпустить в стор

Сейчас в репозитории собирается **debug**-APK и проверяется **unsigned**-сборка iOS. Для магазинов нужны аккаунты и подпись — это делается один раз, дальше автоматизируется.

## Google Play

1. **Аккаунт:** [Google Play Console](https://play.google.com/console) — разовый взнос $25.
2. **Ключ подписи** (создать локально, в репозиторий не коммитить):

   ```bash
   keytool -genkeypair -v -keystore orbita-release.jks \
     -alias orbita -keyalg RSA -keysize 2048 -validity 10000
   base64 -w0 orbita-release.jks > keystore.b64
   ```

3. **Секреты GitHub** (Settings → Secrets and variables → Actions):
   - `KEYSTORE_BASE64` — содержимое `keystore.b64`
   - `KEYSTORE_PASSWORD`
   - `KEY_ALIAS`
   - `KEY_PASSWORD`

4. **Подпись в `app/build.gradle.kts`:**

   ```kotlin
   signingConfigs {
       create("release") {
           storeFile = file(System.getenv("KEYSTORE_PATH") ?: "orbita-release.jks")
           storePassword = System.getenv("KEYSTORE_PASSWORD")
           keyAlias = System.getenv("KEY_ALIAS")
           keyPassword = System.getenv("KEY_PASSWORD")
       }
   }
   buildTypes {
       release {
           signingConfig = signingConfigs.getByName("release")
           isMinifyEnabled = true
       }
   }
   ```

5. **Workflow:** шаг декодирования ключа и `./gradlew bundleRelease` (AAB), затем загрузка через `r0adkll/upload-google-play` или вручную в Play Console.
6. **Закрытое тестирование** → ревью → production. Первый релиз лучше катить в internal testing.

## iOS: unsigned IPA и сайдлоад

CI собирает `orbita-vX.Y-unsigned.ipa` из device-сборки без подписи. Поставить на iPhone без Apple Developer можно так:

1. Установи [AltStore](https://altstore.io) (нужен компьютер с AltServer) или [Sideloadly](https://sideloadly.io).
2. Подключи iPhone, выбери скачанный IPA.
3. Введи свой Apple ID — инструмент переподпишет приложение.
4. Доверь профилю: Настройки → Основные → VPN и управление устройством.

Ограничения бесплатного Apple ID: приложение живёт 7 дней, максимум 3 приложения и 10 App ID в неделю. Для постоянной установки и TestFlight нужен платный аккаунт.

## App Store / TestFlight

1. **Apple Developer Program** — $99/год, нужен Mac или CI с macOS (есть).
2. **Bundle ID:** заменить `com.example.orbit` на свой в `ios/project.yml` и зарегистрировать в [developer.apple.com](https://developer.apple.com).
3. **Сертификат и профиль:** проще всего через `fastlane match` (хранит сертификаты в приватном репозитории) или Xcode Cloud.
4. **Секреты для CI:**
   - `APP_STORE_CONNECT_API_KEY` (файл `.p8`, issuer id, key id)
   - сертификат распределения + provisioning profile (base64)
5. **Сборка:**

   ```bash
   cd ios && xcodegen generate
   xcodebuild -project Orbit.xcodeproj -scheme Orbit \
     -sdk iphoneos -configuration Release \
     -archivePath build/Orbit.xcarchive archive
   xcodebuild -exportArchive -archivePath build/Orbit.xcarchive \
     -exportPath build -exportOptionsPlist ExportOptions.plist
   ```

6. **Загрузка:** `xcrun altool --upload-app` или `fastlane pilot upload` → билд появляется в TestFlight.

## Что стоит сделать до релиза

- Release-подпись и `isMinifyEnabled = true` (ProGuard уже подключён)
- Иконки: сейчас адаптивная иконка нарисована вектором; для сторов нужны 1024×1024 PNG (Android) и AppIcon.appiconset (iOS)
- Скриншоты и описание в сторах
- Политика конфиденциальности (приложение ничего не собирает — можно указать это)
- Проверить геймплей на реальных устройствах: баланс скорости и спавна подбирался расчётом
