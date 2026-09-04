using System;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace LibreMcpAddin
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
                    _this = (Module1)FrameworkApplication.FindModule("LibreMcpAddin_Module");
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
        /// the user starts it manually from the Libre MCP window.
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
    /// </summary>
    public class BridgeControlButton : Button
    {
        protected override void OnClick()
        {
            BridgeControlWindow.ShowPanel();
        }
    }
}
