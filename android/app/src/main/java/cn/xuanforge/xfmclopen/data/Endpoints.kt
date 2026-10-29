/**
 * Endpoints.kt - 下载端点配置
 * 玹砺科技 XUANFORGE
 *
 * 所有外部地址从 assets/endpoints.json 读取；代码内默认值均为占位符。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import android.content.Context
import org.json.JSONObject

data class Endpoints(
    val manifestUrl: String = DEFAULT_MANIFEST,
    val metaRoot: String = DEFAULT_META,
    val dataRoot: String = DEFAULT_DATA,
    val librariesRoot: String = DEFAULT_LIBRARIES,
    val resourcesRoot: String = DEFAULT_RESOURCES,
    val mirrorRoot: String = DEFAULT_MIRROR
) {
    /** 默认占位符视为未配置。 */
    val isConfigured: Boolean
        get() = !manifestUrl.contains("example.com", ignoreCase = true) &&
            !mirrorRoot.contains("example.com", ignoreCase = true)

    companion object {
        private const val DEFAULT_BASE = "https://example.com/minecraft"
        const val DEFAULT_MANIFEST = "$DEFAULT_BASE/version_manifest_v2.json"
        const val DEFAULT_META = "$DEFAULT_BASE/meta"
        const val DEFAULT_DATA = "$DEFAULT_BASE/data"
        const val DEFAULT_LIBRARIES = "$DEFAULT_BASE/libraries"
        const val DEFAULT_RESOURCES = "$DEFAULT_BASE/resources"
        const val DEFAULT_MIRROR = "$DEFAULT_BASE/mirror"

        /** 读取 assets/endpoints.json；缺失或格式错误时返回占位符配置。 */
        fun load(context: Context): Endpoints = runCatching {
            val text = context.assets.open("endpoints.json").bufferedReader().use { it.readText() }
            val json = JSONObject(text)
            Endpoints(
                manifestUrl = json.optString("manifestUrl", DEFAULT_MANIFEST),
                metaRoot = json.optString("metaRoot", DEFAULT_META),
                dataRoot = json.optString("dataRoot", DEFAULT_DATA),
                librariesRoot = json.optString("librariesRoot", DEFAULT_LIBRARIES),
                resourcesRoot = json.optString("resourcesRoot", DEFAULT_RESOURCES),
                mirrorRoot = json.optString("mirrorRoot", DEFAULT_MIRROR)
            )
        }.getOrDefault(Endpoints())
    }
}
