/**
 * VersionRow.cs - 版本列表行模型
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

using System.ComponentModel;

namespace XfmclOpen.UI.Models;

/// <summary>版本列表中的一行。</summary>
public sealed class VersionRow : INotifyPropertyChanged
{
    private bool _installed;

    public required string Id { get; init; }

    public required string Type { get; init; }

    public string ReleaseDate { get; init; } = string.Empty;

    public bool Installed
    {
        get => _installed;
        set
        {
            if (_installed == value)
            {
                return;
            }

            _installed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
        }
    }

    public string TypeLabel => Type switch
    {
        "release" => "正式版",
        "snapshot" => "快照",
        "old_beta" => "旧测试版",
        "old_alpha" => "远古版",
        _ => Type
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}
