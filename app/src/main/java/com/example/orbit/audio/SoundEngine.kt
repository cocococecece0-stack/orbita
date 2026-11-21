package com.example.orbit.audio

import android.content.Context
import android.media.AudioAttributes
import android.media.MediaPlayer
import android.media.SoundPool
import com.example.orbit.data.GameSettings

/**
 * Низколатентные SFX через SoundPool и восемь отдельных музыкальных треков через MediaPlayer.
 * Все файлы — реальные WAV, сгенерированные tools/generate_assets.py.
 */
class SoundEngine(private val context: Context) {

    private val pool: SoundPool
    private val soundIds = HashMap<String, Int>()
    private val resIds = HashMap<String, Int>()
    private var music: MediaPlayer? = null
    private var musicTrack = 0
    private var musicSector = 0
    private var settings = GameSettings()

    var started = false
        private set

    init {
        val attributes = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()
        pool = SoundPool.Builder()
            .setMaxStreams(12)
            .setAudioAttributes(attributes)
            .build()
        SFX_NAMES.forEach { name ->
            val id = context.resources.getIdentifier(name, "raw", context.packageName)
            if (id != 0) {
                resIds[name] = id
                soundIds[name] = pool.load(context, id, 1)
            }
        }
    }

    fun applySettings(newSettings: GameSettings) {
        settings = newSettings
        applyMusicVolume()
        if (settings.muted) pauseMusic()
    }

    fun play(name: String, rate: Float = 1f, volume: Float = 1f) {
        if (settings.muted) return
        val id = soundIds[name] ?: return
        val v = (volume * settings.sfx * profileGain()).coerceIn(0f, 1f)
        if (v <= 0f) return
        pool.play(id, v, v, 1, 0, rate.coerceIn(0.5f, 2f))
    }

    /** Обратная совместимость с вызовами по resource id. */
    fun play(resId: Int, rate: Float = 1f, volume: Float = 1f) {
        val name = resIds.entries.firstOrNull { it.value == resId }?.key ?: return
        play(name, rate, volume)
    }

    fun startMusic(sector: Int) {
        musicSector = sector
        if (settings.muted) return
        if (music == null) {
            music = MediaPlayer().apply {
                setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_GAME)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                        .build(),
                )
                setOnCompletionListener { nextTrack() }
            }
        }
        started = true
        playTrack(trackFor(sector))
    }

    private fun playTrack(index: Int) {
        val player = music ?: return
        musicTrack = index % MUSIC_COUNT
        val name = "music_$musicTrack"
        val id = context.resources.getIdentifier(name, "raw", context.packageName)
        if (id == 0) return
        try {
            player.reset()
            player.setDataSource(context, android.net.Uri.parse("android.resource://${context.packageName}/$id"))
            player.setAudioAttributes(
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_GAME)
                    .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                    .build(),
            )
            player.isLooping = false
            player.prepare()
            applyMusicVolume()
            player.start()
        } catch (_: Exception) {
            // битый или отсутствующий трек просто пропускается
        }
    }

    private fun nextTrack() {
        if (!started || settings.muted) return
        playTrack((musicTrack + 1) % MUSIC_COUNT)
    }

    fun nextSector(sector: Int) {
        if (sector == musicSector) return
        musicSector = sector
        if (started && !settings.muted) playTrack(trackFor(sector))
    }

    private fun trackFor(sector: Int): Int = sector % MUSIC_COUNT

    fun pauseMusic() { try { music?.pause() } catch (_: Exception) {} }
    fun resumeMusic() {
        if (settings.muted || !started) return
        try {
            if (music?.isPlaying == false) music?.start()
        } catch (_: Exception) {}
    }

    fun stopMusic() {
        started = false
        try { music?.stop() } catch (_: Exception) {}
    }

    private fun applyMusicVolume() {
        val v = if (settings.muted) 0f else (settings.music * 0.55f * profileGain()).coerceIn(0f, 1f)
        try { music?.setVolume(v, v) } catch (_: Exception) {}
    }

    /** Отдельный звуковой профиль: ночной приглушает громкость, басовый оставляет динамику. */
    private fun profileGain(): Float = when (settings.audioProfile) {
        "NIGHT" -> 0.65f
        "BASS" -> 1f
        else -> 1f
    }

    fun release() {
        try { pool.release() } catch (_: Exception) {}
        try { music?.release() } catch (_: Exception) {}
        music = null
    }

    private companion object {
        const val MUSIC_COUNT = 8
        val SFX_NAMES = listOf(
            "tap", "dodge", "orb", "powerup", "shield", "crash", "level", "storm", "ui",
            "reward", "achievement", "buy", "boss", "boss_hit", "boss_down", "wormhole",
            "flare", "magnet", "phase", "freeze", "revive", "secret", "victory", "defeat",
            "tick", "gameover",
        )
    }
}
