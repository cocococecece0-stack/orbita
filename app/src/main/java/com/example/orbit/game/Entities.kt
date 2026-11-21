package com.example.orbit.game

import androidx.compose.ui.graphics.Color

enum class GameStatus { Ready, Playing, GameOver }

/** Одно предложение мутации: индексы из [Content.perks]. */
data class PerkDraft(val options: List<Int>, val round: Int)

object Palette {
    val Player = Color(0xFF22D3EE)
    val Orb = Color(0xFFFBBF24)
    val Asteroid = Color(0xFF9D8CFF)
    val Danger = Color(0xFFF472B6)
    val Shield = Color(0xFF4ADE80)
    val Slow = Color(0xFFA78BFA)
    val Double = Color(0xFFFB923C)
    val Magnet = Color(0xFFFFD654)
    val Phase = Color(0xFF78E8FF)
    val Freeze = Color(0xFFFF8CBE)
    val Revive = Color(0xFFFFECC8)
    val Boss = Color(0xFFFF5C70)
}

enum class AsteroidKind { Rock, Comet, Mine, Bolt }

enum class OrbKind { Normal, Golden, Artifact }

sealed interface GameEvent {
    data object Tap : GameEvent
    data class BlockMove(val direction: Int) : GameEvent
    data object BlockRotate : GameEvent
    data object BlockHold : GameEvent
    data class BlockDrop(val cells: Int) : GameEvent
    data class BlockClear(val lines: Int) : GameEvent
    data object BlockTopOut : GameEvent
    data class Dodge(val combo: Int) : GameEvent
    data object Orb : GameEvent
    data class GoldenOrb(val combo: Int) : GameEvent
    data object Artifact : GameEvent
    data class PowerUp(val kind: PowerUpKind) : GameEvent
    data object ShieldBreak : GameEvent
    data object Crash : GameEvent
    data class LevelUp(val level: Int) : GameEvent
    data object NearMiss : GameEvent
    data object Storm : GameEvent
    data object Wormhole : GameEvent
    data object Flare : GameEvent
    data object BossSpawn : GameEvent
    data object BossHit : GameEvent
    data object BossDown : GameEvent
    data object Resonance : GameEvent
    data object Finish : GameEvent
    data object Tick : GameEvent
}

class Asteroid(
    var angle: Float,
    var distance: Float,
    var speed: Float,
    val radius: Float,
    val kind: AsteroidKind,
    val sprite: Int,
) {
    var rotation = 0f
    var spin = 0f
    var dodged = false
    var nearMissed = false
    var dead = false
    var life = 6f
    var pulse = 0f
}

class Orb(var angle: Float, val kind: OrbKind, val sprite: Int) {
    var pulse = 0f
    var dead = false
    var drift = 0f
}

class PowerUp(val angle: Float, val kind: PowerUpKind) {
    var pulse = 0f
    var dead = false
}

class Wormhole(val angle: Float, val partner: Float) {
    var pulse = 0f
    var cooldown = 0f
}

class GravityWell(val angle: Float, val strength: Float) {
    var pulse = 0f
}

class Boss(val angle: Float, var hp: Int, val duration: Float, val kind: Int = 0) {
    var time = 0f
    var charge = 0f
    var hitFlash = 0f
    var dead = false
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
    val spin: Float = 0f,
)

class Star(
    val x: Float,
    val y: Float,
    val size: Float,
    val phase: Float,
    val speed: Float,
    val layer: Int,
)

class TrailPoint(val x: Float, val y: Float, var life: Float, val color: Color)

class RunStats(
    var score: Int = 0,
    var level: Int = 1,
    var orbs: Int = 0,
    var goldenOrbs: Int = 0,
    var rareOrbs: Int = 0,
    var maxCombo: Int = 0,
    var nearMisses: Int = 0,
    var dodges: Int = 0,
    var bossKills: Int = 0,
    var wormholes: Int = 0,
    var seconds: Int = 0,
    var powerups: Int = 0,
    var hits: Int = 0,
)
