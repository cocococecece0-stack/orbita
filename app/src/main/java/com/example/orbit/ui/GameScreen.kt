package com.example.orbit.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
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
import kotlin.math.ceil
import kotlinx.coroutines.delay

private val Neon = Color(0xFF22D3EE)
private val Amber = Color(0xFFFBBF24)
private val Pink = Color(0xFFF472B6)
private val Violet = Color(0xFFA78BFA)
private val PanelColor = Color(0xE60A071C)
private val MutedText = Color(0xFF9AA3B8)

private enum class Screen { Hub, Missions, Hangar, Shop, Achievements, Stats, Settings }

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
    var confirmReset by remember { mutableStateOf(false) }

    val settings = save.settings
    val selectedMode = Mode.from(save.selectedMode)
    val bestForMode = save.bestByMode[selectedMode.id] ?: 0

    val slowSeconds by remember { derivedStateOf { ceil(engine.slowTimer).toInt() } }
    val doubleSeconds by remember { derivedStateOf { ceil(engine.doubleTimer).toInt() } }
    val magnetSeconds by remember { derivedStateOf { ceil(engine.magnetTimer).toInt() } }
    val phaseSeconds by remember { derivedStateOf { ceil(engine.phaseTimer).toInt() } }
    val freezeSeconds by remember { derivedStateOf { ceil(engine.freezeTimer).toInt() } }

    fun haptic(type: HapticFeedbackType) {
        if (viewModel.store.data.settings.haptics) haptics.performHapticFeedback(type)
    }

    fun startRun() {
        val seed = if (selectedMode == Mode.Daily) Content.seedFor(save.dailyDate)
        else (System.currentTimeMillis() and 0x7fffffff).toInt()
        engine.setUpgrades(viewModel.upgrades())
        engine.setBest(bestForMode)
        engine.setReducedMotion(settings.reducedMotion)
        engine.skinColor = shipColor(save.equippedShip, settings.colorblind)
        engine.startRun(selectedMode, save.selectedSector, seed)
        screen = null
        sound.startMusic(engine.sectorId)
        haptic(HapticFeedbackType.LongPress)
    }

    fun goHub() {
        engine.toReady()
        sound.stopMusic()
        screen = Screen.Hub
        sound.play("ui")
    }

    fun openScreen(next: Screen) {
        screen = next
        sound.play("ui")
    }

    SideEffect {
        engine.setBest(bestForMode)
        engine.skinColor = shipColor(save.equippedShip, settings.colorblind)
        engine.setReducedMotion(settings.reducedMotion)
        engine.setUpgrades(viewModel.upgrades())
        sound.applySettings(settings)
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
                GameEvent.Finish -> sound.play("victory")
                GameEvent.Tick -> Unit
            }
        }
        engine.onNewRecord = { sound.play("achievement") }
        engine.onRunEnd = { viewModel.submitRun(engine) }
    }

    val reward = viewModel.lastReward
    LaunchedEffect(reward) {
        if (reward == null) return@LaunchedEffect
        val parts = mutableListOf<String>()
        parts += "+${reward.credits} жетонов"
        if (reward.levelUps > 0) parts += "Новый уровень x${reward.levelUps}"
        parts += reward.achievements
        parts += reward.secrets
        parts += reward.missions
        if (reward.seasonTiers.isNotEmpty()) parts += "Награды сезона: ${reward.seasonTiers.size}"
        toast = parts.joinToString("  ·  ")
        delay(4200)
        toast = null
    }

    DisposableEffect(lifecycleOwner, engine, sound) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_PAUSE -> { engine.pause(); sound.pauseMusic() }
                Lifecycle.Event.ON_RESUME -> {
                    if (screen == null && engine.status == GameStatus.Playing && !engine.paused) sound.resumeMusic()
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
            .then(
                if (screen == null && engine.status == GameStatus.Playing && !engine.paused) {
                    Modifier.pointerInput(engine) { detectTapGestures { engine.tap() } }
                } else Modifier,
            ),
    ) {
        if (screen == null) {
            Canvas(
                modifier = Modifier
                    .fillMaxSize()
                    .onSizeChanged { engine.resize(it.width, it.height) },
            ) {
                drawGame(
                    engine, frame, art,
                    shipSprite = save.equippedShip,
                    planetStyle = save.equippedPlanet,
                    trailStyle = save.equippedTrail,
                )
            }
            InGameHud(engine, settings, bestForMode, slowSeconds, doubleSeconds, magnetSeconds, phaseSeconds, freezeSeconds,
                onPause = { engine.pause(); sound.pauseMusic(); haptic(HapticFeedbackType.TextHandleMove) },
                onToggleMute = { viewModel.updateSettings { it.muted = !it.muted }; sound.play("ui") },
                onHome = { goHub() },
            )
            GameOverlays(
                engine = engine,
                reward = reward,
                best = bestForMode,
                onResume = { engine.resume(); sound.resumeMusic() },
                onRestart = { startRun() },
                onHome = { goHub() },
            )
        } else {
            Canvas(modifier = Modifier.fillMaxSize()) {
                drawMenuBackground(art, save.equippedPlanet * 2, frame)
            }
            when (screen) {
                Screen.Hub -> HubScreen(viewModel, engine, art, frame, ::startRun, ::openScreen)
                Screen.Missions -> MissionsScreen(viewModel, ::goHub)
                Screen.Hangar -> HangarScreen(viewModel, art, ::goHub)
                Screen.Shop -> ShopScreen(viewModel, ::goHub)
                Screen.Achievements -> AchievementsScreen(viewModel, ::goHub)
                Screen.Stats -> StatsScreen(viewModel, ::goHub)
                Screen.Settings -> SettingsScreen(viewModel, confirmReset, { confirmReset = it }, ::goHub)
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
                Surface(color = Color(0xF20B1A2E), shape = RoundedCornerShape(18.dp)) {
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

// ------------------------------------------------------------------ HUD ---

@Composable
private fun InGameHud(
    engine: GameEngine,
    settings: com.example.orbit.data.GameSettings,
    best: Int,
    slow: Int,
    double: Int,
    magnet: Int,
    phase: Int,
    freeze: Int,
    onPause: () -> Unit,
    onToggleMute: () -> Unit,
    onHome: () -> Unit,
) {
    Column(modifier = Modifier.fillMaxSize().statusBarsPadding().padding(16.dp)) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            StatBlock("СЧЁТ", "${engine.score}", if (engine.multiplier > 1) "x${engine.multiplier}" else null, Neon)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                RoundButton(if (settings.muted) "×" else "♪", onToggleMute)
                RoundButton("II", onPause)
                RoundButton("⌂", onHome)
            }
            StatBlock("РЕКОРД", "${maxOf(best, engine.best)}", null, Amber, alignEnd = true)
        }

        if (engine.timeLeft > 0f || engine.combo > 0) {
            Spacer(Modifier.height(10.dp))
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Center) {
                if (engine.timeLeft > 0f) {
                    EffectChip("${ceil(engine.timeLeft).toInt()}с", Violet)
                    Spacer(Modifier.width(6.dp))
                }
                if (engine.combo > 0) EffectChip("КОМБО ${engine.combo}", Neon)
            }
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

        Spacer(Modifier.weight(1f))

        if (engine.stormWarning > 0f) {
            Banner("МЕТЕОРНЫЙ ДОЖДЬ", Pink)
        } else if (engine.flareWarning > 0f) {
            Banner("СОЛНЕЧНАЯ ВСПЫШКА", Amber)
        } else if (engine.levelUpFlash > 0f) {
            Banner("УРОВЕНЬ ${engine.level}", Amber, engine.levelUpFlash)
        }
        Spacer(Modifier.height(64.dp))
    }
}

@Composable
private fun StatBlock(label: String, value: String, extra: String?, color: Color, alignEnd: Boolean = false) {
    Column(horizontalAlignment = if (alignEnd) Alignment.End else Alignment.Start) {
        Text(label, color = MutedText, fontSize = 10.sp, letterSpacing = 3.sp)
        Text(value, color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.Black)
        if (extra != null) Text(extra, color = color, fontSize = 12.sp, fontWeight = FontWeight.Bold)
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
            .background(Color.White.copy(alpha = 0.09f))
            .border(1.dp, Color.White.copy(alpha = 0.12f), CircleShape)
            .clickable(onClick = onClick),
        contentAlignment = Alignment.Center,
    ) {
        Text(label, color = Color.White.copy(alpha = 0.9f), fontSize = 15.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun EffectChip(label: String, color: Color) {
    Surface(color = color.copy(alpha = 0.18f), shape = RoundedCornerShape(50)) {
        Text(label, modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp), color = color,
            fontSize = 10.sp, fontWeight = FontWeight.SemiBold, letterSpacing = 1.sp)
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
) {
    when {
        engine.paused -> ModalCard {
            Title("ПАУЗА", Neon)
            Body("Полёт приостановлен")
            Spacer(Modifier.height(20.dp))
            WideButton("ПРОДОЛЖИТЬ", onResume)
            WideButton("ЗАНОВО", onRestart, accent = Violet)
            WideButton("В ХАБ", onHome, accent = MutedText)
        }

        engine.status == GameStatus.GameOver -> ModalCard {
            Title(if (engine.completed) "МАРШРУТ ЗАВЕРШЁН" else "ПОТЕРЯН СИГНАЛ", if (engine.completed) Neon else Pink)
            Text("${engine.score}", color = Color.White, fontSize = 56.sp, fontWeight = FontWeight.Black)
            if (engine.isNewRecord) Text("НОВЫЙ РЕКОРД", color = Amber, fontSize = 12.sp, letterSpacing = 3.sp)
            else Text("Рекорд: $best", color = MutedText, fontSize = 12.sp)
            Spacer(Modifier.height(10.dp))
            StatRow(
                "Уровень ${engine.level} · Сферы ${engine.orbsCollected} · Комбо x${engine.maxCombo} · " +
                    "Уклонения ${engine.stats.dodges} · Боссы ${engine.stats.bossKills}",
            )
            if (reward != null) {
                Spacer(Modifier.height(12.dp))
                Panel {
                    Text("НАГРАДА", color = Neon, fontSize = 10.sp, letterSpacing = 3.sp)
                    Spacer(Modifier.height(6.dp))
                    StatRow("+${reward.credits} жетонов · +${reward.xp} XP")
                    if (reward.achievements.isNotEmpty()) StatRow("Достижения: ${reward.achievements.joinToString(", ")}", Amber)
                    if (reward.secrets.isNotEmpty()) StatRow("Секреты: ${reward.secrets.joinToString(", ")}", Violet)
                    if (reward.missions.isNotEmpty()) StatRow("Задания: ${reward.missions.joinToString(", ")}", Neon)
                    if (reward.levelUps > 0) StatRow("Уровень игрока +${reward.levelUps}", Amber)
                }
            }
            Spacer(Modifier.height(18.dp))
            WideButton("ЕЩЁ РАЗ", onRestart)
            WideButton("В ХАБ", onHome, accent = MutedText)
        }
    }
}

// ------------------------------------------------------------------ HUB ---

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
    val mode = Mode.from(save.selectedMode)
    val sector = Content.sector(save.selectedSector)
    val progress = (save.xp % Content.xpPerLevel) / Content.xpPerLevel.toFloat()
    val seasonProgress = (save.seasonXp.toFloat() / Content.seasonTiers.last().xp).coerceIn(0f, 1f)
    val missions = Content.missionsFor(save.dailyDate)

    Column(
        modifier = Modifier.fillMaxSize().statusBarsPadding().verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 18.dp),
    ) {
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Center) {
            Canvas(modifier = Modifier.size(56.dp)) {
                drawPreviewPlanet(art, save.equippedPlanet, androidx.compose.ui.geometry.Offset(size.width / 2, size.height / 2), 26.dp.toPx(), frame)
            }
            Spacer(Modifier.width(10.dp))
            Column {
                Text("ОРБИТА", color = Neon, fontSize = 30.sp, fontWeight = FontWeight.Black, letterSpacing = 8.sp)
                Text("СЕЗОН ${save.seasonId} · УРОВЕНЬ ${save.playerLevel}", color = MutedText, fontSize = 10.sp, letterSpacing = 2.sp)
            }
            Spacer(Modifier.weight(1f))
            Column(horizontalAlignment = Alignment.End) {
                Text("◆ ${save.credits}", color = Amber, fontSize = 17.sp, fontWeight = FontWeight.Bold)
                if (save.dailyStreak > 0) Text("${save.dailyStreak} ДНЕЙ", color = Pink, fontSize = 10.sp)
            }
        }
        Spacer(Modifier.height(14.dp))
        Bar(progress, Neon, "XP ${save.xp % Content.xpPerLevel} / ${Content.xpPerLevel}")
        Spacer(Modifier.height(6.dp))
        Bar(seasonProgress, Violet, "СЕЗОН ${save.seasonXp} / ${Content.seasonTiers.last().xp}")

        Spacer(Modifier.height(20.dp))
        SectionTitle("РЕЖИМ")
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Content.modes.forEach { m ->
                ChoiceChip(
                    label = m.title,
                    selected = m.id == mode.id,
                    modifier = Modifier.weight(1f),
                    onClick = { viewModel.selectMode(m.id) },
                )
            }
        }
        Spacer(Modifier.height(6.dp))
        Body(mode.detail)

        Spacer(Modifier.height(16.dp))
        SectionTitle("СЕКТОР")
        Row(modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
            Content.sectors.forEach { s ->
                val unlocked = save.playerLevel >= s.unlockLevel || save.visitedSectors.contains(s.id)
                ChoiceChip(
                    label = if (unlocked) s.name else "🔒 ${s.name}",
                    selected = s.id == sector.id,
                    locked = !unlocked,
                    onClick = { viewModel.selectSector(s.id) },
                )
                Spacer(Modifier.width(8.dp))
            }
        }
        Spacer(Modifier.height(6.dp))
        Body("${sector.subtitle} · Рекорд ${save.bestByMode[mode.id] ?: 0}")

        Spacer(Modifier.height(22.dp))
        WideButton("НАЧАТЬ ПОЛЁТ  ▸", onStart)
        Spacer(Modifier.height(12.dp))
        val missionsDone = missions.indices.count { i ->
            save.dailyProgress.getOrNull(i)?.let { v -> v >= missions[i].goal } == true
        }
        WideButton("ЗАДАНИЯ ДНЯ  $missionsDone/3",
            { onOpen(Screen.Missions) }, accent = Violet)
        Spacer(Modifier.height(20.dp))

        val tiles = listOf(
            Triple("АНГАР", "Корабли и улучшения", Screen.Hangar),
            Triple("МАГАЗИН", "Предметы за жетоны", Screen.Shop),
            Triple("ДОСТИЖЕНИЯ", "Награды и секреты", Screen.Achievements),
            Triple("СТАТИСТИКА", "Рекорды и история", Screen.Stats),
            Triple("НАСТРОЙКИ", "Звук и сохранение", Screen.Settings),
        )
        tiles.chunked(2).forEach { pair ->
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                pair.forEach { (title, subtitle, target) ->
                    NavTile(title, subtitle, Modifier.weight(1f)) { onOpen(target) }
                }
                if (pair.size == 1) Spacer(Modifier.weight(1f))
            }
            Spacer(Modifier.height(10.dp))
        }

        Spacer(Modifier.height(8.dp))
        Body("Тап во время полёта меняет направление. ${engine.stats.dodges} уклонений в прошлом забеге.", MutedText)
        Spacer(Modifier.height(20.dp))
    }
}

// -------------------------------------------------------------- SCREENS ---

@Composable
private fun MissionsScreen(viewModel: GameViewModel, onBack: () -> Unit) {
    val save = viewModel.store.data
    val defs = Content.missionsFor(save.dailyDate)
    ScrollScreen("ЗАДАНИЯ ДНЯ", viewModel, onBack, "Задания обновляются каждый день в 00:00 UTC.") {
        defs.forEachIndexed { index, m ->
            val value = save.dailyProgress.getOrElse(index) { 0 }
            val done = value >= m.goal
            Panel {
                Text(m.title, color = if (done) Neon else Color.White, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(4.dp))
                Bar((value.toFloat() / m.goal).coerceIn(0f, 1f), if (done) Neon else Violet, "$value / ${m.goal}")
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
                    Text("Уровень ${i + 1} · ${tier.xp} XP", color = Color.White, fontSize = 13.sp)
                    Text("+${tier.reward} ◈", color = Amber, fontSize = 13.sp)
                }
                Spacer(Modifier.height(4.dp))
                Text(if (claimed) "ПОЛУЧЕНО" else if (unlocked) "ДОСТУПНО — награда начисляется после забега" else "Ещё ${tier.xp - save.seasonXp} XP",
                    color = if (unlocked) Neon else MutedText, fontSize = 11.sp)
            }
            Spacer(Modifier.height(6.dp))
        }
        Spacer(Modifier.height(12.dp))
        Body("Серия заданий: ${save.dailyStreak} дней · Сыграно дзен-забегов: ${save.zenRuns}")
    }
}

@Composable
private fun HangarScreen(viewModel: GameViewModel, art: GameArt, onBack: () -> Unit) {
    val save = viewModel.store.data
    val context = LocalContext.current
    ScrollScreen("АНГАР", viewModel, onBack, "Все корабли, шлейфы и планеты доступны только локально.") {
        Box(modifier = Modifier.fillMaxWidth().height(170.dp)) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                drawPreviewPlanet(art, save.equippedPlanet, androidx.compose.ui.geometry.Offset(size.width / 2, size.height / 2), 78.dp.toPx(), 0L)
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
                        Text(item.title, color = Color.White, fontSize = 13.sp, fontWeight = FontWeight.Bold)
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
    ScrollScreen("МАГАЗИН", viewModel, onBack, "Жетоны зарабатываются в забегах, заданиях и по достижениям. Баланс: ◆ ${save.credits}") {
        listOf("ship" to "КОРАБЛИ", "trail" to "ШЛЕЙФЫ", "planet" to "ФОНЫ", "upgrade" to "УЛУЧШЕНИЯ").forEach { (cat, title) ->
            SectionTitle(title)
            Content.shop.filter { it.category == cat }.forEach { item ->
                val owned = save.inventory.contains(item.id)
                Panel {
                    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(item.title, color = Color.White, fontSize = 14.sp, fontWeight = FontWeight.Bold)
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
                        Text(a.title, color = if (open) Color.White else MutedText, fontSize = 13.sp, fontWeight = FontWeight.Bold)
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
        Row(modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
            Content.modes.forEach { m ->
                ChoiceChip(m.title, modeTab == m.id) {
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
            else top.forEachIndexed { i, score -> Text("${i + 1}.  $score", color = if (i == 0) Amber else Color.White, fontSize = 14.sp) }
        }
        Spacer(Modifier.height(16.dp))
        SectionTitle("ИСТОРИЯ ЗАБЕГОВ")
        save.history.take(12).forEach { r ->
            Panel {
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text("${Mode.from(r.mode).title} · ${Content.sector(r.sector).name}", color = Color.White, fontSize = 12.sp)
                    Text("${r.score}", color = Neon, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                }
                Body("Ур. ${r.level} · Сферы ${r.orbs} · Комбо x${r.maxCombo} · ${r.seconds}с")
            }
            Spacer(Modifier.height(5.dp))
        }
    }
}

@Composable
private fun SettingsScreen(viewModel: GameViewModel, confirmReset: Boolean, setConfirmReset: (Boolean) -> Unit, onBack: () -> Unit) {
    val save = viewModel.store.data
    var exportText by remember { mutableStateOf("") }
    var importText by remember { mutableStateOf("") }
    var importResult by remember { mutableStateOf("") }
    ScrollScreen("НАСТРОЙКИ", viewModel, onBack, "Прогресс сохраняется автоматически после каждого забега и действия.") {
        SectionTitle("ЗВУК")
        Panel {
            Text("Музыка ${(save.settings.music * 100).toInt()}%", color = Color.White, fontSize = 12.sp)
            Slider(value = save.settings.music, onValueChange = { viewModel.updateSettings { s -> s.music = it } }, valueRange = 0f..1f)
            Text("Эффекты ${(save.settings.sfx * 100).toInt()}%", color = Color.White, fontSize = 12.sp)
            Slider(value = save.settings.sfx, onValueChange = { viewModel.updateSettings { s -> s.sfx = it } }, valueRange = 0f..1f)
            ToggleRow("Полное выключение звука", save.settings.muted) { viewModel.updateSettings { s -> s.muted = it } }
            ToggleRow("Вибрация", save.settings.haptics) { viewModel.updateSettings { s -> s.haptics = it } }
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("ДОСТУПНОСТЬ")
        Panel {
            ToggleRow("Уменьшить анимацию и тряску", save.settings.reducedMotion) { viewModel.updateSettings { s -> s.reducedMotion = it } }
            ToggleRow("Контрастные цвета", save.settings.colorblind) { viewModel.updateSettings { s -> s.colorblind = it } }
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("СОХРАНЕНИЕ")
        Panel {
            Body("Код экспорта создаёт код с контролем целостности. Импорт заменяет текущий прогресс.")
            WideButton("ЭКСПОРТИРОВАТЬ КОД", { exportText = viewModel.exportCode() }, accent = Violet)
            if (exportText.isNotEmpty()) {
                OutlinedTextField(value = exportText, onValueChange = {}, readOnly = true, modifier = Modifier.fillMaxWidth(), singleLine = true)
            }
            Spacer(Modifier.height(8.dp))
            OutlinedTextField(value = importText, onValueChange = { importText = it }, modifier = Modifier.fillMaxWidth(),
                singleLine = true, placeholder = { Text("ORB1-...") })
            Spacer(Modifier.height(6.dp))
            WideButton("ИМПОРТИРОВАТЬ", {
                importResult = if (viewModel.importCode(importText)) "Прогресс восстановлен" else "Код недействителен"
            }, accent = Neon)
            if (importResult.isNotEmpty()) Body(importResult, Amber)
        }
        Spacer(Modifier.height(14.dp))
        SectionTitle("ОПАСНАЯ ЗОНА")
        if (!confirmReset) WideButton("СБРОСИТЬ ПРОГРЕСС", { setConfirmReset(true) }, accent = Pink)
        else {
            Body("Точно удалить все рекорды, предметы и достижения?", Pink)
            WideButton("ДА, СБРОСИТЬ", { viewModel.resetProgress(); setConfirmReset(false) }, accent = Pink)
            WideButton("ОТМЕНА", { setConfirmReset(false) }, accent = MutedText)
        }
        Spacer(Modifier.height(30.dp))
        Body("Орбита 3.5 · Локальная игра без рекламы и сети")
    }
}

// -------------------------------------------------------------- HELPERS ---

@Composable
private fun ScrollScreen(title: String, viewModel: GameViewModel, onBack: () -> Unit, subtitle: String, content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().statusBarsPadding().verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 18.dp),
    ) {
        ScreenHeader(title, viewModel, onBack)
        Spacer(Modifier.height(12.dp))
        Body(subtitle)
        Spacer(Modifier.height(16.dp))
        content()
        Spacer(Modifier.height(24.dp))
    }
}

@Composable
private fun ScreenHeader(title: String, viewModel: GameViewModel, onBack: () -> Unit) {
    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Center) {
        Column(Modifier.weight(1f)) {
            Text(title, color = Neon, fontSize = 26.sp, fontWeight = FontWeight.Black, letterSpacing = 4.sp)
            Text("◆ ${viewModel.store.data.credits}", color = Amber, fontSize = 12.sp)
        }
        SmallButton("← ХАБ", true, onBack)
    }
}

@Composable
private fun SectionTitle(text: String) {
    Text(text, color = Neon.copy(alpha = 0.8f), fontSize = 11.sp, fontWeight = FontWeight.Bold, letterSpacing = 3.sp)
    Spacer(Modifier.height(7.dp))
}

@Composable
private fun Body(text: String, color: Color = MutedText) {
    Text(text, color = color, fontSize = 12.sp, lineHeight = 17.sp)
}

@Composable
private fun Panel(content: @Composable ColumnScope.() -> Unit) {
    Surface(color = PanelColor, shape = RoundedCornerShape(16.dp), modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(14.dp), content = content)
    }
}

@Composable
private fun ModalCard(content: @Composable ColumnScope.() -> Unit) {
    Box(modifier = Modifier.fillMaxSize().background(Color(0x9903020A)), contentAlignment = Alignment.Center) {
        Surface(color = Color(0xF2080A1C), shape = RoundedCornerShape(24.dp), modifier = Modifier.padding(24.dp)) {
            Column(
                modifier = Modifier.fillMaxWidth(0.92f).heightIn(max = 600.dp).verticalScroll(rememberScrollState())
                    .padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                content = content,
            )
        }
    }
}

@Composable
private fun Title(text: String, color: Color) {
    Text(text, color = color, fontSize = 20.sp, fontWeight = FontWeight.Black, letterSpacing = 3.sp, textAlign = TextAlign.Center)
    Spacer(Modifier.height(12.dp))
}

@Composable
private fun StatRow(text: String, color: Color = MutedText) {
    Text(text, color = color, fontSize = 11.sp, lineHeight = 16.sp)
}

@Composable
private fun WideButton(text: String, onClick: () -> Unit, enabled: Boolean = true, accent: Color = Neon) {
    Button(
        onClick = onClick,
        enabled = enabled,
        modifier = Modifier.fillMaxWidth().height(48.dp),
        shape = RoundedCornerShape(14.dp),
    ) {
        Text(text, color = Color(0xFF06131C), fontWeight = FontWeight.Bold, fontSize = 13.sp, letterSpacing = 1.sp)
    }
    Spacer(Modifier.height(5.dp))
}

@Composable
private fun SmallButton(text: String, enabled: Boolean = true, onClick: () -> Unit) {
    Surface(
        color = if (enabled) Neon.copy(alpha = 0.2f) else Color.White.copy(alpha = 0.05f),
        shape = RoundedCornerShape(10.dp),
        modifier = Modifier.clickable(enabled = enabled, onClick = onClick),
    ) {
        Text(text, color = if (enabled) Neon else MutedText, fontSize = 10.sp, fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 7.dp))
    }
}

@Composable
private fun NavTile(title: String, subtitle: String, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Surface(color = PanelColor, shape = RoundedCornerShape(16.dp), modifier = modifier.clickable(onClick = onClick)) {
        Column(modifier = Modifier.padding(14.dp)) {
            Text(title, color = Color.White, fontSize = 13.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(3.dp))
            Text(subtitle, color = MutedText, fontSize = 10.sp)
            Spacer(Modifier.height(10.dp))
            Text("→", color = Neon, fontSize = 16.sp)
        }
    }
}

@Composable
private fun ShopTile(
    title: String, detail: String, price: Int, owned: Boolean, equipped: Boolean,
    modifier: Modifier = Modifier, onBuy: () -> Unit, onEquip: () -> Unit,
) {
    Surface(color = PanelColor, shape = RoundedCornerShape(14.dp), modifier = modifier.clickable {
        if (!owned) onBuy() else onEquip()
    }) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(title, color = if (equipped) Neon else Color.White, fontSize = 12.sp, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(2.dp))
            Text(detail, color = MutedText, fontSize = 9.sp)
            Spacer(Modifier.height(8.dp))
            Text(if (owned) (if (equipped) "НОСИМ" else "НАДЕТЬ") else "$price ◈", color = if (owned) Neon else Amber, fontSize = 11.sp)
        }
    }
}

@Composable
private fun Metric(label: String, value: String, modifier: Modifier = Modifier) {
    Surface(color = PanelColor, shape = RoundedCornerShape(14.dp), modifier = modifier) {
        Column(modifier = Modifier.padding(12.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Text(label, color = MutedText, fontSize = 9.sp, letterSpacing = 2.sp)
            Text(value, color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.Black)
        }
    }
}

@Composable
private fun ChoiceChip(label: String, selected: Boolean, modifier: Modifier = Modifier, locked: Boolean = false, onClick: () -> Unit) {
    Surface(
        color = if (selected) Neon.copy(alpha = 0.22f) else Color.White.copy(alpha = 0.05f),
        shape = RoundedCornerShape(10.dp),
        modifier = modifier.clickable(enabled = !locked, onClick = onClick),
    ) {
        Text(label, color = if (locked) MutedText else if (selected) Neon else Color.White.copy(alpha = 0.8f),
            fontSize = 9.sp, fontWeight = FontWeight.SemiBold, letterSpacing = 1.sp,
            modifier = Modifier.padding(horizontal = 9.dp, vertical = 8.dp), maxLines = 1)
    }
}

@Composable
private fun Bar(fraction: Float, color: Color, label: String) {
    Column {
        Text(label, color = MutedText, fontSize = 9.sp, letterSpacing = 1.sp)
        Spacer(Modifier.height(4.dp))
        Box(modifier = Modifier.fillMaxWidth().height(5.dp).clip(RoundedCornerShape(3.dp)).background(Color.White.copy(alpha = 0.08f))) {
            Box(modifier = Modifier.fillMaxWidth(fraction.coerceIn(0f, 1f)).height(5.dp)
                .clip(RoundedCornerShape(3.dp)).background(color))
        }
    }
}

@Composable
private fun ToggleRow(text: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(modifier = Modifier.fillMaxWidth().padding(top = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(text, color = Color.White.copy(alpha = 0.85f), fontSize = 12.sp, modifier = Modifier.weight(1f))
        Switch(checked = checked, onCheckedChange = onChange)
    }
}

private fun shipColor(index: Int, colorblind: Boolean): Color {
    if (colorblind) return Color(0xFFFFD654)
    return when (index.coerceIn(0, 7)) {
        0 -> Color(0xFF22D3EE); 1 -> Color(0xFFFB7185); 2 -> Color(0xFFA3E635); 3 -> Color(0xFFC084FC)
        4 -> Color(0xFFFBB84F); 5 -> Color(0xFFFF5CCD); 6 -> Color(0xFF78DCFF); else -> Color(0xFF58FFCC)
    }
}



