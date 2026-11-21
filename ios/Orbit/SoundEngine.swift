import AVFoundation

/// Plays the synthesized WAV samples bundled in the app.
final class SoundEngine: ObservableObject {

    private var players: [String: AVAudioPlayer] = [:]
    private var ambient: AVAudioPlayer?

    var muted = false {
        didSet {
            ambient?.volume = muted ? 0 : 0.3
            if muted { ambient?.pause() }
        }
    }

    init() {
        let session = AVAudioSession.sharedInstance()
        try? session.setCategory(.ambient, mode: .default, options: [.mixWithOthers])
        try? session.setActive(true)

        for name in ["tap", "dodge", "orb", "powerup", "shield", "crash", "level"] {
            guard let url = Bundle.main.url(forResource: name, withExtension: "wav") else { continue }
            let player = try? AVAudioPlayer(contentsOf: url)
            player?.prepareToPlay()
            players[name] = player
        }

        if let url = Bundle.main.url(forResource: "ambient", withExtension: "wav") {
            ambient = try? AVAudioPlayer(contentsOf: url)
            ambient?.numberOfLoops = -1
            ambient?.volume = 0.3
            ambient?.prepareToPlay()
        }
    }

    func play(_ name: String, rate: Float = 1, volume: Float = 1) {
        guard !muted, let player = players[name] else { return }
        player.enableRate = true
        player.rate = max(0.5, min(2, rate))
        player.volume = volume
        player.currentTime = 0
        player.play()
    }

    func startAmbient() {
        guard !muted else { return }
        ambient?.play()
    }

    func pauseAmbient() {
        ambient?.pause()
    }
}
