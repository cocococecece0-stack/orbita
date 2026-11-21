package com.example.orbit.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val OrbitColors = darkColorScheme(
    primary = Color(0xFF22D3EE),
    onPrimary = Color(0xFF04222A),
    secondary = Color(0xFFF472B6),
    tertiary = Color(0xFFFBBF24),
    background = Color(0xFF05030F),
    surface = Color(0xFF0F0A22),
    onBackground = Color(0xFFE8E6F5),
    onSurface = Color(0xFFE8E6F5),
)

@Composable
fun OrbitTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = OrbitColors, content = content)
}
