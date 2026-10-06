using System;
using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Framework.Controls;
namespace ArcGisProMcpFree
{
    public class BridgeControlWindow : ProWindow
    {
        private static BridgeControlWindow _window;
        private readonly TextBlock _status = new TextBlock { Margin = new Thickness(0, 14, 0, 14), TextWrapping = TextWrapping.Wrap };
        private readonly TextBox _port = new TextBox { Text = "5876", Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
        public static void ShowPanel()
        {
            if (_window == null) { _window = new BridgeControlWindow(); _window.Closed += (_, __) => _window = null; }
            _window.Show(); _window.Activate();
        }
        private BridgeControlWindow()
        {
            Title = "GIS 实验 MCP"; Width = 420; Height = 290; ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "本地连接端口" }); panel.Children.Add(_port); panel.Children.Add(_status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var start = new Button { Content = "启动 Start", Width = 120, Margin = new Thickness(0, 0, 12, 0) };
            var stop = new Button { Content = "停止 Stop", Width = 120 };
            start.Click += (_, __) =>
            {
                try {
                    if (!int.TryParse(_port.Text, out var port) || port < 1 || port > 65535) throw new ArgumentException("端口必须为 1 至 65535 的整数。");
                    _status.Text = "RUNNING " + Module1.Current.StartBridge(_port.Text);
                } catch (Exception ex) { _status.Text = ex.Message; }
            };
            stop.Click += (_, __) => { Module1.Current.StopBridge(); _status.Text = "STOPPED"; };
            buttons.Children.Add(start); buttons.Children.Add(stop); panel.Children.Add(buttons);
            panel.Children.Add(new TextBlock { Text = "原生工具参数面板、步骤截图与运行记录。\n仅监听本机，不提供 ArcPy 脚本执行入口。", Margin = new Thickness(0, 18, 0, 0), TextWrapping = TextWrapping.Wrap });
            _status.Text = Module1.Current.IsBridgeRunning ? "RUNNING " + Module1.Current.CurrentEndpoint : "STOPPED";
            Content = panel;
        }
    }
}
