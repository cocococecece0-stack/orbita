import SwiftUI

/// Все изображения загружаются один раз и переиспользуются каждым кадром.
final class GameArt {
    let backgrounds: [UIImage]
    let planets: [UIImage]
    let ships: [UIImage]
    let asteroids: [UIImage]
    let comets: [UIImage]
    let powers: [UIImage]
    let orbs: [UIImage]
    let glow: UIImage?
    let ring: UIImage?
    let burst: UIImage?

    init() {
        func set(_ prefix: String, _ count: Int) -> [UIImage] {
            (0..<count).compactMap { UIImage(named: "\(prefix)_\($0)") }
        }
        backgrounds = set("bg", 8)
        planets = set("planet", 8)
        ships = set("ship", 8)
        asteroids = set("ast", 8)
        comets = set("comet", 3)
        powers = set("power", 7)
        orbs = set("orb", 3)
        glow = UIImage(named: "fx_glow")
        ring = UIImage(named: "fx_ring")
        burst = UIImage(named: "fx_burst")
    }
}

private extension GraphicsContext {
    /// UIImage не рисуется GraphicsContext напрямую — оборачиваем в SwiftUI.Image.
    mutating func drawUI(_ image: UIImage, in rect: CGRect,
                         mode: GraphicsContext.BlendMode = .normal, alpha: Double = 1) {
        let resolved = resolve(Image(uiImage: image))
        let oldMode = blendMode
        let oldAlpha = opacity
        blendMode = mode
        opacity = alpha
        draw(resolved, in: rect)
        blendMode = oldMode
        opacity = oldAlpha
    }
}

extension GameEngine {

    func draw(context: inout GraphicsContext, size: CGSize, tick: Int, art: GameArt,
              shipSprite: Int, planetStyle: Int, trailStyle: Int) {
        guard width > 0, !art.backgrounds.isEmpty else { return }
        let center = CGPoint(x: centerX, y: centerY)
        let w = size.width
        let h = size.height
        let full = Path(CGRect(origin: .zero, size: size))

        drawBackground(&context, art: art, sector: sectorId, style: planetStyle, time: time, w: w, h: h)

        for star in stars {
            let twinkle = 0.35 + 0.65 * abs(sin(star.phase + CGFloat(tick) * 0.03 * star.speed))
            let rect = CGRect(x: star.x * w - star.size, y: star.y * h - star.size,
                              width: star.size * 2, height: star.size * 2)
            context.fill(Path(ellipseIn: rect), with: .color(Color.white.opacity(twinkle * 0.75)))
        }

        var world = context
        if shake > 0 {
            world.translateBy(x: sin(time * 83) * shake * 14, y: cos(time * 71) * shake * 14)
        }
        drawWells(&world, art: art)
        drawPlanet(&world, art: art, style: planetStyle)
        drawComboRing(&world)
        drawOrbitRing(&world)
        drawWormholes(&world, art: art)
        drawBosses(&world, art: art)
        drawTrail(&world, art: art, style: trailStyle)
        drawOrbs(&world, art: art)
        drawPowerUps(&world, art: art)
        drawAsteroids(&world, art: art)
        drawPlayer(&world, art: art, shipSprite: shipSprite)
        drawParticles(&world, art: art)

        if multiplier >= 3 {
            context.fill(full, with: .radialGradient(
                Gradient(colors: [Color.clear, skinColor.opacity(Double(multiplier - 2) * 0.07)]),
                center: center, startRadius: 0, endRadius: max(w, h) * 0.72))
        }
        if stormWarning > 0 {
            let pulse = 0.5 + 0.5 * sin(time * 12)
            context.fill(full, with: .color(Palette.danger.opacity(Double(stormWarning) * (0.06 + 0.06 * Double(pulse)))))
        }
        if flareWarning > 0 {
            context.fill(full, with: .color(Palette.orb.opacity(Double(flareWarning) * 0.1)))
        }
        if flash > 0 {
            context.fill(full, with: .radialGradient(
                Gradient(colors: [Color.white.opacity(Double(flash) * 0.2), Color.clear]),
                center: center, startRadius: 0, endRadius: max(w, h) * 0.7))
        }
        if levelUpFlash > 0 {
            context.fill(full, with: .color(Palette.orb.opacity(Double(levelUpFlash) * 0.07)))
        }
        if shieldFlash > 0 {
            context.fill(full, with: .color(Palette.shield.opacity(Double(shieldFlash) * 0.12)))
        }
        context.fill(full, with: .radialGradient(
            Gradient(colors: [Color.clear, Color.black.opacity(0.5)]),
            center: center, startRadius: 0, endRadius: max(w, h) * 0.72))
    }

    private func circlePath(_ center: CGPoint, _ radius: CGFloat) -> Path {
        Path(ellipseIn: CGRect(x: center.x - radius, y: center.y - radius,
                               width: radius * 2, height: radius * 2))
    }

    private func drawBackground(_ context: inout GraphicsContext, art: GameArt, sector: Int,
                                style: Int, time: CGFloat, w: CGFloat, h: CGFloat) {
        let image = art.backgrounds[(sector + style * 2) % art.backgrounds.count]
        let scale = max(w / CGFloat(image.size.width), h / CGFloat(image.size.height)) * 1.04
        let dw = CGFloat(image.size.width) * scale
        let dh = CGFloat(image.size.height) * scale
        let drift = sin(time * 0.12) * 8
        context.drawUI(image, in: CGRect(x: (w - dw) / 2 + drift, y: (h - dh) / 2, width: dw, height: dh))
    }

    private func drawPlanet(_ context: inout GraphicsContext, art: GameArt, style: Int) {
        let center = CGPoint(x: centerX, y: centerY)
        let image = art.planets[(sectorId + style) % art.planets.count]
        let diameter = planetRadius * 2.7
        context.fill(circlePath(center, planetRadius * 2.4), with: .radialGradient(
            Gradient(colors: [Color(hex: 0x7C5CFF).opacity(0.28), Color.clear]),
            center: center, startRadius: 0, endRadius: planetRadius * 2.4))
        let rect = CGRect(x: center.x - diameter / 2, y: center.y - diameter / 2,
                          width: diameter, height: diameter)
        context.drawLayer { layer in
            layer.translateBy(x: center.x, y: center.y)
            layer.rotate(by: .radians(Double(time * 2.2)))
            layer.drawUI(image, in: CGRect(x: -diameter / 2, y: -diameter / 2, width: diameter, height: diameter))
        }
        context.stroke(circlePath(center, planetRadius), with: .color(Color.white.opacity(0.16)),
                       lineWidth: planetRadius * 0.025)
        _ = rect
    }

    private func drawComboRing(_ context: inout GraphicsContext) {
        guard combo > 0 else { return }
        let radius = planetRadius * 1.42
        let fraction = min(max(comboTime / 2.5, 0), 1)
        var arc = Path()
        arc.addArc(center: CGPoint(x: centerX, y: centerY), radius: radius,
                   startAngle: .degrees(-90), endAngle: .degrees(-90 + 360 * Double(fraction)), clockwise: false)
        context.stroke(arc, with: .color(skinColor.opacity(0.55)),
                       style: StrokeStyle(lineWidth: 3, lineCap: .round))
    }

    private func drawOrbitRing(_ context: inout GraphicsContext) {
        context.stroke(circlePath(CGPoint(x: centerX, y: centerY), orbitRadius),
                       with: .color(Color(hex: 0xA78BFA).opacity(0.26)),
                       style: StrokeStyle(lineWidth: 1.5, dash: [9, 13]))
    }

    private func drawWells(_ context: inout GraphicsContext, art: GameArt) {
        for well in wells {
            let x = centerX + cos(well.angle) * orbitRadius * 0.72
            let y = centerY + sin(well.angle) * orbitRadius * 0.72
            let radius = planetRadius * (0.8 + 0.12 * sin(well.pulse))
            if let ring = art.ring {
                context.drawUI(ring, in: CGRect(x: x - radius, y: y - radius,
                                              width: radius * 2, height: radius * 2))
            }
            context.fill(circlePath(CGPoint(x: x, y: y), radius), with: .radialGradient(
                Gradient(colors: [Color.black.opacity(0.7), Color.clear]),
                center: CGPoint(x: x, y: y), startRadius: 0, endRadius: radius))
        }
    }

    private func drawWormholes(_ context: inout GraphicsContext, art: GameArt) {
        for worm in wormholes {
            let x = centerX + cos(worm.angle) * orbitRadius
            let y = centerY + sin(worm.angle) * orbitRadius
            let radius = playerRadius * 5
            if let ring = art.ring {
                context.drawLayer { layer in
                    layer.translateBy(x: x, y: y)
                    layer.rotate(by: .radians(Double(worm.pulse * 70)))
                    layer.drawUI(ring, in: CGRect(x: -radius, y: -radius, width: radius * 2, height: radius * 2))
                }
            }
            context.fill(circlePath(CGPoint(x: x, y: y), radius * 0.8), with: .radialGradient(
                Gradient(colors: [Color(hex: 0x1B0A3C), Palette.phase.opacity(0.5), Color.clear]),
                center: CGPoint(x: x, y: y), startRadius: 0, endRadius: radius))
        }
    }

    private func drawBosses(_ context: inout GraphicsContext, art: GameArt) {
        for boss in boss {
            let x = centerX + cos(boss.angle) * orbitRadius
            let y = centerY + sin(boss.angle) * orbitRadius
            let radius = playerRadius * 6
            let pulse = 1 + 0.14 * sin(boss.time * 5)
            if let glow = art.glow {
                let glowSize = radius * 3 * pulse
                context.drawUI(glow, in: CGRect(x: x - glowSize / 2, y: y - glowSize / 2,
                                              width: glowSize, height: glowSize))
            }
            context.fill(circlePath(CGPoint(x: x, y: y), radius * pulse), with: .radialGradient(
                Gradient(colors: [boss.hitFlash > 0 ? Color.white : Color(hex: 0x3A0A24), Palette.boss.opacity(0.85)]),
                center: CGPoint(x: x, y: y), startRadius: 0, endRadius: radius))
            context.fill(circlePath(CGPoint(x: x, y: y), radius * 0.35), with: .color(Color.white.opacity(0.8)))
            let arcRadius = radius * 1.35
            var arc = Path()
            let hpMax = CGFloat(3 + level / 15)
            arc.addArc(center: CGPoint(x: x, y: y), radius: arcRadius,
                       startAngle: .degrees(-90),
                       endAngle: .degrees(-90 + 360 * Double(CGFloat(boss.hp) / max(1, hpMax))),
                       clockwise: false)
            context.stroke(arc, with: .color(Palette.boss), style: StrokeStyle(lineWidth: 3, lineCap: .round))
        }
    }

    private func drawTrail(_ context: inout GraphicsContext, art: GameArt, style: Int) {
        guard let glow = art.glow else { return }
        for (index, point) in trail.enumerated() {
            let life = min(max(point.life, 0), 1)
            let color: Color
            if style == 2 {
                color = Color(hue: Double(((time * 90 + CGFloat(index) * 12).truncatingRemainder(dividingBy: 360)) / 360),
                              saturation: 0.75, brightness: 1)
            } else { color = point.color }
            let radius: CGFloat
            switch style {
            case 1: radius = playerRadius * (0.6 + life * 1.5)
            case 3: radius = playerRadius * (0.4 + life * 0.9) * (1 + 0.35 * sin(CGFloat(index) * 0.8 + time * 9))
            default: radius = playerRadius * (0.4 + life * 0.8)
            }
            let rect = CGRect(x: point.x - radius, y: point.y - radius, width: radius * 2, height: radius * 2)
            context.drawUI(glow, in: rect, mode: .normal, alpha: Double(life * (style == 1 ? 0.5 : 0.35)))
            context.fill(circlePath(CGPoint(x: point.x, y: point.y), radius * 0.5),
                         with: .color(color.opacity(Double(life * 0.7))))
        }
    }

    private func drawOrbs(_ context: inout GraphicsContext, art: GameArt) {
        for orb in orbs {
            let pulse = 1 + 0.12 * sin(orb.pulse)
            let x = centerX + cos(orb.angle) * orbitRadius
            let y = centerY + sin(orb.angle) * orbitRadius
            let radius = playerRadius * (orb.kind == .artifact ? 2.2 : 1.7) * pulse
            if let glow = art.glow {
                context.drawUI(glow, in: CGRect(x: x - radius * 1.8, y: y - radius * 1.8,
                                              width: radius * 3.6, height: radius * 3.6), alpha: 0.4)
            }
            let sprite = art.orbs[orb.sprite % art.orbs.count]
            context.drawUI(sprite, in: CGRect(x: x - radius, y: y - radius, width: radius * 2, height: radius * 2))
        }
    }

    private func drawPowerUps(_ context: inout GraphicsContext, art: GameArt) {
        for power in powerUps {
            let x = centerX + cos(power.angle) * orbitRadius
            let y = centerY + sin(power.angle) * orbitRadius
            let radius = playerRadius * 2.5 * (1 + 0.09 * sin(power.pulse))
            let index = allPowerKinds.firstIndex(of: power.kind) ?? 0
            let sprite = art.powers[index % art.powers.count]
            context.drawLayer { layer in
                layer.translateBy(x: x, y: y)
                layer.rotate(by: .radians(Double(sin(power.pulse * 0.5) * 8)))
                layer.drawUI(sprite, in: CGRect(x: -radius, y: -radius, width: radius * 2, height: radius * 2))
            }
        }
    }

    private var allPowerKinds: [PowerUpKind] { [.shield, .slow, .double, .magnet, .phase, .freeze, .revive] }

    private func drawAsteroids(_ context: inout GraphicsContext, art: GameArt) {
        for asteroid in asteroids {
            let x = centerX + cos(asteroid.angle) * asteroid.distance
            let y = centerY + sin(asteroid.angle) * asteroid.distance
            switch asteroid.kind {
            case .comet:
                let dx = cos(asteroid.angle)
                let dy = sin(asteroid.angle)
                let start = CGPoint(x: x + dx * asteroid.radius, y: y + dy * asteroid.radius)
                let end = CGPoint(x: x + dx * asteroid.radius * 7, y: y + dy * asteroid.radius * 7)
                var tail = Path()
                tail.move(to: start)
                tail.addLine(to: end)
                context.stroke(tail, with: .linearGradient(
                    Gradient(colors: [Color(hex: 0xFFB86B).opacity(0.85), Color.clear]),
                    startPoint: start, endPoint: end),
                    style: StrokeStyle(lineWidth: asteroid.radius * 0.9, lineCap: .round))
                let sprite = art.comets[asteroid.sprite % art.comets.count]
                let d = asteroid.radius * 3.6
                context.drawUI(sprite, in: CGRect(x: x - d / 2, y: y - d / 2, width: d, height: d))
            case .bolt:
                let dx = cos(asteroid.angle)
                let dy = sin(asteroid.angle)
                var bolt = Path()
                bolt.move(to: CGPoint(x: x + dx * asteroid.radius * 4, y: y + dy * asteroid.radius * 4))
                bolt.addLine(to: CGPoint(x: x - dx * asteroid.radius * 4, y: y - dy * asteroid.radius * 4))
                context.stroke(bolt, with: .color(Palette.boss.opacity(0.9)),
                               style: StrokeStyle(lineWidth: asteroid.radius * 1.2, lineCap: .round))
            case .mine, .rock:
                let sprite = art.asteroids[asteroid.sprite % art.asteroids.count]
                let d = asteroid.radius * 2.6
                context.drawLayer { layer in
                    layer.translateBy(x: x, y: y)
                    layer.rotate(by: .radians(Double(asteroid.rotation)))
                    layer.drawUI(sprite, in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d))
                }
                if asteroid.kind == .mine {
                    context.stroke(circlePath(CGPoint(x: x, y: y), asteroid.radius * 0.75),
                                   with: .color(Palette.danger.opacity(0.75 + 0.25 * sin(asteroid.pulse * 3))),
                                   lineWidth: asteroid.radius * 0.2)
                    context.fill(circlePath(CGPoint(x: x, y: y), asteroid.radius * 0.3),
                                 with: .color(Palette.danger))
                }
            }
        }
    }

    private func drawPlayer(_ context: inout GraphicsContext, art: GameArt, shipSprite: Int) {
        let position = CGPoint(x: playerX, y: playerY)
        let alpha = invulnerable > 0 ? 0.4 + 0.6 * abs(sin(time * 22)) : 1
        let halo = playerRadius * 4.5
        if let glow = art.glow {
            context.drawUI(glow, in: CGRect(x: position.x - halo, y: position.y - halo,
                                          width: halo * 2, height: halo * 2), alpha: Double(0.35 * alpha))
        }
        if shield || phaseTimer > 0 || freezeTimer > 0 {
            let color = phaseTimer > 0 ? Palette.phase : (freezeTimer > 0 ? Palette.freeze : Palette.shield)
            let pulse = 1 + 0.08 * sin(time * 6)
            context.fill(circlePath(position, playerRadius * 2.7 * pulse), with: .color(color.opacity(0.12)))
            context.stroke(circlePath(position, playerRadius * 2.7 * pulse), with: .color(color.opacity(0.75)),
                           lineWidth: playerRadius * 0.4)
        }
        if reverseFlash > 0 {
            context.stroke(circlePath(position, playerRadius * (1.5 + (1 - reverseFlash) * 3)),
                           with: .color(skinColor.opacity(Double(reverseFlash) * 0.5)),
                           lineWidth: playerRadius * 0.35)
        }
        let heading = playerAngle + direction * (CGFloat.pi / 2)
        let d = playerRadius * 6.5
        let sprite = art.ships[shipSprite % art.ships.count]
        context.drawLayer { layer in
            layer.translateBy(x: position.x, y: position.y)
            layer.rotate(by: .radians(Double(heading + CGFloat.pi / 2)))
            layer.drawUI(sprite, in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d), alpha: Double(alpha))
        }
        context.fill(circlePath(position, playerRadius * 0.35), with: .color(Color.white.opacity(alpha)))
    }

    private func drawParticles(_ context: inout GraphicsContext, art: GameArt) {
        guard let glow = art.glow else { return }
        for particle in particles {
            let life = min(max(particle.life / particle.maxLife, 0), 1)
            let radius = particle.size * (1.4 + life)
            context.drawUI(glow, in: CGRect(x: particle.x - radius, y: particle.y - radius,
                                          width: radius * 2, height: radius * 2), alpha: Double(life * 0.85))
        }
    }
}

// MARK: - Menu backgrounds

extension GameArt {
    func drawMenu(in context: inout GraphicsContext, size: CGSize, style: Int, tick: Int) {
        guard !backgrounds.isEmpty else { return }
        let image = backgrounds[style % backgrounds.count]
        let scale = max(size.width / CGFloat(image.size.width), size.height / CGFloat(image.size.height)) * 1.05
        let dw = CGFloat(image.size.width) * scale
        let dh = CGFloat(image.size.height) * scale
        let shift = sin(Double(tick) * 0.004) * 10
        context.drawUI(image, in: CGRect(x: (size.width - dw) / 2 + shift, y: (size.height - dh) / 2,
                                       width: dw, height: dh))
        let full = Path(CGRect(origin: .zero, size: size))
        context.fill(full, with: .radialGradient(
            Gradient(colors: [Color.clear, Color(hex: 0x05030F).opacity(0.86)]),
            center: CGPoint(x: size.width / 2, y: size.height / 2),
            startRadius: 0, endRadius: max(size.width, size.height) * 0.72))
    }

    func drawPlanetPreview(in context: inout GraphicsContext, index: Int, center: CGPoint,
                           radius: CGFloat, tick: Int) {
        guard !planets.isEmpty else { return }
        let image = planets[index % planets.count]
        let d = radius * 2
        context.drawLayer { layer in
            layer.translateBy(x: center.x, y: center.y)
            layer.rotate(by: .radians(Double(tick) * 0.01))
            layer.drawUI(image, in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d))
        }
    }
}
