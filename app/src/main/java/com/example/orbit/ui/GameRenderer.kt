package com.example.orbit.ui

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.imageResource
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import com.example.orbit.R
import com.example.orbit.game.AsteroidKind
import com.example.orbit.game.GameEngine
import com.example.orbit.game.OrbKind
import com.example.orbit.game.Palette
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.roundToInt
import kotlin.math.sin

private const val TAU = 6.2831855f
private const val RAD = 57.2958f
/** Доля видимого радиуса в спрайте планеты (есть прозрачные поля). */
private const val PLANET_SPRITE_VISIBLE = 0.456f

/** Высококонтрастная палитра для режима «Контрастные цвета». */
private object Contrast {
    val Hazard = Color(0xFFFF8A3D)
    val HazardHot = Color(0xFFFF4438)
    val Bonus = Color(0xFF38BDF8)
    val BonusAlt = Color(0xFF34D399)
    val Outline = Color(0xFFFFFFFF)
}

private val SectorAccents = listOf(
    Color(0xFF22D3EE), Color(0xFFFF8A3D), Color(0xFFA78BFA), Color(0xFF67E8F9),
    Color(0xFFFBBF24), Color(0xFFF472B6), Color(0xFFFF5C70), Color(0xFF34D399),
)

/** Все изображения загружаются один раз и переиспользуются каждым кадром. */
class GameArt(
    val backgrounds: List<ImageBitmap>,
    val planets: List<ImageBitmap>,
    val ships: List<ImageBitmap>,
    val asteroids: List<ImageBitmap>,
    val comets: List<ImageBitmap>,
    val powers: List<ImageBitmap>,
    val orbs: List<ImageBitmap>,
    val glow: ImageBitmap,
    val ring: ImageBitmap,
    val burst: ImageBitmap,
)

@Composable
fun rememberGameArt(): GameArt {
    val res = LocalContext.current.resources
    return remember {
        GameArt(
            backgrounds = listOf(
                ImageBitmap.imageResource(res, R.drawable.bg_0), ImageBitmap.imageResource(res, R.drawable.bg_1),
                ImageBitmap.imageResource(res, R.drawable.bg_2), ImageBitmap.imageResource(res, R.drawable.bg_3),
                ImageBitmap.imageResource(res, R.drawable.bg_4), ImageBitmap.imageResource(res, R.drawable.bg_5),
                ImageBitmap.imageResource(res, R.drawable.bg_6), ImageBitmap.imageResource(res, R.drawable.bg_7),
            ),
            planets = listOf(
                ImageBitmap.imageResource(res, R.drawable.planet_0), ImageBitmap.imageResource(res, R.drawable.planet_1),
                ImageBitmap.imageResource(res, R.drawable.planet_2), ImageBitmap.imageResource(res, R.drawable.planet_3),
                ImageBitmap.imageResource(res, R.drawable.planet_4), ImageBitmap.imageResource(res, R.drawable.planet_5),
                ImageBitmap.imageResource(res, R.drawable.planet_6), ImageBitmap.imageResource(res, R.drawable.planet_7),
            ),
            ships = listOf(
                ImageBitmap.imageResource(res, R.drawable.ship_0), ImageBitmap.imageResource(res, R.drawable.ship_1),
                ImageBitmap.imageResource(res, R.drawable.ship_2), ImageBitmap.imageResource(res, R.drawable.ship_3),
                ImageBitmap.imageResource(res, R.drawable.ship_4), ImageBitmap.imageResource(res, R.drawable.ship_5),
                ImageBitmap.imageResource(res, R.drawable.ship_6), ImageBitmap.imageResource(res, R.drawable.ship_7),
            ),
            asteroids = listOf(
                ImageBitmap.imageResource(res, R.drawable.ast_0), ImageBitmap.imageResource(res, R.drawable.ast_1),
                ImageBitmap.imageResource(res, R.drawable.ast_2), ImageBitmap.imageResource(res, R.drawable.ast_3),
                ImageBitmap.imageResource(res, R.drawable.ast_4), ImageBitmap.imageResource(res, R.drawable.ast_5),
                ImageBitmap.imageResource(res, R.drawable.ast_6), ImageBitmap.imageResource(res, R.drawable.ast_7),
            ),
            comets = listOf(
                ImageBitmap.imageResource(res, R.drawable.comet_0), ImageBitmap.imageResource(res, R.drawable.comet_1),
                ImageBitmap.imageResource(res, R.drawable.comet_2),
            ),
            powers = listOf(
                ImageBitmap.imageResource(res, R.drawable.power_0), ImageBitmap.imageResource(res, R.drawable.power_1),
                ImageBitmap.imageResource(res, R.drawable.power_2), ImageBitmap.imageResource(res, R.drawable.power_3),
                ImageBitmap.imageResource(res, R.drawable.power_4), ImageBitmap.imageResource(res, R.drawable.power_5),
                ImageBitmap.imageResource(res, R.drawable.power_6),
            ),
            orbs = listOf(
                ImageBitmap.imageResource(res, R.drawable.orb_0), ImageBitmap.imageResource(res, R.drawable.orb_1),
                ImageBitmap.imageResource(res, R.drawable.orb_2),
            ),
            glow = ImageBitmap.imageResource(res, R.drawable.fx_glow),
            ring = ImageBitmap.imageResource(res, R.drawable.fx_ring),
            burst = ImageBitmap.imageResource(res, R.drawable.fx_burst),
        )
    }
}

/** Мягкое свечение без квадратных артефактов: радиальный градиент с прозрачным краем. */
private fun DrawScope.drawGlow(center: Offset, radius: Float, color: Color, alpha: Float) {
    if (radius <= 0f || alpha <= 0f) return
    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(color.copy(alpha = alpha), color.copy(alpha = alpha * 0.42f), Color.Transparent),
            center = center,
            radius = radius,
        ),
        radius = radius,
        center = center,
    )
}

/** Рисует сцену. [frameTick] инвалидирует фазу анимации каждый кадр. */
fun DrawScope.drawGame(
    engine: GameEngine,
    frameTick: Long,
    art: GameArt,
    shipSprite: Int,
    planetStyle: Int,
    trailStyle: Int,
    colorblind: Boolean = false,
    reducedMotion: Boolean = false,
) {
    if (engine.width <= 0f) return
    val w = size.width
    val h = size.height
    val center = Offset(engine.centerX, engine.centerY)
    val skin = engine.skinColor
    // Игровая логика живёт по engine.time; декоративные эффекты — по «визуальному» времени.
    val visualTime = if (reducedMotion) 0f else engine.time
    val visualTick = if (reducedMotion) 0L else frameTick

    drawBackground(art, engine.sectorId, planetStyle, visualTime, w, h)

    engine.stars.forEach { star ->
        val twinkle = if (reducedMotion) 0.7f else 0.35f + 0.65f * abs(sin(star.phase + visualTick * 0.03f * star.speed))
        drawCircle(
            color = Color.White.copy(alpha = twinkle * 0.75f),
            radius = star.size,
            center = Offset(star.x * w, star.y * h),
        )
    }

    val shakeX = if (!reducedMotion && engine.shake > 0f) sin(visualTime * 83f) * engine.shake * 14f else 0f
    val shakeY = if (!reducedMotion && engine.shake > 0f) cos(visualTime * 71f) * engine.shake * 14f else 0f

    translate(shakeX, shakeY) {
        drawWells(engine, art, colorblind)
        drawPlanet(engine, art, planetStyle, visualTime, colorblind)
        drawComboRing(engine)
        drawOrbitRing(engine)
        drawWormholes(engine, art, colorblind)
        drawBosses(engine, art, visualTime, colorblind)
        drawTrail(engine, art, trailStyle, visualTime)
        drawOrbs(engine, art, colorblind, reducedMotion)
        drawPowerUps(engine, art, colorblind, reducedMotion)
        drawAsteroids(engine, art, colorblind)
        drawPlayer(engine, art, shipSprite, skin, visualTime, colorblind)
        drawParticles(engine, art)
    }

    if (engine.multiplier >= 3) {
        drawRect(
            brush = Brush.radialGradient(
                colors = listOf(Color.Transparent, skin.copy(alpha = (engine.multiplier - 2) * 0.07f)),
                center = center,
                radius = max(w, h) * 0.72f,
            ),
        )
    }
    if (engine.stormWarning > 0f) {
        val pulse = if (reducedMotion) 0.5f else 0.5f + 0.5f * sin(visualTime * 12f)
        val danger = if (colorblind) Contrast.HazardHot else Palette.Danger
        drawRect(color = danger.copy(alpha = engine.stormWarning * (0.06f + 0.06f * pulse)))
    }
    if (engine.flareWarning > 0f) {
        val flare = if (colorblind) Contrast.Hazard else Palette.Orb
        drawRect(color = flare.copy(alpha = engine.flareWarning * 0.10f))
    }
    if (engine.flash > 0f) {
        drawRect(
            brush = Brush.radialGradient(
                colors = listOf(Color.White.copy(alpha = engine.flash * 0.20f), Color.Transparent),
                center = center,
                radius = max(w, h) * 0.7f,
            ),
        )
    }
    if (engine.levelUpFlash > 0f) {
        val bonus = if (colorblind) Contrast.BonusAlt else Palette.Orb
        drawRect(color = bonus.copy(alpha = engine.levelUpFlash * 0.07f))
    }
    if (engine.shieldFlash > 0f) {
        val shield = if (colorblind) Contrast.BonusAlt else Palette.Shield
        drawRect(color = shield.copy(alpha = engine.shieldFlash * 0.12f))
    }
    drawRect(
        brush = Brush.radialGradient(
            colors = listOf(Color.Transparent, Color.Black.copy(alpha = 0.5f)),
            center = center,
            radius = max(w, h) * 0.72f,
        ),
    )
}

private fun DrawScope.drawBackground(
    art: GameArt,
    sector: Int,
    style: Int,
    time: Float,
    w: Float,
    h: Float,
) {
    val image = art.backgrounds[(sector + style * 2) % art.backgrounds.size]
    val scale = max(w / image.width, h / image.height) * 1.04f
    val dw = image.width * scale
    val dh = image.height * scale
    // Дрейф фона минимален и полностью выключен при reducedMotion (time == 0).
    val drift = (sin(time * 0.12f) * 6f).roundToInt()
    drawImage(
        image,
        dstOffset = IntOffset(((w - dw) / 2f).roundToInt() + drift, ((h - dh) / 2f).roundToInt()),
        dstSize = IntSize(dw.roundToInt(), dh.roundToInt()),
    )
    // Тонкий акцент сектора поверх фона, вместо плотной «полосы» в текстуре.
    val accent = SectorAccents[sector.coerceIn(0, SectorAccents.lastIndex)]
    drawRect(
        brush = Brush.radialGradient(
            colors = listOf(accent.copy(alpha = 0.09f), Color.Transparent),
            center = Offset(w * 0.82f, h * 0.18f),
            radius = max(w, h) * 0.55f,
        ),
    )
}

private fun DrawScope.drawPlanet(engine: GameEngine, art: GameArt, style: Int, time: Float, colorblind: Boolean) {
    val center = Offset(engine.centerX, engine.centerY)
    val image = art.planets[(engine.sectorId + style) % art.planets.size]
    // Видимый радиус спрайта ≈ 0.456 * половины side: выравниваем его с радиусом планеты движка.
    val diameter = (engine.planetRadius * 2f / PLANET_SPRITE_VISIBLE).roundToInt()
    drawGlow(center, engine.planetRadius * 1.7f, if (colorblind) Contrast.Bonus else Color(0xFF7C5CFF), 0.22f)
    translate(center.x, center.y) {
        rotate(time * 0.9f) {
            drawImage(
                image,
                dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                dstSize = IntSize(diameter, diameter),
            )
        }
    }
    drawCircle(
        color = if (colorblind) Contrast.Outline.copy(alpha = 0.42f) else Color.White.copy(alpha = 0.16f),
        radius = engine.planetRadius,
        center = center,
        style = Stroke(width = 1.5.dp.toPx()),
    )
}

private fun DrawScope.drawComboRing(engine: GameEngine) {
    if (engine.combo <= 0) return
    val ringRadius = engine.planetRadius * 1.42f
    val sweep = 360f * (engine.comboTime / 2.5f).coerceIn(0f, 1f)
    drawArc(
        color = engine.skinColor.copy(alpha = 0.55f),
        startAngle = -90f,
        sweepAngle = sweep,
        useCenter = false,
        topLeft = Offset(engine.centerX - ringRadius, engine.centerY - ringRadius),
        size = Size(ringRadius * 2f, ringRadius * 2f),
        style = Stroke(width = 3.dp.toPx(), cap = StrokeCap.Round),
    )
}

private fun DrawScope.drawOrbitRing(engine: GameEngine) {
    drawCircle(
        color = Color(0xFFA78BFA).copy(alpha = 0.26f),
        radius = engine.orbitRadius,
        center = Offset(engine.centerX, engine.centerY),
        style = Stroke(
            width = 1.5.dp.toPx(),
            pathEffect = androidx.compose.ui.graphics.PathEffect.dashPathEffect(floatArrayOf(9.dp.toPx(), 13.dp.toPx())),
        ),
    )
}

private fun DrawScope.drawWells(engine: GameEngine, art: GameArt, colorblind: Boolean) {
    engine.wells.forEach { well ->
        val x = engine.centerX + cos(well.angle) * engine.orbitRadius * 0.72f
        val y = engine.centerY + sin(well.angle) * engine.orbitRadius * 0.72f
        val radius = engine.planetRadius * (0.8f + 0.12f * sin(well.pulse))
        val color = if (colorblind) Contrast.Hazard else Palette.Slow
        drawImage(
            art.ring,
            dstOffset = IntOffset((x - radius).roundToInt(), (y - radius).roundToInt()),
            dstSize = IntSize((radius * 2).roundToInt(), (radius * 2).roundToInt()),
            colorFilter = ColorFilter.tint(color.copy(alpha = 0.55f)),
        )
        drawGlow(Offset(x, y), radius, Color.Black, 0.7f)
        if (colorblind) {
            drawCircle(color = Contrast.Outline.copy(alpha = 0.5f), radius = radius, center = Offset(x, y),
                style = Stroke(width = 2.dp.toPx()))
        }
    }
}

private fun DrawScope.drawWormholes(engine: GameEngine, art: GameArt, colorblind: Boolean) {
    engine.wormholes.forEach { worm ->
        val x = engine.centerX + cos(worm.angle) * engine.orbitRadius
        val y = engine.centerY + sin(worm.angle) * engine.orbitRadius
        val radius = engine.playerRadius * 5f
        val color = if (colorblind) Contrast.Bonus else Palette.Phase
        translate(x, y) {
            rotate(worm.pulse * 70f) {
                val sizePx = (radius * 2).roundToInt()
                drawImage(
                    art.ring,
                    dstOffset = IntOffset(-sizePx / 2, -sizePx / 2),
                    dstSize = IntSize(sizePx, sizePx),
                    colorFilter = ColorFilter.tint(color),
                )
            }
        }
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(Color(0xFF1B0A3C), color.copy(alpha = 0.5f), Color.Transparent),
                center = Offset(x, y),
                radius = radius,
            ),
            radius = radius * 0.8f,
            center = Offset(x, y),
        )
    }
}

private fun DrawScope.drawBosses(engine: GameEngine, art: GameArt, time: Float, colorblind: Boolean) {
    val bossColor = if (colorblind) Contrast.HazardHot else Palette.Boss
    engine.boss.forEach { boss ->
        val x = engine.centerX + cos(boss.angle) * engine.orbitRadius
        val y = engine.centerY + sin(boss.angle) * engine.orbitRadius
        val radius = engine.playerRadius * 6f
        val pulse = if (time == 0f) 1f else 1f + 0.14f * sin(boss.time * 5f)
        // Та же прозрачность 0.35, что и в базовой версии.
        drawGlow(Offset(x, y), radius * 3f * pulse, bossColor, 0.35f)
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(
                    if (boss.hitFlash > 0f) Color.White else Color(0xFF3A0A24),
                    bossColor.copy(alpha = 0.85f),
                ),
                center = Offset(x, y),
                radius = radius,
            ),
            radius = radius * pulse,
            center = Offset(x, y),
        )
        drawCircle(color = Color.White.copy(alpha = 0.8f), radius = radius * 0.35f, center = Offset(x, y))
        val arcRadius = radius * 1.35f
        drawArc(
            color = bossColor,
            startAngle = -90f,
            sweepAngle = 360f * boss.hp.toFloat() / max(1f, 3f + engine.level / 15f),
            useCenter = false,
            topLeft = Offset(x - arcRadius, y - arcRadius),
            size = Size(arcRadius * 2, arcRadius * 2),
            style = Stroke(width = (if (colorblind) 5.dp.toPx() else 3.dp.toPx()), cap = StrokeCap.Round),
        )
        if (colorblind) drawChevron(Offset(x, y), radius * 0.65f, Contrast.Outline, 3.dp.toPx())
    }
}

private fun DrawScope.drawTrail(engine: GameEngine, art: GameArt, style: Int, time: Float) {
    engine.trail.forEachIndexed { index, point ->
        val life = point.life.coerceIn(0f, 1f)
        val color = when (style) {
            2 -> Color.hsv((time * 90f + index * 12f) % 360f, 0.75f, 1f)
            else -> point.color
        }
        val radius = engine.playerRadius * when (style) {
            1 -> (0.6f + life * 1.5f)
            3 -> (0.4f + life * 0.9f) * (1f + 0.35f * sin(index * 0.8f + time * 9f))
            else -> (0.4f + life * 0.8f)
        }
        val alpha = life * if (style == 1) 0.5f else 0.35f
        drawGlow(Offset(point.x, point.y), radius * 1.6f, color, alpha)
    }
}

private fun DrawScope.drawOrbs(engine: GameEngine, art: GameArt, colorblind: Boolean, reducedMotion: Boolean) {
    engine.orbs.forEach { orb ->
        val pulse = if (colorblind || reducedMotion) 1f else 1f + 0.12f * sin(orb.pulse)
        val x = engine.centerX + cos(orb.angle) * engine.orbitRadius
        val y = engine.centerY + sin(orb.angle) * engine.orbitRadius
        val radius = engine.playerRadius * (if (orb.kind == OrbKind.Artifact) 2.2f else 1.7f) * pulse
        val glowColor = when {
            colorblind -> if (orb.kind == OrbKind.Normal) Contrast.Bonus else Contrast.BonusAlt
            orb.kind == OrbKind.Normal -> Palette.Orb
            else -> Color(0xFFFFF3B0)
        }
        val sizePx = (radius * 2).roundToInt()
        drawGlow(Offset(x, y), radius * 2.8f, glowColor, 0.4f)
        drawImage(
            art.orbs[orb.sprite.coerceIn(0, art.orbs.lastIndex)],
            dstOffset = IntOffset((x - radius).roundToInt(), (y - radius).roundToInt()),
            dstSize = IntSize(sizePx, sizePx),
        )
        if (colorblind) {
            drawCircle(
                color = Contrast.Outline.copy(alpha = 0.85f),
                radius = radius * 0.8f,
                center = Offset(x, y),
                style = Stroke(width = 2.dp.toPx()),
            )
            if (orb.kind != OrbKind.Normal) drawDiamond(Offset(x, y), radius * 0.35f, Contrast.BonusAlt)
        }
    }
}

private fun DrawScope.drawPowerUps(engine: GameEngine, art: GameArt, colorblind: Boolean, reducedMotion: Boolean) {
    engine.powerUps.forEach { power ->
        val x = engine.centerX + cos(power.angle) * engine.orbitRadius
        val y = engine.centerY + sin(power.angle) * engine.orbitRadius
        val still = colorblind || reducedMotion
        val radius = engine.playerRadius * 2.5f * (if (still) 1f else 1f + 0.09f * sin(power.pulse))
        val sizePx = (radius * 2).roundToInt()
        val index = power.kind.ordinal.coerceIn(0, art.powers.lastIndex)
        val glowColor = if (colorblind) Contrast.BonusAlt else Palette.Shield
        drawGlow(Offset(x, y), radius * 2.4f, glowColor, 0.28f)
        translate(x, y) {
            rotate(if (still) 0f else sin(power.pulse * 0.5f) * 8f) {
                drawImage(
                    art.powers[index],
                    dstOffset = IntOffset(-sizePx / 2, -sizePx / 2),
                    dstSize = IntSize(sizePx, sizePx),
                )
            }
        }
        if (colorblind) drawDiamond(Offset(x, y), radius * 0.55f, Contrast.BonusAlt)
    }
}

private fun DrawScope.drawAsteroids(engine: GameEngine, art: GameArt, colorblind: Boolean) {
    val hazard = if (colorblind) Contrast.HazardHot else Palette.Danger
    engine.asteroids.forEach { asteroid ->
        val x = engine.centerX + cos(asteroid.angle) * asteroid.distance
        val y = engine.centerY + sin(asteroid.angle) * asteroid.distance
        val center = Offset(x, y)
        when (asteroid.kind) {
            AsteroidKind.Comet -> {
                val dx = cos(asteroid.angle)
                val dy = sin(asteroid.angle)
                val tailColor = if (colorblind) Contrast.Hazard else Color(0xFFFFB86B)
                val tailStart = Offset(x + dx * asteroid.radius, y + dy * asteroid.radius)
                val tailEnd = Offset(x + dx * asteroid.radius * 7f, y + dy * asteroid.radius * 7f)
                drawLine(
                    brush = Brush.linearGradient(
                        colors = listOf(tailColor.copy(alpha = 0.85f), Color.Transparent),
                        start = tailStart,
                        end = tailEnd,
                    ),
                    start = tailStart,
                    end = tailEnd,
                    strokeWidth = asteroid.radius * 0.9f,
                    cap = StrokeCap.Round,
                )
                val sprite = art.comets[asteroid.sprite % art.comets.size]
                val diameter = (asteroid.radius * 3.8f).roundToInt()
                translate(x, y) {
                    rotate(asteroid.angle * RAD) {
                        drawImage(
                            sprite,
                            dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                            dstSize = IntSize(diameter, diameter),
                        )
                    }
                }
                if (colorblind) markHazard(center, asteroid.radius, hazard)
            }

            AsteroidKind.Bolt -> {
                val dx = cos(asteroid.angle)
                val dy = sin(asteroid.angle)
                val color = if (colorblind) Contrast.Hazard else Palette.Boss
                drawLine(
                    color = color.copy(alpha = 0.9f),
                    start = Offset(x + dx * asteroid.radius * 4f, y + dy * asteroid.radius * 4f),
                    end = Offset(x - dx * asteroid.radius * 4f, y - dy * asteroid.radius * 4f),
                    strokeWidth = asteroid.radius * 1.2f,
                    cap = StrokeCap.Round,
                )
                if (colorblind) markHazard(center, asteroid.radius, hazard)
            }

            AsteroidKind.Mine -> {
                val sprite = art.asteroids[asteroid.sprite % art.asteroids.size]
                val diameter = (asteroid.radius * 3.6f).roundToInt()
                translate(x, y) {
                    rotate(asteroid.rotation * RAD) {
                        drawImage(
                            sprite,
                            dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                            dstSize = IntSize(diameter, diameter),
                        )
                    }
                }
                drawCircle(
                    color = hazard.copy(alpha = if (colorblind) 1f else 0.75f + 0.25f * sin(asteroid.pulse * 3f)),
                    radius = asteroid.radius * 0.75f,
                    center = center,
                    style = Stroke(width = asteroid.radius * (if (colorblind) 0.34f else 0.3f)),
                )
                drawCircle(color = hazard, radius = asteroid.radius * 0.3f, center = center)
                if (colorblind) markHazard(center, asteroid.radius, hazard)
            }

            AsteroidKind.Rock -> {
                val sprite = art.asteroids[asteroid.sprite % art.asteroids.size]
                val diameter = (asteroid.radius * 3.6f).roundToInt()
                translate(x, y) {
                    rotate(asteroid.rotation * RAD) {
                        drawImage(
                            sprite,
                            dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                            dstSize = IntSize(diameter, diameter),
                        )
                    }
                }
                if (colorblind) markHazard(center, asteroid.radius, hazard)
            }
        }
    }
}

/** Толстый контур + шеврон: опасности различимы без опоры на цвет. */
private fun DrawScope.markHazard(center: Offset, radius: Float, color: Color) {
    drawCircle(color = Contrast.Outline.copy(alpha = 0.75f), radius = radius * 1.15f, center = center,
        style = Stroke(width = (radius * 0.2f).coerceAtLeast(2.dp.toPx())))
    drawChevron(center, radius * 0.6f, color, (radius * 0.18f).coerceAtLeast(2.dp.toPx()))
}

private fun DrawScope.drawChevron(center: Offset, size: Float, color: Color, width: Float) {
    drawLine(
        color = color,
        start = Offset(center.x - size * 0.5f, center.y - size * 0.5f),
        end = Offset(center.x + size * 0.5f, center.y),
        strokeWidth = width,
        cap = StrokeCap.Round,
    )
    drawLine(
        color = color,
        start = Offset(center.x + size * 0.5f, center.y),
        end = Offset(center.x - size * 0.5f, center.y + size * 0.5f),
        strokeWidth = width,
        cap = StrokeCap.Round,
    )
}

/** Отличная от сферы форма для бонусов в контрастном режиме. */
private fun DrawScope.drawDiamond(center: Offset, size: Float, color: Color) {
    val path = androidx.compose.ui.graphics.Path().apply {
        moveTo(center.x, center.y - size)
        lineTo(center.x + size, center.y)
        lineTo(center.x, center.y + size)
        lineTo(center.x - size, center.y)
        close()
    }
    drawPath(path, color = color, style = Stroke(width = 2.dp.toPx()))
}

private fun DrawScope.drawPlayer(
    engine: GameEngine,
    art: GameArt,
    shipSprite: Int,
    skin: Color,
    time: Float,
    colorblind: Boolean,
) {
    val position = Offset(engine.playerX(), engine.playerY())
    val alpha = if (engine.invulnerable > 0f) {
        0.4f + 0.6f * abs(sin(time * 22f))
    } else {
        1f
    }
    val halo = engine.playerRadius * 4.5f
    drawGlow(position, halo, skin, 0.35f * alpha)

    if (engine.shield || engine.phaseTimer > 0f || engine.freezeTimer > 0f) {
        val color = when {
            engine.phaseTimer > 0f -> if (colorblind) Contrast.Bonus else Palette.Phase
            engine.freezeTimer > 0f -> if (colorblind) Contrast.BonusAlt else Palette.Freeze
            else -> if (colorblind) Contrast.BonusAlt else Palette.Shield
        }
        val pulse = if (colorblind) 1f else 1f + 0.08f * sin(time * 6f)
        drawCircle(
            color = color.copy(alpha = 0.75f),
            radius = engine.playerRadius * 2.7f * pulse,
            center = position,
            style = Stroke(width = engine.playerRadius * 0.4f),
        )
        drawCircle(color = color.copy(alpha = 0.12f), radius = engine.playerRadius * 2.7f * pulse, center = position)
    }
    if (engine.reverseFlash > 0f) {
        drawCircle(
            color = skin.copy(alpha = engine.reverseFlash * 0.5f),
            radius = engine.playerRadius * (1.5f + (1f - engine.reverseFlash) * 3f),
            center = position,
            style = Stroke(width = engine.playerRadius * 0.35f),
        )
    }

    val heading = engine.playerAngle + engine.direction * (PI.toFloat() / 2f)
    val diameter = (engine.playerRadius * 6.5f).roundToInt()
    translate(position.x, position.y) {
        rotate(heading * RAD + 90f) {
            drawImage(
                art.ships[shipSprite.coerceIn(0, art.ships.lastIndex)],
                dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                dstSize = IntSize(diameter, diameter),
                alpha = alpha,
            )
        }
    }
    drawCircle(color = Color.White.copy(alpha = alpha), radius = engine.playerRadius * 0.35f, center = position)
}

private fun DrawScope.drawParticles(engine: GameEngine, art: GameArt) {
    engine.particles.forEach { particle ->
        val life = (particle.life / particle.maxLife).coerceIn(0f, 1f)
        val radius = particle.size * (1.4f + life)
        drawGlow(Offset(particle.x, particle.y), radius * 1.4f, particle.color, life * 0.85f)
    }
}

/** Фон для хаба и экранов меню — та же текстура, что и в игре. */
fun DrawScope.drawMenuBackground(
    art: GameArt,
    style: Int,
    tick: Long,
    colorblind: Boolean = false,
    reducedMotion: Boolean = false,
) {
    val image = art.backgrounds[style % art.backgrounds.size]
    val scale = max(size.width / image.width, size.height / image.height) * 1.05f
    val dw = image.width * scale
    val dh = image.height * scale
    val shift = if (reducedMotion) 0 else (sin(tick * 0.004f) * 10f).roundToInt()
    drawImage(
        image,
        dstOffset = IntOffset(((size.width - dw) / 2f).roundToInt() + shift, ((size.height - dh) / 2f).roundToInt()),
        dstSize = IntSize(dw.roundToInt(), dh.roundToInt()),
    )
    drawRect(
        brush = Brush.radialGradient(
            colors = listOf(Color.Transparent, Color(0xFF05030F).copy(alpha = 0.86f)),
            center = Offset(size.width / 2f, size.height / 2f),
            radius = max(size.width, size.height) * 0.72f,
        ),
    )
    val accent = SectorAccents[style.coerceIn(0, SectorAccents.lastIndex)]
    drawRect(
        brush = Brush.radialGradient(
            colors = listOf(accent.copy(alpha = 0.06f), Color.Transparent),
            center = Offset(size.width * 0.82f, size.height * 0.16f),
            radius = max(size.width, size.height) * 0.55f,
        ),
    )
}

/** Планета для ангара/магазина. */
fun DrawScope.drawPreviewPlanet(
    art: GameArt,
    index: Int,
    center: Offset,
    radius: Float,
    tick: Long,
    colorblind: Boolean = false,
    reducedMotion: Boolean = false,
) {
    // radius — видимый радиус: компенсируем прозрачные поля спрайта.
    val diameter = (radius * 2f / PLANET_SPRITE_VISIBLE).roundToInt()
    drawGlow(center, radius * 1.7f, if (colorblind) Contrast.Bonus else Color(0xFF7C5CFF), 0.22f)
    translate(center.x, center.y) {
        rotate(if (reducedMotion) 0f else tick * 0.01f) {
            drawImage(
                art.planets[index % art.planets.size],
                dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                dstSize = IntSize(diameter, diameter),
            )
        }
    }
    drawCircle(
        color = if (colorblind) Contrast.Outline.copy(alpha = 0.42f) else Color.White.copy(alpha = 0.16f),
        radius = radius,
        center = center,
        style = Stroke(width = 1.5.dp.toPx()),
    )
}
