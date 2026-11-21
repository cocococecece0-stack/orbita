package com.example.orbit.ui

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.unit.dp
import com.example.orbit.game.AsteroidKind
import com.example.orbit.game.GameEngine
import com.example.orbit.game.Palette
import com.example.orbit.game.PowerUpKind
import kotlin.math.PI
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.sin

private const val TAU = 6.2831855f

/** Draws the whole scene. [frameTick] keeps the draw phase invalidated every animation frame. */
fun DrawScope.drawGame(engine: GameEngine, frameTick: Long) {
    if (engine.width <= 0f) return

    val center = Offset(engine.centerX, engine.centerY)
    val w = size.width
    val h = size.height
    val skin = engine.skinColor

    drawRect(
        brush = Brush.radialGradient(
            colors = listOf(Color(0xFF1B1140), Color(0xFF0A0618), Color(0xFF030208)),
            center = center,
            radius = max(w, h) * 0.8f,
        ),
    )

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
        drawPlanet(engine, center)
        drawComboRing(engine, center)
        drawOrbitRing(engine, center)
        drawTrail(engine, skin)
        drawOrbs(engine, center)
        drawPowerUps(engine, center)
        drawAsteroids(engine, center)
        drawPlayer(engine, skin)
        drawParticles(engine)
    }

    // High combo paints the screen edges.
    if (engine.multiplier >= 3) {
        drawRect(
            brush = Brush.radialGradient(
                colors = listOf(
                    Color.Transparent,
                    skin.copy(alpha = (engine.multiplier - 2) * 0.07f),
                ),
                center = center,
                radius = max(w, h) * 0.72f,
            ),
        )
    }

    // Meteor storm warning.
    if (engine.stormWarning > 0f) {
        val pulse = 0.5f + 0.5f * sin(engine.time * 12f)
        drawRect(color = Palette.Danger.copy(alpha = engine.stormWarning * (0.06f + 0.06f * pulse)))
    }

    // Near-miss flash.
    if (engine.flash > 0f) {
        drawRect(
            brush = Brush.radialGradient(
                colors = listOf(
                    Color.White.copy(alpha = engine.flash * 0.20f),
                    Color.Transparent,
                ),
                center = center,
                radius = max(w, h) * 0.7f,
            ),
        )
    }

    // Shield break flash.
    if (engine.shieldFlash > 0f) {
        drawRect(color = Palette.Shield.copy(alpha = engine.shieldFlash * 0.12f))
    }

    // Vignette keeps the HUD readable on top of the starfield.
    drawRect(
        brush = Brush.radialGradient(
            colors = listOf(Color.Transparent, Color.Black.copy(alpha = 0.55f)),
            center = center,
            radius = max(w, h) * 0.72f,
        ),
    )
}

private fun DrawScope.drawPlanet(engine: GameEngine, center: Offset) {
    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(Color(0xFF7C5CFF).copy(alpha = 0.35f), Color.Transparent),
            center = center,
            radius = engine.planetRadius * 2.4f,
        ),
        radius = engine.planetRadius * 2.4f,
        center = center,
    )
    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(Color(0xFF6A4BE0), Color(0xFF241246)),
            center = center,
            radius = engine.planetRadius,
        ),
        radius = engine.planetRadius,
        center = center,
    )
    drawCircle(
        color = Color(0xFF9B8CFF).copy(alpha = 0.35f),
        radius = engine.planetRadius,
        center = center,
        style = Stroke(width = engine.planetRadius * 0.035f),
    )
}

private fun DrawScope.drawComboRing(engine: GameEngine, center: Offset) {
    if (engine.combo <= 0) return
    val ringRadius = engine.planetRadius * 1.28f
    val sweep = 360f * (engine.comboTime / 2.5f).coerceIn(0f, 1f)
    drawArc(
        color = engine.skinColor.copy(alpha = 0.5f),
        startAngle = -90f,
        sweepAngle = sweep,
        useCenter = false,
        topLeft = Offset(center.x - ringRadius, center.y - ringRadius),
        size = Size(ringRadius * 2f, ringRadius * 2f),
        style = Stroke(width = 3.dp.toPx(), cap = StrokeCap.Round),
    )
}

private fun DrawScope.drawOrbitRing(engine: GameEngine, center: Offset) {
    drawCircle(
        color = Color(0xFF6D5BD0).copy(alpha = 0.22f),
        radius = engine.orbitRadius,
        center = center,
        style = Stroke(
            width = 1.5.dp.toPx(),
            pathEffect = PathEffect.dashPathEffect(floatArrayOf(9.dp.toPx(), 13.dp.toPx())),
        ),
    )
}

private fun DrawScope.drawTrail(engine: GameEngine, skin: Color) {
    engine.trail.forEach { point ->
        val life = point.life.coerceIn(0f, 1f)
        drawCircle(
            color = skin.copy(alpha = life * 0.35f),
            radius = engine.playerRadius * (0.4f + life * 0.8f),
            center = Offset(point.x, point.y),
        )
    }
}

private fun DrawScope.drawOrbs(engine: GameEngine, center: Offset) {
    engine.orbs.forEach { orb ->
        val pulse = 1f + 0.12f * sin(orb.pulse)
        val position = Offset(
            center.x + cos(orb.angle) * engine.orbitRadius,
            center.y + sin(orb.angle) * engine.orbitRadius,
        )
        val radius = engine.playerRadius * 1.5f * pulse
        drawCircle(
            color = Palette.Orb.copy(alpha = 0.22f),
            radius = radius * 2.1f,
            center = position,
        )
        drawCircle(
            brush = Brush.radialGradient(
                colors = listOf(Color(0xFFFFF3B0), Color(0xFFF59E0B)),
                center = position,
                radius = radius,
            ),
            radius = radius,
            center = position,
        )
    }
}

private fun DrawScope.drawPowerUps(engine: GameEngine, center: Offset) {
    engine.powerUps.forEach { powerUp ->
        val position = Offset(
            center.x + cos(powerUp.angle) * engine.orbitRadius,
            center.y + sin(powerUp.angle) * engine.orbitRadius,
        )
        val color = when (powerUp.kind) {
            PowerUpKind.Shield -> Palette.Shield
            PowerUpKind.Slow -> Palette.Slow
            PowerUpKind.Double -> Palette.Double
        }
        val radius = engine.playerRadius * 2.1f
        val wobble = 1f + 0.1f * sin(powerUp.pulse)

        drawCircle(color = color.copy(alpha = 0.18f), radius = radius * 1.9f, center = position)

        val diamond = Path().apply {
            moveTo(position.x, position.y - radius * wobble)
            lineTo(position.x + radius * wobble, position.y)
            lineTo(position.x, position.y + radius * wobble)
            lineTo(position.x - radius * wobble, position.y)
            close()
        }
        drawPath(diamond, color.copy(alpha = 0.85f))
        drawPath(
            path = diamond,
            color = Color.White.copy(alpha = 0.9f),
            style = Stroke(width = radius * 0.12f),
        )

        when (powerUp.kind) {
            PowerUpKind.Shield -> drawCircle(
                color = Color(0xFF04222A),
                radius = radius * 0.4f,
                center = position,
                style = Stroke(width = radius * 0.16f),
            )

            PowerUpKind.Slow -> {
                drawLine(
                    color = Color(0xFF1B1033),
                    start = Offset(position.x - radius * 0.28f, position.y - radius * 0.3f),
                    end = Offset(position.x - radius * 0.28f, position.y + radius * 0.3f),
                    strokeWidth = radius * 0.16f,
                    cap = StrokeCap.Round,
                )
                drawLine(
                    color = Color(0xFF1B1033),
                    start = Offset(position.x + radius * 0.28f, position.y - radius * 0.3f),
                    end = Offset(position.x + radius * 0.28f, position.y + radius * 0.3f),
                    strokeWidth = radius * 0.16f,
                    cap = StrokeCap.Round,
                )
            }

            PowerUpKind.Double -> {
                drawCircle(Color(0xFF2A1405), radius = radius * 0.13f, center = Offset(position.x - radius * 0.26f, position.y))
                drawCircle(Color(0xFF2A1405), radius = radius * 0.13f, center = Offset(position.x + radius * 0.26f, position.y))
            }
        }
    }
}

private fun DrawScope.drawAsteroids(engine: GameEngine, center: Offset) {
    engine.asteroids.forEach { asteroid ->
        val position = Offset(
            center.x + cos(asteroid.angle) * asteroid.distance,
            center.y + sin(asteroid.angle) * asteroid.distance,
        )

        if (asteroid.kind == AsteroidKind.Comet) {
            drawComet(asteroid, position)
            return@forEach
        }

        val path = Path()
        val count = asteroid.shape.size
        for (index in 0 until count) {
            val angle = asteroid.rotation + index * (TAU / count)
            val radius = asteroid.radius * asteroid.shape[index]
            val x = position.x + cos(angle) * radius
            val y = position.y + sin(angle) * radius
            if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
        }
        path.close()
        drawPath(path = path, color = Color(0xFF2A1D4D))
        drawPath(
            path = path,
            color = Palette.Asteroid.copy(alpha = 0.9f),
            style = Stroke(width = asteroid.radius * 0.16f),
        )
    }
}

private fun DrawScope.drawComet(asteroid: com.example.orbit.game.Asteroid, position: Offset) {
    val dx = cos(asteroid.angle)
    val dy = sin(asteroid.angle)
    val tailStart = Offset(position.x + dx * asteroid.radius, position.y + dy * asteroid.radius)
    val tailEnd = Offset(
        position.x + dx * asteroid.radius * 7f,
        position.y + dy * asteroid.radius * 7f,
    )
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
    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(Color(0xFFFFF1C1), Color(0xFFFB7185)),
            center = position,
            radius = asteroid.radius,
        ),
        radius = asteroid.radius,
        center = position,
    )
    drawCircle(
        color = Color(0xFFFFE4B5).copy(alpha = 0.9f),
        radius = asteroid.radius * 0.35f,
        center = position,
    )
}

private fun DrawScope.drawPlayer(engine: GameEngine, skin: Color) {
    val position = Offset(engine.playerX(), engine.playerY())
    val alpha = if (engine.invulnerable > 0f) {
        0.4f + 0.6f * abs(sin(engine.time * 22f))
    } else {
        1f
    }

    drawCircle(
        brush = Brush.radialGradient(
            colors = listOf(skin.copy(alpha = 0.35f * alpha), Color.Transparent),
            center = position,
            radius = engine.playerRadius * 4.5f,
        ),
        radius = engine.playerRadius * 4.5f,
        center = position,
    )

    if (engine.shield) {
        val pulse = 1f + 0.08f * sin(engine.time * 6f)
        drawCircle(
            color = Palette.Shield.copy(alpha = 0.75f),
            radius = engine.playerRadius * 2.7f * pulse,
            center = position,
            style = Stroke(width = engine.playerRadius * 0.4f),
        )
        drawCircle(
            color = Palette.Shield.copy(alpha = 0.12f),
            radius = engine.playerRadius * 2.7f * pulse,
            center = position,
        )
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
    val shipSize = engine.playerRadius * 2.3f
    val ship = Path().apply {
        moveTo(
            position.x + cos(heading) * shipSize,
            position.y + sin(heading) * shipSize,
        )
        lineTo(
            position.x + cos(heading + 2.6f) * shipSize * 0.8f,
            position.y + sin(heading + 2.6f) * shipSize * 0.8f,
        )
        lineTo(
            position.x + cos(heading - 2.6f) * shipSize * 0.8f,
            position.y + sin(heading - 2.6f) * shipSize * 0.8f,
        )
        close()
    }
    drawPath(ship, skin, alpha = alpha)
    drawCircle(color = Color.White.copy(alpha = alpha), radius = engine.playerRadius * 0.55f, center = position)
}

private fun DrawScope.drawParticles(engine: GameEngine) {
    engine.particles.forEach { particle ->
        val life = (particle.life / particle.maxLife).coerceIn(0f, 1f)
        drawCircle(
            color = particle.color.copy(alpha = life),
            radius = particle.size * (0.5f + life),
            center = Offset(particle.x, particle.y),
        )
    }
}
