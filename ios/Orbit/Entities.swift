import SwiftUI

enum GameStatus { case ready, playing, gameOver }

/// Одно предложение мутации: индексы из Content.perks.
struct PerkDraft {
    let options: [Int]
    let round: Int
}

enum Palette {
    static let player = Color(hex: 0x22D3EE)
    static let orb = Color(hex: 0xFBBF24)
    static let asteroid = Color(hex: 0x9D8CFF)
    static let danger = Color(hex: 0xF472B6)
    static let shield = Color(hex: 0x4ADE80)
    static let slow = Color(hex: 0xA78BFA)
    static let double = Color(hex: 0xFB923C)
    static let magnet = Color(hex: 0xFFD654)
    static let phase = Color(hex: 0x78E8FF)
    static let freeze = Color(hex: 0xFF8CBE)
    static let revive = Color(hex: 0xFFECC8)
    static let boss = Color(hex: 0xFFFF5C70)
}

enum AsteroidKind { case rock, comet, mine, bolt }
enum OrbKind { case normal, golden, artifact }

enum GameEvent {
    case tap
    case blockMove(Int)
    case blockRotate
    case blockHold
    case blockDrop(Int)
    case blockClear(Int)
    case blockTopOut
    case dodge(Int)
    case orb
    case goldenOrb(Int)
    case artifact
    case powerUp(PowerUpKind)
    case shieldBreak
    case crash
    case levelUp(Int)
    case nearMiss
    case storm
    case wormhole
    case flare
    case bossSpawn
    case bossHit
    case bossDown
    case resonance
    case finish
    case tick
}

final class Asteroid {
    var angle: CGFloat
    var distance: CGFloat
    var speed: CGFloat
    let radius: CGFloat
    let kind: AsteroidKind
    let sprite: Int
    var rotation: CGFloat = 0
    var spin: CGFloat = 0
    var dodged = false
    var nearMissed = false
    var dead = false
    var life: CGFloat = 6
    var pulse: CGFloat = 0

    init(angle: CGFloat, distance: CGFloat, speed: CGFloat, radius: CGFloat, kind: AsteroidKind, sprite: Int) {
        self.angle = angle
        self.distance = distance
        self.speed = speed
        self.radius = radius
        self.kind = kind
        self.sprite = sprite
    }
}

final class Orb {
    var angle: CGFloat
    let kind: OrbKind
    let sprite: Int
    var pulse: CGFloat = 0
    var dead = false

    init(angle: CGFloat, kind: OrbKind, sprite: Int) {
        self.angle = angle
        self.kind = kind
        self.sprite = sprite
    }
}

final class PowerUp {
    let angle: CGFloat
    let kind: PowerUpKind
    var pulse: CGFloat = 0

    init(angle: CGFloat, kind: PowerUpKind) {
        self.angle = angle
        self.kind = kind
    }
}

final class Wormhole {
    let angle: CGFloat
    let partner: CGFloat
    var pulse: CGFloat = 0
    var cooldown: CGFloat = 0

    init(angle: CGFloat, partner: CGFloat) {
        self.angle = angle
        self.partner = partner
    }
}

final class GravityWell {
    let angle: CGFloat
    let strength: CGFloat
    var pulse: CGFloat = 0

    init(angle: CGFloat, strength: CGFloat) {
        self.angle = angle
        self.strength = strength
    }
}

final class Boss {
    let angle: CGFloat
    var hp: Int
    let duration: CGFloat
    let kind: Int
    var time: CGFloat = 0
    var charge: CGFloat = 0
    var hitFlash: CGFloat = 0
    var dead = false

    init(angle: CGFloat, hp: Int, duration: CGFloat, kind: Int = 0) {
        self.angle = angle
        self.hp = hp
        self.duration = duration
        self.kind = kind
    }
}

final class Particle {
    var x: CGFloat
    var y: CGFloat
    var vx: CGFloat
    var vy: CGFloat
    var life: CGFloat
    let maxLife: CGFloat
    let color: Color
    let size: CGFloat
    let spin: CGFloat

    init(x: CGFloat, y: CGFloat, vx: CGFloat, vy: CGFloat, life: CGFloat,
         maxLife: CGFloat, color: Color, size: CGFloat, spin: CGFloat = 0) {
        self.x = x; self.y = y; self.vx = vx; self.vy = vy
        self.life = life; self.maxLife = maxLife
        self.color = color; self.size = size; self.spin = spin
    }
}

struct Star {
    let x: CGFloat
    let y: CGFloat
    let size: CGFloat
    let phase: CGFloat
    let speed: CGFloat
    let layer: Int
}

final class TrailPoint {
    let x: CGFloat
    let y: CGFloat
    var life: CGFloat
    let color: Color

    init(x: CGFloat, y: CGFloat, life: CGFloat, color: Color) {
        self.x = x; self.y = y; self.life = life; self.color = color
    }
}

struct RunStats {
    var score = 0
    var level = 1
    var orbs = 0
    var goldenOrbs = 0
    var rareOrbs = 0
    var maxCombo = 0
    var nearMisses = 0
    var dodges = 0
    var bossKills = 0
    var wormholes = 0
    var seconds = 0
    var powerups = 0
    var hits = 0
}

extension Color {
    init(hex: UInt32) {
        self.init(
            .sRGB,
            red: Double((hex >> 16) & 0xFF) / 255.0,
            green: Double((hex >> 8) & 0xFF) / 255.0,
            blue: Double(hex & 0xFF) / 255.0,
            opacity: 1.0
        )
    }
}
