using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
namespace ArcGisProMcpFree.Commands
{
    public static class CoreCommands
    {
        public static async Task<object> HealthCheckAsync()
        {
            long window = await Application.Current.Dispatcher.InvokeAsync(() => new WindowInteropHelper(Application.Current.MainWindow).Handle.ToInt64());
            return await QueuedTask.Run<object>(() => new
            {
                protocol_version = 2, addin_version = "0.2.3-lab",
                process_id = Process.GetCurrentProcess().Id, window_handle = window,
                arcgis_pro_version = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName).ProductVersion,
                project_open = Project.Current != null, project_path = Project.Current?.Path ?? "",
                active_map = MapView.Active?.Map?.Name ?? "", transport = "localhost_tcp",
                gp_busy = GeoprocessingCommands.IsRunning
            });
        }
    }
}
