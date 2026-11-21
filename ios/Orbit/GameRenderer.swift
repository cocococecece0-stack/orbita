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

/// Высококонтрастная палитра для режима «Контрастные цвета».
private enum RenderColors {
    static let hazard = Color(hex: 0xFF7A1A)
    static let hazardHot = Color(hex: 0xFF4438)
    static let hazardDark = Color(hex: 0xC4232B)
    static let bonusBlue = Color(hex: 0x35A7FF)
    static let bonusGreen = Color(hex: 0x37E39B)
}

/// Цвета тетро-фигур: одинаковые на Android и iOS.
private let blockColors: [Color] = [
    Color(hex: 0x22D3EE), Color(hex: 0xD8FF3E), Color(hex: 0xFF5B3D), Color(hex: 0x8B5CF6),
    Color(hex: 0xFFC23D), Color(hex: 0x3B82F6), Color(hex: 0xF472B6),
]

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

    /// Круглое свечение без квадратного изображения fx_glow.
    mutating func drawGlow(_ center: CGPoint, radius: CGFloat, color: Color, alpha: Double) {
        guard radius > 0, alpha > 0 else { return }
        let path = Path(ellipseIn: CGRect(x: center.x - radius, y: center.y - radius,
                                          width: radius * 2, height: radius * 2))
        fill(path, with: .radialGradient(
            Gradient(colors: [color.opacity(alpha), color.opacity(alpha * 0.4), Color.clear]),
            center: center, startRadius: 0, endRadius: radius))
    }
}

extension GameEngine {

    func draw(context: inout GraphicsContext, size: CGSize, tick: Int, art: GameArt,
              shipSprite: Int, planetStyle: Int, trailStyle: Int,
              colorblind: Bool, reducedMotion: Bool) {
        guard width > 0 else { return }
        // Визуальное время отделено от игровой логики: при reducedMotion все пульсации стоят.
        let visualTime: CGFloat = reducedMotion ? 0 : time
        if mode == .stack {
            drawStackArena(&context, size: size, time: visualTime, reducedMotion: reducedMotion)
            return
        }
        guard !art.backgrounds.isEmpty else { return }
        let center = CGPoint(x: centerX, y: centerY)
        let w = size.width
        let h = size.height
        let full = Path(CGRect(origin: .zero, size: size))
        let visualTick: Int = reducedMotion ? 0 : tick

        drawBackground(&context, art: art, sector: sectorId, style: planetStyle,
                       time: visualTime, w: w, h: h)

        for star in stars {
            let twinkle = 0.35 + 0.65 * abs(sin(star.phase + CGFloat(visualTick) * 0.03 * star.speed))
            let rect = CGRect(x: star.x * w - star.size, y: star.y * h - star.size,
                              width: star.size * 2, height: star.size * 2)
            context.fill(Path(ellipseIn: rect), with: .color(Color.white.opacity(twinkle * 0.75)))
        }

        // Орбита, планета и сектор остаются абсолютно неподвижными: дрейф камеры убран.
        drawWells(&context, art: art, reducedMotion: reducedMotion)
        drawPlanet(&context, art: art, style: planetStyle, reducedMotion: reducedMotion,
                   colorblind: colorblind)
        drawComboRing(&context)
        drawResonanceRing(&context)
        drawOrbitRing(&context)
        drawWormholes(&context, art: art, reducedMotion: reducedMotion)
        drawBosses(&context, art: art, reducedMotion: reducedMotion, colorblind: colorblind)
        drawTrail(&context, art: art, style: trailStyle, visualTime: visualTime)
        drawOrbs(&context, art: art, reducedMotion: reducedMotion, colorblind: colorblind)
        drawPowerUps(&context, art: art, reducedMotion: reducedMotion, colorblind: colorblind)
        drawAsteroids(&context, art: art, reducedMotion: reducedMotion, colorblind: colorblind)
        drawPlayer(&context, art: art, shipSprite: shipSprite, visualTime: visualTime,
                   reducedMotion: reducedMotion)
        drawParticles(&context, art: art)

        if multiplier >= 3 {
            context.fill(full, with: .radialGradient(
                Gradient(colors: [Color.clear, skinColor.opacity(Double(multiplier - 2) * 0.07)]),
                center: center, startRadius: 0, endRadius: max(w, h) * 0.72))
        }
        if stormWarning > 0 {
            let pulse = 0.5 + 0.5 * sin(visualTime * 12)
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

    /// Яркая арена «ШТАБЕЛЯ»: зеркально совпадает с Android drawStackArena.
    private func drawStackArena(_ context: inout GraphicsContext, size: CGSize,
                                time: CGFloat, reducedMotion: Bool) {
        let w = size.width
        let h = size.height
        let full = Path(CGRect(origin: .zero, size: size))
        context.fill(full, with: .linearGradient(
            Gradient(colors: [Color(hex: 0x0B1022), Color(hex: 0x17224A), Color(hex: 0x0B1022)]),
            startPoint: CGPoint(x: 0, y: 0), endPoint: CGPoint(x: w, y: h)))

        let pulse: CGFloat = reducedMotion ? 0.5 : 0.5 + 0.5 * sin(time * 1.6)
        let glowR = max(w, h) * 0.42
        context.fill(Path(ellipseIn: CGRect(x: w * 0.12 - glowR, y: h * 0.18 - glowR,
                                            width: glowR * 2, height: glowR * 2)),
                     with: .radialGradient(
                        Gradient(colors: [Color(hex: 0xD8FF3E).opacity(0.16 + Double(pulse) * 0.06), Color.clear]),
                        center: CGPoint(x: w * 0.12, y: h * 0.18), startRadius: 0, endRadius: glowR))
        let coralR = max(w, h) * 0.38
        context.fill(Path(ellipseIn: CGRect(x: w * 0.9 - coralR, y: h * 0.78 - coralR,
                                            width: coralR * 2, height: coralR * 2)),
                     with: .radialGradient(
                        Gradient(colors: [Color(hex: 0xFF5B3D).opacity(0.14), Color.clear]),
                        center: CGPoint(x: w * 0.9, y: h * 0.78), startRadius: 0, endRadius: coralR))
        for i in 0..<7 {
            let y = h * (0.08 + CGFloat(i) * 0.13)
            let line = Path { path in
                path.move(to: CGPoint(x: -w * 0.05, y: y + h * 0.1))
                path.addLine(to: CGPoint(x: w * 1.05, y: y))
            }
            let color = i % 2 == 0 ? Color(hex: 0xD8FF3E).opacity(0.07) : Color(hex: 0xFF5B3D).opacity(0.08)
            context.stroke(line, with: .color(color), lineWidth: 2)
        }

        let topPad = h * 0.15
        let bottomPad = h * 0.15
        let usableH = h - topPad - bottomPad
        let cell = min(w * 0.74 / CGFloat(BlockArena.cols), usableH / CGFloat(BlockArena.rows))
        let boardW = cell * CGFloat(BlockArena.cols)
        let boardH = cell * CGFloat(BlockArena.rows)
        let left = (w - boardW) / 2
        let top = topPad + (usableH - boardH) / 2
        let board = CGRect(x: left, y: top, width: boardW, height: boardH)
        let radius = cell * 0.42

        context.fill(Path(roundedRect: board, cornerRadius: radius), with: .linearGradient(
            Gradient(colors: [Color(hex: 0x141B33), Color(hex: 0x1B2448), Color(hex: 0x11162A)]),
            startPoint: CGPoint(x: left, y: top), endPoint: CGPoint(x: board.maxX, y: board.maxY)))
        for c in 1..<BlockArena.cols {
            let x = left + CGFloat(c) * cell
            let line = Path { path in
                path.move(to: CGPoint(x: x, y: top))
                path.addLine(to: CGPoint(x: x, y: board.maxY))
            }
            context.stroke(line, with: .color(Color.white.opacity(0.055)), lineWidth: 1)
        }
        for r in 1..<BlockArena.rows {
            let y = top + CGFloat(r) * cell
            let line = Path { path in
                path.move(to: CGPoint(x: left, y: y))
                path.addLine(to: CGPoint(x: board.maxX, y: y))
            }
            context.stroke(line, with: .color(Color.white.opacity(0.055)), lineWidth: 1)
        }

        let arena = blocks
        for row in 0..<BlockArena.rows {
            for col in 0..<BlockArena.cols {
                let value = arena.cell(col, row)
                if value != 0 {
                    drawBlockCell(&context, rect: blockRect(left, top, cell, col, row),
                                  color: blockColors[(value - 1) % blockColors.count], cell: cell)
                }
            }
        }

        if !arena.topOut {
            let ghost = arena.ghostY()
            for part in arena.cells(arena.piece, arena.rotation) where ghost + part.1 >= 0 {
                drawBlockCell(&context, rect: blockRect(left, top, cell, arena.x + part.0, ghost + part.1),
                              color: Color(hex: 0xD8FF3E), cell: cell, ghost: true)
            }
            for part in arena.cells(arena.piece, arena.rotation) where arena.y + part.1 >= 0 {
                drawBlockCell(&context, rect: blockRect(left, top, cell, arena.x + part.0, arena.y + part.1),
                              color: blockColors[arena.piece % blockColors.count], cell: cell)
            }
        }

        if arena.clearFlash > 0 {
            let alpha = min(max(arena.clearFlash / 0.42, 0), 1)
            for row in 0..<BlockArena.rows where arena.clearRows[row] {
                context.fill(Path(CGRect(x: left, y: top + CGFloat(row) * cell, width: boardW, height: cell)),
                             with: .color(Color(hex: 0xD8FF3E).opacity(Double(alpha) * 0.55)))
            }
        }

        let sideMargin = left - 10
        if sideMargin > cell * 1.6 {
            let panelW = sideMargin - 6
            let leftPanel = CGRect(x: 10, y: top, width: panelW, height: boardH)
            let rightPanel = CGRect(x: board.maxX + 6, y: top, width: w - 10 - (board.maxX + 6), height: boardH)
            context.fill(Path(roundedRect: leftPanel, cornerRadius: radius),
                         with: .color(Color(hex: 0x111626).opacity(0.6)))
            context.fill(Path(roundedRect: rightPanel, cornerRadius: radius),
                         with: .color(Color(hex: 0x111626).opacity(0.6)))
            if arena.held >= 0 {
                drawMiniPiece(&context, type: arena.held, cx: leftPanel.midX, cy: leftPanel.midY, cell: cell * 0.62)
            }
            for (i, type) in arena.next.prefix(3).enumerated() {
                let cy = rightPanel.minY + rightPanel.height * (0.2 + CGFloat(i) * 0.29)
                drawMiniPiece(&context, type: type, cx: rightPanel.midX, cy: cy, cell: cell * 0.58)
            }
        }

        context.stroke(Path(roundedRect: board, cornerRadius: radius),
                       with: .color(Color(hex: 0xD8FF3E).opacity(0.9)), lineWidth: 3)
        if flash > 0 {
            context.fill(full, with: .color(Color(hex: 0xFFF6E8).opacity(Double(flash) * 0.18)))
        }
    }

    private func blockRect(_ left: CGFloat, _ top: CGFloat, _ cell: CGFloat,
                           _ col: Int, _ row: Int) -> CGRect {
        CGRect(x: left + CGFloat(col) * cell, y: top + CGFloat(row) * cell,
               width: cell, height: cell)
    }

    private func drawBlockCell(_ context: inout GraphicsContext, rect: CGRect,
                               color: Color, cell: CGFloat, ghost: Bool = false) {
        let inset = cell * 0.07
        let box = CGRect(x: rect.minX + inset, y: rect.minY + inset,
                         width: rect.width - inset * 2, height: rect.height - inset * 2)
        let radius = cell * 0.24
        let path = Path(roundedRect: box, cornerRadius: radius)
        if ghost {
            context.stroke(path, with: .color(color.opacity(0.42)),
                           style: StrokeStyle(lineWidth: cell * 0.12))
            return
        }
        context.fill(path, with: .linearGradient(
            Gradient(colors: [color.opacity(0.96), color.opacity(0.66)]),
            startPoint: CGPoint(x: box.minX, y: box.minY),
            endPoint: CGPoint(x: box.maxX, y: box.maxY)))
        context.stroke(path, with: .color(Color.white.opacity(0.32)),
                       style: StrokeStyle(lineWidth: max(1, cell * 0.06)))
    }

    private func drawMiniPiece(_ context: inout GraphicsContext, type: Int,
                               cx: CGFloat, cy: CGFloat, cell: CGFloat) {
        guard type >= 0 else { return }
        let parts = blocks.cells(type, 0)
        guard let maxX = parts.map({ $0.0 }).max(), let maxY = parts.map({ $0.1 }).max() else { return }
        let cols = CGFloat(max(maxX + 1, 1))
        let rows = CGFloat(max(maxY + 1, 1))
        let ox = cx - cols * cell / 2
        let oy = cy - rows * cell / 2
        for part in parts {
            let rect = CGRect(x: ox + CGFloat(part.0) * cell, y: oy + CGFloat(part.1) * cell,
                              width: cell, height: cell)
            drawBlockCell(&context, rect: rect, color: blockColors[type % blockColors.count], cell: cell)
        }
    }

    private func circlePath(_ center: CGPoint, _ radius: CGFloat) -> Path {
        Path(ellipseIn: CGRect(x: center.x - radius, y: center.y - radius,
                               width: radius * 2, height: radius * 2))
    }

    /// Обводка и шевроны над опасностью — читается даже при дальтонизме.
    private func drawHazardMarker(_ context: inout GraphicsContext, center: CGPoint,
                                  radius: CGFloat, colorblind: Bool) {
        guard colorblind, radius > 0 else { return }
        context.stroke(circlePath(center, radius), with: .color(RenderColors.hazard),
                       style: StrokeStyle(lineWidth: max(2, radius * 0.16)))
        for ySign in [-1, 1] {
            let y = center.y + CGFloat(ySign) * radius * 1.3
            let half = radius * 0.5
            var chevron = Path()
            chevron.move(to: CGPoint(x: center.x - half, y: y - radius * 0.22))
            chevron.addLine(to: CGPoint(x: center.x, y: y + radius * 0.22))
            chevron.addLine(to: CGPoint(x: center.x + half, y: y - radius * 0.22))
            context.stroke(chevron, with: .color(RenderColors.hazardDark),
                           style: StrokeStyle(lineWidth: max(2, radius * 0.14),
                                              lineCap: .round, lineJoin: .round))
        }
    }

    /// Разные формы бонусов: квадрат / ромб / треугольник.
    private func drawBonusMarker(_ context: inout GraphicsContext, center: CGPoint,
                                 radius: CGFloat, shape: Int, colorblind: Bool) {
        guard colorblind, radius > 0 else { return }
        let color = shape == 1 ? RenderColors.bonusGreen : RenderColors.bonusBlue
        let r = radius
        var marker = Path()
        switch shape {
        case 1:
            marker.move(to: CGPoint(x: center.x, y: center.y - r))
            marker.addLine(to: CGPoint(x: center.x + r, y: center.y))
            marker.addLine(to: CGPoint(x: center.x, y: center.y + r))
            marker.addLine(to: CGPoint(x: center.x - r, y: center.y))
            marker.closeSubpath()
        case 2:
            marker.move(to: CGPoint(x: center.x, y: center.y - r))
            marker.addLine(to: CGPoint(x: center.x + r, y: center.y + r * 0.8))
            marker.addLine(to: CGPoint(x: center.x - r, y: center.y + r * 0.8))
            marker.closeSubpath()
        default:
            marker = Path(CGRect(x: center.x - r * 0.75, y: center.y - r * 0.75,
                                 width: r * 1.5, height: r * 1.5))
        }
        context.stroke(marker, with: .color(color),
                       style: StrokeStyle(lineWidth: max(1.5, r * 0.14), lineJoin: .round))
    }

    private func drawBackground(_ context: inout GraphicsContext, art: GameArt, sector: Int,
                                 style: Int, time: CGFloat, w: CGFloat, h: CGFloat) {
        let image = art.backgrounds[((sector % art.backgrounds.count) + art.backgrounds.count) % art.backgrounds.count]
        let scale = max(w / CGFloat(image.size.width), h / CGFloat(image.size.height)) * 1.04
        let dw = CGFloat(image.size.width) * scale
        let dh = CGFloat(image.size.height) * scale
        context.drawUI(image, in: CGRect(x: (w - dw) / 2, y: (h - dh) / 2, width: dw, height: dh))
    }

    private func drawPlanet(_ context: inout GraphicsContext, art: GameArt, style: Int,
                            reducedMotion: Bool, colorblind: Bool) {
        let center = CGPoint(x: centerX, y: centerY)
        let image = art.planets[max(0, min(style, art.planets.count - 1))]
        // Видимый радиус спрайта совпадает с радиусом обводки и не заходит на орбиту.
        let diameter = planetRadius * 2.25
        let outlineRadius = planetRadius * 1.02
        context.drawGlow(center, radius: planetRadius * 2.4,
                         color: Color(hex: 0x7C5CFF), alpha: 0.28)
        context.drawLayer { layer in
            layer.translateBy(x: center.x, y: center.y)
            layer.rotate(by: .radians(Double(reducedMotion ? 0 : time * 2.2)))
            layer.drawUI(image, in: CGRect(x: -diameter / 2, y: -diameter / 2,
                                           width: diameter, height: diameter))
        }
        context.stroke(circlePath(center, outlineRadius), with: .color(Color.white.opacity(0.16)),
                       lineWidth: planetRadius * 0.025)
        // Секторный акцент рисуется здесь, а не запекается в текстуру планеты.
        let accents: [UInt32] = [0x22D3EE, 0xFB7185, 0xA78BFA, 0x78DCFF,
                                 0xF472B6, 0xB388FF, 0xFFFF5C70, 0xFBBF24]
        let accent = Color(hex: accents[max(0, min(sectorId, accents.count - 1))])
        var arc = Path()
        arc.addArc(center: center, radius: planetRadius * 1.22,
                   startAngle: .degrees(-115), endAngle: .degrees(35), clockwise: false)
        context.stroke(arc, with: .color(accent.opacity(colorblind ? 0.9 : 0.35)),
                       style: StrokeStyle(lineWidth: colorblind ? 3 : 2, lineCap: .round))
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

    private func drawResonanceRing(_ context: inout GraphicsContext) {
        guard resonance > 0 || resonanceFlash > 0 else { return }
        let radius = orbitRadius * 1.16
        let alpha = min(max(0.22 + Double(resonance) * 0.45 + Double(resonanceFlash) * 0.33, 0), 1)
        let fraction = resonanceFlash > 0 ? 1 : Double(resonance)
        var arc = Path()
        arc.addArc(center: CGPoint(x: centerX, y: centerY), radius: radius,
                   startAngle: .degrees(-90), endAngle: .degrees(-90 + 360 * fraction),
                   clockwise: false)
        context.stroke(arc, with: .color(skinColor.opacity(alpha)),
                       style: StrokeStyle(lineWidth: resonanceFlash > 0 ? 5 : 2.5, lineCap: .round))
    }

    private func drawOrbitRing(_ context: inout GraphicsContext) {
        context.stroke(circlePath(CGPoint(x: centerX, y: centerY), orbitRadius),
                        with: .color(Color(hex: 0xA78BFA).opacity(0.26)),
                        style: StrokeStyle(lineWidth: 1.5, dash: [9, 13]))
    }

    private func drawWells(_ context: inout GraphicsContext, art: GameArt, reducedMotion: Bool) {
        for well in wells {
            let x = centerX + cos(well.angle) * orbitRadius * 0.72
            let y = centerY + sin(well.angle) * orbitRadius * 0.72
            let pulse: CGFloat = reducedMotion ? 0 : 0.12 * sin(well.pulse)
            let radius = planetRadius * (0.8 + pulse)
            let center = CGPoint(x: x, y: y)
            if let ring = art.ring {
                context.drawUI(ring, in: CGRect(x: x - radius, y: y - radius,
                                                width: radius * 2, height: radius * 2))
            }
            context.drawGlow(center, radius: radius, color: Palette.slow, alpha: 0.3)
            context.fill(circlePath(center, radius), with: .radialGradient(
                Gradient(colors: [Color.black.opacity(0.7), Color.clear]),
                center: center, startRadius: 0, endRadius: radius))
        }
    }

    private func drawWormholes(_ context: inout GraphicsContext, art: GameArt, reducedMotion: Bool) {
        for worm in wormholes {
            let x = centerX + cos(worm.angle) * orbitRadius
            let y = centerY + sin(worm.angle) * orbitRadius
            let radius = playerRadius * 5
            if let ring = art.ring {
                context.drawLayer { layer in
                    layer.translateBy(x: x, y: y)
                    layer.rotate(by: .radians(Double(reducedMotion ? 0 : worm.pulse * 70)))
                    layer.drawUI(ring, in: CGRect(x: -radius, y: -radius, width: radius * 2, height: radius * 2))
                }
            }
            context.fill(circlePath(CGPoint(x: x, y: y), radius * 0.8), with: .radialGradient(
                Gradient(colors: [Color(hex: 0x1B0A3C), Palette.phase.opacity(0.5), Color.clear]),
                center: CGPoint(x: x, y: y), startRadius: 0, endRadius: radius))
        }
    }

    private func drawBosses(_ context: inout GraphicsContext, art: GameArt,
                            reducedMotion: Bool, colorblind: Bool) {
        for boss in boss {
            let x = centerX + cos(boss.angle) * orbitRadius
            let y = centerY + sin(boss.angle) * orbitRadius
            let radius = playerRadius * 6
            let pulse: CGFloat = reducedMotion ? 1 : 1 + 0.14 * sin(boss.time * 5)
            let center = CGPoint(x: x, y: y)
            let kindColor: Color = boss.kind % 3 == 1 ? Palette.orb : (boss.kind % 3 == 2 ? Palette.danger : Palette.boss)
            let bossColor: Color = colorblind ? RenderColors.hazardHot : kindColor
            context.drawGlow(center, radius: radius * 1.5 * pulse, color: bossColor, alpha: 0.35)
            context.fill(circlePath(center, radius * pulse), with: .radialGradient(
                Gradient(colors: [boss.hitFlash > 0 ? Color.white : Color(hex: 0x3A0A24), bossColor.opacity(0.85)]),
                center: center, startRadius: 0, endRadius: radius))
            context.fill(circlePath(center, radius * 0.35), with: .color(Color.white.opacity(0.8)))
            let arcRadius = radius * 1.35
            var arc = Path()
            let hpMax = CGFloat(3 + level / 15)
            arc.addArc(center: center, radius: arcRadius,
                       startAngle: .degrees(-90),
                       endAngle: .degrees(-90 + 360 * Double(CGFloat(boss.hp) / max(1, hpMax))),
                       clockwise: false)
            context.stroke(arc, with: .color(bossColor), style: StrokeStyle(lineWidth: 3, lineCap: .round))
            drawHazardMarker(&context, center: center, radius: radius * 1.1, colorblind: colorblind)
        }
    }

    private func drawTrail(_ context: inout GraphicsContext, art: GameArt, style: Int,
                           visualTime: CGFloat) {
        for (index, point) in trail.enumerated() {
            let life = min(max(point.life, 0), 1)
            let color: Color
            if style == 2 {
                color = Color(hue: Double(((visualTime * 90 + CGFloat(index) * 12).truncatingRemainder(dividingBy: 360)) / 360),
                              saturation: 0.75, brightness: 1)
            } else { color = point.color }
            let radius: CGFloat
            switch style {
            case 1: radius = playerRadius * (0.6 + life * 1.5)
            case 3: radius = playerRadius * (0.4 + life * 0.9) *
                        (1 + 0.35 * sin(CGFloat(index) * 0.8 + visualTime * 9))
            default: radius = playerRadius * (0.4 + life * 0.8)
            }
            // Как на Android: только мягкое свечение, без дополнительного круглого ядра.
            context.drawGlow(CGPoint(x: point.x, y: point.y), radius: radius,
                             color: color, alpha: Double(life * (style == 1 ? 0.5 : 0.35)))
        }
    }

    private func drawOrbs(_ context: inout GraphicsContext, art: GameArt,
                          reducedMotion: Bool, colorblind: Bool) {
        for orb in orbs {
            let pulse: CGFloat = reducedMotion ? 1 : 1 + 0.12 * sin(orb.pulse)
            let x = centerX + cos(orb.angle) * orbitRadius
            let y = centerY + sin(orb.angle) * orbitRadius
            let radius = playerRadius * (orb.kind == .artifact ? 2.2 : 1.7) * pulse
            let center = CGPoint(x: x, y: y)
            let haloColor = orb.kind == .normal ? Palette.orb : Color(hex: 0xFFFFF3B0)
            context.drawGlow(center, radius: radius * 1.8, color: haloColor, alpha: 0.4)
            let sprite = art.orbs[orb.sprite % art.orbs.count]
            context.drawUI(sprite, in: CGRect(x: x - radius, y: y - radius, width: radius * 2, height: radius * 2))
            let shape: Int = orb.kind == .artifact ? 2 : (orb.kind == .golden ? 1 : 0)
            drawBonusMarker(&context, center: center, radius: radius * 0.9,
                            shape: shape, colorblind: colorblind)
        }
    }

    private func drawPowerUps(_ context: inout GraphicsContext, art: GameArt,
                              reducedMotion: Bool, colorblind: Bool) {
        for power in powerUps {
            let x = centerX + cos(power.angle) * orbitRadius
            let y = centerY + sin(power.angle) * orbitRadius
            let radius = playerRadius * 2.5 * (reducedMotion ? 1 : 1 + 0.09 * sin(power.pulse))
            let index = allPowerKinds.firstIndex(of: power.kind) ?? 0
            let sprite = art.powers[index % art.powers.count]
            let center = CGPoint(x: x, y: y)
            context.drawGlow(center, radius: radius * 1.5, color: Palette.shield, alpha: 0.28)
            context.drawLayer { layer in
                layer.translateBy(x: x, y: y)
                layer.rotate(by: .radians(Double(reducedMotion ? 0 : sin(power.pulse * 0.5) * 8)))
                layer.drawUI(sprite, in: CGRect(x: -radius, y: -radius, width: radius * 2, height: radius * 2))
            }
            drawBonusMarker(&context, center: center, radius: radius * 0.85,
                            shape: index % 2 == 0 ? 1 : 0, colorblind: colorblind)
        }
    }

    private var allPowerKinds: [PowerUpKind] { [.shield, .slow, .double, .magnet, .phase, .freeze, .revive] }

    private func drawAsteroids(_ context: inout GraphicsContext, art: GameArt,
                               reducedMotion: Bool, colorblind: Bool) {
        for asteroid in asteroids {
            let x = centerX + cos(asteroid.angle) * asteroid.distance
            let y = centerY + sin(asteroid.angle) * asteroid.distance
            let center = CGPoint(x: x, y: y)
            // Тонкий коридор показывает, что угроза летит на свою точку орбиты.
            if asteroid.distance > orbitRadius * 0.9 && asteroid.kind != .bolt {
                let impact = CGPoint(x: centerX + cos(asteroid.angle) * orbitRadius,
                                     y: centerY + sin(asteroid.angle) * orbitRadius)
                var lane = Path()
                lane.move(to: impact)
                lane.addLine(to: center)
                context.stroke(lane, with: .linearGradient(
                    Gradient(colors: [Color.clear, (colorblind ? RenderColors.hazardHot : Palette.danger).opacity(0.13)]),
                    startPoint: impact, endPoint: center),
                    style: StrokeStyle(lineWidth: 1))
            }
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
                    Gradient(colors: [RenderColors.hazard.opacity(0.85), Color.clear]),
                    startPoint: start, endPoint: end),
                    style: StrokeStyle(lineWidth: asteroid.radius * 0.9, lineCap: .round))
                let sprite = art.comets[asteroid.sprite % art.comets.count]
                let d = asteroid.radius * 3.6
                context.drawLayer { layer in
                    layer.translateBy(x: x, y: y)
                    layer.rotate(by: .radians(Double(asteroid.angle)))
                    layer.drawUI(sprite, in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d))
                }
                drawHazardMarker(&context, center: center, radius: asteroid.radius * 1.8,
                                 colorblind: colorblind)
            case .bolt:
                let dx = cos(asteroid.angle)
                let dy = sin(asteroid.angle)
                var bolt = Path()
                bolt.move(to: CGPoint(x: x + dx * asteroid.radius * 4, y: y + dy * asteroid.radius * 4))
                bolt.addLine(to: CGPoint(x: x - dx * asteroid.radius * 4, y: y - dy * asteroid.radius * 4))
                context.stroke(bolt, with: .color((colorblind ? RenderColors.hazardDark : Palette.boss).opacity(0.9)),
                               style: StrokeStyle(lineWidth: asteroid.radius * (colorblind ? 1.6 : 1.2),
                                                  lineCap: .round))
                drawHazardMarker(&context, center: center, radius: asteroid.radius * 1.5,
                                 colorblind: colorblind)
            case .mine, .rock:
                let sprite = art.asteroids[asteroid.sprite % art.asteroids.count]
                let d = asteroid.radius * 3.6
                context.drawLayer { layer in
                    layer.translateBy(x: x, y: y)
                    layer.rotate(by: .radians(Double(asteroid.rotation)))
                    layer.drawUI(sprite, in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d))
                }
                if asteroid.kind == .mine {
                    let alpha: CGFloat = reducedMotion ? 0.9 : 0.75 + 0.25 * sin(asteroid.pulse * 3)
                    let mineColor = colorblind ? RenderColors.hazardDark : Palette.danger
                    context.stroke(circlePath(center, asteroid.radius * 0.75),
                                   with: .color(mineColor.opacity(alpha)),
                                   lineWidth: asteroid.radius * 0.3)
                    context.fill(circlePath(center, asteroid.radius * 0.3), with: .color(mineColor))
                }
                drawHazardMarker(&context, center: center, radius: asteroid.radius * 1.8,
                                 colorblind: colorblind)
            }
        }
    }

    private func drawPlayer(_ context: inout GraphicsContext, art: GameArt, shipSprite: Int,
                            visualTime: CGFloat, reducedMotion: Bool) {
        let position = CGPoint(x: playerX, y: playerY)
        let alpha: CGFloat = (invulnerable > 0 && !reducedMotion)
            ? 0.4 + 0.6 * abs(sin(visualTime * 22)) : 1
        context.drawGlow(position, radius: playerRadius * 4.5, color: skinColor,
                         alpha: Double(0.35 * alpha))
        if shield || phaseTimer > 0 || freezeTimer > 0 {
            let color = phaseTimer > 0 ? Palette.phase : (freezeTimer > 0 ? Palette.freeze : Palette.shield)
            let pulse: CGFloat = reducedMotion ? 1 : 1 + 0.08 * sin(visualTime * 6)
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
        for particle in particles {
            let life = min(max(particle.life / particle.maxLife, 0), 1)
            let radius = particle.size * (1.4 + life)
            context.drawGlow(CGPoint(x: particle.x, y: particle.y), radius: radius,
                             color: particle.color, alpha: Double(life * 0.85))
        }
    }
}

// MARK: - Menu backgrounds

extension GameArt {
    /// Фон меню: тёплый «уличный» постер вместо однотонного неонового космоса.
    func drawMenuBackground(in context: inout GraphicsContext, size: CGSize, style: Int,
                            tick: Int, reducedMotion: Bool) {
        let w = size.width
        let h = size.height
        let paper = Color(hex: 0xFFF6E8)
        let full = Path(CGRect(origin: .zero, size: size))
        context.fill(full, with: .linearGradient(
            Gradient(colors: [Color(hex: 0xFF8A3D), Color(hex: 0xD3457E),
                              Color(hex: 0x5B2E86), Color(hex: 0x12162C)]),
            startPoint: CGPoint(x: w / 2, y: 0), endPoint: CGPoint(x: w / 2, y: h)))

        let sunX = w * 0.78
        let sunY = h * 0.19
        let sunR = min(w, h) * 0.155
        context.fill(Path(ellipseIn: CGRect(x: sunX - sunR, y: sunY - sunR,
                                            width: sunR * 2, height: sunR * 2)),
                     with: .color(Color(hex: 0xFFD34A)))
        let stripR = sunR * 0.66
        context.fill(Path(ellipseIn: CGRect(x: sunX - stripR, y: sunY + sunR * 0.28 - stripR,
                                            width: stripR * 2, height: stripR * 2)),
                     with: .color(Color(hex: 0xFF5B3D).opacity(0.55)))
        let ringR = sunR * 1.32
        context.stroke(Path(ellipseIn: CGRect(x: sunX - ringR, y: sunY - ringR,
                                              width: ringR * 2, height: ringR * 2)),
                       with: .color(paper.opacity(0.35)), lineWidth: 2)

        for row in 0..<15 {
            for col in 0..<10 {
                let x = w * 0.55 + CGFloat(col) * w * 0.052
                let y = h * 0.06 + CGFloat(row) * h * 0.052
                context.fill(Path(ellipseIn: CGRect(x: x - 1.6, y: y - 1.6, width: 3.2, height: 3.2)),
                             with: .color(paper.opacity(0.10)))
            }
        }

        context.drawLayer { layer in
            layer.rotate(by: .degrees(-18))
            for i in 0..<8 {
                let color = i % 2 == 0 ? Color(hex: 0xD8FF3E).opacity(0.14)
                                       : Color(hex: 0xFF5B3D).opacity(0.16)
                let rect = CGRect(x: -w * 0.2 + CGFloat(i) * 58, y: h * 0.62, width: 20, height: h * 0.7)
                layer.fill(Path(rect), with: .color(color))
            }
        }
        for i in 0..<12 {
            let y = h * (0.09 + CGFloat(i) * 0.061)
            let line = Path { path in
                path.move(to: CGPoint(x: w * 0.04, y: y))
                path.addLine(to: CGPoint(x: w * (0.20 + CGFloat(i % 4) * 0.09), y: y - 10))
            }
            context.stroke(line, with: .color(paper.opacity(0.13)), lineWidth: 2)
        }

        let blocks: [CGFloat] = [0.10, 0.07, 0.13, 0.09, 0.16, 0.08, 0.12, 0.10, 0.15, 0.09]
        var x = w * 0.03
        let baseY = h * 0.94
        for (i, rel) in blocks.enumerated() {
            let bw = w * rel
            let bh = h * (0.055 + CGFloat(i % 3) * 0.022)
            context.fill(Path(CGRect(x: x, y: baseY - bh, width: bw, height: bh)),
                         with: .color(Color(hex: 0x0A0E1E).opacity(0.62)))
            for row in 0..<2 {
                for col in 0..<3 {
                    context.fill(Path(CGRect(x: x + bw * (0.16 + CGFloat(col) * 0.26),
                                             y: baseY - bh + bh * (0.25 + CGFloat(row) * 0.35),
                                             width: 3, height: 5)),
                                 with: .color(paper.opacity(0.18)))
                }
            }
            x += bw + w * 0.012
        }

        context.fill(Path(CGRect(x: 0, y: h * 0.52, width: w, height: h * 0.48)),
                     with: .linearGradient(
                        Gradient(colors: [Color.clear, Color(hex: 0x101528).opacity(0.90)]),
                        startPoint: CGPoint(x: w / 2, y: h * 0.52), endPoint: CGPoint(x: w / 2, y: h)))
    }

    func drawPlanetPreview(in context: inout GraphicsContext, index: Int, center: CGPoint,
                           radius: CGFloat, tick: Int, colorblind: Bool, reducedMotion: Bool) {
        guard !planets.isEmpty else { return }
        let image = planets[index % planets.count]
        let d = radius * 2
        context.drawLayer { layer in
            layer.translateBy(x: center.x, y: center.y)
            layer.rotate(by: .radians(reducedMotion ? 0 : Double(tick) * 0.01))
            layer.drawUI(image, in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d))
        }
        if colorblind {
            context.stroke(Path(ellipseIn: CGRect(x: center.x - radius, y: center.y - radius,
                                                  width: radius * 2, height: radius * 2)),
                           with: .color(RenderColors.bonusBlue), lineWidth: 2)
        }
    }

    /// Живая открытка главного экрана: планета-скин, орбита и корабль в полёте.
    func drawHubHero(in context: inout GraphicsContext, size: CGSize, planetIndex: Int,
                     shipSprite: Int, skinColor: Color, tick: Int,
                     colorblind: Bool, reducedMotion: Bool) {
        guard !planets.isEmpty, !ships.isEmpty else { return }
        let cx = size.width / 2
        let cy = size.height / 2
        let r = min(size.width, size.height) * 0.36
        context.drawGlow(CGPoint(x: cx, y: cy), radius: r * 1.35, color: skinColor, alpha: 0.10)
        context.stroke(Path(ellipseIn: CGRect(x: cx - r, y: cy - r, width: r * 2, height: r * 2)),
                       with: .color(skinColor.opacity(0.20)),
                       style: StrokeStyle(lineWidth: 1.5, dash: [8, 12]))
        context.stroke(Path(ellipseIn: CGRect(x: cx - r * 0.68, y: cy - r * 0.68,
                                              width: r * 1.36, height: r * 1.36)),
                       with: .color(Color.white.opacity(0.06)), lineWidth: 1)
        drawPlanetPreview(in: &context, index: planetIndex,
                          center: CGPoint(x: cx, y: cy), radius: r * 0.42,
                          tick: tick, colorblind: colorblind, reducedMotion: reducedMotion)
        let phase: CGFloat = reducedMotion ? -CGFloat.pi / 2 : -CGFloat.pi / 2 + CGFloat(tick) * 0.014
        let px = cx + cos(phase) * r
        let py = cy + sin(phase) * r
        context.drawGlow(CGPoint(x: px, y: py), radius: r * 0.26, color: skinColor, alpha: 0.45)
        let d = r * 0.24
        context.drawLayer { layer in
            layer.translateBy(x: px, y: py)
            layer.rotate(by: .radians(Double(phase + CGFloat.pi)))
            layer.drawUI(ships[shipSprite % ships.count],
                         in: CGRect(x: -d / 2, y: -d / 2, width: d, height: d))
        }
        for i in 1...3 {
            let a = phase + CGFloat(i) * CGFloat.pi / 2
            context.fill(Path(ellipseIn: CGRect(x: cx + cos(a) * r - r * 0.026,
                                                y: cy + sin(a) * r - r * 0.026,
                                                width: r * 0.052, height: r * 0.052)),
                         with: .color(Palette.orb.opacity(0.55)))
        }
    }
}
