/**
 * FileDownloader.cs - 并发文件下载
 * 玹砺科技 XUANFORGE
 *
 * 只使用 .NET 自带 HttpClient，按 SHA1 校验，失败重试。
 * 本项目以 MIT 协议开源。
 */

using System.Diagnostics;
using System.Security.Cryptography;

namespace XfmclOpen.Core.Services;

/// <summary>一个待下载文件；AltUrls 按顺序作为备用源。</summary>
public sealed record DownloadTask(
    string Url,
    string TargetPath,
    string? Sha1 = null,
    long Size = 0,
    IReadOnlyList<string>? AltUrls = null,
    bool VerifyHash = false);

/// <summary>下载进度快照。</summary>
public sealed record DownloadProgress(int Completed, int Total, long Bytes, string? Current);

/// <summary>下载结果。</summary>
public sealed record DownloadResult(int Downloaded, int Skipped, List<string> Failures);

/// <summary>并发下载器：已存在的文件直接跳过，下载完成后校验 SHA1。</summary>
public sealed class FileDownloader
{
    private readonly HttpClient _http;
    private readonly Action<string>? _log;
    private readonly Stopwatch _throttle = Stopwatch.StartNew();
    private readonly object _gate = new();

    private int _completed;
    private int _total;
    private long _bytes;
    private string? _current;

    public FileDownloader(HttpClient http, Action<string>? log = null)
    {
        _http = http;
        _log = log;
    }

    /// <summary>最大并发数。</summary>
    public int MaxParallel { get; set; } = 16;

    /// <summary>单个文件的最大重试次数。</summary>
    public int MaxRetry { get; set; } = 3;

    public async Task<DownloadResult> DownloadAllAsync(
        IReadOnlyList<DownloadTask> tasks,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var pending = new List<DownloadTask>();
        var skipped = 0;

        // 完整性检查可能要算上千个文件的哈希，按核数并行，避免串行扫一遍
        var complete = new bool[tasks.Count];
        Parallel.For(
            0,
            tasks.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) },
            i => complete[i] = IsComplete(tasks[i]));

        for (var i = 0; i < tasks.Count; i++)
        {
            if (complete[i])
            {
                skipped++;
                continue;
            }

            pending.Add(tasks[i]);
        }

        _total = tasks.Count;
        _completed = skipped;
        _bytes = 0;
        _current = null;
        Report(progress, force: true);

        if (pending.Count == 0)
        {
            return new DownloadResult(0, skipped, new List<string>());
        }

        var failures = new List<string>();
        using var limiter = new SemaphoreSlim(Math.Max(1, MaxParallel));

        var jobs = pending.Select(async task =>
        {
            await limiter.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await DownloadWithRetryAsync(task, progress, ct).ConfigureAwait(false);
                Interlocked.Increment(ref _completed);
                Report(progress, force: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lock (_gate)
                {
                    failures.Add($"{Path.GetFileName(task.TargetPath)}: {ex.Message}");
                }

                Interlocked.Increment(ref _completed);
                Report(progress, force: true);
            }
            finally
            {
                limiter.Release();
            }
        });

        await Task.WhenAll(jobs).ConfigureAwait(false);

        lock (_gate)
        {
            if (failures.Count > 0)
            {
                _log?.Invoke($"{failures.Count} 个文件下载失败。");
            }
        }

        return new DownloadResult(pending.Count - failures.Count, skipped, failures);
    }

    private static bool IsComplete(DownloadTask task)
    {
        var info = new FileInfo(task.TargetPath);
        if (!info.Exists)
        {
            return false;
        }

        // 小体积 JSON 配置（版本描述 / 资源索引）按哈希判定，损坏时自动重下
        if (task.VerifyHash && !string.IsNullOrEmpty(task.Sha1))
        {
            try
            {
                using var stream = File.OpenRead(task.TargetPath);
                var actual = Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
                return string.Equals(actual, task.Sha1, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return false;
            }
        }

        if (task.Size > 0)
        {
            return info.Length == task.Size;
        }

        return info.Length > 0;
    }

    private async Task DownloadWithRetryAsync(DownloadTask task, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var candidates = new List<string> { task.Url };
        if (task.AltUrls != null)
        {
            foreach (var alt in task.AltUrls)
            {
                if (!string.IsNullOrEmpty(alt) && !candidates.Contains(alt, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(alt);
                }
            }
        }

        // 至少把每个可用源都试一遍，失败再按原重试次数兜底
        var attempts = Math.Max(MaxRetry, candidates.Count);
        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
            idle.CancelAfter(TimeSpan.FromSeconds(30));

            // 依次轮换：所选源 → 官方 → 镜像 → ……
            var url = candidates[(attempt - 1) % candidates.Count];

            try
            {
                await DownloadOnceAsync(task with { Url = url }, progress, idle).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                last = new TimeoutException("连接超过 30 秒没有数据");
            }
            catch (Exception ex)
            {
                last = ex;
            }

            if (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct).ConfigureAwait(false);
            }
        }

        throw new IOException("下载失败", last);
    }

    private async Task DownloadOnceAsync(DownloadTask task, IProgress<DownloadProgress>? progress, CancellationTokenSource idle)
    {
        var directory = Path.GetDirectoryName(task.TargetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = task.TargetPath + ".part";
        lock (_gate)
        {
            _current = Path.GetFileName(task.TargetPath);
        }

        using (var response = await _http.GetAsync(task.Url, HttpCompletionOption.ResponseHeadersRead, idle.Token).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(idle.Token).ConfigureAwait(false);
            await using var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, idle.Token).ConfigureAwait(false)) > 0)
            {
                // 有数据就续期，只对真正“卡住”的连接超时
                idle.CancelAfter(TimeSpan.FromSeconds(30));
                await target.WriteAsync(buffer.AsMemory(0, read), idle.Token).ConfigureAwait(false);
                Interlocked.Add(ref _bytes, read);
                Report(progress, force: false);
            }
        }

        if (!string.IsNullOrEmpty(task.Sha1))
        {
            var actual = await ComputeSha1Async(temp, idle.Token).ConfigureAwait(false);
            if (!string.Equals(actual, task.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(temp);
                throw new InvalidDataException("SHA1 校验不通过");
            }
        }

        File.Move(temp, task.TargetPath, overwrite: true);
    }

    private static async Task<string> ComputeSha1Async(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = await SHA1.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void Report(IProgress<DownloadProgress>? progress, bool force)
    {
        if (progress == null)
        {
            return;
        }

        lock (_gate)
        {
            if (!force && _throttle.ElapsedMilliseconds < 200)
            {
                return;
            }

            _throttle.Restart();
            progress.Report(new DownloadProgress(_completed, _total, _bytes, _current));
        }
    }
}
