/**
 * LaunchOptions.cs - 启动参数模型
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

namespace XfmclOpen.Core.Models;

/// <summary>一次启动或下载所需的全部输入。</summary>
public sealed class LaunchOptions
{
    /// <summary>游戏根目录，例如 .minecraft。</summary>
    public string GameDirectory { get; set; } = string.Empty;

    /// <summary>要启动的版本 ID。</summary>
    public string VersionId { get; set; } = string.Empty;

    /// <summary>离线玩家名。</summary>
    public string PlayerName { get; set; } = "Player";

    /// <summary>指定的 Java 可执行文件；为空时自动探测。</summary>
    public string? JavaPath { get; set; }

    /// <summary>窗口宽度。</summary>
    public int Width { get; set; } = 1280;

    /// <summary>窗口高度。</summary>
    public int Height { get; set; } = 720;

    /// <summary>最大堆内存（MB）。</summary>
    public int MaxMemoryMb { get; set; } = 2048;

    /// <summary>下载源。</summary>
    public DownloadSourceKind Source { get; set; } = DownloadSourceKind.Official;

    /// <summary>只补齐文件，不启动游戏。</summary>
    public bool InstallOnly { get; set; }
}

/// <summary>下载源类型。</summary>
public enum DownloadSourceKind
{
    /// <summary>Mojang 官方源。</summary>
    Official = 0,

    /// <summary>BMCLAPI 镜像源。</summary>
    BmclApi = 1
}
