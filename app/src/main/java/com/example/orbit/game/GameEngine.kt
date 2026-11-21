package com.example.orbit.game

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt
import kotlin.random.Random

/**
 * Pure game simulation: orbiting player, incoming asteroids, orbs, power-ups
 * and a particle system. Coordinates are pixels relative to the canvas size.
 */
class GameEngine {

    var status by mutableStateOf(GameStatus.Ready)
        private set
    var score by mutableIntStateOf(0)
        private set
    var best by mutableIntStateOf(0)
    var isNewRecord by mutableStateOf(false)
        private set
    var paused by mutableStateOf(false)
        private set

    var combo by mutableIntStateOf(0)
        private set
    var multiplier by mutableIntStateOf(1)
        private set
    var level by mutableIntStateOf(1)
        private set
    var shield by mutableStateOf(false)
        private set

    var onEvent: (GameEvent) -> Unit = {}
    var onNewRecord: (Int) -> Unit = {}

    var width = 0f
        private set
    var height = 0f
        private set
    var centerX = 0f
        private set
    var centerY = 0f
        private set
    var orbitRadius = 0f
        private set
    var planetRadius = 0f
        private set
    var playerRadius = 0f
        private set

    var time = 0f
        private set
    var shake = 0f
        private set
    var flash = 0f
        private set
    var levelUpFlash = 0f
        private set
    var shieldFlash = 0f
        private set
    var reverseFlash = 0f
        private set
    var hitStop = 0f
        private set
    var invulnerable = 0f
        private set
    var slowTimer = 0f
        private set
    var doubleTimer = 0f
        private set
    var playerAngle = -PI.toFloat() / 2f
        private set
    var direction = 1
        private set
    var speedLevel = 0f
        private set

    val asteroids = mutableListOf<Asteroid>()
    val orbs = mutableListOf<Orb>()
    val powerUps = mutableListOf<PowerUp>()
    val particles = mutableListOf<Particle>()
    val stars = mutableListOf<Star>()
    val trail = ArrayDeque<TrailPoint>()

    private val random = Random.Default
    private var lastWidth = -1
    private var lastHeight = -1
    private var minDim = 0f
    private var spawnTimer = 0f
    private var orbTimer = 0f
    private var powerTimer = 0f
    private var comboTime = 0f

    fun resize(w: Int, h: Int) {
        if (w <= 0 || h <= 0) return
        if (w == lastWidth && h == lastHeight) return
        lastWidth = w
        lastHeight = h
        width = w.toFloat()
        height = h.toFloat()
        centerX = width / 2f
        centerY = height / 2f
        minDim = min(width, height)
        orbitRadius = minDim * 0.31f
        planetRadius = minDim * 0.16f
        playerRadius = minDim * 0.017f
        generateStars()
    }

    fun playerX(): Float = centerX + cos(playerAngle) * orbitRadius

    fun playerY(): Float = centerY + sin(playerAngle) * orbitRadius

    fun start() {
        asteroids.clear()
        orbs.clear()
        powerUps.clear()
        particles.clear()
        trail.clear()
        score = 0
        best = max(best, score)
        isNewRecord = false
        paused = false
        combo = 0
        multiplier = 1
        level = 1
        shield = false
        comboTime = 0f
        slowTimer = 0f
        doubleTimer = 0f
        invulnerable = 0f
        hitStop = 0f
        flash = 0f
        levelUpFlash = 0f
        shieldFlash = 0f
        playerAngle = -PI.toFloat() / 2f
        direction = 1
        spawnTimer = 0.7f
        orbTimer = 3.5f
        powerTimer = 6f
        time = 0f
        shake = 0f
        reverseFlash = 0f
        speedLevel = 0f
        status = GameStatus.Playing
    }

    fun tap() {
        onEvent(GameEvent.Tap)
        when {
            paused -> paused = false
            status == GameStatus.Ready -> start()
            status == GameStatus.Playing -> {
                direction = -direction
                reverseFlash = 1f
            }
        }
    }

    fun pause() {
        if (status == GameStatus.Playing) paused = true
    }

    fun update(dt: Float) {
        val step = dt.coerceIn(0f, 0.05f)
        time += step
        decayVisuals(step)
        updateParticles(step)
        updateTrail(step)

        if (hitStop > 0f) {
            hitStop = max(0f, hitStop - step)
            return
        }
        if (status != GameStatus.Playing || paused) return

        if (combo > 0) {
            comboTime -= step
            if (comboTime <= 0f) {
                combo = 0
                multiplier = 1
            }
        }
        if (slowTimer > 0f) slowTimer = max(0f, slowTimer - step)
        if (doubleTimer > 0f) doubleTimer = max(0f, doubleTimer - step)
        if (invulnerable > 0f) invulnerable = max(0f, invulnerable - step)

        speedLevel = min(1f, score / 80f)
        playerAngle += direction * (1.45f + speedLevel * 0.65f) * step
        trail.addLast(TrailPoint(playerX(), playerY(), 1f))

        spawnTimer -= step
        if (spawnTimer <= 0f) {
            spawnAsteroid()
            val base = 1.05f - score * 0.015f
            spawnTimer = base.coerceIn(0.30f, 1.05f) * (0.75f + random.nextFloat() * 0.5f)
        }

        orbTimer -= step
        if (orbTimer <= 0f) {
            orbs += Orb(random.nextFloat() * TAU)
            orbTimer = 3.5f + random.nextFloat() * 2.5f
        }

        powerTimer -= step
        if (powerTimer <= 0f) {
            val kinds = PowerUpKind.entries
            powerUps += PowerUp(random.nextFloat() * TAU, kinds[random.nextInt(kinds.size)])
            powerTimer = 7f + random.nextFloat() * 5f
        }

        val px = playerX()
        val py = playerY()
        val speedScale = if (slowTimer > 0f) 0.45f else 1f

        val toRemove = mutableListOf<Asteroid>()
        var hit: Asteroid? = null
        for (asteroid in asteroids) {
            asteroid.distance -= asteroid.speed * speedScale * step
            asteroid.rotation += asteroid.spin * step

            val ax = centerX + cos(asteroid.angle) * asteroid.distance
            val ay = centerY + sin(asteroid.angle) * asteroid.distance
            val distanceToPlayer = distance(ax, ay, px, py)
            val collisionDistance = asteroid.radius + playerRadius

            if (distanceToPlayer < collisionDistance && invulnerable <= 0f) {
                hit = asteroid
                break
            }
            if (!asteroid.nearMissed &&
                distanceToPlayer < collisionDistance + playerRadius * 2.1f &&
                invulnerable <= 0f
            ) {
                asteroid.nearMissed = true
                bumpCombo()
                addPoints(2)
                flash = 1f
                hitStop = 0.035f
                onEvent(GameEvent.NearMiss)
            }
            if (!asteroid.dodged && asteroid.distance < orbitRadius - asteroid.radius) {
                asteroid.dodged = true
                bumpCombo()
                addPoints(1)
                onEvent(GameEvent.Dodge(combo))
            }
            if (asteroid.distance < planetRadius * 0.35f) {
                burst(ax, ay, 6, Palette.Asteroid, 0.45f)
                toRemove += asteroid
            }
        }
        asteroids.removeAll(toRemove)

        if (hit != null) {
            handleHit(hit)
            return
        }

        val orbIterator = orbs.iterator()
        while (orbIterator.hasNext()) {
            val orb = orbIterator.next()
            orb.pulse += step * 3f
            val ox = centerX + cos(orb.angle) * orbitRadius
            val oy = centerY + sin(orb.angle) * orbitRadius
            if (distance(ox, oy, px, py) < playerRadius * 2.4f) {
                bumpCombo()
                addPoints(3)
                burst(ox, oy, 14, Palette.Orb, 0.9f)
                onEvent(GameEvent.Orb)
                orbIterator.remove()
            }
        }

        val powerIterator = powerUps.iterator()
        while (powerIterator.hasNext()) {
            val powerUp = powerIterator.next()
            powerUp.pulse += step * 4f
            val ox = centerX + cos(powerUp.angle) * orbitRadius
            val oy = centerY + sin(powerUp.angle) * orbitRadius
            if (distance(ox, oy, px, py) < playerRadius * 2.8f) {
                applyPowerUp(powerUp.kind)
                burst(ox, oy, 18, powerColor(powerUp.kind), 1f)
                onEvent(GameEvent.PowerUp(powerUp.kind))
                powerIterator.remove()
            }
        }
    }

    private fun applyPowerUp(kind: PowerUpKind) {
        when (kind) {
            PowerUpKind.Shield -> shield = true
            PowerUpKind.Slow -> slowTimer = 5f
            PowerUpKind.Double -> doubleTimer = 8f
        }
    }

    private fun bumpCombo() {
        combo += 1
        comboTime = 2.5f
        multiplier = min(5, 1 + combo / 3)
    }

    private fun addPoints(base: Int) {
        val factor = if (doubleTimer > 0f) 2 else 1
        score += base * multiplier * factor
        val nextLevel = 1 + score / 50
        if (nextLevel > level) {
            level = nextLevel
            levelUpFlash = 1f
            onEvent(GameEvent.LevelUp(level))
        }
    }

    private fun handleHit(asteroid: Asteroid) {
        if (shield) {
            shield = false
            invulnerable = 1.6f
            shieldFlash = 1f
            shake = 0.9f
            hitStop = 0.06f
            val px = playerX()
            val py = playerY()
            burst(px, py, 24, Palette.Shield, 1.1f)
            asteroids.removeAll { other ->
                val ox = centerX + cos(other.angle) * other.distance
                val oy = centerY + sin(other.angle) * other.distance
                distance(ox, oy, px, py) < orbitRadius * 0.55f
            }
            onEvent(GameEvent.ShieldBreak)
            return
        }
        gameOver()
    }

    private fun gameOver() {
        status = GameStatus.GameOver
        paused = false
        shake = 1f
        hitStop = 0.09f
        burst(playerX(), playerY(), 34, Palette.Player, 1.4f)
        burst(playerX(), playerY(), 14, Palette.Danger, 0.9f)
        if (score > best) {
            best = score
            isNewRecord = true
            onNewRecord(score)
        }
        onEvent(GameEvent.Crash)
    }

    private fun spawnAsteroid() {
        val radius = minDim * (0.028f + random.nextFloat() * 0.02f)
        val speed = minDim * (0.30f + speedLevel * 0.25f) * (0.85f + random.nextFloat() * 0.3f)
        val spawnDistance = sqrt(width * width + height * height) * 0.5f + radius
        asteroids += Asteroid(
            angle = random.nextFloat() * TAU,
            distance = spawnDistance,
            speed = speed,
            radius = radius,
            shape = FloatArray(9) { 0.78f + random.nextFloat() * 0.4f },
            rotation = random.nextFloat() * TAU,
            spin = (random.nextFloat() - 0.5f) * 1.6f,
        )
    }

    private fun decayVisuals(step: Float) {
        if (shake > 0f) shake = max(0f, shake - step * 2.4f)
        if (reverseFlash > 0f) reverseFlash = max(0f, reverseFlash - step * 3.5f)
        if (flash > 0f) flash = max(0f, flash - step * 3.2f)
        if (levelUpFlash > 0f) levelUpFlash = max(0f, levelUpFlash - step * 0.9f)
        if (shieldFlash > 0f) shieldFlash = max(0f, shieldFlash - step * 2.5f)
    }

    private fun burst(x: Float, y: Float, count: Int, color: androidx.compose.ui.graphics.Color, power: Float) {
        repeat(count) {
            val angle = random.nextFloat() * TAU
            val velocity = minDim * (0.05f + random.nextFloat() * 0.18f) * power
            val life = 0.35f + random.nextFloat() * 0.55f
            particles += Particle(
                x = x,
                y = y,
                vx = cos(angle) * velocity,
                vy = sin(angle) * velocity,
                life = life,
                maxLife = life,
                color = color,
                size = minDim * (0.002f + random.nextFloat() * 0.004f),
            )
        }
    }

    private fun updateParticles(dt: Float) {
        val iterator = particles.iterator()
        while (iterator.hasNext()) {
            val particle = iterator.next()
            particle.life -= dt
            if (particle.life <= 0f) {
                iterator.remove()
                continue
            }
            particle.x += particle.vx * dt
            particle.y += particle.vy * dt
            particle.vx *= 0.96f
            particle.vy *= 0.96f
        }
    }

    private fun updateTrail(dt: Float) {
        val iterator = trail.iterator()
        while (iterator.hasNext()) {
            val point = iterator.next()
            point.life -= dt * 2.2f
            if (point.life <= 0f) iterator.remove()
        }
        while (trail.size > 40) trail.removeFirst()
    }

    private fun generateStars() {
        stars.clear()
        repeat(150) {
            stars += Star(
                x = random.nextFloat(),
                y = random.nextFloat(),
                size = minDim * (0.0012f + random.nextFloat() * 0.0032f),
                phase = random.nextFloat() * TAU,
                speed = 0.5f + random.nextFloat() * 2.5f,
            )
        }
    }

    private fun powerColor(kind: PowerUpKind) = when (kind) {
        PowerUpKind.Shield -> Palette.Shield
        PowerUpKind.Slow -> Palette.Slow
        PowerUpKind.Double -> Palette.Double
    }

    private fun distance(x1: Float, y1: Float, x2: Float, y2: Float): Float {
        val dx = x1 - x2
        val dy = y1 - y2
        return sqrt(dx * dx + dy * dy)
    }

    private companion object {
        const val TAU = 6.2831855f
    }
}
