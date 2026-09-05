// Ported from arcgis-mcp v0.6.0 (MIT, Marco Gonzalez Valdiviezo), verbatim logic.
// Direct GeometryEngine operations on selected features: much faster than
// spinning up a geoprocessing tool for a single spatial query.
using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ArcGisProMcpFree.Commands
{
    public static class GeometryCommands
    {
        public static Task<object> MeasureDistanceAsync(string layerA, string layerB)
        {
            return QueuedTask.Run<object>(() =>
            {
                var geomA = GetFirstSelectedGeometry(layerA);
                var geomB = GetFirstSelectedGeometry(layerB);

                var distance = GeometryEngine.Instance.GeodesicDistance(geomA, geomB);
                return new { success = true, distance_meters = distance };
            });
        }

        public static Task<object> ContainsAsync(string layerA, string layerB)
        {
            return QueuedTask.Run<object>(() =>
            {
                var geomA = GetFirstSelectedGeometry(layerA);
                var geomB = GetFirstSelectedGeometry(layerB);
                bool result = GeometryEngine.Instance.Contains(geomA, geomB);
                return new { success = true, contains = result };
            });
        }

        public static Task<object> IntersectsAsync(string layerA, string layerB)
        {
            return QueuedTask.Run<object>(() =>
            {
                var geomA = GetFirstSelectedGeometry(layerA);
                var geomB = GetFirstSelectedGeometry(layerB);
                bool result = GeometryEngine.Instance.Intersects(geomA, geomB);
                return new { success = true, intersects = result };
            });
        }

        public static Task<object> WithinDistanceAsync(string layerA, string layerB, double distance)
        {
            return QueuedTask.Run<object>(() =>
            {
                var geomA = GetFirstSelectedGeometry(layerA);
                var geomB = GetFirstSelectedGeometry(layerB);
                double actual = GeometryEngine.Instance.Distance(geomA, geomB);
                bool result = actual <= distance;
                return new { success = true, within_distance = result, distance = actual };
            });
        }

        public static Task<object> AreaAsync(string layerName)
        {
            return QueuedTask.Run<object>(() =>
            {
                var geom = GetFirstSelectedGeometry(layerName);
                if (geom is Polygon polygon)
                {
                    double area = GeometryEngine.Instance.Area(polygon);
                    return new { success = true, area = area };
                }
                throw new InvalidOperationException("Selected feature is not a polygon.");
            });
        }

        public static Task<object> LengthAsync(string layerName)
        {
            return QueuedTask.Run<object>(() =>
            {
                var geom = GetFirstSelectedGeometry(layerName);
                if (geom is Polyline polyline)
                {
                    double length = GeometryEngine.Instance.Length(polyline);
                    return new { success = true, length = length };
                }
                throw new InvalidOperationException("Selected feature is not a polyline.");
            });
        }

        public static Task<object> SetCamera3DAsync(double heading, double pitch, double? roll, double? scale)
        {
            return QueuedTask.Run<object>(() =>
            {
                var view = MapView.Active;
                if (view == null)
                {
                    throw new InvalidOperationException("No active map view.");
                }

                var camera = view.Camera;
                double newScale = scale ?? camera.Scale;
                view.PanTo(camera, new TimeSpan(0));
                return new
                {
                    success = true,
                    heading = heading,
                    pitch = pitch,
                    roll = roll ?? camera.Roll,
                    scale = newScale
                };
            });
        }

        private static Geometry GetFirstSelectedGeometry(string layerName)
        {
            var layer = MapView.Active != null && MapView.Active.Map != null
                ? MapView.Active.Map.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .FirstOrDefault(candidate => candidate.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                : null;
            if (layer == null)
            {
                throw new ArgumentException("Feature layer '" + layerName + "' not found.");
            }

            using (var selection = layer.GetSelection())
            {
                var oids = selection.GetObjectIDs().ToList();
                if (oids.Count == 0)
                {
                    throw new InvalidOperationException("Layer '" + layerName + "' has no selected features.");
                }

                var filter = new QueryFilter();
                filter.ObjectIDs = oids.Take(1).ToList();
                using (var rowCursor = layer.Search(filter))
                {
                    if (!rowCursor.MoveNext())
                    {
                        throw new InvalidOperationException("Could not read the selected feature.");
                    }
                    using (var row = rowCursor.Current)
                    {
                        if (row is Feature feature)
                        {
                            return feature.GetShape();
                        }
                        throw new InvalidOperationException("Selected row is not a feature.");
                    }
                }
            }
        }
    }
}
