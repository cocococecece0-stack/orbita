import './theme/theme.css';
import { GameLoop } from './core/GameLoop';
import { Rng } from './core/Rng';
import type { Mode } from './core/Config';
import { Renderer } from './render/Renderer';
import { EntityRenderer } from './render/EntityRenderer';
import { Game } from './game/Game';
import { AudioManager, type SfxName } from './audio/Audio';
import { haptic, type HapticPreset } from './platform/Haptics';
import { Save, SKINS, TRAILS } from './save/Save';
import { UI } from './ui/UI';

const canvas = document.getElementById('game') as HTMLCanvasElement | null;
const uiRoot = document.getElementById('ui') as HTMLDivElement | null;
if (!canvas || !uiRoot) throw new Error('Не найдены #game или #ui');

const save = new Save();
const data = save.load();
const renderer = new Renderer(canvas);
const entities = new EntityRenderer(renderer.getGl());
const audio = new AudioManager();
const game = new Game(save.get().best);
const ui = new UI(uiRoot);

let audioReady = false;
let diveTimer = 0;

function applyTheme(): void {
  const s = save.get().settings;
  document.documentElement.dataset['palette'] = s.palette;
  document.documentElement.lang = s.locale;
  document.documentElement.style.setProperty('--fx-intensity', String(s.fx));
  document.documentElement.style.setProperty('--grain', s.reducedMotion ? '0.01' : '0.04');
  const skin = SKINS.find((x) => x.id === save.get().selectedSkin) ?? SKINS[0];
  const gl = (skin as { gl: readonly number[] }).gl;
  if (skin) {
    document.documentElement.style.setProperty('--c-accent', skin.accent);
    document.documentElement.style.setProperty('--gl-acc', `${gl[0]}, ${gl[1]}, ${gl[2]}`);
  }
  renderer.refreshTheme();
  game.setAccent?.(gl[0] ?? 1, gl[1] ?? 0.706, gl[2] ?? 0.329);
}
applyTheme();
ui.setMenuBest(data.best);
ui.syncSettings(save.get());

function insets(): { top: number; right: number; bottom: number; left: number } {
  const cs = getComputedStyle(document.documentElement);
  const n = (name: string): number => parseFloat(cs.getPropertyValue(name)) || 0;
  return { top: n('--safe-top'), right: n('--safe-right'), bottom: n('--safe-bottom'), left: n('--safe-left') };
}

function resize(): void {
  const i = insets();
  renderer.resize(window.innerWidth, window.innerHeight, i);
  game.resize(window.innerWidth, window.innerHeight, i.top, i.bottom);
}
window.addEventListener('resize', resize, { passive: true });
window.addEventListener('orientationchange', resize, { passive: true });
resize();

async function ensureAudio(): Promise<void> {
  if (audioReady) return;
  audioReady = true;
  try {
    await audio.init();
    audio.setMuted(!save.get().settings.sound);
    if (save.get().settings.music) audio.startAmbient();
  } catch (error) {
    console.warn('Аудио недоступно:', error);
  }
}

function sfx(name: SfxName, pitch = 1): void {
  if (!save.get().settings.sound) return;
  void ensureAudio().then(() => audio.play(name, pitch));
}

function hapticSafe(preset: HapticPreset): void {
  if (save.get().settings.haptics) void haptic(preset);
}

game.events.on('orb', (mult) => { sfx('orb', 0.92 + mult * 0.09); hapticSafe('light'); });
game.events.on('nearMiss', () => { sfx('dodge', 1.18); hapticSafe('medium'); });
game.events.on('shieldOn', () => { sfx('shield', 0.9); hapticSafe('medium'); });
game.events.on('shieldBreak', () => { sfx('shield', 0.7); hapticSafe('heavy'); });
game.events.on('slowOn', () => { sfx('powerup', 0.85); hapticSafe('medium'); });
game.events.on('levelUp', (level) => { sfx('level', 0.95 + level * 0.01); hapticSafe('success'); });
game.events.on('feverOn', () => { sfx('powerup', 1.25); hapticSafe('success'); });
game.events.on('newBest', () => { sfx('level', 1.3); hapticSafe('success'); });
game.events.on('death', () => { sfx('crash'); hapticSafe('heavy'); });

function seedFor(mode: Mode): number {
  return mode === 'daily' ? Rng.dailySeedUTC() : Rng.runSeed();
}

function startRun(): void {
  const mode = ui.getMode();
  void ensureAudio();
  game.startRun(seedFor(mode), mode);
  ui.show('playing');
}

function toMenu(): void {
  game.toMenu();
  ui.setMenuBest(save.get().best);
  ui.show('menu');
}

ui.on('play', startRun);
ui.on('again', startRun);
ui.on('pause', () => { game.pause(); ui.show('paused'); });
ui.on('resume', () => { game.resume(); ui.show('playing'); });
ui.on('pause-menu', toMenu);
ui.on('over-menu', toMenu);
ui.on('to-stats', () => ui.showStats(save.get()));
ui.on('stats-back', toMenu);
ui.on('to-custom', () => ui.showCustom(save.get()));
ui.on('custom-back', toMenu);
ui.on('to-settings', () => { ui.syncSettings(save.get()); ui.show('settings'); });
ui.on('settings-back', toMenu);
ui.on('sound', () => {
  save.get().settings.sound = !save.get().settings.sound;
  audio.setMuted(!save.get().settings.sound);
  save.save();
  ui.syncSettings(save.get());
});

ui.onInput('set-sound', (v) => { save.get().settings.sound = v === 'true'; audio.setMuted(v !== 'true'); save.save(); });
ui.onInput('set-music', (v) => { save.get().settings.music = v === 'true'; save.save(); });
ui.onInput('set-haptics', (v) => { save.get().settings.haptics = v === 'true'; save.save(); });
ui.onInput('set-lowfps', (v) => { save.get().settings.lowFps = v === 'true'; loop.setFpsCap(v === 'true' ? 30 : 0); save.save(); });
ui.onInput('set-fx', (v) => { save.get().settings.fx = Number(v); applyTheme(); save.save(); });
ui.onInput('set-palette', (v) => {
  save.get().settings.palette = v as 'default' | 'protan' | 'deutan' | 'tritan';
  applyTheme(); save.save();
});
ui.onInput('set-locale', (v) => { save.setLocale(v as 'ru' | 'en'); applyTheme(); });
ui.on('export-code', () => {
  const code = save.exportCode();
  ui.setTransferCode(code);
  const clip = navigator.clipboard;
  if (!clip) { ui.toast('КОД В ПОЛЕ ВВОДА'); return; }
  void clip.writeText(code).then(
    () => ui.toast('КОД СКОПИРОВАН'),
    () => ui.toast('КОД В ПОЛЕ ВВОДА'),
  ).catch(() => ui.toast('КОД В ПОЛЕ ВВОДА'));
});
ui.on('import-code', () => {
  if (save.importCode(ui.getTransferCode())) {
    applyTheme();
    game.setBest(save.get().best);
    ui.syncSettings(save.get());
    ui.setMenuBest(save.get().best);
    ui.toast('СОХРАНЕНИЕ ВОССТАНОВЛЕНО');
  } else {
    ui.toast('НЕВЕРНЫЙ КОД');
  }
});

ui.root.addEventListener('shop', (event) => {
  const detail = (event as CustomEvent<{ skin?: string; trail?: string; cost: number }>).detail;
  if (detail.skin) {
    const item = SKINS.find((s) => s.id === detail.skin);
    if (!item) return;
    if (!save.get().unlockedSkins.includes(item.id)) {
      if (!save.buySkin(item.id, detail.cost)) { ui.toast('НЕДОСТАТОЧНО ЖЕТОНОВ'); return; }
      ui.toast(`ОТКРЫТО: ${item.name}`);
    }
    save.selectSkin(item.id);
    applyTheme();
  }
  if (detail.trail) {
    const item = TRAILS.find((s) => s.id === detail.trail);
    if (!item) return;
    if (!save.get().unlockedTrails.includes(item.id)) {
      if (!save.buyTrail(item.id, detail.cost)) { ui.toast('НЕДОСТАТОЧНО ЖЕТОНОВ'); return; }
      ui.toast(`ОТКРЫТО: ${item.name}`);
    }
    save.selectTrail(item.id);
  }
  ui.showCustom(save.get());
});

canvas.addEventListener('pointerdown', (e: PointerEvent) => {
  e.preventDefault();
  void ensureAudio();
  const screen = game.getScreen();
  if (screen === 'menu') { startRun(); return; }
  if (screen === 'gameover') { startRun(); return; }
  if (screen === 'paused') { game.resume(); ui.show('playing'); return; }
  game.tap();
  sfx('tap');
  diveTimer = window.setTimeout(() => game.setDiving(true), 140);
}, { passive: false });

canvas.addEventListener('pointerup', () => {
  if (diveTimer) { clearTimeout(diveTimer); diveTimer = 0; }
  game.setDiving(false);
}, { passive: true });
canvas.addEventListener('pointercancel', () => {
  if (diveTimer) { clearTimeout(diveTimer); diveTimer = 0; }
  game.setDiving(false);
}, { passive: true });

let wasPlaying = false;
const loop = new GameLoop({
  update: (dt: number): void => {
    game.update(dt);
    const s = game.getSnapshot();
    if (s.screen === 'playing') wasPlaying = true;
    if (s.screen === 'gameover' && wasPlaying) {
      wasPlaying = false;
      const before = save.get().runs;
      const earned = save.recordRun(
        s.score, s.orbs, s.elapsed, s.combo, s.mode, s.isNewBest,
        seedFor(s.mode),
        { score: s.score, orbs: s.orbs, combo: s.combo, elapsed: s.elapsed,
          nearMisses: s.nearMisses, level: s.level, mode: s.mode, runs: before + 1,
          isNewBest: s.isNewBest },
      );
      game.setBest(save.get().best);
      ui.showGameOver(s, save.get());
      for (const a of earned) ui.toast(`ДОСТИЖЕНИЕ: ${a}`);
    }
  },
  render: (_alpha: number, elapsed: number): void => {
    renderer.draw({ time: elapsed, ...game.getPlanetVisual() });
    const view = renderer.getView();
    const n = game.fillInstances(entities.bufferData, entities.capacity);
    const v = game.getPlanetVisual();
    entities.draw(n, view.width, view.height,
      Math.sin(elapsed * 71) * v.shake, Math.cos(elapsed * 83) * v.shake);
    ui.update(game.getSnapshot());
  },
});
renderer.attach(loop);
if (save.get().settings.lowFps) loop.setFpsCap(30);
loop.start();

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    loop.stop();
    audio.suspend();
    if (game.getScreen() === 'playing') game.pause();
  } else {
    loop.start();
    void ensureAudio().then(() => audio.resume());
  }
});

(window as unknown as { __orbita: unknown }).__orbita = {
  export: (): string => save.exportCode(),
  import: (code: string): boolean => save.importCode(code),
  rng: (seed: number): Rng => new Rng(seed),
};
