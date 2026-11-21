package com.example.orbit.game

import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import com.example.orbit.data.GameSettings
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt

/**
 * Чистая симуляция «Орбиты». Один и тот же контракт используется Android и iOS.
 * Детерминирована: при одинаковом сиде и последовательности тапов даёт одинаковый результат.
 */
class GameEngine {

    var status by androidx.compose.runtime.mutableStateOf(GameStatus.Ready)
        private set
    var score by androidx.compose.runtime.mutableIntStateOf(0)
        private set
    private var bestScore by androidx.compose.runtime.mutableIntStateOf(0)
    val best: Int get() = bestScore
    var isNewRecord by androidx.compose.runtime.mutableStateOf(false)
        private set
    var paused by androidx.compose.runtime.mutableStateOf(false)
        private set
    var combo by androidx.compose.runtime.mutableIntStateOf(0)
        private set
    var multiplier by androidx.compose.runtime.mutableIntStateOf(1)
        private set
    var level by androidx.compose.runtime.mutableIntStateOf(1)
        private set
    var shield by androidx.compose.runtime.mutableStateOf(false)
        private set
    var shieldTime by androidx.compose.runtime.mutableFloatStateOf(0f)
        private set
    var orbsCollected by androidx.compose.runtime.mutableIntStateOf(0)
        private set
    var maxCombo by androidx.compose.runtime.mutableIntStateOf(0)
        private set
    var skinColor by androidx.compose.runtime.mutableStateOf(Palette.Player)

    var mode: Mode = Mode.Classic
        private set
    var sectorId: Int = 0
        private set
    var timeLeft: Float = -1f
        private set
    var completed by androidx.compose.runtime.mutableStateOf(false)
        private set
    var fever by androidx.compose.runtime.mutableStateOf(false)
        private set

    var onEvent: (GameEvent) -> Unit = {}
    var onNewRecord: (Int) -> Unit = {}
    var onRunEnd: (RunStats) -> Unit = {}

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
    var magnetTimer = 0f
        private set
    var phaseTimer = 0f
        private set
    var freezeTimer = 0f
        private set
    var stormWarning = 0f
        private set
    var flareWarning = 0f
        private set
    var comboTime = 0f
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
    val wormholes = mutableListOf<Wormhole>()
    val wells = mutableListOf<GravityWell>()
    val boss = mutableListOf<Boss>()
    val particles = mutableListOf<Particle>()
    val stars = mutableListOf<Star>()
    val trail = ArrayDeque<TrailPoint>()
    val stats = RunStats()

    private var lastWidth = -1
    private var lastHeight = -1
    private var minDim = 0f
    private var spawnTimer = 0f
    private var orbTimer = 0f
    private var powerTimer = 0f
    private var stormTimer = 0f
    private var flareTimer = 0f
    private var mineTimer = 0f
    private var portalTimer = 0f
    private var wellTimer = 0f
    private var lastBossLevel = 0
    private var nextBossLevel = 5
    private var eventCooldown = 0f
    private var elapsed = 0f
    private var ghostTimer = 0f
    private var reviveAvailable = false
    private var settings = GameSettings()
    private var upgradeShield = false
    private var upgradeTime = false
    private var upgradeMagnet = false
    private var upgradeSlow = false
    private var upgradeDouble = false
    private var reducedMotion = false

    private val ghostBuffer = FloatArray(4096)
    var ghostCount = 0
        private set

    fun setSettings(s: GameSettings) { settings = s }
    fun setReducedMotion(value: Boolean) { reducedMotion = value }
    fun setBest(value: Int) { bestScore = value }

    fun resize(w: Int, h: Int) {
        if (w <= 0 || h <= 0 || (w == lastWidth && h == lastHeight)) return
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

    /** Начало забега с фиксированным сидом: основа детерминизма и daily-режима. */
    fun startRun(newMode: Mode, newSector: Int, seed: Int) {
        rngState = (seed.toLong() xor 0x5DEECE66DL) and Long.MAX_VALUE
        mode = newMode
        sectorId = newSector.coerceIn(0, Content.sectors.lastIndex)
        asteroids.clear(); orbs.clear(); powerUps.clear(); wormholes.clear()
        wells.clear(); boss.clear(); particles.clear(); trail.clear()
        ghostCount = 0; ghostTimer = 0f
        score = 0; isNewRecord = false; paused = false
        combo = 0; multiplier = 1; level = if (mode == Mode.Survival) 3 else 1
        shield = upgradeShield
        shieldTime = if (shield) if (upgradeShield) 14f else 10f else 0f
        orbsCollected = 0; maxCombo = 0
        eventCooldown = 0f; elapsed = 0f
        comboTime = 0f; slowTimer = 0f; doubleTimer = 0f
        magnetTimer = 0f; phaseTimer = 0f; freezeTimer = 0f
        invulnerable = 0f; hitStop = 0f; flash = 0f
        levelUpFlash = 0f; shieldFlash = 0f; stormWarning = 0f; flareWarning = 0f
        playerAngle = -PI.toFloat() / 2f; direction = 1
        spawnTimer = 0.7f; orbTimer = 2.2f; powerTimer = 5f
        stormTimer = 20f; flareTimer = 16f; mineTimer = 9f
        portalTimer = 12f; wellTimer = 18f; lastBossLevel = 0; nextBossLevel = 5
        time = 0f; shake = 0f; reverseFlash = 0f; speedLevel = 0f
        fever = false; completed = false; reviveAvailable = false
        timeLeft = if (mode.seconds > 0) mode.seconds.toFloat() + if (upgradeTime) 8f else 0f else -1f
        stats.score = 0; stats.level = level; stats.orbs = 0; stats.goldenOrbs = 0
        stats.rareOrbs = 0; stats.maxCombo = 0; stats.nearMisses = 0; stats.dodges = 0
        stats.bossKills = 0; stats.wormholes = 0; stats.seconds = 0; stats.powerups = 0; stats.hits = 0
        status = GameStatus.Playing
    }

    /** Позволяет注入 флаги улучшений без связи игрового движка с хранилищем. */
    fun setUpgrades(flags: Map<String, Boolean>) {
        upgradeShield = flags["upg_shield"] == true
        upgradeTime = flags["upg_time"] == true
        upgradeMagnet = flags["upg_magnet"] == true
        upgradeSlow = flags["upg_slow"] == true
        upgradeDouble = flags["upg_double"] == true
    }

    fun start() = startRun(mode, sectorId, (System.currentTimeMillis() and 0x7fffffff).toInt())

    /** Тап: меняем направление полёта. Ядро механики не изменилось. */
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

    fun pause() { if (status == GameStatus.Playing) paused = true }
    fun resume() { paused = false }
    fun toReady() { status = GameStatus.Ready; paused = false; shieldTime = 0f }

    /** Выход из забега домой: результат фиксируется, а не теряется. */
    fun abandonRun() {
        if (status != GameStatus.Playing) return
        status = GameStatus.GameOver
        completed = false
        paused = false
        stats.seconds = elapsed.toInt()
        onRunEnd(stats)
    }

    fun update(dtRaw: Float) {
        val step = dtRaw.coerceIn(0f, 0.05f)
        time += step
        elapsed += step
        // Таймер тикает по реальному времени, даже во время hit-stop.
        if (status == GameStatus.Playing && !paused && timeLeft > 0f) {
            timeLeft = max(0f, timeLeft - step)
            stats.seconds = elapsed.toInt()
            if (timeLeft <= 0f) { finishRun(); return }
        } else if (status == GameStatus.Playing && !paused) {
            stats.seconds = elapsed.toInt()
        }
        decayVisuals(step)
        updateParticles(step)
        updateTrail(step)
        if (status != GameStatus.Playing || paused) return
        if (hitStop > 0f) { hitStop = max(0f, hitStop - step); return }
        eventCooldown = max(0f, eventCooldown - step)

        if (shieldTime > 0f) {
            shieldTime = max(0f, shieldTime - step)
            if (shieldTime <= 0f) { shield = false; onEvent(GameEvent.ShieldBreak) }
        }
        if (combo > 0) {
            comboTime -= step
            if (comboTime <= 0f) { combo = 0; multiplier = 1; fever = false }
        }
        slowTimer = max(0f, slowTimer - step)
        doubleTimer = max(0f, doubleTimer - step)
        magnetTimer = max(0f, magnetTimer - step)
        phaseTimer = max(0f, phaseTimer - step)
        freezeTimer = max(0f, freezeTimer - step)
        invulnerable = max(0f, invulnerable - step)

        speedLevel = min(1f, score / 400f + if (mode == Mode.Survival) time / 180f else 0f)
        playerAngle += direction * (1.45f + speedLevel * 0.65f) * step
        trail.addLast(TrailPoint(playerX(), playerY(), 1f, skinColor))
        ghostTimer -= step
        if (ghostTimer <= 0f && ghostCount < ghostBuffer.size) {
            ghostBuffer[ghostCount++] = playerAngle
            ghostTimer = 0.08f
        }

        updateSpawns(step)
        updateHazards(step)
        updateCollisions(step)
    }

    private fun updateSpawns(step: Float) {
        val s = Content.sector(sectorId)
        if (stormWarning > 0f) {
            stormWarning -= step
            if (stormWarning <= 0f) spawnStorm()
        }
        if (flareWarning > 0f) {
            flareWarning -= step
            if (flareWarning <= 0f) spawnFlare()
        }
        if (mode == Mode.Zen) {
            orbTimer -= step
            if (orbTimer <= 0f) { spawnOrb(); orbTimer = 1.6f + rnd() * 1.8f }
            powerTimer -= step
            if (powerTimer <= 0f) { spawnPowerUp(); powerTimer = 5f + rnd() * 4f }
            return
        }

        if (level >= 2) {
            stormTimer -= step
            if (stormTimer <= 0f && stormWarning <= 0f) {
                stormWarning = 1.6f
                stormTimer = 22f + rnd() * 12f
                onEvent(GameEvent.Storm)
            }
        }
        if (s.hazards.contains(HazardKind.Flare) && level >= 6) {
            flareTimer -= step
            if (flareTimer <= 0f && flareWarning <= 0f) {
                flareWarning = 1.2f
                flareTimer = 14f + rnd() * 10f
                onEvent(GameEvent.Flare)
            }
        }
        spawnTimer -= step
        if (spawnTimer <= 0f && stormWarning <= 0f) {
            spawnAsteroid()
            val base = 1.0f - min(0.35f, score * 0.0004f)
            val floor = if (mode == Mode.Survival) 0.40f else 0.55f
            spawnTimer = (base / s.density).coerceIn(floor, 1.1f) * (0.85f + rnd() * 0.3f)
        }
        orbTimer -= step
        if (orbTimer <= 0f) { spawnOrb(); orbTimer = 2.8f + rnd() * 2.5f }
        powerTimer -= step
        if (powerTimer <= 0f) { spawnPowerUp(); powerTimer = 6f + rnd() * 5f }

        if (s.hazards.contains(HazardKind.Mine) && level >= 3) {
            mineTimer -= step
            if (mineTimer <= 0f) { spawnMine(); mineTimer = 7f + rnd() * 6f }
        }
        if (s.hazards.contains(HazardKind.Wormhole) && level >= 4) {
            portalTimer -= step
            if (portalTimer <= 0f) { spawnWormholes(); portalTimer = 16f + rnd() * 10f }
        }
        if (s.hazards.contains(HazardKind.Gravity) && level >= 5) {
            wellTimer -= step
            if (wellTimer <= 0f) {
                wells += GravityWell(rnd() * TAU, 0.5f + rnd() * 0.7f)
                wellTimer = 20f + rnd() * 12f
            }
        }
        // Пороговый спавн: уровни могут перепрыгивать порог, но босс не теряется.
        while (level >= nextBossLevel && status == GameStatus.Playing) {
            spawnBoss()
            nextBossLevel += 5
        }
    }

    private fun updateHazards(step: Float) {
        if (freezeTimer <= 0f) {
            for (w in wells) {
                w.pulse += step * 2f
                for (a in asteroids) {
                    var diff = w.angle - a.angle
                    while (diff > PI) diff -= TAU
                    while (diff < -PI) diff += TAU
                    a.angle = norm(a.angle + diff * w.strength * step * 0.35f)
                }
            }
        }
        for (w in wormholes) {
            w.pulse += step * 3f
            w.cooldown = max(0f, w.cooldown - step)
        }
        for (b in boss) {
            b.time += step
            b.hitFlash = max(0f, b.hitFlash - step * 4f)
            b.charge += step
            if (b.charge >= 2.4f) {
                b.charge = 0f
                for (k in -1..1) {
                    val a = norm(b.angle + k * 0.16f)
                    asteroids += makeBolt(a)
                }
                onEvent(GameEvent.BossHit)
            }
            if (b.time >= b.duration) b.dead = true
        }
        boss.removeAll { it.dead }
    }

    private fun updateCollisions(step: Float) {
        val px = playerX()
        val py = playerY()
        val speedScale = if (slowTimer > 0f) 0.45f else if (freezeTimer > 0f) 0f else 1f
        var hit: Asteroid? = null
        for (a in asteroids) {
            a.pulse += step * 2f
            val prevDistance = a.distance
            if (freezeTimer <= 0f) a.distance -= a.speed * speedScale * step
            a.rotation += a.spin * step
            a.life -= step
            if (a.life <= 0f) { a.dead = true; continue }
            val ax = centerX + cos(a.angle) * a.distance
            val ay = centerY + sin(a.angle) * a.distance
            val d = distance(ax, ay, px, py)
            val cd = a.radius + playerRadius
            val angleToPlayer = abs(angDiff(a.angle, playerAngle))
            if (d < cd && invulnerable <= 0f && phaseTimer <= 0f) { hit = a; break }
            // Близкий пролёт — только рядом с кораблём и на расстоянии «на волоске».
            if (!a.nearMissed && d > cd && d < cd + playerRadius * 1.2f &&
                angleToPlayer < 0.16f && invulnerable <= 0f && a.kind != AsteroidKind.Bolt) {
                a.nearMissed = true
                stats.nearMisses++
                rewardSkill(1)
                flash = 1f
                hitStop = if (reducedMotion) 0f else 0.02f
                onEvent(GameEvent.NearMiss)
            }
            // Уклонение — пересечение орбиты именно в секторе корабля.
            if (!a.dodged && prevDistance >= orbitRadius && a.distance < orbitRadius &&
                angleToPlayer < 0.24f && a.kind != AsteroidKind.Bolt) {
                a.dodged = true
                stats.dodges++
                rewardSkill(1)
                onEvent(GameEvent.Dodge(combo))
            }
            if (a.distance < planetRadius * 0.35f) {
                burst(ax, ay, 6, Palette.Asteroid, 0.45f)
                a.dead = true
            }
        }
        asteroids.removeAll { it.dead }
        if (hit != null) { handleHit(hit); return }

        val itOrb = orbs.iterator()
        while (itOrb.hasNext()) {
            val o = itOrb.next()
            o.pulse += step * 3f
            if (magnetTimer > 0f || upgradeMagnet) {
                var diff = playerAngle - o.angle
                while (diff > PI) diff -= TAU
                while (diff < -PI) diff += TAU
                o.angle = norm(o.angle + diff * step * (if (upgradeMagnet) 0.5f else 1.6f))
            }
            val ox = centerX + cos(o.angle) * orbitRadius
            val oy = centerY + sin(o.angle) * orbitRadius
            if (distance(ox, oy, px, py) < playerRadius * 2.4f) {
                collectOrb(o)
                itOrb.remove()
            }
        }

        val itPower = powerUps.iterator()
        while (itPower.hasNext()) {
            val p = itPower.next()
            p.pulse += step * 4f
            val ox = centerX + cos(p.angle) * orbitRadius
            val oy = centerY + sin(p.angle) * orbitRadius
            if (distance(ox, oy, px, py) < playerRadius * 2.8f) {
                applyPowerUp(p.kind)
                burst(ox, oy, 18, powerColor(p.kind), 1f)
                stats.powerups++
                onEvent(GameEvent.PowerUp(p.kind))
                itPower.remove()
            }
        }

        for (w in wormholes) {
            if (w.cooldown > 0f) continue
            var diff = angDiff(w.angle, playerAngle)
            if (abs(diff) < 0.10f) {
                playerAngle = norm(w.partner + 0.35f * direction)
                w.cooldown = 2f
                stats.wormholes++
                burst(px, py, 20, Palette.Phase, 1f)
                onEvent(GameEvent.Wormhole)
            }
        }
    }

    private fun collectOrb(o: Orb) {
        when (o.kind) {
            OrbKind.Normal -> {
                stats.orbs++
                orbsCollected++
                rewardSkill(2)
                onEvent(GameEvent.Orb)
            }
            OrbKind.Golden -> {
                stats.orbs++
                stats.goldenOrbs++
                orbsCollected++
                rewardSkill(8)
                onEvent(GameEvent.GoldenOrb(combo))
            }
            OrbKind.Artifact -> {
                stats.rareOrbs++
                rewardSkill(25)
                onEvent(GameEvent.Artifact)
            }
        }
        val ox = centerX + cos(o.angle) * orbitRadius
        val oy = centerY + sin(o.angle) * orbitRadius
        burst(ox, oy, 14, if (o.kind == OrbKind.Normal) Palette.Orb else Color(0xFFFFF3B0), 0.9f)
        damageBoss(o.angle)
    }

    private fun damageBoss(angle: Float) {
        for (b in boss) {
            if (abs(angDiff(b.angle, angle)) > 0.75f) continue
            b.hitFlash = 1f
            addPoints(40)
            onEvent(GameEvent.BossHit)
            if (b.hp <= 1) {
                b.dead = true
                stats.bossKills++
                addPoints(150 + 10 * level)
                burst(centerX + cos(b.angle) * orbitRadius, centerY + sin(b.angle) * orbitRadius, 60, Palette.Boss, 2f)
                onEvent(GameEvent.BossDown)
            } else {
                b.hp -= 1
            }
            break
        }
    }

    private fun activeCount(): Int =
        (if (shield) 1 else 0) + (if (slowTimer > 0f) 1 else 0) +
            (if (doubleTimer > 0f) 1 else 0) + (if (magnetTimer > 0f) 1 else 0) +
            (if (phaseTimer > 0f) 1 else 0) + (if (freezeTimer > 0f) 1 else 0)

    private fun applyPowerUp(kind: PowerUpKind) {
        when (kind) {
            PowerUpKind.Shield -> if (activeCount() < 3) {
                shield = true
                shieldTime = if (upgradeShield) 14f else 10f
            }
            PowerUpKind.Slow -> if (activeCount() < 3) slowTimer = if (upgradeSlow) 8f else 5f
            PowerUpKind.Double -> if (activeCount() < 3) doubleTimer = if (upgradeDouble) 12f else 8f
            PowerUpKind.Magnet -> if (activeCount() < 3) magnetTimer = 8f
            PowerUpKind.Phase -> if (activeCount() < 3) phaseTimer = 6f
            PowerUpKind.Freeze -> if (activeCount() < 3) freezeTimer = 4f
            PowerUpKind.Revive -> reviveAvailable = true
        }
    }

    /** Награда за навык: один счётчик на любое событие, чтобы очки не «капали». */
    private fun rewardSkill(base: Int) {
        if (eventCooldown > 0f) return
        eventCooldown = 0.45f
        bumpCombo()
        addPoints(base)
    }

    private fun bumpCombo() {
        combo += 1
        comboTime = 2.0f
        multiplier = min(5, 1 + combo / 5)
        fever = multiplier >= 4
        if (combo > maxCombo) { maxCombo = combo; stats.maxCombo = combo }
    }

    private fun addPoints(base: Int) {
        val factor = if (doubleTimer > 0f) 2 else 1
        val gained = base * multiplier * factor
        score = min(Int.MAX_VALUE / 4, score + gained)
        stats.score = score
        val next = 1 + score / 150
        if (next > level) {
            level = min(level + 1, next)
            stats.level = level
            levelUpFlash = 1f
            onEvent(GameEvent.LevelUp(level))
        }
        if (score > bestScore) {
            bestScore = score
            if (!isNewRecord) { isNewRecord = true; onNewRecord(score) }
        }
    }

    private fun handleHit(a: Asteroid) {
        stats.hits++
        // Любое попадание сбивает цепочку: комбо — навык, а не таймер.
        combo = 0; multiplier = 1; comboTime = 0f; fever = false
        shieldTime = 0f
        when {
            phaseTimer > 0f -> {
                a.dead = true
                burst(playerX(), playerY(), 12, Palette.Phase, 0.8f)
            }
            shield -> {
                shield = false
                invulnerable = 1.6f
                shieldFlash = 1f
                if (!reducedMotion) shake = 0.9f
                hitStop = 0.06f
                burst(playerX(), playerY(), 24, Palette.Shield, 1.1f)
                onEvent(GameEvent.ShieldBreak)
            }
            reviveAvailable -> {
                reviveAvailable = false
                invulnerable = 2.4f
                shieldFlash = 1f
                burst(playerX(), playerY(), 40, Palette.Revive, 1.6f)
                onEvent(GameEvent.PowerUp(PowerUpKind.Revive))
            }
            mode == Mode.Sprint -> {
                timeLeft = max(0f, timeLeft - 3f)
                flash = 1f
                invulnerable = 1.2f
                if (!reducedMotion) shake = 0.7f
                burst(playerX(), playerY(), 20, Palette.Danger, 1f)
                onEvent(GameEvent.Crash)
                if (timeLeft <= 0f) finishRun()
            }
            mode == Mode.Zen -> Unit
            else -> crash()
        }
    }

    private fun crash() {
        status = GameStatus.GameOver
        paused = false
        if (!reducedMotion) shake = 1f
        hitStop = 0.09f
        burst(playerX(), playerY(), 34, skinColor, 1.4f)
        burst(playerX(), playerY(), 14, Palette.Danger, 0.9f)
        onRunEnd(stats)
        onEvent(GameEvent.Crash)
    }

    private fun finishRun() {
        if (status == GameStatus.GameOver) return
        status = GameStatus.GameOver
        completed = true
        paused = false
        stats.seconds = elapsed.toInt().coerceAtLeast(1)
        onRunEnd(stats)
        onEvent(GameEvent.Finish)
    }

    private fun spawnAsteroid() {
        val hazards = Content.sector(sectorId).hazards
        val comet = level >= 2 && hazards.contains(HazardKind.Comet) && rnd() < 0.18f
        val a = makeAsteroid(rnd() * TAU, if (comet) AsteroidKind.Comet else AsteroidKind.Rock)
        asteroids += a
    }

    private fun spawnStorm() {
        val safe = rnd() * TAU
        repeat(if (mode == Mode.Survival) 6 else 5) {
            var angle = rnd() * TAU
            var tries = 0
            while (abs(angDiff(angle, safe)) < 0.8f && tries < 24) { angle = rnd() * TAU; tries++ }
            asteroids += makeAsteroid(angle, AsteroidKind.Rock)
        }
    }

    private fun spawnFlare() {
        val base = rnd() * TAU
        repeat(14) { asteroids += makeBolt(norm(base + (rnd() - 0.5f) * 2.6f)) }
    }

    private fun spawnBoss() {
        lastBossLevel = level
        val b = Boss(rnd() * TAU, 2 + level / 20, 26f)
        boss += b
        // Рядом с боссом появляются сферы — ядро реально можно сбить.
        repeat(2) { orbs += Orb(norm(b.angle + (rnd() - 0.5f) * 0.55f), OrbKind.Normal, 0) }
        onEvent(GameEvent.BossSpawn)
        burst(centerX + cos(b.angle) * orbitRadius, centerY + sin(b.angle) * orbitRadius, 40, Palette.Boss, 1.5f)
    }

    private fun spawnOrb() {
        val v = rnd()
        val kind = when {
            v < 0.04f -> OrbKind.Artifact
            v < 0.20f -> OrbKind.Golden
            else -> OrbKind.Normal
        }
        val sprite = when (kind) { OrbKind.Normal -> 0; OrbKind.Golden -> 1; OrbKind.Artifact -> 2 }
        orbs += Orb(rnd() * TAU, kind, sprite)
    }

    private fun spawnPowerUp() {
        // В Дзене нет астероидов: бесполезные щит/возрождение только занимают слот.
        val kinds = if (mode == Mode.Zen) PowerUpKind.entries.filter {
            it != PowerUpKind.Shield && it != PowerUpKind.Revive
        } else PowerUpKind.entries
        powerUps += PowerUp(rnd() * TAU, kinds[(rnd() * kinds.size).toInt().coerceIn(kinds.indices)])
    }

    private fun spawnMine() {
        val a = makeAsteroid(rnd() * TAU, AsteroidKind.Mine)
        a.speed *= 0.35f
        a.life = 16f
        asteroids += a
    }

    private fun spawnWormholes() {
        val a = rnd() * TAU
        wormholes.clear()
        wormholes += Wormhole(a, norm(a + PI.toFloat()))
        wormholes += Wormhole(norm(a + PI.toFloat()), a)
    }

    private fun makeAsteroid(angle: Float, kind: AsteroidKind): Asteroid {
        val baseRadius = minDim * (0.026f + rnd() * 0.018f)
        val baseSpeed = minDim * (0.30f + speedLevel * 0.25f) * (0.85f + rnd() * 0.3f)
        val comet = kind == AsteroidKind.Comet
        val a = Asteroid(
            angle = angle,
            distance = sqrt(width * width + height * height) * 0.5f + baseRadius,
            speed = if (comet) baseSpeed * 1.9f else baseSpeed,
            radius = if (comet) baseRadius * 0.7f else if (kind == AsteroidKind.Mine) baseRadius * 1.15f else baseRadius,
            kind = kind,
            sprite = (rnd() * 8).toInt().coerceIn(0, 7),
        )
        a.rotation = rnd() * TAU
        a.spin = (rnd() - 0.5f) * 1.6f
        return a
    }

    private fun makeBolt(angle: Float): Asteroid {
        val a = Asteroid(angle, orbitRadius * 2.6f, minDim * 0.55f, minDim * 0.010f, AsteroidKind.Bolt, 0)
        a.life = 5f
        return a
    }

    private fun decayVisuals(step: Float) {
        shake = max(0f, shake - step * 2.4f)
        reverseFlash = max(0f, reverseFlash - step * 3.5f)
        flash = max(0f, flash - step * 3.2f)
        levelUpFlash = max(0f, levelUpFlash - step * 0.9f)
        shieldFlash = max(0f, shieldFlash - step * 2.5f)
    }

    private fun burst(x: Float, y: Float, count: Int, color: Color, power: Float) {
        val n = if (reducedMotion) (count / 3).coerceAtLeast(4) else count
        repeat(n) {
            val angle = rnd() * TAU
            val velocity = minDim * (0.05f + rnd() * 0.18f) * power
            val life = 0.35f + rnd() * 0.55f
            particles += Particle(
                x, y, cos(angle) * velocity, sin(angle) * velocity,
                life, life, color, minDim * (0.002f + rnd() * 0.004f), (rnd() - 0.5f) * 6f,
            )
        }
        while (particles.size > 420) particles.removeAt(0)
    }

    private fun updateParticles(dt: Float) {
        val it = particles.iterator()
        while (it.hasNext()) {
            val p = it.next()
            p.life -= dt
            if (p.life <= 0f) { it.remove(); continue }
            p.x += p.vx * dt
            p.y += p.vy * dt
            p.vx *= 0.96f
            p.vy *= 0.96f
        }
    }

    private fun updateTrail(dt: Float) {
        val it = trail.iterator()
        while (it.hasNext()) {
            val point = it.next()
            point.life -= dt * 2.2f
            if (point.life <= 0f) it.remove()
        }
        while (trail.size > 40) trail.removeFirst()
    }

    private fun generateStars() {
        stars.clear()
        repeat(180) {
            stars += Star(rnd(), rnd(), minDim * (0.0012f + rnd() * 0.0032f), rnd() * TAU, 0.5f + rnd() * 2.5f, (rnd() * 3).toInt())
        }
    }

    private fun powerColor(kind: PowerUpKind) = when (kind) {
        PowerUpKind.Shield -> Palette.Shield
        PowerUpKind.Slow -> Palette.Slow
        PowerUpKind.Double -> Palette.Double
        PowerUpKind.Magnet -> Palette.Magnet
        PowerUpKind.Phase -> Palette.Phase
        PowerUpKind.Freeze -> Palette.Freeze
        PowerUpKind.Revive -> Palette.Revive
    }

    private fun angDiff(a: Float, b: Float): Float {
        var d = (a - b) % TAU
        if (d > PI) d -= TAU
        if (d < -PI) d += TAU
        return d
    }

    private fun norm(a: Float): Float {
        var v = a % TAU
        if (v < 0f) v += TAU
        return v
    }

    private fun distance(x1: Float, y1: Float, x2: Float, y2: Float): Float {
        val dx = x1 - x2; val dy = y1 - y2
        return sqrt(dx * dx + dy * dy)
    }

    /** Условия достижений, вычисляются по завершении забега. */
    fun achievedIds(): List<String> {
        val out = mutableListOf<String>()
        fun add(id: String, ok: Boolean) { if (ok) out += id }
        add("first_run", true)
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
        return out
    }

    fun secretIds(): List<String> {
        val out = mutableListOf<String>()
        if (stats.rareOrbs >= 3) out += "eye"
        if (stats.nearMisses >= 25 && stats.seconds <= 60) out += "dancer"
        if (stats.level >= 20 && stats.hits == 0) out += "survivor"
        return out
    }

    fun ghost(): FloatArray = ghostBuffer.copyOf(ghostCount)

    private fun rnd(): Float {
        rngState = rngState * 6364136223846793005L + 1442695040888963407L
        return ((rngState ushr 33) and 0xFFFFFF).toFloat() / 0x1000000f
    }

    private var rngState: Long = 1L

    private companion object {
        const val TAU = 6.2831855f
    }
}
