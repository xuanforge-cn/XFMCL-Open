/**
 * GamePaths.cs - 游戏目录约定
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

namespace XfmclOpen.Core.Services;

/// <summary>按 Minecraft 目录规范组织路径（Windows / Linux / macOS 通用）。</summary>
public sealed class GamePaths
{
    public GamePaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    /// <summary>游戏根目录。</summary>
    public string Root { get; }

    /// <summary>当前系统的 Minecraft 平台标识。</summary>
    public static string PlatformName => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "osx" : "linux";

    public string VersionsDirectory => Path.Combine(Root, "versions");

    public string LibrariesDirectory => Path.Combine(Root, "libraries");

    public string AssetsDirectory => Path.Combine(Root, "assets");

    public string AssetIndexesDirectory => Path.Combine(AssetsDirectory, "indexes");

    public string AssetObjectsDirectory => Path.Combine(AssetsDirectory, "objects");

    public string LogConfigsDirectory => Path.Combine(AssetsDirectory, "log_configs");

    public string VersionDirectory(string id) => Path.Combine(VersionsDirectory, id);

    public string VersionJson(string id) => Path.Combine(VersionDirectory(id), id + ".json");

    public string VersionJar(string id) => Path.Combine(VersionDirectory(id), id + ".jar");

    public string NativesDirectory(string id) => Path.Combine(VersionDirectory(id), "natives-" + PlatformName);

    public string ManifestFile => Path.Combine(VersionsDirectory, "version_manifest_v2.json");

    /// <summary>资源对象路径：objects/{hash 前两位}/{hash}。</summary>
    public string AssetObjectFile(string hash)
    {
        var prefix = hash.Length >= 2 ? hash[..2] : "00";
        return Path.Combine(AssetObjectsDirectory, prefix, hash);
    }

    /// <summary>把 Maven 相对路径落到 libraries 目录。</summary>
    public string LibraryFile(string relativePath) =>
        Path.Combine(LibrariesDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>建立全部基础目录。</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(VersionsDirectory);
        Directory.CreateDirectory(LibrariesDirectory);
        Directory.CreateDirectory(AssetIndexesDirectory);
        Directory.CreateDirectory(AssetObjectsDirectory);
        Directory.CreateDirectory(LogConfigsDirectory);
    }
}
