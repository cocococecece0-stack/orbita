import type { GameSnapshot } from '../game/Game';
import type { SaveDataV2 } from '../save/Save';

const RU = {
  play: 'НАЖМИ, ЧТОБЫ НАЧАТЬ', menuTitle: 'ОРБИТА', cassette: 'КАССЕТА 01',
  classic: 'КЛАССИКА', score: 'СЧЁТ', best: 'РЕКОРД', level: 'УРОВЕНЬ',
  shield: 'ЩИТ', slow: 'ЗАМЕДЛЕНИЕ', fever: 'ПЕРЕГРУЗ', pause: 'ПАУЗА',
  resume: 'ПРОДОЛЖИТЬ', restart: 'ЕЩЁ РАЗ', toMenu: 'В МЕНЮ',
  sessionEnd: 'СЕАНС ЗАВЕРШЁН', newRecord: 'НОВЫЙ РЕКОРД',
  orbs: 'СФЕРЫ', near: 'БЛИЗКО', combo: 'КОМБО', time: 'ВРЕМЯ',
};

export class UI {
  readonly root: HTMLElement;
  private menu: HTMLDivElement;
  private hud: HTMLDivElement;
  private pause: HTMLDivElement;
  private over: HTMLDivElement;
  private elScore: HTMLDivElement;
  private elBest: HTMLDivElement;
  private elLevel: HTMLDivElement;
  private elCombo: HTMLDivElement;
  private elShield: HTMLDivElement;
  private elSlow: HTMLDivElement;
  private elFever: HTMLDivElement;
  private overScore: HTMLDivElement;
  private overStats: HTMLDivElement;
  private stamp: HTMLDivElement;
  private current = '';

  constructor(parent: HTMLElement) {
    this.root = parent;
    this.menu = this.div('screen menu');
    this.hud = this.div('screen hud hidden');
    this.pause = this.div('screen modal hidden');
    this.over = this.div('screen over hidden');

    this.menu.innerHTML = `
      <div class="meta"><span>ARCHIVE // 001</span><span class="blink">REC ●</span></div>
      <div class="menu-body">
        <div class="title-wrap"><h1>${RU.menuTitle}</h1><div class="subtitle">${RU.cassette}</div></div>
        <div class="modes"><div class="mode active">01&nbsp;&nbsp;${RU.classic}&nbsp;&nbsp;<b>▮</b></div></div>
        <div class="menu-best" id="ui-menu-best">BEST 0000</div>
      </div>
      <button class="cta" id="ui-play">${RU.play}</button>
      <div class="footer"><span>ЛЕНТА 1 / 4</span><span>GRAIN 0.04</span></div>`;

    this.elScore = this.div('hud-score');
    this.elBest = this.div('hud-best');
    this.elLevel = this.div('hud-level');
    this.elCombo = this.div('hud-combo hidden');
    this.elShield = this.div('hud-chip hidden');
    this.elSlow = this.div('hud-chip hidden');
    this.elFever = this.div('hud-fever hidden');
    const controls = this.div('hud-controls');
    controls.innerHTML = `<button id="ui-sound" class="icon-btn" aria-label="звук">♪</button><button id="ui-pause" class="icon-btn" aria-label="пауза">II</button>`;

    this.hud.append(this.elScore, this.elBest, this.elLevel, this.elCombo,
      this.elShield, this.elSlow, this.elFever, controls);

    this.pause.innerHTML = `
      <div class="panel"><div class="kicker">${RU.pause}</div>
      <button class="cta small" id="ui-resume">${RU.resume}</button>
      <button class="ghost" id="ui-pause-menu">${RU.toMenu}</button></div>`;

    this.overScore = this.div('over-score');
    this.overStats = this.div('over-stats');
    this.stamp = this.div('stamp hidden');
    this.over.innerHTML = `
      <div class="kicker">${RU.sessionEnd}</div>
      <div class="over-head">${this.overScore}${this.stamp}</div>
      <div class="over-stats-host"></div>
      <div class="over-actions">
        <button class="cta small" id="ui-again">${RU.restart}</button>
        <button class="ghost" id="ui-over-menu">${RU.toMenu}</button>
      </div>`;
    const host = this.over.querySelector('.over-stats-host');
    if (host) host.appendChild(this.overStats);

    this.root.append(this.menu, this.hud, this.pause, this.over);
  }

  private div(cls: string): HTMLDivElement {
    const el = document.createElement('div');
    el.className = cls;
    return el;
  }

  on(id: string, handler: () => void): void {
    const el = this.root.querySelector(`#${id}`);
    if (el) el.addEventListener('click', (e) => { e.preventDefault(); handler(); });
  }

  show(screen: string): void {
    if (screen === this.current) return;
    this.current = screen;
    this.menu.classList.toggle('hidden', screen !== 'menu');
    this.hud.classList.toggle('hidden', screen !== 'playing');
    this.pause.classList.toggle('hidden', screen !== 'paused');
    this.over.classList.toggle('hidden', screen !== 'gameover');
  }

  setMenuBest(best: number): void {
    const el = this.root.querySelector('#ui-menu-best');
    if (el) el.textContent = `BEST ${String(best).padStart(4, '0')}`;
  }

  update(s: GameSnapshot): void {
    this.show(s.screen);
    if (s.screen !== 'playing' && s.screen !== 'paused') return;
    this.elScore.textContent = String(s.score).padStart(5, '0');
    this.elBest.textContent = `${RU.best} ${String(s.best).padStart(5, '0')}`;
    this.elLevel.textContent = `${RU.level} ${String(s.level).padStart(2, '0')}`;
    if (s.mult > 1) {
      this.elCombo.classList.remove('hidden');
      this.elCombo.textContent = `x${s.mult}`;
      this.elCombo.style.transform = `scale(${1 + Math.min(0.22, s.mult * 0.03)})`;
    } else {
      this.elCombo.classList.add('hidden');
    }
    this.elShield.classList.toggle('hidden', s.shields === 0);
    this.elSlow.classList.toggle('hidden', s.slowLeft <= 0);
    this.elFever.classList.toggle('hidden', s.feverLeft <= 0);
    if (s.feverLeft > 0) this.elFever.textContent = `${RU.fever} ${s.feverLeft.toFixed(1)}`;
  }

  showGameOver(s: GameSnapshot, save: SaveDataV2): void {
    this.overScore.textContent = String(s.score).padStart(5, '0');
    this.stamp.classList.toggle('hidden', !s.isNewBest);
    const avg = save.runs > 0 ? Math.round(save.totalScore / save.runs) : 0;
    this.overStats.innerHTML = `
      <div><span>${RU.orbs}</span><b>${s.orbs}</b></div>
      <div><span>${RU.near}</span><b>${s.nearMisses}</b></div>
      <div><span>${RU.combo}</span><b>x${save.maxCombo}</b></div>
      <div><span>${RU.time}</span><b>${s.elapsed.toFixed(1)}с</b></div>
      <div><span>${RU.best}</span><b>${save.best}</b></div>
      <div><span>СРЕДНЕЕ</span><b>${avg}</b></div>`;
    this.show('gameover');
  }
}
