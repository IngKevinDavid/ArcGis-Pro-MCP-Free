// Ported from arcgis-mcp v0.6.0 (MIT, Marco Gonzalez Valdiviezo), verbatim logic.
// Bulk data-access operations equivalent to arcpy.da cursors: batches of
// records (already JSON-serialised) run as a single grouped EditOperation.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ArcGisProMcpFree.Commands
{
    public static class DataAccessCommands
    {
        /// <summary>
        /// Inserts multiple point features in a single edit operation.
        /// Each item in ``features`` is { x, y, attributes?, wkid? }.
        /// </summary>
        public static async Task<object> InsertFeaturesAsync(
            string layerName,
            List<Dictionary<string, object>> features)
        {
            if (features == null || features.Count == 0)
            {
                throw new ArgumentException("No features provided for insertion.");
            }

            var op = await QueuedTask.Run(() =>
            {
                var layer = GetFeatureLayer(layerName);
                var editOperation = new EditOperation
                {
                    Name = "MCP insert " + features.Count + " features into " + layerName
                };

                foreach (var feature in features)
                {
                    double x = Convert.ToDouble(feature["x"]);
                    double y = Convert.ToDouble(feature["y"]);
                    object w;
                    int wkid = feature.TryGetValue("wkid", out w) && w != null ? Convert.ToInt32(w) : 4326;
                    var sr = SpatialReferenceBuilder.CreateSpatialReference(wkid);
                    var point = MapPointBuilderEx.CreateMapPoint(x, y, sr);

                    var attributes = new Dictionary<string, object>();
                    object attrObj;
                    if (feature.TryGetValue("attributes", out attrObj) && attrObj is Dictionary<string, object>)
                    {
                        attributes = (Dictionary<string, object>)attrObj;
                    }
                    editOperation.Create(layer, point, attributes);
                }

                return editOperation;
            });

            if (!await op.ExecuteAsync())
            {
                throw new InvalidOperationException(op.ErrorMessage);
            }

            return new { success = true, layer_name = layerName, inserted_count = features.Count };
        }

        /// <summary>
        /// Updates multiple features by ObjectID in a single edit operation.
        /// Each item in ``updates`` is { objectid, attributes }.
        /// </summary>
        public static async Task<object> UpdateFeaturesAsync(
            string layerName,
            List<Dictionary<string, object>> updates)
        {
            if (updates == null || updates.Count == 0)
            {
                throw new ArgumentException("No updates provided.");
            }

            var op = await QueuedTask.Run(() =>
            {
                var layer = GetFeatureLayer(layerName);
                var editOperation = new EditOperation
                {
                    Name = "MCP update " + updates.Count + " features in " + layerName
                };

                foreach (var update in updates)
                {
                    long oid = Convert.ToInt64(update["objectid"]);
                    var attributes = new Dictionary<string, object>();
                    object attrObj;
                    if (update.TryGetValue("attributes", out attrObj) && attrObj is Dictionary<string, object>)
                    {
                        attributes = (Dictionary<string, object>)attrObj;
                    }
                    editOperation.Modify(layer, oid, attributes);
                }

                return editOperation;
            });

            if (!await op.ExecuteAsync())
            {
                throw new InvalidOperationException(op.ErrorMessage);
            }

            return new { success = true, layer_name = layerName, updated_count = updates.Count };
        }

        /// <summary>
        /// Deletes the features identified by the given ObjectIDs.
        /// </summary>
        public static async Task<object> DeleteFeaturesAsync(
            string layerName,
            List<long> objectIds)
        {
            if (objectIds == null || objectIds.Count == 0)
            {
                throw new ArgumentException("No ObjectIDs provided for deletion.");
            }

            var op = await QueuedTask.Run(() =>
            {
                var layer = GetFeatureLayer(layerName);
                var editOperation = new EditOperation
                {
                    Name = "MCP delete " + objectIds.Count + " features from " + layerName
                };
                editOperation.Delete(layer, objectIds);
                return editOperation;
            });

            if (!await op.ExecuteAsync())
            {
                throw new InvalidOperationException(op.ErrorMessage);
            }

            return new { success = true, layer_name = layerName, deleted_count = objectIds.Count };
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
