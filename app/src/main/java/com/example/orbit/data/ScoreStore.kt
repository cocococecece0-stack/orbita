package com.example.orbit.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.scoreDataStore: DataStore<Preferences> by preferencesDataStore(name = "orbit_scores")

data class PlayerStats(
    val runs: Int = 0,
    val orbs: Int = 0,
    val bestCombo: Int = 0,
)

class ScoreStore(private val context: Context) {

    private val bestKey = intPreferencesKey("best_score")
    private val mutedKey = booleanPreferencesKey("muted")
    private val hapticsKey = booleanPreferencesKey("haptics")
    private val skinKey = intPreferencesKey("skin")
    private val topKey = stringPreferencesKey("top_scores")
    private val runsKey = intPreferencesKey("runs")
    private val orbsKey = intPreferencesKey("orbs_total")
    private val comboKey = intPreferencesKey("best_combo")

    val bestScore: Flow<Int> = context.scoreDataStore.data.map { it[bestKey] ?: 0 }

    val muted: Flow<Boolean> = context.scoreDataStore.data.map { it[mutedKey] ?: false }

    val hapticsEnabled: Flow<Boolean> = context.scoreDataStore.data.map { it[hapticsKey] ?: true }

    val skinIndex: Flow<Int> = context.scoreDataStore.data.map { it[skinKey] ?: 0 }

    val topScores: Flow<List<Int>> = context.scoreDataStore.data.map { parseTop(it[topKey]) }

    val stats: Flow<PlayerStats> = context.scoreDataStore.data.map { prefs ->
        PlayerStats(
            runs = prefs[runsKey] ?: 0,
            orbs = prefs[orbsKey] ?: 0,
            bestCombo = prefs[comboKey] ?: 0,
        )
    }

    suspend fun submitRun(score: Int, orbs: Int, bestCombo: Int) {
        context.scoreDataStore.edit { prefs ->
            if (score > (prefs[bestKey] ?: 0)) {
                prefs[bestKey] = score
            }
            prefs[runsKey] = (prefs[runsKey] ?: 0) + 1
            prefs[orbsKey] = (prefs[orbsKey] ?: 0) + orbs
            prefs[comboKey] = maxOf(prefs[comboKey] ?: 0, bestCombo)
            val top = (parseTop(prefs[topKey]) + score).sortedDescending().take(5)
            prefs[topKey] = top.joinToString(",")
        }
    }

    suspend fun setMuted(muted: Boolean) {
        context.scoreDataStore.edit { it[mutedKey] = muted }
    }

    suspend fun setHaptics(enabled: Boolean) {
        context.scoreDataStore.edit { it[hapticsKey] = enabled }
    }

    suspend fun setSkin(index: Int) {
        context.scoreDataStore.edit { it[skinKey] = index }
    }

    private fun parseTop(raw: String?): List<Int> =
        raw?.split(',')?.mapNotNull { it.toIntOrNull() } ?: emptyList()
}
