using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace ArcGisProMcpFree
{
    /// <summary>
    /// Manual control panel for the MCP TCP bridge, built fully in code
    /// (no XAML): port with the default prefilled, Start/Stop buttons,
    /// live status and author contact. English by default, Spanish on
    /// toggle (persisted). Listens on 127.0.0.1 only. Nothing starts alone.
    /// </summary>
    public class BridgeControlWindow : Window
    {
        private const string Email = "ingkevindavid@gmail.com";
        private const string LinkedIn = "https://www.linkedin.com/in/kevin-david-condori-quispe/";

        private static BridgeControlWindow _open;
        private TextBox _portBox;
        private TextBlock _status;
        private TextBlock _header;
        private TextBlock _portLabel;
        private TextBlock _note;
        private TextBlock _contactHeader;
        private Button _startBtn;
        private Button _stopBtn;
        private Button _langBtn;

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
            Width = 340;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;

            var panel = new StackPanel { Margin = new Thickness(12) };

            _header = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(_header);
            _portLabel = new TextBlock { Margin = new Thickness(0, 6, 0, 2) };
            panel.Children.Add(_portLabel);
            _portBox = new TextBox
            {
                Text = PipeServer.DefaultPort.ToString(),
                Margin = new Thickness(0, 0, 0, 2)
            };
            panel.Children.Add(_portBox);
            _note = new TextBlock
            {
                FontStyle = FontStyles.Italic,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(_note);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            _startBtn = new Button { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            _startBtn.Click += (s, e) => StartBridge();
            _stopBtn = new Button { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            _stopBtn.Click += (s, e) => StopBridge();
            _langBtn = new Button { Width = 90 };
            _langBtn.Click += (s, e) => { Lang.Toggle(); RefreshTexts(); };
            row.Children.Add(_startBtn);
            row.Children.Add(_stopBtn);
            row.Children.Add(_langBtn);
            panel.Children.Add(row);

            _status = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            };
            panel.Children.Add(_status);

            panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 8) });
            _contactHeader = new TextBlock
            {
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(_contactHeader);

            var contact = new StackPanel { Orientation = Orientation.Horizontal };
            var photo = new Image
            {
                Width = 84,
                Height = 84,
                Margin = new Thickness(0, 0, 10, 0),
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };
            try
            {
                photo.Source = new BitmapImage(new Uri(
                    "pack://application:,,,/ArcGisProMcpFree;component/Images/creator.jpg"));
            }
            catch
            {
                photo.Visibility = Visibility.Collapsed;
            }
            contact.Children.Add(photo);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = "Ing. Kevin David Condori Q.",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            var mail = new TextBlock { Margin = new Thickness(0, 2, 0, 0) };
            var mailLink = new Hyperlink(new Run(Email))
            {
                NavigateUri = new Uri("mailto:" + Email)
            };
            mailLink.RequestNavigate += (s, e) => OpenUrl(e.Uri.AbsoluteUri);
            mail.Inlines.Add(mailLink);
            info.Children.Add(mail);
            var li = new TextBlock { Margin = new Thickness(0, 2, 0, 0) };
            var liLink = new Hyperlink(new Run("LinkedIn"))
            {
                NavigateUri = new Uri(LinkedIn)
            };
            liLink.RequestNavigate += (s, e) => OpenUrl(e.Uri.AbsoluteUri);
            li.Inlines.Add(liLink);
            info.Children.Add(li);
            contact.Children.Add(info);
            panel.Children.Add(contact);

            Content = panel;
            RefreshTexts();
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OpenUrl failed: " + ex.Message);
            }
        }

        private void RefreshTexts()
        {
            Title = "MCP Free Bridge";
            _header.Text = Lang.T("MCP Free Bridge  (127.0.0.1)", "MCP Free Bridge  (127.0.0.1)");
            _portLabel.Text = Lang.T("Port:", "Puerto:");
            _note.Text = Lang.T(
                "Default: " + PipeServer.DefaultPort +
                ". The MCP uses PORT (same default). Listens on your PC only, never on the network.",
                "Por defecto: " + PipeServer.DefaultPort +
                ". El MCP usa PORT (mismo default). Solo escucha en tu PC, nunca en la red.");
            _startBtn.Content = Lang.T("Start", "Iniciar");
            _stopBtn.Content = Lang.T("Stop", "Detener");
            _langBtn.Content = Lang.IsSpanish ? "English" : "Español";
            _contactHeader.Text = Lang.T("Contact", "Contacto");
            RefreshStatus();
        }

        private void StartBridge()
        {
            try
            {
                string endpoint = Module1.Current.StartBridge(_portBox.Text);
                _portBox.Text = Module1.SanitizePort(_portBox.Text).ToString();
                _status.Text = Lang.T("RUNNING  ", "ACTIVO  ") + endpoint;
            }
            catch (Exception ex)
            {
                _status.Text = Lang.T("Failed to start (port busy?): ",
                                      "Error al iniciar (puerto en uso?): ") + ex.Message;
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
                ? Lang.T("RUNNING  ", "ACTIVO  ") + Module1.Current.CurrentEndpoint
                : Lang.T("STOPPED  (press Start when you need it)",
                         "DETENIDO  (inicialo con Iniciar cuando lo necesites)");
        }
    }
}
