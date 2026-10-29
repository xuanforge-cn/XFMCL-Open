/**
 * WebLauncherActivity.kt - HTML 模板宿主
 * 玹砺科技 XUANFORGE
 *
 * 用 WebView 承载 templates/html 模板；页面只做界面，
 * 下载、校验与进度全部复用现有 Kotlin 下载器（VersionRepository / GameInstaller）。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen

import android.annotation.SuppressLint
import android.content.Context
import android.os.Bundle
import android.webkit.JavascriptInterface
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Toast
import androidx.activity.ComponentActivity
import cn.xuanforge.xfmclopen.data.DownloadProgress
import cn.xuanforge.xfmclopen.data.DownloadSource
import cn.xuanforge.xfmclopen.data.Endpoints
import cn.xuanforge.xfmclopen.data.GameInstaller
import cn.xuanforge.xfmclopen.data.GamePaths
import cn.xuanforge.xfmclopen.data.Mirror
import cn.xuanforge.xfmclopen.data.VersionRepository
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import org.json.JSONArray
import org.json.JSONObject

class WebLauncherActivity : ComponentActivity() {

    private lateinit var webView: WebView
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val paths by lazy { GamePaths(applicationContext) }
    private val endpoints by lazy { Endpoints.load(applicationContext) }
    private val prefs by lazy { getSharedPreferences("xfmcl_open", Context.MODE_PRIVATE) }
    private var job: Job? = null

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        webView = WebView(this).apply {
            setBackgroundColor(0xFF0A0C11.toInt())
            settings.javaScriptEnabled = true
            settings.domStorageEnabled = true
            settings.allowFileAccess = true
            settings.allowContentAccess = false
            webViewClient = object : WebViewClient() {
                override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean =
                    !request.url.toString().startsWith(ASSET_ROOT)
            }
            addJavascriptInterface(Bridge(), BRIDGE_NAME)
            loadUrl("$ASSET_ROOT/index.html")
        }

        setContentView(webView)
    }

    override fun onDestroy() {
        job?.cancel()
        scope.cancel()
        webView.removeJavascriptInterface(BRIDGE_NAME)
        webView.destroy()
        super.onDestroy()
    }

    private fun emit(script: String) {
        runOnUiThread { webView.evaluateJavascript(script, null) }
    }

    private fun emitLog(line: String) {
        emit("window.XfmclNative && window.XfmclNative.onLog(${JSONObject.quote(line)})")
    }

    private fun emitProgress(progress: DownloadProgress) {
        val json = JSONObject().apply {
            put("stage", progress.stage)
            put("completed", progress.completed)
            put("total", progress.total)
            put("bytes", progress.bytes)
        }.toString()
        emit("window.XfmclNative && window.XfmclNative.onProgress(${JSONObject.quote(json)})")
    }

    private fun emitDone(ok: Boolean, message: String) {
        emit("window.XfmclNative && window.XfmclNative.onDone($ok, ${JSONObject.quote(message)})")
    }

    private fun emitError(message: String) {
        emit("window.XfmclNative && window.XfmclNative.onError(${JSONObject.quote(message)})")
    }

    private fun currentSource(): DownloadSource =
        if (prefs.getString("source", DownloadSource.OFFICIAL.name) == DownloadSource.MIRROR.name) {
            DownloadSource.MIRROR
        } else {
            DownloadSource.OFFICIAL
        }

    inner class Bridge {

        @JavascriptInterface
        fun init() {
            val json = JSONObject().apply {
                put("configured", endpoints.isConfigured)
                put("player", prefs.getString("name", "Player") ?: "Player")
                put("source", prefs.getString("source", DownloadSource.OFFICIAL.name))
                put("gameDir", paths.root.absolutePath)
            }.toString()
            emit("window.XfmclNative && window.XfmclNative.onInit(${JSONObject.quote(json)})")
        }

        @JavascriptInterface
        fun loadVersions(force: Boolean) {
            if (!endpoints.isConfigured) {
                emitError("尚未配置下载端点")
                return
            }

            job?.cancel()
            job = scope.launch {
                try {
                    val repository = VersionRepository(Mirror(endpoints, currentSource()), paths) { emitLog(it) }
                    val versions = repository.fetchManifest(force)
                    val array = JSONArray()
                    versions.forEach { version ->
                        array.put(JSONObject().apply {
                            put("id", version.id)
                            put("type", version.type)
                            put("date", version.releaseTime.take(10))
                            put("installed", paths.versionJson(version.id).exists())
                        })
                    }
                    emit("window.XfmclNative && window.XfmclNative.onVersions(${JSONObject.quote(array.toString())})")
                } catch (e: CancellationException) {
                    throw e
                } catch (e: Exception) {
                    emitError(e.message ?: e.toString())
                }
            }
        }

        @JavascriptInterface
        fun download(versionId: String) {
            if (!endpoints.isConfigured) {
                emitError("尚未配置下载端点")
                return
            }

            job?.cancel()
            job = scope.launch {
                try {
                    val mirror = Mirror(endpoints, currentSource())
                    val repository = VersionRepository(mirror, paths) { emitLog(it) }
                    val manifest = repository.fetchManifest(false)
                    val entry = manifest.firstOrNull { it.id == versionId }
                        ?: throw IllegalStateException("版本 $versionId 不在清单中")

                    val detail = repository.fetchVersion(entry)
                    val installer = GameInstaller(paths, mirror) { emitLog(it) }
                    installer.install(detail) { progress -> emitProgress(progress) }
                    emitDone(true, "$versionId 下载完成")
                } catch (e: CancellationException) {
                    emitDone(false, "已取消")
                } catch (e: Exception) {
                    emitError(e.message ?: e.toString())
                }
            }
        }

        @JavascriptInterface
        fun cancel() {
            job?.cancel()
        }

        @JavascriptInterface
        fun saveSettings(name: String, source: String) {
            val normalized = if (source == DownloadSource.MIRROR.name) DownloadSource.MIRROR.name else DownloadSource.OFFICIAL.name
            prefs.edit()
                .putString("name", name.take(16))
                .putString("source", normalized)
                .apply()
        }

        @JavascriptInterface
        fun openDirectory() {
            runOnUiThread {
                Toast.makeText(
                    this@WebLauncherActivity,
                    paths.root.absolutePath,
                    Toast.LENGTH_LONG
                ).show()
            }
        }
    }

    companion object {
        private const val BRIDGE_NAME = "XfmclAndroid"
        private const val ASSET_ROOT = "file:///android_asset"
    }
}
