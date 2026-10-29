/**
 * Endpoints.cs - 下载端点配置
 * 玹砺科技 XUANFORGE
 *
 * 所有外部下载地址统一从 endpoints.json 读取；代码内默认值均为占位符，
 * 未配置时不会访问任何真实服务。
 * 本项目以 MIT 协议开源。
 */

using System.Text.Json;

namespace XfmclOpen.Core.Services;

/// <summary>下载端点集合。</summary>
public sealed class Endpoints
{
    private const string PlaceholderHost = "example.com";

    public const string DefaultManifestUrl = "https://example.com/minecraft/version_manifest_v2.json";
    public const string DefaultMetaRoot = "https://example.com/minecraft/meta";
    public const string DefaultDataRoot = "https://example.com/minecraft/data";
    public const string DefaultLibrariesRoot = "https://example.com/minecraft/libraries";
    public const string DefaultResourcesRoot = "https://example.com/minecraft/resources";
    public const string DefaultMirrorRoot = "https://example.com/minecraft/mirror";

    /// <summary>版本清单地址。</summary>
    public string ManifestUrl { get; init; } = DefaultManifestUrl;

    /// <summary>版本元数据根地址（对应 version_manifest 中的 url 前缀）。</summary>
    public string MetaRoot { get; init; } = DefaultMetaRoot;

    /// <summary>版本数据根地址（client.jar 等）。</summary>
    public string DataRoot { get; init; } = DefaultDataRoot;

    /// <summary>依赖库根地址。</summary>
    public string LibrariesRoot { get; init; } = DefaultLibrariesRoot;

    /// <summary>资源文件根地址。</summary>
    public string ResourcesRoot { get; init; } = DefaultResourcesRoot;

    /// <summary>镜像源根地址。</summary>
    public string MirrorRoot { get; init; } = DefaultMirrorRoot;

    /// <summary>是否已经填写真实地址（默认占位符视为未配置）。</summary>
    public bool IsConfigured =>
        !ManifestUrl.Contains(PlaceholderHost, StringComparison.OrdinalIgnoreCase) &&
        !MirrorRoot.Contains(PlaceholderHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>从 JSON 文件读取；文件缺失或格式错误时返回占位符配置。</summary>
    public static Endpoints Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new Endpoints();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new Endpoints
            {
                ManifestUrl = Read(root, "manifestUrl", DefaultManifestUrl),
                MetaRoot = Read(root, "metaRoot", DefaultMetaRoot),
                DataRoot = Read(root, "dataRoot", DefaultDataRoot),
                LibrariesRoot = Read(root, "librariesRoot", DefaultLibrariesRoot),
                ResourcesRoot = Read(root, "resourcesRoot", DefaultResourcesRoot),
                MirrorRoot = Read(root, "mirrorRoot", DefaultMirrorRoot)
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Endpoints();
        }
    }

    private static string Read(JsonElement root, string name, string fallback)
    {
        if (root.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        return fallback;
    }
}
