/** Тонкий мост Capacitor: lifecycle, clipboard и safe area. Игровая логика здесь отсутствует. */
import { App as CapApp } from '@capacitor/app';
import { Clipboard as CapClipboard } from '@capacitor/clipboard';
import { Capacitor } from '@capacitor/core';

export interface SafeInsets {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

export interface LifecycleHooks {
  onPause: () => void;
  onResume: () => void;
  onBack?: () => void;
}

/** CSS env() остаётся единственным источником safe-area; JS только читает результат. */
export function readSafeArea(): SafeInsets {
  const cs = getComputedStyle(document.documentElement);
  const n = (name: string): number => parseFloat(cs.getPropertyValue(name)) || 0;
  return {
    top: n('--safe-top'),
    right: n('--safe-right'),
    bottom: n('--safe-bottom'),
    left: n('--safe-left'),
  };
}

export async function initLifecycle(hooks: LifecycleHooks): Promise<() => void> {
  if (!Capacitor.isNativePlatform()) return () => undefined;
  const handles = [
    await CapApp.addListener('pause', hooks.onPause),
    await CapApp.addListener('resume', hooks.onResume),
    await CapApp.addListener('appStateChange', (state) => {
      if (state.isActive) hooks.onResume();
      else hooks.onPause();
    }),
  ];
  if (hooks.onBack) {
    handles.push(await CapApp.addListener('backButton', hooks.onBack));
  }
  return () => {
    for (const handle of handles) void handle.remove();
  };
}

export async function copyText(text: string): Promise<boolean> {
  try {
    if (Capacitor.isNativePlatform()) {
      await CapClipboard.write({ string: text });
      return true;
    }
  } catch {
    //_web-фолбэк ниже
  }
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // Код остаётся в поле ввода — пользователь скопирует его вручную.
  }
  return false;
}
