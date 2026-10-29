/**
 * OfflineIdentity.cs - 离线登录身份
 * 玹砺科技 XUANFORGE
 *
 * 按 Java 版离线 UUID 规则（name → MD5 生成 v3 UUID）计算身份，
 * 不涉及任何账号凭据，也不连接任何鉴权服务器。
 * 本项目以 MIT 协议开源。
 */

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XfmclOpen.Core.Services;

/// <summary>离线玩家身份。</summary>
public sealed class OfflineIdentity
{
    private static readonly Regex NamePattern = new("^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);

    private OfflineIdentity(string name, string uuid)
    {
        Name = name;
        Uuid = uuid;
    }

    public string Name { get; }

    /// <summary>不带连字符的 UUID，用于启动参数。</summary>
    public string Uuid { get; }

    public string AccessToken => "0";

    public string UserType => "legacy";

    /// <summary>校验离线玩家名（与游戏内允许的字符一致）。</summary>
    public static bool IsValidName(string? name) => name != null && NamePattern.IsMatch(name);

    /// <summary>用玩家名生成离线身份。</summary>
    public static OfflineIdentity Create(string name)
    {
        var safe = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + safe));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return new OfflineIdentity(safe, hex);
    }
}
