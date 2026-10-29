/**
 * Mirror.kt - 下载源
 * 玹砺科技 XUANFORGE
 *
 * 官方源面向境外直连；BMCLAPI 用于国内网络环境加速。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

enum class DownloadSource(val label: String) {
    OFFICIAL("Mojang 官方源"),
    BMCLAPI("BMCLAPI 镜像源")
}

class Mirror(private val source: DownloadSource) {

    fun rewrite(url: String): String {
        if (source == DownloadSource.OFFICIAL || url.isEmpty()) {
            return url
        }

        return when {
            url.startsWith(META) -> ROOT + url.removePrefix(META)
            url.startsWith(DATA) -> ROOT + url.removePrefix(DATA)
            url.startsWith(LIBRARIES) -> ROOT + "/maven" + url.removePrefix(LIBRARIES)
            url.startsWith(RESOURCES) -> ROOT + "/assets" + url.removePrefix(RESOURCES)
            else -> url
        }
    }

    fun libraryUrl(mavenPath: String): String =
        if (source == DownloadSource.BMCLAPI) "$ROOT/maven/$mavenPath" else "$LIBRARIES/$mavenPath"

    fun assetUrl(hash: String): String =
        if (source == DownloadSource.BMCLAPI) "$ROOT/assets/${hash.take(2)}/$hash"
        else "$RESOURCES/${hash.take(2)}/$hash"

    companion object {
        private const val ROOT = "https://bmclapi2.bangbang93.com"
        private const val META = "https://piston-meta.mojang.com"
        private const val DATA = "https://piston-data.mojang.com"
        private const val LIBRARIES = "https://libraries.minecraft.net"
        private const val RESOURCES = "https://resources.download.minecraft.net"
    }
}
