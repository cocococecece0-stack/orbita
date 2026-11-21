import SwiftUI

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
    @Published var frameTick = 0

    var onEvent: ((GameEvent) -> Void)?
    var onNewRecord: ((Int) -> Void)?

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
    private(set) var playerAngle: CGFloat = -CGFloat.pi / 2
    private(set) var direction: CGFloat = 1

    var asteroids: [Asteroid] = []
    var orbs: [Orb] = []
    var powerUps: [PowerUp] = []
    var particles: [Particle] = []
    var stars: [Star] = []
    var trail: [TrailPoint] = []

    private let tau = CGFloat.pi * 2
    private var minDim: CGFloat = 0
    private var spawnTimer: CGFloat = 0
    private var orbTimer: CGFloat = 0
    private var powerTimer: CGFloat = 0
    private var comboTime: CGFloat = 0
    private var speedLevel: CGFloat = 0

    var playerX: CGFloat { centerX + cos(playerAngle) * orbitRadius }
    var playerY: CGFloat { centerY + sin(playerAngle) * orbitRadius }

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

    func start() {
        asteroids.removeAll()
        orbs.removeAll()
        powerUps.removeAll()
        particles.removeAll()
        trail.removeAll()
        score = 0
        isNewRecord = false
        paused = false
        combo = 0
        multiplier = 1
        level = 1
        shield = false
        comboTime = 0
        slowTimer = 0
        doubleTimer = 0
        invulnerable = 0
        hitStop = 0
        flash = 0
        levelUpFlash = 0
        shieldFlash = 0
        playerAngle = -CGFloat.pi / 2
        direction = 1
        spawnTimer = 0.7
        orbTimer = 3.5
        powerTimer = 6
        time = 0
        shake = 0
        reverseFlash = 0
        speedLevel = 0
        status = .playing
    }

    func tap() {
        onEvent?(.tap)
        if paused {
            paused = false
            return
        }
        switch status {
        case .ready:
            start()
        case .playing:
            direction = -direction
            reverseFlash = 1
        case .gameOver:
            break
        }
    }

    func pause() {
        if status == .playing { paused = true }
    }

    func tick(dt: CGFloat) {
        defer { frameTick &+= 1 }
        guard width > 0 else { return }

        let step = min(max(dt, 0), 0.05)
        time += step
        decayVisuals(step)
        updateParticles(step)
        updateTrail(step)

        if hitStop > 0 {
            hitStop = max(0, hitStop - step)
            return
        }
        guard status == .playing, !paused else { return }

        if combo > 0 {
            comboTime -= step
            if comboTime <= 0 {
                combo = 0
                multiplier = 1
            }
        }
        if slowTimer > 0 { slowTimer = max(0, slowTimer - step) }
        if doubleTimer > 0 { doubleTimer = max(0, doubleTimer - step) }
        if invulnerable > 0 { invulnerable = max(0, invulnerable - step) }

        speedLevel = min(1, CGFloat(score) / 80)
        playerAngle += direction * (1.45 + speedLevel * 0.65) * step
        trail.append(TrailPoint(x: playerX, y: playerY, life: 1))

        spawnTimer -= step
        if spawnTimer <= 0 {
            spawnAsteroid()
            let base = 1.05 - CGFloat(score) * 0.015
            spawnTimer = min(max(base, 0.30), 1.05) * (0.75 + CGFloat.random(in: 0...1) * 0.5)
        }

        orbTimer -= step
        if orbTimer <= 0 {
            orbs.append(Orb(angle: CGFloat.random(in: 0...tau)))
            orbTimer = 3.5 + CGFloat.random(in: 0...1) * 2.5
        }

        powerTimer -= step
        if powerTimer <= 0 {
            let kinds = PowerUpKind.allCases
            let kind = kinds[Int.random(in: 0..<kinds.count)]
            powerUps.append(PowerUp(angle: CGFloat.random(in: 0...tau), kind: kind))
            powerTimer = 7 + CGFloat.random(in: 0...1) * 5
        }

        let px = playerX
        let py = playerY
        let speedScale: CGFloat = slowTimer > 0 ? 0.45 : 1
        var hit: Asteroid?

        for asteroid in asteroids {
            asteroid.distance -= asteroid.speed * speedScale * step
            asteroid.rotation += asteroid.spin * step

            let ax = centerX + cos(asteroid.angle) * asteroid.distance
            let ay = centerY + sin(asteroid.angle) * asteroid.distance
            let distanceToPlayer = distance(ax, ay, px, py)
            let collisionDistance = asteroid.radius + playerRadius

            if distanceToPlayer < collisionDistance, invulnerable <= 0 {
                hit = asteroid
                break
            }
            if !asteroid.nearMissed,
               distanceToPlayer < collisionDistance + playerRadius * 2.1,
               invulnerable <= 0 {
                asteroid.nearMissed = true
                bumpCombo()
                addPoints(2)
                flash = 1
                hitStop = 0.035
                onEvent?(.nearMiss)
            }
            if !asteroid.dodged, asteroid.distance < orbitRadius - asteroid.radius {
                asteroid.dodged = true
                bumpCombo()
                addPoints(1)
                onEvent?(.dodge(combo))
            }
            if asteroid.distance < planetRadius * 0.35 {
                burst(ax, ay, count: 6, color: Palette.asteroid, power: 0.45)
                asteroid.dead = true
            }
        }
        asteroids.removeAll { $0.dead }

        if let hit {
            handleHit(hit)
            return
        }

        for orb in orbs {
            orb.pulse += step * 3
            let ox = centerX + cos(orb.angle) * orbitRadius
            let oy = centerY + sin(orb.angle) * orbitRadius
            if distance(ox, oy, px, py) < playerRadius * 2.4 {
                bumpCombo()
                addPoints(3)
                burst(ox, oy, count: 14, color: Palette.orb, power: 0.9)
                onEvent?(.orb)
                orb.dead = true
            }
        }
        orbs.removeAll { $0.dead }

        for powerUp in powerUps {
            powerUp.pulse += step * 4
            let ox = centerX + cos(powerUp.angle) * orbitRadius
            let oy = centerY + sin(powerUp.angle) * orbitRadius
            if distance(ox, oy, px, py) < playerRadius * 2.8 {
                applyPowerUp(powerUp.kind)
                burst(ox, oy, count: 18, color: powerColor(powerUp.kind), power: 1)
                onEvent?(.powerUp(powerUp.kind))
                powerUp.dead = true
            }
        }
        powerUps.removeAll { $0.dead }
    }

    private func applyPowerUp(_ kind: PowerUpKind) {
        switch kind {
        case .shield: shield = true
        case .slow: slowTimer = 5
        case .double: doubleTimer = 8
        }
    }

    private func bumpCombo() {
        combo += 1
        comboTime = 2.5
        multiplier = min(5, 1 + combo / 3)
    }

    private func addPoints(_ base: Int) {
        let factor = doubleTimer > 0 ? 2 : 1
        score += base * multiplier * factor
        let nextLevel = 1 + score / 50
        if nextLevel > level {
            level = nextLevel
            levelUpFlash = 1
            onEvent?(.levelUp(level))
        }
    }

    private func handleHit(_ asteroid: Asteroid) {
        if shield {
            shield = false
            invulnerable = 1.6
            shieldFlash = 1
            shake = 0.9
            hitStop = 0.06
            let px = playerX
            let py = playerY
            burst(px, py, count: 24, color: Palette.shield, power: 1.1)
            asteroids.removeAll { other in
                let ox = centerX + cos(other.angle) * other.distance
                let oy = centerY + sin(other.angle) * other.distance
                return distance(ox, oy, px, py) < orbitRadius * 0.55
            }
            onEvent?(.shieldBreak)
            return
        }
        gameOver()
    }

    private func gameOver() {
        status = .gameOver
        paused = false
        shake = 1
        hitStop = 0.09
        burst(playerX, playerY, count: 34, color: Palette.player, power: 1.4)
        burst(playerX, playerY, count: 14, color: Palette.danger, power: 0.9)
        if score > best {
            best = score
            isNewRecord = true
            onNewRecord?(score)
        }
        onEvent?(.crash)
    }

    private func spawnAsteroid() {
        let radius = minDim * (0.028 + CGFloat.random(in: 0...1) * 0.02)
        let speed = minDim * (0.30 + speedLevel * 0.25) * (0.85 + CGFloat.random(in: 0...1) * 0.3)
        let spawnDistance = sqrt(width * width + height * height) * 0.5 + radius
        asteroids.append(
            Asteroid(
                angle: CGFloat.random(in: 0...tau),
                distance: spawnDistance,
                speed: speed,
                radius: radius,
                shape: (0..<9).map { _ in 0.78 + CGFloat.random(in: 0...1) * 0.4 },
                rotation: CGFloat.random(in: 0...tau),
                spin: (CGFloat.random(in: 0...1) - 0.5) * 1.6
            )
        )
    }

    private func decayVisuals(_ step: CGFloat) {
        if shake > 0 { shake = max(0, shake - step * 2.4) }
        if reverseFlash > 0 { reverseFlash = max(0, reverseFlash - step * 3.5) }
        if flash > 0 { flash = max(0, flash - step * 3.2) }
        if levelUpFlash > 0 { levelUpFlash = max(0, levelUpFlash - step * 0.9) }
        if shieldFlash > 0 { shieldFlash = max(0, shieldFlash - step * 2.5) }
    }

    private func burst(_ x: CGFloat, _ y: CGFloat, count: Int, color: Color, power: CGFloat) {
        for _ in 0..<count {
            let angle = CGFloat.random(in: 0...tau)
            let velocity = minDim * (0.05 + CGFloat.random(in: 0...1) * 0.18) * power
            let life = 0.35 + CGFloat.random(in: 0...1) * 0.55
            particles.append(
                Particle(
                    x: x,
                    y: y,
                    vx: cos(angle) * velocity,
                    vy: sin(angle) * velocity,
                    life: life,
                    maxLife: life,
                    color: color,
                    size: minDim * (0.002 + CGFloat.random(in: 0...1) * 0.004)
                )
            )
        }
    }

    private func updateParticles(_ dt: CGFloat) {
        for particle in particles {
            particle.life -= dt
            if particle.life <= 0 { continue }
            particle.x += particle.vx * dt
            particle.y += particle.vy * dt
            particle.vx *= 0.96
            particle.vy *= 0.96
        }
        particles.removeAll { $0.life <= 0 }
    }

    private func updateTrail(_ dt: CGFloat) {
        for point in trail {
            point.life -= dt * 2.2
        }
        trail.removeAll { $0.life <= 0 }
        if trail.count > 40 {
            trail.removeFirst(trail.count - 40)
        }
    }

    private func generateStars() {
        stars = (0..<150).map { _ in
            Star(
                x: CGFloat.random(in: 0...1),
                y: CGFloat.random(in: 0...1),
                size: minDim * (0.0012 + CGFloat.random(in: 0...1) * 0.0032),
                phase: CGFloat.random(in: 0...tau),
                speed: 0.5 + CGFloat.random(in: 0...1) * 2.5
            )
        }
    }

    private func powerColor(_ kind: PowerUpKind) -> Color {
        switch kind {
        case .shield: return Palette.shield
        case .slow: return Palette.slow
        case .double: return Palette.double
        }
    }

    private func distance(_ x1: CGFloat, _ y1: CGFloat, _ x2: CGFloat, _ y2: CGFloat) -> CGFloat {
        let dx = x1 - x2
        let dy = y1 - y2
        return sqrt(dx * dx + dy * dy)
    }
}
