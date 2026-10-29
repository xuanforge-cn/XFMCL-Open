/**
 * LauncherState.kt - 界面状态与操作编排
 * 玹砺科技 XUANFORGE
 *
 * Android 端定位：版本下载与文件管理；不启动 Java 版游戏。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.ui

import android.content.Context
import android.os.Handler
import android.os.Looper
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import cn.xuanforge.xfmclopen.data.DownloadSource
import cn.xuanforge.xfmclopen.data.Endpoints
import cn.xuanforge.xfmclopen.data.GameInstaller
import cn.xuanforge.xfmclopen.data.GamePaths
import cn.xuanforge.xfmclopen.data.ManifestVersion
import cn.xuanforge.xfmclopen.data.Mirror
import cn.xuanforge.xfmclopen.data.OfflineIdentity
import cn.xuanforge.xfmclopen.data.VersionRepository
import cn.xuanforge.xfmclopen.data.VersionRow
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import java.util.Locale

class LauncherState(context: Context) {

    private val prefs = context.getSharedPreferences("xfmcl_open", Context.MODE_PRIVATE)
    private val handler = Handler(Looper.getMainLooper())
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)

    val paths = GamePaths(context)
    val endpoints = Endpoints.load(context)

    var playerName by mutableStateOf(prefs.getString("name", "Player") ?: "Player")
    var source by mutableStateOf(
        runCatching { DownloadSource.valueOf(prefs.getString("source", DownloadSource.OFFICIAL.name)!!) }
            .getOrDefault(DownloadSource.OFFICIAL)
    )

    var versions by mutableStateOf<List<VersionRow>>(emptyList())
    var filter by mutableStateOf("")
    var selectedId by mutableStateOf<String?>(null)
    var status by mutableStateOf("准备就绪")
    var progress by mutableStateOf(0f)
    var progressText by mutableStateOf("")
    var log by mutableStateOf("")
    var busy by mutableStateOf(false)
    var indeterminate by mutableStateOf(false)

    private var manifest: List<ManifestVersion> = emptyList()
    private var job: Job? = null
    private var started = false

    val filteredVersions: List<VersionRow>
        get() = if (filter.isBlank()) versions else versions.filter { it.id.contains(filter.trim(), ignoreCase = true) }

    val selectedVersion: VersionRow?
        get() = versions.firstOrNull { it.id == selectedId }

    fun start() {
        if (started) {
            return
        }

        started = true
        job = scope.launch { loadVersions(force = false) }
    }

    fun refresh() {
        if (busy) {
            return
        }

        job = scope.launch { loadVersions(force = true) }
    }

    fun saveSettings() {
        prefs.edit()
            .putString("name", playerName)
            .putString("source", source.name)
            .apply()
    }

    fun download() {
        if (busy) {
            return
        }

        if (!endpoints.isConfigured) {
            status = "尚未配置下载端点"
            appendLog("请填写 assets/endpoints.json 后重新打开应用。")
            return
        }

        val version = selectedVersion ?: return
        val name = playerName.trim()
        if (!OfflineIdentity.isValid(name)) {
            status = "玩家名需为 3-16 位字母、数字或下划线"
            return
        }

        playerName = name
        saveSettings()

        job = scope.launch {
            busy = true
            indeterminate = false
            progress = 0f
            progressText = ""
            status = "正在准备 ${version.id}…"

            try {
                val mirror = Mirror(endpoints, source)
                val repository = VersionRepository(mirror, paths) { appendLog(it) }
                val entry = manifest.firstOrNull { it.id == version.id }
                    ?: throw IllegalStateException("版本 ${version.id} 不在清单中。")

                val detail = repository.fetchVersion(entry)
                val installer = GameInstaller(paths, mirror) { appendLog(it) }
                installer.install(detail) { p ->
                    val ratio = if (p.total > 0) p.completed.toFloat() / p.total else 0f
                    handler.post {
                        progress = ratio
                        progressText = "${p.stage} ${p.completed}/${p.total} · ${formatBytes(p.bytes)}"
                        status = "正在下载 ${version.id} · ${p.stage}…"
                    }
                }

                versions = versions.map { if (it.id == version.id) it.copy(installed = true) else it }
                progress = 1f
                status = "${version.id} 下载完成"
            } catch (e: CancellationException) {
                status = "已取消"
                appendLog("任务已取消。")
                throw e
            } catch (e: Exception) {
                status = "下载失败"
                appendLog(e.message ?: e.toString())
            } finally {
                busy = false
            }
        }
    }

    fun cancel() {
        job?.cancel()
        status = "正在取消…"
    }

    fun appendLog(line: String) {
        handler.post {
            val lines = (log + line + "\n").lines().takeLast(150)
            log = lines.joinToString("\n")
        }
    }

    private suspend fun loadVersions(force: Boolean) {
        if (!endpoints.isConfigured) {
            status = "尚未配置下载端点"
            appendLog("请填写 assets/endpoints.json 中的 manifestUrl 与 mirrorRoot。")
            return
        }

        busy = true
        indeterminate = true
        status = "正在读取版本清单…"

        try {
            val repository = VersionRepository(Mirror(endpoints, source), paths) { appendLog(it) }
            val list = repository.fetchManifest(force)
            manifest = list
            versions = list.map {
                VersionRow(
                    id = it.id,
                    type = it.type,
                    date = it.releaseTime.take(10),
                    installed = paths.versionJson(it.id).exists()
                )
            }
            selectedId = list.firstOrNull { it.id == repository.latestRelease }?.id ?: list.firstOrNull()?.id
            status = "共 ${versions.size} 个版本"
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            status = "版本清单获取失败"
            appendLog(e.message ?: e.toString())
        } finally {
            busy = false
            indeterminate = false
        }
    }

    private fun formatBytes(bytes: Long): String = when {
        bytes < 1024 -> "$bytes B"
        bytes < 1024 * 1024 -> String.format(Locale.US, "%.1f KB", bytes / 1024.0)
        else -> String.format(Locale.US, "%.1f MB", bytes / (1024.0 * 1024.0))
    }
}
