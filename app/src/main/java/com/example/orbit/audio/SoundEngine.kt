package com.example.orbit.audio

import android.content.Context
import android.media.AudioAttributes
import android.media.MediaPlayer
import android.media.SoundPool
import com.example.orbit.R

/**
 * Low-latency sound effects through SoundPool plus a looping ambient pad.
 * All samples are synthesized by tools/generate_sounds.py.
 */
class SoundEngine(context: Context) {

    private val pool: SoundPool
    private val soundIds = HashMap<Int, Int>()
    private val ambient: MediaPlayer? = MediaPlayer.create(context, R.raw.ambient)?.apply {
        isLooping = true
        setVolume(AMBIENT_VOLUME, AMBIENT_VOLUME)
    }

    var muted: Boolean = false
        set(value) {
            field = value
            ambient?.setVolume(
                if (value) 0f else AMBIENT_VOLUME,
                if (value) 0f else AMBIENT_VOLUME,
            )
            if (value) ambient?.pause()
        }

    init {
        val attributes = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()
        pool = SoundPool.Builder()
            .setMaxStreams(10)
            .setAudioAttributes(attributes)
            .build()
        listOf(
            R.raw.tap,
            R.raw.dodge,
            R.raw.orb,
            R.raw.powerup,
            R.raw.shield,
            R.raw.crash,
            R.raw.level,
        ).forEach { res ->
            soundIds[res] = pool.load(context, res, 1)
        }
    }

    fun play(resId: Int, rate: Float = 1f, volume: Float = 1f) {
        if (muted) return
        val id = soundIds[resId] ?: return
        pool.play(id, volume, volume, 1, 0, rate.coerceIn(0.5f, 2f))
    }

    fun startAmbient() {
        if (muted) return
        val player = ambient ?: return
        if (!player.isPlaying) player.start()
    }

    fun pauseAmbient() {
        val player = ambient ?: return
        if (player.isPlaying) player.pause()
    }

    fun release() {
        pool.release()
        ambient?.release()
    }

    private companion object {
        const val AMBIENT_VOLUME = 0.30f
    }
}
