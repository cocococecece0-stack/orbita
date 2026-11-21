package com.example.orbit.game

/** Статический контент: режимы, сектора, предметы, достижения. Зеркально совпадает с iOS Content.swift. */

enum class Mode(val id: String, val title: String, val short: String, val detail: String, val seconds: Int) {
    Classic("classic", "КЛАССИКА", "КЛАСС", "Бесконечный полёт. Один промах — конец.", 0),
    Zen("zen", "ДЗЕН", "ДЗЕН", "90 секунд без астероидов. Собирай сферы и бонусы.", 90),
    Sprint("sprint", "СПРИНТ", "СПРИНТ", "60 секунд на максимальный счёт. Удар стоит 3 секунды.", 60),
    Survival("survival", "ВЫЖИВАНИЕ", "ВЫЖИВ", "Плотность растёт постоянно. Ошибка — конец.", 0),
    Daily("daily", "ДЕЙЛИ", "ДЕЙЛИ", "Фиксированный сид дня. Общий вызов для всех.", 0);

    companion object {
        fun from(id: String?): Mode = entries.firstOrNull { it.id == id } ?: Classic
    }
}

enum class PowerUpKind(val id: String, val title: String) {
    Shield("shield", "ЩИТ"),
    Slow("slow", "ЗАМЕДЛЕНИЕ"),
    Double("double", "ДВОЙНЫЕ"),
    Magnet("magnet", "МАГНИТ"),
    Phase("phase", "ФАЗА"),
    Freeze("freeze", "ЗАМОРОЗКА"),
    Revive("revive", "ВТОРОЙ ШАНС");

    companion object {
        fun from(id: String?): PowerUpKind = entries.firstOrNull { it.id == id } ?: Shield
    }
}

enum class HazardKind(val id: String) {
    Rock("rock"), Comet("comet"), Mine("mine"), Wormhole("wormhole"),
    Gravity("gravity"), Flare("flare"), Boss("boss");

    companion object {
        fun from(id: String?): HazardKind = entries.firstOrNull { it.id == id } ?: Rock
    }
}

data class SectorDef(
    val id: Int,
    val name: String,
    val subtitle: String,
    val unlockLevel: Int,
    val hazards: List<HazardKind>,
    val density: Float,
)

object Content {
    val modes: List<Mode> = Mode.entries

    /** Названия скинов орбиты/планеты — индекс совпадает с save.equippedPlanet. */
    val planetSkins = listOf(
        "ЗЕМЛЯ", "МАРС", "ЮПИТЕР", "НЕПТУН",
        "ВЕНЕРА", "САТУРН", "ЛУНА", "ЭКЗО",
    )

    /** Типы боссов: ядро, рой и импульс. Индекс хранится в [Boss.kind]. */
    val bossNames = listOf("ЯДРО", "РОЙ", "ИМПУЛЬС")

    /** Мутации забега: игрок выбирает одну из трёх каждые ~18 секунд. У каждой есть цена. */
    data class Perk(
        val id: String,
        val title: String,
        val detail: String,
        val risk: String,
        val glyph: String,
    )

    val perks = listOf(
        Perk("overclock", "ПЕРЕГРУЗ", "+25% очков до конца забега", "Угрозы на 10% быстрее", "✦"),
        Perk("barrier", "БАРЬЕР", "Щит на 14 секунд", "−10% очков", "◆"),
        Perk("magnet", "МАГНИТ", "Сферы притягиваются 10 секунд", "Угрозы на 10% плотнее", "◎"),
        Perk("slowmo", "ЗАМЕДЛЕНИЕ", "Угрозы летят медленнее 10 секунд", "−10% очков", "◐"),
        Perk("pulse", "ИМПУЛЬС", "Волна резонанса очищает орбиту", "Сбрасывает комбо", "✺"),
        Perk("ghost", "ФАНТОМ", "Неуязвимость на 8 секунд", "Сферы появляются реже", "◇"),
        Perk("harvest", "УРОЖАЙ", "Сферы дают двойной опыт", "−10% очков", "●"),
        Perk("chronos", "ХРОНОС", "+12 секунд, если есть таймер", "Угрозы на 15% плотнее", "◷"),
    )

    /** Цель забега: видна в брифинге и в HUD, даёт смысл каждому полёту. */
    data class Objective(val title: String, val target: Int, val field: String)

    fun objective(mode: Mode, sectorId: Int): Objective {
        val tier = sector(sectorId).id
        return when (mode) {
            Mode.Zen -> Objective("СОБРАТЬ СФЕР", 18 + tier * 4, "orbs")
            Mode.Sprint -> Objective("НАБРАТЬ ОЧКОВ", 1200 + tier * 300, "score")
            else -> Objective("НАБРАТЬ ОЧКОВ", 600 + tier * 200, "score")
        }
    }

    /** Сезонные модификаторы: выбираются детерминированно по номеру сезона. */
    val seasonModifiers = listOf(
        "СТАНДАРТ" to 1.00f,
        "МЕТЕОРНЫЙ СЕЗОН" to 1.18f,
        "ЭНЕРГЕТИЧЕСКИЙ СЕЗОН" to 1.10f,
        "ТИХИЙ СЕЗОН" to 0.90f,
    )

    fun seasonModifier(seasonId: String): Pair<String, Float> {
        val n = seasonId.fold(0) { acc, c -> acc + c.code }
        return seasonModifiers[Math.floorMod(n, seasonModifiers.size)]
    }

    val sectors = listOf(
        SectorDef(0, "ОРБИТА-01", "Учебный сектор", 1, listOf(HazardKind.Rock), 0.75f),
        SectorDef(1, "ПЕПЕЛ", "Горящий пояс", 2, listOf(HazardKind.Rock, HazardKind.Comet), 0.90f),
        SectorDef(2, "НЕОНОВЫЙ ПОЯС", "Свет и помехи", 3, listOf(HazardKind.Rock, HazardKind.Comet, HazardKind.Mine), 1.00f),
        SectorDef(3, "ЛЕДЯНОЕ КОЛЬЦО", "Тяжёлые льды", 5, listOf(HazardKind.Rock, HazardKind.Gravity), 1.05f),
        SectorDef(4, "СЕРДЦЕ ШТОРМА", "Залпы и вспышки", 7, listOf(HazardKind.Rock, HazardKind.Comet, HazardKind.Flare), 1.15f),
        SectorDef(5, "ЧЕРВОТОЧИНА", "Пространство рвётся", 9, listOf(HazardKind.Rock, HazardKind.Wormhole), 1.10f),
        SectorDef(6, "ЯДРО ГИГАНТА", "Стража ядра", 12, listOf(HazardKind.Rock, HazardKind.Comet, HazardKind.Boss), 1.20f),
        SectorDef(7, "СЕКРЕТ", "Скрытый сектор", 99, listOf(HazardKind.Rock, HazardKind.Mine, HazardKind.Boss), 1.30f),
        SectorDef(8, "ПОЛЯРНАЯ КОРОНА", "Ледяные арки и кометы", 15, listOf(HazardKind.Rock, HazardKind.Comet, HazardKind.Flare), 1.22f),
        SectorDef(9, "СИНГУЛЯРНОСТЬ", "Гравитация и боссы", 22, listOf(HazardKind.Rock, HazardKind.Gravity, HazardKind.Boss), 1.28f),
    )

    fun sector(id: Int): SectorDef = sectors.getOrElse(id.coerceIn(0, sectors.lastIndex)) { sectors[0] }

    data class Achievement(
        val id: String,
        val title: String,
        val detail: String,
        val reward: Int,
        val xp: Int,
    )

    val achievements = listOf(
        Achievement("first_run", "ПЕРВЫЙ ВЫЛЕТ", "Завершить забег", 40, 20),
        Achievement("score_500", "ПОЛУТЫСЯЧНИК", "Набрать 500 очков за забег", 80, 40),
        Achievement("score_1500", "МАСТЕР ОРБИТЫ", "Набрать 1500 очков за забег", 160, 80),
        Achievement("score_5000", "ЛЕГЕНДА", "Набрать 5000 очков за забег", 400, 200),
        Achievement("combo_10", "ЦЕПНАЯ РЕАКЦИЯ", "Достичь комбо x10", 100, 50),
        Achievement("combo_25", "НЕУДЕРЖИМЫЙ", "Достичь комбо x25", 220, 110),
        Achievement("orbs_50", "СОБИРАТЕЛЬ", "50 сфер за забег", 90, 45),
        Achievement("near_10", "НА ВОЛОСКЕ", "10 близких пролётов за забег", 120, 60),
        Achievement("near_30", "АКРОБАТ", "30 близких пролётов за забег", 260, 130),
        Achievement("level_10", "ДОЛГОЛЁТ", "Достичь 10 уровня", 140, 70),
        Achievement("level_20", "ВЕТЕРАН", "Достичь 20 уровня", 300, 150),
        Achievement("boss_1", "ОХОТНИК", "Уничтожить ядро босса", 180, 90),
        Achievement("boss_5", "УБИЙЦА ГИГАНТОВ", "5 ядер за один забег", 480, 240),
        Achievement("worm_5", "ПУТЕШЕСТВЕННИК", "5 червоточин за забег", 130, 65),
        Achievement("all_modes", "УНИВЕРСАЛ", "Сыграть во все режимы", 200, 100),
        Achievement("sector_7", "ТАЙНАЯ ДОРОГА", "Открыть секрет сектор", 500, 250),
        Achievement("daily_7", "ДИСЦИПЛИНА", "7 дней подряд с заданиями", 350, 175),
        Achievement("rich_5000", "МАГНАТ", "Накопить 5000 жетонов", 300, 150),
    )

    data class ShopItem(
        val id: String,
        val title: String,
        val detail: String,
        val price: Int,
        val category: String,
        val value: Int,
    )

    val shop = listOf(
        ShopItem("ship_cyan", "КОМЕТА", "Стандартный корпус", 0, "ship", 0),
        ShopItem("ship_rose", "МАЯК", "Коралловый корпус", 300, "ship", 1),
        ShopItem("ship_lime", "ИМПУЛЬС", "Лаймовый корпус", 600, "ship", 2),
        ShopItem("ship_violet", "ПРИЗРАК", "Фиолетовый корпус", 900, "ship", 3),
        ShopItem("ship_amber", "СОЛНЦЕ", "Янтарный корпус", 1400, "ship", 4),
        ShopItem("ship_magenta", "НОВАЯ", "Пурпурный корпус", 2000, "ship", 5),
        ShopItem("ship_ice", "АЙСБЕРГ", "Ледяной корпус", 2800, "ship", 6),
        ShopItem("ship_mint", "ХЛОРОФИЛЛ", "Мятный корпус", 3600, "ship", 7),
        ShopItem("trail_spark", "ИСКРЫ", "Классический шлейф", 0, "trail", 0),
        ShopItem("trail_smoke", "ДЫМ", "Плотный шлейф", 450, "trail", 1),
        ShopItem("trail_rainbow", "СПЕКТР", "Радужный шлейф", 1200, "trail", 2),
        ShopItem("trail_wave", "ВОЛНА", "Волнистый шлейф", 2200, "trail", 3),
        ShopItem("planet_earth", "ЗЕМЛЯ", "Сине-зелёная планета", 0, "planet", 0),
        ShopItem("planet_mars", "МАРС", "Красная планета", 700, "planet", 1),
        ShopItem("planet_jupiter", "ЮПИТЕР", "Газовый гигант", 1500, "planet", 2),
        ShopItem("planet_neptune", "НЕПТУН", "Ледяной гигант", 2600, "planet", 3),
        ShopItem("planet_venus", "ВЕНЕРА", "Раскалённый мир", 3400, "planet", 4),
        ShopItem("planet_saturn", "САТУРН", "Кольцевая планета", 4200, "planet", 5),
        ShopItem("planet_moon", "ЛУНА", "Каменный спутник", 5000, "planet", 6),
        ShopItem("planet_exo", "ЭКЗО", "Неизведанный мир", 6000, "planet", 7),
        ShopItem("upg_shield", "РЕЗЕРВНЫЙ ЩИТ", "Щит в начале каждого забега", 800, "upgrade", 1),
        ShopItem("upg_time", "ХРОНОМЕТР", "+8 секунд в дзен и спринте", 1000, "upgrade", 1),
        ShopItem("upg_magnet", "ПОСТОЯННЫЙ МАГНИТ", "Сферы притягиваются слабее, но всегда", 1500, "upgrade", 1),
        ShopItem("upg_slow", "УДЛИНЁННОЕ ЗАМЕДЛЕНИЕ", "Замедление длится дольше", 1800, "upgrade", 1),
        ShopItem("upg_double", "БОНУСНЫЙ МНОЖИТЕЛЬ", "Двойные очки дольше", 2400, "upgrade", 1),
    )

    data class Secret(val id: String, val title: String, val hint: String)

    val secrets = listOf(
        Secret("eye", "НАБЛЮДАТЕЛЬ", "Собери 3 редкие сферы за забег"),
        Secret("dancer", "ТАНЦОР", "25 близких пролётов за 60 секунд"),
        Secret("survivor", "ЖИВУЧИЙ", "Доживи до 20 уровня без щита"),
        Secret("collector", "КОЛЛЕКЦИОНЕР", "Купи все 8 корпусов"),
        Secret("wanderer", "БРОДЯГА", "Побывай во всех секторах"),
        Secret("hermit", "ОТШЕЛЬНИК", "Сыграй 10 забегов в дзен-режиме"),
    )

    data class MissionTemplate(
        val id: String,
        val title: String,
        val field: String,
        val base: Int,
        val reward: Int,
    )

    val missionTemplates = listOf(
        MissionTemplate("orbs", "СОБРАТЬ СФЕР", "orbs", 24, 70),
        MissionTemplate("dodge", "УКЛОНИТЬСЯ", "dodges", 30, 80),
        MissionTemplate("near", "РИСКНУТЬ", "nearMisses", 8, 100),
        MissionTemplate("combo", "ДЕРЖАТЬ КОМБО", "maxCombo", 10, 90),
        MissionTemplate("score", "НАБРАТЬ ОЧКОВ", "score", 600, 130),
        MissionTemplate("boss", "СБИТЬ ЯДРО", "bossKills", 1, 160),
        MissionTemplate("worm", "ВОЙТИ В ПОРТАЛ", "wormholes", 3, 110),
        MissionTemplate("level", "ДОСТИЧЬ УРОВНЯ", "level", 8, 120),
    )

    data class Mission(val id: String, val title: String, val field: String, val goal: Int, val reward: Int)

    /** Три задания дня выбираются детерминированно из UTC-даты. */
    fun missionsFor(date: String): List<Mission> {
        var h = 2166136261.toInt()
        for (ch in date) {
            h = (h xor ch.code) * 16777619
        }
        val seed = h and 0x7fffffff
        val offset = seed % missionTemplates.size
        return (0..2).map { i ->
            val t = missionTemplates[(offset + i * 3) % missionTemplates.size]
            val scale = 1 + ((seed shr (i * 4 + 3)) and 3) * 0.15f
            Mission(t.id, t.title, t.field, maxOf(1, (t.base * scale).toInt()), (t.reward * (1 + ((seed shr (i * 6 + 5)) and 3) * 0.1f)).toInt())
        }
    }

    data class SeasonTier(val xp: Int, val reward: Int)

    val seasonTiers = listOf(
        SeasonTier(120, 90), SeasonTier(400, 160),
        SeasonTier(1000, 300), SeasonTier(2400, 600),
    )
    val xpPerLevel: Int get() = 100

    /** Детерминированный сид для daily-забега и заданий. Одинаков на обеих платформах. */
    fun seedFor(text: String): Int {
        var h = 2166136261L
        for (ch in text) h = ((h xor ch.code.toLong()) * 16777619L) and 0xffffffffL
        return (h and 0x7fffffffL).toInt()
    }
}
