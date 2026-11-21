package com.example.orbit.game

import kotlin.math.max
import kotlin.math.pow

/** Результат одного шага в режиме «ШТАБЕЛЬ»: сколько строк закрыто и на сколько опущен блок. */
data class BlockStep(
    val cleared: Int = 0,
    val dropped: Int = 0,
    val locked: Boolean = false,
    val toppedOut: Boolean = false,
)

/**
 * Детерминированный тетро-движок. Не знает про орбиту, сохранение и UI:
 * движок игры вызывает шаги и сам начисляет очки/события.
 */
class BlockArena {
    companion object {
        const val COLS = 10
        const val ROWS = 18
        private const val TYPES = 7
    }

    val grid = IntArray(COLS * ROWS)
    val next = mutableListOf<Int>()
    val clearRows = BooleanArray(ROWS)

    var piece = 0
        private set
    var rotation = 0
        private set
    var x = 0
        private set
    var y = 0
        private set
    var held = -1
        private set
    var canHold = true
        private set
    var lines = 0
        private set
    var level = 1
        private set
    var clearFlash = 0f
        private set
    var topOut = false
        private set

    private var fallTimer = 0f
    private var random: () -> Float = { 0.5f }
    private val bag = mutableListOf<Int>()

    private val shapes = arrayOf(
        intArrayOf(0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0),
        intArrayOf(0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0),
        intArrayOf(0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 0, 0, 0, 0),
    )

    fun reset(randomSource: () -> Float) {
        random = randomSource
        grid.fill(0)
        clearRows.fill(false)
        bag.clear()
        next.clear()
        piece = 0
        rotation = 0
        x = 0
        y = 0
        held = -1
        canHold = true
        lines = 0
        level = 1
        clearFlash = 0f
        topOut = false
        fallTimer = 0f
        repeat(4) { next += nextType() }
        spawn()
    }

    fun cell(col: Int, row: Int): Int = if (col in 0 until COLS && row in 0 until ROWS) grid[row * COLS + col] else 0

    /** Клетки фигуры в координатах 4×4, нормализованные к левому верхнему углу. */
    fun cells(type: Int, rot: Int): List<IntArray> {
        val t = type.coerceIn(0, TYPES - 1)
        var current = shapes[t].copyOf()
        if (t != 1) {
            repeat(((rot % 4) + 4) % 4) {
                val turned = IntArray(16)
                for (yy in 0..3) {
                    for (xx in 0..3) {
                        val value = current[yy * 4 + xx]
                        if (value != 0) turned[xx * 4 + (3 - yy)] = value
                    }
                }
                current = turned
            }
        }
        var minX = 4
        var minY = 4
        for (yy in 0..3) {
            for (xx in 0..3) {
                if (current[yy * 4 + xx] == 0) continue
                minX = minOf(minX, xx)
                minY = minOf(minY, yy)
            }
        }
        val cells = mutableListOf<IntArray>()
        for (yy in 0..3) {
            for (xx in 0..3) {
                if (current[yy * 4 + xx] != 0) cells += intArrayOf(xx - minX, yy - minY)
            }
        }
        return cells
    }

    fun canPlace(type: Int, rot: Int, px: Int, py: Int): Boolean {
        for (cell in cells(type, rot)) {
            val cx = px + cell[0]
            val cy = py + cell[1]
            if (cx < 0 || cx >= COLS || cy >= ROWS) return false
            if (cy >= 0 && grid[cy * COLS + cx] != 0) return false
        }
        return true
    }

    fun ghostY(): Int {
        var gy = y
        while (canPlace(piece, rotation, x, gy + 1)) gy++
        return gy
    }

    fun move(dx: Int): Boolean {
        if (topOut) return false
        if (!canPlace(piece, rotation, x + dx, y)) return false
        x += dx
        return true
    }

    fun rotate(): Boolean {
        if (topOut) return false
        val nextRotation = (rotation + 1) % 4
        val kicks = arrayOf(intArrayOf(0, 0), intArrayOf(-1, 0), intArrayOf(1, 0),
            intArrayOf(-2, 0), intArrayOf(2, 0), intArrayOf(0, -1))
        for (kick in kicks) {
            if (canPlace(piece, nextRotation, x + kick[0], y + kick[1])) {
                rotation = nextRotation
                x += kick[0]
                y += kick[1]
                return true
            }
        }
        return false
    }

    fun softDrop(): BlockStep {
        if (topOut) return BlockStep()
        if (canPlace(piece, rotation, x, y + 1)) {
            y++
            fallTimer = 0f
            return BlockStep()
        }
        return lockPiece(0)
    }

    fun hardDrop(): BlockStep {
        if (topOut) return BlockStep()
        var distance = 0
        while (canPlace(piece, rotation, x, y + 1)) {
            y++
            distance++
        }
        return lockPiece(distance)
    }

    fun hold(): BlockStep {
        if (topOut || !canHold) return BlockStep()
        val current = piece
        if (held < 0) {
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

    fun update(dt: Float): BlockStep {
        clearFlash = max(0f, clearFlash - dt)
        if (topOut) return BlockStep()
        fallTimer += dt
        val interval = max(0.075f, 0.78f * 0.84f.pow(level - 1))
        while (fallTimer >= interval) {
            fallTimer -= interval
            if (canPlace(piece, rotation, x, y + 1)) {
                y++
            } else {
                return lockPiece(0)
            }
        }
        return BlockStep()
    }

    private fun lockPiece(dropped: Int): BlockStep {
        var overflow = false
        for (cell in cells(piece, rotation)) {
            val cx = x + cell[0]
            val cy = y + cell[1]
            if (cy < 0) {
                overflow = true
            } else if (cx in 0 until COLS && cy in 0 until ROWS) {
                grid[cy * COLS + cx] = piece + 1
            }
        }
        clearRows.fill(false)
        var cleared = 0
        for (row in ROWS - 1 downTo 0) {
            var full = true
            for (col in 0 until COLS) {
                if (grid[row * COLS + col] == 0) { full = false; break }
            }
            if (full) { clearRows[row] = true; cleared++ }
        }
        if (cleared > 0) {
            var write = ROWS - 1
            for (read in ROWS - 1 downTo 0) {
                if (clearRows[read]) continue
                if (write != read) {
                    for (col in 0 until COLS) grid[write * COLS + col] = grid[read * COLS + col]
                }
                write--
            }
            while (write >= 0) {
                for (col in 0 until COLS) grid[write * COLS + col] = 0
                write--
            }
            lines += cleared
            level = minOf(20, 1 + lines / 8)
        }
        clearFlash = if (cleared > 0) 0.42f else 0f
        canHold = true
        spawn()
        return BlockStep(cleared = cleared, dropped = dropped, locked = true,
            toppedOut = overflow || topOut)
    }

    private fun spawn() {
        piece = next.removeAt(0)
        while (next.size < 4) next += nextType()
        placeAtSpawn()
    }

    private fun placeAtSpawn() {
        rotation = 0
        x = 3
        y = 0
        fallTimer = 0f
        if (!canPlace(piece, rotation, x, y)) topOut = true
    }

    private fun nextType(): Int {
        if (bag.isEmpty()) {
            bag.addAll(0 until TYPES)
            for (i in bag.lastIndex downTo 1) {
                val j = (random() * (i + 1)).toInt().coerceIn(0, i)
                val tmp = bag[i]
                bag[i] = bag[j]
                bag[j] = tmp
            }
        }
        return bag.removeAt(bag.lastIndex)
    }
}
