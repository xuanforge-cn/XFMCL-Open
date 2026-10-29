/**
 * VersionManifest.cs - 版本清单模型
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

namespace XfmclOpen.Core.Models;

/// <summary>版本清单（version_manifest_v2.json）。</summary>
public sealed class VersionManifest
{
    public LatestVersions? Latest { get; set; }

    public List<ManifestVersion> Versions { get; set; } = new();
}

/// <summary>最新正式版与快照标识。</summary>
public sealed class LatestVersions
{
    public string Release { get; set; } = string.Empty;

    public string Snapshot { get; set; } = string.Empty;
}

/// <summary>清单中的单条版本记录。</summary>
public sealed class ManifestVersion
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Sha1 { get; set; }

    public DateTimeOffset? ReleaseTime { get; set; }
}
