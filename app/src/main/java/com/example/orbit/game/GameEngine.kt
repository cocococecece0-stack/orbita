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
    /** Открытый выбор мутации: пока он не null, симуляция на паузе. */
    var draft by androidx.compose.runtime.mutableStateOf<PerkDraft?>(null)
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
    var resonanceFlash = 0f
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
    /** Заряд резонанса 0..1: заполняется за навык и очищает орбиту волной. */
    var resonance by androidx.compose.runtime.mutableFloatStateOf(0f)
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
    /** Отдельная арена для режима «ШТАБЕЛЬ»: орбитальные сущности в нём не используются. */
    val blocks = BlockArena()
    /** Индексы мутаций, выбранных в текущем забеге. */
    val perkIds = mutableListOf<Int>()

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
    private var seasonDensity = 1f
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
    private var scoreBoost = 1f
    private var orbBoost = 1
    private var hazardSpeed = 1f
    private var hazardDensity = 1f
    private var nextDraftAt = 16f
    private var draftRound = 0
    private var objectiveRewarded = false

    private val ghostBuffer = FloatArray(4096)
    var ghostCount = 0
        private set

    fun setSettings(s: GameSettings) { settings = s }
    fun setReducedMotion(value: Boolean) { reducedMotion = value }
    fun setBest(value: Int) { bestScore = value }
    fun setSeasonModifier(value: Float) { seasonDensity = value.coerceIn(0.5f, 2f) }

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
        blocks.reset { rnd() }
        sectorId = newSector.coerceIn(0, Content.sectors.lastIndex)
        asteroids.clear(); orbs.clear(); powerUps.clear(); wormholes.clear()
        wells.clear(); boss.clear(); particles.clear(); trail.clear()
        ghostCount = 0; ghostTimer = 0f
        score = 0; isNewRecord = false; paused = false; perkIds.clear()
        combo = 0; multiplier = 1; level = if (mode == Mode.Survival) 3 else 1
        shield = upgradeShield
        shieldTime = if (shield) if (upgradeShield) 14f else 10f else 0f
        orbsCollected = 0; maxCombo = 0
        eventCooldown = 0f; elapsed = 0f
        comboTime = 0f; slowTimer = 0f; doubleTimer = 0f
        magnetTimer = 0f; phaseTimer = 0f; freezeTimer = 0f
        invulnerable = 0f; hitStop = 0f; flash = 0f
        levelUpFlash = 0f; shieldFlash = 0f; resonanceFlash = 0f; resonance = 0f
        stormWarning = 0f; flareWarning = 0f
        playerAngle = -PI.toFloat() / 2f; direction = 1
        spawnTimer = 0.7f; orbTimer = 2.2f; powerTimer = 5f
        stormTimer = 20f; flareTimer = 16f; mineTimer = 9f
        portalTimer = 12f; wellTimer = 18f; lastBossLevel = 0; nextBossLevel = 5
        orbSpawnCursor = 0f
        hazardSpawnCursor = rnd() * TAU
        time = 0f; shake = 0f; reverseFlash = 0f; speedLevel = 0f
        draft = null; scoreBoost = 1f; orbBoost = 1; hazardSpeed = 1f; hazardDensity = 1f
        nextDraftAt = 16f; draftRound = 0; objectiveRewarded = false
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
        if (mode == Mode.Stack) return
        when {
            paused -> paused = false
            status == GameStatus.Ready -> start()
            status == GameStatus.Playing -> {
                direction = -direction
                reverseFlash = 1f
            }
        }
    }

    private fun canBlockInput(): Boolean =
        mode == Mode.Stack && status == GameStatus.Playing && !paused && draft == null

    fun blockMove(direction: Int) {
        if (canBlockInput() && blocks.move(direction)) onEvent(GameEvent.BlockMove(direction))
    }

    fun blockRotate() {
        if (canBlockInput() && blocks.rotate()) onEvent(GameEvent.BlockRotate)
    }

    fun blockHold() {
        if (!canBlockInput()) return
        blocks.hold()
        onEvent(GameEvent.BlockHold)
        if (blocks.topOut) blockOver()
    }

    fun blockSoftDrop() {
        if (canBlockInput()) applyBlockStep(blocks.softDrop())
    }

    fun blockHardDrop() {
        if (!canBlockInput()) return
        val step = blocks.hardDrop()
        if (step.dropped > 0) onEvent(GameEvent.BlockDrop(step.dropped))
        applyBlockStep(step)
    }

    fun pause() { if (status == GameStatus.Playing) paused = true }
    fun resume() { paused = false }
    fun toReady() { status = GameStatus.Ready; paused = false; shieldTime = 0f; draft = null }

    /** Выход из забега домой: результат фиксируется, а не теряется. */
    fun abandonRun() {
        if (status != GameStatus.Playing) return
        status = GameStatus.GameOver
        completed = false
        paused = false
        draft = null
        stats.seconds = elapsed.toInt()
        onRunEnd(stats)
    }

    fun update(dtRaw: Float) {
        val step = dtRaw.coerceIn(0f, 0.05f)
        time += step
        elapsed += step
        // Во время выбора мутации полёт заморожен: таймер и угрозы не двигаются.
        val running = status == GameStatus.Playing && !paused && draft == null
        if (running && timeLeft > 0f) {
            timeLeft = max(0f, timeLeft - step)
            stats.seconds = elapsed.toInt()
            if (timeLeft <= 0f) { finishRun(); return }
        } else if (running) {
            stats.seconds = elapsed.toInt()
        }
        decayVisuals(step)
        updateParticles(step)
        updateTrail(step)
        if (!running) return
        if (mode == Mode.Stack) {
            applyBlockStep(blocks.update(step))
            if (status == GameStatus.Playing && !objectiveRewarded && objectiveDone()) {
                objectiveRewarded = true
                resonanceFlash = 1f
                addBlockPoints(200)
            }
            return
        }
        if (draftRound < 6 && time >= nextDraftAt) { openDraft(); return }
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
        if (status == GameStatus.Playing && !objectiveRewarded && objectiveDone()) {
            objectiveRewarded = true
            resonanceFlash = 1f
            addPoints(200)
        }
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
            spawnTimer = (base / (s.density * seasonDensity * hazardDensity)).coerceIn(floor, 1.1f) * (0.85f + rnd() * 0.3f)
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
                // Гравитация ускоряет полёт к орбите, но не сворачивает с радиального курса:
                // так направление угрозы всегда читается однозначно.
                for (a in asteroids) {
                    if (a.kind != AsteroidKind.Bolt) a.speed *= 1f + w.strength * step * 0.10f
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
            if (b.charge >= 2.4f / (1f + b.kind * 0.22f)) {
                b.charge = 0f
                val shots = 1 + b.kind.coerceIn(0, 2)
                for (k in -shots..shots) {
                    val a = norm(b.angle + k * (0.16f - shots * 0.02f))
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
        val speedScale = (if (slowTimer > 0f) 0.45f else if (freezeTimer > 0f) 0f else 1f) * hazardSpeed
        var hit: Asteroid? = null
        for (a in asteroids) {
            if (a.dead) continue
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
                if (a.dead) continue
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
                if (a.dead) continue
                onEvent(GameEvent.Dodge(combo))
            }
            // Угроза завершает полёт на своей точке орбиты, а не в общем центре.
            if (a.distance <= orbitRadius * 0.95f) {
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
        // Резонанс может сработать при сборе сферы/бонуса уже после основной уборки.
        asteroids.removeAll { it.dead }
    }

    private fun collectOrb(o: Orb) {
        chargeResonance(0.04f)
        when (o.kind) {
            OrbKind.Normal -> {
                stats.orbs++
                orbsCollected++
                rewardSkill(2 * orbBoost)
                onEvent(GameEvent.Orb)
            }
            OrbKind.Golden -> {
                stats.orbs++
                stats.goldenOrbs++
                orbsCollected++
                rewardSkill(8 * orbBoost)
                onEvent(GameEvent.GoldenOrb(combo))
            }
            OrbKind.Artifact -> {
                stats.rareOrbs++
                rewardSkill(25 * orbBoost)
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
        chargeResonance(0.08f)
        if (eventCooldown > 0f) return
        eventCooldown = 0.45f
        bumpCombo()
        addPoints(base)
    }

    /** Резонанс: волна сбрасывает угрозы у орбиты и даёт бонус за каждую. */
    private fun chargeResonance(amount: Float) {
        resonance = min(1f, resonance + amount)
        if (resonance >= 1f) triggerResonance()
    }

    private fun triggerResonance() {
        resonance = 0f
        resonanceFlash = 1f
        addPoints(80)
        var cleared = 0
        for (a in asteroids) {
            // Только помечаем: удаление прямо здесь ломает итератор цикла столкновений.
            if (a.dead || a.distance > orbitRadius * 1.45f || a.kind == AsteroidKind.Bolt) continue
            val x = centerX + cos(a.angle) * a.distance
            val y = centerY + sin(a.angle) * a.distance
            burst(x, y, 12, Palette.Orb, 0.9f)
            a.dead = true
            cleared++
        }
        if (cleared > 0) addPoints(cleared * 12)
        onEvent(GameEvent.Resonance)
    }

    private fun bumpCombo() {
        combo += 1
        comboTime = 2.0f
        multiplier = min(5, 1 + combo / 5)
        fever = multiplier >= 4
        if (combo > maxCombo) { maxCombo = combo; stats.maxCombo = combo }
    }

    private fun addBlockPoints(base: Int) {
        val boosted = max(1, (base * scoreBoost + 0.5f).toInt())
        val gained = boosted * multiplier * if (doubleTimer > 0f) 2 else 1
        score = min(Int.MAX_VALUE / 4, score + gained)
        stats.score = score
        if (score > bestScore) {
            bestScore = score
            if (!isNewRecord) { isNewRecord = true; onNewRecord(score) }
        }
    }

    /** Применяет результат шага «ШТАБЕЛЯ» к общему прогрессу забега. */
    private fun applyBlockStep(step: BlockStep) {
        if (step.dropped > 0) addBlockPoints(step.dropped * 2)
        if (step.cleared > 0) {
            val count = step.cleared.coerceIn(1, 4)
            stats.orbs += step.cleared
            orbsCollected += step.cleared
            combo += 1
            if (combo > maxCombo) { maxCombo = combo; stats.maxCombo = combo }
            multiplier = minOf(5, 1 + combo / 3)
            fever = multiplier >= 4
            val base = intArrayOf(0, 100, 300, 500, 800)[count] * blocks.level
            addBlockPoints(base + if (count == 4) 200 else 0)
            flash = maxOf(flash, 0.55f)
            if (!reducedMotion) shake = 0.35f
            onEvent(GameEvent.BlockClear(step.cleared))
        } else if (step.locked) {
            combo = 0
            multiplier = 1
            fever = false
        }
        level = blocks.level
        stats.level = level
        if (step.toppedOut) blockOver()
    }

    private fun blockOver() {
        if (status == GameStatus.GameOver) return
        status = GameStatus.GameOver
        completed = false
        paused = false
        draft = null
        stats.seconds = elapsed.toInt()
        flash = 1f
        if (!reducedMotion) shake = 0.7f
        burst(centerX, centerY, 30, Color(0xFFD8FF3E), 1.2f)
        onRunEnd(stats)
        onEvent(GameEvent.BlockTopOut)
    }

    private fun addPoints(base: Int) {
        val factor = if (doubleTimer > 0f) 2 else 1
        val boosted = (base * scoreBoost + 0.5f).toInt().coerceAtLeast(1)
        val gained = boosted * multiplier * factor
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

    /** Цель забега: та же формула на обеих платформах. */
    fun objective(): Content.Objective = Content.objective(mode, sectorId)

    fun objectiveValue(): Int = when (objective().field) {
        "orbs" -> orbsCollected
        "lines" -> stats.orbs
        else -> score
    }

    fun objectiveDone(): Boolean = objectiveValue() >= objective().target

    /** Открыть выбор мутации: три разных предложения из общего пула. */
    private fun openDraft() {
        draftRound += 1
        val pool = Content.perks.indices.toMutableList()
        val options = mutableListOf<Int>()
        repeat(3) {
            val at = (rnd() * pool.size).toInt().coerceIn(0, pool.lastIndex)
            options += pool.removeAt(at)
        }
        draft = PerkDraft(options, draftRound)
    }

    /** Применить выбранную мутацию и продолжить полёт. */
    fun choosePerk(option: Int) {
        val current = draft ?: return
        val index = current.options.getOrNull(option) ?: return
        val perk = Content.perks[index]
        perkIds += index
        when (perk.id) {
            "overclock" -> { scoreBoost += 0.25f; hazardSpeed += 0.10f }
            "barrier" -> {
                scoreBoost = max(0.5f, scoreBoost - 0.10f)
                shield = true; shieldTime = max(shieldTime, 14f)
            }
            "magnet" -> { magnetTimer = max(magnetTimer, 10f); hazardDensity += 0.10f }
            "slowmo" -> { scoreBoost = max(0.5f, scoreBoost - 0.10f); slowTimer = max(slowTimer, 10f) }
            "pulse" -> { triggerResonance(); combo = 0; multiplier = 1; comboTime = 0f }
            "ghost" -> {
                phaseTimer = max(phaseTimer, 8f); invulnerable = max(invulnerable, 8f)
                orbTimer += 4f
            }
            "harvest" -> { scoreBoost = max(0.5f, scoreBoost - 0.10f); orbBoost = 2 }
            "chronos" -> {
                hazardDensity += 0.15f
                if (timeLeft > 0f) timeLeft += 12f else addPoints(150)
            }
        }
        nextDraftAt = time + 18f
        paused = false
        draft = null
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
        draft = null
        stats.seconds = elapsed.toInt().coerceAtLeast(1)
        onRunEnd(stats)
        onEvent(GameEvent.Finish)
    }

    private fun spawnAsteroid() {
        val hazards = Content.sector(sectorId).hazards
        val comet = level >= 2 && hazards.contains(HazardKind.Comet) && rnd() < 0.18f
        val a = makeAsteroid(nextHazardAngle(), if (comet) AsteroidKind.Comet else AsteroidKind.Rock)
        asteroids += a
    }

    private fun spawnStorm() {
        val safe = rnd() * TAU
        repeat((if (mode == Mode.Survival) 6 else 5) + if (seasonDensity > 1.1f) 1 else 0) {
            var angle = nextHazardAngle()
            var tries = 0
            while (abs(angDiff(angle, safe)) < 0.8f && tries < 24) { angle = nextHazardAngle(); tries++ }
            asteroids += makeAsteroid(angle, AsteroidKind.Rock)
        }
    }

    private fun spawnFlare() {
        repeat(14) { asteroids += makeBolt(nextHazardAngle()) }
    }

    private fun spawnBoss() {
        lastBossLevel = level
        val kind = Math.floorMod(level / 5, Content.bossNames.size)
        val b = Boss(nextHazardAngle(), 2 + level / 20 + kind, 26f, kind)
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
        // Золотой угол разносит новые сферы по разным сторонам орбиты.
        val angle = norm(orbSpawnCursor + (rnd() - 0.5f) * 0.35f)
        orbSpawnCursor = norm(orbSpawnCursor + 2.3999632f)
        orbs += Orb(angle, kind, sprite)
    }

    private fun spawnPowerUp() {
        // В Дзене нет астероидов: бесполезные щит/возрождение только занимают слот.
        val kinds = if (mode == Mode.Zen) PowerUpKind.entries.filter {
            it != PowerUpKind.Shield && it != PowerUpKind.Revive
        } else PowerUpKind.entries
        powerUps += PowerUp(rnd() * TAU, kinds[(rnd() * kinds.size).toInt().coerceIn(kinds.indices)])
    }

    private fun spawnMine() {
        val a = makeAsteroid(nextHazardAngle(), AsteroidKind.Mine)
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
        resonanceFlash = max(0f, resonanceFlash - step * 1.8f)
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

    /** Следующая угроза появляется в новой точке круга, а не рядом с предыдущей. */
    private fun nextHazardAngle(): Float {
        val a = norm(hazardSpawnCursor + (rnd() - 0.5f) * 0.3f)
        hazardSpawnCursor = norm(hazardSpawnCursor + 2.3999632f)
        return a
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
        add("stack_40", mode == Mode.Stack && stats.orbs >= 40)
        add("objective_done", objectiveDone())
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
    /** Счётчик «золотого угла»: сферы равномерно распределяются по орбите, а не в одной точке. */
    private var orbSpawnCursor = 0f
    /** Угол следующей угрозы: метеориты приходят со всех сторон, а не с правого бока. */
    private var hazardSpawnCursor = 0f

    private companion object {
        const val TAU = 6.2831855f
    }
}
