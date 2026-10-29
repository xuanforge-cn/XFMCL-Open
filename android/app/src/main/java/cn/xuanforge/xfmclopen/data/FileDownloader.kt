/**
 * FileDownloader.kt - 并发文件下载
 * 玹砺科技 XUANFORGE
 *
 * 已存在的文件按大小（必要时 SHA1）跳过；下载完成后校验 SHA1。
 * 本项目以 MIT 协议开源。
 */

package cn.xuanforge.xfmclopen.data

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import java.io.File
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import java.util.concurrent.ConcurrentLinkedQueue
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong
import kotlin.math.max

class FileDownloader(private val log: (String) -> Unit) {

    suspend fun downloadAll(
        tasks: List<DownloadTask>,
        concurrency: Int = 12,
        onProgress: (completed: Int, total: Int, bytes: Long) -> Unit
    ): List<String> = coroutineScope {
        val total = tasks.size
        val pending = tasks.filterNot { isComplete(it) }
        val done = AtomicInteger(total - pending.size)
        val bytes = AtomicLong(0)
        val lastReport = AtomicLong(0)
        val failures = ConcurrentLinkedQueue<String>()

        fun report(force: Boolean) {
            val now = System.currentTimeMillis()
            if (!force && now - lastReport.get() < 200) {
                return
            }

            lastReport.set(now)
            onProgress(done.get(), total, bytes.get())
        }

        report(true)
        if (pending.isEmpty()) {
            return@coroutineScope emptyList()
        }

        val limiter = Semaphore(max(1, concurrency))
        pending.map { task ->
            async(Dispatchers.IO) {
                limiter.withPermit {
                    try {
                        downloadWithRetry(task) { delta ->
                            bytes.addAndGet(delta)
                            report(false)
                        }
                        done.incrementAndGet()
                        report(false)
                    } catch (e: CancellationException) {
                        throw e
                    } catch (e: Exception) {
                        failures.add("${task.target.name}: ${e.message}")
                        done.incrementAndGet()
                        report(true)
                    }
                }
            }
        }.awaitAll()

        report(true)
        if (failures.isNotEmpty()) {
            log("${failures.size} 个文件下载失败。")
        }

        failures.toList()
    }

    private fun isComplete(task: DownloadTask): Boolean {
        if (!task.target.exists()) {
            return false
        }

        if (task.size > 0) {
            return task.target.length() == task.size
        }

        if (task.sha1 != null) {
            return task.target.length() > 0 && sha1(task.target).equals(task.sha1, ignoreCase = true)
        }

        return task.target.length() > 0
    }

    private suspend fun downloadWithRetry(task: DownloadTask, onBytes: (Long) -> Unit) {
        val candidates = listOfNotNull(task.primaryUrl, task.altUrl)
            .filter { it.isNotEmpty() }
            .distinct()

        if (candidates.isEmpty()) {
            throw IOException("没有可用的下载地址")
        }

        val attempts = max(3, candidates.size)
        var last: Exception? = null

        for (attempt in 0 until attempts) {
            val url = candidates[attempt % candidates.size]
            try {
                downloadOnce(task, url, onBytes)
                return
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                last = e
            }

            delay(400L * (attempt + 1).coerceAtMost(3))
        }

        throw IOException("下载失败：${last?.message}", last)
    }

    private fun downloadOnce(task: DownloadTask, url: String, onBytes: (Long) -> Unit) {
        task.target.parentFile?.mkdirs()
        val part = File(task.target.parentFile, "${task.target.name}.part")

        if (part.exists()) {
            part.delete()
        }

        val connection = URL(url).openConnection() as HttpURLConnection
        try {
            connection.connectTimeout = 30_000
            connection.readTimeout = 30_000
            connection.requestMethod = "GET"
            connection.setRequestProperty("User-Agent", "XFMCL-Open/1.0.0 (Android)")

            val code = connection.responseCode
            if (code !in 200..299) {
                throw IOException("HTTP $code")
            }

            connection.inputStream.use { input ->
                part.outputStream().use { output ->
                    val buffer = ByteArray(64 * 1024)
                    while (true) {
                        val read = input.read(buffer)
                        if (read <= 0) {
                            break
                        }

                        output.write(buffer, 0, read)
                        onBytes(read.toLong())
                    }
                }
            }
        } finally {
            connection.disconnect()
        }

        if (task.sha1 != null && !sha1(part).equals(task.sha1, ignoreCase = true)) {
            part.delete()
            throw IOException("SHA1 校验不通过")
        }

        if (task.size > 0 && part.length() != task.size) {
            part.delete()
            throw IOException("文件大小不符")
        }

        if (task.target.exists() && !task.target.delete()) {
            part.delete()
            throw IOException("无法覆盖旧文件")
        }

        if (!part.renameTo(task.target)) {
            part.copyTo(task.target, overwrite = true)
            part.delete()
        }
    }

    private fun sha1(file: File): String {
        val digest = MessageDigest.getInstance("SHA-1")
        file.inputStream().use { input ->
            val buffer = ByteArray(64 * 1024)
            while (true) {
                val read = input.read(buffer)
                if (read <= 0) {
                    break
                }

                digest.update(buffer, 0, read)
            }
        }

        return digest.digest().joinToString("") { "%02x".format(it) }
    }
}
