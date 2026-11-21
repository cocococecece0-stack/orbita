import SwiftUI

/// Чистая симуляция «Орбиты». Зеркально совпадает с Android GameEngine.kt.
final class GameEngine: ObservableObject {

    @Published var status: GameStatus = .ready
    @Published var score: Int = 0
    @Published var best: Int = 0
    @Published var isNewRecord = false
    @Published var paused = false
    @Published var combo = 0
    @Published var multiplier = 1
    @Published var level = 1
    @Published var shield = false
    @Published var shieldTime: CGFloat = 0
    @Published var orbsCollected = 0
    @Published var maxCombo = 0
    @Published var frameTick: UInt64 = 0
    @Published var timeLeft: CGFloat = -1
    @Published var completed = false
    @Published var fever = false
    /// Открытый выбор мутации: пока он не null, симуляция на паузе.
    @Published var draft: PerkDraft?
    @Published var perkIds: [Int] = []

    var skinColor: Color = Palette.player
    var mode: Mode = .classic
    var sectorId = 0

    var onEvent: ((GameEvent) -> Void)?
    var onNewRecord: ((Int) -> Void)?
    var onRunEnd: ((RunStats) -> Void)?

    private(set) var width: CGFloat = 0
    private(set) var height: CGFloat = 0
    private(set) var centerX: CGFloat = 0
    private(set) var centerY: CGFloat = 0
    private(set) var orbitRadius: CGFloat = 0
    private(set) var planetRadius: CGFloat = 0
    private(set) var playerRadius: CGFloat = 0

    private(set) var time: CGFloat = 0
    private(set) var shake: CGFloat = 0
    private(set) var flash: CGFloat = 0
    private(set) var levelUpFlash: CGFloat = 0
    private(set) var shieldFlash: CGFloat = 0
    private(set) var reverseFlash: CGFloat = 0
    private(set) var hitStop: CGFloat = 0
    private(set) var invulnerable: CGFloat = 0
    private(set) var slowTimer: CGFloat = 0
    private(set) var doubleTimer: CGFloat = 0
    private(set) var magnetTimer: CGFloat = 0
    private(set) var phaseTimer: CGFloat = 0
    private(set) var freezeTimer: CGFloat = 0
    private(set) var stormWarning: CGFloat = 0
    private(set) var flareWarning: CGFloat = 0
    private(set) var comboTime: CGFloat = 0
    /// Заряд резонанса 0..1: заполняется за навык и очищает орбиту волной.
    @Published var resonance: CGFloat = 0
    @Published var resonanceFlash: CGFloat = 0
    private(set) var playerAngle: CGFloat = -CGFloat.pi / 2
    private(set) var direction: CGFloat = 1
    private(set) var speedLevel: CGFloat = 0

    var asteroids: [Asteroid] = []
    var orbs: [Orb] = []
    var powerUps: [PowerUp] = []
    var wormholes: [Wormhole] = []
    var wells: [GravityWell] = []
    var boss: [Boss] = []
    var particles: [Particle] = []
    var stars: [Star] = []
    var trail: [TrailPoint] = []
    var stats = RunStats()

    private var minDim: CGFloat = 0
    private var spawnTimer: CGFloat = 0
    private var orbTimer: CGFloat = 0
    private var powerTimer: CGFloat = 0
    private var stormTimer: CGFloat = 0
    private var flareTimer: CGFloat = 0
    private var mineTimer: CGFloat = 0
    private var portalTimer: CGFloat = 0
    private var wellTimer: CGFloat = 0
    private var lastBossLevel = 0
    private var nextBossLevel = 5
    private var eventCooldown: CGFloat = 0
    private var seasonDensity: CGFloat = 1
    private var elapsed: CGFloat = 0
    private var ghostTimer: CGFloat = 0
    private var reviveAvailable = false
    private var upgradeShield = false
    private var upgradeTime = false
    private var upgradeMagnet = false
    private var upgradeSlow = false
    private var upgradeDouble = false
    private var reducedMotion = false
    private var scoreBoost: CGFloat = 1
    private var orbBoost = 1
    private var hazardSpeed: CGFloat = 1
    private var hazardDensity: CGFloat = 1
    private var nextDraftAt: CGFloat = 16
    private var draftRound = 0
    private var objectiveRewarded = false

    private var ghostBuffer = [CGFloat](repeating: 0, count: 4096)
    private(set) var ghostCount = 0

    var playerX: CGFloat { centerX + cos(playerAngle) * orbitRadius }
    var playerY: CGFloat { centerY + sin(playerAngle) * orbitRadius }

    func setReducedMotion(_ value: Bool) { reducedMotion = value }
    func setBest(_ value: Int) { best = value }
    func setSeasonModifier(_ value: CGFloat) { seasonDensity = max(0.5, min(2, value)) }

    func setUpgrades(_ flags: [String: Bool]) {
        upgradeShield = flags["upg_shield"] == true
        upgradeTime = flags["upg_time"] == true
        upgradeMagnet = flags["upg_magnet"] == true
        upgradeSlow = flags["upg_slow"] == true
        upgradeDouble = flags["upg_double"] == true
    }

    func resize(_ size: CGSize) {
        guard size.width > 0, size.height > 0 else { return }
        guard size.width != width || size.height != height else { return }
        width = size.width
        height = size.height
        centerX = width / 2
        centerY = height / 2
        minDim = min(width, height)
        orbitRadius = minDim * 0.31
        planetRadius = minDim * 0.16
        playerRadius = minDim * 0.017
        generateStars()
    }

    func startRun(_ newMode: Mode, sector newSector: Int, seed: Int) {
        rngState = UInt64(bitPattern: Int64(seed) ^ 0x5DEECE66D) & ~(UInt64(1) << 63)
        mode = newMode
        sectorId = max(0, min(newSector, Content.sectors.count - 1))
        asteroids.removeAll(); orbs.removeAll(); powerUps.removeAll()
        wormholes.removeAll(); wells.removeAll(); boss.removeAll()
        particles.removeAll(); trail.removeAll()
        ghostCount = 0; ghostTimer = 0
        score = 0; isNewRecord = false; paused = false
        combo = 0; multiplier = 1; level = mode == .survival ? 3 : 1
        shield = upgradeShield
        shieldTime = shield ? (upgradeShield ? 14 : 10) : 0
        orbsCollected = 0; maxCombo = 0
        eventCooldown = 0; elapsed = 0
        comboTime = 0; slowTimer = 0; doubleTimer = 0
        magnetTimer = 0; phaseTimer = 0; freezeTimer = 0
        invulnerable = 0; hitStop = 0; flash = 0
        levelUpFlash = 0; shieldFlash = 0; resonanceFlash = 0; resonance = 0
        stormWarning = 0; flareWarning = 0
        playerAngle = -CGFloat.pi / 2; direction = 1
        spawnTimer = 0.7; orbTimer = 2.2; powerTimer = 5
        stormTimer = 20; flareTimer = 16; mineTimer = 9
        portalTimer = 12; wellTimer = 18; lastBossLevel = 0; nextBossLevel = 5
        orbSpawnCursor = 0
        hazardSpawnCursor = rnd() * tau
        time = 0; shake = 0; reverseFlash = 0; speedLevel = 0
        draft = nil; perkIds.removeAll(); scoreBoost = 1; orbBoost = 1
        hazardSpeed = 1; hazardDensity = 1; nextDraftAt = 16; draftRound = 0
        objectiveRewarded = false
        fever = false; completed = false; reviveAvailable = false
        timeLeft = mode.seconds > 0 ? CGFloat(mode.seconds) + (upgradeTime ? 8 : 0) : -1
        stats = RunStats(level: level)
        status = .playing
    }

    func start() {
        startRun(mode, sector: sectorId, seed: Int(Date().timeIntervalSince1970) & 0x7fffffff)
    }

    /// Тап: смена направления. Ядро механики не изменилось.
    func tap() {
        onEvent?(.tap)
        if paused { paused = false; return }
        switch status {
        case .ready: start()
        case .playing:
            direction = -direction
            reverseFlash = 1
        case .gameOver: break
        }
    }

    func pause() { if status == .playing { paused = true } }
    func resume() { paused = false }
    func toReady() { status = .ready; paused = false; shieldTime = 0; draft = nil }

    /// Выход из забега домой: результат фиксируется, а не теряется.
    func abandonRun() {
        guard status == .playing else { return }
        status = .gameOver
        completed = false
        paused = false
        draft = nil
        stats.seconds = Int(elapsed)
        onRunEnd?(stats)
    }

    func tick(dt raw: CGFloat) {
        defer { frameTick &+= 1 }
        guard width > 0 else { return }
        let step = max(0, min(raw, 0.05))
        time += step
        elapsed += step
        // Во время выбора мутации полёт заморожен: таймер и угрозы не двигаются.
        let running = status == .playing && !paused && draft == nil
        if running && timeLeft > 0 {
            timeLeft = max(0, timeLeft - step)
            stats.seconds = Int(elapsed)
            if timeLeft <= 0 { finishRun(); return }
        } else if running {
            stats.seconds = Int(elapsed)
        }
        decayVisuals(step)
        updateParticles(step)
        updateTrail(step)
        guard running else { return }
        if draftRound < 6 && time >= nextDraftAt { openDraft(); return }
        if hitStop > 0 { hitStop = max(0, hitStop - step); return }
        eventCooldown = max(0, eventCooldown - step)

        if shieldTime > 0 {
            shieldTime = max(0, shieldTime - step)
            if shieldTime <= 0 { shield = false; onEvent?(.shieldBreak) }
        }
        if combo > 0 {
            comboTime -= step
            if comboTime <= 0 { combo = 0; multiplier = 1; fever = false }
        }
        slowTimer = max(0, slowTimer - step)
        doubleTimer = max(0, doubleTimer - step)
        magnetTimer = max(0, magnetTimer - step)
        phaseTimer = max(0, phaseTimer - step)
        freezeTimer = max(0, freezeTimer - step)
        invulnerable = max(0, invulnerable - step)

        speedLevel = min(1, CGFloat(score) / 400 + (mode == .survival ? time / 180 : 0))
        playerAngle += direction * (1.45 + speedLevel * 0.65) * step
        trail.append(TrailPoint(x: playerX, y: playerY, life: 1, color: skinColor))
        ghostTimer -= step
        if ghostTimer <= 0 && ghostCount < ghostBuffer.count {
            ghostBuffer[ghostCount] = playerAngle
            ghostCount += 1
            ghostTimer = 0.08
        }

        updateSpawns(step)
        updateHazards(step)
        updateCollisions(step)
        if status == .playing && !objectiveRewarded && objectiveDone() {
            objectiveRewarded = true
            resonanceFlash = 1
            addPoints(200)
        }
    }

    private func updateSpawns(_ step: CGFloat) {
        let s = Content.sector(sectorId)
        if stormWarning > 0 {
            stormWarning -= step
            if stormWarning <= 0 { spawnStorm() }
        }
        if flareWarning > 0 {
            flareWarning -= step
            if flareWarning <= 0 { spawnFlare() }
        }
        if mode == .zen {
            orbTimer -= step
            if orbTimer <= 0 { spawnOrb(); orbTimer = 1.6 + rnd() * 1.8 }
            powerTimer -= step
            if powerTimer <= 0 { spawnPowerUp(); powerTimer = 5 + rnd() * 4 }
            return
        }
        if level >= 2 {
            stormTimer -= step
            if stormTimer <= 0 && stormWarning <= 0 {
                stormWarning = 1.6
                stormTimer = 22 + rnd() * 12
                onEvent?(.storm)
            }
        }
        if s.hazards.contains(.flare) && level >= 6 {
            flareTimer -= step
            if flareTimer <= 0 && flareWarning <= 0 {
                flareWarning = 1.2
                flareTimer = 14 + rnd() * 10
                onEvent?(.flare)
            }
        }
        spawnTimer -= step
        if spawnTimer <= 0 && stormWarning <= 0 {
            spawnAsteroid()
            let base = 1.0 - min(0.35, CGFloat(score) * 0.0004)
            let floor: CGFloat = mode == .survival ? 0.40 : 0.55
            spawnTimer = min(max(base / (CGFloat(s.density) * seasonDensity * hazardDensity), floor), 1.1) * (0.85 + rnd() * 0.3)
        }
        orbTimer -= step
        if orbTimer <= 0 { spawnOrb(); orbTimer = 2.8 + rnd() * 2.5 }
        powerTimer -= step
        if powerTimer <= 0 { spawnPowerUp(); powerTimer = 6 + rnd() * 5 }
        if s.hazards.contains(.mine) && level >= 3 {
            mineTimer -= step
            if mineTimer <= 0 { spawnMine(); mineTimer = 7 + rnd() * 6 }
        }
        if s.hazards.contains(.wormhole) && level >= 4 {
            portalTimer -= step
            if portalTimer <= 0 { spawnWormholes(); portalTimer = 16 + rnd() * 10 }
        }
        if s.hazards.contains(.gravity) && level >= 5 {
            wellTimer -= step
            if wellTimer <= 0 {
                wells.append(GravityWell(angle: rnd() * tau, strength: 0.5 + rnd() * 0.7))
                wellTimer = 20 + rnd() * 12
            }
        }
        // Пороговый спавн: уровни могут перепрыгивать порог, но босс не теряется.
        while level >= nextBossLevel && status == .playing {
            spawnBoss()
            nextBossLevel += 5
        }
    }

    private func updateHazards(_ step: CGFloat) {
        if freezeTimer <= 0 {
            for w in wells {
                w.pulse += step * 2
                // Гравитация ускоряет полёт к орбите, но не сворачивает с радиального курса.
                for a in asteroids where a.kind != .bolt {
                    a.speed *= 1 + w.strength * step * 0.10
                }
            }
        }
        for w in wormholes {
            w.pulse += step * 3
            w.cooldown = max(0, w.cooldown - step)
        }
        for b in boss {
            b.time += step
            b.hitFlash = max(0, b.hitFlash - step * 4)
            b.charge += step
            if b.charge >= 2.4 / (1 + CGFloat(b.kind) * 0.22) {
                b.charge = 0
                let shots = 1 + min(2, max(0, b.kind))
                for k in -shots...shots {
                    asteroids.append(makeBolt(norm(b.angle + CGFloat(k) * (0.16 - CGFloat(shots) * 0.02))))
                }
                onEvent?(.bossHit)
            }
            if b.time >= b.duration { b.dead = true }
        }
        boss.removeAll { $0.dead }
    }

    private func updateCollisions(_ step: CGFloat) {
        let px = playerX
        let py = playerY
        let speedScale: CGFloat = (slowTimer > 0 ? 0.45 : (freezeTimer > 0 ? 0 : 1)) * hazardSpeed
        var hit: Asteroid?
        for a in asteroids {
            if a.dead { continue }
            a.pulse += step * 2
            let prevDistance = a.distance
            if freezeTimer <= 0 { a.distance -= a.speed * speedScale * step }
            a.rotation += a.spin * step
            a.life -= step
            if a.life <= 0 { a.dead = true; continue }
            let ax = centerX + cos(a.angle) * a.distance
            let ay = centerY + sin(a.angle) * a.distance
            let d = distance(ax, ay, px, py)
            let cd = a.radius + playerRadius
            let angleToPlayer = abs(angDiff(a.angle, playerAngle))
            if d < cd && invulnerable <= 0 && phaseTimer <= 0 { hit = a; break }
            // Близкий пролёт — только рядом с кораблём и на расстоянии «на волоске».
            if !a.nearMissed && d > cd && d < cd + playerRadius * 1.2 &&
                angleToPlayer < 0.16 && invulnerable <= 0 && a.kind != .bolt {
                a.nearMissed = true
                stats.nearMisses += 1
                rewardSkill(1)
                if a.dead { continue }
                flash = 1
                hitStop = reducedMotion ? 0 : 0.02
                onEvent?(.nearMiss)
            }
            // Уклонение — пересечение орбиты именно в секторе корабля.
            if !a.dodged && prevDistance >= orbitRadius && a.distance < orbitRadius &&
                angleToPlayer < 0.24 && a.kind != .bolt {
                a.dodged = true
                stats.dodges += 1
                rewardSkill(1)
                if a.dead { continue }
                onEvent?(.dodge(combo))
            }
            // Угроза завершает полёт на своей точке орбиты, а не в общем центре.
            if a.distance <= orbitRadius * 0.95 {
                burst(ax, ay, count: 6, color: Palette.asteroid, power: 0.45)
                a.dead = true
            }
        }
        asteroids.removeAll { $0.dead }
        if let hit { handleHit(hit); return }

        var i = 0
        while i < orbs.count {
            let o = orbs[i]
            o.pulse += step * 3
            if magnetTimer > 0 || upgradeMagnet {
                o.angle = norm(o.angle + angDiff(playerAngle, o.angle) * step * (upgradeMagnet ? 0.5 : 1.6))
            }
            let ox = centerX + cos(o.angle) * orbitRadius
            let oy = centerY + sin(o.angle) * orbitRadius
            if distance(ox, oy, px, py) < playerRadius * 2.4 {
                collectOrb(o)
                orbs.remove(at: i)
            } else { i += 1 }
        }

        i = 0
        while i < powerUps.count {
            let p = powerUps[i]
            p.pulse += step * 4
            let ox = centerX + cos(p.angle) * orbitRadius
            let oy = centerY + sin(p.angle) * orbitRadius
            if distance(ox, oy, px, py) < playerRadius * 2.8 {
                applyPowerUp(p.kind)
                burst(ox, oy, count: 18, color: powerColor(p.kind), power: 1)
                stats.powerups += 1
                onEvent?(.powerUp(p.kind))
                powerUps.remove(at: i)
            } else { i += 1 }
        }

        for w in wormholes where w.cooldown <= 0 {
            if abs(angDiff(w.angle, playerAngle)) < 0.10 {
                playerAngle = norm(w.partner + 0.35 * direction)
                w.cooldown = 2
                stats.wormholes += 1
                burst(px, py, count: 20, color: Palette.phase, power: 1)
                onEvent?(.wormhole)
            }
        }
        // Резонанс может сработать при сборе сферы/бонуса уже после основной уборки.
        asteroids.removeAll { $0.dead }
    }

    private func collectOrb(_ o: Orb) {
        chargeResonance(0.04)
        switch o.kind {
        case .normal:
            stats.orbs += 1; orbsCollected += 1
            rewardSkill(2 * orbBoost); onEvent?(.orb)
        case .golden:
            stats.orbs += 1; stats.goldenOrbs += 1; orbsCollected += 1
            rewardSkill(8 * orbBoost); onEvent?(.goldenOrb(combo))
        case .artifact:
            stats.rareOrbs += 1
            rewardSkill(25 * orbBoost); onEvent?(.artifact)
        }
        let ox = centerX + cos(o.angle) * orbitRadius
        let oy = centerY + sin(o.angle) * orbitRadius
        burst(ox, oy, count: 14, color: o.kind == .normal ? Palette.orb : Color(hex: 0xFFFFF3B0), power: 0.9)
        damageBoss(o.angle)
    }

    private func damageBoss(_ angle: CGFloat) {
        for b in boss {
            if abs(angDiff(b.angle, angle)) > 0.75 { continue }
            b.hitFlash = 1
            addPoints(40)
            onEvent?(.bossHit)
            if b.hp <= 1 {
                b.dead = true
                stats.bossKills += 1
                addPoints(150 + 10 * level)
                burst(centerX + cos(b.angle) * orbitRadius, centerY + sin(b.angle) * orbitRadius,
                      count: 60, color: Palette.boss, power: 2)
                onEvent?(.bossDown)
            } else { b.hp -= 1 }
            break
        }
    }

    private func activeCount() -> Int {
        (shield ? 1 : 0) + (slowTimer > 0 ? 1 : 0) + (doubleTimer > 0 ? 1 : 0) +
        (magnetTimer > 0 ? 1 : 0) + (phaseTimer > 0 ? 1 : 0) + (freezeTimer > 0 ? 1 : 0)
    }

    private func applyPowerUp(_ kind: PowerUpKind) {
        switch kind {
        case .shield: if activeCount() < 3 { shield = true; shieldTime = upgradeShield ? 14 : 10 }
        case .slow: if activeCount() < 3 { slowTimer = upgradeSlow ? 8 : 5 }
        case .double: if activeCount() < 3 { doubleTimer = upgradeDouble ? 12 : 8 }
        case .magnet: if activeCount() < 3 { magnetTimer = 8 }
        case .phase: if activeCount() < 3 { phaseTimer = 6 }
        case .freeze: if activeCount() < 3 { freezeTimer = 4 }
        case .revive: reviveAvailable = true
        }
    }

    /// Награда за навык: один счётчик на любое событие, чтобы очки не «капали».
    private func rewardSkill(_ base: Int) {
        chargeResonance(0.08)
        if eventCooldown > 0 { return }
        eventCooldown = 0.45
        bumpCombo()
        addPoints(base)
    }

    /// Резонанс: волна сбрасывает угрозы у орбиты и даёт бонус за каждую.
    private func chargeResonance(_ amount: CGFloat) {
        resonance = min(1, resonance + amount)
        if resonance >= 1 { triggerResonance() }
    }

    private func triggerResonance() {
        resonance = 0
        resonanceFlash = 1
        addPoints(80)
        var cleared = 0
        for a in asteroids {
            // Только помечаем: замена массива ломает текущий цикл столкновений.
            guard !a.dead, a.distance <= orbitRadius * 1.45, a.kind != .bolt else { continue }
            let x = centerX + cos(a.angle) * a.distance
            let y = centerY + sin(a.angle) * a.distance
            burst(x, y, count: 12, color: Palette.orb, power: 0.9)
            a.dead = true
            cleared += 1
        }
        if cleared > 0 { addPoints(cleared * 12) }
        onEvent?(.resonance)
    }

    private func bumpCombo() {
        combo += 1
        comboTime = 2.0
        multiplier = min(5, 1 + combo / 5)
        fever = multiplier >= 4
        if combo > maxCombo { maxCombo = combo; stats.maxCombo = combo }
    }

    private func addPoints(_ base: Int) {
        let boosted = max(1, Int(CGFloat(base) * scoreBoost + 0.5))
        let gained = boosted * multiplier * (doubleTimer > 0 ? 2 : 1)
        let sum = score.addingReportingOverflow(gained)
        score = sum.overflow ? Int.max / 4 : min(Int.max / 4, sum.partialValue)
        stats.score = score
        let next = 1 + score / 150
        if next > level {
            level = min(level + 1, next)
            stats.level = level
            levelUpFlash = 1
            onEvent?(.levelUp(level))
        }
        if score > best {
            best = score
            if !isNewRecord { isNewRecord = true; onNewRecord?(score) }
        }
    }

    /// Цель забега: та же формула на обеих платформах.
    func objective() -> Objective { Content.objective(mode, sectorId: sectorId) }

    func objectiveValue() -> Int { objective().field == "orbs" ? orbsCollected : score }

    func objectiveDone() -> Bool { objectiveValue() >= objective().target }

    /// Открыть выбор мутации: три разных предложения из общего пула.
    private func openDraft() {
        draftRound += 1
        var pool = Content.perks.indices.map { $0 }
        var options: [Int] = []
        for _ in 0..<3 {
            let at = Int(rnd() * CGFloat(pool.count))
            options.append(pool.remove(at: min(max(at, 0), pool.count - 1)))
        }
        draft = PerkDraft(options: options, round: draftRound)
    }

    /// Применить выбранную мутацию и продолжить полёт.
    func choosePerk(_ option: Int) {
        guard let current = draft, current.options.indices.contains(option) else { return }
        let index = current.options[option]
        let perk = Content.perks[index]
        perkIds.append(index)
        switch perk.id {
        case "overclock": scoreBoost += 0.25; hazardSpeed += 0.10
        case "barrier":
            scoreBoost = max(0.5, scoreBoost - 0.10)
            shield = true; shieldTime = max(shieldTime, 14)
        case "magnet": magnetTimer = max(magnetTimer, 10); hazardDensity += 0.10
        case "slowmo":
            scoreBoost = max(0.5, scoreBoost - 0.10)
            slowTimer = max(slowTimer, 10)
        case "pulse":
            triggerResonance(); combo = 0; multiplier = 1; comboTime = 0
        case "ghost":
            phaseTimer = max(phaseTimer, 8); invulnerable = max(invulnerable, 8)
            orbTimer += 4
        case "harvest": scoreBoost = max(0.5, scoreBoost - 0.10); orbBoost = 2
        case "chronos":
            hazardDensity += 0.15
            if timeLeft > 0 { timeLeft += 12 } else { addPoints(150) }
        default: break
        }
        nextDraftAt = time + 18
        paused = false
        draft = nil
    }

    private func handleHit(_ a: Asteroid) {
        stats.hits += 1
        // Любое попадание сбивает цепочку: комбо — навык, а не таймер.
        combo = 0; multiplier = 1; comboTime = 0; fever = false
        shieldTime = 0
        if phaseTimer > 0 {
            a.dead = true
            burst(playerX, playerY, count: 12, color: Palette.phase, power: 0.8)
        } else if shield {
            shield = false
            invulnerable = 1.6
            shieldFlash = 1
            if !reducedMotion { shake = 0.9 }
            hitStop = 0.06
            burst(playerX, playerY, count: 24, color: Palette.shield, power: 1.1)
            onEvent?(.shieldBreak)
        } else if reviveAvailable {
            reviveAvailable = false
            invulnerable = 2.4
            shieldFlash = 1
            burst(playerX, playerY, count: 40, color: Palette.revive, power: 1.6)
            onEvent?(.powerUp(.revive))
        } else if mode == .sprint {
            timeLeft = max(0, timeLeft - 3)
            flash = 1
            invulnerable = 1.2
            if !reducedMotion { shake = 0.7 }
            burst(playerX, playerY, count: 20, color: Palette.danger, power: 1)
            onEvent?(.crash)
            if timeLeft <= 0 { finishRun() }
        } else if mode == .zen {
            // Дзен безопасен.
        } else {
            crash()
        }
    }

    private func crash() {
        status = .gameOver
        paused = false
        if !reducedMotion { shake = 1 }
        hitStop = 0.09
        burst(playerX, playerY, count: 34, color: skinColor, power: 1.4)
        burst(playerX, playerY, count: 14, color: Palette.danger, power: 0.9)
        onRunEnd?(stats)
        onEvent?(.crash)
    }

    private func finishRun() {
        guard status != .gameOver else { return }
        status = .gameOver
        completed = true
        paused = false
        draft = nil
        stats.seconds = max(1, Int(elapsed))
        onRunEnd?(stats)
        onEvent?(.finish)
    }

    private func spawnAsteroid() {
        let hazards = Content.sector(sectorId).hazards
        let comet = level >= 2 && hazards.contains(.comet) && rnd() < 0.18
        asteroids.append(makeAsteroid(nextHazardAngle(), comet ? .comet : .rock))
    }

    private func spawnStorm() {
        let safe = rnd() * tau
        for _ in 0..<((mode == .survival ? 6 : 5) + (seasonDensity > 1.1 ? 1 : 0)) {
            var angle = nextHazardAngle()
            var tries = 0
            while abs(angDiff(angle, safe)) < 0.8 && tries < 24 {
                angle = nextHazardAngle()
                tries += 1
            }
            asteroids.append(makeAsteroid(angle, .rock))
        }
    }

    private func spawnFlare() {
        for _ in 0..<14 { asteroids.append(makeBolt(nextHazardAngle())) }
    }

    private func spawnBoss() {
        lastBossLevel = level
        let kind = (level / 5) % Content.bossNames.count
        let b = Boss(angle: nextHazardAngle(), hp: 2 + level / 20 + kind, duration: 26, kind: kind)
        boss.append(b)
        // Рядом с боссом появляются сферы — ядро реально можно сбить.
        for _ in 0..<2 { orbs.append(Orb(angle: norm(b.angle + (rnd() - 0.5) * 0.55), kind: .normal, sprite: 0)) }
        onEvent?(.bossSpawn)
        burst(centerX + cos(b.angle) * orbitRadius, centerY + sin(b.angle) * orbitRadius,
              count: 40, color: Palette.boss, power: 1.5)
    }

    private func spawnOrb() {
        let v = rnd()
        let kind: OrbKind = v < 0.04 ? .artifact : (v < 0.20 ? .golden : .normal)
        let sprite = kind == .normal ? 0 : (kind == .golden ? 1 : 2)
        // Золотой угол разносит новые сферы по разным сторонам орбиты.
        let angle = norm(orbSpawnCursor + (rnd() - 0.5) * 0.35)
        orbSpawnCursor = norm(orbSpawnCursor + 2.3999632)
        orbs.append(Orb(angle: angle, kind: kind, sprite: sprite))
    }

    private func spawnPowerUp() {
        // В Дзене нет астероидов: бесполезные щит/возрождение только занимают слот.
        let kinds = mode == .zen ? PowerUpKind.allCases.filter { $0 != .shield && $0 != .revive } : PowerUpKind.allCases
        let k = kinds[Int(rnd() * CGFloat(kinds.count)) % kinds.count]
        powerUps.append(PowerUp(angle: rnd() * tau, kind: k))
    }

    private func spawnMine() {
        let a = makeAsteroid(nextHazardAngle(), .mine)
        a.life = 16
        asteroids.append(a)
    }

    private func spawnWormholes() {
        let a = rnd() * tau
        wormholes = [
            Wormhole(angle: a, partner: norm(a + CGFloat.pi)),
            Wormhole(angle: norm(a + CGFloat.pi), partner: a),
        ]
    }

    private func makeAsteroid(_ angle: CGFloat, _ kind: AsteroidKind) -> Asteroid {
        let baseRadius = minDim * (0.026 + rnd() * 0.018)
        let baseSpeed = minDim * (0.30 + speedLevel * 0.25) * (0.85 + rnd() * 0.3)
        let comet = kind == .comet
        let a = Asteroid(
            angle: angle,
            distance: sqrt(width * width + height * height) * 0.5 + baseRadius,
            speed: comet ? baseSpeed * 1.9 : (kind == .mine ? baseSpeed * 0.35 : baseSpeed),
            radius: comet ? baseRadius * 0.7 : (kind == .mine ? baseRadius * 1.15 : baseRadius),
            kind: kind,
            sprite: Int(rnd() * 8) % 8
        )
        a.rotation = rnd() * tau
        a.spin = (rnd() - 0.5) * 1.6
        a.life = kind == .mine ? 16 : 6
        return a
    }

    private func makeBolt(_ angle: CGFloat) -> Asteroid {
        let a = Asteroid(angle: angle, distance: orbitRadius * 2.6, speed: minDim * 0.55,
                         radius: minDim * 0.010, kind: .bolt, sprite: 0)
        a.life = 5
        return a
    }

    private func decayVisuals(_ step: CGFloat) {
        shake = max(0, shake - step * 2.4)
        reverseFlash = max(0, reverseFlash - step * 3.5)
        flash = max(0, flash - step * 3.2)
        levelUpFlash = max(0, levelUpFlash - step * 0.9)
        shieldFlash = max(0, shieldFlash - step * 2.5)
        resonanceFlash = max(0, resonanceFlash - step * 1.8)
    }

    private func burst(_ x: CGFloat, _ y: CGFloat, count: Int, color: Color, power: CGFloat) {
        let n = reducedMotion ? max(4, count / 3) : count
        for _ in 0..<n {
            let angle = rnd() * tau
            let velocity = minDim * (0.05 + rnd() * 0.18) * power
            let life = 0.35 + rnd() * 0.55
            particles.append(Particle(
                x: x, y: y,
                vx: cos(angle) * velocity, vy: sin(angle) * velocity,
                life: life, maxLife: life, color: color,
                size: minDim * (0.002 + rnd() * 0.004),
                spin: (rnd() - 0.5) * 6
            ))
        }
        if particles.count > 420 { particles.removeFirst(particles.count - 420) }
    }

    private func updateParticles(_ dt: CGFloat) {
        for p in particles {
            p.life -= dt
            if p.life <= 0 { continue }
            p.x += p.vx * dt
            p.y += p.vy * dt
            p.vx *= 0.96
            p.vy *= 0.96
        }
        particles.removeAll { $0.life <= 0 }
    }

    private func updateTrail(_ dt: CGFloat) {
        for p in trail { p.life -= dt * 2.2 }
        trail.removeAll { $0.life <= 0 }
        if trail.count > 40 { trail.removeFirst(trail.count - 40) }
    }

    private func generateStars() {
        stars = (0..<180).map { _ in
            Star(x: rnd(), y: rnd(), size: minDim * (0.0012 + rnd() * 0.0032),
                 phase: rnd() * tau, speed: 0.5 + rnd() * 2.5, layer: Int(rnd() * 3))
        }
    }

    private func powerColor(_ kind: PowerUpKind) -> Color {
        switch kind {
        case .shield: return Palette.shield
        case .slow: return Palette.slow
        case .double: return Palette.double
        case .magnet: return Palette.magnet
        case .phase: return Palette.phase
        case .freeze: return Palette.freeze
        case .revive: return Palette.revive
        }
    }

    private func angDiff(_ a: CGFloat, _ b: CGFloat) -> CGFloat {
        var d = (a - b).truncatingRemainder(dividingBy: tau)
        if d > CGFloat.pi { d -= tau }
        if d < -CGFloat.pi { d += tau }
        return d
    }

    private func norm(_ a: CGFloat) -> CGFloat {
        var v = a.truncatingRemainder(dividingBy: tau)
        if v < 0 { v += tau }
        return v
    }

    /// Следующая угроза появляется в новой точке круга, а не рядом с предыдущей.
    private func nextHazardAngle() -> CGFloat {
        let a = norm(hazardSpawnCursor + (rnd() - 0.5) * 0.3)
        hazardSpawnCursor = norm(hazardSpawnCursor + 2.3999632)
        return a
    }

    private func distance(_ x1: CGFloat, _ y1: CGFloat, _ x2: CGFloat, _ y2: CGFloat) -> CGFloat {
        let dx = x1 - x2; let dy = y1 - y2
        return sqrt(dx * dx + dy * dy)
    }

    func achievedIds() -> [String] {
        var out: [String] = ["first_run"]
        func add(_ id: String, _ ok: Bool) { if ok { out.append(id) } }
        add("score_500", stats.score >= 500)
        add("score_1500", stats.score >= 1500)
        add("score_5000", stats.score >= 5000)
        add("combo_10", stats.maxCombo >= 10)
        add("combo_25", stats.maxCombo >= 25)
        add("orbs_50", stats.orbs >= 50)
        add("near_10", stats.nearMisses >= 10)
        add("near_30", stats.nearMisses >= 30)
        add("level_10", stats.level >= 10)
        add("level_20", stats.level >= 20)
        add("boss_1", stats.bossKills >= 1)
        add("boss_5", stats.bossKills >= 5)
        add("worm_5", stats.wormholes >= 5)
        add("objective_done", objectiveDone())
        return out
    }

    func secretIds() -> [String] {
        var out: [String] = []
        if stats.rareOrbs >= 3 { out.append("eye") }
        if stats.nearMisses >= 25 && stats.seconds <= 60 { out.append("dancer") }
        if stats.level >= 20 && stats.hits == 0 { out.append("survivor") }
        return out
    }

    func ghost() -> [CGFloat] { Array(ghostBuffer.prefix(ghostCount)) }

    private var rngState: UInt64 = 1
    /// Счётчик «золотого угла»: сферы равномерно распределяются по орбите, а не в одной точке.
    private var orbSpawnCursor: CGFloat = 0
    /// Угол следующей угрозы: метеориты приходят со всех сторон, а не с правого бока.
    private var hazardSpawnCursor: CGFloat = 0

    private func rnd() -> CGFloat {
        rngState = rngState &* 6364136223846793005 &+ 1442695040888963407
        let v = UInt32(truncatingIfNeeded: rngState >> 33) & 0xFFFFFF
        return CGFloat(v) / CGFloat(0x1000000)
    }

    private var tau: CGFloat { CGFloat.pi * 2 }
}
