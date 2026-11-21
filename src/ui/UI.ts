import type { GameSnapshot } from '../game/Game';
import type { Mode } from '../core/Config';
import type { SaveDataV2 } from '../save/Save';
import { ACHIEVEMENTS } from '../game/Achievements';
import { SKINS, THEMES, TRAILS } from '../save/Save';
import { missionsFor } from '../game/Daily';
import { currentSeason, SEASON_TIERS } from '../game/Seasons';

const T = {
  play: 'НАЖМИ, ЧТОБЫ НАЧАТЬ', title: 'ОРБИТА', cassette: 'КАССЕТА 01',
  classic: 'КЛАССИКА', zen: 'ДЗЕН', hardcore: 'ХАРДКОР', daily: 'ДЕЙЛИ',
  score: 'СЧЁТ', best: 'РЕКОРД', level: 'УРОВЕНЬ', shield: 'ЩИТ', slow: 'ЗАМЕДЛЕНИЕ',
  fever: 'ПЕРЕГРУЗ', pause: 'ПАУЗА', resume: 'ПРОДОЛЖИТЬ', restart: 'ЕЩЁ РАЗ',
  menu: 'В МЕНЮ', end: 'СЕАНС ЗАВЕРШЁН', record: 'НОВЫЙ РЕКОРД',
  orbs: 'СФЕРЫ', near: 'БЛИЗКО', combo: 'КОМБО', time: 'ВРЕМЯ',
  stats: 'СТАТИСТИКА', custom: 'АНГАР', settings: 'НАСТРОЙКИ',
};

export class UI {
  readonly root: HTMLElement;
  private screens: Record<string, HTMLDivElement> = {};
  private current = '';
  private mode: Mode = 'classic';
  private toastHost: HTMLDivElement;

  constructor(parent: HTMLElement) {
    this.root = parent;
    this.toastHost = document.createElement('div');
    this.toastHost.className = 'toast-host';
    this.toastHost.setAttribute('role', 'status');
    this.toastHost.setAttribute('aria-live', 'polite');

    this.screens.menu = this.div('screen menu');
    this.screens.menu.innerHTML = `
      <div class="meta"><span>ARCHIVE // 001</span><span class="blink">REC ●</span></div>
      <div class="menu-body">
        <div class="title-wrap"><h1>${T.title}</h1><div class="subtitle">${T.cassette}</div></div>
        <div class="modes">
          <button class="mode active" data-mode="classic">01&nbsp;&nbsp;${T.classic}</button>
          <button class="mode" data-mode="zen">02&nbsp;&nbsp;${T.zen}</button>
          <button class="mode" data-mode="hardcore">03&nbsp;&nbsp;${T.hardcore}</button>
          <button class="mode" data-mode="daily">04&nbsp;&nbsp;${T.daily}</button>
        </div>
        <div class="menu-best" id="menu-best">BEST 0000</div>
        <div class="meta-strip" id="daily-strip"></div>
        <div class="meta-strip" id="season-strip"></div>
      </div>
      <button class="cta" id="play">${T.play}</button>
      <div class="nav"><button id="to-stats">${T.stats}</button><button id="to-custom">${T.custom}</button><button id="to-settings">${T.settings}</button></div>`;

    this.screens.hud = this.div('screen hud hidden');
    this.screens.hud.innerHTML = `
      <div class="hud-score" id="hud-score">00000</div>
      <div class="hud-best" id="hud-best">РЕКОРД 00000</div>
      <div class="hud-level" id="hud-level">УРОВЕНЬ 01</div>
      <div class="hud-combo hidden" id="hud-combo">x1</div>
      <div class="hud-chip hidden" id="hud-shield">ЩИТ</div>
      <div class="hud-chip hidden" id="hud-slow">ЗАМЕДЛЕНИЕ</div>
      <div class="hud-fever hidden" id="hud-fever">ПЕРЕГРУЗ</div>
      <div class="hud-effects hidden" id="hud-effects"></div>
      <div class="hud-boss hidden" id="hud-boss">БОСС</div>
      <div class="hud-storm hidden" id="hud-storm">МЕТЕОРИТНЫЙ ДОЖДЬ</div>
      <div class="hud-controls"><button class="icon-btn" id="sound" aria-label="Звук">♪</button><button class="icon-btn" id="pause" aria-label="Пауза">II</button></div>`;

    this.screens.paused = this.div('screen modal hidden');
    this.screens.paused.innerHTML = `<div class="panel"><div class="kicker">${T.pause}</div>
      <button class="cta small" id="resume">${T.resume}</button>
      <button class="ghost" id="pause-menu">${T.menu}</button></div>`;

    this.screens.over = this.div('screen over hidden');
    this.screens.over.innerHTML = `
      <div class="kicker">${T.end}</div>
      <div class="over-head"><div class="over-score" id="over-score">00000</div><div class="stamp hidden" id="stamp">НОВЫЙ РЕКОРД</div></div>
      <div class="over-stats" id="over-stats"></div>
      <div class="over-actions"><button class="cta small" id="again">${T.restart}</button><button class="ghost" id="over-menu">${T.menu}</button></div>`;

    this.screens.stats = this.div('screen sub hidden');
    this.screens.stats.innerHTML = `<div class="kicker">${T.stats}</div><div class="grid-stats" id="stats-grid"></div>
      <div class="grid-ach" id="ach-grid"></div><div class="season-panel" id="season-panel"></div><button class="ghost" id="stats-back">${T.menu}</button>`;

    this.screens.custom = this.div('screen sub hidden');
    this.screens.custom.innerHTML = `<div class="kicker">${T.custom}</div><div class="wallet" id="wallet">ЖЕТОНЫ 0</div>
      <div class="shop-label">ПЛАНЕТА</div><div class="shop-list" id="skin-list"></div>
      <div class="shop-label">ШЛЕЙФ</div><div class="shop-list" id="trail-list"></div>
      <button class="ghost" id="custom-back">${T.menu}</button>`;

    this.screens.settings = this.div('screen sub hidden');
    this.screens.settings.innerHTML = `<div class="kicker">${T.settings}</div>
      <div class="settings-list">
        <label>ЗВУК <input type="checkbox" id="set-sound" checked></label>
        <label>МУЗЫКА <input type="checkbox" id="set-music" checked></label>
        <label>ХАПТИКА <input type="checkbox" id="set-haptics" checked></label>
        <label>ЭФФЕКТЫ <input type="range" id="set-fx" min="0" max="1" step="0.05" value="1"></label>
        <label> ТЕМА <select id="set-theme"></select></label>
        <label>ПАЛИТРА <select id="set-palette"><option value="default">ОСНОВНАЯ</option><option value="protan">ПРОТАН</option><option value="deutan">ДЕЙТАН</option><option value="tritan">ТРИТАН</option></select></label>
        <label>ЯЗЫК <select id="set-locale"><option value="ru">RU</option><option value="en">EN</option></select></label>
        <label>УМЕНЬШИТЬ АНИМАЦИЮ <input type="checkbox" id="set-reduced"></label>
        <label>ЭКОНОМИЯ БАТАРЕИ <input type="checkbox" id="set-lowfps"></label>
      </div>
      <div class="shop-label">ПЕРЕНОС СОХРАНЕНИЯ</div>
      <textarea id="transfer-code" class="transfer-code" rows="3" spellcheck="false" placeholder="ORB1-..."></textarea>
      <div class="nav"><button id="export-code">ЭКСПОРТ</button><button id="import-code">ИМПОРТ</button></div>
      <button class="ghost" id="settings-back" style="margin-top:16px">${T.menu}</button>`;

    for (const [name, el] of Object.entries(this.screens)) this.root.appendChild(el);
    this.root.appendChild(this.toastHost);
    this.bindModes();
    this.bindClaims();
    this.populateThemes();
  }

  private div(cls: string): HTMLDivElement {
    const el = document.createElement('div');
    el.className = cls;
    return el;
  }

  private q<T extends Element = HTMLElement>(id: string): T | null {
    return this.root.querySelector<T>(`#${id}`);
  }

  on(id: string, handler: () => void): void {
    const el = this.q(id);
    if (el) el.addEventListener('click', (e) => { e.preventDefault(); handler(); });
  }

  onInput(id: string, handler: (value: string) => void): void {
    const el = this.q<HTMLInputElement | HTMLSelectElement>(id);
    if (!el) return;
    el.addEventListener('change', () => {
      const value = el instanceof HTMLInputElement && el.type === 'checkbox'
        ? String(el.checked)
        : el.value;
      handler(value);
    });
    if (el instanceof HTMLInputElement && el.type === 'range') {
      el.addEventListener('input', () => handler(el.value));
    }
  }

  getMode(): Mode { return this.mode; }

  private bindClaims(): void {
    this.root.addEventListener('click', (event) => {
      const target = event.target as HTMLElement | null;
      const button = target?.closest<HTMLButtonElement>('[data-claim]');
      if (!button) return;
      const raw = button.dataset['claim'] ?? '';
      const [kind, index] = raw.split(':');
      if (!kind || index === undefined) return;
      this.root.dispatchEvent(new CustomEvent('claim', {
        detail: { kind, index: Number(index) }, bubbles: true,
      }));
    });
  }

  private populateThemes(): void {
    const el = this.q<HTMLSelectElement>('set-theme');
    if (!el) return;
    el.innerHTML = THEMES.map((t) => `<option value="${t.id}">${t.name}</option>`).join('');
  }

  onClaim(handler: (kind: 'daily' | 'season', index: number) => void): void {
    this.root.addEventListener('claim', (event) => {
      const detail = (event as CustomEvent<{ kind: string; index: number }>).detail;
      if (detail.kind === 'daily' || detail.kind === 'season') handler(detail.kind, detail.index);
    });
  }

  private bindModes(): void {
    const buttons = this.root.querySelectorAll<HTMLButtonElement>('.mode[data-mode]');
    buttons.forEach((b) => {
      b.addEventListener('click', () => {
        this.mode = (b.dataset['mode'] ?? 'classic') as Mode;
        buttons.forEach((x) => x.classList.toggle('active', x === b));
      });
    });
  }

  show(screen: string): void {
    if (screen === this.current) return;
    this.current = screen;
    for (const [name, el] of Object.entries(this.screens)) {
      el.classList.toggle('hidden', name !== screen);
    }
  }

  setMenuBest(best: number): void {
    const el = this.q('menu-best');
    if (el) el.textContent = `BEST ${String(best).padStart(4, '0')}`;
  }

  /** Перерисовка заданий и сезона без смены экрана. */
  refreshProgress(save: SaveDataV2): void {
    this.setMenuBest(save.best);
    const daily = this.q('daily-strip');
    if (daily) {
      const missions = missionsFor(save.daily.date);
      daily.innerHTML = `<span class="strip-title">ЗАДАНИЯ ${save.daily.date}</span>` + missions.map((m, i) => {
        const value = Math.min(m.goal, save.daily.progress[i] ?? 0);
        const done = value >= m.goal;
        const claimed = save.daily.claimed[i] === true;
        return `<span class="strip-item">${m.title} ${value}/${m.goal} · ${m.reward} жет.${done && !claimed ? `<button data-claim="daily:${i}">ЗАБРАТЬ</button>` : ''}</span>`;
      }).join('');
    }
    const season = this.q('season-strip');
    if (season) {
      const info = currentSeason();
      const next = SEASON_TIERS.find((t) => save.season.xp < t.xp);
      season.innerHTML = `<span class="strip-title">${info.name}</span><span class="strip-item">XP ${save.season.xp}${next ? ` · ДО ${next.title}: ${next.xp - save.season.xp}` : ' · ВСЕ УРОВНИ ОТКРЫТЫ'}</span>`;
    }
    this.drawSeasonPanel(save);
  }

  private drawSeasonPanel(save: SaveDataV2): void {
    const host = this.q('season-panel');
    if (!host) return;
    const info = currentSeason();
    host.innerHTML = `<div class="season-title">${info.name} · XP ${save.season.xp}</div>` + SEASON_TIERS.map((t, i) => {
      const done = save.season.xp >= t.xp;
      const claimed = save.season.claimed[i] === true;
      return `<div class="season-tier"><span>${t.title} · ${t.xp} XP · ${t.reward} жет.</span>${done && !claimed ? `<button data-claim="season:${i}">ЗАБРАТЬ</button>` : `<b>${claimed ? 'ПОЛУЧЕНО' : done ? 'ДОСТУПНО' : 'ЗАКРЫТО'}</b>`}</div>`;
    }).join('');
  }

  update(s: GameSnapshot): void {
    this.show(s.screen);
    if (s.screen !== 'playing' && s.screen !== 'paused') return;
    const set = (id: string, text: string): void => {
      const el = this.q(id);
      if (el) el.textContent = text;
    };
    set('hud-score', String(s.score).padStart(5, '0'));
    set('hud-best', `${T.best} ${String(s.best).padStart(5, '0')}`);
    set('hud-level', `${T.level} ${String(s.level).padStart(2, '0')}`);
    const combo = this.q('hud-combo');
    if (combo) {
      combo.classList.toggle('hidden', s.mult <= 1);
      combo.textContent = `x${s.mult}`;
      combo.style.transform = `scale(${1 + Math.min(0.22, s.mult * 0.03)})`;
    }
    this.q('hud-shield')?.classList.toggle('hidden', s.shields === 0);
    this.q('hud-slow')?.classList.toggle('hidden', s.slowLeft <= 0);
    const fever = this.q('hud-fever');
    if (fever) {
      fever.classList.toggle('hidden', s.feverLeft <= 0);
      if (s.feverLeft > 0) fever.textContent = `${T.fever} ${s.feverLeft.toFixed(1)}`;
    }
    const names: Array<[number, string]> = [
      [1, 'ЩИТ'], [2, 'ЗАМЕДЛЕНИЕ'], [4, 'МАГНИТ'], [8, 'x2'],
      [16, 'ФАЗА'], [32, 'ЗАМОРОЗКА'], [64, '2-Й ШАНС'],
    ];
    const active = names.filter(([bit]) => (s.effects & bit) !== 0).map(([, name]) => name);
    const effects = this.q('hud-effects');
    if (effects) {
      effects.classList.toggle('hidden', active.length === 0);
      effects.textContent = active.join(' · ');
    }
    const boss = this.q('hud-boss');
    if (boss) {
      boss.classList.toggle('hidden', s.bossHp <= 0);
      if (s.bossHp > 0) boss.textContent = `БОСС ${'◆'.repeat(s.bossHp)}  ЗАРЯД ${Math.min(3, s.bossCharge)}/3`;
    }
    this.q('hud-storm')?.classList.toggle('hidden', s.stormWarn <= 0);
  }

  showGameOver(s: GameSnapshot, save: SaveDataV2): void {
    const score = this.q('over-score');
    if (score) score.textContent = String(s.score).padStart(5, '0');
    this.q('stamp')?.classList.toggle('hidden', !s.isNewBest);
    const avg = save.runs > 0 ? Math.round(save.totalScore / save.runs) : 0;
    const host = this.q('over-stats');
    if (host) {
      host.innerHTML = `
        <div><span>${T.orbs}</span><b>${s.orbs}</b></div>
        <div><span>${T.near}</span><b>${s.nearMisses}</b></div>
        <div><span>${T.combo}</span><b>x${save.maxCombo}</b></div>
        <div><span>${T.time}</span><b>${s.elapsed.toFixed(1)}с</b></div>
        <div><span>${T.best}</span><b>${save.best}</b></div>
        <div><span>СРЕДНЕЕ</span><b>${avg}</b></div>`;
    }
    this.show('gameover');
  }

  showStats(save: SaveDataV2): void {
    const avg = save.runs > 0 ? Math.round(save.totalScore / save.runs) : 0;
    const g = this.q('stats-grid');
    if (g) {
      g.innerHTML = `
        <div><span>ЗАБЕГИ</span><b>${save.runs}</b></div>
        <div><span>ЛУЧШИЙ</span><b>${save.best}</b></div>
        <div><span>СРЕДНИЙ</span><b>${avg}</b></div>
        <div><span>ВСЕГО ОЧКОВ</span><b>${save.totalScore}</b></div>
        <div><span>СФЕРЫ</span><b>${save.totalOrbs}</b></div>
        <div><span>МАКС КОМБО</span><b>x${save.maxCombo}</b></div>
        <div><span>ВРЕМЯ</span><b>${Math.floor(save.totalPlaySec / 60)}м</b></div>
        <div><span>ЖЕТОНЫ</span><b>${save.currency}</b></div>
        <div><span>КЛАССИКА</span><b>${save.bestByMode['classic'] ?? 0}</b></div>
        <div><span>ДЗЕН</span><b>${save.bestByMode['zen'] ?? 0}</b></div>
        <div><span>ХАРДКОР</span><b>${save.bestByMode['hardcore'] ?? 0}</b></div>
        <div><span>ДЕЙЛИ</span><b>${save.bestByMode['daily'] ?? 0}</b></div>`;
    }
    const ach = this.q('ach-grid');
    if (ach) {
      ach.innerHTML = ACHIEVEMENTS.map((a) => {
        const done = save.achievements.includes(a.id);
        return `<div class="ach ${done ? 'done' : ''}"><span>${a.icon}</span><b>${a.title}</b><small>${a.desc}</small></div>`;
      }).join('');
    }
    this.drawSeasonPanel(save);
    this.show('stats');
  }

  showCustom(save: SaveDataV2): void {
    const wallet = this.q('wallet');
    if (wallet) wallet.textContent = `ЖЕТОНЫ ${save.currency}`;
    const skinHost = this.q('skin-list');
    if (skinHost) {
      skinHost.innerHTML = SKINS.map((s) => {
        const owned = save.unlockedSkins.includes(s.id);
        const active = save.selectedSkin === s.id;
        return `<button class="shop-item ${active ? 'active' : ''}" data-skin="${s.id}" data-cost="${s.cost}">
          <span class="swatch" style="background:${s.accent}"></span><b>${s.name}</b>
          <small>${active ? 'ВЫБРАНО' : owned ? 'ВЫБРАТЬ' : `${s.cost} жет.`}</small></button>`;
      }).join('');
      skinHost.querySelectorAll<HTMLButtonElement>('button').forEach((b) => {
        b.addEventListener('click', () => this.onShop(b));
      });
    }
    const trailHost = this.q('trail-list');
    if (trailHost) {
      trailHost.innerHTML = TRAILS.map((s) => {
        const owned = save.unlockedTrails.includes(s.id);
        const active = save.selectedTrail === s.id;
        return `<button class="shop-item ${active ? 'active' : ''}" data-trail="${s.id}" data-cost="${s.cost}">
          <b>${s.name}</b><small>${active ? 'ВЫБРАНО' : owned ? 'ВЫБРАТЬ' : `${s.cost} жет.`}</small></button>`;
      }).join('');
      trailHost.querySelectorAll<HTMLButtonElement>('button').forEach((b) => {
        b.addEventListener('click', () => this.onShop(b));
      });
    }
    this.show('custom');
  }

  private onShop(button: HTMLButtonElement): void {
    const cost = Number(button.dataset['cost'] ?? '0');
    const skin = button.dataset['skin'];
    const trail = button.dataset['trail'];
    this.root.dispatchEvent(new CustomEvent('shop', { detail: { skin, trail, cost } }));
  }

  syncSettings(save: SaveDataV2): void {
    const s = save.settings;
    const check = (id: string, v: boolean): void => {
      const el = this.q<HTMLInputElement>(id);
      if (el) el.checked = v;
    };
    check('set-sound', s.sound);
    check('set-music', s.music);
    check('set-haptics', s.haptics);
    check('set-lowfps', s.lowFps);
    check('set-reduced', s.reducedMotion);
    const fx = this.q<HTMLInputElement>('set-fx');
    if (fx) fx.value = String(s.fx);
    const pal = this.q<HTMLSelectElement>('set-palette');
    if (pal) pal.value = s.palette;
    const loc = this.q<HTMLSelectElement>('set-locale');
    if (loc) loc.value = s.locale;
    const theme = this.q<HTMLSelectElement>('set-theme');
    if (theme) theme.value = s.theme;
  }

  setTransferCode(code: string): void {
    const el = this.q<HTMLTextAreaElement>('transfer-code');
    if (el) el.value = code;
  }

  getTransferCode(): string {
    return this.q<HTMLTextAreaElement>('transfer-code')?.value.trim() ?? '';
  }

  toast(text: string): void {
    const el = document.createElement('div');
    el.className = 'toast';
    el.textContent = text;
    this.toastHost.appendChild(el);
    window.setTimeout(() => el.remove(), 2400);
  }
}
