package com.example.orbit.data

import android.content.Context
import com.example.orbit.game.Content
import com.example.orbit.game.Mode
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.time.LocalDate
import java.time.ZoneOffset
import java.util.Base64
import java.util.zip.CRC32

/** Полностью локальное сохранение JSON v3: валидация, резервная копия, экспорт/импорт кодом. */
data class RunRecord(
    val score: Int = 0,
    val mode: String = "classic",
    val sector: Int = 0,
    val level: Int = 1,
    val orbs: Int = 0,
    val maxCombo: Int = 0,
    val nearMisses: Int = 0,
    val dodges: Int = 0,
    val bossKills: Int = 0,
    val wormholes: Int = 0,
    val rareOrbs: Int = 0,
    val seconds: Int = 0,
    val timestamp: Long = 0L,
)

data class GameSettings(
    var music: Float = 0.7f,
    var sfx: Float = 0.9f,
    var muted: Boolean = false,
    var haptics: Boolean = true,
    var reducedMotion: Boolean = false,
    var colorblind: Boolean = false,
)

data class SaveData(
    var version: Int = 3,
    var credits: Int = 0,
    var xp: Int = 0,
    var bestByMode: MutableMap<String, Int> = mutableMapOf(),
    var topByMode: MutableMap<String, MutableList<Int>> = mutableMapOf(),
    var history: MutableList<RunRecord> = mutableListOf(),
    var achievements: MutableList<String> = mutableListOf(),
    var dailyDate: String = "",
    var dailyProgress: MutableList<Int> = mutableListOf(0, 0, 0),
    var dailyClaimed: MutableList<Boolean> = mutableListOf(false, false, false),
    var dailyStreak: Int = 0,
    var lastPlayedDay: String = "",
    var seasonId: String = "",
    var seasonXp: Int = 0,
    var seasonClaimed: MutableList<Boolean> = mutableListOf(false, false, false, false),
    var inventory: MutableList<String> = mutableListOf("ship_cyan", "trail_spark", "planet_nebula"),
    var equippedShip: Int = 0,
    var equippedTrail: Int = 0,
    var equippedPlanet: Int = 0,
    var upgrades: MutableMap<String, Int> = mutableMapOf(),
    var secrets: MutableList<String> = mutableListOf(),
    var visitedSectors: MutableList<Int> = mutableListOf(),
    var modesPlayed: MutableList<String> = mutableListOf(),
    var zenRuns: Int = 0,
    var selectedMode: String = "classic",
    var selectedSector: Int = 0,
    var mutedTrack: Int = -1,
    var tutorialSeen: Boolean = false,
    var settings: GameSettings = GameSettings(),
) {
    val playerLevel: Int get() = 1 + xp / Content.xpPerLevel
}

data class RewardSummary(
    val credits: Int = 0,
    val xp: Int = 0,
    val achievements: List<String> = emptyList(),
    val missions: List<String> = emptyList(),
    val secrets: List<String> = emptyList(),
    val seasonTiers: List<Int> = emptyList(),
    val levelUps: Int = 0,
    val newBest: Boolean = false,
    val creditsBefore: Int = 0,
)

class SaveStore(context: Context) {
    private val dir = context.filesDir
    private val mainFile = File(dir, "orbit_save_v3.json")
    private val backupFile = File(dir, "orbit_save_v3.bak")

    var data: SaveData = readAll()
        private set

    fun today(): String = LocalDate.now(ZoneOffset.UTC).toString()

    fun seasonId(): String {
        val epoch = LocalDate.of(2026, 1, 5).toEpochDay()
        val today = LocalDate.now(ZoneOffset.UTC).toEpochDay()
        val week = Math.floorDiv(today - epoch, 56)
        return "S${String.format("%02d", maxOf(1, week.toInt() + 1))}"
    }

    private fun readAll(): SaveData {
        val parsed = decode(tryRead(mainFile)) ?: decode(tryRead(backupFile))
        return parsed ?: SaveData().also {
            it.dailyDate = today()
            it.seasonId = seasonId()
            writeAll(it)
        }
    }

    private fun tryRead(file: File): String? = try {
        if (file.exists()) file.readText(Charsets.UTF_8) else null
    } catch (_: Exception) {
        null
    }

    private fun decode(raw: String?): SaveData? {
        if (raw.isNullOrBlank()) return null
        return try {
            val o = JSONObject(raw)
            if (o.optInt("version", 0) != 3) return null
            val d = SaveData()
            d.version = 3
            d.credits = o.optInt("credits", 0).coerceAtLeast(0)
            d.xp = o.optInt("xp", 0).coerceAtLeast(0)
            val best = o.optJSONObject("bestByMode")
            if (best != null) for (k in best.keys()) d.bestByMode[k] = best.optInt(k, 0)
            val top = o.optJSONObject("topByMode")
            if (top != null) for (k in top.keys()) {
                val arr = top.optJSONArray(k) ?: continue
                d.topByMode[k] = MutableList(arr.length()) { arr.optInt(it, 0) }
                    .filter { it >= 0 }.sortedDescending().take(10).toMutableList()
            }
            val hist = o.optJSONArray("history")
            if (hist != null) for (i in 0 until hist.length()) {
                val r = hist.optJSONObject(i) ?: continue
                d.history += RunRecord(
                    score = r.optInt("score"), mode = r.optString("mode", "classic"),
                    sector = r.optInt("sector"), level = r.optInt("level", 1),
                    orbs = r.optInt("orbs"), maxCombo = r.optInt("maxCombo"),
                    nearMisses = r.optInt("nearMisses"), dodges = r.optInt("dodges"),
                    bossKills = r.optInt("bossKills"), wormholes = r.optInt("wormholes"),
                    rareOrbs = r.optInt("rareOrbs"), seconds = r.optInt("seconds"),
                    timestamp = r.optLong("timestamp"),
                )
            }
            d.achievements += strings(o.optJSONArray("achievements"))
            d.dailyDate = o.optString("dailyDate", today())
            d.dailyProgress = ints(o.optJSONArray("dailyProgress"), 3)
            d.dailyClaimed = bools(o.optJSONArray("dailyClaimed"), 3)
            d.dailyStreak = o.optInt("dailyStreak")
            d.lastPlayedDay = o.optString("lastPlayedDay", "")
            d.seasonId = o.optString("seasonId", seasonId())
            d.seasonXp = o.optInt("seasonXp")
            d.seasonClaimed = bools(o.optJSONArray("seasonClaimed"), 4)
            d.inventory = strings(o.optJSONArray("inventory")).ifEmpty { d.inventory }
            d.equippedShip = o.optInt("equippedShip")
            d.equippedTrail = o.optInt("equippedTrail")
            d.equippedPlanet = o.optInt("equippedPlanet")
            val up = o.optJSONObject("upgrades")
            if (up != null) for (k in up.keys()) d.upgrades[k] = up.optInt(k, 0)
            d.secrets += strings(o.optJSONArray("secrets"))
            d.visitedSectors = ints(o.optJSONArray("visitedSectors"), 0).toMutableList()
            d.modesPlayed = strings(o.optJSONArray("modesPlayed")).toMutableList()
            d.zenRuns = o.optInt("zenRuns")
            d.selectedMode = o.optString("selectedMode", "classic")
            d.selectedSector = o.optInt("selectedSector")
            d.mutedTrack = o.optInt("mutedTrack", -1)
            d.tutorialSeen = o.optBoolean("tutorialSeen")
            val s = o.optJSONObject("settings")
            if (s != null) {
                d.settings.music = s.optDouble("music", 0.7).toFloat().coerceIn(0f, 1f)
                d.settings.sfx = s.optDouble("sfx", 0.9).toFloat().coerceIn(0f, 1f)
                d.settings.muted = s.optBoolean("muted")
                d.settings.haptics = s.optBoolean("haptics", true)
                d.settings.reducedMotion = s.optBoolean("reducedMotion")
                d.settings.colorblind = s.optBoolean("colorblind")
            }
            if (d.dailyDate != today()) {
                d.dailyDate = today()
                d.dailyProgress = mutableListOf(0, 0, 0)
                d.dailyClaimed = mutableListOf(false, false, false)
            }
            if (d.seasonId != seasonId()) {
                d.seasonId = seasonId()
                d.seasonXp = 0
                d.seasonClaimed = mutableListOf(false, false, false, false)
            }
            d
        } catch (_: Exception) {
            null
        }
    }

    private fun strings(a: JSONArray?): MutableList<String> {
        val out = mutableListOf<String>()
        if (a == null) return out
        for (i in 0 until a.length()) {
            val v = a.optString(i, "") ?: ""
            if (v.isNotEmpty()) out += v
        }
        return out
    }

    private fun ints(a: JSONArray?, size: Int): MutableList<Int> {
        if (a == null) return MutableList(size.coerceAtLeast(0)) { 0 }
        val out = MutableList(size.coerceAtLeast(0)) { 0 }
        val count = if (size > 0) minOf(size, a.length()) else a.length()
        for (i in 0 until count) {
            val v = a.optInt(i, 0).coerceAtLeast(0)
            if (size > 0) out[i] = v else out += v
        }
        return out
    }

    private fun bools(a: JSONArray?, size: Int): MutableList<Boolean> {
        val out = MutableList(size) { false }
        if (a == null) return out
        for (i in 0 until minOf(size, a.length())) out[i] = a.optBoolean(i, false)
        return out
    }

    fun encode(d: SaveData = data): String {
        val o = JSONObject()
        o.put("version", 3)
        o.put("credits", d.credits)
        o.put("xp", d.xp)
        val best = JSONObject()
        d.bestByMode.forEach { (k, v) -> best.put(k, v) }
        o.put("bestByMode", best)
        val top = JSONObject()
        d.topByMode.forEach { (k, v) -> top.put(k, JSONArray(v)) }
        o.put("topByMode", top)
        val hist = JSONArray()
        d.history.take(25).forEach { r ->
            val x = JSONObject()
            x.put("score", r.score); x.put("mode", r.mode); x.put("sector", r.sector)
            x.put("level", r.level); x.put("orbs", r.orbs); x.put("maxCombo", r.maxCombo)
            x.put("nearMisses", r.nearMisses); x.put("dodges", r.dodges)
            x.put("bossKills", r.bossKills); x.put("wormholes", r.wormholes)
            x.put("rareOrbs", r.rareOrbs); x.put("seconds", r.seconds); x.put("timestamp", r.timestamp)
            hist.put(x)
        }
        o.put("history", hist)
        o.put("achievements", JSONArray(d.achievements))
        o.put("dailyDate", d.dailyDate)
        o.put("dailyProgress", JSONArray(d.dailyProgress))
        o.put("dailyClaimed", JSONArray(d.dailyClaimed))
        o.put("dailyStreak", d.dailyStreak)
        o.put("lastPlayedDay", d.lastPlayedDay)
        o.put("seasonId", d.seasonId)
        o.put("seasonXp", d.seasonXp)
        o.put("seasonClaimed", JSONArray(d.seasonClaimed))
        o.put("inventory", JSONArray(d.inventory))
        o.put("equippedShip", d.equippedShip)
        o.put("equippedTrail", d.equippedTrail)
        o.put("equippedPlanet", d.equippedPlanet)
        val up = JSONObject()
        d.upgrades.forEach { (k, v) -> up.put(k, v) }
        o.put("upgrades", up)
        o.put("secrets", JSONArray(d.secrets))
        o.put("visitedSectors", JSONArray(d.visitedSectors))
        o.put("modesPlayed", JSONArray(d.modesPlayed))
        o.put("zenRuns", d.zenRuns)
        o.put("selectedMode", d.selectedMode)
        o.put("selectedSector", d.selectedSector)
        o.put("mutedTrack", d.mutedTrack)
        o.put("tutorialSeen", d.tutorialSeen)
        val s = JSONObject()
        s.put("music", d.settings.music.toDouble())
        s.put("sfx", d.settings.sfx.toDouble())
        s.put("muted", d.settings.muted)
        s.put("haptics", d.settings.haptics)
        s.put("reducedMotion", d.settings.reducedMotion)
        s.put("colorblind", d.settings.colorblind)
        o.put("settings", s)
        return o.toString()
    }

    private fun writeAll(d: SaveData) {
        try {
            if (mainFile.exists()) mainFile.copyTo(backupFile, overwrite = true)
            mainFile.writeText(encode(d), Charsets.UTF_8)
        } catch (_: Exception) {
            // Диск может быть переполнен — прогресс в памяти остаётся актуальным.
        }
    }

    fun save() = writeAll(data)

    fun submitRun(
        record: RunRecord,
        achieved: List<String>,
        foundSecrets: List<String>,
    ): RewardSummary {
        val d = data
        val day = today()
        if (d.dailyDate != day) {
            d.dailyDate = day
            d.dailyProgress = mutableListOf(0, 0, 0)
            d.dailyClaimed = mutableListOf(false, false, false)
        }
        if (d.seasonId != seasonId()) {
            d.seasonId = seasonId()
            d.seasonXp = 0
            d.seasonClaimed = mutableListOf(false, false, false, false)
        }
        val before = d.credits
        val isBest = record.score > (d.bestByMode[record.mode] ?: 0)
        if (isBest) d.bestByMode[record.mode] = record.score
        val list = d.topByMode.getOrPut(record.mode) { mutableListOf() }
        list += record.score
        val sorted = list.sortedDescending().take(10).toMutableList()
        d.topByMode[record.mode] = sorted
        d.history.add(0, record)
        while (d.history.size > 25) d.history.removeAt(d.history.lastIndex)

        var credits = record.score / 10 + record.orbs + record.rareOrbs * 20
        var xp = record.score / 20
        val levelBefore = d.playerLevel
        d.xp += xp

        if (!d.modesPlayed.contains(record.mode)) d.modesPlayed += record.mode
        if (!d.visitedSectors.contains(record.sector)) d.visitedSectors += record.sector
        if (record.mode == Mode.Zen.id) d.zenRuns += 1
        d.dailyStreak = if (d.lastPlayedDay == day) d.dailyStreak
        else if (d.lastPlayedDay == LocalDate.now(ZoneOffset.UTC).minusDays(1).toString()) d.dailyStreak + 1
        else 1
        d.lastPlayedDay = day
        d.seasonXp += record.score / 25

        val newAchievements = mutableListOf<String>()
        for (a in Content.achievements) {
            if (d.achievements.contains(a.id) || !achieved.contains(a.id)) continue
            d.achievements += a.id
            credits += a.reward
            xp += a.xp
            d.xp += a.xp
            newAchievements += a.title
        }

        val missions = mutableListOf<String>()
        val defs = Content.missionsFor(day)
        val values = mapOf(
            "orbs" to record.orbs, "dodges" to record.dodges, "nearMisses" to record.nearMisses,
            "maxCombo" to record.maxCombo, "score" to record.score, "bossKills" to record.bossKills,
            "wormholes" to record.wormholes, "level" to record.level,
        )
        defs.forEachIndexed { i, m ->
            val v = values[m.field] ?: 0
            if (v > (d.dailyProgress.getOrElse(i) { 0 })) d.dailyProgress[i] = v
            if (d.dailyProgress[i] >= m.goal && !d.dailyClaimed[i]) {
                d.dailyClaimed[i] = true
                credits += m.reward
                xp += m.reward / 3
                d.xp += m.reward / 3
                missions += "${m.title} +${m.reward}"
            }
        }

        val newSecrets = mutableListOf<String>()
        for (id in foundSecrets) {
            if (d.secrets.contains(id)) continue
            d.secrets += id
            val def = Content.secrets.firstOrNull { it.id == id } ?: continue
            credits += 200
            d.xp += 100
            newSecrets += def.title
            if (id == "collector") unlockSector(7)
        }
        if (d.secrets.contains("eye") || d.achievements.contains("boss_1")) unlockSector(7)

        val tiers = mutableListOf<Int>()
        Content.seasonTiers.forEachIndexed { i, tier ->
            if (d.seasonXp >= tier.xp && !d.seasonClaimed[i]) {
                d.seasonClaimed[i] = true
                credits += tier.reward
                tiers += i
            }
        }
        if (d.credits >= 5000 && !d.achievements.contains("rich_5000")) {
            d.achievements += "rich_5000"
            credits += 300
            newAchievements += Content.achievements.first { it.id == "rich_5000" }.title
        }
        d.credits += credits
        writeAll(d)
        return RewardSummary(
            credits = credits, xp = xp, achievements = newAchievements, missions = missions,
            secrets = newSecrets, seasonTiers = tiers, levelUps = d.playerLevel - levelBefore,
            newBest = isBest, creditsBefore = before,
        )
    }

    private fun unlockSector(id: Int) {
        val s = Content.sector(id)
        if (s.unlockLevel == 99 && !data.visitedSectors.contains(id)) {
            data.visitedSectors.add(id)
        }
    }

    fun claimMission(index: Int): Boolean {
        val defs = Content.missionsFor(data.dailyDate)
        val m = defs.getOrNull(index) ?: return false
        val progress = data.dailyProgress.getOrElse(index) { 0 }
        if (data.dailyClaimed[index] || progress < m.goal) return false
        data.dailyClaimed[index] = true
        data.credits += m.reward
        data.xp += m.reward / 2
        save()
        return true
    }

    fun buy(itemId: String): Boolean {
        val item = Content.shop.firstOrNull { it.id == itemId } ?: return false
        if (data.inventory.contains(itemId) || data.credits < item.price) return false
        data.credits -= item.price
        data.inventory += itemId
        if (item.category == "upgrade") data.upgrades[itemId] = item.value
        save()
        return true
    }

    fun equip(itemId: String): Boolean {
        val item = Content.shop.firstOrNull { it.id == itemId } ?: return false
        if (!data.inventory.contains(itemId)) return false
        when (item.category) {
            "ship" -> data.equippedShip = item.value
            "trail" -> data.equippedTrail = item.value
            "planet" -> data.equippedPlanet = item.value
            else -> return false
        }
        save()
        return true
    }

    fun hasUpgrade(id: String): Boolean = (data.upgrades[id] ?: 0) > 0

    fun exportCode(): String {
        val bytes = encode().toByteArray(Charsets.UTF_8)
        val crc = CRC32().apply { update(bytes) }.value.toInt()
        val payload = bytes + byteArrayOf(
            (crc ushr 24).toByte(), (crc ushr 16).toByte(), (crc ushr 8).toByte(), crc.toByte()
        )
        return "ORB1-" + Base64.getUrlEncoder().withoutPadding().encodeToString(payload)
    }

    fun importCode(code: String): Boolean {
        val raw = code.trim().removePrefix("ORB1-")
        return try {
            val bytes = Base64.getUrlDecoder().decode(raw)
            if (bytes.size <= 4) return false
            val body = bytes.copyOfRange(0, bytes.size - 4)
            val expected = ((bytes[bytes.size - 4].toInt() and 0xFF) shl 24) or
                ((bytes[bytes.size - 3].toInt() and 0xFF) shl 16) or
                ((bytes[bytes.size - 2].toInt() and 0xFF) shl 8) or
                (bytes[bytes.size - 1].toInt() and 0xFF)
            if ((CRC32().apply { update(body) }.value.toInt()) != expected) return false
            val parsed = decode(String(body, Charsets.UTF_8)) ?: return false
            data = parsed
            writeAll(parsed)
            true
        } catch (_: Exception) {
            false
        }
    }

    fun reset() {
        data = SaveData().also { it.dailyDate = today(); it.seasonId = seasonId() }
        writeAll(data)
    }
}
