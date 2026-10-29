/**
 * ArgumentEntryConverter.cs - 启动参数 JSON 转换器
 * 玹砺科技 XUANFORGE
 *
 * 处理 arguments 数组中「字符串 / 带 rules 的对象 / 数组值」混排。
 * 本项目以 MIT 协议开源。
 */

using System.Text.Json;
using System.Text.Json.Serialization;
using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>把版本描述里的混合参数格式统一成 <see cref="ArgumentEntry"/>。</summary>
public sealed class ArgumentEntryConverter : JsonConverter<ArgumentEntry>
{
    public override ArgumentEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var single = new ArgumentEntry();
            single.Values.Add(reader.GetString() ?? string.Empty);
            return single;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("无法识别的启动参数格式。");
        }

        var entry = new ArgumentEntry();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var name = reader.GetString();
            reader.Read();

            if (string.Equals(name, "value", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    entry.Values.Add(reader.GetString() ?? string.Empty);
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            entry.Values.Add(reader.GetString() ?? string.Empty);
                        }
                    }
                }
                else
                {
                    reader.Skip();
                }
            }
            else if (string.Equals(name, "rules", StringComparison.OrdinalIgnoreCase))
            {
                // 走源生成元数据，裁剪后仍可用
                entry.Rules = JsonSerializer.Deserialize(ref reader, JsonContext.Default.ListRule)
                              ?? new List<Rule>();
            }
            else
            {
                reader.Skip();
            }
        }

        return entry;
    }

    public override void Write(Utf8JsonWriter writer, ArgumentEntry value, JsonSerializerOptions options)
    {
        if (value.Values.Count == 1)
        {
            writer.WriteStringValue(value.Values[0]);
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value.Values)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
