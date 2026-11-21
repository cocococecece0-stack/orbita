import Foundation

/// Результат одного шага в режиме «ШТАБЕЛЬ». Зеркально совпадает с Android BlockStep.
struct BlockStep {
    var cleared: Int = 0
    var dropped: Int = 0
    var locked: Bool = false
    var toppedOut: Bool = false
}

/// Детерминированный тетро-движок, общий контракт с Android BlockArena.
final class BlockArena {
    static let cols = 10
    static let rows = 18
    private static let types = 7

    private(set) var grid = [Int](repeating: 0, count: cols * rows)
    private(set) var next: [Int] = []
    private(set) var clearRows = [Bool](repeating: false, count: rows)

    private(set) var piece = 0
    private(set) var rotation = 0
    private(set) var x = 0
    private(set) var y = 0
    private(set) var held = -1
    private(set) var canHold = true
    private(set) var lines = 0
    private(set) var level = 1
    private(set) var clearFlash: CGFloat = 0
    private(set) var topOut = false

    private var fallTimer: CGFloat = 0
    private var random: () -> CGFloat = { 0.5 }
    private var bag: [Int] = []

    private let shapes = [
        [0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0],
        [0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0],
    ]

    func reset(_ randomSource: @escaping () -> CGFloat) {
        random = randomSource
        grid = [Int](repeating: 0, count: BlockArena.cols * BlockArena.rows)
        clearRows = [Bool](repeating: false, count: BlockArena.rows)
        bag.removeAll()
        next.removeAll()
        piece = 0
        rotation = 0
        x = 0
        y = 0
        held = -1
        canHold = true
        lines = 0
        level = 1
        clearFlash = 0
        topOut = false
        fallTimer = 0
        for _ in 0..<4 { next.append(nextType()) }
        spawn()
    }

    func cell(_ col: Int, _ row: Int) -> Int {
        guard col >= 0, col < BlockArena.cols, row >= 0, row < BlockArena.rows else { return 0 }
        return grid[row * BlockArena.cols + col]
    }

    /// Клетки фигуры в координатах 4×4, нормализованные к левому верхнему углу.
    func cells(_ type: Int, _ rot: Int) -> [(Int, Int)] {
        let t = max(0, min(type, BlockArena.types - 1))
        var current = shapes[t]
        if t != 1 {
            for _ in 0..<((rot % 4 + 4) % 4) {
                var turned = [Int](repeating: 0, count: 16)
                for yy in 0..<4 {
                    for xx in 0..<4 {
                        let value = current[yy * 4 + xx]
                        if value != 0 { turned[xx * 4 + (3 - yy)] = value }
                    }
                }
                current = turned
            }
        }
        var minX = 4
        var minY = 4
        for yy in 0..<4 {
            for xx in 0..<4 where current[yy * 4 + xx] != 0 {
                minX = min(minX, xx)
                minY = min(minY, yy)
            }
        }
        var result: [(Int, Int)] = []
        for yy in 0..<4 {
            for xx in 0..<4 where current[yy * 4 + xx] != 0 {
                result.append((xx - minX, yy - minY))
            }
        }
        return result
    }

    func canPlace(_ type: Int, _ rot: Int, _ px: Int, _ py: Int) -> Bool {
        for cell in cells(type, rot) {
            let cx = px + cell.0
            let cy = py + cell.1
            if cx < 0 || cx >= BlockArena.cols || cy >= BlockArena.rows { return false }
            if cy >= 0 && grid[cy * BlockArena.cols + cx] != 0 { return false }
        }
        return true
    }

    func ghostY() -> Int {
        var gy = y
        while canPlace(piece, rotation, x, gy + 1) { gy += 1 }
        return gy
    }

    @discardableResult
    func move(_ dx: Int) -> Bool {
        guard !topOut, canPlace(piece, rotation, x + dx, y) else { return false }
        x += dx
        return true
    }

    @discardableResult
    func rotate() -> Bool {
        guard !topOut else { return false }
        let nextRotation = (rotation + 1) % 4
        let kicks = [(0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0), (0, -1)]
        for kick in kicks {
            if canPlace(piece, nextRotation, x + kick.0, y + kick.1) {
                rotation = nextRotation
                x += kick.0
                y += kick.1
                return true
            }
        }
        return false
    }

    @discardableResult
    func softDrop() -> BlockStep {
        guard !topOut else { return BlockStep() }
        if canPlace(piece, rotation, x, y + 1) {
            y += 1
            fallTimer = 0
            return BlockStep()
        }
        return lockPiece(0)
    }

    @discardableResult
    func hardDrop() -> BlockStep {
        guard !topOut else { return BlockStep() }
        var distance = 0
        while canPlace(piece, rotation, x, y + 1) {
            y += 1
            distance += 1
        }
        return lockPiece(distance)
    }

    @discardableResult
    func hold() -> BlockStep {
        guard !topOut, canHold else { return BlockStep() }
        let current = piece
        if held < 0 {
            held = current
            spawn()
        } else {
            piece = held
            held = current
            placeAtSpawn()
        }
        canHold = false
        return BlockStep()
    }

    @discardableResult
    func update(_ dt: CGFloat) -> BlockStep {
        clearFlash = max(0, clearFlash - dt)
        guard !topOut else { return BlockStep() }
        fallTimer += dt
        let interval = max(0.075, 0.78 * pow(0.84, CGFloat(level - 1)))
        while fallTimer >= interval {
            fallTimer -= interval
            if canPlace(piece, rotation, x, y + 1) {
                y += 1
            } else {
                return lockPiece(0)
            }
        }
        return BlockStep()
    }

    private func lockPiece(_ dropped: Int) -> BlockStep {
        var overflow = false
        for cell in cells(piece, rotation) {
            let cx = x + cell.0
            let cy = y + cell.1
            if cy < 0 {
                overflow = true
            } else if cx >= 0, cx < BlockArena.cols, cy >= 0, cy < BlockArena.rows {
                grid[cy * BlockArena.cols + cx] = piece + 1
            }
        }
        clearRows = [Bool](repeating: false, count: BlockArena.rows)
        var cleared = 0
        for row in (0..<BlockArena.rows).reversed() {
            var full = true
            for col in 0..<BlockArena.cols where grid[row * BlockArena.cols + col] == 0 {
                full = false
                break
            }
            if full {
                clearRows[row] = true
                cleared += 1
            }
        }
        if cleared > 0 {
            var write = BlockArena.rows - 1
            for read in (0..<BlockArena.rows).reversed() {
                if clearRows[read] { continue }
                if write != read {
                    for col in 0..<BlockArena.cols {
                        grid[write * BlockArena.cols + col] = grid[read * BlockArena.cols + col]
                    }
                }
                write -= 1
            }
            while write >= 0 {
                for col in 0..<BlockArena.cols { grid[write * BlockArena.cols + col] = 0 }
                write -= 1
            }
            lines += cleared
            level = min(20, 1 + lines / 8)
        }
        clearFlash = cleared > 0 ? 0.42 : 0
        canHold = true
        spawn()
        return BlockStep(cleared: cleared, dropped: dropped, locked: true,
                         toppedOut: overflow || topOut)
    }

    private func spawn() {
        piece = next.removeFirst()
        while next.count < 4 { next.append(nextType()) }
        placeAtSpawn()
    }

    private func placeAtSpawn() {
        rotation = 0
        x = 3
        y = 0
        fallTimer = 0
        if !canPlace(piece, rotation, x, y) { topOut = true }
    }

    private func nextType() -> Int {
        if bag.isEmpty {
            bag = Array(0..<BlockArena.types)
            for i in stride(from: bag.count - 1, through: 1, by: -1) {
                let j = Int(random() * CGFloat(i + 1))
                bag.swapAt(i, min(max(j, 0), i))
            }
        }
        return bag.removeLast()
    }
}
