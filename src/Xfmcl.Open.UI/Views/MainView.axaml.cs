/**
 * MainView.axaml.cs - 主界面逻辑
 * 玹砺科技 XUANFORGE
 *
 * 只做三件事：离线登录、下载/补全版本文件、启动游戏。
 * 本项目以 MIT 协议开源。
 */

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XfmclOpen.Core.Models;
using XfmclOpen.Core.Services;
using XfmclOpen.UI.Models;

namespace XfmclOpen.UI.Views;

public partial class MainView : UserControl
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly Endpoints _endpoints = Endpoints.Load(Path.Combine(AppSettings.ConfigDirectory, "endpoints.json"));
    private readonly ObservableCollection<VersionRow> _all = new();
    private readonly ObservableCollection<VersionRow> _visible = new();
    private readonly StringBuilder _logText = new();
    private readonly LaunchService _service = new();

    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _gameRunning;
    private bool _initialized;

    public MainView()
    {
        InitializeComponent();
        VersionList.ItemsSource = _visible;

        _service.Output += line => AppendLog(line, timestamp: false);
        _service.Exited += code => Dispatcher.UIThread.Post(() =>
        {
            _gameRunning = false;
            AppendLog($"游戏进程已退出（退出码 {code}）。");
            StatusText.Text = "游戏已退出";
            UpdateButtons();
        });

        Loaded += async (_, _) =>
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            await InitializeAsync();
        };

        DetachedFromVisualTree += (_, _) => SaveSettings();
    }

    /// <summary>Android 端只提供下载与文件管理，启动需要桌面平台。</summary>
    private static bool CanLaunch => !OperatingSystem.IsAndroid();

    private DownloadSourceKind SelectedSource =>
        SourceMirrorButton.IsChecked == true ? DownloadSourceKind.BmclApi : DownloadSourceKind.Official;

    private async Task InitializeAsync()
    {
        NameBox.Text = _settings.PlayerName;
        var mirrorSelected = _settings.Source == DownloadSourceKind.BmclApi;
        SourceMirrorButton.IsChecked = mirrorSelected;
        SourceOfficialButton.IsChecked = !mirrorSelected;
        GameDirBox.Text = _settings.GameDirectory;

        _ = DetectJavaAsync();

        if (!_endpoints.IsConfigured)
        {
            StatusText.Text = "尚未配置下载端点";
            AppendLog("请复制 config/endpoints.example.json 到 " +
                      Path.Combine(AppSettings.ConfigDirectory, "endpoints.json") +
                      "，并填写实际下载地址。");
            return;
        }

        await LoadVersionsAsync(forceRefresh: false);
    }

    private async Task DetectJavaAsync()
    {
        JavaInfo.Text = "正在检测 Java…";
        var runtimes = await Task.Run(JavaLocator.Discover);

        if (runtimes.Count == 0)
        {
            JavaInfo.Text = CanLaunch ? "未检测到 Java" : "Android 端仅支持下载";
            JavaDot.Fill = new SolidColorBrush(Color.Parse("#F59E0B"));
            JavaInfo.Foreground = new SolidColorBrush(Color.Parse("#F0B860"));
            return;
        }

        JavaInfo.Text = "已检测 " + string.Join(
            "、",
            runtimes.DistinctBy(x => (x.MajorVersion, x.Version)).Take(2).Select(x => x.Display));
        JavaDot.Fill = new SolidColorBrush(Color.Parse("#4ADE80"));
        JavaInfo.Foreground = new SolidColorBrush(Color.Parse("#8FE0AE"));
    }

    private async Task LoadVersionsAsync(bool forceRefresh)
    {
        if (_busy)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true, "正在读取版本清单…");
        Bar.IsIndeterminate = true;

        try
        {
            var paths = new GamePaths(GameDirBox.Text ?? AppSettings.DefaultGameDirectory);
            paths.EnsureCreated();

            var repository = new VersionRepository(_service.Http, paths, new MirrorSource(SelectedSource, _endpoints));
            var manifest = await repository.GetManifestAsync(forceRefresh, _cts.Token);

            _all.Clear();
            foreach (var version in manifest.Versions)
            {
                _all.Add(new VersionRow
                {
                    Id = version.Id,
                    Type = version.Type,
                    ReleaseDate = version.ReleaseTime?.ToLocalTime().ToString("yyyy-MM-dd") ?? string.Empty,
                    Installed = File.Exists(paths.VersionJson(version.Id))
                });
            }

            ApplyFilter();
            AppendLog($"版本清单加载完成，共 {manifest.Versions.Count} 个版本。");

            var target = _all.FirstOrDefault(x => x.Id == manifest.Latest?.Release) ?? _all.FirstOrDefault();
            if (target != null)
            {
                VersionList.SelectedItem = target;
                Dispatcher.UIThread.Post(() => VersionList.ScrollIntoView(target));
            }

            StatusText.Text = $"共 {_all.Count} 个版本，准备就绪";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "已取消";
        }
        catch (Exception ex)
        {
            StatusText.Text = "版本清单获取失败";
            AppendLog("读取版本清单失败：" + ex.Message);
        }
        finally
        {
            Bar.IsIndeterminate = false;
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void ApplyFilter()
    {
        var keyword = FilterBox.Text?.Trim() ?? string.Empty;
        var selected = VersionList.SelectedItem as VersionRow;

        _visible.Clear();
        foreach (var row in _all)
        {
            if (keyword.Length == 0 || row.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                _visible.Add(row);
            }
        }

        if (selected != null && _visible.Contains(selected))
        {
            VersionList.SelectedItem = selected;
        }
    }

    private void RefreshInstalledFlags()
    {
        var paths = new GamePaths(GameDirBox.Text ?? AppSettings.DefaultGameDirectory);
        foreach (var row in _all)
        {
            row.Installed = File.Exists(paths.VersionJson(row.Id));
        }
    }

    private async Task RunAsync(bool installOnly)
    {
        if (_busy || _gameRunning)
        {
            return;
        }

        if (VersionList.SelectedItem is not VersionRow row)
        {
            StatusText.Text = "请先选择一个版本";
            return;
        }

        var name = NameBox.Text?.Trim() ?? string.Empty;
        if (!OfflineIdentity.IsValidName(name))
        {
            StatusText.Text = "玩家名需为 3-16 位字母、数字或下划线";
            NameBox.Focus();
            return;
        }

        if (!_endpoints.IsConfigured)
        {
            StatusText.Text = "尚未配置下载端点";
            AppendLog("请先配置 endpoints.json 后再下载或启动。");
            return;
        }

        SaveSettings();

        _cts = new CancellationTokenSource();
        SetBusy(true, installOnly ? $"正在下载 {row.Id}…" : $"正在准备 {row.Id}…");
        Bar.IsIndeterminate = false;
        Bar.Maximum = 1;
        Bar.Value = 0;

        try
        {
            var options = new LaunchOptions
            {
                GameDirectory = GameDirBox.Text ?? AppSettings.DefaultGameDirectory,
                VersionId = row.Id,
                PlayerName = name,
                Source = SelectedSource,
                Endpoints = _endpoints,
                InstallOnly = installOnly
            };

            var progress = new Progress<InstallProgress>(ReportProgress);
            var session = await _service.LaunchAsync(options, progress, line => AppendLog(line), _cts.Token);

            if (installOnly)
            {
                Bar.Value = Bar.Maximum;
                row.Installed = true;
                StatusText.Text = $"{row.Id} 下载完成";
            }
            else if (session.Process != null)
            {
                _gameRunning = true;
                StatusText.Text = "游戏已启动";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "已取消";
            AppendLog("任务已取消。");
        }
        catch (Exception ex)
        {
            StatusText.Text = "操作失败";
            AppendLog("错误：" + ex.Message);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void ReportProgress(InstallProgress progress)
    {
        Bar.Maximum = Math.Max(1, progress.Total);
        Bar.Value = progress.Completed;

        var stage = progress.Stage == InstallStage.Libraries ? "游戏库" : "资源文件";
        ProgressText.Text = $"{stage} {progress.Completed}/{progress.Total} · {FormatBytes(progress.Bytes)}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024.0).ToString("0.0") + " KB";
        }

        return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;

        if (status != null)
        {
            StatusText.Text = status;
        }

        NameBox.IsEnabled = !busy;
        SourceOfficialButton.IsEnabled = !busy;
        SourceMirrorButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !busy;
        FilterBox.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        VersionList.IsEnabled = !busy;
        CancelButton.IsVisible = busy;

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var row = VersionList.SelectedItem as VersionRow;
        var hasSelection = row != null;

        InstallButton.IsEnabled = !_busy && !_gameRunning && hasSelection;
        LaunchButton.IsEnabled = !_busy && !_gameRunning && hasSelection && CanLaunch;

        SelectedVersionText.Text = row?.Id ?? "—";
        SelectedStateText.Text = row == null ? "—" : row.Installed ? "已安装" : "未安装";
        SelectedStateText.Foreground = new SolidColorBrush(Color.Parse(
            row == null ? "#8A93A6" : row.Installed ? "#4ADE80" : "#8A93A6"));
    }

    private void AppendLog(string line, bool timestamp = true)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => AppendLog(line, timestamp));
            return;
        }

        if (timestamp)
        {
            _logText.Append(DateTime.Now.ToString("HH:mm:ss")).Append(' ');
        }

        _logText.AppendLine(line);

        // 只保留最近一段输出，避免长时间运行时日志无限增长
        if (_logText.Length > 20000)
        {
            var text = _logText.ToString();
            var cut = text.IndexOf('\n', Math.Max(0, text.Length - 14000));
            if (cut > 0)
            {
                _logText.Clear();
                _logText.Append(text[(cut + 1)..]);
            }
        }

        LogText.Text = _logText.ToString();
        LogScroll.ScrollToEnd();
    }

    private void SaveSettings()
    {
        _settings.PlayerName = NameBox.Text?.Trim() ?? "Player";
        _settings.Source = SelectedSource;
        _settings.GameDirectory = GameDirBox.Text ?? AppSettings.DefaultGameDirectory;
        _settings.Save();
    }

    private void VersionList_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void Source_Click(object? sender, RoutedEventArgs e)
    {
        // 分段按钮保持单选，不允许两个都取消
        var mirror = ReferenceEquals(sender, SourceMirrorButton);
        SourceOfficialButton.IsChecked = !mirror;
        SourceMirrorButton.IsChecked = mirror;

        _settings.Source = SelectedSource;
        _settings.Save();
    }

    private void Filter_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private async void Install_Click(object? sender, RoutedEventArgs e) => await RunAsync(installOnly: true);

    private async void Launch_Click(object? sender, RoutedEventArgs e) => await RunAsync(installOnly: false);

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StatusText.Text = "正在取消…";
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e) => await LoadVersionsAsync(forceRefresh: true);

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            return;
        }

        try
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择游戏目录",
                AllowMultiple = false
            });

            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameDirBox.Text = path;
            SaveSettings();
            RefreshInstalledFlags();
            await LoadVersionsAsync(forceRefresh: false);
        }
        catch (Exception ex)
        {
            AppendLog("选择目录失败：" + ex.Message);
        }
    }

    private void OpenDir_Click(object? sender, RoutedEventArgs e)
    {
        var directory = GameDirBox.Text;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!CanLaunch)
        {
            StatusText.Text = "Android 端请通过文件管理器查看游戏目录";
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);

            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{directory}\"") { UseShellExecute = false });
            }
        }
        catch (Exception ex)
        {
            AppendLog("打开目录失败：" + ex.Message);
        }
    }
}
