using System;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace ArcGisProMcpFree
{
    public class Module1 : Module
    {
        private static Module1 _this = null;
        private PipeServer _bridge = null;
        private int _port = PipeServer.DefaultPort;

        /// <summary>
        /// Retrieve the singleton instance of this module.
        /// </summary>
        public static Module1 Current
        {
            get
            {
                if (_this == null)
                    _this = (Module1)FrameworkApplication.FindModule("ArcGisProMcpFree_Module");
                return _this;
            }
        }

        public bool IsBridgeRunning => _bridge != null && _bridge.IsRunning;

        public string CurrentEndpoint =>
            PipeServer.LoopbackHost + ":" + (_bridge != null ? _bridge.Port : _port);

        public static int SanitizePort(string text)
        {
            return PipeServer.ParsePort(text);
        }

        /// <summary>
        /// Starts the bridge on the given port (manual activation only).
        /// Restarts it if it was already running. Returns "127.0.0.1:port".
        /// </summary>
        public string StartBridge(string portText)
        {
            StopBridge();
            _port = SanitizePort(portText);
            _bridge = new PipeServer(_port);
            _bridge.Start();
            return CurrentEndpoint;
        }

        public void StopBridge()
        {
            try
            {
                if (_bridge != null)
                    _bridge.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error stopping bridge: " + ex.Message);
            }
            finally
            {
                _bridge = null;
            }
        }

        #region Overrides
        /// <summary>
        /// Called when the Module is initialized. The bridge is NOT started here:
        /// the user starts it manually from the MCP Free Bridge window.
        /// </summary>
        protected override bool Initialize()
        {
            return base.Initialize();
        }

        /// <summary>
        /// Called when ArcGIS Pro is closing and unloading the module.
        /// </summary>
        protected override void Uninitialize()
        {
            StopBridge();
            base.Uninitialize();
        }

        protected override bool CanUnload()
        {
            return true;
        }
        #endregion
    }

    /// <summary>
    /// Ribbon button that opens the manual bridge control window.
    /// Traffic light: red icon while the bridge is stopped, green while running.
    /// </summary>
    public class BridgeControlButton : Button
    {
        private static System.Windows.Media.Imaging.BitmapImage _red16;
        private static System.Windows.Media.Imaging.BitmapImage _red32;
        private static System.Windows.Media.Imaging.BitmapImage _green16;
        private static System.Windows.Media.Imaging.BitmapImage _green32;
        private bool? _lastRunning;

        private static System.Windows.Media.Imaging.BitmapImage Icon(string file)
        {
            return new System.Windows.Media.Imaging.BitmapImage(new System.Uri(
                "pack://application:,,,/ArcGisProMcpFree;component/Images/" + file));
        }

        protected override void OnClick()
        {
            BridgeControlWindow.ShowPanel();
        }

        protected override void OnUpdate()
        {
            bool running = Module1.Current != null && Module1.Current.IsBridgeRunning;
            if (_lastRunning == running)
                return;
            _lastRunning = running;
            if (running)
            {
                if (_green16 == null) _green16 = Icon("mcp_green16.png");
                if (_green32 == null) _green32 = Icon("mcp_green32.png");
                SmallImage = _green16;
                LargeImage = _green32;
            }
            else
            {
                if (_red16 == null) _red16 = Icon("mcp_red16.png");
                if (_red32 == null) _red32 = Icon("mcp_red32.png");
                SmallImage = _red16;
                LargeImage = _red32;
            }
        }
    }
}
