/**
 * Models.kt - 数据模型
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import java.io.File

/** 版本清单中的一条记录。 */
data class ManifestVersion(
    val id: String,
    val type: String,
    val url: String,
    val releaseTime: String
)

/** 列表展示用的版本行。 */
data class VersionRow(
    val id: String,
    val type: String,
    val date: String,
    val installed: Boolean
) {
    val typeLabel: String
        get() = when (type) {
            "release" -> "正式版"
            "snapshot" -> "快照"
            "old_beta" -> "旧测试版"
            "old_alpha" -> "远古版"
            else -> type
        }
}

/** 一个待下载文件。 */
data class DownloadTask(
    val primaryUrl: String,
    val target: File,
    val sha1: String?,
    val size: Long,
    val altUrl: String? = null
)

/** 下载进度。 */
data class DownloadProgress(
    val stage: String,
    val completed: Int,
    val total: Int,
    val bytes: Long
)
