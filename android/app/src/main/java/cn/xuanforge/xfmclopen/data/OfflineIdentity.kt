/**
 * OfflineIdentity.kt - 离线登录身份
 * 玹砺科技 XUANFORGE
 *
 * 按 Java 版离线 UUID 规则（name → MD5 生成 v3 UUID）计算身份，
 * 不涉及任何账号凭据，也不连接鉴权服务器。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import java.security.MessageDigest

object OfflineIdentity {
    private val NAME_PATTERN = Regex("^[A-Za-z0-9_]{3,16}$")

    fun isValid(name: String): Boolean = NAME_PATTERN.matches(name)

    /** 生成不带连字符的 v3 UUID。 */
    fun uuid(name: String): String {
        val digest = MessageDigest.getInstance("MD5")
        val hash = digest.digest("OfflinePlayer:$name".toByteArray(Charsets.UTF_8))
        hash[6] = ((hash[6].toInt() and 0x0F) or 0x30).toByte()
        hash[8] = ((hash[8].toInt() and 0x3F) or 0x80).toByte()
        return buildString {
            hash.forEach { append("%02x".format(it)) }
        }
    }
}
