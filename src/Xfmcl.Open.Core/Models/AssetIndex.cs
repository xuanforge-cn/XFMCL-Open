/**
 * AssetIndex.cs - 资源索引模型
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

namespace XfmclOpen.Core.Models;

/// <summary>资源索引（assets/indexes/{id}.json）。</summary>
public sealed class AssetIndex
{
    public Dictionary<string, AssetObject> Objects { get; set; } = new();
}

/// <summary>单个资源对象。</summary>
public sealed class AssetObject
{
    public string Hash { get; set; } = string.Empty;

    public long Size { get; set; }
}
