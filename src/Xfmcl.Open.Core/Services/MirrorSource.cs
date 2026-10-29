/**
 * MirrorSource.cs - 下载源重写
 * 玹砺科技 XUANFORGE
 *
 * 官方源面向境外直连；BMCLAPI 用于国内网络环境加速。
 * 本项目以 MIT 协议开源。
 */

using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>把 Mojang 官方地址按所选下载源重写。</summary>
public sealed class MirrorSource
{
    private const string BmclApiRoot = "https://bmclapi2.bangbang93.com";

    public MirrorSource(DownloadSourceKind kind)
    {
        Kind = kind;
    }

    public DownloadSourceKind Kind { get; }

    public string DisplayName => Kind == DownloadSourceKind.BmclApi ? "BMCLAPI 镜像源" : "Mojang 官方源";

    /// <summary>重写单个地址；官方源原样返回。</summary>
    public string Rewrite(string url)
    {
        if (Kind == DownloadSourceKind.Official || string.IsNullOrEmpty(url))
        {
            return url;
        }

        const string meta = "https://piston-meta.mojang.com";
        const string data = "https://piston-data.mojang.com";
        const string libraries = "https://libraries.minecraft.net";
        const string resources = "https://resources.download.minecraft.net";

        if (url.StartsWith(meta, StringComparison.OrdinalIgnoreCase))
        {
            return BmclApiRoot + url[meta.Length..];
        }

        if (url.StartsWith(data, StringComparison.OrdinalIgnoreCase))
        {
            return BmclApiRoot + url[data.Length..];
        }

        if (url.StartsWith(libraries, StringComparison.OrdinalIgnoreCase))
        {
            return BmclApiRoot + "/maven" + url[libraries.Length..];
        }

        if (url.StartsWith(resources, StringComparison.OrdinalIgnoreCase))
        {
            return BmclApiRoot + "/assets" + url[resources.Length..];
        }

        return url;
    }

    /// <summary>库文件缺失下载地址时，按 Maven 坐标拼接。</summary>
    public string LibraryUrl(string mavenPath)
    {
        return Kind == DownloadSourceKind.BmclApi
            ? $"{BmclApiRoot}/maven/{mavenPath}"
            : $"https://libraries.minecraft.net/{mavenPath}";
    }

    /// <summary>资源对象下载地址。</summary>
    public string AssetUrl(string hash)
    {
        return Kind == DownloadSourceKind.BmclApi
            ? $"{BmclApiRoot}/assets/{hash[..2]}/{hash}"
            : $"https://resources.download.minecraft.net/{hash[..2]}/{hash}";
    }
}
