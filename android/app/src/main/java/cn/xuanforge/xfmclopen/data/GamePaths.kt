/**
 * GamePaths.kt - 游戏目录约定
 * 玹砺科技 XUANFORGE
 *
 * 文件保存在应用专属外部目录，无需存储权限，卸载应用时一并清除。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import android.content.Context
import java.io.File

class GamePaths(context: Context) {

    /** 应用专属目录下的 minecraft 根目录。 */
    val root: File = File(context.getExternalFilesDir(null) ?: context.filesDir, "minecraft")

    val versionsDir = File(root, "versions")
    val librariesDir = File(root, "libraries")
    val assetsDir = File(root, "assets")
    val indexesDir = File(assetsDir, "indexes")
    val objectsDir = File(assetsDir, "objects")
    val manifestFile = File(versionsDir, "version_manifest_v2.json")

    fun versionJson(id: String): File = File(File(versionsDir, id), "$id.json")

    fun assetObject(hash: String): File = File(objectsDir, "${hash.take(2)}/$hash")

    fun ensureCreated() {
        versionsDir.mkdirs()
        librariesDir.mkdirs()
        indexesDir.mkdirs()
        objectsDir.mkdirs()
    }
}
