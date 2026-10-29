/**
 * GameInstaller.cs - 游戏文件补全
 * 玹砺科技 XUANFORGE
 *
 * 负责 client.jar、libraries、natives 解压与 assets 的完整性补齐。
 * 本项目以 MIT 协议开源。
 */

using System.IO.Compression;
using System.Text.Json;
using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>安装阶段。</summary>
public enum InstallStage
{
    Libraries,
    Assets
}

/// <summary>安装阶段进度。</summary>
public sealed record InstallProgress(InstallStage Stage, int Completed, int Total, long Bytes);

/// <summary>补全完成的版本信息。</summary>
public sealed class PreparedVersion
{
    public required VersionDetail Detail { get; init; }

    public required string ClientJar { get; init; }

    public required List<string> Classpath { get; init; }

    public required string NativesDirectory { get; init; }

    public required string AssetsIndexName { get; init; }

    public required string AssetsRoot { get; init; }

    public string? LogConfigArgument { get; init; }

    public int RequiredJavaMajor { get; init; }
}

/// <summary>按版本描述文件补齐本地文件。</summary>
public sealed class GameInstaller
{
    private static readonly string OsName = GamePaths.PlatformName;

    private readonly GamePaths _paths;
    private readonly MirrorSource _mirror;
    private readonly MirrorSource _official;
    private readonly HttpClient _http;
    private readonly Action<string>? _log;

    public GameInstaller(GamePaths paths, MirrorSource mirror, HttpClient http, Action<string>? log = null)
    {
        _paths = paths;
        _mirror = mirror;
        _official = new MirrorSource(DownloadSourceKind.Official, mirror.Endpoints);
        _http = http;
        _log = log;
    }

    public async Task<PreparedVersion> PrepareAsync(
        VersionDetail detail,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(detail.InheritsFrom))
        {
            // 调用方应先用 VersionInheritance 合并父版本，这里再做一次兜底
            throw new NotSupportedException($"版本 {detail.Id} 继承自 {detail.InheritsFrom}，必须先合并父版本。");
        }

        _paths.EnsureCreated();
        Directory.CreateDirectory(_paths.VersionDirectory(detail.Id));

        var tasks = new List<DownloadTask>();
        var classpath = new List<string>();
        var nativeArchives = new List<(DownloadTask Task, ExtractRules? Extract)>();

        // 1. 客户端主 jar（继承版本的 jar 位于父版本目录，路径来自 downloads.client.path）
        var clientJar = ResolveClientJar(detail);
        if (detail.Downloads?.Client is { } client)
        {
            var primary = _mirror.Rewrite(client.Url);
            tasks.Add(new DownloadTask(
                primary,
                clientJar,
                client.Sha1,
                client.Size,
                Alternates(primary, _official.Rewrite(client.Url))));
        }
        else if (!File.Exists(clientJar))
        {
            throw new InvalidOperationException("版本描述文件缺少客户端下载信息。");
        }

        // 2. 游戏库
        foreach (var library in detail.Libraries)
        {
            if (!IsAllowed(library.Rules))
            {
                continue;
            }

            if (library.Natives != null)
            {
                var classifierKey = library.Natives.TryGetValue(OsName, out var key) ? key : null;
                if (classifierKey == null)
                {
                    continue;
                }

                classifierKey = classifierKey.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
                if (library.Downloads?.Classifiers == null ||
                    !library.Downloads.Classifiers.TryGetValue(classifierKey, out var native))
                {
                    continue;
                }

                var nativeRelative = !string.IsNullOrEmpty(native.Path)
                    ? native.Path
                    : Library.MavenPath(library.Name + ":" + classifierKey);
                var nativePath = _paths.LibraryFile(nativeRelative);

                var nativePrimary = _mirror.Rewrite(native.Url);
                var nativeTask = new DownloadTask(
                    nativePrimary,
                    nativePath,
                    native.Sha1,
                    native.Size,
                    Alternates(nativePrimary, _official.Rewrite(native.Url)));
                tasks.Add(nativeTask);
                nativeArchives.Add((nativeTask, library.Extract));
                continue;
            }

            // 现代版本把 natives 作为普通库放在 classpath 上
            var artifact = library.Downloads?.Artifact;
            var relative = artifact != null && !string.IsNullOrEmpty(artifact.Path)
                ? artifact.Path
                : Library.MavenPath(library.Name);
            var target = _paths.LibraryFile(relative);
            var primary = artifact != null && !string.IsNullOrEmpty(artifact.Url)
                ? _mirror.Rewrite(artifact.Url)
                : _mirror.LibraryUrl(relative);

            tasks.Add(new DownloadTask(
                primary,
                target,
                artifact?.Sha1,
                artifact?.Size ?? 0,
                Alternates(primary, _official.LibraryUrl(relative))));
            classpath.Add(target);
        }

        classpath.Add(clientJar);

        // 3. 资源索引
        if (detail.AssetIndex == null)
        {
            throw new NotSupportedException($"版本 {detail.Id} 使用旧版资源结构（1.7 之前），暂不支持。");
        }

        var indexName = string.IsNullOrEmpty(detail.AssetIndex.Id) ? (detail.Assets ?? "legacy") : detail.AssetIndex.Id;
        var indexFile = Path.Combine(_paths.AssetIndexesDirectory, indexName + ".json");
        var indexPrimary = _mirror.Rewrite(detail.AssetIndex.Url);
        var indexTask = new DownloadTask(
            indexPrimary,
            indexFile,
            detail.AssetIndex.Sha1,
            0,
            Alternates(indexPrimary, _official.Rewrite(detail.AssetIndex.Url)),
            VerifyHash: true);
        tasks.Add(indexTask);

        // 4. 日志配置
        string? logArgument = null;
        if (detail.Logging?.Client is { } logging && !string.IsNullOrEmpty(logging.File?.Url))
        {
            var logFile = Path.Combine(_paths.LogConfigsDirectory, logging.File.Id);
            var logPrimary = _mirror.Rewrite(logging.File.Url);
            tasks.Add(new DownloadTask(
                logPrimary,
                logFile,
                logging.File.Sha1,
                logging.File.Size,
                Alternates(logPrimary, _official.Rewrite(logging.File.Url)),
                VerifyHash: true));
            logArgument = logging.Argument.Replace("${path}", logFile, StringComparison.OrdinalIgnoreCase);
        }

        // 先下载库与索引，再解析 assets 清单
        _log?.Invoke($"检查游戏库与资源索引（{tasks.Count} 项）…");
        var downloader = new FileDownloader(_http, _log);
        var first = await downloader.DownloadAllAsync(
            tasks,
            new Progress<DownloadProgress>(p => progress?.Report(new InstallProgress(InstallStage.Libraries, p.Completed, p.Total, p.Bytes))),
            ct).ConfigureAwait(false);

        if (first.Failures.Count > 0)
        {
            throw new IOException("部分文件下载失败：" + string.Join("；", first.Failures.Take(5)));
        }

        var index = await ReadAssetIndexAsync(indexFile, ct).ConfigureAwait(false);
        if (index == null)
        {
            // 索引为空或损坏：删掉重下一次
            _log?.Invoke("资源索引损坏，正在重新下载…");
            TryDelete(indexFile);

            var repair = await downloader.DownloadAllAsync(new[] { indexTask }, null, ct).ConfigureAwait(false);
            index = await ReadAssetIndexAsync(indexFile, ct).ConfigureAwait(false);
            if (index == null)
            {
                throw new IOException("资源索引修复失败：" + string.Join("；", repair.Failures.Take(3)));
            }
        }

        // 同一 hash 会被多个资源名引用，按 hash 去重避免并发写同一个文件
        var assetTasks = index.Objects.Values
            .GroupBy(obj => obj.Hash, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(obj => new DownloadTask(
                _mirror.AssetUrl(obj.Hash),
                _paths.AssetObjectFile(obj.Hash),
                obj.Hash,
                obj.Size,
                Alternates(_mirror.AssetUrl(obj.Hash), _official.AssetUrl(obj.Hash))))
            .ToList();

        _log?.Invoke($"检查游戏资源（{assetTasks.Count} 项）…");
        var assetResult = await downloader.DownloadAllAsync(
            assetTasks,
            new Progress<DownloadProgress>(p => progress?.Report(new InstallProgress(InstallStage.Assets, p.Completed, p.Total, p.Bytes))),
            ct).ConfigureAwait(false);

        if (assetResult.Failures.Count > 0)
        {
            throw new IOException("部分资源下载失败：" + string.Join("；", assetResult.Failures.Take(5)));
        }

        // 5. 解压旧版 natives
        var nativesDir = _paths.NativesDirectory(detail.Id);
        if (nativeArchives.Count > 0)
        {
            if (Directory.Exists(nativesDir))
            {
                Directory.Delete(nativesDir, recursive: true);
            }

            Directory.CreateDirectory(nativesDir);
            foreach (var (task, extract) in nativeArchives)
            {
                ExtractNatives(task.TargetPath, nativesDir, extract);
            }
        }
        else
        {
            Directory.CreateDirectory(nativesDir);
        }

        return new PreparedVersion
        {
            Detail = detail,
            ClientJar = clientJar,
            Classpath = classpath,
            NativesDirectory = nativesDir,
            AssetsIndexName = indexName,
            AssetsRoot = _paths.AssetsDirectory,
            LogConfigArgument = logArgument,
            RequiredJavaMajor = detail.JavaVersion?.MajorVersion ?? 8
        };
    }

    /// <summary>继承版本的 client.jar 落在父版本目录，因此优先使用描述文件里的相对路径。</summary>
    private string ResolveClientJar(VersionDetail detail)
    {
        var relative = detail.Downloads?.Client?.Path;
        if (string.IsNullOrWhiteSpace(relative))
        {
            return _paths.VersionJar(detail.Id);
        }

        var root = Path.GetFullPath(_paths.VersionsDirectory) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(
            _paths.VersionsDirectory,
            relative.Replace('/', Path.DirectorySeparatorChar)));

        // 防止描述文件里的路径跳出 versions 目录
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? full
            : _paths.VersionJar(detail.Id);
    }

    /// <summary>读取资源索引；文件缺失或 JSON 损坏时返回 null，由调用方重下。</summary>
    private static async Task<AssetIndex?> ReadAssetIndexAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var index = await JsonSerializer.DeserializeAsync(stream, JsonContext.Default.AssetIndex, ct).ConfigureAwait(false);
            return index?.Objects.Count > 0 ? index : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 删不掉就让下载器覆盖写
        }
    }

    /// <summary>备用地址：去掉与主地址重复的项。</summary>
    private static IReadOnlyList<string> Alternates(string primary, params string?[] candidates)
    {
        var list = new List<string>();
        foreach (var url in candidates)
        {
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (string.Equals(url, primary, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!list.Contains(url, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(url);
            }
        }

        return list;
    }

    private static void ExtractNatives(string archive, string targetDirectory, ExtractRules? rules)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rules != null && rules.Exclude.Any(x => entry.FullName.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var destination = Path.Combine(targetDirectory, Path.GetFileName(entry.FullName));
            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    /// <summary>按当前系统与特性判断规则是否放行。</summary>
    public static bool IsAllowed(List<Rule> rules)
    {
        if (rules.Count == 0)
        {
            return true;
        }

        var allowed = false;
        foreach (var rule in rules)
        {
            if (!Matches(rule))
            {
                continue;
            }

            allowed = string.Equals(rule.Action, "allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    private static bool Matches(Rule rule)
    {
        if (rule.Features is { Count: > 0 })
        {
            // 不启用演示模式与自定义分辨率特性
            return false;
        }

        if (rule.Os == null)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(rule.Os.Name) &&
            !string.Equals(rule.Os.Name, OsName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.Os.Arch))
        {
            var arch = Environment.Is64BitOperatingSystem ? "x86_64" : "x86";
            if (!string.Equals(rule.Os.Arch, arch, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
