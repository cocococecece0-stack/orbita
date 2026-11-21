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
import { copyText, initLifecycle, readSafeArea } from './platform/Bridge';

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
const reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');

let audioReady = false;
let diveTimer = 0;

function applyTheme(): void {
  const s = save.get().settings;
  document.documentElement.dataset['palette'] = s.palette;
  document.documentElement.dataset['scene'] = s.theme;
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
  game.setAccent(gl[0] ?? 1, gl[1] ?? 0.706, gl[2] ?? 0.329);
  game.setReducedMotion(s.reducedMotion || reducedMotionQuery.matches);
  game.setFxScale(s.fx);
}
applyTheme();
reducedMotionQuery.addEventListener('change', () => applyTheme());
ui.setMenuBest(data.best);
ui.syncSettings(save.get());
{
  const ghost = save.get().ghost;
  if (ghost.length > 1) game.setBestGhost(Float32Array.from(ghost), ghost.length);
  ui.refreshProgress(save.get());
}

function resize(): void {
  const i = readSafeArea();
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
    audio.setMusicEnabled(save.get().settings.music);
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
game.events.on('feverOn', () => { sfx('powerup', 1.25); hapticSafe('pulse'); });
game.events.on('newBest', () => { sfx('level', 1.3); hapticSafe('record'); });
game.events.on('death', () => { sfx('crash'); audio.duck(); hapticSafe('heavy'); });
game.events.on('magnet', () => { sfx('powerup', 1.1); hapticSafe('medium'); ui.toast('МАГНИТ'); });
game.events.on('double', () => { sfx('powerup', 1.35); hapticSafe('success'); ui.toast('ДВОЙНЫЕ ОЧКИ'); });
game.events.on('phase', () => { sfx('shield', 1.2); hapticSafe('medium'); ui.toast('ФАЗА'); });
game.events.on('freeze', () => { sfx('powerup', 0.7); hapticSafe('medium'); ui.toast('ЗАМОРОЗКА'); });
game.events.on('secondChance', () => { sfx('shield', 1.4); hapticSafe('record'); ui.toast('ВТОРОЙ ШАНС'); });
game.events.on('bossSpawn', () => { sfx('storm', 0.85); hapticSafe('heavy'); ui.toast('БОСС'); });
game.events.on('bossHit', (hp) => { sfx('orb', 1.5); hapticSafe('medium'); ui.toast(`ЯДРО ${hp}`); });
game.events.on('bossDown', () => { sfx('level', 1.5); hapticSafe('record'); ui.toast('БОСС УНИЧТОЖЕН'); });
game.events.on('stormWarn', () => { sfx('storm'); hapticSafe('medium'); ui.toast('МЕТЕОРИТНЫЙ ДОЖДЬ'); });
game.events.on('wormholeWarn', () => { sfx('shield', 0.8); hapticSafe('light'); ui.toast('ЧЕРВОТОЧИНЫ ОТКРЫТЫ'); });
game.events.on('wormhole', () => { sfx('powerup', 0.7); hapticSafe('pulse'); ui.toast('ЧЕРВОТОЧИНА'); });

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
ui.onInput('set-music', (v) => { save.get().settings.music = v === 'true'; audio.setMusicEnabled(v === 'true'); save.save(); });
ui.onInput('set-haptics', (v) => { save.get().settings.haptics = v === 'true'; save.save(); });
ui.onInput('set-reduced', (v) => { save.get().settings.reducedMotion = v === 'true'; applyTheme(); save.save(); });
ui.onInput('set-lowfps', (v) => { save.get().settings.lowFps = v === 'true'; loop.setFpsCap(v === 'true' ? 30 : 0); save.save(); });
ui.onInput('set-fx', (v) => { save.get().settings.fx = Number(v); applyTheme(); save.save(); });
ui.onInput('set-theme', (v) => { save.setTheme(v as 'observatory' | 'neon' | 'sunset'); applyTheme(); });
ui.onInput('set-palette', (v) => {
  save.get().settings.palette = v as 'default' | 'protan' | 'deutan' | 'tritan';
  applyTheme(); save.save();
});
ui.onInput('set-locale', (v) => { save.setLocale(v as 'ru' | 'en'); applyTheme(); });
ui.onClaim((kind, index) => {
  const ok = kind === 'daily' ? save.claimDaily(index) : save.claimSeason(index);
  ui.toast(ok ? 'НАГРАДА ПОЛУЧЕНА' : 'НАГРАДА НЕДОСТУПНА');
  ui.refreshProgress(save.get());
});

ui.on('export-code', () => {
  const code = save.exportCode();
  ui.setTransferCode(code);
  void copyText(code).then((ok) => ui.toast(ok ? 'КОД СКОПИРОВАН' : 'КОД В ПОЛЕ ВВОДА'));
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

// Клавиатура: те же действия, что и тап, без изменения правил симуляции.
window.addEventListener('keydown', (event) => {
  const target = event.target;
  if (target instanceof HTMLButtonElement || target instanceof HTMLInputElement
      || target instanceof HTMLSelectElement || target instanceof HTMLTextAreaElement) return;
  if (event.repeat) return;
  const code = event.code;
  if (code === 'Space' || code === 'Enter' || code === 'ArrowLeft'
      || code === 'ArrowRight' || code === 'Escape' || code === 'KeyP') event.preventDefault();
  const screen = game.getScreen();
  if (code === 'ArrowLeft') { game.setOrbit(0); return; }
  if (code === 'ArrowRight') { game.setOrbit(1); return; }
  if (code === 'Space' || code === 'Enter') {
    if (screen === 'menu' || screen === 'gameover') startRun();
    else if (screen === 'paused') { game.resume(); ui.show('playing'); }
    else { game.tap(); sfx('tap'); }
    return;
  }
  if (code === 'Escape' || code === 'KeyP') {
    if (screen === 'playing') { game.pause(); ui.show('paused'); }
    else if (screen === 'paused') { game.resume(); ui.show('playing'); }
  }
});

let wasPlaying = false;
const loop = new GameLoop({
  update: (dt: number): void => {
    game.update(dt);
    const s = game.getSnapshot();
    audio.update(s.level, s.feverLeft > 0, s.screen === 'playing');
    if (s.screen === 'playing') wasPlaying = true;
    if (s.screen === 'gameover' && wasPlaying) {
      wasPlaying = false;
      const before = save.get().runs;
      const outcome = save.recordRun({
        score: s.score, orbs: s.orbs, sec: s.elapsed, combo: s.combo,
        mode: s.mode, isNewBest: s.isNewBest, seed: seedFor(s.mode),
        context: { score: s.score, orbs: s.orbs, combo: s.combo, elapsed: s.elapsed,
          nearMisses: s.nearMisses, level: s.level, mode: s.mode, runs: before + 1,
          isNewBest: s.isNewBest, bossKills: s.bossKills, powerupsTaken: s.powerupsTaken,
          wormholesUsed: s.wormholesUsed },
        ghost: game.getCurrentGhost(),
      });
      game.setBest(save.get().best);
      ui.showGameOver(s, save.get());
      ui.refreshProgress(save.get());
      for (const a of outcome.achievements) ui.toast(`ДОСТИЖЕНИЕ: ${a}`);
      for (const m of outcome.missions) ui.toast(`ЗАДАНИЕ: ${m.title} · +${m.reward}`);
      for (const i of outcome.seasonTiers) ui.toast(`СЕЗОННЫЙ УРОВЕНЬ ${i + 1}`);
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

function pauseEngine(): void {
  loop.stop();
  audio.suspend();
  if (game.getScreen() === 'playing') game.pause();
}

function resumeEngine(): void {
  loop.start();
  void ensureAudio().then(() => audio.resume());
}

function onBack(): void {
  const screen = game.getScreen();
  if (screen === 'playing') { game.pause(); ui.show('paused'); }
  else if (screen === 'paused' || screen === 'gameover') toMenu();
  else if (screen !== 'menu') toMenu();
}

document.addEventListener('visibilitychange', () => {
  if (document.hidden) pauseEngine();
  else resumeEngine();
});
window.addEventListener('pagehide', pauseEngine);
window.addEventListener('pageshow', resumeEngine);
void initLifecycle({ onPause: pauseEngine, onResume: resumeEngine, onBack });

(window as unknown as { __orbita: unknown }).__orbita = {
  export: (): string => save.exportCode(),
  import: (code: string): boolean => save.importCode(code),
  rng: (seed: number): Rng => new Rng(seed),
};
