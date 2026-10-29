/**
 * Mirror.kt - 下载源
 * 玹砺科技 XUANFORGE
 *
 * 地址全部来自 Endpoints，代码内不包含真实服务地址。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

enum class DownloadSource(val label: String) {
    OFFICIAL("官方源"),
    BMCLAPI("镜像源")
}

class Mirror(val endpoints: Endpoints, private val source: DownloadSource) {

    fun rewrite(url: String): String {
        if (source == DownloadSource.OFFICIAL || url.isEmpty()) {
            return url
        }

        return when {
            url.startsWith(endpoints.metaRoot) -> endpoints.mirrorRoot + url.removePrefix(endpoints.metaRoot)
            url.startsWith(endpoints.dataRoot) -> endpoints.mirrorRoot + url.removePrefix(endpoints.dataRoot)
            url.startsWith(endpoints.librariesRoot) -> endpoints.mirrorRoot + "/maven" + url.removePrefix(endpoints.librariesRoot)
            url.startsWith(endpoints.resourcesRoot) -> endpoints.mirrorRoot + "/assets" + url.removePrefix(endpoints.resourcesRoot)
            else -> url
        }
    }

    fun libraryUrl(mavenPath: String): String =
        if (source == DownloadSource.BMCLAPI) "${endpoints.mirrorRoot}/maven/$mavenPath"
        else "${endpoints.librariesRoot}/$mavenPath"

    fun assetUrl(hash: String): String =
        if (source == DownloadSource.BMCLAPI) "${endpoints.mirrorRoot}/assets/${hash.take(2)}/$hash"
        else "${endpoints.resourcesRoot}/${hash.take(2)}/$hash"
}
