package com.example.orbit.ui

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.example.orbit.GameViewModel
import com.example.orbit.R
import com.example.orbit.audio.SoundEngine
import com.example.orbit.game.GameEngine
import com.example.orbit.game.GameEvent
import com.example.orbit.game.GameStatus
import com.example.orbit.game.Palette
import kotlin.math.ceil

private val Neon = Color(0xFF22D3EE)
private val Amber = Color(0xFFFBBF24)
private val Pink = Color(0xFFF472B6)

@Composable
fun GameScreen(viewModel: GameViewModel = viewModel()) {
    val best by viewModel.best.collectAsStateWithLifecycle()
    val muted by viewModel.muted.collectAsStateWithLifecycle()
    val engine = remember { GameEngine() }
    val haptics = LocalHapticFeedback.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val context = LocalContext.current
    val sound = remember { SoundEngine(context) }

    var frame by remember { mutableLongStateOf(0L) }

    val slowSeconds by remember { derivedStateOf { ceil(engine.slowTimer).toInt() } }
    val doubleSeconds by remember { derivedStateOf { ceil(engine.doubleTimer).toInt() } }

    DisposableEffect(sound) {
        onDispose { sound.release() }
    }

    SideEffect {
        engine.best = best
        sound.muted = muted
        engine.onNewRecord = { score -> viewModel.submit(score) }
        engine.onEvent = { event ->
            when (event) {
                GameEvent.Tap -> {
                    sound.play(R.raw.tap)
                    sound.startAmbient()
                }

                is GameEvent.Dodge -> sound.play(
                    resId = R.raw.dodge,
                    rate = (1f + event.combo * 0.045f).coerceAtMost(1.9f),
                    volume = 0.45f,
                )

                GameEvent.Orb -> sound.play(R.raw.orb)

                is GameEvent.PowerUp -> {
                    sound.play(R.raw.powerup)
                    haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
                }

                GameEvent.ShieldBreak -> {
                    sound.play(R.raw.shield)
                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                }

                GameEvent.Crash -> {
                    sound.play(R.raw.crash)
                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                }

                is GameEvent.LevelUp -> sound.play(R.raw.level)

                GameEvent.NearMiss -> haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove)
            }
        }
    }

    LaunchedEffect(engine) {
        var lastNanos = 0L
        while (true) {
            withFrameNanos { now ->
                val dt = if (lastNanos == 0L) {
                    0f
                } else {
                    ((now - lastNanos) / 1_000_000_000f).coerceIn(0f, 0.05f)
                }
                lastNanos = now
                engine.update(dt)
                frame++
            }
        }
    }

    DisposableEffect(lifecycleOwner, engine, sound) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_PAUSE -> {
                    engine.pause()
                    sound.pauseAmbient()
                }

                Lifecycle.Event.ON_RESUME -> {
                    if (engine.status != GameStatus.Ready) sound.startAmbient()
                }

                else -> Unit
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFF05030F))
            .pointerInput(engine) {
                detectTapGestures { engine.tap() }
            },
    ) {
        Canvas(
            modifier = Modifier
                .fillMaxSize()
                .onSizeChanged { engine.resize(it.width, it.height) },
        ) {
            drawGame(engine, frame)
        }

        Hud(
            score = engine.score,
            best = maxOf(best, engine.best),
            multiplier = engine.multiplier,
            muted = muted,
            onPause = { engine.pause() },
            onToggleSound = { viewModel.toggleMuted() },
            modifier = Modifier.align(Alignment.TopCenter),
        )

        Row(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 104.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            if (engine.shield) {
                EffectChip(stringResource(R.string.power_shield), Palette.Shield)
            }
            if (slowSeconds > 0) {
                EffectChip("${stringResource(R.string.power_slow)} ${slowSeconds}с", Palette.Slow)
            }
            if (doubleSeconds > 0) {
                EffectChip("${stringResource(R.string.power_double)} ${doubleSeconds}с", Palette.Double)
            }
        }

        if (engine.levelUpFlash > 0f) {
            Text(
                text = stringResource(R.string.level_banner, engine.level),
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .statusBarsPadding()
                    .padding(top = 156.dp),
                color = Amber.copy(alpha = engine.levelUpFlash),
                fontSize = 15.sp,
                fontWeight = FontWeight.Bold,
                letterSpacing = 5.sp,
            )
        }

        when {
            engine.paused -> PausedCard(
                onResume = { engine.tap() },
                onRestart = { engine.start() },
            )

            engine.status == GameStatus.Ready -> ReadyCard(best = maxOf(best, engine.best))

            engine.status == GameStatus.GameOver -> GameOverCard(
                score = engine.score,
                best = maxOf(best, engine.score),
                level = engine.level,
                isNewRecord = engine.isNewRecord,
                onRestart = { engine.start() },
            )
        }
    }
}

@Composable
private fun Hud(
    score: Int,
    best: Int,
    multiplier: Int,
    muted: Boolean,
    onPause: () -> Unit,
    onToggleSound: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .statusBarsPadding()
            .padding(horizontal = 20.dp, vertical = 14.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.Top,
    ) {
        Column {
            Text(
                text = stringResource(R.string.score_label),
                color = Color.White.copy(alpha = 0.5f),
                fontSize = 11.sp,
                letterSpacing = 3.sp,
            )
            Text(
                text = "$score",
                color = Color.White,
                fontSize = 30.sp,
                fontWeight = FontWeight.Bold,
            )
            if (multiplier > 1) {
                Text(
                    text = stringResource(R.string.combo_badge, multiplier),
                    color = Amber,
                    fontSize = 13.sp,
                    fontWeight = FontWeight.Bold,
                )
            }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            RoundButton(label = if (muted) "🔇" else "🔊", onClick = onToggleSound)
            RoundButton(label = "II", onClick = onPause)
        }

        Column(horizontalAlignment = Alignment.End) {
            Text(
                text = stringResource(R.string.best_label),
                color = Color.White.copy(alpha = 0.5f),
                fontSize = 11.sp,
                letterSpacing = 3.sp,
            )
            Text(
                text = "$best",
                color = Neon.copy(alpha = 0.9f),
                fontSize = 30.sp,
                fontWeight = FontWeight.Bold,
            )
        }
    }
}

@Composable
private fun RoundButton(label: String, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .size(38.dp)
            .clip(CircleShape)
            .background(Color.White.copy(alpha = 0.08f))
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = label,
            color = Color.White.copy(alpha = 0.85f),
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold,
        )
    }
}

@Composable
private fun EffectChip(label: String, color: Color) {
    Surface(
        color = color.copy(alpha = 0.16f),
        shape = RoundedCornerShape(50),
    ) {
        Text(
            text = label,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp),
            color = color,
            fontSize = 10.sp,
            fontWeight = FontWeight.SemiBold,
            letterSpacing = 1.sp,
        )
    }
}

@Composable
private fun ReadyCard(best: Int) {
    val transition = rememberInfiniteTransition(label = "ready")
    val pulse by transition.animateFloat(
        initialValue = 0.3f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(tween(900), RepeatMode.Reverse),
        label = "pulse",
    )

    CenterCard {
        Text(
            text = stringResource(R.string.ready_title),
            color = Neon,
            fontSize = 42.sp,
            fontWeight = FontWeight.Black,
            letterSpacing = 10.sp,
        )
        Spacer(Modifier.height(12.dp))
        Text(
            text = stringResource(R.string.ready_subtitle),
            color = Color.White.copy(alpha = 0.75f),
            fontSize = 15.sp,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(6.dp))
        Text(
            text = stringResource(R.string.ready_hint),
            color = Color.White.copy(alpha = 0.45f),
            fontSize = 13.sp,
            textAlign = TextAlign.Center,
        )
        if (best > 0) {
            Spacer(Modifier.height(14.dp))
            Text(
                text = stringResource(R.string.best_score, best),
                color = Amber.copy(alpha = 0.9f),
                fontSize = 13.sp,
                fontWeight = FontWeight.SemiBold,
            )
        }
        Spacer(Modifier.height(26.dp))
        Text(
            text = stringResource(R.string.ready_tap),
            color = Neon.copy(alpha = pulse),
            fontSize = 13.sp,
            fontWeight = FontWeight.SemiBold,
            letterSpacing = 2.sp,
        )
    }
}

@Composable
private fun PausedCard(onResume: () -> Unit, onRestart: () -> Unit) {
    CenterCard {
        Text(
            text = stringResource(R.string.paused_title),
            color = Color.White,
            fontSize = 28.sp,
            fontWeight = FontWeight.Black,
            letterSpacing = 6.sp,
        )
        Spacer(Modifier.height(10.dp))
        Text(
            text = stringResource(R.string.paused_hint),
            color = Color.White.copy(alpha = 0.6f),
            fontSize = 14.sp,
        )
        Spacer(Modifier.height(24.dp))
        Button(
            onClick = onResume,
            modifier = Modifier
                .fillMaxWidth()
                .height(50.dp),
        ) {
            Text(stringResource(R.string.resume), fontWeight = FontWeight.SemiBold)
        }
        Spacer(Modifier.height(4.dp))
        TextButton(onClick = onRestart) {
            Text(stringResource(R.string.restart), color = Color.White.copy(alpha = 0.6f))
        }
    }
}

@Composable
private fun GameOverCard(
    score: Int,
    best: Int,
    level: Int,
    isNewRecord: Boolean,
    onRestart: () -> Unit,
) {
    CenterCard {
        Text(
            text = stringResource(R.string.game_over_title),
            color = Pink,
            fontSize = 12.sp,
            fontWeight = FontWeight.Bold,
            letterSpacing = 4.sp,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = "$score",
            color = Color.White,
            fontSize = 64.sp,
            fontWeight = FontWeight.Black,
        )
        Spacer(Modifier.height(2.dp))
        if (isNewRecord) {
            Text(
                text = stringResource(R.string.new_record),
                color = Amber,
                fontSize = 13.sp,
                fontWeight = FontWeight.Bold,
                letterSpacing = 2.sp,
            )
        } else {
            Text(
                text = stringResource(R.string.best_score, best),
                color = Color.White.copy(alpha = 0.55f),
                fontSize = 14.sp,
            )
        }
        Spacer(Modifier.height(6.dp))
        Text(
            text = stringResource(R.string.level_banner, level),
            color = Color.White.copy(alpha = 0.35f),
            fontSize = 11.sp,
            letterSpacing = 3.sp,
        )
        Spacer(Modifier.height(22.dp))
        Button(
            onClick = onRestart,
            modifier = Modifier
                .fillMaxWidth()
                .height(52.dp),
        ) {
            Text(stringResource(R.string.play_again), fontWeight = FontWeight.SemiBold)
        }
    }
}

@Composable
private fun CenterCard(content: @Composable ColumnScope.() -> Unit) {
    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Surface(
            color = Color(0xE60A0618),
            shape = RoundedCornerShape(28.dp),
            modifier = Modifier.padding(32.dp),
        ) {
            Column(
                modifier = Modifier.padding(horizontal = 28.dp, vertical = 32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                content = content,
            )
        }
    }
}
