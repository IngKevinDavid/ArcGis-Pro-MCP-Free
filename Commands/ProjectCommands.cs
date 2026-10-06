// Ported from arcgis-mcp v0.6.0 (MIT, Marco Gonzalez Valdiviezo), verbatim logic.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;

namespace ArcGisProMcpFree.Commands
{
    public static class ProjectCommands
    {
        public static async Task<object> CreateMapAsync(string name, string mapType, int wkid)
        {
            EnsureProject();
            if (mapType != "Map" && mapType != "Scene") throw new ArgumentException("map_type must be Map or Scene.");
            var map = await QueuedTask.Run(() =>
            {
                var created = MapFactory.Instance.CreateMap(name, mapType == "Scene" ? MapType.Scene : MapType.Map,
                    mapType == "Scene" ? MapViewingMode.SceneLocal : MapViewingMode.Map, Basemap.None);
                created.SetSpatialReference(ArcGIS.Core.Geometry.SpatialReferenceBuilder.CreateSpatialReference(wkid));
                return created;
            });
            await FrameworkApplication.Panes.CreateMapPaneAsync(map);
            return new { name, map_type = mapType, wkid };
        }

        public static async Task<object> OpenAttributeTableAsync(string layerName)
        {
            var layer = await QueuedTask.Run(() => MapView.Active?.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                .FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase)));
            if (layer == null) throw new ArgumentException("Feature layer not found: " + layerName);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => FrameworkApplication.Panes.OpenTablePane(layer));
            return new { layer_name = layerName, table_open = true };
        }

        public static async Task<object> OpenLayoutAsync(string layoutName)
        {
            var layout = await QueuedTask.Run(() => Project.Current.GetItems<LayoutProjectItem>()
                .FirstOrDefault(l => l.Name.Equals(layoutName, StringComparison.OrdinalIgnoreCase))?.GetLayout());
            if (layout == null) throw new ArgumentException("Layout not found: " + layoutName);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await FrameworkApplication.Panes.CreateLayoutPaneAsync(layout)).Task.Unwrap();
            return new { layout_name = layoutName, layout_open = true };
        }

        public static Task<object> ListMapsAsync()
        {
            return QueuedTask.Run<object>(() =>
            {
                EnsureProject();
                var maps = Project.Current.GetItems<MapProjectItem>()
                    .Select(item => new { name = item.Name })
                    .ToList();
                return new { maps };
            });
        }

        public static async Task<object> OpenMapAsync(string mapName)
        {
            EnsureProject();
            var item = await QueuedTask.Run(() =>
                Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(map => map.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase)));
            if (item == null)
            {
                throw new ArgumentException("Map '" + mapName + "' not found.");
            }
            var map = await QueuedTask.Run(() => item.GetMap());
            // Let the map's stored viewing mode select 2D, local scene, or global scene.
            IMapPane pane = await FrameworkApplication.Panes.CreateMapPaneAsync(map);
            return new
            {
                success = true,
                map_name = item.Name,
                pane_visible = pane != null
            };
        }

        public static async Task<object> SaveProjectAsAsync(string outputPath, bool overwrite)
        {
            return await Task.Factory.StartNew(
                async () =>
                {
                    EnsureProject();
                    var saved = await Project.Current.SaveAsAsync(outputPath, overwrite);
                    return (object)new
                    {
                        success = saved,
                        output_path = outputPath
                    };
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                QueuedTask.UIScheduler
            ).Unwrap();
        }

        public static Task<object> ListProjectItemsAsync()
        {
            return QueuedTask.Run<object>(() =>
            {
                EnsureProject();
                var items = Project.Current.GetItems<MapProjectItem>()
                    .Select(item => new { type = "Map", name = item.Name, path = "" })
                    .Cast<object>()
                    .Concat(Project.Current.GetItems<LayoutProjectItem>()
                        .Select(item => new { type = "Layout", name = item.Name, path = "" })
                        .Cast<object>())
                    .Append(new { type = "Project", name = Project.Current.Name, path = Project.Current.Path })
                    .ToList();
                return new { items };
            });
        }

        public static Task<object> ListBookmarksAsync(string mapName)
        {
            return QueuedTask.Run<object>(() =>
            {
                Map map = null;
                if (!string.IsNullOrWhiteSpace(mapName))
                {
                    var item = Project.Current.GetItems<MapProjectItem>()
                        .FirstOrDefault(candidate => candidate.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
                    if (item != null) map = item.GetMap();
                }
                else
                {
                    if (MapView.Active != null) map = MapView.Active.Map;
                }

                if (map == null)
                {
                    throw new InvalidOperationException("No map available for bookmark listing.");
                }

                var bookmarks = map.GetBookmarks()
                    .Select(bookmark => new { name = bookmark.Name })
                    .ToList();
                return new { map_name = map.Name, bookmarks };
            });
        }

        private static void EnsureProject()
        {
            if (Project.Current == null)
            {
                throw new InvalidOperationException("No active project found in ArcGIS Pro.");
            }
        }
    }
}
