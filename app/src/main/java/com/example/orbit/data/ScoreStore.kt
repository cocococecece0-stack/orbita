package com.example.orbit.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.scoreDataStore: DataStore<Preferences> by preferencesDataStore(name = "orbit_scores")

class ScoreStore(private val context: Context) {

    private val bestKey = intPreferencesKey("best_score")

    val bestScore: Flow<Int> = context.scoreDataStore.data.map { prefs ->
        prefs[bestKey] ?: 0
    }

    suspend fun submit(score: Int) {
        context.scoreDataStore.edit { prefs ->
            if (score > (prefs[bestKey] ?: 0)) {
                prefs[bestKey] = score
            }
        }
    }
}
