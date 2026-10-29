/**
 * JsonContext.cs - 源生成的 JSON 上下文
 * 玹砺科技 XUANFORGE
 *
 * 裁剪（PublishTrimmed）后反射式序列化会被裁掉，所有 JSON 读写统一走这里生成的元数据。
 * 本项目以 MIT 协议开源。
 */

using System.Text.Json.Serialization;
using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = new[] { typeof(ArgumentEntryConverter) })]
[JsonSerializable(typeof(VersionManifest))]
[JsonSerializable(typeof(VersionDetail))]
[JsonSerializable(typeof(AssetIndex))]
[JsonSerializable(typeof(List<Rule>))]
internal sealed partial class JsonContext : JsonSerializerContext
{
}
