import SwiftUI

/// Unlock thresholds are based on the all-time best score.
private let shipSkins: [Color] = [
    Color(hex: 0x22D3EE),
    Color(hex: 0xFB7185),
    Color(hex: 0xA3E635),
    Color(hex: 0xC084FC),
]
private let skinThresholds = [0, 150, 400, 800]

struct ContentView: View {

    @StateObject private var engine = GameEngine()
    @StateObject private var sound = SoundEngine()

    @State private var best = UserDefaults.standard.integer(forKey: "bestScore")
    @State private var muted = UserDefaults.standard.bool(forKey: "muted")
    @State private var haptics = UserDefaults.standard.object(forKey: "haptics") as? Bool ?? true
    @State private var skinIndex = UserDefaults.standard.integer(forKey: "skinIndex")
    @State private var topScores = UserDefaults.standard.array(forKey: "topScores") as? [Int] ?? []
    @State private var runs = UserDefaults.standard.integer(forKey: "runs")
    @State private var bestCombo = UserDefaults.standard.integer(forKey: "bestCombo")

    @State private var pulse = false
    @Environment(\.scenePhase) private var scenePhase

    private let timer = Timer.publish(every: 1.0 / 60.0, on: .main, in: .common).autoconnect()

    var body: some View {
        GeometryReader { geo in
            ZStack {
                Color(hex: 0x05030F).ignoresSafeArea()

                Canvas { context, size in
                    engine.draw(context: &context, size: size, tick: engine.frameTick)
                }
                .ignoresSafeArea()
                .contentShape(Rectangle())
                .onTapGesture { engine.tap() }

                hud
                overlays
            }
            .onAppear {
                engine.resize(geo.size)
                engine.best = best
                engine.skinColor = shipSkins[clampedSkin]
                engine.onNewRecord = { score in
                    best = max(best, score)
                    UserDefaults.standard.set(best, forKey: "bestScore")
                }
                engine.onRunEnd = { score, orbs, combo in
                    runs += 1
                    bestCombo = max(bestCombo, combo)
                    var top = (topScores + [score]).sorted(by: >)
                    if top.count > 5 { top = Array(top.prefix(5)) }
                    topScores = top
                    UserDefaults.standard.set(runs, forKey: "runs")
                    UserDefaults.standard.set(bestCombo, forKey: "bestCombo")
                    UserDefaults.standard.set(top, forKey: "topScores")
                    _ = orbs
                }
                engine.onEvent = handle
                sound.muted = muted
                withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                    pulse = true
                }
            }
            .onChange(of: geo.size) { newSize in
                engine.resize(newSize)
            }
        }
        .onReceive(timer) { _ in
            engine.tick(dt: 1.0 / 60.0)
        }
        .onChange(of: scenePhase) { phase in
            if phase != .active {
                engine.pause()
                sound.pauseAmbient()
            }
        }
    }

    private var clampedSkin: Int {
        min(max(skinIndex, 0), shipSkins.count - 1)
    }

    // MARK: - HUD

    private var hud: some View {
        VStack(spacing: 6) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 2) {
                    Text("СЧЁТ")
                        .font(.system(size: 11))
                        .tracking(3)
                        .foregroundColor(.white.opacity(0.5))
                    Text("\(engine.score)")
                        .font(.system(size: 30, weight: .bold))
                        .foregroundColor(.white)
                    if engine.multiplier > 1 {
                        Text("x\(engine.multiplier)")
                            .font(.system(size: 13, weight: .bold))
                            .foregroundColor(Palette.orb)
                    }
                }

                Spacer()

                HStack(spacing: 10) {
                    roundButton(muted ? "🔇" : "🔊") {
                        muted.toggle()
                        sound.muted = muted
                        UserDefaults.standard.set(muted, forKey: "muted")
                    }
                    roundButton("II") {
                        engine.pause()
                    }
                }

                Spacer()

                VStack(alignment: .trailing, spacing: 2) {
                    Text("РЕКОРД")
                        .font(.system(size: 11))
                        .tracking(3)
                        .foregroundColor(.white.opacity(0.5))
                    Text("\(max(best, engine.best))")
                        .font(.system(size: 30, weight: .bold))
                        .foregroundColor(Palette.player.opacity(0.9))
                }
            }

            HStack(spacing: 8) {
                if engine.shield {
                    chip("ЩИТ", Palette.shield)
                }
                if engine.slowTimer > 0 {
                    chip("ЗАМЕДЛЕНИЕ \(Int(engine.slowTimer.rounded(.up)))с", Palette.slow)
                }
                if engine.doubleTimer > 0 {
                    chip("ДВОЙНЫЕ \(Int(engine.doubleTimer.rounded(.up)))с", Palette.double)
                }
            }

            if engine.stormWarning > 0 {
                Text("МЕТЕОРНЫЙ ДОЖДЬ")
                    .font(.system(size: 13, weight: .bold))
                    .tracking(4)
                    .foregroundColor(Palette.danger.opacity(0.4 + 0.6 * min(1, Double(engine.stormWarning) / 1.6)))
            } else if engine.levelUpFlash > 0 {
                Text("УРОВЕНЬ \(engine.level)")
                    .font(.system(size: 15, weight: .bold))
                    .tracking(5)
                    .foregroundColor(Palette.orb.opacity(Double(engine.levelUpFlash)))
            }
        }
        .padding(.horizontal, 20)
        .padding(.top, 6)
        .frame(maxHeight: .infinity, alignment: .top)
    }

    private func roundButton(_ label: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(label)
                .font(.system(size: 14, weight: .semibold))
                .foregroundColor(.white.opacity(0.85))
                .frame(width: 38, height: 38)
                .background(Circle().fill(Color.white.opacity(0.08)))
        }
        .buttonStyle(.plain)
    }

    private func chip(_ text: String, _ color: Color) -> some View {
        Text(text)
            .font(.system(size: 10, weight: .semibold))
            .tracking(1)
            .padding(.horizontal, 10)
            .padding(.vertical, 4)
            .background(Capsule().fill(color.opacity(0.16)))
            .foregroundColor(color)
    }

    // MARK: - Overlays

    @ViewBuilder
    private var overlays: some View {
        if engine.paused {
            card {
                VStack(spacing: 10) {
                    Text("ПАУЗА")
                        .font(.system(size: 28, weight: .black))
                        .tracking(6)
                        .foregroundColor(.white)
                    Text("Игра остановлена")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.6))
                    Spacer().frame(height: 14)
                    primaryButton("Продолжить") { engine.tap() }
                    Button(action: { engine.start() }) {
                        Text("Заново")
                            .font(.system(size: 15))
                            .foregroundColor(.white.opacity(0.6))
                            .padding(.vertical, 6)
                    }
                    .buttonStyle(.plain)
                }
            }
        } else if engine.status == .ready {
            readyCard
        } else if engine.status == .gameOver {
            gameOverCard
        }
    }

    private var readyCard: some View {
        card {
            VStack(spacing: 0) {
                Text("ОРБИТА")
                    .font(.system(size: 42, weight: .black))
                    .tracking(10)
                    .foregroundColor(Palette.player)
                Spacer().frame(height: 12)
                Text("Уклоняйся от астероидов,\nсобирай сферы и бонусы")
                    .font(.system(size: 15))
                    .multilineTextAlignment(.center)
                    .foregroundColor(.white.opacity(0.75))
                Spacer().frame(height: 6)
                Text("Тап — сменить направление полёта")
                    .font(.system(size: 13))
                    .multilineTextAlignment(.center)
                    .foregroundColor(.white.opacity(0.45))
                if best > 0 {
                    Spacer().frame(height: 14)
                    Text("Рекорд: \(best)")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(Palette.orb.opacity(0.9))
                }

                Spacer().frame(height: 18)
                HStack(spacing: 10) {
                    ForEach(0..<shipSkins.count, id: \.self) { index in
                        let unlocked = best >= skinThresholds[index]
                        Circle()
                            .fill(shipSkins[index])
                            .frame(width: 26, height: 26)
                            .overlay(
                                Circle().stroke(
                                    index == skinIndex ? Color.white : Color.white.opacity(0.2),
                                    lineWidth: index == skinIndex ? 2 : 1
                                )
                            )
                            .opacity(unlocked ? 1 : 0.25)
                            .onTapGesture {
                                guard unlocked else { return }
                                skinIndex = index
                                engine.skinColor = shipSkins[index]
                                UserDefaults.standard.set(index, forKey: "skinIndex")
                            }
                    }
                }

                if runs > 0 {
                    Spacer().frame(height: 12)
                    Text("Забегов: \(runs) · Лучшее комбо: \(bestCombo)")
                        .font(.system(size: 11))
                        .foregroundColor(.white.opacity(0.35))
                }

                Spacer().frame(height: 10)
                Button {
                    haptics.toggle()
                    UserDefaults.standard.set(haptics, forKey: "haptics")
                } label: {
                    Text(haptics ? "ВИБРО: ВКЛ" : "ВИБРО: ВЫКЛ")
                        .font(.system(size: 10))
                        .tracking(1)
                        .foregroundColor(.white.opacity(0.4))
                        .padding(.horizontal, 10)
                        .padding(.vertical, 4)
                }
                .buttonStyle(.plain)

                Spacer().frame(height: 20)
                Text("НАЖМИ, ЧТОБЫ НАЧАТЬ")
                    .font(.system(size: 13, weight: .semibold))
                    .tracking(2)
                    .foregroundColor(Palette.player)
                    .opacity(pulse ? 1 : 0.3)
            }
        }
        .onTapGesture { engine.tap() }
    }

    private var gameOverCard: some View {
        card {
            VStack(spacing: 0) {
                Text("ИГРА ОКОНЧЕНА")
                    .font(.system(size: 12, weight: .bold))
                    .tracking(4)
                    .foregroundColor(Palette.danger)
                Spacer().frame(height: 8)
                Text("\(engine.score)")
                    .font(.system(size: 64, weight: .black))
                    .foregroundColor(.white)
                Spacer().frame(height: 2)
                if engine.isNewRecord {
                    Text("НОВЫЙ РЕКОРД")
                        .font(.system(size: 13, weight: .bold))
                        .tracking(2)
                        .foregroundColor(Palette.orb)
                } else {
                    Text("Рекорд: \(max(best, engine.score))")
                        .font(.system(size: 14))
                        .foregroundColor(.white.opacity(0.55))
                }
                Spacer().frame(height: 6)
                Text("УРОВЕНЬ \(engine.level) · Сферы: \(engine.orbsCollected) · Комбо x\(engine.maxCombo)")
                    .font(.system(size: 11))
                    .tracking(1)
                    .foregroundColor(.white.opacity(0.35))

                if topScores.count > 1 {
                    Spacer().frame(height: 14)
                    Text("ЛУЧШИЕ")
                        .font(.system(size: 10))
                        .tracking(3)
                        .foregroundColor(.white.opacity(0.3))
                    Spacer().frame(height: 4)
                    Text(topScores.map { String($0) }.joined(separator: " · "))
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.white.opacity(0.55))
                }

                Spacer().frame(height: 22)
                primaryButton("Ещё раз") { engine.start() }
            }
        }
    }

    private func card<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        content()
            .padding(.horizontal, 28)
            .padding(.vertical, 32)
            .frame(maxWidth: 340)
            .background(
                RoundedRectangle(cornerRadius: 28)
                    .fill(Color(hex: 0x0A0618).opacity(0.92))
            )
            .padding(32)
    }

    private func primaryButton(_ title: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Text(title)
                .font(.system(size: 16, weight: .semibold))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .background(Capsule().fill(Palette.player))
                .foregroundColor(Color(hex: 0x04222A))
        }
        .buttonStyle(.plain)
    }

    // MARK: - Events

    private func handle(_ event: GameEvent) {
        switch event {
        case .tap:
            sound.play("tap")
            sound.startAmbient()
        case .dodge(let combo):
            sound.play(
                "dodge",
                rate: min(1.9, 1 + Float(combo) * 0.045),
                volume: 0.45
            )
        case .orb:
            sound.play("orb")
        case .powerUp:
            sound.play("powerup")
            haptic(.light)
        case .shieldBreak:
            sound.play("shield")
            haptic(.heavy)
        case .crash:
            sound.play("crash")
            haptic(.heavy)
        case .levelUp:
            sound.play("level")
        case .nearMiss:
            haptic(.light)
        case .storm:
            sound.play("storm")
            haptic(.medium)
        }
    }

    private func haptic(_ style: UIImpactFeedbackGenerator.FeedbackStyle) {
        guard haptics else { return }
        UIImpactFeedbackGenerator(style: style).impactOccurred()
    }
}
