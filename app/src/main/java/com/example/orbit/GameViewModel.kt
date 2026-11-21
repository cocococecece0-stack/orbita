package com.example.orbit

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.example.orbit.data.PlayerStats
import com.example.orbit.data.ScoreStore
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class GameViewModel(application: Application) : AndroidViewModel(application) {

    private val store = ScoreStore(application)

    val best: StateFlow<Int> = store.bestScore.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = 0,
    )

    val muted: StateFlow<Boolean> = store.muted.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = false,
    )

    val hapticsEnabled: StateFlow<Boolean> = store.hapticsEnabled.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = true,
    )

    val skinIndex: StateFlow<Int> = store.skinIndex.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = 0,
    )

    val topScores: StateFlow<List<Int>> = store.topScores.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = emptyList(),
    )

    val stats: StateFlow<PlayerStats> = store.stats.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = PlayerStats(),
    )

    fun submitRun(score: Int, orbs: Int, bestCombo: Int) {
        viewModelScope.launch { store.submitRun(score, orbs, bestCombo) }
    }

    fun toggleMuted() {
        viewModelScope.launch { store.setMuted(!muted.value) }
    }

    fun toggleHaptics() {
        viewModelScope.launch { store.setHaptics(!hapticsEnabled.value) }
    }

    fun selectSkin(index: Int) {
        viewModelScope.launch { store.setSkin(index) }
    }
}
