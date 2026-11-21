import SwiftUI

enum GameStatus {
    case ready
    case playing
    case gameOver
}

enum Palette {
    static let player = Color(hex: 0x22D3EE)
    static let orb = Color(hex: 0xFBBF24)
    static let asteroid = Color(hex: 0x9D8CFF)
    static let danger = Color(hex: 0xF472B6)
    static let shield = Color(hex: 0x4ADE80)
    static let slow = Color(hex: 0xA78BFA)
    static let double = Color(hex: 0xFB923C)
}

enum PowerUpKind: CaseIterable {
    case shield
    case slow
    case double
}

enum AsteroidKind {
    case rock
    case comet
}

enum GameEvent {
    case tap
    case dodge(Int)
    case orb
    case powerUp(PowerUpKind)
    case shieldBreak
    case crash
    case levelUp(Int)
    case nearMiss
    case storm
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

final class Asteroid {
    let angle: CGFloat
    var distance: CGFloat
    let speed: CGFloat
    let radius: CGFloat
    let shape: [CGFloat]
    var rotation: CGFloat
    let spin: CGFloat
    let kind: AsteroidKind
    var dodged = false
    var nearMissed = false
    var dead = false

    init(
        angle: CGFloat,
        distance: CGFloat,
        speed: CGFloat,
        radius: CGFloat,
        shape: [CGFloat],
        rotation: CGFloat,
        spin: CGFloat,
        kind: AsteroidKind = .rock
    ) {
        self.angle = angle
        self.distance = distance
        self.speed = speed
        self.radius = radius
        self.shape = shape
        self.rotation = rotation
        self.spin = spin
        self.kind = kind
    }
}

final class Orb {
    let angle: CGFloat
    var pulse: CGFloat = 0
    var dead = false

    init(angle: CGFloat) {
        self.angle = angle
    }
}

final class PowerUp {
    let angle: CGFloat
    let kind: PowerUpKind
    var pulse: CGFloat = 0
    var dead = false

    init(angle: CGFloat, kind: PowerUpKind) {
        self.angle = angle
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

    init(
        x: CGFloat,
        y: CGFloat,
        vx: CGFloat,
        vy: CGFloat,
        life: CGFloat,
        maxLife: CGFloat,
        color: Color,
        size: CGFloat
    ) {
        self.x = x
        self.y = y
        self.vx = vx
        self.vy = vy
        self.life = life
        self.maxLife = maxLife
        self.color = color
        self.size = size
    }
}

struct Star {
    let x: CGFloat
    let y: CGFloat
    let size: CGFloat
    let phase: CGFloat
    let speed: CGFloat
}

final class TrailPoint {
    let x: CGFloat
    let y: CGFloat
    var life: CGFloat

    init(x: CGFloat, y: CGFloat, life: CGFloat) {
        self.x = x
        self.y = y
        self.life = life
    }
}
