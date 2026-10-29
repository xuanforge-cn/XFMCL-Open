/**
 * MainActivity.kt - 应用入口
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import cn.xuanforge.xfmclopen.ui.LauncherScreen
import cn.xuanforge.xfmclopen.ui.XfmclTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            XfmclTheme {
                LauncherScreen()
            }
        }
    }
}
