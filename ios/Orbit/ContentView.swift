import SwiftUI
import UIKit

private enum Screen: String {
    case hub, missions, hangar, shop, achievements, stats, settings
}

struct ContentView: View {

    @StateObject private var engine = GameEngine()
    @StateObject private var sound = SoundEngine()
    @StateObject private var store = SaveStore()

    @State private var art = GameArt()
    @State private var screen: Screen? = .hub
    @State private var frame: UInt64 = 0
    @State private var toast: String?
    @State private var confirmReset = false
    @State private var exportText = ""
    @State private var importText = ""
    @State private var importResult = ""
    @State private var statMode = "classic"

    @Environment(\.scenePhase) private var scenePhase
    @State private var timer = Timer.publish(every: 1.0 / 60.0, on: .main, in: .common).autoconnect()

    private var save: SaveData { store.data }
    private var mode: Mode { Mode.from(save.selectedMode) }
    private var sector: SectorDef { Content.sector(save.selectedSector) }

    var body: some View {
        GeometryReader { geo in
            ZStack {
                Color(hex: 0x05030F).ignoresSafeArea()

                if screen == nil {
                    Canvas { context, size in
                        engine.draw(context: &context, size: size, tick: Int(engine.frameTick),
                                    art: art, shipSprite: save.equippedShip,
                                    planetStyle: save.equippedPlanet, trailStyle: save.equippedTrail)
                    }
                    .ignoresSafeArea()
                    .contentShape(Rectangle())
                    .onTapGesture {
                        guard engine.status == .playing, !engine.paused else { return }
                        engine.tap()
                    }
                    .onAppear { engine.resize(geo.size) }
                    .onChange(of: geo.size) { engine.resize($0) }

                    hud
                    gameOverlays
                } else {
                    Canvas { context, size in
                        art.drawMenu(in: &context, size: size, style: save.equippedPlanet * 2, tick: Int(frame))
                    }
                    .ignoresSafeArea()

                    Group {
                        switch screen {
                        case .hub: hubScreen
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

                if let toast {
                    VStack {
                        Spacer()
                        Text(toast)
                            .font(.system(size: 12, weight: .semibold))
                            .multilineTextAlignment(.center)
                            .foregroundColor(Palette.orb)
                            .padding(.horizontal, 18).padding(.vertical, 12)
                            .background(RoundedRectangle(cornerRadius: 18).fill(Color(hex: 0x0B1A2E).opacity(0.95)))
                            .padding(.bottom, 20)
                            .transition(.opacity)
                    }
                    .animation(.easeInOut, value: toast)
                }
            }
        }
        .onReceive(timer) { _ in
            frame &+= 1
            engine.tick(dt: 1.0 / 60.0)
        }
        .onReceive(store.objectWillChange) { syncEngine() }
        .onAppear {
            configureEngine()
            syncEngine()
            sound.applySettings(save.settings)
        }
        .onChange(of: scenePhase) { phase in
            if phase != .active {
                engine.pause()
                sound.pauseMusic()
            } else if screen == nil, engine.status == .playing, !engine.paused {
                sound.resumeMusic()
            }
        }
    }

    private func syncEngine() {
        engine.setBest(save.bestByMode[mode.id] ?? 0)
        engine.skinColor = shipColor(save.equippedShip, save.settings.colorblind)
        engine.setReducedMotion(save.settings.reducedMotion)
        engine.setUpgrades(store.upgrades())
        sound.applySettings(save.settings)
    }

    private func startRun() {
        let seed = mode == .daily ? Content.seedFor(save.dailyDate) : (Int(Date().timeIntervalSince1970) & 0x7fffffff)
        syncEngine()
        engine.startRun(mode, sector: save.selectedSector, seed: seed)
        screen = nil
        sound.startMusic(engine.sectorId)
        haptic(.heavy)
    }

    private func goHub() {
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
            var parts = ["+\(summary.credits) жетонов"]
            if summary.levelUps > 0 { parts.append("Новый уровень x\(summary.levelUps)") }
            parts.append(contentsOf: summary.achievements)
            parts.append(contentsOf: summary.secrets)
            parts.append(contentsOf: summary.missions)
            if !summary.seasonTiers.isEmpty { parts.append("Награды сезона: \(summary.seasonTiers.count)") }
            toast = parts.joined(separator: "  ·  ")
            DispatchQueue.main.asyncAfter(deadline: .now() + 4.2) { toast = nil }
        }
    }

    private func haptic(_ style: UIImpactFeedbackGenerator.FeedbackStyle) {
        guard save.settings.haptics else { return }
        UIImpactFeedbackGenerator(style: style).impactOccurred()
    }

    // MARK: - HUD

    private var hud: some View {
        VStack(spacing: 0) {
            HStack(alignment: .top) {
                statBlock("СЧЁТ", "\(engine.score)", engine.multiplier > 1 ? "x\(engine.multiplier)" : nil, Palette.player)
                Spacer()
                HStack(spacing: 8) {
                    roundButton(save.settings.muted ? "×" : "♪") {
                        store.updateSettings { $0.muted.toggle() }
                    }
                    roundButton("II") { engine.pause(); sound.pauseMusic() }
                    roundButton("⌂") { goHub() }
                }
                Spacer()
                statBlock("РЕКОРД", "\(max(save.bestByMode[mode.id] ?? 0, engine.best))", nil, Palette.orb, end: true)
            }

            if engine.timeLeft > 0 || engine.combo > 0 {
                HStack(spacing: 6) {
                    if engine.timeLeft > 0 { chip("\(Int(ceil(engine.timeLeft)))с", Palette.slow) }
                    if engine.combo > 0 { chip("КОМБО \(engine.combo)", Palette.player) }
                }
                .padding(.top, 8)
            }

            let chips = activeChips()
            if !chips.isEmpty {
                HStack(spacing: 5) {
                    ForEach(chips.indices, id: \.self) { i in
                        chip(chips[i].0, chips[i].1)
                    }
                }
                .padding(.top, 8)
            }
            Spacer()
            if engine.stormWarning > 0 {
                banner("МЕТЕОРНЫЙ ДОЖДЬ", Palette.danger)
            } else if engine.flareWarning > 0 {
                banner("СОЛНЕЧНАЯ ВСПЫШКА", Palette.orb)
            } else if engine.levelUpFlash > 0 {
                banner("УРОВЕНЬ \(engine.level)", Palette.orb, Double(engine.levelUpFlash))
            }
            Spacer().frame(height: 60)
        }
        .padding(.horizontal, 16)
        .padding(.top, 8)
    }

    private func activeChips() -> [(String, Color)] {
        var out: [(String, Color)] = []
        if engine.shield { out.append(("ЩИТ", Palette.shield)) }
        if engine.slowTimer > 0 { out.append(("ЗАМЕДЛЕНИЕ \(Int(ceil(engine.slowTimer)))с", Palette.slow)) }
        if engine.doubleTimer > 0 { out.append(("x2 \(Int(ceil(engine.doubleTimer)))с", Palette.double)) }
        if engine.magnetTimer > 0 { out.append(("МАГНИТ \(Int(ceil(engine.magnetTimer)))с", Palette.magnet)) }
        if engine.phaseTimer > 0 { out.append(("ФАЗА \(Int(ceil(engine.phaseTimer)))с", Palette.phase)) }
        if engine.freezeTimer > 0 { out.append(("ЗАМОРОЗКА \(Int(ceil(engine.freezeTimer)))с", Palette.freeze)) }
        return out
    }

    private func statBlock(_ label: String, _ value: String, _ extra: String?, _ color: Color, end: Bool = false) -> some View {
        VStack(alignment: end ? .trailing : .leading, spacing: 2) {
            Text(label).font(.system(size: 10)).tracking(3).foregroundColor(.white.opacity(0.5))
            Text(value).font(.system(size: 30, weight: .black)).foregroundColor(.white)
            if let extra { Text(extra).font(.system(size: 12, weight: .bold)).foregroundColor(color) }
        }
    }

    private func roundButton(_ label: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(label).font(.system(size: 15, weight: .bold)).foregroundColor(.white.opacity(0.9))
                .frame(width: 38, height: 38)
                .background(Circle().fill(Color.white.opacity(0.09)))
                .overlay(Circle().stroke(Color.white.opacity(0.12), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    private func chip(_ text: String, _ color: Color) -> some View {
        Text(text).font(.system(size: 10, weight: .semibold)).tracking(1)
            .padding(.horizontal, 10).padding(.vertical, 4)
            .background(Capsule().fill(color.opacity(0.18))).foregroundColor(color)
    }

    private func banner(_ text: String, _ color: Color, _ alpha: Double = 1) -> some View {
        Text(text).font(.system(size: 13, weight: .bold)).tracking(4)
            .foregroundColor(color.opacity(alpha)).frame(maxWidth: .infinity)
    }

    // MARK: - Game overlays

    @ViewBuilder
    private var gameOverlays: some View {
        if engine.paused {
            modal {
                title("ПАУЗА", Palette.player)
                bodyText("Полёт приостановлен")
                Spacer().frame(height: 20)
                wideButton("ПРОДОЛЖИТЬ") { engine.resume(); sound.resumeMusic() }
                wideButton("ЗАНОВО", accent: Palette.slow) { startRun() }
                wideButton("В ХАБ", accent: .gray) { goHub() }
            }
        } else if engine.status == .gameOver {
            modal {
                title(engine.completed ? "МАРШРУТ ЗАВЕРШЁН" : "ПОТЕРЯН СИГНАЛ",
                      engine.completed ? Palette.player : Palette.danger)
                Text("\(engine.score)").font(.system(size: 56, weight: .black)).foregroundColor(.white)
                if engine.isNewRecord {
                    Text("НОВЫЙ РЕКОРД").font(.system(size: 12, weight: .bold)).tracking(3).foregroundColor(Palette.orb)
                } else {
                    Text("Рекорд: \(save.bestByMode[mode.id] ?? 0)").font(.system(size: 12)).foregroundColor(.gray)
                }
                Spacer().frame(height: 8)
                bodyText("Уровень \(engine.level) · Сферы \(engine.orbsCollected) · Комбо x\(engine.maxCombo) · " +
                         "Уклонения \(engine.stats.dodges) · Боссы \(engine.stats.bossKills)")
                Spacer().frame(height: 10)
                panel {
                    sectionTitle("НАГРАДА")
                    bodyText("+\(save.credits) жетонов всего · Уровень \(save.playerLevel)", Palette.orb)
                }
                Spacer().frame(height: 18)
                wideButton("ЕЩЁ РАЗ") { startRun() }
                wideButton("В ХАБ", accent: .gray) { goHub() }
            }
        }
    }

    // MARK: - Hub

    private var hubScreen: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    Canvas { context, size in
                        art.drawPlanetPreview(in: &context, index: save.equippedPlanet,
                                              center: CGPoint(x: size.width / 2, y: size.height / 2),
                                              radius: min(size.width, size.height) / 2, tick: Int(frame))
                    }
                    .frame(width: 56, height: 56)
                    VStack(alignment: .leading) {
                        Text("ОРБИТА").font(.system(size: 28, weight: .black)).tracking(8).foregroundColor(Palette.player)
                        Text("СЕЗОН \(save.seasonId) · УРОВЕНЬ \(save.playerLevel)")
                            .font(.system(size: 10)).tracking(2).foregroundColor(.gray)
                    }
                    Spacer()
                    VStack(alignment: .trailing) {
                        Text("◆ \(save.credits)").font(.system(size: 17, weight: .bold)).foregroundColor(Palette.orb)
                        if save.dailyStreak > 0 {
                            Text("\(save.dailyStreak) ДНЕЙ").font(.system(size: 10)).foregroundColor(Palette.danger)
                        }
                    }
                }

                bar(Float(save.xp % Content.xpPerLevel) / Float(Content.xpPerLevel),
                    Palette.player, "XP \(save.xp % Content.xpPerLevel) / \(Content.xpPerLevel)")
                bar(Float(save.seasonXp) / Float(Content.seasonTiers.last!.xp),
                    Palette.slow, "СЕЗОН \(save.seasonXp) / \(Content.seasonTiers.last!.xp)")

                sectionTitle("РЕЖИМ")
                HStack(spacing: 6) {
                    ForEach(Content.modes, id: \.id) { m in
                        choiceChip(m.title, m.id == mode.id) { store.selectMode(m.id) }
                    }
                }
                bodyText(mode.detail)

                sectionTitle("СЕКТОР")
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(Content.sectors, id: \.id) { s in
                            let unlocked = save.playerLevel >= s.unlockLevel || save.visitedSectors.contains(s.id)
                            choiceChip(unlocked ? s.name : "🔒 \(s.name)", s.id == sector.id, disabled: !unlocked) {
                                store.selectSector(s.id)
                            }
                        }
                    }
                }
                bodyText("\(sector.subtitle) · Рекорд \(save.bestByMode[mode.id] ?? 0)")

                Spacer().frame(height: 6)
                wideButton("НАЧАТЬ ПОЛЁТ  ▸") { startRun() }
                let defs = Content.missionsFor(save.dailyDate)
                let done = defs.enumerated().filter { save.dailyProgress[$0.offset] >= $0.element.goal }.count
                wideButton("ЗАДАНИЯ ДНЯ  \(done)/3", accent: Palette.slow) { open(.missions) }

                let tiles: [(String, String, Screen)] = [
                    ("АНГАР", "Корабли и улучшения", .hangar),
                    ("МАГАЗИН", "Предметы за жетоны", .shop),
                    ("ДОСТИЖЕНИЯ", "Награды и секреты", .achievements),
                    ("СТАТИСТИКА", "Рекорды и история", .stats),
                    ("НАСТРОЙКИ", "Звук и сохранение", .settings),
                ]
                ForEach(0..<tiles.count/2, id: \.self) { i in
                    HStack(spacing: 10) {
                        navTile(tiles[i*2].0, tiles[i*2].1) { open(tiles[i*2].2) }
                        navTile(tiles[i*2+1].0, tiles[i*2+1].1) { open(tiles[i*2+1].2) }
                    }
                }
                HStack(spacing: 10) {
                    navTile(tiles[4].0, tiles[4].1) { open(tiles[4].2) }
                    Spacer()
                }
                bodyText("Тап во время полёта меняет направление.")
                Spacer().frame(height: 20)
            }
            .padding(.horizontal, 20)
            .padding(.top, 18)
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
                    Text(m.title).font(.system(size: 15, weight: .bold)).foregroundColor(done ? Palette.player : .white)
                    Spacer().frame(height: 4)
                    bar(Float(value) / Float(m.goal), done ? Palette.player : Palette.slow, "\(value) / \(m.goal)")
                    Spacer().frame(height: 8)
                    HStack {
                        Text("+\(m.reward) жетонов").font(.system(size: 12)).foregroundColor(Palette.orb)
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
                        Text("Уровень \(i + 1) · \(tier.xp) XP").font(.system(size: 13)).foregroundColor(.white)
                        Spacer()
                        Text("+\(tier.reward) ◈").font(.system(size: 13)).foregroundColor(Palette.orb)
                    }
                    bodyText(save.seasonClaimed[i] ? "ПОЛУЧЕНО" :
                        (save.seasonXp >= tier.xp ? "Награда начислится после забега" : "Ещё \(tier.xp - save.seasonXp) XP"))
                }
            }
            bodyText("Серия заданий: \(save.dailyStreak) дней · Дзен-забегов: \(save.zenRuns)")
        }
    }

    // MARK: - Hangar

    private var hangarScreen: some View {
        scrollScreen("АНГАР", "Все предметы хранятся локально.") {
            ZStack {
                Canvas { context, size in
                    art.drawPlanetPreview(in: &context, index: save.equippedPlanet,
                                          center: CGPoint(x: size.width / 2, y: size.height / 2),
                                          radius: 78, tick: Int(frame))
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
                            Text(item.title).font(.system(size: 13, weight: .bold)).foregroundColor(.white)
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
        scrollScreen("МАГАЗИН", "Баланс: ◆ \(save.credits)") {
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
                                Text(item.title).font(.system(size: 14, weight: .bold)).foregroundColor(.white)
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
                        Text(open ? "★" : "☆").font(.system(size: 18)).foregroundColor(open ? Palette.orb : .gray)
                        VStack(alignment: .leading) {
                            Text(a.title).font(.system(size: 13, weight: .bold))
                                .foregroundColor(open ? .white : .gray)
                            bodyText(a.detail)
                        }
                        Spacer()
                        Text("+\(a.reward) ◈").font(.system(size: 11)).foregroundColor(open ? Palette.orb : .gray)
                    }
                }
            }
            sectionTitle("СЕКРЕТЫ")
            ForEach(Content.secrets, id: \.id) { s in
                let open = save.secrets.contains(s.id)
                panel {
                    Text(open ? s.title : "???").font(.system(size: 13, weight: .bold))
                        .foregroundColor(open ? Palette.slow : .gray)
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
                        choiceChip(m.title, statMode == m.id) { statMode = m.id }
                    }
                }
            }
            panel {
                let top = save.topByMode[statMode] ?? []
                if top.isEmpty { bodyText("В этом режиме ещё нет забегов.") }
                else {
                    ForEach(0..<top.count, id: \.self) { i in
                        Text("\(i + 1).  \(top[i])").font(.system(size: 14, weight: i == 0 ? .bold : .regular))
                            .foregroundColor(i == 0 ? Palette.orb : .white)
                    }
                }
            }
            sectionTitle("ИСТОРИЯ ЗАБЕГОВ")
            ForEach(save.history.prefix(12), id: \.timestamp) { r in
                panel {
                    HStack {
                        Text("\(Mode.from(r.mode).title) · \(Content.sector(r.sector).name)")
                            .font(.system(size: 12)).foregroundColor(.white)
                        Spacer()
                        Text("\(r.score)").font(.system(size: 14, weight: .bold)).foregroundColor(Palette.player)
                    }
                    bodyText("Ур. \(r.level) · Сферы \(r.orbs) · Комбо x\(r.maxCombo) · \(r.seconds)с")
                }
            }
        }
    }

    // MARK: - Settings

    private var settingsScreen: some View {
        scrollScreen("НАСТРОЙКИ", "Прогресс сохраняется автоматически после каждого действия.") {
            sectionTitle("ЗВУК")
            panel {
                Text("Музыка \(Int(save.settings.music * 100))%").font(.system(size: 12)).foregroundColor(.white)
                Slider(value: Binding(get: { save.settings.music },
                                      set: { v in store.updateSettings { $0.music = v } }), in: 0...1)
                Text("Эффекты \(Int(save.settings.sfx * 100))%").font(.system(size: 12)).foregroundColor(.white)
                Slider(value: Binding(get: { save.settings.sfx },
                                      set: { v in store.updateSettings { $0.sfx = v } }), in: 0...1)
                toggleRow("Полное выключение звука", Binding(get: { save.settings.muted },
                                                             set: { v in store.updateSettings { $0.muted = v } }))
                toggleRow("Вибрация", Binding(get: { save.settings.haptics },
                                              set: { v in store.updateSettings { $0.haptics = v } }))
            }
            sectionTitle("ДОСТУПНОСТЬ")
            panel {
                toggleRow("Уменьшить анимацию и тряску", Binding(get: { save.settings.reducedMotion },
                                                                 set: { v in store.updateSettings { $0.reducedMotion = v } }))
                toggleRow("Контрастные цвета", Binding(get: { save.settings.colorblind },
                                                       set: { v in store.updateSettings { $0.colorblind = v } }))
            }
            sectionTitle("СОХРАНЕНИЕ")
            panel {
                bodyText("Код экспорта содержит контроль целостности. Импорт заменяет текущий прогресс.")
                wideButton("ЭКСПОРТИРОВАТЬ КОД", accent: Palette.slow) { exportText = store.exportCode() }
                if !exportText.isEmpty {
                    TextField("", text: $exportText).textFieldStyle(.roundedBorder).font(.system(size: 10))
                }
                TextField("ORB1-...", text: $importText).textFieldStyle(.roundedBorder).font(.system(size: 12))
                wideButton("ИМПОРТИРОВАТЬ") {
                    importResult = store.importCode(importText) ? "Прогресс восстановлен" : "Код недействителен"
                }
                if !importResult.isEmpty { bodyText(importResult, Palette.orb) }
            }
            sectionTitle("ОПАСНАЯ ЗОНА")
            if !confirmReset {
                wideButton("СБРОСИТЬ ПРОГРЕСС", accent: Palette.danger) { confirmReset = true }
            } else {
                bodyText("Точно удалить все рекорды и предметы?", Palette.danger)
                wideButton("ДА, СБРОСИТЬ", accent: Palette.danger) { store.reset(); confirmReset = false }
                wideButton("ОТМЕНА", accent: .gray) { confirmReset = false }
            }
            bodyText("Орбита 3.5 · Локальная игра без рекламы и сети")
        }
    }

    // MARK: - Reusable UI

    private func scrollScreen<Content: View>(_ title: String, _ subtitle: String,
                                             @ViewBuilder content: () -> Content) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    VStack(alignment: .leading) {
                        Text(title).font(.system(size: 26, weight: .black)).tracking(4).foregroundColor(Palette.player)
                        Text("◆ \(save.credits)").font(.system(size: 12)).foregroundColor(Palette.orb)
                    }
                    Spacer()
                    smallButton("← ХАБ", true) { goHub() }
                }
                bodyText(subtitle)
                content()
                Spacer().frame(height: 24)
            }
            .padding(.horizontal, 20).padding(.top, 18)
        }
    }

    private func sectionTitle(_ text: String) -> some View {
        Text(text).font(.system(size: 11, weight: .bold)).tracking(3)
            .foregroundColor(Palette.player.opacity(0.8))
    }

    private func bodyText(_ text: String, _ color: Color = .gray) -> some View {
        Text(text).font(.system(size: 12)).foregroundColor(color).lineSpacing(4)
    }

    private func panel<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            content()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(14)
        .background(RoundedRectangle(cornerRadius: 16).fill(Color(hex: 0x0A071C).opacity(0.92)))
    }

    private func modal<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        VStack(spacing: 4) {
            content()
        }
        .frame(maxWidth: 360)
        .padding(24)
        .background(RoundedRectangle(cornerRadius: 24).fill(Color(hex: 0x080A1C).opacity(0.96)))
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color.black.opacity(0.6))
    }

    private func title(_ text: String, _ color: Color) -> some View {
        Text(text).font(.system(size: 20, weight: .black)).tracking(3).foregroundColor(color)
            .multilineTextAlignment(.center)
    }

    private func wideButton(_ text: String, accent: Color = Palette.player,
                            _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(text).font(.system(size: 13, weight: .bold)).tracking(1)
                .frame(maxWidth: .infinity).padding(.vertical, 14)
                .background(Capsule().fill(accent.opacity(0.85)))
                .foregroundColor(Color(hex: 0x06131C))
        }
        .buttonStyle(.plain)
    }

    private func smallButton(_ text: String, _ enabled: Bool = true,
                             _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(text).font(.system(size: 10, weight: .bold))
                .padding(.horizontal, 10).padding(.vertical, 7)
                .background(Capsule().fill(enabled ? Palette.player.opacity(0.2) : Color.white.opacity(0.05)))
                .foregroundColor(enabled ? Palette.player : .gray)
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
    }

    private func navTile(_ title: String, _ subtitle: String, _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 3) {
                Text(title).font(.system(size: 13, weight: .bold)).foregroundColor(.white)
                Text(subtitle).font(.system(size: 10)).foregroundColor(.gray)
                Spacer().frame(height: 8)
                Text("→").font(.system(size: 16)).foregroundColor(Palette.player)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(14)
            .background(RoundedRectangle(cornerRadius: 16).fill(Color(hex: 0x0A071C).opacity(0.92)))
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
                Text(item.title).font(.system(size: 12, weight: .bold))
                    .foregroundColor(equipped ? Palette.player : .white)
                Text(item.detail).font(.system(size: 9)).foregroundColor(.gray).lineLimit(1)
                Spacer().frame(height: 6)
                Text(owned ? (equipped ? "НОСИМ" : "НАДЕТЬ") : "\(item.price) ◈")
                    .font(.system(size: 11)).foregroundColor(owned ? Palette.player : Palette.orb)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(12)
            .background(RoundedRectangle(cornerRadius: 14).fill(Color(hex: 0x0A071C).opacity(0.92)))
        }
        .buttonStyle(.plain)
    }

    private func choiceChip(_ label: String, _ selected: Bool, disabled: Bool = false,
                            _ action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(label).font(.system(size: 9, weight: .semibold)).tracking(1).lineLimit(1)
                .padding(.horizontal, 9).padding(.vertical, 8)
                .background(Capsule().fill(selected ? Palette.player.opacity(0.22) : Color.white.opacity(0.05)))
                .foregroundColor(disabled ? .gray : (selected ? Palette.player : .white.opacity(0.8)))
        }
        .buttonStyle(.plain)
        .disabled(disabled)
    }

    private func metric(_ label: String, _ value: String) -> some View {
        VStack(spacing: 2) {
            Text(label).font(.system(size: 9)).tracking(2).foregroundColor(.gray)
            Text(value).font(.system(size: 20, weight: .black)).foregroundColor(.white)
        }
        .frame(maxWidth: .infinity)
        .padding(12)
        .background(RoundedRectangle(cornerRadius: 14).fill(Color(hex: 0x0A071C).opacity(0.92)))
    }

    private func bar(_ fraction: Float, _ color: Color, _ label: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label).font(.system(size: 9)).tracking(1).foregroundColor(.gray)
            GeometryReader { g in
                ZStack(alignment: .leading) {
                    Capsule().fill(Color.white.opacity(0.08))
                    Capsule().fill(color).frame(width: g.size.width * CGFloat(min(max(fraction, 0), 1)))
                }
            }
            .frame(height: 5)
        }
    }

    private func toggleRow(_ text: String, _ binding: Binding<Bool>) -> some View {
        HStack {
            Text(text).font(.system(size: 12)).foregroundColor(.white.opacity(0.85))
            Spacer()
            Toggle("", isOn: binding).labelsHidden().toggleStyle(.switch)
        }
        .padding(.top, 8)
    }
}

private func shipColor(_ index: Int, _ colorblind: Bool) -> Color {
    if colorblind { return Color(hex: 0xFFD654) }
    let colors: [UInt32] = [0x22D3EE, 0xFB7185, 0xA3E635, 0xC084FC, 0xFBB84F, 0xFF5CCD, 0x78DCFF, 0x58FFCC]
    return Color(hex: colors[max(0, min(index, colors.count - 1))])
}
