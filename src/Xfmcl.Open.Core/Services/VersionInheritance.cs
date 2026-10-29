/**
 * VersionInheritance.cs - 继承版本合并
 * 玹砺科技 XUANFORGE
 *
 * 安装器生成的版本文件只描述自己新增的部分，
 * 其余字段（client.jar、libraries、arguments、assets）都来自 inheritsFrom 指向的父版本。
 * 本项目以 MIT 协议开源。
 */

using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>把子版本与父版本合并成一个可直接启动的完整描述。</summary>
public static class VersionInheritance
{
    /// <summary>最多支持的继承层数，防止脏数据造成死循环。</summary>
    public const int MaxDepth = 6;

    public static VersionDetail Merge(VersionDetail child, VersionDetail parent)
    {
        return new VersionDetail
        {
            // 子版本保留自己的 ID；其余空字段一律继承
            Id = child.Id,
            Type = FirstNonEmpty(child.Type, parent.Type),
            MainClass = FirstNonEmpty(child.MainClass, parent.MainClass),
            Assets = child.Assets ?? parent.Assets,
            AssetIndex = child.AssetIndex ?? parent.AssetIndex,
            Downloads = child.Downloads ?? parent.Downloads,
            JavaVersion = child.JavaVersion ?? parent.JavaVersion,
            Logging = child.Logging ?? parent.Logging,
            MinecraftArguments = child.MinecraftArguments ?? parent.MinecraftArguments,
            Libraries = MergeLibraries(child.Libraries, parent.Libraries),
            Arguments = MergeArguments(child.Arguments, parent.Arguments),

            // 父版本可能还有更上一层，交给调用方继续解析
            InheritsFrom = parent.InheritsFrom
        };
    }

    /// <summary>父库在前，子库追加；同名依赖由子版本覆盖。</summary>
    private static List<Library> MergeLibraries(List<Library> child, List<Library> parent)
    {
        var merged = new List<Library>(parent.Count + child.Count);
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var library in parent)
        {
            index[library.Name] = merged.Count;
            merged.Add(library);
        }

        foreach (var library in child)
        {
            if (index.TryGetValue(library.Name, out var slot))
            {
                merged[slot] = library;
            }
            else
            {
                index[library.Name] = merged.Count;
                merged.Add(library);
            }
        }

        return merged;
    }

    /// <summary>参数按 父 → 子 的顺序拼接。</summary>
    private static VersionArguments? MergeArguments(VersionArguments? child, VersionArguments? parent)
    {
        if (child == null)
        {
            return parent;
        }

        if (parent == null)
        {
            return child;
        }

        return new VersionArguments
        {
            Game = parent.Game.Concat(child.Game).ToList(),
            Jvm = parent.Jvm.Concat(child.Jvm).ToList()
        };
    }

    private static string FirstNonEmpty(string primary, string secondary) =>
        string.IsNullOrWhiteSpace(primary) ? secondary : primary;
}
