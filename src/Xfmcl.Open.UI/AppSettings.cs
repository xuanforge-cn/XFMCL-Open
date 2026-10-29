/**
 * AppSettings.cs - 本地设置读写
 * 玹砺科技 XUANFORGE
 *
 * 只保存玩家名、下载源与游戏目录，文件为简单的 key=value 文本。
 * 本项目以 MIT 协议开源。
 */

using XfmclOpen.Core.Models;

namespace XfmclOpen.UI;

/// <summary>启动器本地设置（跨平台配置目录）。</summary>
public sealed class AppSettings
{
    public string PlayerName { get; set; } = "Player";

    public DownloadSourceKind Source { get; set; } = DownloadSourceKind.Official;

    public string GameDirectory { get; set; } = DefaultGameDirectory;

    /// <summary>配置目录：各平台的用户配置根目录下 XFMCL-Open。</summary>
    public static string ConfigDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Combine(appData, "XFMCL-Open");
        }
    }

    /// <summary>各平台惯用的 .minecraft 位置。</summary>
    public static string DefaultGameDirectory
    {
        get
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(profile, "Library", "Application Support", "minecraft");
            }

            return Path.Combine(profile, ".minecraft");
        }
    }

    private static string FilePath => Path.Combine(ConfigDirectory, "settings.ini");

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        try
        {
            if (!File.Exists(FilePath))
            {
                return settings;
            }

            foreach (var line in File.ReadLines(FilePath))
            {
                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                var key = line[..split].Trim();
                var value = line[(split + 1)..].Trim();

                switch (key)
                {
                    case "name" when value.Length > 0:
                        settings.PlayerName = value;
                        break;
                    case "source":
                        settings.Source = string.Equals(value, "bmclapi", StringComparison.OrdinalIgnoreCase)
                            ? DownloadSourceKind.BmclApi
                            : DownloadSourceKind.Official;
                        break;
                    case "dir" when value.Length > 0:
                        settings.GameDirectory = value;
                        break;
                }
            }
        }
        catch (IOException)
        {
            // 配置损坏时使用默认值
        }
        catch (UnauthorizedAccessException)
        {
            // 只读目录时使用默认值
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var source = Source == DownloadSourceKind.BmclApi ? "bmclapi" : "official";
            File.WriteAllLines(FilePath, new[]
            {
                "name=" + PlayerName,
                "source=" + source,
                "dir=" + GameDirectory
            });
        }
        catch (IOException)
        {
            // 保存失败不影响本次使用
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}
