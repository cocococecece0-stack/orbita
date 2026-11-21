package com.example.orbit.game

import androidx.compose.runtime.getValue
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
 * Pure game simulation: orbiting player, incoming asteroids, collectible orbs
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

    var onCollect: () -> Unit = {}
    var onCrash: () -> Unit = {}
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
    var reverseFlash = 0f
        private set
    var playerAngle = -PI.toFloat() / 2f
        private set
    var direction = 1
        private set
    var speedLevel = 0f
        private set

    val asteroids = mutableListOf<Asteroid>()
    val orbs = mutableListOf<Orb>()
    val particles = mutableListOf<Particle>()
    val stars = mutableListOf<Star>()
    val trail = ArrayDeque<TrailPoint>()

    private val random = Random.Default
    private var lastWidth = -1
    private var lastHeight = -1
    private var minDim = 0f
    private var spawnTimer = 0f
    private var orbTimer = 0f

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
        particles.clear()
        trail.clear()
        score = 0
        isNewRecord = false
        paused = false
        playerAngle = -PI.toFloat() / 2f
        direction = 1
        spawnTimer = 0.7f
        orbTimer = 3.5f
        time = 0f
        shake = 0f
        reverseFlash = 0f
        speedLevel = 0f
        status = GameStatus.Playing
    }

    fun tap() {
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
        if (shake > 0f) shake = max(0f, shake - step * 2.4f)
        if (reverseFlash > 0f) reverseFlash = max(0f, reverseFlash - step * 3.5f)
        updateParticles(step)
        updateTrail(step)

        if (status != GameStatus.Playing || paused) return

        speedLevel = min(1f, score / 40f)
        playerAngle += direction * (1.45f + speedLevel * 0.6f) * step
        trail.addLast(TrailPoint(playerX(), playerY(), 1f))

        spawnTimer -= step
        if (spawnTimer <= 0f) {
            spawnAsteroid()
            val base = 1.05f - score * 0.018f
            spawnTimer = base.coerceIn(0.32f, 1.05f) * (0.75f + random.nextFloat() * 0.5f)
        }

        orbTimer -= step
        if (orbTimer <= 0f) {
            orbs += Orb(random.nextFloat() * TAU)
            orbTimer = 3.5f + random.nextFloat() * 2.5f
        }

        val px = playerX()
        val py = playerY()

        val asteroidIterator = asteroids.iterator()
        while (asteroidIterator.hasNext()) {
            val asteroid = asteroidIterator.next()
            asteroid.distance -= asteroid.speed * step
            asteroid.rotation += asteroid.spin * step

            val ax = centerX + cos(asteroid.angle) * asteroid.distance
            val ay = centerY + sin(asteroid.angle) * asteroid.distance

            if (distance(ax, ay, px, py) < asteroid.radius + playerRadius) {
                crash()
                return
            }
            if (!asteroid.dodged && asteroid.distance < orbitRadius - asteroid.radius) {
                asteroid.dodged = true
                score += 1
            }
            if (asteroid.distance < planetRadius * 0.35f) {
                burst(ax, ay, 6, Palette.Asteroid, 0.45f)
                asteroidIterator.remove()
            }
        }

        val orbIterator = orbs.iterator()
        while (orbIterator.hasNext()) {
            val orb = orbIterator.next()
            orb.pulse += step * 3f
            val ox = centerX + cos(orb.angle) * orbitRadius
            val oy = centerY + sin(orb.angle) * orbitRadius
            if (distance(ox, oy, px, py) < playerRadius * 2.4f) {
                score += 3
                burst(ox, oy, 14, Palette.Orb, 0.9f)
                onCollect()
                orbIterator.remove()
            }
        }
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

    private fun crash() {
        status = GameStatus.GameOver
        paused = false
        shake = 1f
        burst(playerX(), playerY(), 28, Palette.Player, 1.3f)
        burst(playerX(), playerY(), 10, Palette.Danger, 0.8f)
        if (score > best) {
            best = score
            isNewRecord = true
            onNewRecord(score)
        }
        onCrash()
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

    private fun distance(x1: Float, y1: Float, x2: Float, y2: Float): Float {
        val dx = x1 - x2
        val dy = y1 - y2
        return sqrt(dx * dx + dy * dy)
    }

    private companion object {
        const val TAU = 6.2831855f
    }
}
