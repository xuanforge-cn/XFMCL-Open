/**
 * MirrorSource.cs - 下载源重写
 * 玹砺科技 XUANFORGE
 *
 * 所有地址来自 Endpoints 配置，代码内不包含真实服务地址。
 * 本项目以 MIT 协议开源。
 */

using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>按所选下载源重写地址；官方源原样返回。</summary>
public sealed class MirrorSource
{
    private readonly Endpoints _endpoints;

    public MirrorSource(DownloadSourceKind kind, Endpoints endpoints)
    {
        Kind = kind;
        _endpoints = endpoints;
    }

    public DownloadSourceKind Kind { get; }

    public Endpoints Endpoints => _endpoints;

    public string DisplayName => Kind == DownloadSourceKind.BmclApi ? "镜像源" : "官方源";

    /// <summary>重写单个地址；官方源原样返回。</summary>
    public string Rewrite(string url)
    {
        if (Kind == DownloadSourceKind.Official || string.IsNullOrEmpty(url))
        {
            return url;
        }

        if (url.StartsWith(_endpoints.MetaRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _endpoints.MirrorRoot + url[_endpoints.MetaRoot.Length..];
        }

        if (url.StartsWith(_endpoints.DataRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _endpoints.MirrorRoot + url[_endpoints.DataRoot.Length..];
        }

        if (url.StartsWith(_endpoints.LibrariesRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _endpoints.MirrorRoot + "/maven" + url[_endpoints.LibrariesRoot.Length..];
        }

        if (url.StartsWith(_endpoints.ResourcesRoot, StringComparison.OrdinalIgnoreCase))
        {
            return _endpoints.MirrorRoot + "/assets" + url[_endpoints.ResourcesRoot.Length..];
        }

        return url;
    }

    /// <summary>库文件缺失下载地址时，按 Maven 坐标拼接。</summary>
    public string LibraryUrl(string mavenPath)
    {
        return Kind == DownloadSourceKind.BmclApi
            ? $"{_endpoints.MirrorRoot}/maven/{mavenPath}"
            : $"{_endpoints.LibrariesRoot}/{mavenPath}";
    }

    /// <summary>资源对象下载地址。</summary>
    public string AssetUrl(string hash)
    {
        return Kind == DownloadSourceKind.BmclApi
            ? $"{_endpoints.MirrorRoot}/assets/{hash[..2]}/{hash}"
            : $"{_endpoints.ResourcesRoot}/{hash[..2]}/{hash}";
    }
}
