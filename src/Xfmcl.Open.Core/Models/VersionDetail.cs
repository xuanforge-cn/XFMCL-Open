/**
 * VersionDetail.cs - 版本描述文件模型
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

namespace XfmclOpen.Core.Models;

/// <summary>版本描述文件（versions/{id}/{id}.json）。</summary>
public sealed class VersionDetail
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string MainClass { get; set; } = string.Empty;

    public string? Assets { get; set; }

    public AssetIndexRef? AssetIndex { get; set; }

    public VersionDownloads? Downloads { get; set; }

    public List<Library> Libraries { get; set; } = new();

    public VersionArguments? Arguments { get; set; }

    /// <summary>1.13 之前的旧版参数（空格分隔字符串）。</summary>
    public string? MinecraftArguments { get; set; }

    public JavaVersionRequirement? JavaVersion { get; set; }

    public LoggingConfig? Logging { get; set; }

    /// <summary>继承的父版本（安装器生成的版本文件会带此字段）。</summary>
    public string? InheritsFrom { get; set; }
}

/// <summary>版本资源索引引用。</summary>
public sealed class AssetIndexRef
{
    public string Id { get; set; } = string.Empty;

    public string Sha1 { get; set; } = string.Empty;

    public long Size { get; set; }

    public long TotalSize { get; set; }

    public string Url { get; set; } = string.Empty;
}

/// <summary>版本下载项。</summary>
public sealed class VersionDownloads
{
    public DownloadArtifact? Client { get; set; }

    public DownloadArtifact? Server { get; set; }
}

/// <summary>带校验信息的下载项。</summary>
public sealed class DownloadArtifact
{
    public string Path { get; set; } = string.Empty;

    public string Sha1 { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Url { get; set; } = string.Empty;
}

/// <summary>游戏库。</summary>
public sealed class Library
{
    public string Name { get; set; } = string.Empty;

    public LibraryDownloads? Downloads { get; set; }

    /// <summary>旧版 natives 映射，例如 { "windows": "natives-windows-64" }。</summary>
    public Dictionary<string, string>? Natives { get; set; }

    public ExtractRules? Extract { get; set; }

    public List<Rule> Rules { get; set; } = new();

    /// <summary>把 Maven 坐标转换为相对路径。</summary>
    public static string MavenPath(string name)
    {
        var parts = name.Split(':');
        if (parts.Length < 3)
        {
            return name.Replace(':', '/');
        }

        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length > 3 ? "-" + parts[3] : string.Empty;
        return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
    }
}

/// <summary>库下载项。</summary>
public sealed class LibraryDownloads
{
    public DownloadArtifact? Artifact { get; set; }

    public Dictionary<string, DownloadArtifact>? Classifiers { get; set; }
}

/// <summary>解压排除规则。</summary>
public sealed class ExtractRules
{
    public List<string> Exclude { get; set; } = new();
}

/// <summary>规则条目，用于按系统与特性筛选库与参数。</summary>
public sealed class Rule
{
    public string Action { get; set; } = "allow";

    public RuleOs? Os { get; set; }

    public Dictionary<string, bool>? Features { get; set; }
}

/// <summary>规则中的系统条件。</summary>
public sealed class RuleOs
{
    public string? Name { get; set; }

    public string? Arch { get; set; }

    public string? Version { get; set; }
}

/// <summary>启动参数集合。</summary>
public sealed class VersionArguments
{
    public List<ArgumentEntry> Game { get; set; } = new();

    public List<ArgumentEntry> Jvm { get; set; } = new();
}

/// <summary>单条启动参数，可能是纯字符串，也可能是带规则、值为字符串或字符串数组的对象。</summary>
public sealed class ArgumentEntry
{
    public List<string> Values { get; } = new();

    public List<Rule> Rules { get; set; } = new();
}

/// <summary>Java 版本要求。</summary>
public sealed class JavaVersionRequirement
{
    public string Component { get; set; } = string.Empty;

    public int MajorVersion { get; set; }
}

/// <summary>日志配置。</summary>
public sealed class LoggingConfig
{
    public LoggingClient? Client { get; set; }
}

/// <summary>日志客户端配置项。</summary>
public sealed class LoggingClient
{
    public string Argument { get; set; } = string.Empty;

    public LoggingFile? File { get; set; }

    public string Type { get; set; } = string.Empty;
}

/// <summary>日志文件下载信息。</summary>
public sealed class LoggingFile
{
    public string Id { get; set; } = string.Empty;

    public string Sha1 { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Url { get; set; } = string.Empty;
}
