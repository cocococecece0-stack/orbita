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

    static let maxValue = 100_000_000

    var playerLevel: Int { 1 + xp / Content.xpPerLevel }

    /** Гарантирует, что битое сохранение не сломает экономику и шкалы. */
    mutating func sanitize() {
        credits = credits.clamped(to: 0...SaveData.maxValue)
        xp = xp.clamped(to: 0...SaveData.maxValue)
        seasonXp = seasonXp.clamped(to: 0...SaveData.maxValue)
        dailyStreak = dailyStreak.clamped(to: 0...3650)
        zenRuns = zenRuns.clamped(to: 0...SaveData.maxValue)
        selectedSector = selectedSector.clamped(to: 0...7)
        equippedShip = equippedShip.clamped(to: 0...7)
        equippedTrail = equippedTrail.clamped(to: 0...7)
        equippedPlanet = equippedPlanet.clamped(to: 0...7)
        if !(-1...7).contains(mutedTrack) { mutedTrack = -1 }
        bestByMode = bestByMode.mapValues { $0.clamped(to: 0...SaveData.maxValue) }
        topByMode = topByMode.mapValues { Array($0.map { $0.clamped(to: 0...SaveData.maxValue) }.sorted(by: >).prefix(10)) }
        history = history.map {
            var r = $0
            r.score = r.score.clamped(to: 0...SaveData.maxValue)
            r.level = r.level.clamped(to: 1...10000)
            r.orbs = r.orbs.clamped(to: 0...SaveData.maxValue)
            r.maxCombo = r.maxCombo.clamped(to: 0...SaveData.maxValue)
            r.nearMisses = r.nearMisses.clamped(to: 0...SaveData.maxValue)
            r.dodges = r.dodges.clamped(to: 0...SaveData.maxValue)
            r.bossKills = r.bossKills.clamped(to: 0...SaveData.maxValue)
            r.wormholes = r.wormholes.clamped(to: 0...SaveData.maxValue)
            r.rareOrbs = r.rareOrbs.clamped(to: 0...SaveData.maxValue)
            r.seconds = r.seconds.clamped(to: 0...100000)
            r.sector = r.sector.clamped(to: 0...7)
            return r
        }
        if dailyProgress.count != 3 { dailyProgress = [0, 0, 0] }
        if dailyClaimed.count != 3 { dailyClaimed = [false, false, false] }
        if seasonClaimed.count != 4 { seasonClaimed = [false, false, false, false] }
        dailyProgress = dailyProgress.map { $0.clamped(to: 0...SaveData.maxValue) }
        upgrades = upgrades.mapValues { $0.clamped(to: 0...1) }
        visitedSectors.removeAll { !(0...7).contains($0) }
        if !Content.modes.contains(where: { $0.id == selectedMode }) { selectedMode = Mode.classic.id }
        settings.music = settings.music.clamped(to: 0...1)
        settings.sfx = settings.sfx.clamped(to: 0...1)
    }
}

private extension Comparable {
    func clamped(to range: ClosedRange<Self>) -> Self {
        min(max(self, range.lowerBound), range.upperBound)
    }
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
    var creditsBefore = 0
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
        guard let raw, let obj = try? JSONSerialization.jsonObject(with: raw) as? [String: Any] else { return nil }
        var valid = true
        // Числа читаем через NSNumber/Int64: битые значения отклоняют весь файл,
        // а не подставляют дефолт в середине экономики.
        func read(_ value: Any?, _ fallback: Int, _ min: Int = 0, _ max: Int = SaveData.maxValue) -> Int {
            guard let value else { return fallback }
            guard let n = value as? NSNumber else { valid = false; return fallback }
            let v = n.int64Value
            guard v >= Int64(min), v <= Int64(max) else { valid = false; return fallback }
            return Int(v)
        }
        func field(_ o: [String: Any], _ key: String, _ fallback: Int = 0, _ min: Int = 0, _ max: Int = SaveData.maxValue) -> Int {
            read(o[key], fallback, min, max)
        }
        guard field(obj, "version", 0, 3, 3) == 3 else { return nil }
        let today = computeToday()
        let season = computeSeasonId()
        var d = SaveData()
        d.credits = field(obj, "credits")
        d.xp = field(obj, "xp")
        if let m = obj["bestByMode"] as? [String: Any] {
            d.bestByMode = m.mapValues { read($0, 0) }
        }
        if let m = obj["topByMode"] as? [String: Any] {
            d.topByMode = m.compactMapValues { $0 as? [Any] }
                .mapValues { Array($0.map { read($0, 0) }.sorted(by: >).prefix(10)) }
        }
        if let h = obj["history"] as? [[String: Any]] {
            d.history = h.map { r in
                let stamp = (r["timestamp"] as? NSNumber)?.int64Value ?? 0
                return RunRecord(
                    score: field(r, "score"),
                    mode: r["mode"] as? String ?? "classic",
                    sector: field(r, "sector", 0, 0, 7),
                    level: field(r, "level", 1, 1, 10000),
                    orbs: field(r, "orbs"),
                    maxCombo: field(r, "maxCombo"),
                    nearMisses: field(r, "nearMisses"),
                    dodges: field(r, "dodges"),
                    bossKills: field(r, "bossKills"),
                    wormholes: field(r, "wormholes"),
                    rareOrbs: field(r, "rareOrbs"),
                    seconds: field(r, "seconds", 0, 0, 100000),
                    timestamp: stamp
                )
            }
        }
        d.achievements = obj["achievements"] as? [String] ?? []
        d.dailyDate = obj["dailyDate"] as? String ?? today
        d.dailyProgress = ints(obj["dailyProgress"], 3)
        d.dailyClaimed = bools(obj["dailyClaimed"], 3)
        d.dailyStreak = field(obj, "dailyStreak", 0, 0, 3650)
        d.lastPlayedDay = obj["lastPlayedDay"] as? String ?? ""
        d.seasonId = obj["seasonId"] as? String ?? season
        d.seasonXp = field(obj, "seasonXp")
        d.seasonClaimed = bools(obj["seasonClaimed"], 4)
        if let inv = obj["inventory"] as? [String], !inv.isEmpty { d.inventory = inv }
        d.equippedShip = field(obj, "equippedShip", 0, 0, 7)
        d.equippedTrail = field(obj, "equippedTrail", 0, 0, 7)
        d.equippedPlanet = field(obj, "equippedPlanet", 0, 0, 7)
        if let up = obj["upgrades"] as? [String: Any] { d.upgrades = up.mapValues { read($0, 0, 0, 1) } }
        d.secrets = obj["secrets"] as? [String] ?? []
        d.visitedSectors = obj["visitedSectors"] as? [Int] ?? []
        d.modesPlayed = obj["modesPlayed"] as? [String] ?? []
        d.zenRuns = field(obj, "zenRuns")
        d.selectedMode = obj["selectedMode"] as? String ?? "classic"
        d.selectedSector = field(obj, "selectedSector", 0, 0, 7)
        d.mutedTrack = field(obj, "mutedTrack", -1, -1, 7)
        d.tutorialSeen = obj["tutorialSeen"] as? Bool ?? false
        if let s = obj["settings"] as? [String: Any] {
            d.settings.music = Float(s["music"] as? Double ?? 0.7).clamped(to: 0...1)
            d.settings.sfx = Float(s["sfx"] as? Double ?? 0.9).clamped(to: 0...1)
            d.settings.muted = s["muted"] as? Bool ?? false
            d.settings.haptics = s["haptics"] as? Bool ?? true
            d.settings.reducedMotion = s["reducedMotion"] as? Bool ?? false
            d.settings.colorblind = s["colorblind"] as? Bool ?? false
        }
        guard valid else { return nil }
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
        d.sanitize()
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
        var d = d
        d.sanitize()
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

    /** Сложение с насыщением: экономика никогда не уходит в минус/переполнение. */
    private func satAdd(_ a: Int, _ b: Int) -> Int {
        min(SaveData.maxValue, max(0, a) + max(0, b))
    }

    private func clampRecord(_ r: RunRecord) -> RunRecord {
        var r = r
        r.score = r.score.clamped(to: 0...SaveData.maxValue)
        r.sector = r.sector.clamped(to: 0...7)
        r.level = r.level.clamped(to: 1...10000)
        r.orbs = r.orbs.clamped(to: 0...SaveData.maxValue)
        r.maxCombo = r.maxCombo.clamped(to: 0...SaveData.maxValue)
        r.nearMisses = r.nearMisses.clamped(to: 0...SaveData.maxValue)
        r.dodges = r.dodges.clamped(to: 0...SaveData.maxValue)
        r.bossKills = r.bossKills.clamped(to: 0...SaveData.maxValue)
        r.wormholes = r.wormholes.clamped(to: 0...SaveData.maxValue)
        r.rareOrbs = r.rareOrbs.clamped(to: 0...SaveData.maxValue)
        r.seconds = r.seconds.clamped(to: 0...100000)
        return r
    }

    func submitRun(_ record: RunRecord, achieved: [String], foundSecrets: [String]) -> RewardSummary {
        var d = data
        d.sanitize()
        var rr = clampRecord(record)
        if !Content.modes.contains(where: { $0.id == rr.mode }) { rr.mode = Mode.classic.id }
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
        let before = d.credits
        let isBest = rr.score > (d.bestByMode[rr.mode] ?? 0)
        if isBest { d.bestByMode[rr.mode] = rr.score }
        var top = (d.topByMode[rr.mode] ?? []) + [rr.score]
        top = Array(top.sorted(by: >).prefix(10))
        d.topByMode[rr.mode] = top
        d.history.insert(rr, at: 0)
        if d.history.count > 25 { d.history.removeLast(d.history.count - 25) }

        var credits = satAdd(rr.score / 10, satAdd(rr.orbs, rr.rareOrbs * 20))
        var xp = rr.score / 20
        let levelBefore = d.playerLevel

        if !d.modesPlayed.contains(rr.mode) { d.modesPlayed.append(rr.mode) }
        if !d.visitedSectors.contains(rr.sector) { d.visitedSectors.append(rr.sector) }
        if rr.mode == Mode.zen.id { d.zenRuns = satAdd(d.zenRuns, 1) }
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(secondsFromGMT: 0) ?? .current
        let yesterday = cal.date(byAdding: .day, value: -1, to: Date()) ?? Date()
        let f = DateFormatter(); f.dateFormat = "yyyy-MM-dd"; f.timeZone = TimeZone(secondsFromGMT: 0)
        d.dailyStreak = d.lastPlayedDay == day ? d.dailyStreak
            : (d.lastPlayedDay == f.string(from: yesterday) ? satAdd(d.dailyStreak, 1) : 1)
        d.lastPlayedDay = day
        d.seasonXp = satAdd(d.seasonXp, rr.score / 25)

        var summary = RewardSummary()
        summary.creditsBefore = before
        for a in Content.achievements where !d.achievements.contains(a.id) && achieved.contains(a.id) {
            d.achievements.append(a.id)
            credits = satAdd(credits, a.reward)
            xp = satAdd(xp, a.xp)
            summary.achievements.append(a.title)
        }

        let defs = Content.missionsFor(day)
        let values: [String: Int] = [
            "orbs": rr.orbs, "dodges": rr.dodges, "nearMisses": rr.nearMisses,
            "maxCombo": rr.maxCombo, "score": rr.score, "bossKills": rr.bossKills,
            "wormholes": rr.wormholes, "level": rr.level,
        ]
        for (i, m) in defs.enumerated() {
            let v = values[m.field] ?? 0
            d.dailyProgress[i] = max(d.dailyProgress[i], v)
            if d.dailyProgress[i] >= m.goal && !d.dailyClaimed[i] {
                d.dailyClaimed[i] = true
                credits = satAdd(credits, m.reward)
                xp = satAdd(xp, m.reward / 3)
                summary.missions.append("\(m.title) +\(m.reward)")
            }
        }

        for id in foundSecrets where !d.secrets.contains(id) {
            d.secrets.append(id)
            guard let def = Content.secrets.first(where: { $0.id == id }) else { continue }
            credits = satAdd(credits, 200)
            xp = satAdd(xp, 100)
            summary.secrets.append(def.title)
            if id == "collector" && !d.visitedSectors.contains(7) { d.visitedSectors.append(7) }
        }
        if d.secrets.contains("eye") || d.achievements.contains("boss_1") {
            if !d.visitedSectors.contains(7) { d.visitedSectors.append(7) }
        }

        for (i, tier) in Content.seasonTiers.enumerated() where d.seasonXp >= tier.xp && !d.seasonClaimed[i] {
            d.seasonClaimed[i] = true
            credits = satAdd(credits, tier.reward)
            summary.seasonTiers.append(i)
        }
        if satAdd(d.credits, credits) >= 5000 && !d.achievements.contains("rich_5000") {
            d.achievements.append("rich_5000")
            credits = satAdd(credits, 300)
            summary.achievements.append("МАГНАТ")
        }
        d.credits = satAdd(d.credits, credits)
        d.xp = satAdd(d.xp, xp)
        summary.credits = credits
        summary.xp = xp
        summary.levelUps = max(0, d.playerLevel - levelBefore)
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
        data.credits = satAdd(data.credits, defs[index].reward)
        data.xp = satAdd(data.xp, defs[index].reward / 2)
        touch()
        return true
    }

    func buy(_ itemId: String) -> Bool {
        guard let item = Content.shop.first(where: { $0.id == itemId }),
              !data.inventory.contains(itemId), data.credits >= item.price else { return false }
        data.credits = max(0, data.credits - item.price)
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
