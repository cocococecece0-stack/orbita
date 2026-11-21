import Foundation

/** Полностью локальное сохранение JSON v3: валидация, резервная копия, экспорт/импорт кодом. */
struct RunRecord {
    var score: Int = 0
    var mode: String = "classic"
    var sector: Int = 0
    var level: Int = 1
    var orbs: Int = 0
    var maxCombo: Int = 0
    var nearMisses: Int = 0
    var dodges: Int = 0
    var bossKills: Int = 0
    var wormholes: Int = 0
    var rareOrbs: Int = 0
    var seconds: Int = 0
    var timestamp: Int64 = 0
}

struct GameSettings {
    var music: Float = 0.7
    var sfx: Float = 0.9
    var muted = false
    var haptics = true
    var reducedMotion = false
    var colorblind = false
}

struct SaveData {
    var version = 3
    var credits = 0
    var xp = 0
    var bestByMode: [String: Int] = [:]
    var topByMode: [String: [Int]] = [:]
    var history: [RunRecord] = []
    var achievements: [String] = []
    var dailyDate = ""
    var dailyProgress = [0, 0, 0]
    var dailyClaimed = [false, false, false]
    var dailyStreak = 0
    var lastPlayedDay = ""
    var seasonId = ""
    var seasonXp = 0
    var seasonClaimed = [false, false, false, false]
    var inventory = ["ship_cyan", "trail_spark", "planet_nebula"]
    var equippedShip = 0
    var equippedTrail = 0
    var equippedPlanet = 0
    var upgrades: [String: Int] = [:]
    var secrets: [String] = []
    var visitedSectors: [Int] = []
    var modesPlayed: [String] = []
    var zenRuns = 0
    var selectedMode = "classic"
    var selectedSector = 0
    var mutedTrack = -1
    var tutorialSeen = false
    var settings = GameSettings()

    var playerLevel: Int { 1 + xp / Content.xpPerLevel }
}

struct RewardSummary {
    var credits = 0
    var xp = 0
    var achievements: [String] = []
    var missions: [String] = []
    var secrets: [String] = []
    var seasonTiers: [Int] = []
    var levelUps = 0
    var newBest = false
}

final class SaveStore: ObservableObject {
    private let mainURL: URL
    private let backupURL: URL
    @Published private(set) var data: SaveData

    init() {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        mainURL = docs.appendingPathComponent("orbit_save_v3.json")
        backupURL = docs.appendingPathComponent("orbit_save_v3.bak")
        data = SaveStore.decode(SaveStore.read(mainURL)) ?? SaveStore.decode(SaveStore.read(backupURL))
            ?? SaveStore.fresh()
    }

    func today() -> String { SaveStore.computeToday() }

    func seasonId() -> String { SaveStore.computeSeasonId() }

    static func computeToday() -> String {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd"
        f.timeZone = TimeZone(secondsFromGMT: 0) ?? .current
        return f.string(from: Date())
    }

    static func computeSeasonId() -> String {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(secondsFromGMT: 0) ?? .current
        let epoch = cal.date(from: DateComponents(year: 2026, month: 1, day: 5))!
        let days = cal.dateComponents([.day], from: epoch, to: Date()).day ?? 0
        return String(format: "S%02d", max(1, days / 56 + 1))
    }

    private static func fresh() -> SaveData {
        var d = SaveData()
        d.dailyDate = computeToday()
        d.seasonId = computeSeasonId()
        return d
    }

    private static func read(_ url: URL) -> Data? {
        try? Data(contentsOf: url)
    }

    private static func decode(_ raw: Data?) -> SaveData? {
        guard let raw, let obj = try? JSONSerialization.jsonObject(with: raw) as? [String: Any],
              obj["version"] as? Int == 3 else { return nil }
        let today = computeToday()
        let season = computeSeasonId()
        var d = SaveData()
        d.credits = max(0, obj["credits"] as? Int ?? 0)
        d.xp = max(0, obj["xp"] as? Int ?? 0)
        if let m = obj["bestByMode"] as? [String: Int] { d.bestByMode = m }
        if let m = obj["topByMode"] as? [String: [Int]] {
            d.topByMode = m.mapValues { Array($0.filter { $0 >= 0 }.sorted(by: >).prefix(10)) }
        }
        if let h = obj["history"] as? [[String: Any]] {
            d.history = h.compactMap { r in
                RunRecord(
                    score: r["score"] as? Int ?? 0,
                    mode: r["mode"] as? String ?? "classic",
                    sector: r["sector"] as? Int ?? 0,
                    level: r["level"] as? Int ?? 1,
                    orbs: r["orbs"] as? Int ?? 0,
                    maxCombo: r["maxCombo"] as? Int ?? 0,
                    nearMisses: r["nearMisses"] as? Int ?? 0,
                    dodges: r["dodges"] as? Int ?? 0,
                    bossKills: r["bossKills"] as? Int ?? 0,
                    wormholes: r["wormholes"] as? Int ?? 0,
                    rareOrbs: r["rareOrbs"] as? Int ?? 0,
                    seconds: r["seconds"] as? Int ?? 0,
                    timestamp: r["timestamp"] as? Int64 ?? 0
                )
            }
        }
        d.achievements = obj["achievements"] as? [String] ?? []
        d.dailyDate = obj["dailyDate"] as? String ?? today
        d.dailyProgress = ints(obj["dailyProgress"], 3)
        d.dailyClaimed = bools(obj["dailyClaimed"], 3)
        d.dailyStreak = obj["dailyStreak"] as? Int ?? 0
        d.lastPlayedDay = obj["lastPlayedDay"] as? String ?? ""
        d.seasonId = obj["seasonId"] as? String ?? season
        d.seasonXp = obj["seasonXp"] as? Int ?? 0
        d.seasonClaimed = bools(obj["seasonClaimed"], 4)
        if let inv = obj["inventory"] as? [String], !inv.isEmpty { d.inventory = inv }
        d.equippedShip = obj["equippedShip"] as? Int ?? 0
        d.equippedTrail = obj["equippedTrail"] as? Int ?? 0
        d.equippedPlanet = obj["equippedPlanet"] as? Int ?? 0
        if let up = obj["upgrades"] as? [String: Int] { d.upgrades = up }
        d.secrets = obj["secrets"] as? [String] ?? []
        d.visitedSectors = obj["visitedSectors"] as? [Int] ?? []
        d.modesPlayed = obj["modesPlayed"] as? [String] ?? []
        d.zenRuns = obj["zenRuns"] as? Int ?? 0
        d.selectedMode = obj["selectedMode"] as? String ?? "classic"
        d.selectedSector = obj["selectedSector"] as? Int ?? 0
        d.mutedTrack = obj["mutedTrack"] as? Int ?? -1
        d.tutorialSeen = obj["tutorialSeen"] as? Bool ?? false
        if let s = obj["settings"] as? [String: Any] {
            d.settings.music = Float(s["music"] as? Double ?? 0.7)
            d.settings.sfx = Float(s["sfx"] as? Double ?? 0.9)
            d.settings.muted = s["muted"] as? Bool ?? false
            d.settings.haptics = s["haptics"] as? Bool ?? true
            d.settings.reducedMotion = s["reducedMotion"] as? Bool ?? false
            d.settings.colorblind = s["colorblind"] as? Bool ?? false
        }
        if d.dailyDate != today {
            d.dailyDate = today
            d.dailyProgress = [0, 0, 0]
            d.dailyClaimed = [false, false, false]
        }
        if d.seasonId != season {
            d.seasonId = season
            d.seasonXp = 0
            d.seasonClaimed = [false, false, false, false]
        }
        return d
    }

    private static func ints(_ value: Any?, _ size: Int) -> [Int] {
        guard let a = value as? [Int] else { return Array(repeating: 0, count: size) }
        var out = Array(repeating: 0, count: size)
        for i in 0..<min(size, a.count) { out[i] = max(0, a[i]) }
        return out
    }

    private static func bools(_ value: Any?, _ size: Int) -> [Bool] {
        guard let a = value as? [Bool] else { return Array(repeating: false, count: size) }
        var out = Array(repeating: false, count: size)
        for i in 0..<min(size, a.count) { out[i] = a[i] }
        return out
    }

    func encode(_ value: SaveData? = nil) -> Data {
        let d = value ?? data
        var hist: [[String: Any]] = []
        for r in Array(d.history.prefix(25)) {
            hist.append([
                "score": r.score, "mode": r.mode, "sector": r.sector, "level": r.level,
                "orbs": r.orbs, "maxCombo": r.maxCombo, "nearMisses": r.nearMisses,
                "dodges": r.dodges, "bossKills": r.bossKills, "wormholes": r.wormholes,
                "rareOrbs": r.rareOrbs, "seconds": r.seconds, "timestamp": r.timestamp,
            ])
        }
        let obj: [String: Any] = [
            "version": 3,
            "credits": d.credits,
            "xp": d.xp,
            "bestByMode": d.bestByMode,
            "topByMode": d.topByMode,
            "history": hist,
            "achievements": d.achievements,
            "dailyDate": d.dailyDate,
            "dailyProgress": d.dailyProgress,
            "dailyClaimed": d.dailyClaimed,
            "dailyStreak": d.dailyStreak,
            "lastPlayedDay": d.lastPlayedDay,
            "seasonId": d.seasonId,
            "seasonXp": d.seasonXp,
            "seasonClaimed": d.seasonClaimed,
            "inventory": d.inventory,
            "equippedShip": d.equippedShip,
            "equippedTrail": d.equippedTrail,
            "equippedPlanet": d.equippedPlanet,
            "upgrades": d.upgrades,
            "secrets": d.secrets,
            "visitedSectors": d.visitedSectors,
            "modesPlayed": d.modesPlayed,
            "zenRuns": d.zenRuns,
            "selectedMode": d.selectedMode,
            "selectedSector": d.selectedSector,
            "mutedTrack": d.mutedTrack,
            "tutorialSeen": d.tutorialSeen,
            "settings": [
                "music": Double(d.settings.music), "sfx": Double(d.settings.sfx),
                "muted": d.settings.muted, "haptics": d.settings.haptics,
                "reducedMotion": d.settings.reducedMotion, "colorblind": d.settings.colorblind,
            ],
        ]
        return (try? JSONSerialization.data(withJSONObject: obj, options: [.sortedKeys])) ?? Data()
    }

    private func writeAll(_ d: SaveData) {
        do {
            if FileManager.default.fileExists(atPath: mainURL.path) {
                try? FileManager.default.removeItem(at: backupURL)
                try FileManager.default.copyItem(at: mainURL, to: backupURL)
            }
            try encode(d).write(to: mainURL, options: .atomic)
        } catch {
            // Прогресс в памяти остаётся актуальным.
        }
    }

    func save() { writeAll(data) }

    func submitRun(_ record: RunRecord, achieved: [String], foundSecrets: [String]) -> RewardSummary {
        var d = data
        let day = today()
        if d.dailyDate != day {
            d.dailyDate = day
            d.dailyProgress = [0, 0, 0]
            d.dailyClaimed = [false, false, false]
        }
        if d.seasonId != seasonId() {
            d.seasonId = seasonId()
            d.seasonXp = 0
            d.seasonClaimed = [false, false, false, false]
        }
        let isBest = record.score > (d.bestByMode[record.mode] ?? 0)
        if isBest { d.bestByMode[record.mode] = record.score }
        var top = (d.topByMode[record.mode] ?? []) + [record.score]
        top = Array(top.sorted(by: >).prefix(10))
        d.topByMode[record.mode] = top
        d.history.insert(record, at: 0)
        if d.history.count > 25 { d.history.removeLast(d.history.count - 25) }

        var credits = record.score / 10 + record.orbs + record.rareOrbs * 20
        var xp = record.score / 20
        let levelBefore = d.playerLevel
        d.xp += xp

        if !d.modesPlayed.contains(record.mode) { d.modesPlayed.append(record.mode) }
        if !d.visitedSectors.contains(record.sector) { d.visitedSectors.append(record.sector) }
        if record.mode == Mode.zen.id { d.zenRuns += 1 }
        let yesterday = Calendar.current.date(byAdding: .day, value: -1, to: Date())!
        let f = DateFormatter(); f.dateFormat = "yyyy-MM-dd"; f.timeZone = TimeZone(secondsFromGMT: 0)
        d.dailyStreak = d.lastPlayedDay == day ? d.dailyStreak
            : (d.lastPlayedDay == f.string(from: yesterday) ? d.dailyStreak + 1 : 1)
        d.lastPlayedDay = day
        d.seasonXp += record.score / 25

        var summary = RewardSummary()
        for a in Content.achievements where !d.achievements.contains(a.id) && achieved.contains(a.id) {
            d.achievements.append(a.id)
            credits += a.reward
            xp += a.xp
            d.xp += a.xp
            summary.achievements.append(a.title)
        }

        let defs = Content.missionsFor(day)
        let values: [String: Int] = [
            "orbs": record.orbs, "dodges": record.dodges, "nearMisses": record.nearMisses,
            "maxCombo": record.maxCombo, "score": record.score, "bossKills": record.bossKills,
            "wormholes": record.wormholes, "level": record.level,
        ]
        for (i, m) in defs.enumerated() {
            let v = values[m.field] ?? 0
            d.dailyProgress[i] = max(d.dailyProgress[i], v)
            if d.dailyProgress[i] >= m.goal && !d.dailyClaimed[i] {
                d.dailyClaimed[i] = true
                credits += m.reward
                xp += m.reward / 3
                d.xp += m.reward / 3
                summary.missions.append("\(m.title) +\(m.reward)")
            }
        }

        for id in foundSecrets where !d.secrets.contains(id) {
            d.secrets.append(id)
            guard let def = Content.secrets.first(where: { $0.id == id }) else { continue }
            credits += 200
            d.xp += 100
            summary.secrets.append(def.title)
            if id == "collector" && !d.visitedSectors.contains(7) { d.visitedSectors.append(7) }
        }
        if d.secrets.contains("eye") || d.achievements.contains("boss_1") {
            if !d.visitedSectors.contains(7) { d.visitedSectors.append(7) }
        }

        for (i, tier) in Content.seasonTiers.enumerated() where d.seasonXp >= tier.xp && !d.seasonClaimed[i] {
            d.seasonClaimed[i] = true
            credits += tier.reward
            summary.seasonTiers.append(i)
        }
        if d.credits >= 5000 && !d.achievements.contains("rich_5000") {
            d.achievements.append("rich_5000")
            credits += 300
            summary.achievements.append("МАГНАТ")
        }
        d.credits += credits
        summary.credits = credits
        summary.xp = xp
        summary.levelUps = d.playerLevel - levelBefore
        summary.newBest = isBest
        data = d
        writeAll(d)
        return summary
    }

    func claimMission(_ index: Int) -> Bool {
        let defs = Content.missionsFor(data.dailyDate)
        guard index < defs.count, !data.dailyClaimed[index],
              data.dailyProgress[index] >= defs[index].goal else { return false }
        data.dailyClaimed[index] = true
        data.credits += defs[index].reward
        data.xp += defs[index].reward / 2
        touch()
        return true
    }

    func buy(_ itemId: String) -> Bool {
        guard let item = Content.shop.first(where: { $0.id == itemId }),
              !data.inventory.contains(itemId), data.credits >= item.price else { return false }
        data.credits -= item.price
        data.inventory.append(itemId)
        if item.category == "upgrade" { data.upgrades[itemId] = item.value }
        touch()
        return true
    }

    func equip(_ itemId: String) -> Bool {
        guard let item = Content.shop.first(where: { $0.id == itemId }),
              data.inventory.contains(itemId) else { return false }
        switch item.category {
        case "ship": data.equippedShip = item.value
        case "trail": data.equippedTrail = item.value
        case "planet": data.equippedPlanet = item.value
        default: return false
        }
        touch()
        return true
    }

    func hasUpgrade(_ id: String) -> Bool { (data.upgrades[id] ?? 0) > 0 }

    func touch() {
        objectWillChange.send()
        save()
    }

    func selectMode(_ id: String) {
        guard Content.modes.contains(where: { $0.id == id }) else { return }
        data.selectedMode = id
        touch()
    }

    func selectSector(_ id: Int) {
        guard Content.sectors.contains(where: { $0.id == id }) else { return }
        data.selectedSector = id
        touch()
    }

    func updateSettings(_ block: (inout GameSettings) -> Void) {
        block(&data.settings)
        touch()
    }

    func upgrades() -> [String: Bool] {
        [
            "upg_shield": hasUpgrade("upg_shield"),
            "upg_time": hasUpgrade("upg_time"),
            "upg_magnet": hasUpgrade("upg_magnet"),
            "upg_slow": hasUpgrade("upg_slow"),
            "upg_double": hasUpgrade("upg_double"),
        ]
    }

    func exportCode() -> String {
        let body = encode()
        var crc = crc32(body)
        var payload = body
        payload.append(contentsOf: [
            UInt8((crc >> 24) & 0xFF), UInt8((crc >> 16) & 0xFF),
            UInt8((crc >> 8) & 0xFF), UInt8(crc & 0xFF),
        ])
        return "ORB1-" + payload.base64EncodedString(options: [.endLineWithLineFeed])
            .replacingOccurrences(of: "\n", with: "")
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
    }

    func importCode(_ code: String) -> Bool {
        var raw = code.trimmingCharacters(in: .whitespacesAndNewlines)
        if raw.hasPrefix("ORB1-") { raw.removeFirst(5) }
        raw = raw.replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        guard let bytes = Data(base64Encoded: raw), bytes.count > 4 else { return false }
        let body = bytes.prefix(bytes.count - 4)
        let expected = (UInt32(bytes[bytes.count - 4]) << 24) | (UInt32(bytes[bytes.count - 3]) << 16)
            | (UInt32(bytes[bytes.count - 2]) << 8) | UInt32(bytes[bytes.count - 1])
        guard crc32(body) == expected, let parsed = SaveStore.decode(body) else { return false }
        data = parsed
        writeAll(parsed)
        return true
    }

    private func crc32(_ data: Data) -> UInt32 {
        var crc: UInt32 = 0xFFFFFFFF
        for byte in data {
            crc ^= UInt32(byte)
            for _ in 0..<8 {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1
            }
        }
        return ~crc
    }

    func reset() {
        data = SaveData()
        data.dailyDate = today()
        data.seasonId = seasonId()
        writeAll(data)
    }
}
