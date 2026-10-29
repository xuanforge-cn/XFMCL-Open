/**
 * JavaLocator.cs - Java 运行时探测
 * 玹砺科技 XUANFORGE
 *
 * 覆盖 Windows / Linux / macOS 常见安装位置，只依赖 .NET 自带 API。
 * 本项目以 MIT 协议开源。
 */

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace XfmclOpen.Core.Services;

/// <summary>一个可用的 Java 运行时。</summary>
public sealed record JavaRuntime(string Path, string Version, int MajorVersion)
{
    public string Display => $"Java {MajorVersion} · {Version}";
}

/// <summary>扫描本机常见的 Java 安装位置。</summary>
public static class JavaLocator
{
    private static readonly Regex VersionPattern = new(@"JAVA_VERSION=""?([\d._]+)", RegexOptions.Compiled);

    /// <summary>当前系统的 Java 可执行文件名。</summary>
    public static string ExecutableName => OperatingSystem.IsWindows() ? "java.exe" : "java";

    /// <summary>返回去重后的 Java 列表（版本从高到低）。</summary>
    public static IReadOnlyList<JavaRuntime> Discover()
    {
        var candidates = new List<string>();

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            candidates.Add(Path.Combine(javaHome, "bin", ExecutableName));
        }

        foreach (var root in EnumerateRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    candidates.Add(Path.Combine(dir, "bin", ExecutableName));
                    candidates.Add(Path.Combine(dir, "Contents", "Home", "bin", ExecutableName));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 目录不可读时忽略
            }
        }

        var onPath = FindOnPath();
        if (onPath != null)
        {
            candidates.Add(onPath);
        }

        var result = new List<JavaRuntime>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate) || !seen.Add(Path.GetFullPath(candidate)))
            {
                continue;
            }

            var runtime = Probe(candidate);
            if (runtime != null)
            {
                result.Add(runtime);
            }
        }

        return result.OrderByDescending(x => x.MajorVersion).ToList();
    }

    /// <summary>挑选满足要求的 Java：优先完全匹配，其次取满足要求的最低版本。</summary>
    public static JavaRuntime? Select(IReadOnlyList<JavaRuntime> runtimes, int requiredMajor)
    {
        var exact = runtimes.FirstOrDefault(x => x.MajorVersion == requiredMajor);
        if (exact != null)
        {
            return exact;
        }

        return runtimes.Where(x => x.MajorVersion >= requiredMajor).OrderBy(x => x.MajorVersion).FirstOrDefault();
    }

    /// <summary>读取单个 Java 的版本。</summary>
    public static JavaRuntime? Probe(string javaPath)
    {
        var version = ReadFromReleaseFile(javaPath) ?? RunVersionCommand(javaPath);
        if (version == null)
        {
            return null;
        }

        return new JavaRuntime(javaPath, version, ParseMajor(version));
    }

    /// <summary>把 1.8.0_392 / 25.0.2 之类版本号折算成主版本。</summary>
    public static int ParseMajor(string version)
    {
        var parts = version.Split('.', '_', '-', '+');
        if (parts.Length == 0 || !int.TryParse(parts[0], out var first))
        {
            return 0;
        }

        if (first == 1 && parts.Length > 1 && int.TryParse(parts[1], out var legacy))
        {
            return legacy;
        }

        return first;
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            yield return @"C:\Program Files\Java";
            yield return @"C:\Program Files\Eclipse Adoptium";
            yield return @"C:\Program Files\Microsoft";
            yield return @"C:\Program Files\Zulu";
            yield return @"C:\Program Files\BellSoft";
            yield return @"C:\Program Files\Amazon Corretto";
            yield return Path.Combine(userProfile, ".jdks");

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(localAppData, "Programs", "Eclipse Adoptium");
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "/Library/Java/JavaVirtualMachines";
            yield return Path.Combine(userProfile, ".jdks");
            yield return Path.Combine(userProfile, "Library", "Java", "JavaVirtualMachines");
            yield break;
        }

        yield return "/usr/lib/jvm";
        yield return "/usr/java";
        yield return "/opt/java";
        yield return Path.Combine(userProfile, ".jdks");
        yield return Path.Combine(userProfile, ".sdkman", "candidates", "java");
    }

    private static string? FindOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            var candidate = Path.Combine(dir.Trim(), ExecutableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ReadFromReleaseFile(string javaPath)
    {
        var home = Path.GetDirectoryName(Path.GetDirectoryName(javaPath));
        if (home == null)
        {
            return null;
        }

        var release = Path.Combine(home, "release");
        if (!File.Exists(release))
        {
            return null;
        }

        foreach (var line in File.ReadLines(release))
        {
            if (!line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal))
            {
                continue;
            }

            var match = VersionPattern.Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string? RunVersionCommand(string javaPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var text = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            var match = Regex.Match(text, "version \"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
