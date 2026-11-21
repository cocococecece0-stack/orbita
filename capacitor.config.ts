import type { CapacitorConfig } from '@capacitor/cli';

// Bundle ID и имя приложения НЕ меняются — legacy-совместимость сохранена.
const config: CapacitorConfig = {
  appId: 'com.example.orbit',
  appName: 'Orbit',
  webDir: 'dist',
  server: { androidScheme: 'https' },
};

export default config;
