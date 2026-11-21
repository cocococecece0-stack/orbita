import Foundation

/// Статический контент: режимы, сектора, предметы, достижения. Зеркально совпадает с Android Content.kt.

enum Mode: String, CaseIterable {
    case classic, zen, sprint, survival, daily

    var id: String { rawValue }

    var title: String {
        switch self {
        case .classic: return "КЛАССИКА"
        case .zen: return "ДЗЕН"
        case .sprint: return "СПРИНТ"
        case .survival: return "ВЫЖИВАНИЕ"
        case .daily: return "ДЕЙЛИ"
        }
    }

    /// Короткая подпись для чипов в хабе и таблице рекордов.
    var shortTitle: String {
        switch self {
        case .classic: return "КЛАСС"
        case .zen: return "ДЗЕН"
        case .sprint: return "СПРИНТ"
        case .survival: return "ВЫЖИВ"
        case .daily: return "ДЕЙЛИ"
        }
    }

    var detail: String {
        switch self {
        case .classic: return "Бесконечный полёт. Один промах — конец."
        case .zen: return "90 секунд без астероидов. Собирай сферы и бонусы."
        case .sprint: return "60 секунд на максимальный счёт. Удар стоит 3 секунды."
        case .survival: return "Плотность растёт постоянно. Ошибка — конец."
        case .daily: return "Фиксированный сид дня. Общий вызов для всех."
        }
    }

    var seconds: Int {
        switch self {
        case .zen: return 90
        case .sprint: return 60
        default: return 0
        }
    }

    static func from(_ id: String?) -> Mode { Mode(rawValue: id ?? "") ?? .classic }
}

enum PowerUpKind: String, CaseIterable {
    case shield, slow, double, magnet, phase, freeze, revive

    var id: String { rawValue }

    var title: String {
        switch self {
        case .shield: return "ЩИТ"
        case .slow: return "ЗАМЕДЛЕНИЕ"
        case .double: return "ДВОЙНЫЕ"
        case .magnet: return "МАГНИТ"
        case .phase: return "ФАЗА"
        case .freeze: return "ЗАМОРОЗКА"
        case .revive: return "ВТОРОЙ ШАНС"
        }
    }

    static func from(_ id: String?) -> PowerUpKind { PowerUpKind(rawValue: id ?? "") ?? .shield }
}

enum HazardKind: String, CaseIterable {
    case rock, comet, mine, wormhole, gravity, flare, boss
    static func from(_ id: String?) -> HazardKind { HazardKind(rawValue: id ?? "") ?? .rock }
}

struct SectorDef {
    let id: Int
    let name: String
    let subtitle: String
    let unlockLevel: Int
    let hazards: [HazardKind]
    let density: Float
}

struct AchievementDef {
    let id: String
    let title: String
    let detail: String
    let reward: Int
    let xp: Int
}

/// Мутации забега: игрок выбирает одну из трёх каждые ~18 секунд.
struct Perk {
    let id: String
    let title: String
    let detail: String
    let glyph: String
}

/// Цель забега: видна в брифинге и в HUD, даёт смысл каждому полёту.
struct Objective {
    let title: String
    let target: Int
    let field: String
}

struct ShopItem {
    let id: String
    let title: String
    let detail: String
    let price: Int
    let category: String
    let value: Int
}

struct SecretDef {
    let id: String
    let title: String
    let hint: String
}

struct MissionTemplate {
    let id: String
    let title: String
    let field: String
    let base: Int
    let reward: Int
}

struct Mission {
    let id: String
    let title: String
    let field: String
    let goal: Int
    let reward: Int
}

enum Content {
    static let modes = Mode.allCases

    /// Названия скинов орбиты/планеты — индекс совпадает с save.equippedPlanet.
    static let planetSkins = ["ЗЕМЛЯ", "МАРС", "ЮПИТЕР", "НЕПТУН",
                              "ВЕНЕРА", "САТУРН", "ЛУНА", "ЭКЗО"]

    /// Типы боссов: ядро, рой и импульс. Индекс хранится в Boss.kind.
    static let bossNames = ["ЯДРО", "РОЙ", "ИМПУЛЬС"]

    static let perks = [
        Perk(id: "overclock", title: "ПЕРЕГРУЗ", detail: "+25% очков до конца забега", glyph: "✦"),
        Perk(id: "barrier", title: "БАРЬЕР", detail: "Щит на 14 секунд", glyph: "◆"),
        Perk(id: "magnet", title: "МАГНИТ", detail: "Сферы притягиваются 10 секунд", glyph: "◎"),
        Perk(id: "slowmo", title: "ЗАМЕДЛЕНИЕ", detail: "Угрозы летят медленнее 10 секунд", glyph: "◐"),
        Perk(id: "pulse", title: "ИМПУЛЬС", detail: "Волна резонанса очищает орбиту", glyph: "✺"),
        Perk(id: "ghost", title: "ФАНТОМ", detail: "Неуязвимость на 8 секунд", glyph: "◇"),
        Perk(id: "harvest", title: "УРОЖАЙ", detail: "Сферы дают двойной опыт", glyph: "●"),
        Perk(id: "chronos", title: "ХРОНОС", detail: "+12 секунд, если есть таймер", glyph: "◷"),
    ]

    static func objective(_ mode: Mode, sectorId: Int) -> Objective {
        let tier = sector(sectorId).id
        switch mode {
        case .zen: return Objective(title: "СОБРАТЬ СФЕР", target: 18 + tier * 4, field: "orbs")
        case .sprint: return Objective(title: "НАБРАТЬ ОЧКОВ", target: 1200 + tier * 300, field: "score")
        default: return Objective(title: "НАБРАТЬ ОЧКОВ", target: 600 + tier * 200, field: "score")
        }
    }

    /// Сезонные модификаторы: выбираются детерминированно по номеру сезона.
    static let seasonModifiers: [(String, CGFloat)] = [
        ("СТАНДАРТ", 1.00), ("МЕТЕОРНЫЙ СЕЗОН", 1.18),
        ("ЭНЕРГЕТИЧЕСКИЙ СЕЗОН", 1.10), ("ТИХИЙ СЕЗОН", 0.90),
    ]

    static func seasonModifier(_ seasonId: String) -> (String, CGFloat) {
        let n = seasonId.unicodeScalars.reduce(0) { $0 + Int($1.value) }
        return seasonModifiers[((n % seasonModifiers.count) + seasonModifiers.count) % seasonModifiers.count]
    }

    static let sectors = [
        SectorDef(id: 0, name: "ОРБИТА-01", subtitle: "Учебный сектор", unlockLevel: 1,
                  hazards: [.rock], density: 0.75),
        SectorDef(id: 1, name: "ПЕПЕЛ", subtitle: "Горящий пояс", unlockLevel: 2,
                  hazards: [.rock, .comet], density: 0.90),
        SectorDef(id: 2, name: "НЕОНОВЫЙ ПОЯС", subtitle: "Свет и помехи", unlockLevel: 3,
                  hazards: [.rock, .comet, .mine], density: 1.00),
        SectorDef(id: 3, name: "ЛЕДЯНОЕ КОЛЬЦО", subtitle: "Тяжёлые льды", unlockLevel: 5,
                  hazards: [.rock, .gravity], density: 1.05),
        SectorDef(id: 4, name: "СЕРДЦЕ ШТОРМА", subtitle: "Залпы и вспышки", unlockLevel: 7,
                  hazards: [.rock, .comet, .flare], density: 1.15),
        SectorDef(id: 5, name: "ЧЕРВОТОЧИНА", subtitle: "Пространство рвётся", unlockLevel: 9,
                  hazards: [.rock, .wormhole], density: 1.10),
        SectorDef(id: 6, name: "ЯДРО ГИГАНТА", subtitle: "Стража ядра", unlockLevel: 12,
                  hazards: [.rock, .comet, .boss], density: 1.20),
        SectorDef(id: 7, name: "СЕКРЕТ", subtitle: "Скрытый сектор", unlockLevel: 99,
                  hazards: [.rock, .mine, .boss], density: 1.30),
        SectorDef(id: 8, name: "ПОЛЯРНАЯ КОРОНА", subtitle: "Ледяные арки и кометы", unlockLevel: 15,
                  hazards: [.rock, .comet, .flare], density: 1.22),
        SectorDef(id: 9, name: "СИНГУЛЯРНОСТЬ", subtitle: "Гравитация и боссы", unlockLevel: 22,
                  hazards: [.rock, .gravity, .boss], density: 1.28),
    ]

    static func sector(_ id: Int) -> SectorDef { sectors[max(0, min(id, sectors.count - 1))] }

    static let achievements = [
        AchievementDef(id: "first_run", title: "ПЕРВЫЙ ВЫЛЕТ", detail: "Завершить забег", reward: 40, xp: 20),
        AchievementDef(id: "score_500", title: "ПОЛУТЫСЯЧНИК", detail: "Набрать 500 очков за забег", reward: 80, xp: 40),
        AchievementDef(id: "score_1500", title: "МАСТЕР ОРБИТЫ", detail: "Набрать 1500 очков за забег", reward: 160, xp: 80),
        AchievementDef(id: "score_5000", title: "ЛЕГЕНДА", detail: "Набрать 5000 очков за забег", reward: 400, xp: 200),
        AchievementDef(id: "combo_10", title: "ЦЕПНАЯ РЕАКЦИЯ", detail: "Достичь комбо x10", reward: 100, xp: 50),
        AchievementDef(id: "combo_25", title: "НЕУДЕРЖИМЫЙ", detail: "Достичь комбо x25", reward: 220, xp: 110),
        AchievementDef(id: "orbs_50", title: "СОБИРАТЕЛЬ", detail: "50 сфер за забег", reward: 90, xp: 45),
        AchievementDef(id: "near_10", title: "НА ВОЛОСКЕ", detail: "10 близких пролётов за забег", reward: 120, xp: 60),
        AchievementDef(id: "near_30", title: "АКРОБАТ", detail: "30 близких пролётов за забег", reward: 260, xp: 130),
        AchievementDef(id: "level_10", title: "ДОЛГОЛЁТ", detail: "Достичь 10 уровня", reward: 140, xp: 70),
        AchievementDef(id: "level_20", title: "ВЕТЕРАН", detail: "Достичь 20 уровня", reward: 300, xp: 150),
        AchievementDef(id: "boss_1", title: "ОХОТНИК", detail: "Уничтожить ядро босса", reward: 180, xp: 90),
        AchievementDef(id: "boss_5", title: "УБИЙЦА ГИГАНТОВ", detail: "5 ядер за один забег", reward: 480, xp: 240),
        AchievementDef(id: "worm_5", title: "ПУТЕШЕСТВЕННИК", detail: "5 червоточин за забег", reward: 130, xp: 65),
        AchievementDef(id: "all_modes", title: "УНИВЕРСАЛ", detail: "Сыграть во все режимы", reward: 200, xp: 100),
        AchievementDef(id: "sector_7", title: "ТАЙНАЯ ДОРОГА", detail: "Открыть секрет сектор", reward: 500, xp: 250),
        AchievementDef(id: "daily_7", title: "ДИСЦИПЛИНА", detail: "7 дней подряд с заданиями", reward: 350, xp: 175),
        AchievementDef(id: "rich_5000", title: "МАГНАТ", detail: "Накопить 5000 жетонов", reward: 300, xp: 150),
    ]

    static let shop = [
        ShopItem(id: "ship_cyan", title: "КОМЕТА", detail: "Стандартный корпус", price: 0, category: "ship", value: 0),
        ShopItem(id: "ship_rose", title: "МАЯК", detail: "Коралловый корпус", price: 300, category: "ship", value: 1),
        ShopItem(id: "ship_lime", title: "ИМПУЛЬС", detail: "Лаймовый корпус", price: 600, category: "ship", value: 2),
        ShopItem(id: "ship_violet", title: "ПРИЗРАК", detail: "Фиолетовый корпус", price: 900, category: "ship", value: 3),
        ShopItem(id: "ship_amber", title: "СОЛНЦЕ", detail: "Янтарный корпус", price: 1400, category: "ship", value: 4),
        ShopItem(id: "ship_magenta", title: "НОВАЯ", detail: "Пурпурный корпус", price: 2000, category: "ship", value: 5),
        ShopItem(id: "ship_ice", title: "АЙСБЕРГ", detail: "Ледяной корпус", price: 2800, category: "ship", value: 6),
        ShopItem(id: "ship_mint", title: "ХЛОРОФИЛЛ", detail: "Мятный корпус", price: 3600, category: "ship", value: 7),
        ShopItem(id: "trail_spark", title: "ИСКРЫ", detail: "Классический шлейф", price: 0, category: "trail", value: 0),
        ShopItem(id: "trail_smoke", title: "ДЫМ", detail: "Плотный шлейф", price: 450, category: "trail", value: 1),
        ShopItem(id: "trail_rainbow", title: "СПЕКТР", detail: "Радужный шлейф", price: 1200, category: "trail", value: 2),
        ShopItem(id: "trail_wave", title: "ВОЛНА", detail: "Волнистый шлейф", price: 2200, category: "trail", value: 3),
        ShopItem(id: "planet_earth", title: "ЗЕМЛЯ", detail: "Сине-зелёная планета", price: 0, category: "planet", value: 0),
        ShopItem(id: "planet_mars", title: "МАРС", detail: "Красная планета", price: 700, category: "planet", value: 1),
        ShopItem(id: "planet_jupiter", title: "ЮПИТЕР", detail: "Газовый гигант", price: 1500, category: "planet", value: 2),
        ShopItem(id: "planet_neptune", title: "НЕПТУН", detail: "Ледяной гигант", price: 2600, category: "planet", value: 3),
        ShopItem(id: "planet_venus", title: "ВЕНЕРА", detail: "Раскалённый мир", price: 3400, category: "planet", value: 4),
        ShopItem(id: "planet_saturn", title: "САТУРН", detail: "Кольцевая планета", price: 4200, category: "planet", value: 5),
        ShopItem(id: "planet_moon", title: "ЛУНА", detail: "Каменный спутник", price: 5000, category: "planet", value: 6),
        ShopItem(id: "planet_exo", title: "ЭКЗО", detail: "Неизведанный мир", price: 6000, category: "planet", value: 7),
        ShopItem(id: "upg_shield", title: "РЕЗЕРВНЫЙ ЩИТ", detail: "Щит в начале каждого забега", price: 800, category: "upgrade", value: 1),
        ShopItem(id: "upg_time", title: "ХРОНОМЕТР", detail: "+8 секунд в дзен и спринте", price: 1000, category: "upgrade", value: 1),
        ShopItem(id: "upg_magnet", title: "ПОСТОЯННЫЙ МАГНИТ", detail: "Сферы притягиваются всегда", price: 1500, category: "upgrade", value: 1),
        ShopItem(id: "upg_slow", title: "УДЛИНЁННОЕ ЗАМЕДЛЕНИЕ", detail: "Замедление длится дольше", price: 1800, category: "upgrade", value: 1),
        ShopItem(id: "upg_double", title: "БОНУСНЫЙ МНОЖИТЕЛЬ", detail: "Двойные очки дольше", price: 2400, category: "upgrade", value: 1),
    ]

    static let secrets = [
        SecretDef(id: "eye", title: "НАБЛЮДАТЕЛЬ", hint: "Собери 3 редкие сферы за забег"),
        SecretDef(id: "dancer", title: "ТАНЦОР", hint: "25 близких пролётов за 60 секунд"),
        SecretDef(id: "survivor", title: "ЖИВУЧИЙ", hint: "Доживи до 20 уровня без щита"),
        SecretDef(id: "collector", title: "КОЛЛЕКЦИОНЕР", hint: "Купи все 8 корпусов"),
        SecretDef(id: "wanderer", title: "БРОДЯГА", hint: "Побывай во всех секторах"),
        SecretDef(id: "hermit", title: "ОТШЕЛЬНИК", hint: "Сыграй 10 забегов в дзен-режиме"),
    ]

    static let missionTemplates = [
        MissionTemplate(id: "orbs", title: "СОБРАТЬ СФЕР", field: "orbs", base: 24, reward: 70),
        MissionTemplate(id: "dodge", title: "УКЛОНИТЬСЯ", field: "dodges", base: 30, reward: 80),
        MissionTemplate(id: "near", title: "РИСКНУТЬ", field: "nearMisses", base: 8, reward: 100),
        MissionTemplate(id: "combo", title: "ДЕРЖАТЬ КОМБО", field: "maxCombo", base: 10, reward: 90),
        MissionTemplate(id: "score", title: "НАБРАТЬ ОЧКОВ", field: "score", base: 600, reward: 130),
        MissionTemplate(id: "boss", title: "СБИТЬ ЯДРО", field: "bossKills", base: 1, reward: 160),
        MissionTemplate(id: "worm", title: "ВОЙТИ В ПОРТАЛ", field: "wormholes", base: 3, reward: 110),
        MissionTemplate(id: "level", title: "ДОСТИЧЬ УРОВНЯ", field: "level", base: 8, reward: 120),
    ]

    static func missionsFor(_ date: String) -> [Mission] {
        var h: Int64 = 2166136261
        for scalar in date.unicodeScalars {
            h = (h ^ Int64(scalar.value)) &* 16777619
        }
        let seed = Int(h & 0x7fffffff)
        let offset = seed % missionTemplates.count
        return (0..<3).map { i in
            let t = missionTemplates[(offset + i * 3) % missionTemplates.count]
            let scale = 1 + Float((seed >> (i * 4 + 3)) & 3) * 0.15
            let bonus = 1 + Float((seed >> (i * 6 + 5)) & 3) * 0.1
            return Mission(
                id: t.id,
                title: t.title,
                field: t.field,
                goal: max(1, Int(Float(t.base) * scale)),
                reward: Int(Float(t.reward) * bonus)
            )
        }
    }

    static let seasonTiers: [(xp: Int, reward: Int)] = [(120, 90), (400, 160), (1000, 300), (2400, 600)]
    static let xpPerLevel = 100

    /// Детерминированный сид для daily-забега. Зеркально совпадает с Android Content.seedFor.
    static func seedFor(_ text: String) -> Int {
        var h: UInt32 = 2166136261
        for scalar in text.unicodeScalars {
            h = (h ^ scalar.value) &* 16777619
        }
        return Int(h & 0x7fffffff)
    }
}
