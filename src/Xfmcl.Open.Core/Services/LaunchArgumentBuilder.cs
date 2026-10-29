/**
 * LaunchArgumentBuilder.cs - 启动参数拼装
 * 玹砺科技 XUANFORGE
 *
 * 只支持离线身份，classpath 分隔符按当前平台取值。
 * 本项目以 MIT 协议开源。
 */

using XfmclOpen.Core.Models;

namespace XfmclOpen.Core.Services;

/// <summary>拼装完成的启动命令。</summary>
public sealed record LaunchCommand(
    string JavaPath,
    List<string> JvmArguments,
    string MainClass,
    List<string> GameArguments)
{
    /// <summary>可读的命令行文本，用于日志。</summary>
    public string ToCommandLine() =>
        string.Join(' ', new[] { JavaPath }.Concat(JvmArguments).Append(MainClass).Concat(GameArguments));
}

/// <summary>把版本描述中的占位符替换为真实启动值。</summary>
public static class LaunchArgumentBuilder
{
    public const string LauncherName = "XFMCL Open";
    public const string LauncherVersion = "1.0.0";

    public static LaunchCommand Build(PreparedVersion prepared, LaunchOptions options, JavaRuntime java)
    {
        var identity = OfflineIdentity.Create(options.PlayerName);
        var resourceRoot = Path.GetFullPath(options.GameDirectory);
        var gameDirectory = Path.GetFullPath(options.GameDirectory);
        var className = string.Join(Path.PathSeparator, prepared.Classpath);
        var arguments = prepared.Detail.Arguments;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auth_player_name"] = identity.Name,
            ["version_name"] = prepared.Detail.Id,
            ["game_directory"] = gameDirectory,
            ["assets_root"] = prepared.AssetsRoot,
            ["game_assets"] = prepared.AssetsRoot,
            ["assets_index_name"] = prepared.AssetsIndexName,
            ["auth_uuid"] = identity.Uuid,
            ["auth_access_token"] = identity.AccessToken,
            ["auth_session"] = identity.AccessToken,
            ["clientid"] = "0",
            ["auth_xuid"] = "0",
            ["user_type"] = identity.UserType,
            ["version_type"] = prepared.Detail.Type,
            ["user_properties"] = "{}",
            ["natives_directory"] = prepared.NativesDirectory,
            ["launcher_name"] = LauncherName,
            ["launcher_version"] = LauncherVersion,
            ["classpath"] = className,
            ["classpath_separator"] = Path.PathSeparator.ToString(),
            ["library_directory"] = Path.Combine(resourceRoot, "libraries"),
            ["resolution_width"] = options.Width.ToString(),
            ["resolution_height"] = options.Height.ToString()
        };

        var jvm = new List<string>
        {
            $"-Xmx{Math.Max(512, options.MaxMemoryMb)}M",
            "-Dfile.encoding=UTF-8"
        };

        if (arguments?.Jvm.Count > 0)
        {
            foreach (var entry in arguments.Jvm)
            {
                if (!GameInstaller.IsAllowed(entry.Rules))
                {
                    continue;
                }

                foreach (var value in entry.Values)
                {
                    jvm.Add(Replace(value, values));
                }
            }
        }
        else
        {
            // 1.13 之前的版本没有 arguments.jvm
            jvm.Add("-Djava.library.path=" + prepared.NativesDirectory);
            jvm.Add("-cp");
            jvm.Add(className);
        }

        if (!string.IsNullOrEmpty(prepared.LogConfigArgument))
        {
            jvm.Add(prepared.LogConfigArgument!);
        }

        var game = new List<string>();
        if (arguments?.Game.Count > 0)
        {
            foreach (var entry in arguments.Game)
            {
                if (!GameInstaller.IsAllowed(entry.Rules))
                {
                    continue;
                }

                foreach (var value in entry.Values)
                {
                    game.Add(Replace(value, values));
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(prepared.Detail.MinecraftArguments))
        {
            game.AddRange(SplitArguments(prepared.Detail.MinecraftArguments).Select(x => Replace(x, values)));
        }

        if (!game.Any(x => string.Equals(x, "--width", StringComparison.OrdinalIgnoreCase)))
        {
            game.Add("--width");
            game.Add(options.Width.ToString());
            game.Add("--height");
            game.Add(options.Height.ToString());
        }

        return new LaunchCommand(java.Path, jvm, prepared.Detail.MainClass, game);
    }

    private static string Replace(string raw, Dictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(raw) || !raw.Contains("${", StringComparison.Ordinal))
        {
            return raw;
        }

        var result = raw;
        foreach (var pair in values)
        {
            result = result.Replace("${" + pair.Key + "}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static IEnumerable<string> SplitArguments(string raw) =>
        raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
