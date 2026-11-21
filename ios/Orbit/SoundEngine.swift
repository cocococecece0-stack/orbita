import AVFoundation

/// Низколатентные SFX и восемь отдельных музыкальных треков (реальные WAV из tools/generate_assets.py).
final class SoundEngine: ObservableObject, AVAudioPlayerDelegate {

    private var players: [String: AVAudioPlayer] = [:]
    private var music: AVAudioPlayer?
    private var musicTrack = 0
    private var musicSector = 0
    private var started = false
    private var settings = GameSettings()

    private let sfxNames = [
        "tap", "dodge", "orb", "powerup", "shield", "crash", "level", "storm", "ui",
        "reward", "achievement", "buy", "boss", "boss_hit", "boss_down", "wormhole",
        "flare", "magnet", "phase", "freeze", "revive", "secret", "victory", "defeat",
        "tick", "gameover",
    ]

    init() {
        let session = AVAudioSession.sharedInstance()
        try? session.setCategory(.ambient, mode: .default, options: [.mixWithOthers])
        try? session.setActive(true)

        for name in sfxNames {
            guard let url = Bundle.main.url(forResource: name, withExtension: "wav") else { continue }
            guard let player = try? AVAudioPlayer(contentsOf: url) else { continue }
            player.enableRate = true
            player.prepareToPlay()
            players[name] = player
        }
    }

    func applySettings(_ newSettings: GameSettings) {
        settings = newSettings
        applyMusicVolume()
        if settings.muted { pauseMusic() }
    }

    func play(_ name: String, rate: Float = 1, volume: Float = 1) {
        guard !settings.muted, let player = players[name] else { return }
        let v = min(max(volume * settings.sfx, 0), 1)
        guard v > 0 else { return }
        player.rate = min(max(rate, 0.5), 2)
        player.volume = v
        player.currentTime = 0
        player.play()
    }

    func startMusic(_ sector: Int) {
        musicSector = sector
        guard !settings.muted else { return }
        started = true
        playTrack(trackFor(sector))
    }

    private func trackFor(_ sector: Int) -> Int { sector % 8 }

    private func playTrack(_ index: Int) {
        musicTrack = index % 8
        guard let url = Bundle.main.url(forResource: "music_\(musicTrack)", withExtension: "wav"),
              let player = try? AVAudioPlayer(contentsOf: url) else { return }
        player.delegate = self
        player.numberOfLoops = 0
        player.prepareToPlay()
        music = player
        applyMusicVolume()
        player.play()
    }

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        guard flag, started, !settings.muted, player === music else { return }
        playTrack((musicTrack + 1) % 8)
    }

    func nextSector(_ sector: Int) {
        guard sector != musicSector else { return }
        musicSector = sector
        if started && !settings.muted { playTrack(trackFor(sector)) }
    }

    func pauseMusic() { music?.pause() }
    func resumeMusic() {
        guard !settings.muted, started, let music, !music.isPlaying else { return }
        music.play()
    }
    func stopMusic() { started = false; music?.stop(); music = nil }

    private func applyMusicVolume() {
        let v = settings.muted ? 0 : min(max(settings.music * 0.55, 0), 1)
        music?.volume = v
    }
}
