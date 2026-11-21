package com.example.orbit

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
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

    fun submit(score: Int) {
        viewModelScope.launch { store.submit(score) }
    }

    fun toggleMuted() {
        viewModelScope.launch { store.setMuted(!muted.value) }
    }
}
