import SwiftUI
import UIKit

private enum Screen: String {
    case hub, league, missions, hangar, shop, achievements, stats, settings
}

/// Новый визуальный язык меню: уличная аркада, а не неоновый космос.
private enum UI {
    static let night = Color(hex: 0x101528)
    static let ink = Color(hex: 0x111526)
    static let paper = Color(hex: 0xFFF6E8)
    static let lime = Color(hex: 0xD8FF3E)
    static let sun = Color(hex: 0xFFC23D)
    static let coral = Color(hex: 0xFF5B3D)
    static let grape = Color(hex: 0x8B5CF6)
    static let mint = Color(hex: 0x4ADE80)
    static let muted = Color(hex: 0x5D6478)
}

struct ContentView: View {

    @StateObject private var engine = GameEngine()
    @StateObject private var sound = SoundEngine()
    @StateObject private var store = SaveStore()

    @State private var art = GameArt()
    @State private var screen: Screen? = .hub
    @State private var frame: UInt64 = 0
    @State private var toast: String?
    @State private var toastIsRun = false
    @State private var toastWork: DispatchWorkItem?
    @State private var confirmReset = false
    @State private var showAbandonConfirm = false
    @State private var exportText = ""
    @State private var importText = ""
    @State private var importResult = ""
    @State private var importSucceeded = false
    @State private var statMode = "classic"
    @State private var lastSummary: RewardSummary?
    @State private var timerSeconds = 0
    @State private var lastFrameDate = Date()

    @Environment(\.scenePhase) private var scenePhase

    private var save: SaveData { store.data }
    private var mode: Mode { Mode.from(save.selectedMode) }
    private var sector: SectorDef { Content.sector(save.selectedSector) }
    private var isModalShown: Bool { engine.paused || engine.draft != nil || engine.status == .gameOver }

    var body: some View {
        GeometryReader { geo in
            TimelineView(.animation) { timeline in
                ZStack {
                    UI.night.ignoresSafeArea()

                    if screen == nil {
                        Canvas { context, size in
                            engine.draw(context: &context, size: size, tick: Int(engine.frameTick),
                                        art: art, shipSprite: save.equippedShip,
                                        planetStyle: save.equippedPlanet, trailStyle: save.equippedTrail,
                                        colorblind: save.settings.colorblind,
                                        reducedMotion: save.settings.reducedMotion)
                        }
                        .ignoresSafeArea()
                        .contentShape(Rectangle())
                        .onTapGesture {
                            guard engine.status == .playing, !engine.paused, engine.draft == nil else { return }
                            engine.tap()
                        }
                        .onAppear { engine.resize(geo.size) }
                        .onChange(of: geo.size) { engine.resize($0) }

                        // HUD скрыт на паузе и на экране результата, чтобы чипы не перекрывали затемнение.
                        if engine.status == .playing && !engine.paused && engine.draft == nil && !showAbandonConfirm {
                            hud
                        }
                        gameOverlays
                    } else {
                        Canvas { context, size in
                            art.drawMenuBackground(in: &context, size: size,
                                                   style: save.equippedPlanet * 2,
                                                   tick: Int(frame),
                                                   reducedMotion: save.settings.reducedMotion)
                        }
                        .ignoresSafeArea()

                        Group {
                            switch screen {
                            case .hub: hubScreen
                            case .league: leagueScreen
                            case .missions: missionsScreen
                            case .hangar: hangarScreen
                            case .shop: shopScreen
                            case .achievements: achievementsScreen
                            case .stats: statsScreen
                            case .settings: settingsScreen
                            case nil: EmptyView()
                            }
                        }
                    }

                    toastView
                }
                .onChange(of: timeline.date) { date in
                    onFrame(date)
                }
            }
        }
        .dynamicTypeSize(save.settings.largeText ? .xxxLarge : .large)
        .onReceive(store.objectWillChange) { syncEngine() }
        .onAppear {
            configureEngine()
            syncEngine()
            sound.applySettings(save.settings)
        }
        .onChange(of: scenePhase) { phase in
            if phase != .active {
                // Выбор мутации уже замораживает симуляцию: не оставляем игру на паузе после выбора.
                if engine.draft == nil { engine.pause() }
                sound.pauseMusic()
            } else if screen == nil, engine.status == .playing, !engine.paused {
                sound.resumeMusic()
            }
        }
        .alert("Прервать забег и сохранить результат?", isPresented: $showAbandonConfirm) {
            Button("ПРОДОЛЖИТЬ", role: .cancel) { }
            Button("В ХАБ", role: .destructive) { abandonAndGoHub() }
        }
    }

    /// Реальное время кадра (не фиксированный 1/60), игровая логика идёт только в активном забеге.
    private func onFrame(_ date: Date) {
        let dt = min(max(date.timeIntervalSince(lastFrameDate), 0), 0.05)
        lastFrameDate = date
        if screen == nil {
            guard engine.status == .playing, !engine.paused else { return }
            engine.tick(dt: CGFloat(dt))
            let seconds = engine.timeLeft > 0 ? Int(ceil(engine.timeLeft)) : 0
            if seconds != timerSeconds { timerSeconds = seconds }
        } else {
            frame &+= 1
        }
    }

    private func syncEngine() {
        // Во время забега не перезаписываем engine.best сохранённым значением:
        // иначе переключение звука в HUD «откатывает» рекорд текущей сессии.
        if engine.status != .playing { engine.setBest(save.bestByMode[mode.id] ?? 0) }
        engine.skinColor = shipColor(save.equippedShip, save.settings.colorblind)
        engine.setReducedMotion(save.settings.reducedMotion)
        engine.setUpgrades(store.upgrades())
        engine.setSeasonModifier(Content.seasonModifier(save.seasonId).1)
        sound.applySettings(save.settings)
    }

    private func startRun() {
        let seed = mode == .daily ? Content.seedFor(save.dailyDate) : (Int(Date().timeIntervalSince1970) & 0x7fffffff)
        engine.setBest(save.bestByMode[mode.id] ?? 0)
        syncEngine()
        engine.startRun(mode, sector: save.selectedSector, seed: seed)
        timerSeconds = engine.timeLeft > 0 ? Int(ceil(engine.timeLeft)) : 0
        screen = nil
        sound.startMusic(engine.sectorId)
        haptic(.heavy)
    }

    private func goHub() {
        if engine.status == .playing {
            showAbandonConfirm = true
            return
        }
        navigateHub()
    }

    private func abandonAndGoHub() {
        // onRunEnd сам фиксирует результат прерванного забега — повторной отправки нет.
        if engine.status == .playing { engine.abandonRun() }
        navigateHub()
    }

    private func navigateHub() {
        engine.toReady()
        sound.stopMusic()
        screen = .hub
        sound.play("ui")
    }

    private func open(_ next: Screen) {
        screen = next
        sound.play("ui")
    }

    private func handle(_ event: GameEvent) {
        switch event {
        case .tap: sound.play("tap"); haptic(.light)
        case .dodge(let combo): sound.play("dodge", rate: min(1.9, 1 + Float(combo) * 0.045), volume: 0.45)
        case .orb: sound.play("orb")
        case .goldenOrb: sound.play("reward", rate: 1.2, volume: 0.7); haptic(.light)
        case .artifact: sound.play("secret"); haptic(.heavy)
        case .powerUp(let kind):
            let name = kind == .revive ? "revive" : (kind == .phase ? "phase" : "powerup")
            sound.play(name); haptic(.light)
        case .shieldBreak: sound.play("shield"); haptic(.heavy)
        case .crash: sound.play("crash"); haptic(.heavy)
        case .levelUp: sound.play("level"); haptic(.light)
        case .nearMiss: sound.play("tick", rate: 1.4, volume: 0.25); haptic(.light)
        case .storm: sound.play("storm"); haptic(.heavy)
        case .wormhole: sound.play("wormhole"); haptic(.heavy)
        case .flare: sound.play("flare")
        case .bossSpawn: sound.play("boss"); haptic(.heavy)
        case .bossHit: sound.play("boss_hit", volume: 0.6)
        case .bossDown: sound.play("boss_down"); haptic(.heavy)
        case .resonance: sound.play("reward", rate: 0.9, volume: 0.8); haptic(.heavy)
        case .finish: sound.play("victory")
        case .tick: break
        }
    }

    private func configureEngine() {
        engine.onEvent = handle
        engine.onNewRecord = { _ in sound.play("achievement") }
        engine.onRunEnd = { stats in
            let record = RunRecord(
                score: stats.score,
                mode: engine.mode.id,
                sector: engine.sectorId,
                level: stats.level,
                orbs: stats.orbs,
                maxCombo: stats.maxCombo,
                nearMisses: stats.nearMisses,
                dodges: stats.dodges,
                bossKills: stats.bossKills,
                wormholes: stats.wormholes,
                rareOrbs: stats.rareOrbs,
                seconds: stats.seconds,
                timestamp: Int64(Date().timeIntervalSince1970 * 1000)
            )
            let summary = store.submitRun(record, achieved: engine.achievedIds(), foundSecrets: engine.secretIds())
            lastSummary = summary
            // Полный состав награды показан в карточке результата; тост не нужен поверх модалки.
            guard screen == nil, !engine.paused, engine.status != .gameOver else { return }
            showToast("Награда: +\(summary.credits) жетонов", isRun: true)
        }
    }

    private func haptic(_ style: UIImpactFeedbackGenerator.FeedbackStyle) {
        guard save.settings.haptics else { return }
        let resolved: UIImpactFeedbackGenerator.FeedbackStyle
        switch save.settings.hapticProfile {
        case "SOFT": resolved = .soft
        case "STRONG": resolved = .rigid
        default: resolved = style
        }
        UIImpactFeedbackGenerator(style: resolved).impactOccurred()
    }

    // MARK: - HUD

    private var hud: some View {
        VStack(spacing: 0) {
            HStack(alignment: .top) {
                statBlock("СЧЁТ", "\(engine.score)", engine.multiplier > 1 ? "x\(engine.multiplier)" : nil, UI.lime)
                Spacer(minLength: 6)
                HStack(spacing: 8) {
                    roundButton(save.settings.muted ? "×" : "♪") {
                        store.updateSettings { $0.muted.toggle() }
                    }
                    roundButton("II") { engine.pause(); sound.pauseMusic() }
                    roundButton("⌂") { goHub() }
                }
                Spacer(minLength: 6)
                let savedBest = save.bestByMode[mode.id] ?? 0
                let liveBest = engine.status == .playing ? engine.best : 0
                statBlock("РЕКОРД", "\(max(savedBest, liveBest))", nil, UI.sun, end: true)
            }

            if engine.timeLeft > 0 {
                timerChip(timerSeconds)
                    .padding(.top, 8)
            }
            if engine.combo > 0 {
                chip("КОМБО \(engine.combo)", UI.lime)
                    .padding(.top, 8)
            }

            let objective = engine.objective()
            let reached = min(engine.objectiveValue(), objective.target)
            chip("\(objective.title) \(reached)/\(objective.target)",
                 engine.objectiveDone() ? UI.mint : UI.sun)
                .padding(.top, 6)

            bar(Float(engine.resonance), UI.lime,
                "РЕЗОНАНС \(Int(engine.resonance * 100))%")
                .padding(.top, 6)

            let chips = activeChips()
            if !chips.isEmpty {
                HStack(spacing: 5) {
                    ForEach(chips.indices, id: \.self) { i in
                        chip(chips[i].0, chips[i].1)
                    }
                }
                .padding(.top, 8)
            }
            if !engine.perkIds.isEmpty {
                HStack(spacing: 5) {
                    ForEach(engine.perkIds.indices, id: \.self) { i in
                        let perk = Content.perks[engine.perkIds[i]]
                        chip("\(perk.glyph) \(perk.title)", UI.grape)
                    }
                }
                .padding(.top, 6)
            }
            Spacer()
            if engine.stormWarning > 0 {
                banner("МЕТЕОРНЫЙ ДОЖДЬ", UI.coral)
            } else if engine.flareWarning > 0 {
                banner("СОЛНЕЧНАЯ ВСПЫШКА", UI.sun)
            } else if engine.resonanceFlash > 0 {
                banner("РЕЗОНАНС!", UI.lime, Double(engine.resonanceFlash))
            } else if engine.levelUpFlash > 0 {
                banner("УРОВЕНЬ \(engine.level)", UI.sun, Double(engine.levelUpFlash))
            }
            Spacer().frame(height: 60)
        }
        .padding(.horizontal, 16)
        .padding(.top, 8)
    }

    private func activeChips() -> [(String, Color)] {
        var out: [(String, Color)] = []
        if engine.shield { out.append(("ЩИТ", UI.mint)) }
        if engine.slowTimer > 0 { out.append(("ЗАМЕДЛЕНИЕ \(Int(ceil(engine.slowTimer)))с", UI.grape)) }
        if engine.doubleTimer > 0 { out.append(("x2 \(Int(ceil(engine.doubleTimer)))с", Palette.double)) }
        if engine.magnetTimer > 0 { out.append(("МАГНИТ \(Int(ceil(engine.magnetTimer)))с", Palette.magnet)) }
        if engine.phaseTimer > 0 { out.append(("ФАЗА \(Int(ceil(engine.phaseTimer)))с", Palette.phase)) }
        if engine.freezeTimer > 0 { out.append(("ЗАМОРОЗКА \(Int(ceil(engine.freezeTimer)))с", Palette.freeze)) }
        return out
    }

    private func statBlock(_ label: String, _ value: String, _ extra: String?, _ color: Color, end: Bool = false) -> some View {
        VStack(alignment: end ? .trailing : .leading, spacing: 2) {
            Text(label).font(.system(size: 10)).tracking(3).foregroundColor(UI.paper.opacity(0.75))
                .lineLimit(1).minimumScaleFactor(0.8)
            Text(value).font(.system(size: 30, weight: .black)).foregroundColor(UI.paper)
                .lineLimit(1).minimumScaleFactor(0.8)
            if let extra { Text(extra).font(.system(size: 12, weight: .bold)).foregroundColor(color) }
        }
    }

    private func roundButton(_ label: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(label).font(.system(size: 15, weight: .bold)).foregroundColor(UI.ink)
                .frame(width: 38, height: 38)
                .background(Circle().fill(UI.paper))
                .overlay(Circle().stroke(UI.ink, lineWidth: 2))
        }
        .buttonStyle(.plain)
    }

    private func chip(_ text: String, _ color: Color) -> some View {
        Text(text).font(.system(size: 10, weight: .black)).tracking(0.5)
            .lineLimit(1).minimumScaleFactor(0.8)
            .padding(.horizontal, 10).padding(.vertical, 5)
            .background(Capsule().fill(color))
            .foregroundColor(UI.ink)
            .overlay(Capsule().stroke(UI.ink, lineWidth: 1.5))
    }

    private func timerChip(_ seconds: Int) -> some View {
        Text("\(seconds)с")
            .font(.system(size: 10, weight: .black))
            .monospacedDigit()
            .frame(minWidth: 56)
            .padding(.horizontal, 10).padding(.vertical, 5)
            .background(Capsule().fill(UI.sun))
            .foregroundColor(UI.ink)
            .overlay(Capsule().stroke(UI.ink, lineWidth: 1.5))
    }

    private func banner(_ text: String, _ color: Color, _ alpha: Double = 1) -> some View {
        Text(text).font(.system(size: 13, weight: .bold)).tracking(4)
            .foregroundColor(color.opacity(alpha)).frame(maxWidth: .infinity)
    }

    // MARK: - Game overlays

    @ViewBuilder
    private var gameOverlays: some View {
        if let draft = engine.draft {
            modal {
                title("ВЫБЕРИ СВОЙ РИСК", UI.ink)
                bodyText("Раунд \(draft.round) · преимущество всегда имеет цену")
                Spacer().frame(height: 12)
                VStack(spacing: 9) {
                    ForEach(draft.options.indices, id: \.self) { i in
                        let perk = Content.perks[draft.options[i]]
                        Button {
                            engine.choosePerk(i)
                            sound.play("powerup")
                            sound.resumeMusic()
                            haptic(.heavy)
                        } label: {
                            HStack(spacing: 11) {
                                Text(perk.glyph).font(.system(size: 24, weight: .black))
                                    .foregroundColor(UI.ink).frame(width: 42, height: 42)
                                    .background(RoundedRectangle(cornerRadius: 12).fill(UI.sun))
                                    .overlay(RoundedRectangle(cornerRadius: 12)
                                        .stroke(UI.ink, lineWidth: 1.5))
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(perk.title).font(.system(size: 14, weight: .black))
                                        .foregroundColor(UI.ink)
                                    Text(perk.detail).font(.system(size: 11)).foregroundColor(UI.ink)
                                    Text("ЦЕНА: \(perk.risk)").font(.system(size: 10, weight: .bold))
                                        .foregroundColor(UI.coral)
                                }
                                Spacer(minLength: 0)
                                Text("▸").font(.system(size: 20, weight: .black)).foregroundColor(UI.ink)
                            }
                            .padding(13)
                            .background(RoundedRectangle(cornerRadius: 18).fill(UI.paper))
                            .overlay(RoundedRectangle(cornerRadius: 18)
                                .stroke(UI.ink, lineWidth: 2))
                        }
                        .buttonStyle(.plain)
                    }
                }
                Spacer().frame(height: 8)
                bodyText("Следующий выбор — примерно через 18 секунд полёта.", UI.muted)
            }
        } else if engine.paused {
            modal {
                title("ПАУЗА", UI.lime)
                bodyText("Полёт приостановлен")
                Spacer().frame(height: 16)
                VStack(spacing: 12) {
                    wideButton("ПРОДОЛЖИТЬ") { engine.resume(); sound.resumeMusic() }
                    outlineButton("ЗАНОВО", accent: UI.grape) { startRun() }
                    outlineButton("В ХАБ") { goHub() }
                }
            }
        } else if engine.status == .gameOver {
            modal {
                title(engine.completed ? "МАРШРУТ ЗАВЕРШЁН" : "ПОТЕРЯН СИГНАЛ",
                      engine.completed ? UI.lime : UI.coral)
                Text("\(engine.score)").font(.system(size: 56, weight: .black)).foregroundColor(UI.ink)
                if engine.isNewRecord {
                    Text("НОВЫЙ РЕКОРД").font(.system(size: 12, weight: .bold)).tracking(3).foregroundColor(UI.sun)
                } else {
                    Text("Рекорд: \(save.bestByMode[mode.id] ?? 0)").font(.system(size: 12)).foregroundColor(.gray)
                }
                Spacer().frame(height: 8)
                let objective = engine.objective()
                let reached = min(engine.objectiveValue(), objective.target)
                bodyText(engine.objectiveDone()
                         ? "ЦЕЛЬ ВЫПОЛНЕНА: \(objective.title) \(reached)/\(objective.target) · +120 ◈"
                         : "ЦЕЛЬ: \(objective.title) \(reached)/\(objective.target)",
                         engine.objectiveDone() ? UI.mint : UI.sun)
                if !engine.perkIds.isEmpty {
                    bodyText("Мутации: " + engine.perkIds.map { Content.perks[$0].title }.joined(separator: " · "),
                             UI.grape)
                }
                VStack(alignment: .leading, spacing: 3) {
                    bodyText("Уровень \(engine.level)")
                    bodyText("Сферы \(engine.orbsCollected)")
                    bodyText("Комбо x\(engine.maxCombo)")
                    bodyText("Уклонения \(engine.stats.dodges)")
                    bodyText("Боссы \(engine.stats.bossKills)")
                }
                Spacer().frame(height: 12)
                rewardCard(lastSummary)
                Spacer().frame(height: 16)
                VStack(spacing: 12) {
                    wideButton("ЕЩЁ РАЗ") { startRun() }
                    outlineButton("В ХАБ") { goHub() }
                }
            }
        }
    }

    private func rewardCard(_ summary: RewardSummary?) -> some View {
        panel {
            sectionTitle("НАГРАДА")
            let parts = rewardParts(summary ?? RewardSummary())
            if parts.isEmpty {
                bodyText("Награда не начислена", UI.sun)
            } else {
                VStack(alignment: .leading, spacing: 5) {
                    ForEach(parts, id: \.self) { part in
                        HStack(alignment: .firstTextBaseline, spacing: 6) {
                            Text("•").foregroundColor(UI.sun)
                            bodyText(part, UI.sun)
                        }
                    }
                }
            }
        }
    }

    /// Единый формат награды: жетоны → XP/уровень → достижения → секреты → задания → сезон.
    private func rewardParts(_ summary: RewardSummary) -> [String] {
        var parts: [String] = []
        if summary.credits > 0 { parts.append("+\(summary.credits) жетонов") }
        if summary.xp > 0 { parts.append("+\(summary.xp) XP") }
        if summary.levelUps > 0 { parts.append("Уровень игрока +\(summary.levelUps)") }
        if !summary.achievements.isEmpty { parts.append("Достижения: \(summary.achievements.joined(separator: ", "))") }
        if !summary.secrets.isEmpty { parts.append("Секреты: \(summary.secrets.joined(separator: ", "))") }
        if !summary.missions.isEmpty { parts.append("Задания: \(summary.missions.joined(separator: ", "))") }
        if !summary.seasonTiers.isEmpty { parts.append("Награды сезона: \(summary.seasonTiers.count)") }
        return parts.filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    }

    // MARK: - Hub

    private var hubScreen: some View {
        GeometryReader { geo in
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    HStack(spacing: 8) {
                        Text("ORBITA").font(.system(size: 28, weight: .black)).tracking(-1)
                            .foregroundColor(UI.ink).lineLimit(1).minimumScaleFactor(0.8)
                        Text("3.7").font(.system(size: 10, weight: .black)).foregroundColor(UI.paper)
                            .padding(.horizontal, 7).padding(.vertical, 3)
                            .background(RoundedRectangle(cornerRadius: 8).fill(UI.coral))
                            .overlay(RoundedRectangle(cornerRadius: 8).stroke(UI.ink, lineWidth: 1.5))
                        Spacer(minLength: 4)
                        creditsChip
                        Text("LV \(save.playerLevel)").font(.system(size: 11, weight: .black))
                            .foregroundColor(UI.lime).padding(.horizontal, 11).padding(.vertical, 6)
                            .background(Capsule().fill(UI.ink)).lineLimit(1)
                    }

                    let briefing = Content.objective(mode, sectorId: sector.id)
                    let seasonMod = Content.seasonModifier(save.seasonId)
                    ZStack(alignment: .topLeading) {
                        Canvas { context, size in
                            art.drawHubHero(in: &context, size: size, planetIndex: save.equippedPlanet,
                                            shipSprite: save.equippedShip,
                                            skinColor: shipColor(save.equippedShip, save.settings.colorblind),
                                            tick: Int(frame), colorblind: save.settings.colorblind,
                                            reducedMotion: save.settings.reducedMotion)
                        }
                        .frame(height: 206)
                        .background(UI.ink)
                        .clipShape(RoundedRectangle(cornerRadius: 24))
                        .overlay(RoundedRectangle(cornerRadius: 24).stroke(UI.lime, lineWidth: 3))

                        VStack(spacing: 0) {
                            HStack(alignment: .top) {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("НОВЫЙ РЕЙД").font(.system(size: 11, weight: .black)).tracking(2)
                                        .foregroundColor(UI.lime)
                                    Text(seasonMod.0).font(.system(size: 10)).foregroundColor(UI.paper.opacity(0.8))
                                }
                                Spacer(minLength: 8)
                                Text("СЕЗОН \(save.seasonId)").font(.system(size: 10, weight: .black))
                                    .foregroundColor(UI.paper).padding(.horizontal, 12).padding(.vertical, 6)
                                    .background(RoundedRectangle(cornerRadius: 12).fill(UI.coral))
                            }
                            Spacer(minLength: 0)
                            HStack(alignment: .bottom) {
                                VStack(alignment: .leading, spacing: 1) {
                                    Text("ЦЕЛЬ ВЫЛЕТА").font(.system(size: 9, weight: .bold)).tracking(2)
                                        .foregroundColor(UI.paper.opacity(0.7))
                                    Text(briefing.title).font(.system(size: 14, weight: .black))
                                        .foregroundColor(UI.paper)
                                    Text("\(briefing.target)").font(.system(size: 30, weight: .black))
                                        .foregroundColor(UI.lime).lineLimit(1).minimumScaleFactor(0.7)
                                }
                                Spacer(minLength: 8)
                                VStack(alignment: .trailing, spacing: 2) {
                                    Text("+120 ◈").font(.system(size: 18, weight: .black)).foregroundColor(UI.sun)
                                    Text("за выполнение цели").font(.system(size: 9))
                                        .foregroundColor(UI.paper.opacity(0.7))
                                    if save.dailyStreak > 0 {
                                        Text("🔥 \(save.dailyStreak) \(daysWord(save.dailyStreak).lowercased())")
                                            .font(.system(size: 10, weight: .bold)).foregroundColor(UI.coral)
                                    }
                                }
                            }
                        }
                        .padding(14)
                    }

                    Text("КОНТРАКТЫ ДНЯ").font(.system(size: 13, weight: .black)).tracking(1)
                        .foregroundColor(UI.ink)
                    let defs = Content.missionsFor(save.dailyDate)
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 8) {
                            ForEach(0..<defs.count, id: \.self) { i in
                                let mission = defs[i]
                                let value = min(save.dailyProgress[i], mission.goal)
                                let done = value >= mission.goal
                                Button { open(.missions) } label: {
                                    VStack(alignment: .leading, spacing: 4) {
                                        Text("КОНТРАКТ \(i + 1)").font(.system(size: 9, weight: .bold))
                                            .tracking(1).foregroundColor(UI.ink.opacity(0.6))
                                        Text(mission.title).font(.system(size: 12, weight: .black))
                                            .foregroundColor(UI.ink).lineLimit(1).minimumScaleFactor(0.8)
                                        Spacer().frame(height: 6)
                                        ZStack(alignment: .leading) {
                                            Capsule().fill(UI.ink.opacity(0.12))
                                            Capsule().fill(done ? UI.ink : UI.coral)
                                                .frame(width: 136 * CGFloat(value) / CGFloat(max(1, mission.goal)))
                                        }
                                        .frame(height: 7)
                                        Text("\(value) / \(mission.goal)   +\(mission.reward) ◈")
                                            .font(.system(size: 10, weight: .bold)).foregroundColor(UI.ink)
                                            .lineLimit(1).minimumScaleFactor(0.8)
                                    }
                                    .frame(width: 158, height: 96, alignment: .leading)
                                    .padding(11)
                                    .background(RoundedRectangle(cornerRadius: 16).fill(done ? UI.lime : UI.paper))
                                    .overlay(RoundedRectangle(cornerRadius: 16).stroke(UI.ink, lineWidth: 2))
                                }
                                .buttonStyle(.plain)
                            }
                        }
                    }

                    let nextTier = Content.seasonTiers.first { save.seasonXp < $0.xp }
                    panel {
                        Text("ЗАЧЕМ ИГРАТЬ").font(.system(size: 12, weight: .black)).tracking(1)
                            .foregroundColor(UI.ink)
                        Spacer().frame(height: 4)
                        bodyText("Выполни цель забега — +120 жетонов. Закрывай контракты, качай сезон и открывай новые планеты.", UI.ink)
                        if let tier = nextTier {
                            Spacer().frame(height: 7)
                            bar(Float(save.seasonXp) / Float(tier.xp), UI.coral,
                                "ДО СЛЕДУЮЩЕГО УРОВНЯ СЕЗОНА: \(tier.xp - save.seasonXp) XP")
                        }
                    }

                    Text("ВЫЛЕТ").font(.system(size: 12, weight: .black)).tracking(1)
                        .foregroundColor(UI.ink)
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 6) {
                            ForEach(Content.modes, id: \.id) { m in
                                choiceChip(m.shortTitle, m.id == mode.id) { store.selectMode(m.id) }
                            }
                        }
                    }
                    bodyText(mode.detail, UI.ink)
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 6) {
                            ForEach(Content.sectors, id: \.id) { s in
                                let unlocked = save.playerLevel >= s.unlockLevel || save.visitedSectors.contains(s.id)
                                choiceChip(unlocked ? s.name : "🔒 \(s.name)", s.id == sector.id,
                                           disabled: !unlocked) { store.selectSector(s.id) }
                            }
                        }
                    }
                    bodyText("\(sector.subtitle) · Рекорд \(save.bestByMode[mode.id] ?? 0)", UI.ink)

                    wideButton("ПОЕХАЛИ  ▸") { startRun() }

                    let docks: [(String, String, Screen)] = [
                        ("★", "ЛИГА", .league), ("◇", "ГАРАЖ", .hangar),
                        ("◈", "МАГАЗИН", .shop), ("✓", "ДОСТИЖ.", .achievements),
                        ("▤", "РЕКОРДЫ", .stats), ("⚙", "НАСТР.", .settings),
                    ]
                    HStack(spacing: 6) {
                        ForEach(0..<docks.count, id: \.self) { i in
                            Button { open(docks[i].2) } label: {
                                VStack(spacing: 3) {
                                    Text(docks[i].0).font(.system(size: 17, weight: .black))
                                        .foregroundColor(UI.coral)
                                    Text(docks[i].1).font(.system(size: 8, weight: .bold))
                                        .foregroundColor(UI.ink).lineLimit(1).minimumScaleFactor(0.7)
                                }
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 9)
                                .background(RoundedRectangle(cornerRadius: 14).fill(UI.paper))
                                .overlay(RoundedRectangle(cornerRadius: 14).stroke(UI.ink, lineWidth: 1.5))
                            }
                            .buttonStyle(.plain)
                        }
                    }
                    Spacer().frame(height: 20)
                }
                .padding(.horizontal, 16)
                .padding(.top, 14)
                .padding(.bottom, geo.safeAreaInsets.bottom + 16)
            }
            .scrollDismissesKeyboard(.interactively)
        }
    }

    // MARK: - League

    private var leagueScreen: some View {
        let objective = Content.objective(mode, sectorId: save.selectedSector)
        let defs = Content.missionsFor(save.dailyDate)
        let missionsDone = defs.enumerated().filter { save.dailyProgress[$0.offset] >= $0.element.goal }.count
        let nextTier = Content.seasonTiers.first { save.seasonXp < $0.xp }
        let shipsOwned = Content.shop.filter { $0.category == "ship" && save.inventory.contains($0.id) }.count
        let planetsOwned = Content.shop.filter { $0.category == "planet" && save.inventory.contains($0.id) }.count
        let trophies = (save.achievements.contains("boss_1") ? 1 : 0) +
                       (save.achievements.contains("boss_5") ? 1 : 0)
        let reasons: [(String, String, String)] = [
            ("01", "СЕЗОННАЯ ГОНКА", nextTier == nil
                ? "Все уровни сезона пройдены"
                : "\(save.seasonXp) / \(nextTier!.xp) XP · награда +\(nextTier!.reward) ◈"),
            ("02", "КОНТРАКТЫ ДНЯ", "\(missionsDone) / 3 выполнено · обновляются в 00:00 UTC"),
            ("03", "КОЛЛЕКЦИЯ", "\(shipsOwned) / 8 кораблей · \(planetsOwned) / 8 планет открыто"),
            ("04", "ОХОТА НА БОССОВ", trophies == 0
                ? "Первое ядро ещё ждёт"
                : "\(trophies) трофея в копилке"),
        ]

        return scrollScreen("ЛИГА", "Здесь собрано всё, ради чего стоит возвращаться в орбиту.") {
            VStack(alignment: .leading, spacing: 4) {
                Text("ГЛАВНАЯ ЦЕЛЬ ВЫЛЕТА").font(.system(size: 10, weight: .black)).tracking(2)
                    .foregroundColor(UI.lime)
                Text("\(objective.title): \(objective.target)").font(.system(size: 22, weight: .black))
                    .foregroundColor(UI.paper)
                bodyText("Выполнение даёт +200 очков и +120 жетонов.", UI.paper.opacity(0.85))
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(14)
            .background(RoundedRectangle(cornerRadius: 18).fill(UI.ink))
            .overlay(RoundedRectangle(cornerRadius: 18).stroke(UI.lime, lineWidth: 2))

            Spacer().frame(height: 4)
            Text("ЧЕТЫРЕ ПРИЧИНЫ ВОЗВРАЩАТЬСЯ").font(.system(size: 11, weight: .bold)).tracking(3)
                .foregroundColor(UI.ink)
            ForEach(0..<reasons.count, id: \.self) { i in
                let reason = reasons[i]
                panel {
                    HStack(spacing: 10) {
                        Text(reason.0).font(.system(size: 13, weight: .black)).foregroundColor(UI.ink)
                            .padding(.horizontal, 8).padding(.vertical, 5)
                            .background(RoundedRectangle(cornerRadius: 10).fill(i == 0 ? UI.lime : UI.sun))
                            .overlay(RoundedRectangle(cornerRadius: 10).stroke(UI.ink, lineWidth: 1.5))
                        VStack(alignment: .leading, spacing: 2) {
                            Text(reason.1).font(.system(size: 13, weight: .black)).foregroundColor(UI.ink)
                            Text(reason.2).font(.system(size: 11)).foregroundColor(UI.muted)
                        }
                        Spacer(minLength: 0)
                    }
                }
            }

            Spacer().frame(height: 4)
            Text("МУТАЦИИ: ВЫБОР С ЦЕНОЙ").font(.system(size: 11, weight: .bold)).tracking(3)
                .foregroundColor(UI.ink)
            ForEach(Content.perks, id: \.id) { perk in
                panel {
                    Text("\(perk.glyph)  \(perk.title)").font(.system(size: 13, weight: .black))
                        .foregroundColor(UI.ink)
                    Text(perk.detail).font(.system(size: 11)).foregroundColor(UI.ink)
                    Text("ЦЕНА: \(perk.risk)").font(.system(size: 10, weight: .bold))
                        .foregroundColor(UI.coral)
                }
            }
            wideButton("ВСЕ ДОСТИЖЕНИЯ  ▸", accent: UI.sun) { open(.achievements) }
        }
    }

    // MARK: - Missions

    private var missionsScreen: some View {
        scrollScreen("ЗАДАНИЯ ДНЯ", "Задания обновляются каждый день в 00:00 UTC.") {
            let defs = Content.missionsFor(save.dailyDate)
            ForEach(0..<defs.count, id: \.self) { i in
                let m = defs[i]
                let value = save.dailyProgress[i]
                let done = value >= m.goal
                panel {
                    Text(m.title).font(.system(size: 15, weight: .bold)).foregroundColor(done ? UI.lime : UI.ink)
                    Spacer().frame(height: 4)
                    bar(clamped(Float(value) / Float(m.goal)), done ? UI.lime : UI.grape,
                        "\(value) / \(m.goal)")
                    Spacer().frame(height: 8)
                    HStack {
                        Text("+\(m.reward) жетонов").font(.system(size: 12)).foregroundColor(UI.sun)
                        Spacer()
                        if save.dailyClaimed[i] {
                            Text("ПОЛУЧЕНО").font(.system(size: 11)).foregroundColor(.gray)
                        } else {
                            smallButton("ЗАБРАТЬ", done) { _ = store.claimMission(i) }
                        }
                    }
                }
            }
            sectionTitle("СЕЗОН \(save.seasonId)")
            ForEach(0..<Content.seasonTiers.count, id: \.self) { i in
                let tier = Content.seasonTiers[i]
                panel {
                    HStack {
                        Text("Уровень \(i + 1) · \(tier.xp) XP").font(.system(size: 13)).foregroundColor(UI.ink)
                        Spacer()
                        Text("+\(tier.reward) ◈").font(.system(size: 13)).foregroundColor(UI.sun)
                    }
                    bar(clamped(Float(save.seasonXp) / Float(tier.xp)), UI.grape,
                        save.seasonClaimed[i] ? "ПОЛУЧЕНО" : "Сезон \(save.seasonXp) / \(tier.xp) XP")
                }
            }
            bodyText("Серия заданий: \(save.dailyStreak) \(daysWord(save.dailyStreak).lowercased()) · Дзен-забегов: \(save.zenRuns)")
        }
    }

    // MARK: - Hangar

    private var hangarScreen: some View {
        scrollScreen("АНГАР", "Все предметы хранятся локально.") {
            ZStack {
                Canvas { context, size in
                    art.drawPlanetPreview(in: &context, index: save.equippedPlanet,
                                          center: CGPoint(x: size.width / 2, y: size.height / 2),
                                          radius: 64, tick: Int(frame),
                                          colorblind: save.settings.colorblind,
                                          reducedMotion: save.settings.reducedMotion)
                }
                if let image = UIImage(named: "ship_\(save.equippedShip)") {
                    Image(uiImage: image).resizable().frame(width: 96, height: 96)
                }
            }
            .frame(height: 170)

            sectionTitle("КОРПУСА")
            let ships = Content.shop.filter { $0.category == "ship" }
            ForEach(0..<ships.count/2, id: \.self) { i in
                HStack(spacing: 8) {
                    shopTile(ships[i*2])
                    shopTile(ships[i*2+1])
                }
            }
            sectionTitle("ШЛЕЙФЫ")
            HStack(spacing: 6) {
                ForEach(Content.shop.filter { $0.category == "trail" }, id: \.id) { item in
                    let owned = save.inventory.contains(item.id)
                    choiceChip(owned ? item.title : "🔒 \(item.title)", save.equippedTrail == item.value) {
                        if owned { store.equip(item.id) } else { _ = store.buy(item.id) }
                    }
                }
            }
            sectionTitle("ПЛАНЕТЫ")
            HStack(spacing: 6) {
                ForEach(Content.shop.filter { $0.category == "planet" }, id: \.id) { item in
                    let owned = save.inventory.contains(item.id)
                    choiceChip(owned ? item.title : "🔒 \(item.title)", save.equippedPlanet == item.value) {
                        if owned { store.equip(item.id) } else { _ = store.buy(item.id) }
                    }
                }
            }
            sectionTitle("УЛУЧШЕНИЯ")
            ForEach(Content.shop.filter { $0.category == "upgrade" }, id: \.id) { item in
                let owned = save.inventory.contains(item.id)
                panel {
                    HStack {
                        VStack(alignment: .leading) {
                            Text(item.title).font(.system(size: 13, weight: .bold)).foregroundColor(UI.ink)
                            bodyText(item.detail)
                        }
                        Spacer()
                        smallButton(owned ? "АКТИВНО" : "\(item.price) ◈", !owned) {
                            if !owned { _ = store.buy(item.id) }
                        }
                    }
                }
            }
        }
    }

    // MARK: - Shop

    private var shopScreen: some View {
        scrollScreen("МАГАЗИН", "Баланс: ◆ \(compactCredits(save.credits))") {
            let cats: [(String, String)] = [("ship", "КОРАБЛИ"), ("trail", "ШЛЕЙФЫ"),
                                            ("planet", "ФОНЫ"), ("upgrade", "УЛУЧШЕНИЯ")]
            ForEach(cats.indices, id: \.self) { ci in
                let cat = cats[ci].0
                sectionTitle(cats[ci].1)
                ForEach(Content.shop.filter { $0.category == cat }, id: \.id) { item in
                    let owned = save.inventory.contains(item.id)
                    panel {
                        HStack {
                            VStack(alignment: .leading) {
                                Text(item.title).font(.system(size: 14, weight: .bold)).foregroundColor(UI.ink)
                                bodyText(item.detail)
                            }
                            Spacer()
                            if owned && item.category != "upgrade" {
                                let equipped = (item.category == "ship" && save.equippedShip == item.value) ||
                                    (item.category == "trail" && save.equippedTrail == item.value) ||
                                    (item.category == "planet" && save.equippedPlanet == item.value)
                                smallButton(equipped ? "НОСИМ" : "НАДЕТЬ", !equipped) { _ = store.equip(item.id) }
                            } else if owned {
                                Text("КУПЛЕНО").font(.system(size: 10)).foregroundColor(.gray)
                            } else {
                                smallButton("\(item.price) ◈", save.credits >= item.price) { _ = store.buy(item.id) }
                            }
                        }
                    }
                }
            }
        }
    }

    // MARK: - Achievements

    private var achievementsScreen: some View {
        scrollScreen("ДОСТИЖЕНИЯ", "Открыто \(save.achievements.count) из \(Content.achievements.count).") {
            ForEach(Content.achievements, id: \.id) { a in
                let open = save.achievements.contains(a.id)
                panel {
                    HStack {
                        Text(open ? "★" : "☆").font(.system(size: 18)).foregroundColor(open ? UI.sun : .gray)
                        VStack(alignment: .leading) {
                            Text(a.title).font(.system(size: 13, weight: .bold))
                                .foregroundColor(open ? UI.ink : UI.muted)
                            bodyText(a.detail)
                        }
                        Spacer()
                        Text("+\(a.reward) ◈").font(.system(size: 11)).foregroundColor(open ? UI.sun : .gray)
                    }
                }
            }
            sectionTitle("СЕКРЕТЫ")
            ForEach(Content.secrets, id: \.id) { s in
                let open = save.secrets.contains(s.id)
                panel {
                    Text(open ? s.title : "???").font(.system(size: 13, weight: .bold))
                        .foregroundColor(open ? UI.grape : .gray)
                    bodyText(s.hint)
                }
            }
        }
    }

    // MARK: - Stats

    private var statsScreen: some View {
        scrollScreen("СТАТИСТИКА", "Рекорды хранятся только на этом устройстве.") {
            HStack(spacing: 8) {
                metric("ЗАБЕГОВ", "\(save.history.count)")
                metric("ОЧКОВ", "\(save.history.reduce(0) { $0 + $1.score })")
                metric("СФЕР", "\(save.history.reduce(0) { $0 + $1.orbs })")
            }
            HStack(spacing: 8) {
                metric("БОССОВ", "\(save.history.reduce(0) { $0 + $1.bossKills })")
                metric("ПОРТАЛОВ", "\(save.history.reduce(0) { $0 + $1.wormholes })")
                metric("УРОВЕНЬ", "\(save.playerLevel)")
            }
            sectionTitle("ТАБЛИЦА РЕКОРДОВ")
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 8) {
                    ForEach(Content.modes, id: \.id) { m in
                        choiceChip(m.shortTitle, statMode == m.id) { statMode = m.id }
                    }
                }
                .padding(.horizontal, 20)
            }
            .padding(.horizontal, -20)
            panel {
                let top = save.topByMode[statMode] ?? []
                if top.isEmpty { bodyText("В этом режиме ещё нет забегов.") }
                else {
                    ForEach(0..<top.count, id: \.self) { i in
                        Text("\(i + 1).  \(top[i])").font(.system(size: 14, weight: i == 0 ? .bold : .regular))
                            .foregroundColor(i == 0 ? UI.sun : UI.ink)
                    }
                }
            }
            outlineButton("СКОПИРОВАТЬ КОД РЕКОРДОВ", accent: UI.grape) {
                UIPasteboard.general.string = store.exportCode()
                showToast("Код скопирован")
            }
            sectionTitle("ИСТОРИЯ ЗАБЕГОВ")
            ForEach(save.history.prefix(12), id: \.timestamp) { r in
                panel {
                    HStack {
                        Text("\(Mode.from(r.mode).title) · \(Content.sector(r.sector).name)")
                            .font(.system(size: 12)).foregroundColor(UI.ink)
                        Spacer()
                        Text("\(r.score)").font(.system(size: 14, weight: .bold)).foregroundColor(UI.lime)
                    }
                    bodyText("Ур. \(r.level) · Сферы \(r.orbs) · Комбо x\(r.maxCombo) · \(r.seconds)с")
                }
            }
        }
    }

    // MARK: - Settings

    private var settingsScreen: some View {
        scrollScreen("НАСТРОЙКИ", "Прогресс сохраняется автоматически после каждого действия.") {
            HStack(spacing: 12) {
                Canvas { context, size in
                    art.drawPlanetPreview(in: &context, index: save.equippedPlanet,
                                          center: CGPoint(x: size.width / 2, y: size.height / 2),
                                          radius: 28, tick: Int(frame),
                                          colorblind: save.settings.colorblind,
                                          reducedMotion: save.settings.reducedMotion)
                }
                .frame(width: 56, height: 56)
                VStack(alignment: .leading, spacing: 2) {
                    Text("ТЕКУЩАЯ ПЛАНЕТА").font(.system(size: 10, weight: .bold)).tracking(2)
                        .foregroundColor(UI.muted)
                    Text(Content.shop.first { $0.category == "planet" && $0.value == save.equippedPlanet }?.title ?? "ЗЕМЛЯ")
                        .font(.system(size: 13, weight: .bold)).foregroundColor(UI.ink)
                }
                Spacer()
            }

            sectionTitle("ЗВУК")
            panel {
                Text("Музыка \(Int(save.settings.music * 100))%").font(.system(size: 12)).foregroundColor(UI.ink)
                Slider(value: Binding(get: { save.settings.music },
                                      set: { v in store.updateSettings { $0.music = v } }), in: 0...1)
                Text("Эффекты \(Int(save.settings.sfx * 100))%").font(.system(size: 12)).foregroundColor(UI.ink)
                Slider(value: Binding(get: { save.settings.sfx },
                                      set: { v in store.updateSettings { $0.sfx = v } }), in: 0...1)
                toggleRow("Полное выключение звука", Binding(get: { save.settings.muted },
                                                             set: { v in store.updateSettings { $0.muted = v } }))
                toggleRow("Вибрация", Binding(get: { save.settings.haptics },
                                              set: { v in store.updateSettings { $0.haptics = v } }))
            }
            sectionTitle("ПРОФИЛИ")
            panel {
                Text("ЗВУК").font(.system(size: 9, weight: .bold)).tracking(2).foregroundColor(.gray)
                HStack(spacing: 6) {
                    ForEach(Array([("BALANCED", "БАЛАНС"), ("NIGHT", "НОЧЬ"), ("BASS", "БАС")].enumerated()), id: \.offset) { _, item in
                        choiceChip(item.1, save.settings.audioProfile == item.0) {
                            store.updateSettings { $0.audioProfile = item.0 }
                        }
                    }
                }
                Text("ВИБРАЦИЯ").font(.system(size: 9, weight: .bold)).tracking(2).foregroundColor(.gray)
                    .padding(.top, 6)
                HStack(spacing: 6) {
                    ForEach(Array([("CLASSIC", "КЛАССИКА"), ("SOFT", "МЯГКАЯ"), ("STRONG", "СИЛЬНАЯ")].enumerated()), id: \.offset) { _, item in
                        choiceChip(item.1, save.settings.hapticProfile == item.0) {
                            store.updateSettings { $0.hapticProfile = item.0 }
                        }
                    }
                }
            }
            sectionTitle("ДОСТУПНОСТЬ")
            panel {
                toggleRow("Уменьшить анимацию и тряску", Binding(get: { save.settings.reducedMotion },
                                                                 set: { v in store.updateSettings { $0.reducedMotion = v } }))
                toggleRow("Контрастные цвета", Binding(get: { save.settings.colorblind },
                                                       set: { v in store.updateSettings { $0.colorblind = v } }))
                toggleRow("Крупный текст", Binding(get: { save.settings.largeText },
                                                   set: { v in store.updateSettings { $0.largeText = v } }))
            }
            sectionTitle("СОХРАНЕНИЕ")
            panel {
                bodyText("Код экспорта содержит контроль целостности. Импорт заменяет текущий прогресс.")
                outlineButton("ЭКСПОРТИРОВАТЬ КОД", accent: UI.grape) { exportText = store.exportCode() }
                if !exportText.isEmpty {
                    Spacer().frame(height: 10)
                    Text("КОД ЭКСПОРТА").font(.system(size: 10, weight: .bold)).tracking(2)
                        .foregroundColor(UI.muted)
                    HStack(spacing: 8) {
                        TextField("", text: $exportText)
                            .textFieldStyle(.roundedBorder)
                            .font(.system(size: 10, design: .monospaced))
                            .autocorrectionDisabled()
                            .textInputAutocapitalization(.never)
                            .disabled(true)
                        smallButton("КОПИРОВАТЬ") {
                            UIPasteboard.general.string = exportText
                            showToast("Код скопирован")
                        }
                    }
                }
                Spacer().frame(height: 10)
                Text("КОД ИМПОРТА").font(.system(size: 10, weight: .bold)).tracking(2)
                    .foregroundColor(UI.muted)
                TextField("ORB1-...", text: $importText)
                    .textFieldStyle(.roundedBorder)
                    .font(.system(size: 12, design: .monospaced))
                    .autocorrectionDisabled()
                    .textInputAutocapitalization(.never)
                wideButton("ИМПОРТИРОВАТЬ", accent: UI.grape) {
                    importSucceeded = store.importCode(importText)
                    importResult = importSucceeded ? "Прогресс восстановлен" : "Код недействителен"
                    importText = ""
                }
                if !importResult.isEmpty {
                    bodyText(importResult, importSucceeded ? UI.lime : UI.coral)
                }
            }
            sectionTitle("ОПАСНАЯ ЗОНА")
            VStack(alignment: .leading, spacing: 10) {
                HStack(spacing: 8) {
                    Image(systemName: "exclamationmark.triangle.fill").foregroundColor(UI.coral)
                    Text("ОПАСНАЯ ЗОНА").font(.system(size: 12, weight: .bold)).tracking(2)
                        .foregroundColor(UI.coral)
                }
                bodyText("Сброс удалит все рекорды, предметы и достижения без возможности восстановления.",
                         UI.coral.opacity(0.9))
                if !confirmReset {
                    outlineButton("СБРОСИТЬ ПРОГРЕСС", accent: UI.coral) { confirmReset = true }
                } else {
                    bodyText("Точно удалить все рекорды и предметы?", UI.coral)
                    HStack(spacing: 12) {
                        wideButton("ДА, СБРОСИТЬ", accent: UI.coral) {
                            store.reset()
                            confirmReset = false
                        }
                        outlineButton("ОТМЕНА", accent: .gray) { confirmReset = false }
                    }
                }
            }
            .padding(14)
            .background(RoundedRectangle(cornerRadius: 16).fill(Color(hex: 0x2A0A18).opacity(0.98)))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(UI.coral.opacity(0.4), lineWidth: 1))
            bodyText("Орбита 3.7 · Локальная игра без рекламы и сети")
        }
    }

    // MARK: - Reusable UI

    private func scrollScreen<Content: View>(_ title: String, _ subtitle: String,
                                             @ViewBuilder content: @escaping () -> Content) -> some View {
        GeometryReader { geo in
            ScrollView {
                VStack(alignment: .leading, spacing: 12) {
                    HStack(alignment: .center, spacing: 8) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(title).font(.system(size: 22, weight: .black)).tracking(-0.5)
                                .foregroundColor(UI.ink)
                                .lineLimit(2).minimumScaleFactor(0.8)
                                .fixedSize(horizontal: false, vertical: true)
                            creditsChip
                        }
                        Spacer(minLength: 6)
                        smallButton("← ХАБ", true) { goHub() }.fixedSize()
                    }
                    bodyText(subtitle)
                    content()
                    Spacer().frame(height: 24)
                }
                .padding(.horizontal, 20).padding(.top, 18)
                .padding(.bottom, geo.safeAreaInsets.bottom + 16)
            }
            .scrollDismissesKeyboard(.interactively)
        }
    }

    private var creditsChip: some View {
        HStack(spacing: 4) {
            Text("◆").font(.system(size: 11, weight: .black)).foregroundColor(UI.ink)
            Text(compactCredits(save.credits)).font(.system(size: 12, weight: .black))
                .monospacedDigit().lineLimit(1).minimumScaleFactor(0.8)
        }
        .foregroundColor(UI.ink)
        .padding(.horizontal, 10).padding(.vertical, 6)
        .background(Capsule().fill(UI.sun))
        .overlay(Capsule().stroke(UI.ink, lineWidth: 1.5))
        .fixedSize()
    }

    private func sectionTitle(_ text: String) -> some View {
        Text(text).font(.system(size: 12, weight: .black)).tracking(1)
            .foregroundColor(UI.ink)
    }

    private func bodyText(_ text: String, _ color: Color = UI.muted) -> some View {
        Text(text).font(.system(size: 12)).foregroundColor(color).lineSpacing(4)
    }

    private func panel<Content: View>(@ViewBuilder content: @escaping () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 18).fill(UI.paper))
        .overlay(RoundedRectangle(cornerRadius: 18).stroke(UI.ink, lineWidth: 2))
    }

    private func modal<Content: View>(@ViewBuilder content: @escaping () -> Content) -> some View {
        GeometryReader { geo in
            ZStack {
                UI.night.opacity(0.88).ignoresSafeArea()
                ScrollView(.vertical) {
                    VStack(spacing: 12) {
                        content()
                    }
                    .frame(maxWidth: 380)
                    .padding(22)
                    .background(RoundedRectangle(cornerRadius: 26).fill(UI.paper))
                    .overlay(RoundedRectangle(cornerRadius: 26).stroke(UI.ink, lineWidth: 3))
                    .padding(.horizontal, 16)
                    .padding(.top, geo.safeAreaInsets.top + 20)
                    .padding(.bottom, geo.safeAreaInsets.bottom + 16)
                    .frame(maxWidth: .infinity, minHeight: geo.size.height, alignment: .center)
                }
                .scrollDismissesKeyboard(.interactively)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    @ViewBuilder
    private var toastView: some View {
        if let toast, !(toastIsRun && (screen != nil || isModalShown)) {
            VStack {
                Spacer()
                Text(toast)
                    .font(.system(size: 12, weight: .black))
                    .multilineTextAlignment(.center)
                    .foregroundColor(UI.ink)
                    .padding(.horizontal, 18).padding(.vertical, 12)
                    .background(RoundedRectangle(cornerRadius: 16).fill(UI.sun))
                    .overlay(RoundedRectangle(cornerRadius: 16).stroke(UI.ink, lineWidth: 2))
                    .padding(.bottom, 20)
                    .transition(.opacity)
            }
            .animation(.easeInOut, value: toast)
        }
    }

    private func showToast(_ text: String, isRun: Bool = false) {
        toastWork?.cancel()
        toast = text
        toastIsRun = isRun
        let work = DispatchWorkItem {
            toast = nil
            toastIsRun = false
            toastWork = nil
        }
        toastWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 4.2, execute: work)
    }

    private func title(_ text: String, _ color: Color) -> some View {
        Text(text).font(.system(size: 22, weight: .black)).tracking(0.5).foregroundColor(color)
            .multilineTextAlignment(.center)
            .minimumScaleFactor(0.8)
    }

    private func wideButton(_ text: String, accent: Color = UI.lime,
                            _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(text).font(.system(size: 15, weight: .black)).tracking(1)
                .lineLimit(1).minimumScaleFactor(0.8)
                .frame(maxWidth: .infinity).padding(.vertical, 15)
                .background(RoundedRectangle(cornerRadius: 16).fill(accent))
                .overlay(RoundedRectangle(cornerRadius: 16).stroke(UI.ink, lineWidth: 2))
                .foregroundColor(UI.ink)
        }
        .buttonStyle(.plain)
    }

    private func outlineButton(_ text: String, accent: Color = UI.lime,
                               _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(text).font(.system(size: 13, weight: .black)).tracking(1)
                .lineLimit(1).minimumScaleFactor(0.8)
                .frame(maxWidth: .infinity).padding(.vertical, 13)
                .foregroundColor(UI.ink)
                .background(RoundedRectangle(cornerRadius: 14).fill(UI.paper))
                .overlay(RoundedRectangle(cornerRadius: 14).stroke(UI.ink, lineWidth: 2))
        }
        .buttonStyle(.plain)
    }

    private func smallButton(_ text: String, _ enabled: Bool = true,
                             _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(text).font(.system(size: 10, weight: .black)).lineLimit(1)
                .padding(.horizontal, 10).padding(.vertical, 7)
                .background(Capsule().fill(enabled ? UI.sun : UI.paper))
                .foregroundColor(enabled ? UI.ink : UI.muted)
                .overlay(Capsule().stroke(UI.ink, lineWidth: 1.5))
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }

    private func navTile(_ title: String, _ subtitle: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 3) {
                Text(title).font(.system(size: 13, weight: .black)).foregroundColor(UI.ink)
                Text(subtitle).font(.system(size: 10)).foregroundColor(UI.muted)
                Spacer().frame(height: 8)
                Text("→").font(.system(size: 16, weight: .black)).foregroundColor(UI.coral)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(14)
            .background(RoundedRectangle(cornerRadius: 16).fill(UI.paper))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(UI.ink, lineWidth: 2))
        }
        .buttonStyle(.plain)
    }

    private func shopTile(_ item: ShopItem) -> some View {
        let owned = save.inventory.contains(item.id)
        let equipped = save.equippedShip == item.value
        return Button {
            if owned { store.equip(item.id) } else { _ = store.buy(item.id) }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(item.title).font(.system(size: 12, weight: .black))
                    .foregroundColor(UI.ink)
                Text(item.detail).font(.system(size: 9)).foregroundColor(UI.muted).lineLimit(1)
                Spacer().frame(height: 6)
                Text(owned ? (equipped ? "НОСИМ" : "НАДЕТЬ") : "\(item.price) ◈")
                    .font(.system(size: 11, weight: .black))
                    .foregroundColor(owned ? UI.ink : UI.coral)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 14).fill(UI.paper))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(UI.ink, lineWidth: 1.5))
        }
        .buttonStyle(.plain)
    }

    private func choiceChip(_ label: String, _ selected: Bool, disabled: Bool = false,
                            _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(label).font(.system(size: 10, weight: .black)).tracking(0.3).lineLimit(1)
                .minimumScaleFactor(0.8)
                .padding(.horizontal, 11).padding(.vertical, 8)
                .background(Capsule().fill(selected ? UI.lime : UI.paper))
                .foregroundColor(disabled ? UI.muted : UI.ink)
                .overlay(Capsule().stroke(UI.ink, lineWidth: 1.5))
        }
        .buttonStyle(.plain)
        .disabled(disabled)
    }

    private func metric(_ label: String, _ value: String) -> some View {
        VStack(spacing: 2) {
            Text(label).font(.system(size: 9, weight: .bold)).tracking(2).foregroundColor(UI.muted)
                .lineLimit(1).minimumScaleFactor(0.8)
            Text(value).font(.system(size: 20, weight: .black)).foregroundColor(UI.ink)
                .lineLimit(1).minimumScaleFactor(0.8)
        }
        .frame(maxWidth: .infinity)
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 14).fill(UI.paper))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(UI.ink, lineWidth: 1.5))
    }

    private func bar(_ fraction: Float, _ color: Color, _ label: String) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            Text(label).font(.system(size: 9, weight: .bold)).tracking(1).foregroundColor(UI.ink)
                .lineLimit(1).minimumScaleFactor(0.8)
            GeometryReader { g in
                ZStack(alignment: .leading) {
                    Capsule().fill(UI.ink.opacity(0.12))
                    Capsule().fill(color).frame(width: g.size.width * CGFloat(clamped(fraction)))
                }
            }
            .frame(height: 7)
        }
    }

    private func toggleRow(_ text: String, _ binding: Binding<Bool>) -> some View {
        HStack {
            Text(text).font(.system(size: 12, weight: .bold)).foregroundColor(UI.ink)
            Spacer()
            Toggle("", isOn: binding).labelsHidden().toggleStyle(.switch)
        }
        .padding(.top, 8)
    }

    // MARK: - Helpers

    private func clamped(_ value: Float) -> Float {
        guard value.isFinite else { return 0 }
        return min(max(value, 0), 1)
    }

    private func xpInLevel(_ xp: Int) -> Int {
        ((xp % Content.xpPerLevel) + Content.xpPerLevel) % Content.xpPerLevel
    }

    private func daysWord(_ count: Int) -> String {
        let mod100 = abs(count) % 100
        if mod100 >= 11 && mod100 <= 14 { return "ДНЕЙ" }
        switch abs(count) % 10 {
        case 1: return "ДЕНЬ"
        case 2, 3, 4: return "ДНЯ"
        default: return "ДНЕЙ"
        }
    }

    private func compactCredits(_ value: Int) -> String {
        let locale = Locale(identifier: "en_US_POSIX")
        let format: (Double, String) -> String = { amount, suffix in
            let text = String(format: "%.1f", locale: locale, amount)
            return (text.hasSuffix(".0") ? String(text.dropLast(2)) : text) + suffix
        }
        if value >= 1_000_000 { return format(Double(value) / 1_000_000, "m") }
        if value >= 1_000 { return format(Double(value) / 1_000, "k") }
        return "\(value)"
    }
}

private func shipColor(_ index: Int, _ colorblind: Bool) -> Color {
    if colorblind { return Color(hex: 0xFFD654) }
    let colors: [UInt32] = [0x22D3EE, 0xFB7185, 0xA3E635, 0xC084FC, 0xFBB84F, 0xFF5CCD, 0x78DCFF, 0x58FFCC]
    return Color(hex: colors[max(0, min(index, colors.count - 1))])
}
