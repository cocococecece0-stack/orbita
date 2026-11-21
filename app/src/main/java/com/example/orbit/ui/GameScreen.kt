package com.example.orbit.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.ui.input.pointer.awaitPointerEvent
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.viewmodel.compose.viewModel
import com.example.orbit.GameViewModel
import com.example.orbit.R
import com.example.orbit.audio.SoundEngine
import com.example.orbit.data.RewardSummary
import com.example.orbit.game.Content
import com.example.orbit.game.GameEngine
import com.example.orbit.game.GameEvent
import com.example.orbit.game.GameStatus
import com.example.orbit.game.Mode
import com.example.orbit.game.Palette
import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.min
import kotlin.math.roundToInt
import kotlinx.coroutines.delay

// Новый визуальный язык: уличная аркада — тёплая бумага, лайм, коралл, плотный инк.
private val Ink = Color(0xFF111526)
private val Paper = Color(0xFFFFF6E8)
private val Neon = Color(0xFFD8FF3E)
private val Amber = Color(0xFFFFC23D)
private val Pink = Color(0xFFFF5B3D)
private val Violet = Color(0xFF8B5CF6)
private val DangerRed = Color(0xFFFF4D4D)
private val PanelColor = Paper
private val MutedText = Color(0xFF5D6478)

private enum class Screen { Hub, League, Missions, Hangar, Shop, Achievements, Stats, Settings }

@Composable
fun GameScreen(viewModel: GameViewModel = viewModel()) {
    LaunchedEffect(viewModel.revision) { }
    val save = viewModel.store.data
    val context = LocalContext.current
    val haptics = LocalHapticFeedback.current
    val lifecycleOwner = LocalLifecycleOwner.current

    val engine = remember { GameEngine() }
    val art = rememberGameArt()
    val sound = remember { SoundEngine(context) }

    var screen by remember { mutableStateOf<Screen?>(Screen.Hub) }
    var frame by remember { mutableLongStateOf(0L) }
    var toast by remember { mutableStateOf<String?>(null) }
    var consumedReward by remember { mutableStateOf<RewardSummary?>(null) }
    var confirmReset by remember { mutableStateOf(false) }
    var confirmExit by remember { mutableStateOf(false) }

    val settings = save.settings
    val selectedMode = Mode.from(save.selectedMode)
    val bestForMode = save.bestByMode[selectedMode.id] ?: 0

    // Движок обновляет эти поля каждый кадр, поэтому пересчёт привязан к frame.
    val timerSeconds by remember(frame) { derivedStateOf { if (engine.timeLeft > 0f) ceil(engine.timeLeft).toInt() else 0 } }
    val slowSeconds by remember(frame) { derivedStateOf { ceil(engine.slowTimer).toInt() } }
    val doubleSeconds by remember(frame) { derivedStateOf { ceil(engine.doubleTimer).toInt() } }
    val magnetSeconds by remember(frame) { derivedStateOf { ceil(engine.magnetTimer).toInt() } }
    val phaseSeconds by remember(frame) { derivedStateOf { ceil(engine.phaseTimer).toInt() } }
    val freezeSeconds by remember(frame) { derivedStateOf { ceil(engine.freezeTimer).toInt() } }

    val hudVisible = screen == null && engine.status == GameStatus.Playing && !engine.paused &&
        engine.draft == null && !confirmExit

    fun haptic(type: HapticFeedbackType) {
        if (!viewModel.store.data.settings.haptics) return
        val resolved = when (viewModel.store.data.settings.hapticProfile) {
            "SOFT" -> HapticFeedbackType.TextHandleMove
            "STRONG" -> HapticFeedbackType.LongPress
            else -> type
        }
        haptics.performHapticFeedback(resolved)
    }

    fun startRun() {
        val seed = if (selectedMode == Mode.Daily) Content.seedFor(save.dailyDate)
        else (System.currentTimeMillis() and 0x7fffffff).toInt()
        engine.setUpgrades(viewModel.upgrades())
        engine.setBest(bestForMode)
        engine.setReducedMotion(settings.reducedMotion)
        engine.setSeasonModifier(Content.seasonModifier(save.seasonId).second)
        engine.skinColor = shipColor(save.equippedShip, settings.colorblind)
        engine.startRun(selectedMode, save.selectedSector, seed)
        viewModel.markTutorialSeen()
        screen = null
        sound.startMusic(engine.sectorId)
        haptic(HapticFeedbackType.LongPress)
    }

    fun finishGoHub() {
        // abandonRun уже зафиксировал результат; toReady вызываем только после submitRun.
        engine.setBest(save.bestByMode[selectedMode.id] ?: 0)
        engine.toReady()
        sound.stopMusic()
        confirmExit = false
        screen = Screen.Hub
        sound.play("ui")
    }

    fun goHub() {
        if (screen == null && engine.status == GameStatus.Playing) {
            confirmExit = true
            sound.play("ui")
            return
        }
        finishGoHub()
    }

    fun openScreen(next: Screen) {
        screen = next
        sound.play("ui")
    }

    SideEffect {
        engine.skinColor = shipColor(save.equippedShip, settings.colorblind)
        engine.setReducedMotion(settings.reducedMotion)
        engine.setUpgrades(viewModel.upgrades())
        engine.setSeasonModifier(Content.seasonModifier(save.seasonId).second)
        sound.applySettings(settings)
    }

    // Рекорд синхронизируем из сохранения при старте и после завершения забега,
    // но не на каждой перерисовке во время игры.
    LaunchedEffect(bestForMode, engine.status) {
        if (engine.status != GameStatus.Playing) engine.setBest(bestForMode)
    }

    DisposableEffect(sound) { onDispose { sound.release() } }

    LaunchedEffect(engine) {
        var lastNanos = 0L
        while (true) {
            withFrameNanos { now ->
                val dt = if (lastNanos == 0L) 0f else ((now - lastNanos) / 1_000_000_000f).coerceIn(0f, 0.05f)
                lastNanos = now
                engine.update(dt)
                frame++
            }
        }
    }

    LaunchedEffect(Unit) {
        engine.onEvent = { event ->
            when (event) {
                GameEvent.Tap -> { sound.play("tap"); haptic(HapticFeedbackType.TextHandleMove) }
                is GameEvent.BlockMove -> sound.play("tap", 1.18f, 0.16f)
                GameEvent.BlockRotate -> sound.play("tap", 1.35f, 0.22f)
                GameEvent.BlockHold -> { sound.play("powerup", 1.25f, 0.35f); haptic(HapticFeedbackType.TextHandleMove) }
                is GameEvent.BlockDrop -> { sound.play("dodge", 1f, 0.28f); haptic(HapticFeedbackType.TextHandleMove) }
                is GameEvent.BlockClear -> {
                    sound.play(if (event.lines >= 4) "victory" else "reward", 1f, 0.8f)
                    haptic(HapticFeedbackType.LongPress)
                }
                GameEvent.BlockTopOut -> { sound.play("crash"); haptic(HapticFeedbackType.LongPress) }
                is GameEvent.Dodge -> sound.play("dodge", 1f + event.combo * 0.045f, 0.45f)
                GameEvent.Orb -> sound.play("orb")
                is GameEvent.GoldenOrb -> { sound.play("reward", 1.2f, 0.7f); haptic(HapticFeedbackType.TextHandleMove) }
                GameEvent.Artifact -> { sound.play("secret"); haptic(HapticFeedbackType.LongPress) }
                is GameEvent.PowerUp -> {
                    sound.play(if (event.kind.id == "revive") "revive" else if (event.kind.id == "phase") "phase" else "powerup")
                    haptic(HapticFeedbackType.TextHandleMove)
                }
                GameEvent.ShieldBreak -> { sound.play("shield"); haptic(HapticFeedbackType.LongPress) }
                GameEvent.Crash -> { sound.play("crash"); haptic(HapticFeedbackType.LongPress) }
                is GameEvent.LevelUp -> { sound.play("level"); haptic(HapticFeedbackType.TextHandleMove) }
                GameEvent.NearMiss -> { sound.play("tick", 1.4f, 0.25f); haptic(HapticFeedbackType.TextHandleMove) }
                GameEvent.Storm -> { sound.play("storm"); haptic(HapticFeedbackType.LongPress) }
                GameEvent.Wormhole -> { sound.play("wormhole"); haptic(HapticFeedbackType.LongPress) }
                GameEvent.Flare -> sound.play("flare")
                GameEvent.BossSpawn -> { sound.play("boss"); haptic(HapticFeedbackType.LongPress) }
                GameEvent.BossHit -> sound.play("boss_hit", 1f, 0.6f)
                GameEvent.BossDown -> { sound.play("boss_down"); haptic(HapticFeedbackType.LongPress) }
                GameEvent.Resonance -> { sound.play("reward", 0.9f, 0.8f); haptic(HapticFeedbackType.LongPress) }
                GameEvent.Finish -> sound.play("victory")
                GameEvent.Tick -> Unit
            }
        }
        engine.onNewRecord = { sound.play("achievement") }
        engine.onRunEnd = { viewModel.submitRun(engine) }
    }

    val reward = viewModel.lastReward
    LaunchedEffect(reward, screen, confirmExit) {
        if (reward == null || reward == consumedReward) return@LaunchedEffect
        consumedReward = reward
        // Награда за забег показывается в карточке результата, а не поверх неё тостом.
        if (screen != null || confirmExit || engine.status == GameStatus.GameOver || engine.paused) return@LaunchedEffect
        toast = rewardLines(reward).joinToString(" · ")
        delay(4200)
        toast = null
    }
    LaunchedEffect(screen, confirmExit, engine.status, engine.paused) {
        if (screen != null || confirmExit || engine.status == GameStatus.GameOver || engine.paused) toast = null
    }

    DisposableEffect(lifecycleOwner, engine, sound) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_PAUSE -> {
                    // Выбор мутации уже замораживает симуляцию: не оставляем игру на паузе после выбора.
                    if (engine.draft == null) engine.pause()
                    sound.pauseMusic()
                }
                Lifecycle.Event.ON_RESUME -> {
                    if (screen == null && engine.status == GameStatus.Playing && !engine.paused) sound.resumeMusic()
                }
                else -> Unit
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    val currentDensity = LocalDensity.current
    CompositionLocalProvider(
        LocalDensity provides Density(
            currentDensity.density,
            currentDensity.fontScale * if (settings.largeText) 1.5f else 1f,
        ),
    ) {
    Box(modifier = Modifier.fillMaxSize().background(Color(0xFF101528))) {
        if (screen == null) {
            Canvas(
                modifier = Modifier
                    .fillMaxSize()
                    .onSizeChanged { engine.resize(it.width, it.height) }
                    .then(
                        if (engine.status == GameStatus.Playing && !engine.paused && engine.draft == null && !confirmExit) {
                            Modifier.pointerInput(engine) {
                                awaitEachGesture {
                                    val down = awaitFirstDown(requireUnconsumed = false)
                                    var accX = 0f
                                    var accY = 0f
                                    var moved = false
                                    while (true) {
                                        val event = awaitPointerEvent(PointerEventPass.Main)
                                        val change = event.changes.firstOrNull { it.id == down.id } ?: break
                                        if (!change.pressed) break
                                        val dx = change.position.x - change.previousPosition.x
                                        val dy = change.position.y - change.previousPosition.y
                                        if (engine.mode == Mode.Stack) {
                                            accX += dx
                                            accY += dy
                                            while (abs(accX) >= 20f) {
                                                val dir = if (accX > 0f) 1 else -1
                                                engine.blockMove(dir)
                                                accX -= dir * 20f
                                                moved = true
                                            }
                                            while (accY >= 26f) {
                                                engine.blockSoftDrop()
                                                accY -= 26f
                                                moved = true
                                            }
                                            if (accY <= -44f) {
                                                engine.blockRotate()
                                                accY += 44f
                                                moved = true
                                            }
                                        }
                                    }
                                    if (!moved) {
                                        if (engine.mode == Mode.Stack) {
                                            val x = down.position.x
                                            val y = down.position.y
                                            val w = size.width.toFloat()
                                            val h = size.height.toFloat()
                                            when {
                                                x < w * 0.22f -> engine.blockMove(-1)
                                                x > w * 0.78f -> engine.blockMove(1)
                                                y > h * 0.74f -> engine.blockHardDrop()
                                                else -> engine.blockRotate()
                                            }
                                        } else {
                                            engine.tap()
                                        }
                                    }
                                }
                            }
                        } else Modifier,
                    ),
            ) {
                drawGame(
                    engine, frame, art,
                    shipSprite = save.equippedShip,
                    planetStyle = save.equippedPlanet,
                    trailStyle = save.equippedTrail,
                    colorblind = settings.colorblind,
                    reducedMotion = settings.reducedMotion,
                )
            }
            if (hudVisible) {
                InGameHud(engine, settings, bestForMode, timerSeconds, slowSeconds, doubleSeconds, magnetSeconds,
                    phaseSeconds, freezeSeconds,
                    onPause = { engine.pause(); sound.pauseMusic(); haptic(HapticFeedbackType.TextHandleMove) },
                    onToggleMute = { viewModel.updateSettings { it.muted = !it.muted }; sound.play("ui") },
                    onHome = { goHub() },
                )
            }
            if (!confirmExit) {
                GameOverlays(
                    engine = engine,
                    reward = reward,
                    best = bestForMode,
                    onResume = { engine.resume(); sound.resumeMusic() },
                    onRestart = { startRun() },
                    onHome = { goHub() },
                    onPickPerk = { index ->
                        engine.choosePerk(index)
                        sound.play("powerup")
                        sound.resumeMusic()
                        haptic(HapticFeedbackType.LongPress)
                    },
                )
            }
            if (confirmExit) {
                ModalCard {
                    Title("ВЫХОД ИЗ ЗАБЕГА", Amber)
                    Body("Прервать забег и сохранить результат?")
                    Spacer(Modifier.height(20.dp))
                    WideButton("ПРОДОЛЖИТЬ", { confirmExit = false })
                    Spacer(Modifier.height(12.dp))
                    SecondaryButton("В ХАБ", {
                        engine.abandonRun()
                        finishGoHub()
                    }, accent = Amber)
                }
            }
        } else {
            Canvas(modifier = Modifier.fillMaxSize()) {
                drawMenuBackground(art, save.equippedPlanet * 2, frame, settings.colorblind, settings.reducedMotion)
            }
            when (screen) {
                Screen.Hub -> HubScreen(viewModel, engine, art, frame, ::startRun, ::openScreen)
                Screen.League -> LeagueScreen(viewModel, ::goHub, ::openScreen)
                Screen.Missions -> MissionsScreen(viewModel, ::goHub)
                Screen.Hangar -> HangarScreen(viewModel, art, ::goHub)
                Screen.Shop -> ShopScreen(viewModel, ::goHub)
                Screen.Achievements -> AchievementsScreen(viewModel, ::goHub)
                Screen.Stats -> StatsScreen(viewModel, ::goHub)
                Screen.Settings -> SettingsScreen(viewModel, art, frame, confirmReset, { confirmReset = it }, ::goHub)
                else -> Unit
            }
        }

        AnimatedVisibility(
            visible = toast != null,
            enter = fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.align(Alignment.BottomCenter).padding(16.dp),
        ) {
            if (toast != null) {
                Surface(color = Color(0xF2111526), shape = RoundedCornerShape(18.dp)) {
                    Text(
                        text = toast!!,
                        modifier = Modifier.padding(horizontal = 18.dp, vertical = 12.dp),
                        color = Amber,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold,
                        textAlign = TextAlign.Center,
                    )
                }
            }
        }
    }
    }
}

// ------------------------------------------------------------------ HUD ---

@Composable
private fun InGameHud(
    engine: GameEngine,
    settings: com.example.orbit.data.GameSettings,
    best: Int,
    timer: Int,
    slow: Int,
    double: Int,
    magnet: Int,
    phase: Int,
    freeze: Int,
    onPause: () -> Unit,
    onToggleMute: () -> Unit,
    onHome: () -> Unit,
) {
    Box(modifier = Modifier.fillMaxSize().statusBarsPadding().padding(16.dp)) {
    Column(modifier = Modifier.fillMaxSize()) {
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top,
            horizontalArrangement = Arrangement.SpaceBetween) {
            StatBlock("СЧЁТ", "${engine.score}", if (engine.multiplier > 1) "x${engine.multiplier}" else null,
                Neon, valueSize = 52.sp, highlight = true)
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                RoundButton(if (settings.muted) "×" else "♪", onToggleMute)
                RoundButton("II", onPause)
                RoundButton("⌂", onHome)
            }
            val record = if (engine.status == GameStatus.Playing) maxOf(best, engine.best) else best
            StatBlock("РЕКОРД", "$record", null, Amber, alignEnd = true, valueSize = 16.sp)
        }

        if (engine.timeLeft > 0f || engine.combo > 0) {
            Spacer(Modifier.height(10.dp))
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
                if (engine.timeLeft > 0f) {
                    TimerChip(timer)
                    Spacer(Modifier.width(6.dp))
                }
                if (engine.combo > 0) EffectChip("КОМБО ${engine.combo}", Neon)
            }
        }

        val objective = engine.objective()
        val reached = min(engine.objectiveValue(), objective.target)
        Spacer(Modifier.height(6.dp))
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
            EffectChip(
                "${objective.title} $reached/${objective.target}",
                if (engine.objectiveDone()) Palette.Shield else Amber,
            )
        }

        Spacer(Modifier.height(6.dp))
        if (engine.mode == Mode.Stack) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
                EffectChip("ЛИНИИ ${engine.blocks.lines}", Neon)
                Spacer(Modifier.width(5.dp))
                EffectChip("УРОВЕНЬ ${engine.level}", Amber)
            }
        } else {
            Bar(engine.resonance.coerceIn(0f, 1f), Neon, "РЕЗОНАНС ${(engine.resonance * 100f).toInt()}%")
        }

        val chips = mutableListOf<Pair<String, Color>>()
        if (engine.shield) chips += "ЩИТ" to Palette.Shield
        if (slow > 0) chips += "ЗАМЕДЛЕНИЕ ${slow}с" to Palette.Slow
        if (double > 0) chips += "x2 ${double}с" to Palette.Double
        if (magnet > 0) chips += "МАГНИТ ${magnet}с" to Palette.Magnet
        if (phase > 0) chips += "ФАЗА ${phase}с" to Palette.Phase
        if (freeze > 0) chips += "ЗАМОРОЗКА ${freeze}с" to Palette.Freeze
        if (chips.isNotEmpty()) {
            Spacer(Modifier.height(10.dp))
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
                chips.forEach { (label, color) -> EffectChip(label, color); Spacer(Modifier.width(5.dp)) }
            }
        }
        if (engine.perkIds.isNotEmpty()) {
            Spacer(Modifier.height(6.dp))
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
                engine.perkIds.forEach { index ->
                    Content.perks.getOrNull(index)?.let { EffectChip("${it.glyph} ${it.title}", Violet) }
                    Spacer(Modifier.width(5.dp))
                }
            }
        }

        Spacer(Modifier.weight(1f))

        if (engine.stormWarning > 0f) {
            Banner("МЕТЕОРНЫЙ ДОЖДЬ", Pink)
        } else if (engine.flareWarning > 0f) {
            Banner("СОЛНЕЧНАЯ ВСПЫШКА", Amber)
        } else if (engine.resonanceFlash > 0f) {
            Banner("РЕЗОНАНС!", Neon, engine.resonanceFlash)
        } else if (engine.levelUpFlash > 0f) {
            Banner("УРОВЕНЬ ${engine.level}", Amber, engine.levelUpFlash)
        }
        Spacer(Modifier.height(if (engine.mode == Mode.Stack) 88.dp else 48.dp))
    }
    if (engine.mode == Mode.Stack) {
        StackControls(Modifier.align(Alignment.BottomCenter), engine)
    }
    }
}

@Composable
private fun StatBlock(
    label: String, value: String, extra: String?, color: Color,
    alignEnd: Boolean = false, valueSize: androidx.compose.ui.unit.TextUnit = 30.sp,
    highlight: Boolean = false,
) {
    Column(horizontalAlignment = if (alignEnd) Alignment.End else Alignment.Start) {
        Text(label, color = Paper.copy(alpha = 0.75f), fontSize = 10.sp, letterSpacing = 3.sp,
            maxLines = 1, softWrap = false)
        Text(value, color = if (highlight) color else Paper, fontSize = valueSize,
            fontWeight = FontWeight.Black, maxLines = 1, softWrap = false)
        if (extra != null) Text(extra, color = color, fontSize = 12.sp, fontWeight = FontWeight.Bold,
            maxLines = 1, softWrap = false)
    }
}

@Composable
private fun StackControls(modifier: Modifier, engine: GameEngine) {
    Surface(
        color = Ink.copy(alpha = 0.88f), shape = RoundedCornerShape(22.dp),
        border = BorderStroke(2.dp, Neon), modifier = modifier,
    ) {
        Row(modifier = Modifier.padding(6.dp), horizontalArrangement = Arrangement.spacedBy(5.dp)) {
            ControlKey("◀") { engine.blockMove(-1) }
            ControlKey("⟳") { engine.blockRotate() }
            ControlKey("▶") { engine.blockMove(1) }
            ControlKey("▼") { engine.blockSoftDrop() }
            ControlKey("⇣", accent = Amber) { engine.blockHardDrop() }
            ControlKey("HOLD", accent = Violet, wide = true) { engine.blockHold() }
        }
    }
}

@Composable
private fun ControlKey(label: String, accent: Color = Paper, wide: Boolean = false, onClick: () -> Unit) {
    Box(
        modifier = Modifier.size(width = if (wide) 58.dp else 44.dp, height = 44.dp)
            .clip(RoundedCornerShape(13.dp)).background(accent.copy(alpha = 0.18f))
            .border(1.5.dp, accent, RoundedCornerShape(13.dp)).clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Text(label, color = accent, fontSize = if (wide) 10.sp else 16.sp, fontWeight = FontWeight.Black,
            maxLines = 1, softWrap = false)
    }
}

@Composable
private fun Banner(text: String, color: Color, alpha: Float = 1f) {
    Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
        Text(text, color = color.copy(alpha = alpha), fontSize = 13.sp, fontWeight = FontWeight.Bold, letterSpacing = 4.sp)
    }
}

@Composable
private fun RoundButton(label: String, onClick: () -> Unit) {
    Box(
        modifier = Modifier.size(38.dp).clip(CircleShape)
            .background(Paper)
            .border(2.dp, Ink, CircleShape)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Text(label, color = Ink, fontSize = 15.sp, fontWeight = FontWeight.Black)
    }
}

@Composable
private fun EffectChip(label: String, color: Color) {
    Surface(color = color, shape = RoundedCornerShape(50), border = BorderStroke(1.5.dp, Ink)) {
        Text(label, modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp), color = Ink,
            fontSize = 10.sp, fontWeight = FontWeight.Black, letterSpacing = 0.5.sp,
            maxLines = 1, softWrap = false)
    }
}

/** Таймер фиксированной ширины: табular-цифры не дёргают чип при отсчёте. */
@Composable
private fun TimerChip(seconds: Int) {
    Surface(color = Amber, shape = RoundedCornerShape(50), border = BorderStroke(1.5.dp, Ink)) {
        Text(
            "$seconds" + "с",
            modifier = Modifier.widthIn(min = 56.dp).padding(horizontal = 10.dp, vertical = 5.dp),
            color = Ink, fontSize = 10.sp, fontWeight = FontWeight.Black, letterSpacing = 0.5.sp,
            textAlign = TextAlign.Center,
            maxLines = 1, softWrap = false,
        )
    }
}

@Composable
private fun GameOverlays(
    engine: GameEngine,
    reward: RewardSummary?,
    best: Int,
    onResume: () -> Unit,
    onRestart: () -> Unit,
    onHome: () -> Unit,
    onPickPerk: (Int) -> Unit,
) {
    when {
        engine.draft != null -> DraftOverlay(engine, onPickPerk)
        engine.paused -> ModalCard {
            Title("ПАУЗА", Neon)
            Body("Полёт приостановлен")
            Spacer(Modifier.height(20.dp))
            WideButton("ПРОДОЛЖИТЬ", onResume)
            Spacer(Modifier.height(12.dp))
            SecondaryButton("ЗАНОВО", onRestart, accent = Violet)
            Spacer(Modifier.height(12.dp))
            SecondaryButton("В ХАБ", onHome, accent = MutedText)
        }

        engine.status == GameStatus.GameOver -> ModalCard {
            Title(if (engine.completed) "МАРШРУТ ЗАВЕРШЁН" else "ПОТЕРЯН СИГНАЛ", if (engine.completed) Neon else Pink)
            Text("${engine.score}", color = Ink, fontSize = 56.sp, fontWeight = FontWeight.Black,
                maxLines = 1, softWrap = false)
            if (engine.isNewRecord) Text("НОВЫЙ РЕКОРД", color = Amber, fontSize = 12.sp, letterSpacing = 3.sp)
            else Text("Рекорд: $best", color = MutedText, fontSize = 12.sp)
            Spacer(Modifier.height(10.dp))
            val objective = engine.objective()
            val reached = min(engine.objectiveValue(), objective.target)
            StatRow(
                if (engine.objectiveDone()) "ЦЕЛЬ ВЫПОЛНЕНА: ${objective.title} $reached/${objective.target} · +120 ◈"
                else "ЦЕЛЬ: ${objective.title} $reached/${objective.target}",
                if (engine.objectiveDone()) Palette.Shield else Amber,
            )
            if (engine.perkIds.isNotEmpty()) {
                StatRow("Мутации: " + engine.perkIds.joinToString(" · ") {
                    Content.perks.getOrNull(it)?.let { perk -> "${perk.glyph} ${perk.title}" } ?: "—"
                }, Violet)
            }
            StatRow(
                "Уровень ${engine.level} · " +
                    (if (engine.mode == Mode.Stack)
                        "Линии ${engine.orbsCollected} · Комбо x${engine.maxCombo} · Строк ${engine.blocks.lines}"
                    else
                        "Сферы ${engine.orbsCollected} · Комбо x${engine.maxCombo} · " +
                            "Уклонения ${engine.stats.dodges} · Боссы ${engine.stats.bossKills}"),
            )
            if (reward != null) {
                Spacer(Modifier.height(12.dp))
                Panel {
                    Text("НАГРАДА", color = Neon, fontSize = 10.sp, letterSpacing = 3.sp)
                    Spacer(Modifier.height(6.dp))
                    rewardLines(reward).forEachIndexed { index, line ->
                        StatRow(line, if (index == 0) Neon else MutedText)
                    }
                }
            }
            Spacer(Modifier.height(18.dp))
            WideButton("ЕЩЁ РАЗ", onRestart)
            Spacer(Modifier.height(12.dp))
            SecondaryButton("В ХАБ", onHome, accent = MutedText)
        }
    }
}

/** Рогалик-выбор: три мутации с ценой. Полёт стоит до выбора. */
@Composable
private fun DraftOverlay(engine: GameEngine, onPick: (Int) -> Unit) {
    val draft = engine.draft ?: return
    ModalCard {
        Title("ВЫБЕРИ СВОЙ РИСК", Ink)
        Body("Раунд ${draft.round} · преимущество всегда имеет цену")
        Spacer(Modifier.height(14.dp))
        draft.options.forEachIndexed { index, perkIndex ->
            val perk = Content.perks.getOrNull(perkIndex) ?: return@forEachIndexed
            Surface(
                color = Paper,
                shape = RoundedCornerShape(18.dp),
                border = BorderStroke(2.dp, Ink),
                modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(18.dp)).clickable { onPick(index) },
            ) {
                Row(modifier = Modifier.padding(13.dp), verticalAlignment = Alignment.CenterVertically) {
                    Surface(color = Amber, shape = RoundedCornerShape(12.dp),
                        border = BorderStroke(1.5.dp, Ink), modifier = Modifier.size(42.dp)) {
                        Box(contentAlignment = Alignment.Center) {
                            Text(perk.glyph, color = Ink, fontSize = 24.sp, fontWeight = FontWeight.Black)
                        }
                    }
                    Spacer(Modifier.width(11.dp))
                    Column(Modifier.weight(1f)) {
                        Text(perk.title, color = Ink, fontSize = 14.sp, fontWeight = FontWeight.Black,
                            letterSpacing = 0.5.sp)
                        Spacer(Modifier.height(2.dp))
                        Text(perk.detail, color = Ink, fontSize = 11.sp)
                        Text("ЦЕНА: ${perk.risk}", color = Pink, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                    }
                    Text("▸", color = Ink, fontSize = 20.sp, fontWeight = FontWeight.Black)
                }
            }
            Spacer(Modifier.height(9.dp))
        }
        Spacer(Modifier.height(2.dp))
        Body("Следующий выбор — примерно через 18 секунд полёта.", MutedText)
    }
}

// ------------------------------------------------------------------ HUB ---

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun HubScreen(
    viewModel: GameViewModel,
    engine: GameEngine,
    art: GameArt,
    frame: Long,
    onStart: () -> Unit,
    onOpen: (Screen) -> Unit,
) {
    val save = viewModel.store.data
    val settings = save.settings
    val mode = Mode.from(save.selectedMode)
    val sector = Content.sector(save.selectedSector)
    val missions = Content.missionsFor(save.dailyDate)
    val seasonMod = Content.seasonModifier(save.seasonId)
    val shipTint = shipColor(save.equippedShip, settings.colorblind)
    val briefing = Content.objective(mode, sector.id)
    val nextTier = Content.seasonTiers.firstOrNull { save.seasonXp < it.xp }
    val best = save.bestByMode[mode.id] ?: 0
    val top = save.topByMode[mode.id].orEmpty()

    Column(
        modifier = Modifier.fillMaxSize().statusBarsPadding().imePadding().navigationBarsPadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 14.dp),
    ) {
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text("ORBITA", color = Ink, fontSize = 42.sp, fontWeight = FontWeight.Black,
                letterSpacing = (-3).sp, maxLines = 1, softWrap = false)
            Spacer(Modifier.width(5.dp))
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Surface(color = Pink, shape = RoundedCornerShape(8.dp), border = BorderStroke(1.5.dp, Ink)) {
                    Text("3.8", color = Paper, fontSize = 10.sp, fontWeight = FontWeight.Black,
                        modifier = Modifier.padding(horizontal = 7.dp, vertical = 3.dp))
                }
                Text(mode.short, color = Ink, fontSize = 8.sp, fontWeight = FontWeight.Black,
                    maxLines = 1, softWrap = false)
            }
            Spacer(Modifier.weight(1f))
            Surface(color = Amber, shape = RoundedCornerShape(50), border = BorderStroke(1.5.dp, Ink)) {
                Text("◆ ${compactCurrency(save.credits)}", color = Ink, fontSize = 12.sp,
                    fontWeight = FontWeight.Black, modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
                    maxLines = 1, softWrap = false)
            }
            Spacer(Modifier.width(5.dp))
            Surface(color = Ink, shape = RoundedCornerShape(50)) {
                Text("LV ${save.playerLevel}", color = Neon, fontSize = 11.sp, fontWeight = FontWeight.Black,
                    modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp), maxLines = 1, softWrap = false)
            }
        }

        Spacer(Modifier.height(10.dp))
        Box(
            modifier = Modifier.fillMaxWidth().height(252.dp).clip(RoundedCornerShape(26.dp))
                .background(Ink).border(3.dp, Neon, RoundedCornerShape(26.dp)),
        ) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                drawHubHero(
                    art = art,
                    planetIndex = save.equippedPlanet,
                    shipSprite = save.equippedShip,
                    skinColor = shipTint,
                    tick = frame,
                    colorblind = settings.colorblind,
                    reducedMotion = settings.reducedMotion,
                )
            }
            Text(
                "ORBITA", color = Neon.copy(alpha = 0.13f), fontSize = 82.sp,
                fontWeight = FontWeight.Black, letterSpacing = (-6).sp,
                maxLines = 1, softWrap = false, modifier = Modifier.align(Alignment.TopCenter).padding(top = 6.dp),
            )
            Column(modifier = Modifier.align(Alignment.TopStart).padding(14.dp)) {
                Text(mode.title, color = Paper, fontSize = 19.sp, fontWeight = FontWeight.Black,
                    maxLines = 1, softWrap = false)
                Text(seasonMod.first, color = Neon, fontSize = 10.sp, fontWeight = FontWeight.Bold)
                Text(sector.name, color = Paper.copy(alpha = 0.65f), fontSize = 9.sp, maxLines = 1)
            }
            Surface(color = Pink, shape = RoundedCornerShape(0.dp, 0.dp, 0.dp, 14.dp),
                modifier = Modifier.align(Alignment.TopEnd)) {
                Text("СЕЗОН ${save.seasonId}", color = Paper, fontSize = 10.sp, fontWeight = FontWeight.Black,
                    letterSpacing = 1.sp, modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp))
            }
            Column(modifier = Modifier.align(Alignment.BottomStart).padding(14.dp)) {
                Text("ЦЕЛЬ", color = Paper.copy(alpha = 0.7f), fontSize = 9.sp,
                    fontWeight = FontWeight.Bold, letterSpacing = 2.sp)
                Text(briefing.title, color = Paper, fontSize = 12.sp, fontWeight = FontWeight.Black)
                Text(briefing.target.toString(), color = Neon, fontSize = 54.sp, fontWeight = FontWeight.Black,
                    lineHeight = 54.sp, maxLines = 1, softWrap = false)
                Text("+120 ◈ за выполнение", color = Amber, fontSize = 10.sp, fontWeight = FontWeight.Bold)
            }
            Column(modifier = Modifier.align(Alignment.BottomEnd).padding(14.dp),
                horizontalAlignment = Alignment.End) {
                Text("ТОП ${mode.short}", color = Paper.copy(alpha = 0.65f), fontSize = 9.sp,
                    fontWeight = FontWeight.Bold, letterSpacing = 1.sp)
                if (top.isEmpty()) {
                    Text("—", color = Paper, fontSize = 20.sp, fontWeight = FontWeight.Black)
                } else {
                    top.take(3).forEachIndexed { index, value ->
                        Text("${index + 1}. $value", color = if (index == 0) Amber else Paper,
                            fontSize = 16.sp, fontWeight = FontWeight.Black, maxLines = 1)
                    }
                }
                if (save.dailyStreak > 0) Text(
                    "🔥 ${save.dailyStreak} ${daysWord(save.dailyStreak)}",
                    color = Pink, fontSize = 10.sp, fontWeight = FontWeight.Bold,
                )
            }
        }

        Spacer(Modifier.height(14.dp))
        Text("КОНТРАКТЫ ДНЯ", color = Ink, fontSize = 13.sp, fontWeight = FontWeight.Black,
            letterSpacing = 1.sp)
        Spacer(Modifier.height(7.dp))
        Row(modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
            missions.forEachIndexed { index, mission ->
                val value = min(save.dailyProgress.getOrElse(index) { 0 }, mission.goal)
                val done = value >= mission.goal
                Surface(
                    color = if (done) Neon else Paper,
                    shape = RoundedCornerShape(16.dp),
                    border = BorderStroke(2.dp, Ink),
                    modifier = Modifier.width(158.dp).height(96.dp).clickable { onOpen(Screen.Missions) },
                ) {
                    Column(modifier = Modifier.padding(11.dp)) {
                        Text("КОНТРАКТ ${index + 1}", color = Ink.copy(alpha = 0.6f), fontSize = 9.sp,
                            fontWeight = FontWeight.Bold, letterSpacing = 1.sp)
                        Text(mission.title, color = Ink, fontSize = 12.sp, fontWeight = FontWeight.Black,
                            maxLines = 1, softWrap = false)
                        Spacer(Modifier.height(8.dp))
                        Box(modifier = Modifier.fillMaxWidth().height(7.dp).clip(RoundedCornerShape(4.dp))
                            .background(Ink.copy(alpha = 0.12f))) {
                            Box(modifier = Modifier.fillMaxWidth(value.toFloat() / mission.goal.coerceAtLeast(1))
                                .height(7.dp).clip(RoundedCornerShape(4.dp))
                                .background(if (done) Ink else Pink))
                        }
                        Spacer(Modifier.height(7.dp))
                        Text("$value / ${mission.goal}   +${mission.reward} ◈", color = Ink,
                            fontSize = 10.sp, fontWeight = FontWeight.Bold, maxLines = 1, softWrap = false)
                    }
                }
                Spacer(Modifier.width(8.dp))
            }
        }

        Spacer(Modifier.height(12.dp))
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Surface(color = Amber, shape = RoundedCornerShape(14.dp),
                border = BorderStroke(1.5.dp, Ink), modifier = Modifier.weight(1f)) {
                Text("+120 ◈\nЦЕЛЬ", color = Ink, fontSize = 10.sp, fontWeight = FontWeight.Black,
                    textAlign = TextAlign.Center, modifier = Modifier.padding(vertical = 8.dp), maxLines = 2)
            }
            Surface(color = Neon, shape = RoundedCornerShape(14.dp),
                border = BorderStroke(1.5.dp, Ink), modifier = Modifier.weight(1.4f)) {
                Text(
                    if (nextTier != null) "СЕЗОН ${save.seasonXp}/${nextTier.xp}"
                    else "СЕЗОН МАКС",
                    color = Ink, fontSize = 10.sp, fontWeight = FontWeight.Black,
                    textAlign = TextAlign.Center, modifier = Modifier.padding(vertical = 8.dp), maxLines = 1,
                )
            }
            Surface(color = Paper, shape = RoundedCornerShape(14.dp),
                border = BorderStroke(1.5.dp, Ink), modifier = Modifier.weight(1f)) {
                Text("ЖЕТОНЫ\n${compactCurrency(save.credits)} ◈", color = Ink, fontSize = 10.sp,
                    fontWeight = FontWeight.Black, textAlign = TextAlign.Center,
                    modifier = Modifier.padding(vertical = 8.dp), maxLines = 2)
            }
        }

        Spacer(Modifier.height(12.dp))
        Text("ВЫЛЕТ", color = Ink, fontSize = 12.sp, fontWeight = FontWeight.Black, letterSpacing = 1.sp)
        Spacer(Modifier.height(6.dp))
        Row(modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
            Content.modes.forEach { m ->
                ChoiceChip(m.short, m.id == mode.id) { viewModel.selectMode(m.id) }
                Spacer(Modifier.width(6.dp))
            }
        }
        Spacer(Modifier.height(6.dp))
        Body("${mode.detail}", Ink)
        Spacer(Modifier.height(8.dp))
        Row(modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
            Content.sectors.forEach { s ->
                val unlocked = save.playerLevel >= s.unlockLevel || save.visitedSectors.contains(s.id)
                ChoiceChip(if (unlocked) s.name else "🔒 ${s.name}", s.id == sector.id, locked = !unlocked) {
                    viewModel.selectSector(s.id)
                }
                Spacer(Modifier.width(6.dp))
            }
        }
        Spacer(Modifier.height(5.dp))
        Body("${sector.subtitle} · Рекорд $best · Топ: ${top.take(3).joinToString(" / ").ifEmpty { "—" }}", Ink)

        Spacer(Modifier.height(12.dp))
        Button(
            onClick = onStart,
            modifier = Modifier.fillMaxWidth().height(64.dp),
            shape = RoundedCornerShape(20.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Neon, contentColor = Ink),
        ) {
            Text("ИГРАТЬ  ▸", color = Ink, fontSize = 23.sp, fontWeight = FontWeight.Black,
                letterSpacing = 1.sp)
        }

        Spacer(Modifier.height(14.dp))
        val docks = listOf(
            "★" to ("ЛИГА" to Screen.League),
            "◇" to ("ГАРАЖ" to Screen.Hangar),
            "◈" to ("МАГАЗИН" to Screen.Shop),
            "✓" to ("ДОСТИЖ." to Screen.Achievements),
            "▤" to ("РЕКОРДЫ" to Screen.Stats),
            "⚙" to ("НАСТР." to Screen.Settings),
        )
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            docks.forEach { (icon, entry) ->
                DockButton(icon, entry.first, Modifier.weight(1f)) { onOpen(entry.second) }
            }
        }
        Spacer(Modifier.height(16.dp))
    }
}

// -------------------------------------------------------------- SCREENS ---

@Composable
private fun LeagueScreen(
    viewModel: GameViewModel,
    onBack: () -> Unit,
    onOpen: (Screen) -> Unit,
) {
    val save = viewModel.store.data
    val mode = Mode.from(save.selectedMode)
    val objective = Content.objective(mode, save.selectedSector)
    val defs = Content.missionsFor(save.dailyDate)
    val missionsDone = defs.indices.count { i -> save.dailyProgress.getOrElse(i) { 0 } >= defs[i].goal }
    val nextTier = Content.seasonTiers.firstOrNull { save.seasonXp < it.xp }
    val shipsOwned = Content.shop.count { it.category == "ship" && save.inventory.contains(it.id) }
    val planetsOwned = Content.shop.count { it.category == "planet" && save.inventory.contains(it.id) }
    val trophies = (if (save.achievements.contains("boss_1")) 1 else 0) +
        (if (save.achievements.contains("boss_5")) 1 else 0)

    ScrollScreen("ЛИГА", viewModel, onBack,
        "Здесь собрано всё, ради чего стоит возвращаться в орбиту.") {
        Panel(color = Ink) {
            Text("ГЛАВНАЯ ЦЕЛЬ ВЫЛЕТА", color = Neon, fontSize = 10.sp, fontWeight = FontWeight.Black,
                letterSpacing = 2.sp)
            Spacer(Modifier.height(5.dp))
            Text("${objective.title}: ${objective.target}", color = Paper, fontSize = 22.sp,
                fontWeight = FontWeight.Black)
            Spacer(Modifier.height(3.dp))
            Body("Выполнение даёт +200 очков и +120 жетонов.", Paper.copy(alpha = 0.85f))
        }

        Spacer(Modifier.height(16.dp))
        SectionTitle("ЧЕТЫРЕ ПРИЧИНЫ ВОЗВРАЩАТЬСЯ")
        val reasons = listOf(
            "01" to ("СЕЗОННАЯ ГОНКА" to if (nextTier != null)
                "${save.seasonXp} / ${nextTier.xp} XP · награда +${nextTier.reward} ◈"
            else "Все уровни сезона пройдены"),
            "02" to ("КОНТРАКТЫ ДНЯ" to "$missionsDone / 3 выполнено · обновляются в 00:00 UTC"),
            "03" to ("КОЛЛЕКЦИЯ" to "$shipsOwned / 8 кораблей · $planetsOwned / 8 планет открыто"),
            "04" to ("ОХОТА НА БОССОВ" to if (trophies == 0) "Первое ядро ещё ждёт" else "$trophies трофея в копилке"),
        )
        reasons.forEach { (num, entry) ->
            Panel {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Surface(color = if (num == "01") Neon else Amber, shape = RoundedCornerShape(10.dp),
                        border = BorderStroke(1.5.dp, Ink)) {
                        Text(num, color = Ink, fontSize = 13.sp, fontWeight = FontWeight.Black,
                            modifier = Modifier.padding(horizontal = 8.dp, vertical = 5.dp))
                    }
                    Spacer(Modifier.width(10.dp))
                    Column {
                        Text(entry.first, color = Ink, fontSize = 13.sp, fontWeight = FontWeight.Black)
                        Spacer(Modifier.height(2.dp))
                        Text(entry.second, color = MutedText, fontSize = 11.sp)
                    }
                }
            }
            Spacer(Modifier.height(8.dp))
        }

        Spacer(Modifier.height(8.dp))
        SectionTitle("МУТАЦИИ: ВЫБОР С ЦЕНОЙ")
        Content.perks.forEach { perk ->
            Panel {
                Text("${perk.glyph}  ${perk.title}", color = Ink, fontSize = 13.sp,
                    fontWeight = FontWeight.Black)
                Spacer(Modifier.height(2.dp))
                Text(perk.detail, color = Ink, fontSize = 11.sp)
                Text("ЦЕНА: ${perk.risk}", color = Pink, fontSize = 10.sp, fontWeight = FontWeight.Bold)
            }
            Spacer(Modifier.height(7.dp))
        }
        Spacer(Modifier.height(6.dp))
        WideButton("ВСЕ ДОСТИЖЕНИЯ  ▸", { onOpen(Screen.Achievements) }, accent = Amber)
    }
}

@Composable
private fun MissionsScreen(viewModel: GameViewModel, onBack: () -> Unit) {
    val save = viewModel.store.data
    val defs = Content.missionsFor(save.dailyDate)
    ScrollScreen("ЗАДАНИЯ ДНЯ", viewModel, onBack, "Задания обновляются каждый день в 00:00 UTC.") {
        defs.forEachIndexed { index, m ->
            val value = save.dailyProgress.getOrElse(index) { 0 }
            val done = value >= m.goal
            Panel {
                Text(m.title, color = if (done) Neon else Ink, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(4.dp))
                Bar((value.toFloat() / m.goal.coerceAtLeast(1)).coerceIn(0f, 1f), if (done) Neon else Violet, "$value / ${m.goal}")
                Spacer(Modifier.height(8.dp))
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text("+${m.reward} жетонов", color = Amber, fontSize = 12.sp)
                    if (save.dailyClaimed.getOrElse(index) { false }) {
                        Text("ПОЛУЧЕНО", color = MutedText, fontSize = 11.sp)
                    } else {
                        SmallButton("ЗАБРАТЬ", done) { viewModel.claimMission(index) }
                    }
                }
            }
            Spacer(Modifier.height(8.dp))
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("СЕЗОН ${save.seasonId}")
        Content.seasonTiers.forEachIndexed { i, tier ->
            val claimed = save.seasonClaimed.getOrElse(i) { false }
            val unlocked = save.seasonXp >= tier.xp
            Panel {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text("Уровень ${i + 1} · ${tier.xp} XP", color = Ink, fontSize = 13.sp)
                    Text("+${tier.reward} ◈", color = Amber, fontSize = 13.sp)
                }
                Spacer(Modifier.height(4.dp))
                Text(if (claimed) "ПОЛУЧЕНО" else if (unlocked) "ДОСТУПНО — награда начисляется после забега" else "Ещё ${tier.xp - save.seasonXp} XP",
                    color = if (unlocked) Neon else MutedText, fontSize = 11.sp)
            }
            Spacer(Modifier.height(6.dp))
        }
        Spacer(Modifier.height(12.dp))
        Body("Серия заданий: ${save.dailyStreak} ${daysWord(save.dailyStreak)} · Сыграно дзен-забегов: ${save.zenRuns}")
    }
}

@Composable
private fun HangarScreen(viewModel: GameViewModel, art: GameArt, onBack: () -> Unit) {
    val save = viewModel.store.data
    val context = LocalContext.current
    ScrollScreen("АНГАР", viewModel, onBack, "Все корабли, шлейфы и планеты доступны только локально.") {
        Box(modifier = Modifier.fillMaxWidth().height(170.dp)) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                drawPreviewPlanet(art, save.equippedPlanet, Offset(size.width / 2, size.height / 2),
                    78.dp.toPx(), 0L, save.settings.colorblind, save.settings.reducedMotion)
            }
            Image(
                painter = painterResource(context.resources.getIdentifier("ship_${save.equippedShip}", "drawable", context.packageName)),
                contentDescription = null,
                modifier = Modifier.align(Alignment.Center).size(96.dp),
            )
        }
        Spacer(Modifier.height(10.dp))

        SectionTitle("КОРПУСА")
        val ships = Content.shop.filter { it.category == "ship" }
        ships.chunked(2).forEach { chunk ->
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                chunk.forEach { item ->
                    val owned = save.inventory.contains(item.id)
                    val equipped = save.equippedShip == item.value
                    ShopTile(item.title, item.detail, item.price, owned, equipped, Modifier.weight(1f),
                        onBuy = { viewModel.buy(item.id) }, onEquip = { viewModel.equip(item.id) })
                }
                if (chunk.size == 1) Spacer(Modifier.weight(1f))
            }
            Spacer(Modifier.height(8.dp))
        }

        SectionTitle("ШЛЕЙФЫ")
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Content.shop.filter { it.category == "trail" }.forEach { item ->
                val owned = save.inventory.contains(item.id)
                ChoiceChip(if (owned) item.title else "🔒 ${item.title}", save.equippedTrail == item.value, Modifier.weight(1f)) {
                    if (owned) viewModel.equip(item.id) else viewModel.buy(item.id)
                }
            }
        }
        Spacer(Modifier.height(12.dp))
        SectionTitle("ПЛАНЕТЫ")
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Content.shop.filter { it.category == "planet" }.forEach { item ->
                val owned = save.inventory.contains(item.id)
                ChoiceChip(if (owned) item.title else "🔒 ${item.title}", save.equippedPlanet == item.value, Modifier.weight(1f)) {
                    if (owned) viewModel.equip(item.id) else viewModel.buy(item.id)
                }
            }
        }
        Spacer(Modifier.height(12.dp))
        SectionTitle("УЛУЧШЕНИЯ")
        Content.shop.filter { it.category == "upgrade" }.forEach { item ->
            val owned = save.inventory.contains(item.id)
            Panel {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Column(Modifier.weight(1f)) {
                        Text(item.title, color = Ink, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                        Body(item.detail)
                    }
                    SmallButton(if (owned) "АКТИВНО" else "${item.price} ◈", owned) {
                        if (!owned) viewModel.buy(item.id)
                    }
                }
            }
            Spacer(Modifier.height(6.dp))
        }
    }
}

@Composable
private fun ShopScreen(viewModel: GameViewModel, onBack: () -> Unit) {
    val save = viewModel.store.data
    ScrollScreen("МАГАЗИН", viewModel, onBack, "Жетоны зарабатываются в забегах, заданиях и по достижениям. Баланс: ◆ ${compactCurrency(save.credits)}") {
        listOf("ship" to "КОРАБЛИ", "trail" to "ШЛЕЙФЫ", "planet" to "ФОНЫ", "upgrade" to "УЛУЧШЕНИЯ").forEach { (cat, title) ->
            SectionTitle(title)
            Content.shop.filter { it.category == cat }.forEach { item ->
                val owned = save.inventory.contains(item.id)
                Panel {
                    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(item.title, color = Ink, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                            Body(item.detail)
                        }
                        Spacer(Modifier.width(8.dp))
                        when {
                            owned && (item.category == "ship" || item.category == "trail" || item.category == "planet") -> {
                                val equipped = when (item.category) {
                                    "ship" -> save.equippedShip == item.value
                                    "trail" -> save.equippedTrail == item.value
                                    else -> save.equippedPlanet == item.value
                                }
                                SmallButton(if (equipped) "НОСИМ" else "НАДЕТЬ", !equipped) { viewModel.equip(item.id) }
                            }
                            owned -> Text("КУПЛЕНО", color = MutedText, fontSize = 10.sp)
                            else -> SmallButton("${item.price} ◈", save.credits >= item.price) { viewModel.buy(item.id) }
                        }
                    }
                }
                Spacer(Modifier.height(6.dp))
            }
            Spacer(Modifier.height(12.dp))
        }
    }
}

@Composable
private fun AchievementsScreen(viewModel: GameViewModel, onBack: () -> Unit) {
    val save = viewModel.store.data
    ScrollScreen("ДОСТИЖЕНИЯ", viewModel, onBack, "Открыто ${save.achievements.size} из ${Content.achievements.size}.") {
        Content.achievements.forEach { a ->
            val open = save.achievements.contains(a.id)
            Panel {
                Row(modifier = Modifier.fillMaxWidth()) {
                    Text(if (open) "★" else "☆", color = if (open) Amber else MutedText, fontSize = 18.sp)
                    Spacer(Modifier.width(10.dp))
                    Column(Modifier.weight(1f)) {
                        Text(a.title, color = if (open) Ink else MutedText, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                        Body(a.detail)
                    }
                    Text("+${a.reward} ◈", color = if (open) Amber else MutedText, fontSize = 11.sp)
                }
            }
            Spacer(Modifier.height(5.dp))
        }
        Spacer(Modifier.height(16.dp))
        SectionTitle("СЕКРЕТЫ")
        Content.secrets.forEach { s ->
            val open = save.secrets.contains(s.id)
            Panel {
                Text(if (open) s.title else "???", color = if (open) Violet else MutedText, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                Body(s.hint)
            }
            Spacer(Modifier.height(5.dp))
        }
    }
}

@Composable
private fun StatsScreen(viewModel: GameViewModel, onBack: () -> Unit) {
    val save = viewModel.store.data
    var modeTab by remember { mutableStateOf(Mode.Classic.id) }
    ScrollScreen("СТАТИСТИКА", viewModel, onBack, "Все рекорды хранятся только на этом устройстве.") {
        val total = save.history.sumOf { it.score }
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Metric("ЗАБЕГОВ", "${save.history.size}", Modifier.weight(1f))
            Metric("ОЧКОВ", "$total", Modifier.weight(1f))
            Metric("СФЕР", "${save.history.sumOf { it.orbs }}", Modifier.weight(1f))
        }
        Spacer(Modifier.height(8.dp))
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Metric("БОССОВ", "${save.history.sumOf { it.bossKills }}", Modifier.weight(1f))
            Metric("ПОРТАЛОВ", "${save.history.sumOf { it.wormholes }}", Modifier.weight(1f))
            Metric("УРОВЕНЬ", "${save.playerLevel}", Modifier.weight(1f))
        }
        Spacer(Modifier.height(18.dp))
        SectionTitle("ТАБЛИЦА РЕКОРДОВ")
        Row(
            modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(end = 20.dp),
        ) {
            Content.modes.forEach { m ->
                ChoiceChip(m.short, modeTab == m.id) {
                    modeTab = m.id
                    viewModel.refresh()
                }
                Spacer(Modifier.width(8.dp))
            }
        }
        Spacer(Modifier.height(8.dp))
        val top = save.topByMode[modeTab].orEmpty()
        Panel {
            if (top.isEmpty()) Body("В этом режиме ещё нет забегов.")
            else top.forEachIndexed { i, score -> Text("${i + 1}.  $score", color = if (i == 0) Amber else Ink, fontSize = 14.sp) }
        }
        Spacer(Modifier.height(8.dp))
        val statsClipboard = LocalClipboardManager.current
        SecondaryButton("СКОПИРОВАТЬ КОД РЕКОРДОВ", {
            statsClipboard.setText(AnnotatedString(viewModel.exportCode()))
        }, accent = Violet)
        Spacer(Modifier.height(16.dp))
        SectionTitle("ИСТОРИЯ ЗАБЕГОВ")
        save.history.take(12).forEach { r ->
            Panel {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text("${Mode.from(r.mode).title} · ${Content.sector(r.sector).name}", color = Ink, fontSize = 12.sp)
                    Text("${r.score}", color = Neon, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                }
                Body("Ур. ${r.level} · Сферы ${r.orbs} · Комбо x${r.maxCombo} · ${r.seconds}с")
            }
            Spacer(Modifier.height(5.dp))
        }
    }
}

@Composable
private fun SettingsScreen(
    viewModel: GameViewModel,
    art: GameArt,
    frame: Long,
    confirmReset: Boolean,
    setConfirmReset: (Boolean) -> Unit,
    onBack: () -> Unit,
) {
    val save = viewModel.store.data
    val clipboard = LocalClipboardManager.current
    var exportText by remember { mutableStateOf("") }
    var importText by remember { mutableStateOf("") }
    var importResult by remember { mutableStateOf("") }
    ScrollScreen("НАСТРОЙКИ", viewModel, onBack, "Прогресс сохраняется автоматически после каждого забега и действия.") {
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Canvas(modifier = Modifier.size(58.dp)) {
                drawPreviewPlanet(art, save.equippedPlanet, Offset(size.width / 2, size.height / 2),
                    28.dp.toPx(), frame, save.settings.colorblind, save.settings.reducedMotion)
            }
            Body("Предпросмотр планеты: так выглядят выбранные цвета и анимация.")
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("ЗВУК")
        Panel {
            Text("Музыка ${(save.settings.music * 100).toInt()}%", color = Ink, fontSize = 12.sp)
            Slider(value = save.settings.music, onValueChange = { viewModel.updateSettings { s -> s.music = it } }, valueRange = 0f..1f)
            Text("Эффекты ${(save.settings.sfx * 100).toInt()}%", color = Ink, fontSize = 12.sp)
            Slider(value = save.settings.sfx, onValueChange = { viewModel.updateSettings { s -> s.sfx = it } }, valueRange = 0f..1f)
            ToggleRow("Полное выключение звука", save.settings.muted) { viewModel.updateSettings { s -> s.muted = it } }
            ToggleRow("Вибрация", save.settings.haptics) { viewModel.updateSettings { s -> s.haptics = it } }
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("ПРОФИЛИ")
        Panel {
            Text("ЗВУК", color = MutedText, fontSize = 9.sp, letterSpacing = 2.sp)
            Spacer(Modifier.height(4.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                listOf("BALANCED" to "БАЛАНС", "NIGHT" to "НОЧЬ", "BASS" to "БАС").forEach { (code, label) ->
                    ChoiceChip(
                        label, save.settings.audioProfile == code, Modifier.weight(1f),
                    ) { viewModel.updateSettings { s -> s.audioProfile = code } }
                }
            }
            Spacer(Modifier.height(10.dp))
            Text("ВИБРАЦИЯ", color = MutedText, fontSize = 9.sp, letterSpacing = 2.sp)
            Spacer(Modifier.height(4.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                listOf("CLASSIC" to "КЛАССИКА", "SOFT" to "МЯГКАЯ", "STRONG" to "СИЛЬНАЯ").forEach { (code, label) ->
                    ChoiceChip(
                        label, save.settings.hapticProfile == code, Modifier.weight(1f),
                    ) { viewModel.updateSettings { s -> s.hapticProfile = code } }
                }
            }
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("ДОСТУПНОСТЬ")
        Panel {
            ToggleRow("Уменьшить анимацию и тряску", save.settings.reducedMotion) { viewModel.updateSettings { s -> s.reducedMotion = it } }
            ToggleRow("Контрастные цвета", save.settings.colorblind) { viewModel.updateSettings { s -> s.colorblind = it } }
            ToggleRow("Крупный текст", save.settings.largeText) { viewModel.updateSettings { s -> s.largeText = it } }
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("СОХРАНЕНИЕ")
        Panel {
            Body("Код экспорта создаёт код с контролем целостности. Импорт заменяет текущий прогресс.")
            Spacer(Modifier.height(8.dp))
            SecondaryButton("ЭКСПОРТИРОВАТЬ КОД", { exportText = viewModel.exportCode() }, accent = Violet)
            if (exportText.isNotEmpty()) {
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(
                    value = exportText,
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("КОД ЭКСПОРТА") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    textStyle = TextStyle(fontFamily = FontFamily.Monospace, fontSize = 12.sp),
                )
                Spacer(Modifier.height(6.dp))
                SmallButton("КОПИРОВАТЬ", true) { clipboard.setText(AnnotatedString(exportText)) }
            }
            Spacer(Modifier.height(14.dp))
            OutlinedTextField(
                value = importText,
                onValueChange = { importText = it },
                label = { Text("КОД ИМПОРТА") },
                placeholder = { Text("ORB1-...") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
                textStyle = TextStyle(fontFamily = FontFamily.Monospace, fontSize = 12.sp),
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.None,
                    autoCorrect = false,
                    keyboardType = KeyboardType.Text,
                ),
            )
            Spacer(Modifier.height(8.dp))
            WideButton("ИМПОРТИРОВАТЬ", {
                importResult = if (viewModel.importCode(importText)) "Прогресс восстановлен" else "Код недействителен"
                importText = ""
            }, accent = Neon)
            if (importResult.isNotEmpty()) {
                Spacer(Modifier.height(6.dp))
                Body(importResult, Amber)
            }
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("ОПАСНАЯ ЗОНА", DangerRed)
        Panel(color = Color(0xFF2A0B16)) {
            Text("⚠  СБРОС ПРОГРЕССА", color = DangerRed, fontSize = 12.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(8.dp))
            if (!confirmReset) {
                Body("Удалить все рекорды, предметы и достижения?", DangerRed)
                Spacer(Modifier.height(10.dp))
                WideButton("СБРОСИТЬ ПРОГРЕСС", { setConfirmReset(true) }, accent = DangerRed)
            } else {
                Body("Точно удалить все рекорды, предметы и достижения?", DangerRed)
                Spacer(Modifier.height(12.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    SecondaryButton("ОТМЕНА", { setConfirmReset(false) }, Modifier.weight(1f), MutedText)
                    WideButton("ДА, СБРОСИТЬ", {
                        viewModel.resetProgress()
                        setConfirmReset(false)
                    }, modifier = Modifier.weight(1f), accent = DangerRed)
                }
            }
        }
        Spacer(Modifier.height(24.dp))
        Body("Орбита 3.8 · Локальная игра без рекламы и сети")
    }
}

// -------------------------------------------------------------- HELPERS ---

@Composable
private fun ScrollScreen(title: String, viewModel: GameViewModel, onBack: () -> Unit, subtitle: String, content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().statusBarsPadding().imePadding().navigationBarsPadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 18.dp),
    ) {
        ScreenHeader(title, viewModel, onBack)
        Spacer(Modifier.height(12.dp))
        Body(subtitle, Paper.copy(alpha = 0.85f))
        Spacer(Modifier.height(16.dp))
        content()
        Spacer(Modifier.height(28.dp))
    }
}

@Composable
private fun ScreenHeader(title: String, viewModel: GameViewModel, onBack: () -> Unit) {
    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(title, color = Neon, fontSize = 22.sp, fontWeight = FontWeight.Black,
                letterSpacing = 1.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
            Text("◆ ${compactCurrency(viewModel.store.data.credits)}", color = Amber, fontSize = 12.sp,
                maxLines = 1, softWrap = false)
        }
        SmallButton("← ХАБ", true, onBack)
    }
}

@Composable
private fun SectionTitle(text: String, color: Color = Neon.copy(alpha = 0.8f)) {
    Text(text, color = color, fontSize = 11.sp, fontWeight = FontWeight.Bold, letterSpacing = 3.sp)
    Spacer(Modifier.height(7.dp))
}

@Composable
private fun Body(text: String, color: Color = MutedText) {
    Text(text, color = color, fontSize = 12.sp, lineHeight = 17.sp)
}

@Composable
private fun Panel(color: Color = PanelColor, content: @Composable ColumnScope.() -> Unit) {
    Surface(
        color = color,
        shape = RoundedCornerShape(18.dp),
        border = if (color == Ink) null else BorderStroke(2.dp, Ink),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(modifier = Modifier.padding(14.dp), content = content)
    }
}

@Composable
private fun ModalCard(content: @Composable ColumnScope.() -> Unit) {
    Box(
        modifier = Modifier.fillMaxSize().background(Color(0xAA0B1020))
            .statusBarsPadding().navigationBarsPadding()
            .pointerInput(Unit) { detectTapGestures { /* модальный экран не пропускает тапы в игру */ } },
        contentAlignment = Alignment.Center,
    ) {
        Surface(
            color = Paper,
            shape = RoundedCornerShape(26.dp),
            border = BorderStroke(3.dp, Ink),
            modifier = Modifier.padding(20.dp),
        ) {
            Column(
                modifier = Modifier.fillMaxWidth(0.94f).heightIn(max = 620.dp).verticalScroll(rememberScrollState())
                    .navigationBarsPadding().padding(22.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                content = content,
            )
        }
    }
}

@Composable
private fun Title(text: String, color: Color) {
    Text(text, color = color, fontSize = 22.sp, fontWeight = FontWeight.Black, letterSpacing = 1.sp,
        textAlign = TextAlign.Center, maxLines = 2, overflow = TextOverflow.Ellipsis)
    Spacer(Modifier.height(12.dp))
}

@Composable
private fun StatRow(text: String, color: Color = MutedText) {
    Text(text, color = color, fontSize = 11.sp, lineHeight = 16.sp)
}

@Composable
private fun WideButton(
    text: String,
    onClick: () -> Unit,
    enabled: Boolean = true,
    accent: Color = Neon,
    modifier: Modifier = Modifier,
) {
    Surface(
        color = if (enabled) accent else accent.copy(alpha = 0.45f),
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(2.dp, Ink),
        modifier = modifier.fillMaxWidth().height(54.dp).clip(RoundedCornerShape(16.dp))
            .clickable(enabled = enabled, onClick = onClick),
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(text, color = Ink, fontWeight = FontWeight.Black, fontSize = 15.sp,
                letterSpacing = 1.sp, maxLines = 1, softWrap = false)
        }
    }
}

/** Второстепенное действие: бумажная кнопка с жёстким контуром. */
@Composable
private fun SecondaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    accent: Color = Violet,
    enabled: Boolean = true,
) {
    Surface(
        color = Paper,
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(2.dp, Ink),
        modifier = modifier.fillMaxWidth().height(50.dp).clip(RoundedCornerShape(16.dp))
            .clickable(enabled = enabled, onClick = onClick),
    ) {
        Box(contentAlignment = Alignment.Center) {
            Text(text, color = Ink, fontWeight = FontWeight.Black, fontSize = 13.sp,
                letterSpacing = 1.sp, maxLines = 1, softWrap = false)
        }
    }
}

@Composable
private fun SmallButton(text: String, enabled: Boolean = true, onClick: () -> Unit) {
    Surface(
        color = if (enabled) Amber else Paper.copy(alpha = 0.6f),
        shape = RoundedCornerShape(10.dp),
        border = BorderStroke(1.5.dp, Ink),
        modifier = Modifier.clickable(enabled = enabled, onClick = onClick),
    ) {
        Text(text, color = Ink, fontSize = 10.sp, fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 7.dp), maxLines = 1, softWrap = false)
    }
}

@Composable
private fun DockButton(icon: String, label: String, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Surface(
        color = Paper,
        shape = RoundedCornerShape(14.dp),
        border = BorderStroke(1.5.dp, Ink),
        modifier = modifier.clickable(onClick = onClick),
    ) {
        Column(
            modifier = Modifier.padding(vertical = 9.dp, horizontal = 2.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(icon, color = Pink, fontSize = 17.sp, fontWeight = FontWeight.Black, maxLines = 1)
            Spacer(Modifier.height(3.dp))
            Text(label, color = Ink, fontSize = 8.sp, fontWeight = FontWeight.Bold,
                maxLines = 1, softWrap = false)
        }
    }
}

@Composable
private fun NavTile(title: String, subtitle: String, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Surface(color = PanelColor, shape = RoundedCornerShape(16.dp), border = BorderStroke(2.dp, Ink),
        modifier = modifier.clickable(onClick = onClick)) {
        Column(modifier = Modifier.padding(14.dp)) {
            Text(title, color = Ink, fontSize = 13.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(3.dp))
            Text(subtitle, color = MutedText, fontSize = 10.sp)
            Spacer(Modifier.height(10.dp))
            Text("→", color = Pink, fontSize = 16.sp, fontWeight = FontWeight.Black)
        }
    }
}

@Composable
private fun ShopTile(
    title: String, detail: String, price: Int, owned: Boolean, equipped: Boolean,
    modifier: Modifier = Modifier, onBuy: () -> Unit, onEquip: () -> Unit,
) {
    Surface(color = PanelColor, shape = RoundedCornerShape(14.dp), border = BorderStroke(1.5.dp, Ink),
        modifier = modifier.clickable { if (!owned) onBuy() else onEquip() }) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(title, color = if (equipped) Ink else Ink, fontSize = 12.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(2.dp))
            Text(detail, color = MutedText, fontSize = 9.sp)
            Spacer(Modifier.height(8.dp))
            Text(if (owned) (if (equipped) "НОСИМ" else "НАДЕТЬ") else "$price ◈",
                color = if (owned) Ink else Pink, fontSize = 11.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun Metric(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(color = PanelColor, shape = RoundedCornerShape(14.dp), border = BorderStroke(1.5.dp, Ink),
        modifier = modifier) {
        Column(modifier = Modifier.padding(12.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(label, color = MutedText, fontSize = 9.sp, letterSpacing = 2.sp)
            Text(value, color = Ink, fontSize = 20.sp, fontWeight = FontWeight.Black, maxLines = 1)
        }
    }
}

@Composable
private fun ChoiceChip(
    label: String,
    selected: Boolean,
    modifier: Modifier = Modifier,
    locked: Boolean = false,
    onClick: () -> Unit,
) {
    Surface(
        color = if (selected) Neon else Paper,
        shape = RoundedCornerShape(10.dp),
        border = BorderStroke(1.5.dp, Ink),
        modifier = modifier.clickable(enabled = !locked, onClick = onClick),
    ) {
        Text(
            label,
            color = if (locked) MutedText else Ink,
            fontSize = 10.sp,
            fontWeight = if (selected) FontWeight.Black else FontWeight.Bold,
            letterSpacing = 0.3.sp,
            modifier = Modifier.padding(horizontal = 11.dp, vertical = 8.dp),
            maxLines = 1, softWrap = false, overflow = TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun Bar(fraction: Float, color: Color, label: String) {
    Column {
        Text(label, color = Ink, fontSize = 9.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.sp)
        Spacer(Modifier.height(5.dp))
        Box(modifier = Modifier.fillMaxWidth().height(8.dp).clip(RoundedCornerShape(4.dp))
            .background(Ink.copy(alpha = 0.12f))) {
            Box(modifier = Modifier.fillMaxWidth(fraction.coerceIn(0f, 1f)).height(8.dp)
                .clip(RoundedCornerShape(4.dp)).background(color))
        }
    }
}

@Composable
private fun ToggleRow(text: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(modifier = Modifier.fillMaxWidth().padding(top = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(text, color = Ink, fontSize = 12.sp, modifier = Modifier.weight(1f))
        Switch(checked = checked, onCheckedChange = onChange)
    }
}

/** Русское склонение дней: 1 ДЕНЬ, 2 ДНЯ, 5 ДНЕЙ. */
private fun daysWord(days: Int, capitalize: Boolean = false): String {
    val n = kotlin.math.abs(days) % 100
    val word = when {
        n in 11..14 -> "дней"
        n % 10 == 1 -> "день"
        n % 10 in 2..4 -> "дня"
        else -> "дней"
    }
    return if (capitalize) word.uppercase() else word
}

/** Компактный вывод валюты: 12300 → 12.3k, без переносов в шапке. */
private fun compactCurrency(value: Int): String {
    val v = value.coerceAtLeast(0)
    return when {
        v >= 1_000_000 -> "${(v / 100_000f).roundToInt() / 10f}M"
        v >= 100_000 -> "${(v / 1000f).roundToInt()}k"
        v >= 1000 -> "${(v / 100f).roundToInt() / 10f}k"
        else -> "$v"
    }
}

/** Единый формат награды: жетоны → XP/уровень → достижения → секреты → задания → сезон. */
private fun rewardLines(reward: RewardSummary): List<String> {
    val lines = mutableListOf<String>()
    if (reward.credits > 0) lines += "+${reward.credits} жетонов"
    val progress = mutableListOf<String>()
    if (reward.xp > 0) progress += "+${reward.xp} XP"
    if (reward.levelUps > 0) progress += "уровень +${reward.levelUps}"
    if (progress.isNotEmpty()) lines += progress.joinToString(", ")
    if (reward.achievements.isNotEmpty()) lines += "Достижения: ${reward.achievements.joinToString(", ")}"
    if (reward.secrets.isNotEmpty()) lines += "Секреты: ${reward.secrets.joinToString(", ")}"
    if (reward.missions.isNotEmpty()) lines += "Задания: ${reward.missions.joinToString(", ")}"
    if (reward.seasonTiers.isNotEmpty()) lines += "Сезон: уровней ${reward.seasonTiers.size}"
    return lines.filter { it.isNotBlank() }
}

private fun shipColor(index: Int, colorblind: Boolean): Color {
    if (colorblind) return Color(0xFFFFD654)
    return when (index.coerceIn(0, 7)) {
        0 -> Color(0xFF22D3EE); 1 -> Color(0xFFFB7185); 2 -> Color(0xFFA3E635); 3 -> Color(0xFFC084FC)
        4 -> Color(0xFFFBB84F); 5 -> Color(0xFFFF5CCD); 6 -> Color(0xFF78DCFF); else -> Color(0xFF58FFCC)
    }
}
