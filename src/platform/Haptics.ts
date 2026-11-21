import { Haptics, ImpactStyle, NotificationType } from '@capacitor/haptics';

export type HapticPreset = 'light' | 'medium' | 'heavy' | 'success' | 'fail' | 'pulse' | 'record';

/** Одна карта на iOS (CoreHaptics) и Android (VibrationEffect), web — vibrate-фолбэк. */
export async function haptic(preset: HapticPreset): Promise<void> {
  try {
    switch (preset) {
      case 'light': await Haptics.impact({ style: ImpactStyle.Light }); return;
      case 'medium': await Haptics.impact({ style: ImpactStyle.Medium }); return;
      case 'heavy': await Haptics.impact({ style: ImpactStyle.Heavy }); return;
      case 'success': await Haptics.notification({ type: NotificationType.Success }); return;
      case 'fail': await Haptics.notification({ type: NotificationType.Warning }); return;
      case 'pulse':
        await Haptics.impact({ style: ImpactStyle.Light });
        await Haptics.impact({ style: ImpactStyle.Medium });
        await Haptics.impact({ style: ImpactStyle.Heavy });
        return;
      case 'record':
        await Haptics.notification({ type: NotificationType.Success });
        await Haptics.vibrate({ duration: 60 });
        return;
    }
  } catch {
    const patterns: Record<HapticPreset, number | number[]> = {
      light: 12, medium: 22, heavy: 42, success: [18, 40, 30],
      fail: [50, 40, 50], pulse: [12, 30, 18, 30, 26], record: [20, 30, 20, 30, 70],
    };
    if ('vibrate' in navigator) navigator.vibrate(patterns[preset]);
  }
}
