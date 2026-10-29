/**
 * LaunchService.cs - 启动编排
 * 玹砺科技 XUANFORGE
 *
 * 串起版本解析、文件补全、Java 选择、参数拼装与进程启动。
 * 本项目以 MIT 协议开源。
 */

using System.Diagnostics;
using System.Text;
using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>一次已启动的会话。</summary>
public sealed class LaunchSession
{
    public required PreparedVersion Prepared { get; init; }

    public JavaRuntime? Java { get; init; }

    public LaunchCommand? Command { get; init; }

    public Process? Process { get; init; }
}

/// <summary>启动器运行时服务。</summary>
public sealed class LaunchService
{
    private static readonly HttpClient SharedHttp = CreateHttpClient();

    public LaunchService()
    {
        Http = SharedHttp;
    }

    public HttpClient Http { get; }

    /// <summary>游戏进程的标准输出（后台线程触发）。</summary>
    public event Action<string>? Output;

    /// <summary>游戏进程退出（后台线程触发）。</summary>
    public event Action<int>? Exited;

    /// <summary>补齐文件并按需启动游戏。</summary>
    public async Task<LaunchSession> LaunchAsync(
        LaunchOptions options,
        IProgress<InstallProgress>? progress,
        Action<string>? log,
        CancellationToken ct)
    {
        if (!OfflineIdentity.IsValidName(options.PlayerName))
        {
            throw new ArgumentException("离线玩家名需为 3-16 位字母、数字或下划线。", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.GameDirectory))
        {
            throw new ArgumentException("游戏目录不能为空。", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.VersionId))
        {
            throw new ArgumentException("请先选择游戏版本。", nameof(options));
        }

        Directory.CreateDirectory(options.GameDirectory);

        var paths = new GamePaths(options.GameDirectory);
        paths.EnsureCreated();

        var mirror = new MirrorSource(options.Source);
        var repository = new VersionRepository(Http, paths, mirror);

        log?.Invoke($"下载源：{mirror.DisplayName}");
        log?.Invoke("正在读取版本清单…");

        VersionDetail detail;
        ManifestVersion? target = null;
        VersionManifest? manifest = null;
        try
        {
            manifest = await repository.GetManifestAsync(forceRefresh: false, ct).ConfigureAwait(false);
            target = manifest.Versions.FirstOrDefault(v => string.Equals(v.Id, options.VersionId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (repository.GetLocalVersion(options.VersionId) != null)
        {
            log?.Invoke("版本清单请求失败，改用本地版本文件继续。");
            log?.Invoke(ex.Message);
        }

        if (target != null)
        {
            detail = await repository.GetVersionAsync(target, ct).ConfigureAwait(false);
        }
        else
        {
            detail = repository.GetLocalVersion(options.VersionId)
                     ?? throw new InvalidOperationException($"未找到版本 {options.VersionId}。");
        }

        // 安装器生成的版本文件只写增量，需要合并父版本后才能补全文件
        detail = await ResolveInheritanceAsync(detail, repository, manifest, log, ct).ConfigureAwait(false);

        log?.Invoke($"版本：{detail.Id}（需要 Java {detail.JavaVersion?.MajorVersion ?? 8}）");

        if (repository.UsedFallback)
        {
            log?.Invoke("所选下载源不可用，已自动切换到其它源。");
        }

        var installer = new GameInstaller(paths, mirror, Http, log);
        var prepared = await installer.PrepareAsync(detail, progress, ct).ConfigureAwait(false);
        log?.Invoke("游戏文件校验完成。");

        if (options.InstallOnly)
        {
            // 仅下载/校验时不依赖本机 Java，缺 Java 也能先把文件备好
            log?.Invoke("文件已准备好（未启动游戏）。");
            return new LaunchSession
            {
                Prepared = prepared,
                Process = null
            };
        }

        var java = ResolveJava(options.JavaPath, prepared.RequiredJavaMajor);
        log?.Invoke($"Java：{java.Path}（{java.Display}）");

        var identity = OfflineIdentity.Create(options.PlayerName);
        log?.Invoke($"离线身份：{identity.Name}（{identity.Uuid}）");

        var command = LaunchArgumentBuilder.Build(prepared, options, java);

        return new LaunchSession
        {
            Prepared = prepared,
            Java = java,
            Command = command,
            Process = StartProcess(command, options, log)
        };
    }

    /// <summary>沿 inheritsFrom 链向上合并，直到拿到完整的原版描述。</summary>
    private static async Task<VersionDetail> ResolveInheritanceAsync(
        VersionDetail detail,
        VersionRepository repository,
        VersionManifest? manifest,
        Action<string>? log,
        CancellationToken ct)
    {
        var depth = 0;
        while (!string.IsNullOrEmpty(detail.InheritsFrom))
        {
            if (++depth > VersionInheritance.MaxDepth)
            {
                throw new InvalidOperationException($"版本继承层数过深（最后一层父版本：{detail.InheritsFrom}）。");
            }

            var parentId = detail.InheritsFrom!;
            var parent = repository.GetLocalVersion(parentId);
            if (parent == null)
            {
                var entry = manifest?.Versions.FirstOrDefault(v => string.Equals(v.Id, parentId, StringComparison.OrdinalIgnoreCase))
                            ?? throw new InvalidOperationException($"未找到父版本 {parentId}。");
                parent = await repository.GetVersionAsync(entry, ct).ConfigureAwait(false);
            }

            log?.Invoke($"继承父版本：{parentId}");
            detail = VersionInheritance.Merge(detail, parent);
        }

        return detail;
    }

    private static JavaRuntime ResolveJava(string? explicitPath, int requiredMajor)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (!File.Exists(explicitPath))
            {
                throw new FileNotFoundException("指定的 Java 可执行文件不存在。", explicitPath);
            }

            return JavaLocator.Probe(explicitPath)
                   ?? throw new InvalidOperationException("无法读取指定的 Java 版本。");
        }

        var runtimes = JavaLocator.Discover();
        var selected = JavaLocator.Select(runtimes, requiredMajor);
        if (selected == null)
        {
            var found = runtimes.Count == 0
                ? "本机未找到 Java"
                : "已安装版本：" + string.Join(", ", runtimes.Select(x => x.MajorVersion));
            throw new InvalidOperationException($"该版本需要 Java {requiredMajor}（{found}）。");
        }

        return selected;
    }

    private Process? StartProcess(LaunchCommand command, LaunchOptions options, Action<string>? log)
    {
        var info = new ProcessStartInfo
        {
            FileName = command.JavaPath,
            WorkingDirectory = Path.GetFullPath(options.GameDirectory),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in command.JvmArguments)
        {
            info.ArgumentList.Add(arg);
        }

        info.ArgumentList.Add(command.MainClass);

        foreach (var arg in command.GameArguments)
        {
            info.ArgumentList.Add(arg);
        }

        log?.Invoke("启动命令：" + command.ToCommandLine());

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Output?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                Output?.Invoke(e.Data);
            }
        };
        process.Exited += (_, _) =>
        {
            try
            {
                Exited?.Invoke(process.ExitCode);
            }
            catch (InvalidOperationException)
            {
                // 进程对象已释放
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("游戏进程启动失败。");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        log?.Invoke($"游戏进程已启动（PID {process.Id}）。");
        return process;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"{LaunchArgumentBuilder.LauncherName}/{LaunchArgumentBuilder.LauncherVersion}");
        return client;
    }
}
