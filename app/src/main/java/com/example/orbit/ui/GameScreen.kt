package com.example.orbit.ui

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
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
import com.example.orbit.game.GameEngine
import com.example.orbit.game.GameStatus

private val Neon = Color(0xFF22D3EE)
private val Amber = Color(0xFFFBBF24)
private val Pink = Color(0xFFF472B6)

@Composable
fun GameScreen(viewModel: GameViewModel = viewModel()) {
    val best by viewModel.best.collectAsStateWithLifecycle()
    val engine = remember { GameEngine() }
    val haptics = LocalHapticFeedback.current
    val lifecycleOwner = LocalLifecycleOwner.current

    var frame by remember { mutableLongStateOf(0L) }

    SideEffect {
        engine.best = best
        engine.onCollect = { haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove) }
        engine.onCrash = { haptics.performHapticFeedback(HapticFeedbackType.LongPress) }
        engine.onNewRecord = { score -> viewModel.submit(score) }
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

    DisposableEffect(lifecycleOwner, engine) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_PAUSE) engine.pause()
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
            modifier = Modifier.align(Alignment.TopCenter),
        )

        when {
            engine.paused -> CenterCard {
                Text(
                    text = stringResource(R.string.paused_title),
                    color = Color.White,
                    fontSize = 28.sp,
                    fontWeight = FontWeight.Black,
                    letterSpacing = 6.sp,
                )
                Spacer(Modifier.height(12.dp))
                Text(
                    text = stringResource(R.string.paused_hint),
                    color = Color.White.copy(alpha = 0.65f),
                    fontSize = 15.sp,
                )
            }

            engine.status == GameStatus.Ready -> ReadyCard()

            engine.status == GameStatus.GameOver -> GameOverCard(
                score = engine.score,
                best = maxOf(best, engine.score),
                isNewRecord = engine.isNewRecord,
                onRestart = { engine.start() },
            )
        }
    }
}

@Composable
private fun Hud(score: Int, best: Int, modifier: Modifier = Modifier) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .statusBarsPadding()
            .padding(horizontal = 24.dp, vertical = 16.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
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
private fun ReadyCard() {
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
        Spacer(Modifier.height(28.dp))
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
private fun GameOverCard(
    score: Int,
    best: Int,
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
        Spacer(Modifier.height(4.dp))
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
        Spacer(Modifier.height(24.dp))
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
