/**
 * Theme.kt - 深色主题
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

@Composable
fun XfmclTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = darkColorScheme(
            primary = Color(0xFF2F6BFF),
            onPrimary = Color.White,
            background = Color(0xFF0E1015),
            onBackground = Color(0xFFEEF2F9),
            surface = Color(0xFF151821),
            onSurface = Color(0xFFD6DDEA),
            surfaceVariant = Color(0xFF1E2431),
            onSurfaceVariant = Color(0xFF9AA4B8),
            outline = Color(0xFF2A3242)
        ),
        content = content
    )
}
