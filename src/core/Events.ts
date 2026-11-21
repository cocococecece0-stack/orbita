export type GameEvent =
  | 'orb' | 'nearMiss' | 'shieldOn' | 'shieldBreak' | 'slowOn'
  | 'levelUp' | 'feverOn' | 'feverOff' | 'death' | 'newBest' | 'comboUp';

type Handler = (payload: number) => void;

/** Typed event bus без аллокаций в тике: слушатели регистрируются один раз. */
export class EventBus {
  private readonly map = new Map<GameEvent, Handler[]>();

  on(event: GameEvent, handler: Handler): void {
    const list = this.map.get(event);
    if (list) list.push(handler);
    else this.map.set(event, [handler]);
  }

  emit(event: GameEvent, payload = 0): void {
    const list = this.map.get(event);
    if (!list) return;
    for (let i = 0; i < list.length; i++) {
      const h = list[i];
      if (h) h(payload);
    }
  }
}
