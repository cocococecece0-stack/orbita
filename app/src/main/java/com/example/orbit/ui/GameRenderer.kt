package com.example.orbit.ui

import androidx.compose.runtime.Composable
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.graphics.drawscope.translate
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
fun rememberGameArt(): GameArt = GameArt(
    backgrounds = listOf(
        imageResource(R.drawable.bg_0), imageResource(R.drawable.bg_1),
        imageResource(R.drawable.bg_2), imageResource(R.drawable.bg_3),
        imageResource(R.drawable.bg_4), imageResource(R.drawable.bg_5),
        imageResource(R.drawable.bg_6), imageResource(R.drawable.bg_7),
    ),
    planets = listOf(
        imageResource(R.drawable.planet_0), imageResource(R.drawable.planet_1),
        imageResource(R.drawable.planet_2), imageResource(R.drawable.planet_3),
        imageResource(R.drawable.planet_4), imageResource(R.drawable.planet_5),
        imageResource(R.drawable.planet_6), imageResource(R.drawable.planet_7),
    ),
    ships = listOf(
        imageResource(R.drawable.ship_0), imageResource(R.drawable.ship_1),
        imageResource(R.drawable.ship_2), imageResource(R.drawable.ship_3),
        imageResource(R.drawable.ship_4), imageResource(R.drawable.ship_5),
        imageResource(R.drawable.ship_6), imageResource(R.drawable.ship_7),
    ),
    asteroids = listOf(
        imageResource(R.drawable.ast_0), imageResource(R.drawable.ast_1),
        imageResource(R.drawable.ast_2), imageResource(R.drawable.ast_3),
        imageResource(R.drawable.ast_4), imageResource(R.drawable.ast_5),
        imageResource(R.drawable.ast_6), imageResource(R.drawable.ast_7),
    ),
    comets = listOf(
        imageResource(R.drawable.comet_0), imageResource(R.drawable.comet_1),
        imageResource(R.drawable.comet_2),
    ),
    powers = listOf(
        imageResource(R.drawable.power_0), imageResource(R.drawable.power_1),
        imageResource(R.drawable.power_2), imageResource(R.drawable.power_3),
        imageResource(R.drawable.power_4), imageResource(R.drawable.power_5),
        imageResource(R.drawable.power_6),
    ),
    orbs = listOf(
        imageResource(R.drawable.orb_0), imageResource(R.drawable.orb_1),
        imageResource(R.drawable.orb_2),
    ),
    glow = imageResource(R.drawable.fx_glow),
    ring = imageResource(R.drawable.fx_ring),
    burst = imageResource(R.drawable.fx_burst),
)

/** Рисует сцену. [frameTick] инвалидирует фазу анимации каждый кадр. */
fun DrawScope.drawGame(
    engine: GameEngine,
    frameTick: Long,
    art: GameArt,
    shipSprite: Int,
    planetStyle: Int,
    trailStyle: Int,
) {
    if (engine.width <= 0f) return
    val w = size.width
    val h = size.height
    val center = Offset(engine.centerX, engine.centerY)
    val skin = engine.skinColor

    drawBackground(art, engine.sectorId, planetStyle, engine.time, w, h)

    engine.stars.forEach { star ->
        val twinkle = 0.35f + 0.65f * abs(sin(star.phase + frameTick * 0.03f * star.speed))
        drawCircle(
            color = Color.White.copy(alpha = twinkle * 0.75f),
            radius = star.size,
            center = Offset(star.x * w, star.y * h),
        )
    }

    val shakeX = if (engine.shake > 0f) sin(engine.time * 83f) * engine.shake * 14f else 0f
    val shakeY = if (engine.shake > 0f) cos(engine.time * 71f) * engine.shake * 14f else 0f

    translate(shakeX, shakeY) {
        drawWells(engine, art)
        drawPlanet(engine, art, planetStyle)
        drawComboRing(engine)
        drawOrbitRing(engine)
        drawWormholes(engine, art)
        drawBosses(engine, art)
        drawTrail(engine, art, trailStyle)
        drawOrbs(engine, art)
        drawPowerUps(engine, art)
        drawAsteroids(engine, art)
        drawPlayer(engine, art, shipSprite, skin)
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
        val pulse = 0.5f + 0.5f * sin(engine.time * 12f)
        drawRect(color = Palette.Danger.copy(alpha = engine.stormWarning * (0.06f + 0.06f * pulse)))
    }
    if (engine.flareWarning > 0f) {
        drawRect(color = Palette.Orb.copy(alpha = engine.flareWarning * 0.10f))
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
        drawRect(color = Palette.Orb.copy(alpha = engine.levelUpFlash * 0.07f))
    }
    if (engine.shieldFlash > 0f) {
        drawRect(color = Palette.Shield.copy(alpha = engine.shieldFlash * 0.12f))
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
    val drift = (sin(time * 0.12f) * 8f).roundToInt()
    drawImage(
        image,
        dstOffset = IntOffset(((w - dw) / 2f).roundToInt() + drift, ((h - dh) / 2f).roundToInt()),
        dstSize = IntSize(dw.roundToInt(), dh.roundToInt()),
    )
}

private fun DrawScope.drawPlanet(engine: GameEngine, art: GameArt, style: Int) {
    val center = Offset(engine.centerX, engine.centerY)
    val image = art.planets[(engine.sectorId + style) % art.planets.size]
    val diameter = engine.planetRadius * 2.7f
    val sizePx = diameter.roundToInt()
    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(Color(0xFF7C5CFF).copy(alpha = 0.28f), Color.Transparent),
            center = center,
            radius = engine.planetRadius * 2.4f,
        ),
        radius = engine.planetRadius * 2.4f,
        center = center,
    )
    translate(center.x, center.y) {
        rotate(engine.time * 2.2f) {
            drawImage(
                image,
                dstOffset = IntOffset(-sizePx / 2, -sizePx / 2),
                dstSize = IntSize(sizePx, sizePx),
            )
        }
    }
    drawCircle(
        color = Color.White.copy(alpha = 0.16f),
        radius = engine.planetRadius,
        center = center,
        style = Stroke(width = engine.planetRadius * 0.025f),
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
            pathEffect = PathEffect.dashPathEffect(floatArrayOf(9.dp.toPx(), 13.dp.toPx())),
        ),
    )
}

private fun DrawScope.drawWells(engine: GameEngine, art: GameArt) {
    engine.wells.forEach { well ->
        val x = engine.centerX + cos(well.angle) * engine.orbitRadius * 0.72f
        val y = engine.centerY + sin(well.angle) * engine.orbitRadius * 0.72f
        val radius = engine.planetRadius * (0.8f + 0.12f * sin(well.pulse))
        drawImage(
            art.ring,
            dstOffset = IntOffset((x - radius).roundToInt(), (y - radius).roundToInt()),
            dstSize = IntSize((radius * 2).roundToInt(), (radius * 2).roundToInt()),
            colorFilter = ColorFilter.tint(Palette.Slow.copy(alpha = 0.55f)),
        )
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(Color.Black.copy(alpha = 0.7f), Color.Transparent),
                center = Offset(x, y),
                radius = radius,
            ),
            radius = radius,
            center = Offset(x, y),
        )
    }
}

private fun DrawScope.drawWormholes(engine: GameEngine, art: GameArt) {
    engine.wormholes.forEach { worm ->
        val x = engine.centerX + cos(worm.angle) * engine.orbitRadius
        val y = engine.centerY + sin(worm.angle) * engine.orbitRadius
        val radius = engine.playerRadius * 5f
        translate(x, y) {
            rotate(worm.pulse * 70f) {
                val sizePx = (radius * 2).roundToInt()
                drawImage(
                    art.ring,
                    dstOffset = IntOffset(-sizePx / 2, -sizePx / 2),
                    dstSize = IntSize(sizePx, sizePx),
                    colorFilter = ColorFilter.tint(Palette.Phase),
                )
            }
        }
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(Color(0xFF1B0A3C), Palette.Phase.copy(alpha = 0.5f), Color.Transparent),
                center = Offset(x, y),
                radius = radius,
            ),
            radius = radius * 0.8f,
            center = Offset(x, y),
        )
    }
}

private fun DrawScope.drawBosses(engine: GameEngine, art: GameArt) {
    engine.boss.forEach { boss ->
        val x = engine.centerX + cos(boss.angle) * engine.orbitRadius
        val y = engine.centerY + sin(boss.angle) * engine.orbitRadius
        val radius = engine.playerRadius * 6f
        val pulse = 1f + 0.14f * sin(boss.time * 5f)
        val glow = (radius * 3f * pulse).roundToInt()
        drawImage(
            art.glow,
            dstOffset = IntOffset((x - glow / 2f).roundToInt(), (y - glow / 2f).roundToInt()),
            dstSize = IntSize(glow, glow),
            alpha = 0.35f,
            colorFilter = ColorFilter.tint(Palette.Boss),
        )
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(
                    if (boss.hitFlash > 0f) Color.White else Color(0xFF3A0A24),
                    Palette.Boss.copy(alpha = 0.85f),
                ),
                center = Offset(x, y),
                radius = radius,
            ),
            radius = radius * pulse,
            center = Offset(x, y),
        )
        drawCircle(
            color = Color.White.copy(alpha = 0.8f),
            radius = radius * 0.35f,
            center = Offset(x, y),
        )
        val arcRadius = radius * 1.35f
        drawArc(
            color = Palette.Boss,
            startAngle = -90f,
            sweepAngle = 360f * boss.hp.toFloat() / max(1f, 3f + engine.level / 15f),
            useCenter = false,
            topLeft = Offset(x - arcRadius, y - arcRadius),
            size = Size(arcRadius * 2, arcRadius * 2),
            style = Stroke(width = 3.dp.toPx(), cap = StrokeCap.Round),
        )
    }
}

private fun DrawScope.drawTrail(engine: GameEngine, art: GameArt, style: Int) {
    engine.trail.forEachIndexed { index, point ->
        val life = point.life.coerceIn(0f, 1f)
        val color = when (style) {
            2 -> Color.hsv((engine.time * 90f + index * 12f) % 360f, 0.75f, 1f)
            else -> point.color
        }
        val radius = engine.playerRadius * when (style) {
            1 -> (0.6f + life * 1.5f)
            3 -> (0.4f + life * 0.9f) * (1f + 0.35f * sin(index * 0.8f + engine.time * 9f))
            else -> (0.4f + life * 0.8f)
        }
        val alpha = life * if (style == 1) 0.5f else 0.35f
        val sizePx = (radius * 2).roundToInt()
        drawImage(
            art.glow,
            dstOffset = IntOffset((point.x - radius).roundToInt(), (point.y - radius).roundToInt()),
            dstSize = IntSize(sizePx, sizePx),
            alpha = alpha,
            colorFilter = ColorFilter.tint(color),
        )
    }
}

private fun DrawScope.drawOrbs(engine: GameEngine, art: GameArt) {
    engine.orbs.forEach { orb ->
        val pulse = 1f + 0.12f * sin(orb.pulse)
        val x = engine.centerX + cos(orb.angle) * engine.orbitRadius
        val y = engine.centerY + sin(orb.angle) * engine.orbitRadius
        val radius = engine.playerRadius * (if (orb.kind == OrbKind.Artifact) 2.2f else 1.7f) * pulse
        val sizePx = (radius * 2).roundToInt()
        drawImage(
            art.glow,
            dstOffset = IntOffset((x - radius * 1.8f).roundToInt(), (y - radius * 1.8f).roundToInt()),
            dstSize = IntSize((sizePx * 1.8f).roundToInt(), (sizePx * 1.8f).roundToInt()),
            alpha = 0.4f,
            colorFilter = ColorFilter.tint(if (orb.kind == OrbKind.Normal) Palette.Orb else Color(0xFFFFF3B0)),
        )
        drawImage(
            art.orbs[orb.sprite.coerceIn(0, art.orbs.lastIndex)],
            dstOffset = IntOffset((x - radius).roundToInt(), (y - radius).roundToInt()),
            dstSize = IntSize(sizePx, sizePx),
        )
    }
}

private fun DrawScope.drawPowerUps(engine: GameEngine, art: GameArt) {
    engine.powerUps.forEach { power ->
        val x = engine.centerX + cos(power.angle) * engine.orbitRadius
        val y = engine.centerY + sin(power.angle) * engine.orbitRadius
        val radius = engine.playerRadius * 2.5f * (1f + 0.09f * sin(power.pulse))
        val sizePx = (radius * 2).roundToInt()
        val index = power.kind.ordinal.coerceIn(0, art.powers.lastIndex)
        drawImage(
            art.glow,
            dstOffset = IntOffset((x - radius * 1.5f).roundToInt(), (y - radius * 1.5f).roundToInt()),
            dstSize = IntSize((sizePx * 1.5f).roundToInt(), (sizePx * 1.5f).roundToInt()),
            alpha = 0.28f,
            colorFilter = ColorFilter.tint(Palette.Shield),
        )
        translate(x, y) {
            rotate(sin(power.pulse * 0.5f) * 8f) {
                drawImage(
                    art.powers[index],
                    dstOffset = IntOffset(-sizePx / 2, -sizePx / 2),
                    dstSize = IntSize(sizePx, sizePx),
                )
            }
        }
    }
}

private fun DrawScope.drawAsteroids(engine: GameEngine, art: GameArt) {
    engine.asteroids.forEach { asteroid ->
        val x = engine.centerX + cos(asteroid.angle) * asteroid.distance
        val y = engine.centerY + sin(asteroid.angle) * asteroid.distance
        when (asteroid.kind) {
            AsteroidKind.Comet -> {
                val dx = cos(asteroid.angle)
                val dy = sin(asteroid.angle)
                val tailStart = Offset(x + dx * asteroid.radius, y + dy * asteroid.radius)
                val tailEnd = Offset(x + dx * asteroid.radius * 7f, y + dy * asteroid.radius * 7f)
                drawLine(
                    brush = Brush.linearGradient(
                        colors = listOf(Color(0xFFFFB86B).copy(alpha = 0.85f), Color.Transparent),
                        start = tailStart,
                        end = tailEnd,
                    ),
                    start = tailStart,
                    end = tailEnd,
                    strokeWidth = asteroid.radius * 0.9f,
                    cap = StrokeCap.Round,
                )
                val sprite = art.comets[asteroid.sprite % art.comets.size]
                val diameter = (asteroid.radius * 3.6f).roundToInt()
                drawImage(
                    sprite,
                    dstOffset = IntOffset((x - diameter / 2f).roundToInt(), (y - diameter / 2f).roundToInt()),
                    dstSize = IntSize(diameter, diameter),
                )
            }

            AsteroidKind.Bolt -> {
                val dx = cos(asteroid.angle)
                val dy = sin(asteroid.angle)
                drawLine(
                    color = Palette.Boss.copy(alpha = 0.9f),
                    start = Offset(x + dx * asteroid.radius * 4f, y + dy * asteroid.radius * 4f),
                    end = Offset(x - dx * asteroid.radius * 4f, y - dy * asteroid.radius * 4f),
                    strokeWidth = asteroid.radius * 1.2f,
                    cap = StrokeCap.Round,
                )
            }

            AsteroidKind.Mine -> {
                val sprite = art.asteroids[asteroid.sprite % art.asteroids.size]
                val diameter = (asteroid.radius * 2.6f).roundToInt()
                translate(x, y) {
                    rotate(asteroid.rotation * 57.3f) {
                        drawImage(
                            sprite,
                            dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                            dstSize = IntSize(diameter, diameter),
                        )
                    }
                }
                drawCircle(
                    color = Palette.Danger.copy(alpha = 0.75f + 0.25f * sin(asteroid.pulse * 3f)),
                    radius = asteroid.radius * 0.75f,
                    center = Offset(x, y),
                    style = Stroke(width = asteroid.radius * 0.2f),
                )
                drawCircle(color = Palette.Danger, radius = asteroid.radius * 0.3f, center = Offset(x, y))
            }

            AsteroidKind.Rock -> {
                val sprite = art.asteroids[asteroid.sprite % art.asteroids.size]
                val diameter = (asteroid.radius * 2.6f).roundToInt()
                translate(x, y) {
                    rotate(asteroid.rotation * 57.3f) {
                        drawImage(
                            sprite,
                            dstOffset = IntOffset(-diameter / 2, -diameter / 2),
                            dstSize = IntSize(diameter, diameter),
                        )
                    }
                }
            }
        }
    }
}

private fun DrawScope.drawPlayer(engine: GameEngine, art: GameArt, shipSprite: Int, skin: Color) {
    val position = Offset(engine.playerX(), engine.playerY())
    val alpha = if (engine.invulnerable > 0f) {
        0.4f + 0.6f * abs(sin(engine.time * 22f))
    } else {
        1f
    }
    val halo = engine.playerRadius * 4.5f
    drawImage(
        art.glow,
        dstOffset = IntOffset((position.x - halo).roundToInt(), (position.y - halo).roundToInt()),
        dstSize = IntSize((halo * 2).roundToInt(), (halo * 2).roundToInt()),
        alpha = 0.35f * alpha,
        colorFilter = ColorFilter.tint(skin),
    )

    if (engine.shield || engine.phaseTimer > 0f || engine.freezeTimer > 0f) {
        val color = when {
            engine.phaseTimer > 0f -> Palette.Phase
            engine.freezeTimer > 0f -> Palette.Freeze
            else -> Palette.Shield
        }
        val pulse = 1f + 0.08f * sin(engine.time * 6f)
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
        rotate(heading * 57.2958f + 90f) {
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
        val sizePx = max(2, (radius * 2).roundToInt())
        drawImage(
            art.glow,
            dstOffset = IntOffset((particle.x - radius).roundToInt(), (particle.y - radius).roundToInt()),
            dstSize = IntSize(sizePx, sizePx),
            alpha = life * 0.85f,
            colorFilter = ColorFilter.tint(particle.color),
        )
    }
}

/** Фон для хаба и экранов меню — та же текстура, что и в игре. */
fun DrawScope.drawMenuBackground(art: GameArt, style: Int, tick: Long) {
    val image = art.backgrounds[style % art.backgrounds.size]
    val scale = max(size.width / image.width, size.height / image.height) * 1.05f
    val dw = image.width * scale
    val dh = image.height * scale
    val shift = (sin(tick * 0.004f) * 10f).roundToInt()
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
}

/** Планета для ангара/магазина. */
fun DrawScope.drawPreviewPlanet(art: GameArt, index: Int, center: Offset, radius: Float, tick: Long) {
    val sizePx = (radius * 2).roundToInt()
    translate(center.x, center.y) {
        rotate(tick * 0.01f) {
            drawImage(
                art.planets[index % art.planets.size],
                dstOffset = IntOffset(-sizePx / 2, -sizePx / 2),
                dstSize = IntSize(sizePx, sizePx),
            )
        }
    }
}
