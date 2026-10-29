/**
 * GameInstaller.kt - 游戏文件补全（Android）
 * 玹砺科技 XUANFORGE
 *
 * Android 端只下载与平台无关的部分：client.jar、非 natives 游戏库、资源索引与资源文件。
 * natives 属于桌面系统二进制，留给桌面版启动器补齐。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.io.IOException

class GameInstaller(
    private val paths: GamePaths,
    private val mirror: Mirror,
    private val log: (String) -> Unit
) {
    private val official = Mirror(mirror.endpoints, DownloadSource.OFFICIAL)

    suspend fun install(detail: JSONObject, onProgress: (DownloadProgress) -> Unit) = withContext(Dispatchers.IO) {
        paths.ensureCreated()

        val tasks = mutableListOf<DownloadTask>()

        // 1. 客户端主 jar
        val client = detail.optJSONObject("downloads")?.optJSONObject("client")
        if (client != null) {
            val relative = client.optString("path").ifEmpty {
                "${detail.getString("id")}/${detail.getString("id")}.jar"
            }
            tasks += task(client.optString("url"), File(paths.versionsDir, relative), client)
        }

        // 2. 游戏库：跳过 natives（桌面系统二进制，Android 上无意义）
        val libraries = detail.optJSONArray("libraries") ?: JSONArray()
        for (index in 0 until libraries.length()) {
            val library = libraries.getJSONObject(index)
            if (library.has("natives")) {
                continue
            }

            val artifact = library.optJSONObject("downloads")?.optJSONObject("artifact") ?: continue
            val relative = artifact.optString("path").ifEmpty { mavenPath(library.optString("name")) }
            val url = artifact.optString("url").ifEmpty { mirror.libraryUrl(relative) }
            tasks += task(url, File(paths.librariesDir, relative), artifact)
        }

        // 3. 资源索引
        val indexRef = detail.optJSONObject("assetIndex")
            ?: throw IllegalStateException("该版本使用旧版资源结构（1.7 之前），暂不支持。")
        val indexId = indexRef.optString("id").ifEmpty { detail.optString("assets", "legacy") }
        val indexFile = File(paths.indexesDir, "$indexId.json")
        tasks += task(indexRef.optString("url"), indexFile, indexRef)

        log("检查游戏库与资源索引（${tasks.size} 项）…")
        val downloader = FileDownloader(log)
        val libraryFailures = downloader.downloadAll(tasks, 12) { completed, total, bytes ->
            onProgress(DownloadProgress("游戏库", completed, total, bytes))
        }

        if (libraryFailures.isNotEmpty()) {
            throw IOException("部分文件下载失败：${libraryFailures.take(3).joinToString("；")}")
        }

        // 4. 资源文件（按 hash 去重）
        if (!indexFile.exists()) {
            throw IOException("资源索引缺失。")
        }

        val objects = JSONObject(indexFile.readText()).getJSONObject("objects")
        val seen = HashSet<String>()
        val assetTasks = mutableListOf<DownloadTask>()

        objects.keys().forEach { key ->
            val obj = objects.getJSONObject(key)
            val hash = obj.getString("hash")
            if (seen.add(hash)) {
                assetTasks += DownloadTask(
                    primaryUrl = mirror.assetUrl(hash),
                    target = paths.assetObject(hash),
                    sha1 = hash,
                    size = obj.optLong("size", 0),
                    altUrl = official.assetUrl(hash)
                )
            }
        }

        log("检查资源文件（${assetTasks.size} 项）…")
        val assetFailures = downloader.downloadAll(assetTasks, 16) { completed, total, bytes ->
            onProgress(DownloadProgress("资源文件", completed, total, bytes))
        }

        if (assetFailures.isNotEmpty()) {
            throw IOException("部分资源下载失败：${assetFailures.take(3).joinToString("；")}")
        }

        log("全部文件已就绪。")
    }

    private fun task(url: String, target: File, artifact: JSONObject): DownloadTask {
        val primary = mirror.rewrite(url)
        return DownloadTask(
            primaryUrl = primary,
            target = target,
            sha1 = artifact.optString("sha1").ifEmpty { null },
            size = artifact.optLong("size", 0),
            altUrl = if (url.isNotEmpty()) official.rewrite(url) else null
        )
    }

    /** 把 Maven 坐标转换成仓库相对路径。 */
    private fun mavenPath(name: String): String {
        val parts = name.split(":")
        if (parts.size < 3) {
            return name.replace(':', '/')
        }

        val group = parts[0].replace('.', '/')
        val artifact = parts[1]
        val version = parts[2]
        val classifier = if (parts.size > 3) "-${parts[3]}" else ""
        return "$group/$artifact/$version/$artifact-$version$classifier.jar"
    }
}
