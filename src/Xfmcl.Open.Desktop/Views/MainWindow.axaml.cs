/**
 * MainWindow.axaml.cs - 桌面主窗口
 * 玹砺科技 XUANFORGE
 *
 * 窗口只承载跨平台主界面，逻辑全部位于 Xfmcl.Open.UI。
 * 本项目以 MIT 协议开源。
 */

using Avalonia.Controls;

namespace XfmclOpen.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
