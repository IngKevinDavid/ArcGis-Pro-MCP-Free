// Ported from arcgis-mcp v0.6.0 (MIT, Marco Gonzalez Valdiviezo), verbatim logic.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ArcGisProMcpFree.Commands
{
    public static class EditingCommands
    {
        private static EditOperation LastOperation { get; set; }

        public static async Task<object> UpdateAttributesAsync(
            string layerName,
            long objectId,
            Dictionary<string, object> attributes)
        {
            var operation = await QueuedTask.Run(() =>
            {
                var layer = GetFeatureLayer(layerName);
                var editOperation = new EditOperation
                {
                    Name = "MCP update attributes " + layerName + ":" + objectId
                };
                editOperation.Modify(layer, objectId, attributes);
                return editOperation;
            });

            if (!await operation.ExecuteAsync())
            {
                throw new InvalidOperationException(operation.ErrorMessage);
            }

            LastOperation = operation;
            return new { success = true, layer_name = layerName, object_id = objectId };
        }

        public static async Task<object> CreateFeatureAsync(
            string layerName,
            JsonElement geometryJson,
            int wkid,
            Dictionary<string, object> attributes)
        {
            var operation = await QueuedTask.Run(() =>
            {
                var layer = GetFeatureLayer(layerName);
                var spatialReference = SpatialReferenceBuilder.CreateSpatialReference(wkid);
                string kind = geometryJson.GetProperty("type").GetString();
                var coordinates = geometryJson.GetProperty("coordinates");
                Geometry geometry;
                if (kind == "Point")
                {
                    var xy = coordinates.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                    if (xy.Length != 2) throw new ArgumentException("Point needs x and y.");
                    geometry = MapPointBuilderEx.CreateMapPoint(xy[0], xy[1], spatialReference);
                }
                else
                {
                    var points = coordinates.EnumerateArray().Select(v =>
                    {
                        var xy = v.EnumerateArray().Select(n => n.GetDouble()).ToArray();
                        if (xy.Length != 2) throw new ArgumentException("Each vertex needs x and y.");
                        return MapPointBuilderEx.CreateMapPoint(xy[0], xy[1], spatialReference);
                    }).ToArray();
                    if (kind == "Polyline" && points.Length >= 2) geometry = PolylineBuilderEx.CreatePolyline(points, spatialReference);
                    else if (kind == "Polygon" && points.Length >= 4)
                    {
                        if (points[0].X != points[^1].X || points[0].Y != points[^1].Y) throw new ArgumentException("Polygon ring must be closed.");
                        geometry = PolygonBuilderEx.CreatePolygon(points, spatialReference);
                    }
                    else throw new ArgumentException("Expected Point, Polyline (2+ vertices), or closed Polygon (4+ vertices).");
                }
                using var featureClass = layer.GetFeatureClass();
                using var definition = featureClass.GetDefinition();
                if (geometry.GeometryType != definition.GetShapeType()) throw new ArgumentException("Geometry type does not match the target feature class.");
                geometry = GeometryEngine.Instance.Project(geometry, definition.GetSpatialReference());
                if (!GeometryEngine.Instance.IsSimpleAsFeature(geometry)) throw new ArgumentException("Geometry is not simple. Correct the vertices before creating it.");
                var editOperation = new EditOperation
                {
                    Name = "MCP create feature " + layerName
                };
                editOperation.Create(layer, geometry, attributes);
                return editOperation;
            });

            if (!await operation.ExecuteAsync())
            {
                throw new InvalidOperationException(operation.ErrorMessage);
            }

            LastOperation = operation;
            return new { success = true, layer_name = layerName, geometry_type = geometryJson.GetProperty("type").GetString(), wkid };
        }

        public static async Task<object> DeleteSelectedFeaturesAsync(string layerName)
        {
            var payload = await QueuedTask.Run(() =>
            {
                var layer = GetFeatureLayer(layerName);
                using (var selection = layer.GetSelection())
                {
                    var ids = selection.GetObjectIDs().ToList();
                    if (!ids.Any())
                    {
                        throw new InvalidOperationException("Layer '" + layerName + "' has no selected features.");
                    }
                    var editOperation = new EditOperation
                    {
                        Name = "MCP delete selected " + layerName
                    };
                    editOperation.Delete(layer, ids);
                    return new { operation = editOperation, count = ids.Count };
                }
            });

            if (!await payload.operation.ExecuteAsync())
            {
                throw new InvalidOperationException(payload.operation.ErrorMessage);
            }

            LastOperation = payload.operation;
            return new { success = true, layer_name = layerName, deleted_count = payload.count };
        }

        public static async Task<object> UndoLastEditAsync()
        {
            if (LastOperation == null)
            {
                throw new InvalidOperationException("No MCP edit operation is available to undo.");
            }

            if (!await LastOperation.UndoAsync())
            {
                throw new InvalidOperationException("ArcGIS Pro could not undo the last MCP edit operation.");
            }

            LastOperation = null;
            return new { success = true };
        }

        private static FeatureLayer GetFeatureLayer(string layerName)
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
            return layer;
        }
    }
}
