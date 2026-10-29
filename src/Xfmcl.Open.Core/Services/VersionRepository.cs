/**
 * VersionRepository.cs - 版本清单与描述文件读取
 * 玹砺科技 XUANFORGE
 *
 * 下载顺序：所选下载源 → Mojang 官方 → 本地缓存。
 * 本地 JSON 损坏（版本描述 / 清单）时自动重新拉取，不需要用户手动清缓存。
 * 本项目以 MIT 协议开源。
 */

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>负责拉取并缓存版本清单与版本描述文件。</summary>
public sealed class VersionRepository
{
    private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    private readonly HttpClient _http;
    private readonly GamePaths _paths;
    private readonly MirrorSource _mirror;
    private readonly MirrorSource _official = new(DownloadSourceKind.Official);

    public VersionRepository(HttpClient http, GamePaths paths, MirrorSource mirror)
    {
        _http = http;
        _paths = paths;
        _mirror = mirror;
    }

    /// <summary>所选源失败并回退到其它源时为 true。</summary>
    public bool UsedFallback { get; private set; }

    /// <summary>所选源失败时回退官方源（去重）。</summary>
    private IEnumerable<MirrorSource> Sources()
    {
        yield return _mirror;

        if (_mirror.Kind != DownloadSourceKind.Official)
        {
            yield return _official;
        }
    }

    /// <summary>获取版本清单；任一源失败自动切换，全部失败时回退本地缓存。</summary>
    public async Task<VersionManifest> GetManifestAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && File.Exists(_paths.ManifestFile))
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(_paths.ManifestFile);
            if (age < TimeSpan.FromMinutes(30))
            {
                var cached = await TryReadAsync(_paths.ManifestFile, JsonContext.Default.VersionManifest, ct);
                if (cached != null)
                {
                    return cached;
                }
            }
        }

        Exception? last = null;
        foreach (var source in Sources())
        {
            try
            {
                var manifest = await FetchManifestAsync(source.Rewrite(ManifestUrl), ct).ConfigureAwait(false);
                UsedFallback |= source.Kind != _mirror.Kind;
                return manifest;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                last = ex;
            }
        }

        var cachedFallback = await TryReadAsync(_paths.ManifestFile, JsonContext.Default.VersionManifest, ct);
        if (cachedFallback != null)
        {
            return cachedFallback;
        }

        throw last ?? new InvalidOperationException("版本清单下载失败。");
    }

    private async Task<VersionManifest> FetchManifestAsync(string url, CancellationToken ct)
    {
        var json = await GetStringWithTimeoutAsync(url, ct);
        Directory.CreateDirectory(_paths.VersionsDirectory);
        await File.WriteAllTextAsync(_paths.ManifestFile, json, ct);
        return JsonSerializer.Deserialize(json, JsonContext.Default.VersionManifest)
               ?? throw new InvalidOperationException("版本清单解析失败。");
    }

    /// <summary>限制网络请求时间，避免网络异常时界面长时间无响应。</summary>
    private async Task<string> GetStringWithTimeoutAsync(string url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await _http.GetStringAsync(url, timeout.Token);
    }

    /// <summary>获取指定版本的描述文件；本地文件缺失或损坏时自动重下。</summary>
    public async Task<VersionDetail> GetVersionAsync(ManifestVersion version, CancellationToken ct)
    {
        var path = _paths.VersionJson(version.Id);
        if (File.Exists(path))
        {
            var local = await TryReadAsync(path, JsonContext.Default.VersionDetail, ct);
            if (IsUsable(local))
            {
                return local!;
            }

            // 描述文件损坏：删掉重下，避免每次启动都解析失败
            TryDelete(path);
        }

        Exception? last = null;
        foreach (var source in Sources())
        {
            try
            {
                var json = await GetStringWithTimeoutAsync(source.Rewrite(version.Url), ct);
                var detail = JsonSerializer.Deserialize(json, JsonContext.Default.VersionDetail);
                if (!IsUsable(detail))
                {
                    throw new InvalidOperationException($"版本 {version.Id} 描述文件解析失败。");
                }

                Directory.CreateDirectory(_paths.VersionDirectory(version.Id));
                await File.WriteAllTextAsync(path, json, ct);
                UsedFallback |= source.Kind != _mirror.Kind;
                return detail!;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException($"无法加载版本 {version.Id}。");
    }

    /// <summary>直接读取本地版本文件（不联网）。</summary>
    public VersionDetail? GetLocalVersion(string id)
    {
        var path = _paths.VersionJson(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var detail = JsonSerializer.Deserialize(File.ReadAllText(path), JsonContext.Default.VersionDetail);
            return IsUsable(detail) ? detail : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>描述文件至少要有主类（或继承父版本）才算可用。</summary>
    private static bool IsUsable(VersionDetail? detail) =>
        detail != null && (!string.IsNullOrEmpty(detail.MainClass) || !string.IsNullOrEmpty(detail.InheritsFrom));

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // 删不掉时后续写入会覆盖
        }
    }

    private static async Task<T?> TryReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize(text, typeInfo);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (IOException)
        {
            return default;
        }
    }
}
