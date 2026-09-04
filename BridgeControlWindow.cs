using System;
using System.Windows;
using System.Windows.Controls;

namespace LibreMcpAddin
{
    /// <summary>
    /// Manual control panel for the MCP TCP bridge, built fully in code
    /// (no XAML): port with the default prefilled, Start/Stop buttons and
    /// live status. Listens on 127.0.0.1 only. Nothing starts on its own.
    /// </summary>
    public class BridgeControlWindow : Window
    {
        private static BridgeControlWindow _open;
        private TextBox _portBox;
        private TextBlock _status;

        public static void ShowPanel()
        {
            if (_open == null)
            {
                _open = new BridgeControlWindow();
                _open.Closed += (s, e) => _open = null;
                _open.Show();
            }
            else
            {
                if (_open.WindowState == WindowState.Minimized)
                    _open.WindowState = WindowState.Normal;
                _open.Activate();
            }
        }

        private BridgeControlWindow()
        {
            Title = "Puente MCP";
            Width = 340;
            Height = 300;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(12) };

            panel.Children.Add(new TextBlock
            {
                Text = "Puente MCP  (127.0.0.1)",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Puerto:",
                Margin = new Thickness(0, 6, 0, 2)
            });
            _portBox = new TextBox
            {
                Text = PipeServer.DefaultPort.ToString(),
                Margin = new Thickness(0, 0, 0, 2)
            };
            panel.Children.Add(_portBox);
            panel.Children.Add(new TextBlock
            {
                Text = "Por defecto: " + PipeServer.DefaultPort +
                       ". El MCP usa PORT (mismo default). " +
                       "Solo escucha en tu PC, nunca en la red.",
                FontStyle = FontStyles.Italic,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var start = new Button { Content = "Iniciar", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            start.Click += (s, e) => StartBridge();
            var stop = new Button { Content = "Detener", Width = 90 };
            stop.Click += (s, e) => StopBridge();
            row.Children.Add(start);
            row.Children.Add(stop);
            panel.Children.Add(row);

            _status = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            panel.Children.Add(_status);

            Content = panel;
            RefreshStatus();
        }

        private void StartBridge()
        {
            try
            {
                string endpoint = Module1.Current.StartBridge(_portBox.Text);
                _portBox.Text = Module1.SanitizePort(_portBox.Text).ToString();
                _status.Text = "ACTIVO  " + endpoint;
            }
            catch (Exception ex)
            {
                _status.Text = "Error al iniciar (puerto en uso?): " + ex.Message;
            }
        }

        private void StopBridge()
        {
            try
            {
                Module1.Current.StopBridge();
            }
            finally
            {
                RefreshStatus();
            }
        }

        private void RefreshStatus()
        {
            _status.Text = Module1.Current.IsBridgeRunning
                ? "ACTIVO  " + Module1.Current.CurrentEndpoint
                : "DETENIDO  (inicialo con Iniciar cuando lo necesites)";
        }
    }
}
