/**
 * Http.kt - 轻量 HTTP 读取
 * 玹砺科技 XUANFORGE
 *
 * 只使用 java.net.HttpURLConnection，不引入网络框架。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

object Http {
    private const val USER_AGENT = "XFMCL-Open/1.0.0 (Android)"

    fun getText(url: String, timeoutMs: Int = 30_000): String {
        val connection = URL(url).openConnection() as HttpURLConnection
        return try {
            connection.connectTimeout = timeoutMs
            connection.readTimeout = timeoutMs
            connection.requestMethod = "GET"
            connection.setRequestProperty("User-Agent", USER_AGENT)

            val code = connection.responseCode
            if (code !in 200..299) {
                throw IOException("HTTP $code：$url")
            }

            connection.inputStream.bufferedReader().use { it.readText() }
        } finally {
            connection.disconnect()
        }
    }
}
