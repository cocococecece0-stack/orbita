package com.example.orbit.game

import androidx.compose.ui.graphics.Color

enum class GameStatus { Ready, Playing, GameOver }

/** Neon palette shared by the engine (particles) and the renderer. */
object Palette {
    val Player = Color(0xFF22D3EE)
    val Orb = Color(0xFFFBBF24)
    val Asteroid = Color(0xFF9D8CFF)
    val Danger = Color(0xFFF472B6)
    val Shield = Color(0xFF4ADE80)
    val Slow = Color(0xFFA78BFA)
    val Double = Color(0xFFFB923C)
}

enum class PowerUpKind { Shield, Slow, Double }

/** Everything the UI layer needs to react to: sounds, haptics, banners. */
sealed interface GameEvent {
    data object Tap : GameEvent
    data class Dodge(val combo: Int) : GameEvent
    data object Orb : GameEvent
    data class PowerUp(val kind: PowerUpKind) : GameEvent
    data object ShieldBreak : GameEvent
    data object Crash : GameEvent
    data class LevelUp(val level: Int) : GameEvent
    data object NearMiss : GameEvent
}

class Asteroid(
    val angle: Float,
    var distance: Float,
    val speed: Float,
    val radius: Float,
    val shape: FloatArray,
    var rotation: Float,
    val spin: Float,
) {
    var dodged: Boolean = false
    var nearMissed: Boolean = false
}

class Orb(val angle: Float) {
    var pulse: Float = 0f
}

class PowerUp(val angle: Float, val kind: PowerUpKind) {
    var pulse: Float = 0f
}

class Particle(
    var x: Float,
    var y: Float,
    var vx: Float,
    var vy: Float,
    var life: Float,
    val maxLife: Float,
    val color: Color,
    val size: Float,
)

class Star(
    val x: Float,
    val y: Float,
    val size: Float,
    val phase: Float,
    val speed: Float,
)

class TrailPoint(val x: Float, val y: Float, var life: Float)
