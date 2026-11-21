import { Haptics, ImpactStyle, NotificationType } from '@capacitor/haptics';

export type HapticPreset = 'light' | 'medium' | 'heavy' | 'success' | 'fail';

/** Единые пресеты: CoreHaptics на iOS / Vibrator на Android, vibrate — web-фолбэк. */
export async function haptic(preset: HapticPreset): Promise<void> {
  try {
    switch (preset) {
      case 'light':
        await Haptics.impact({ style: ImpactStyle.Light });
        break;
      case 'medium':
        await Haptics.impact({ style: ImpactStyle.Medium });
        break;
      case 'heavy':
        await Haptics.impact({ style: ImpactStyle.Heavy });
        break;
      case 'success':
        await Haptics.notification({ type: NotificationType.Success });
        break;
      case 'fail':
        await Haptics.notification({ type: NotificationType.Warning });
        break;
    }
  } catch {
    // WebView без плагина: нативный мост недоступен, используем вибрацию браузера.
    if ('vibrate' in navigator) {
      navigator.vibrate(preset === 'heavy' ? 40 : preset === 'fail' ? 30 : 15);
    }
  }
}
