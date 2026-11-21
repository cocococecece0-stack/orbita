package com.example.orbit

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import com.example.orbit.data.GameSettings
import com.example.orbit.data.RewardSummary
import com.example.orbit.data.RunRecord
import com.example.orbit.data.SaveStore
import com.example.orbit.game.Content
import com.example.orbit.game.GameEngine
import com.example.orbit.game.RunStats

/** Единая точка доступа к прогрессу. UI перерисовывается при смене [revision]. */
class GameViewModel(application: Application) : AndroidViewModel(application) {

    val store = SaveStore(application)

    var revision by mutableIntStateOf(0)
        private set

    var lastReward by mutableStateOf<RewardSummary?>(null)
        private set

    fun refresh() { revision++ }

    fun selectMode(id: String) {
        if (Content.modes.none { it.id == id }) return
        store.data.selectedMode = id
        store.save()
        refresh()
    }

    fun selectSector(id: Int) {
        if (Content.sectors.none { it.id == id }) return
        store.data.selectedSector = id
        store.save()
        refresh()
    }

    fun markTutorialSeen() {
        if (store.data.tutorialSeen) return
        store.data.tutorialSeen = true
        store.save()
        refresh()
    }

    fun submitRun(engine: GameEngine) {
        val s: RunStats = engine.stats
        val record = RunRecord(
            score = s.score,
            mode = engine.mode.id,
            sector = engine.sectorId,
            level = s.level,
            orbs = s.orbs,
            maxCombo = s.maxCombo,
            nearMisses = s.nearMisses,
            dodges = s.dodges,
            bossKills = s.bossKills,
            wormholes = s.wormholes,
            rareOrbs = s.rareOrbs,
            seconds = s.seconds,
            timestamp = System.currentTimeMillis(),
        )
        lastReward = store.submitRun(record, engine.achievedIds(), engine.secretIds())
        refresh()
    }

    fun claimMission(index: Int) {
        if (store.claimMission(index)) refresh()
    }

    fun buy(itemId: String) {
        if (store.buy(itemId)) refresh()
    }

    fun equip(itemId: String) {
        if (store.equip(itemId)) refresh()
    }

    fun updateSettings(block: (GameSettings) -> Unit) {
        block(store.data.settings)
        store.save()
        refresh()
    }

    fun exportCode(): String = store.exportCode()

    fun importCode(code: String): Boolean {
        val ok = store.importCode(code)
        if (ok) refresh()
        return ok
    }

    fun resetProgress() {
        store.reset()
        lastReward = null
        refresh()
    }

    fun upgrades(): Map<String, Boolean> = mapOf(
        "upg_shield" to store.hasUpgrade("upg_shield"),
        "upg_time" to store.hasUpgrade("upg_time"),
        "upg_magnet" to store.hasUpgrade("upg_magnet"),
        "upg_slow" to store.hasUpgrade("upg_slow"),
        "upg_double" to store.hasUpgrade("upg_double"),
    )
}
