import SwiftUI

extension GameEngine {

    func draw(context: inout GraphicsContext, size: CGSize, tick: Int) {
        guard width > 0 else { return }
        let center = CGPoint(x: centerX, y: centerY)
        let w = size.width
        let h = size.height
        let background = Path(CGRect(origin: .zero, size: size))

        context.fill(
            background,
            with: .radialGradient(
                Gradient(colors: [Color(hex: 0x1B1140), Color(hex: 0x0A0618), Color(hex: 0x030208)]),
                center: center,
                startRadius: 0,
                endRadius: max(w, h) * 0.8
            )
        )

        for star in stars {
            let twinkle = 0.35 + 0.65 * abs(sin(star.phase + CGFloat(tick) * 0.03 * star.speed))
            let rect = CGRect(
                x: star.x * w - star.size,
                y: star.y * h - star.size,
                width: star.size * 2,
                height: star.size * 2
            )
            context.fill(Path(ellipseIn: rect), with: .color(Color.white.opacity(twinkle * 0.75)))
        }

        var world = context
        if shake > 0 {
            world.translateBy(
                x: sin(time * 83) * shake * 14,
                y: cos(time * 71) * shake * 14
            )
        }
        drawPlanet(&world, center: center)
        drawOrbitRing(&world, center: center)
        drawTrail(&world)
        drawOrbs(&world, center: center)
        drawPowerUps(&world, center: center)
        drawAsteroids(&world, center: center)
        drawPlayer(&world)
        drawParticles(&world)

        if flash > 0 {
            context.fill(
                background,
                with: .radialGradient(
                    Gradient(colors: [Color.white.opacity(flash * 0.20), Color.clear]),
                    center: center,
                    startRadius: 0,
                    endRadius: max(w, h) * 0.7
                )
            )
        }
        if shieldFlash > 0 {
            context.fill(background, with: .color(Palette.shield.opacity(shieldFlash * 0.12)))
        }

        context.fill(
            background,
            with: .radialGradient(
                Gradient(colors: [Color.clear, Color.black.opacity(0.55)]),
                center: center,
                startRadius: 0,
                endRadius: max(w, h) * 0.72
            )
        )
    }

    private func circlePath(_ center: CGPoint, _ radius: CGFloat) -> Path {
        Path(ellipseIn: CGRect(
            x: center.x - radius,
            y: center.y - radius,
            width: radius * 2,
            height: radius * 2
        ))
    }

    private func drawPlanet(_ context: inout GraphicsContext, center: CGPoint) {
        context.fill(
            circlePath(center, planetRadius * 2.4),
            with: .radialGradient(
                Gradient(colors: [Color(hex: 0x7C5CFF).opacity(0.35), Color.clear]),
                center: center,
                startRadius: 0,
                endRadius: planetRadius * 2.4
            )
        )
        context.fill(
            circlePath(center, planetRadius),
            with: .radialGradient(
                Gradient(colors: [Color(hex: 0x6A4BE0), Color(hex: 0x241246)]),
                center: center,
                startRadius: 0,
                endRadius: planetRadius
            )
        )
        context.stroke(
            circlePath(center, planetRadius),
            with: .color(Color(hex: 0x9B8CFF).opacity(0.35)),
            lineWidth: planetRadius * 0.035
        )
    }

    private func drawOrbitRing(_ context: inout GraphicsContext, center: CGPoint) {
        context.stroke(
            circlePath(center, orbitRadius),
            with: .color(Color(hex: 0x6D5BD0).opacity(0.22)),
            style: StrokeStyle(lineWidth: 1.5, dash: [9, 13])
        )
    }

    private func drawTrail(_ context: inout GraphicsContext) {
        for point in trail {
            let life = min(max(point.life, 0), 1)
            context.fill(
                circlePath(CGPoint(x: point.x, y: point.y), playerRadius * (0.4 + life * 0.8)),
                with: .color(Palette.player.opacity(life * 0.35))
            )
        }
    }

    private func drawOrbs(_ context: inout GraphicsContext, center: CGPoint) {
        for orb in orbs {
            let pulse = 1 + 0.12 * sin(orb.pulse)
            let position = CGPoint(
                x: center.x + cos(orb.angle) * orbitRadius,
                y: center.y + sin(orb.angle) * orbitRadius
            )
            let radius = playerRadius * 1.5 * pulse
            context.fill(circlePath(position, radius * 2.1), with: .color(Palette.orb.opacity(0.22)))
            context.fill(
                circlePath(position, radius),
                with: .radialGradient(
                    Gradient(colors: [Color(hex: 0xFFF3B0), Color(hex: 0xF59E0B)]),
                    center: position,
                    startRadius: 0,
                    endRadius: radius
                )
            )
        }
    }

    private func drawPowerUps(_ context: inout GraphicsContext, center: CGPoint) {
        for powerUp in powerUps {
            let position = CGPoint(
                x: center.x + cos(powerUp.angle) * orbitRadius,
                y: center.y + sin(powerUp.angle) * orbitRadius
            )
            let color: Color
            switch powerUp.kind {
            case .shield: color = Palette.shield
            case .slow: color = Palette.slow
            case .double: color = Palette.double
            }
            let radius = playerRadius * 2.1
            let wobble = 1 + 0.1 * sin(powerUp.pulse)

            context.fill(circlePath(position, radius * 1.9), with: .color(color.opacity(0.18)))

            var diamond = Path()
            diamond.move(to: CGPoint(x: position.x, y: position.y - radius * wobble))
            diamond.addLine(to: CGPoint(x: position.x + radius * wobble, y: position.y))
            diamond.addLine(to: CGPoint(x: position.x, y: position.y + radius * wobble))
            diamond.addLine(to: CGPoint(x: position.x - radius * wobble, y: position.y))
            diamond.closeSubpath()

            context.fill(diamond, with: .color(color.opacity(0.85)))
            context.stroke(diamond, with: .color(Color.white.opacity(0.9)), lineWidth: radius * 0.12)

            switch powerUp.kind {
            case .shield:
                context.stroke(
                    circlePath(position, radius * 0.4),
                    with: .color(Color(hex: 0x04222A)),
                    lineWidth: radius * 0.16
                )
            case .slow:
                var bars = Path()
                bars.move(to: CGPoint(x: position.x - radius * 0.28, y: position.y - radius * 0.3))
                bars.addLine(to: CGPoint(x: position.x - radius * 0.28, y: position.y + radius * 0.3))
                bars.move(to: CGPoint(x: position.x + radius * 0.28, y: position.y - radius * 0.3))
                bars.addLine(to: CGPoint(x: position.x + radius * 0.28, y: position.y + radius * 0.3))
                context.stroke(
                    bars,
                    with: .color(Color(hex: 0x1B1033)),
                    style: StrokeStyle(lineWidth: radius * 0.16, lineCap: .round)
                )
            case .double:
                context.fill(
                    circlePath(CGPoint(x: position.x - radius * 0.26, y: position.y), radius * 0.13),
                    with: .color(Color(hex: 0x2A1405))
                )
                context.fill(
                    circlePath(CGPoint(x: position.x + radius * 0.26, y: position.y), radius * 0.13),
                    with: .color(Color(hex: 0x2A1405))
                )
            }
        }
    }

    private func drawAsteroids(_ context: inout GraphicsContext, center: CGPoint) {
        for asteroid in asteroids {
            let position = CGPoint(
                x: center.x + cos(asteroid.angle) * asteroid.distance,
                y: center.y + sin(asteroid.angle) * asteroid.distance
            )
            var path = Path()
            for index in 0..<asteroid.shape.count {
                let angle = asteroid.rotation + CGFloat(index) * (CGFloat.pi * 2 / CGFloat(asteroid.shape.count))
                let radius = asteroid.radius * asteroid.shape[index]
                let point = CGPoint(x: position.x + cos(angle) * radius, y: position.y + sin(angle) * radius)
                if index == 0 {
                    path.move(to: point)
                } else {
                    path.addLine(to: point)
                }
            }
            path.closeSubpath()
            context.fill(path, with: .color(Color(hex: 0x2A1D4D)))
            context.stroke(path, with: .color(Palette.asteroid.opacity(0.9)), lineWidth: asteroid.radius * 0.16)
        }
    }

    private func drawPlayer(_ context: inout GraphicsContext) {
        let position = CGPoint(x: playerX, y: playerY)
        let alpha = invulnerable > 0 ? 0.4 + 0.6 * abs(sin(time * 22)) : 1

        context.fill(
            circlePath(position, playerRadius * 4.5),
            with: .radialGradient(
                Gradient(colors: [Palette.player.opacity(0.35 * alpha), Color.clear]),
                center: position,
                startRadius: 0,
                endRadius: playerRadius * 4.5
            )
        )

        if shield {
            let pulse = 1 + 0.08 * sin(time * 6)
            context.fill(
                circlePath(position, playerRadius * 2.7 * pulse),
                with: .color(Palette.shield.opacity(0.12))
            )
            context.stroke(
                circlePath(position, playerRadius * 2.7 * pulse),
                with: .color(Palette.shield.opacity(0.75)),
                lineWidth: playerRadius * 0.4
            )
        }

        if reverseFlash > 0 {
            context.stroke(
                circlePath(position, playerRadius * (1.5 + (1 - reverseFlash) * 3)),
                with: .color(Palette.player.opacity(reverseFlash * 0.5)),
                lineWidth: playerRadius * 0.35
            )
        }

        let heading = playerAngle + direction * (CGFloat.pi / 2)
        let shipSize = playerRadius * 2.3
        var ship = Path()
        ship.move(to: CGPoint(
            x: position.x + cos(heading) * shipSize,
            y: position.y + sin(heading) * shipSize
        ))
        ship.addLine(to: CGPoint(
            x: position.x + cos(heading + 2.6) * shipSize * 0.8,
            y: position.y + sin(heading + 2.6) * shipSize * 0.8
        ))
        ship.addLine(to: CGPoint(
            x: position.x + cos(heading - 2.6) * shipSize * 0.8,
            y: position.y + sin(heading - 2.6) * shipSize * 0.8
        ))
        ship.closeSubpath()

        context.fill(ship, with: .color(Palette.player.opacity(alpha)))
        context.fill(circlePath(position, playerRadius * 0.55), with: .color(Color.white.opacity(alpha)))
    }

    private func drawParticles(_ context: inout GraphicsContext) {
        for particle in particles {
            let life = min(max(particle.life / particle.maxLife, 0), 1)
            context.fill(
                circlePath(CGPoint(x: particle.x, y: particle.y), particle.size * (0.5 + life)),
                with: .color(particle.color.opacity(life))
            )
        }
    }
}
