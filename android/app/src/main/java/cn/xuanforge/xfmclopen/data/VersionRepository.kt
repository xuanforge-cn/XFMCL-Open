/**
 * VersionRepository.kt - 版本清单与版本描述读取
 * 玹砺科技 XUANFORGE
 *
 * 下载顺序：所选下载源 → 官方源；本地 JSON 损坏时自动重下。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.io.IOException

class VersionRepository(
    private val mirror: Mirror,
    private val paths: GamePaths,
    private val log: (String) -> Unit
) {
    var latestRelease: String? = null
        private set

    suspend fun fetchManifest(force: Boolean): List<ManifestVersion> = withContext(Dispatchers.IO) {
        paths.ensureCreated()

        if (!force && paths.manifestFile.exists() && isFresh(paths.manifestFile)) {
            val cached = runCatching { parseManifest(paths.manifestFile.readText()) }.getOrNull()
            if (cached != null) {
                return@withContext cached
            }
        }

        val text = downloadWithFallback(mirror.endpoints.manifestUrl)
        paths.manifestFile.writeText(text)
        parseManifest(text)
    }

    suspend fun fetchVersion(version: ManifestVersion): JSONObject = withContext(Dispatchers.IO) {
        val target = paths.versionJson(version.id)

        if (target.exists()) {
            val cached = runCatching { JSONObject(target.readText()) }.getOrNull()
            if (cached != null && isUsable(cached)) {
                return@withContext cached
            }

            target.delete()
        }

        val text = downloadWithFallback(version.url)
        target.parentFile?.mkdirs()
        target.writeText(text)

        val detail = JSONObject(text)
        if (!isUsable(detail)) {
            throw IOException("版本 ${version.id} 描述文件解析失败。")
        }

        detail
    }

    private fun parseManifest(text: String): List<ManifestVersion> {
        val root = JSONObject(text)
        latestRelease = root.optJSONObject("latest")?.optString("release")
        val array = root.getJSONArray("versions")

        return (0 until array.length()).map { index ->
            val item = array.getJSONObject(index)
            ManifestVersion(
                id = item.getString("id"),
                type = item.optString("type"),
                url = item.getString("url"),
                releaseTime = item.optString("releaseTime")
            )
        }
    }

    private fun isUsable(detail: JSONObject): Boolean =
        detail.optString("mainClass").isNotEmpty() || detail.optString("inheritsFrom").isNotEmpty()

    private fun isFresh(file: java.io.File): Boolean =
        System.currentTimeMillis() - file.lastModified() < 30 * 60_000L

    private fun downloadWithFallback(url: String): String {
        val official = Mirror(mirror.endpoints, DownloadSource.OFFICIAL).rewrite(url)
        val candidates = listOf(mirror.rewrite(url), official).distinct()
        var last: Exception? = null

        for (candidate in candidates) {
            try {
                return Http.getText(candidate)
            } catch (e: Exception) {
                last = e
            }
        }

        throw IOException("下载失败：${last?.message}", last)
    }
}
