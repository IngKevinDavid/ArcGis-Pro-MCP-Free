// Libre subset dispatcher: same {command, params} protocol and response
// envelope as arcgis-mcp v0.6.0 (MIT). Unsupported commands report
// "Unsupported command" exactly like upstream.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using ArcGisProMcpFree.Commands;

namespace ArcGisProMcpFree
{
    public static class CommandHandler
    {
        public static async Task<string> HandleAsync(string jsonRequest)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using (var doc = JsonDocument.Parse(jsonRequest))
                {
                    var root = doc.RootElement;

                    JsonElement cmdProp;
                    if (!root.TryGetProperty("command", out cmdProp) || cmdProp.GetString() == null)
                    {
                        return SerializeError("Missing or invalid 'command' property in request.");
                    }

                    string command = cmdProp.GetString();
                    JsonElement paramsEl = root.TryGetProperty("params", out var paramsProp) ? paramsProp : default;

                    System.Diagnostics.Debug.WriteLine("Processing MCP Command: " + command);

                    object resultData = null;

                    switch (command)
                    {
                        case "ping":
                            resultData = new { message = "pong", time = DateTime.Now.ToString("o") };
                            break;

                        case "health_check":
                            resultData = await CoreCommands.HealthCheckAsync();
                            break;

                        case "get_capabilities":
                            resultData = await CoreCommands.GetCapabilitiesAsync();
                            break;

                        case "check_license":
                            resultData = await LicenseCommands.CheckLicenseAsync();
                            break;

                        case "list_maps":
                            resultData = await ProjectCommands.ListMapsAsync();
                            break;

                        case "open_map":
                            resultData = await ProjectCommands.OpenMapAsync(ReqStr(paramsEl, "map_name", true));
                            break;

                        case "save_project_as":
                            resultData = await ProjectCommands.SaveProjectAsAsync(
                                ReqStr(paramsEl, "output_path", true),
                                OptBool(paramsEl, "overwrite", false));
                            break;

                        case "list_project_items":
                            resultData = await ProjectCommands.ListProjectItemsAsync();
                            break;

                        case "list_bookmarks":
                            resultData = await ProjectCommands.ListBookmarksAsync(OptStr(paramsEl, "map_name", ""));
                            break;

                        // Map Commands
                        case "get_active_map":
                            resultData = await MapCommands.GetActiveMapAsync();
                            break;

                        case "list_layers":
                            resultData = await MapCommands.ListLayersAsync(OptBool(paramsEl, "include_hidden", false));
                            break;

                        case "zoom_to_layer":
                            resultData = await MapCommands.ZoomToLayerAsync(ReqStr(paramsEl, "layer_name", true));
                            break;

                        case "toggle_layer_visibility":
                            resultData = await MapCommands.ToggleLayerVisibilityAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqBool(paramsEl, "visible"));
                            break;

                        case "set_map_extent":
                            resultData = await MapCommands.SetMapExtentAsync(
                                ReqDouble(paramsEl, "xmin"),
                                ReqDouble(paramsEl, "ymin"),
                                ReqDouble(paramsEl, "xmax"),
                                ReqDouble(paramsEl, "ymax"),
                                OptInt(paramsEl, "wkid", 4326));
                            break;

                        case "add_layer_to_map":
                            resultData = await MapCommands.AddLayerToMapAsync(
                                ReqStr(paramsEl, "data_path", true),
                                OptStr(paramsEl, "layer_name", ""));
                            break;

                        case "create_group_layer":
                            resultData = await MapCommands.CreateGroupLayerAsync(
                                ReqStr(paramsEl, "group_name", true),
                                OptStrArray(paramsEl, "layer_names"));
                            break;

                        case "add_layer_to_group":
                            resultData = await MapCommands.AddLayerToGroupAsync(
                                ReqStr(paramsEl, "group_name", true),
                                OptStrArray(paramsEl, "layer_names"));
                            break;

                        case "set_layer_transparency":
                            resultData = await MapCommands.SetLayerTransparencyAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqDouble(paramsEl, "transparency"));
                            break;

                        case "set_definition_query":
                            resultData = await MapCommands.SetDefinitionQueryAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                OptStr(paramsEl, "sql_filter", ""));
                            break;

                        case "clear_selection":
                            resultData = await MapCommands.ClearSelectionAsync(OptStr(paramsEl, "layer_name", ""));
                            break;

                        case "save_project":
                            resultData = await MapCommands.SaveProjectAsync();
                            break;

                        // Data Commands
                        case "count_features":
                            resultData = await DataCommands.CountFeaturesAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                OptStr(paramsEl, "sql_filter", ""));
                            break;

                        case "select_features":
                            resultData = await DataCommands.SelectFeaturesAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqStr(paramsEl, "sql_filter", true),
                                OptStr(paramsEl, "combination", "NEW"));
                            break;

                        case "get_selected_features":
                            resultData = await DataCommands.GetSelectedFeaturesAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                OptInt(paramsEl, "max_features", 100));
                            break;

                        case "get_layer_fields":
                            resultData = await DataCommands.GetLayerFieldsAsync(ReqStr(paramsEl, "layer_name", true));
                            break;

                        case "query_layer":
                            resultData = await DataCommands.QueryLayerAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                OptStr(paramsEl, "where_clause", "1=1"),
                                OptStr(paramsEl, "fields", "*"),
                                OptInt(paramsEl, "limit", 100),
                                OptBool(paramsEl, "include_geometry", false));
                            break;

                        // Symbology and Labeling Commands
                        case "apply_symbology_from_layer":
                            resultData = await SymbologyCommands.ApplySymbologyFromLayerAsync(
                                ReqStr(paramsEl, "target_layer", true),
                                ReqStr(paramsEl, "symbology_layer", true));
                            break;

                        case "label_layer":
                            resultData = await SymbologyCommands.LabelLayerAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqStr(paramsEl, "field_name", true),
                                OptBool(paramsEl, "visible", true),
                                OptStr(paramsEl, "expression_engine", "Arcade"),
                                OptDouble(paramsEl, "halo_size", 0),
                                OptStr(paramsEl, "halo_color", "#FFFFFF"));
                            break;

                        case "get_layer_symbology":
                            resultData = await SymbologyCommands.GetLayerSymbologyAsync(ReqStr(paramsEl, "layer_name", true));
                            break;

                        // Layer IO Commands
                        case "save_layer_file":
                            resultData = await LayerIoCommands.SaveLayerFileAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqStr(paramsEl, "output_path", true));
                            break;

                        case "load_layer_file":
                            resultData = await LayerIoCommands.LoadLayerFileAsync(ReqStr(paramsEl, "layer_file_path", true));
                            break;

                        case "export_layer":
                            resultData = await LayerIoCommands.ExportLayerAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqStr(paramsEl, "output_path", true),
                                OptStr(paramsEl, "where_clause", ""));
                            break;

                        case "remove_layer":
                            resultData = await LayerIoCommands.RemoveLayerAsync(ReqStr(paramsEl, "layer_name", true));
                            break;

                        // Geoprocessing Commands
                        case "run_gp_tool":
                            {
                                string gpTool = ReqStr(paramsEl, "tool_name", true);
                                bool gpAllowDelete = OptBool(paramsEl, "allow_delete", false);
                                bool addGpOutputs = OptBool(paramsEl, "add_outputs_to_map", false);
                                var paramListProp = paramsEl.GetProperty("parameters");
                                string[] gpParams = new string[paramListProp.GetArrayLength()];
                                int index = 0;
                                foreach (var item in paramListProp.EnumerateArray())
                                {
                                    gpParams[index++] = item.GetString() ?? "";
                                }
                                resultData = await GeoprocessingCommands.RunGpToolAsync(gpTool, gpParams, gpAllowDelete, addGpOutputs);
                                break;
                            }

                        // Geodatabase Commands
                        case "list_feature_classes":
                            resultData = await GeodatabaseCommands.ListFeatureClassesAsync(ReqStr(paramsEl, "workspace_path", true));
                            break;

                        case "list_domains":
                            resultData = await GeodatabaseCommands.ListDomainsAsync(ReqStr(paramsEl, "workspace_path", true));
                            break;

                        case "create_domain":
                            resultData = await GeodatabaseCommands.CreateDomainAsync(
                                ReqStr(paramsEl, "workspace_path", true),
                                ReqStr(paramsEl, "domain_name", true),
                                OptStr(paramsEl, "field_type", "TEXT"),
                                OptStr(paramsEl, "domain_type", "CODED"),
                                OptStr(paramsEl, "description", ""));
                            break;

                        case "describe_dataset":
                            resultData = await GeodatabaseCommands.DescribeDatasetAsync(ReqStr(paramsEl, "dataset_path", true));
                            break;

                        // Bookmark commands (list_bookmarks already handled above)
                        case "create_bookmark":
                            resultData = await BookmarkCommands.CreateBookmarkAsync(ReqStr(paramsEl, "name", true));
                            break;

                        case "zoom_to_bookmark":
                            resultData = await BookmarkCommands.ZoomToBookmarkAsync(ReqStr(paramsEl, "name", true));
                            break;

                        case "delete_bookmark":
                            resultData = await BookmarkCommands.DeleteBookmarkAsync(ReqStr(paramsEl, "name", true));
                            break;

                        // Layout Commands
                        case "list_layouts":
                            resultData = await LayoutCommands.ListLayoutsAsync();
                            break;

                        case "export_layout":
                            resultData = await LayoutCommands.ExportLayoutAsync(
                                ReqStr(paramsEl, "layout_name", true),
                                ReqStr(paramsEl, "output_path", true),
                                OptStr(paramsEl, "format", "PDF"),
                                OptInt(paramsEl, "resolution", 300));
                            break;

                        case "export_all_layouts":
                            resultData = await LayoutCommands.ExportAllLayoutsAsync(
                                ReqStr(paramsEl, "output_directory", true),
                                OptStr(paramsEl, "format", "PDF"),
                                OptInt(paramsEl, "resolution", 300),
                                OptBool(paramsEl, "include_map_series", true));
                            break;

                        case "create_basic_layout":
                            resultData = await LayoutCommands.CreateBasicLayoutAsync(
                                ReqStr(paramsEl, "layout_name", true),
                                OptStr(paramsEl, "title", ""),
                                OptDouble(paramsEl, "page_width", 11.0),
                                OptDouble(paramsEl, "page_height", 8.5));
                            break;

                        case "export_active_map":
                            resultData = await LayoutCommands.ExportActiveMapAsync(
                                ReqStr(paramsEl, "output_path", true),
                                OptStr(paramsEl, "format", "PNG"),
                                OptInt(paramsEl, "width", 1920),
                                OptInt(paramsEl, "height", 1080),
                                OptInt(paramsEl, "resolution", 150));
                            break;

                        case "create_map_series":
                            resultData = await LayoutCommands.CreateMapSeriesAsync(
                                ReqStr(paramsEl, "layout_name", true),
                                ReqStr(paramsEl, "map_frame_name", true),
                                ReqStr(paramsEl, "index_layer_name", true),
                                ReqStr(paramsEl, "name_field", true));
                            break;

                        case "export_map_series":
                            resultData = await LayoutCommands.ExportMapSeriesAsync(
                                ReqStr(paramsEl, "layout_name", true),
                                ReqStr(paramsEl, "output_path", true),
                                OptStr(paramsEl, "format", "PDF"),
                                OptInt(paramsEl, "resolution", 300));
                            break;

                        case "add_dynamic_text":
                            resultData = await LayoutCommands.AddDynamicTextAsync(
                                ReqStr(paramsEl, "layout_name", true),
                                OptStr(paramsEl, "text", ""),
                                OptDouble(paramsEl, "x", 0.5),
                                OptDouble(paramsEl, "y", 0.5),
                                OptDouble(paramsEl, "width", 4.0),
                                OptDouble(paramsEl, "height", 0.5),
                                OptStr(paramsEl, "element_name", "MCP Dynamic Text"));
                            break;

                        case "update_layout_element":
                            {
                                bool uleHasText = HasProp(paramsEl, "text");
                                string uleText = uleHasText ? (OptStr(paramsEl, "text", "")) : "";
                                bool? uleVisible = HasProp(paramsEl, "visible") ? (bool?)OptBool(paramsEl, "visible", false) : (bool?)null;
                                resultData = await LayoutCommands.UpdateLayoutElementAsync(
                                    ReqStr(paramsEl, "layout_name", true),
                                    ReqStr(paramsEl, "element_name", true),
                                    uleText, uleHasText, uleVisible);
                                break;
                            }

                        // Editing Commands
                        case "update_attributes":
                            resultData = await EditingCommands.UpdateAttributesAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqInt64(paramsEl, "object_id"),
                                DeserializeAttributes(paramsEl.GetProperty("attributes")));
                            break;

                        case "create_feature":
                            {
                                var createAttrs = HasProp(paramsEl, "attributes")
                                    ? DeserializeAttributes(paramsEl.GetProperty("attributes"))
                                    : new Dictionary<string, object>();
                                resultData = await EditingCommands.CreateFeatureAsync(
                                    ReqStr(paramsEl, "layer_name", true),
                                    ReqDouble(paramsEl, "x"),
                                    ReqDouble(paramsEl, "y"),
                                    OptInt(paramsEl, "wkid", 4326),
                                    createAttrs);
                                break;
                            }

                        case "delete_selected_features":
                            resultData = await EditingCommands.DeleteSelectedFeaturesAsync(
                                ReqStr(paramsEl, "layer_name", true));
                            break;

                        case "undo_last_edit":
                            resultData = await EditingCommands.UndoLastEditAsync();
                            break;

                        // Bulk Data-Access Commands (arcpy.da-style)
                        case "insert_features":
                            resultData = await DataAccessCommands.InsertFeaturesAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                DeserializeFeatureList(paramsEl.GetProperty("features")));
                            break;

                        case "update_features":
                            resultData = await DataAccessCommands.UpdateFeaturesAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                DeserializeFeatureList(paramsEl.GetProperty("updates")));
                            break;

                        case "delete_features":
                            {
                                var oidProp = paramsEl.GetProperty("object_ids");
                                var oids = new List<long>();
                                foreach (var item in oidProp.EnumerateArray())
                                {
                                    long v;
                                    if (item.TryGetInt64(out v)) oids.Add(v);
                                }
                                resultData = await DataAccessCommands.DeleteFeaturesAsync(
                                    ReqStr(paramsEl, "layer_name", true), oids);
                                break;
                            }

                        // GeometryEngine Commands
                        case "measure_distance":
                            resultData = await GeometryCommands.MeasureDistanceAsync(
                                ReqStr(paramsEl, "layer_a", true),
                                ReqStr(paramsEl, "layer_b", true));
                            break;

                        case "geometry_contains":
                            resultData = await GeometryCommands.ContainsAsync(
                                ReqStr(paramsEl, "layer_a", true),
                                ReqStr(paramsEl, "layer_b", true));
                            break;

                        case "geometry_intersects":
                            resultData = await GeometryCommands.IntersectsAsync(
                                ReqStr(paramsEl, "layer_a", true),
                                ReqStr(paramsEl, "layer_b", true));
                            break;

                        case "geometry_within_distance":
                            resultData = await GeometryCommands.WithinDistanceAsync(
                                ReqStr(paramsEl, "layer_a", true),
                                ReqStr(paramsEl, "layer_b", true),
                                ReqDouble(paramsEl, "distance"));
                            break;

                        case "geometry_area":
                            resultData = await GeometryCommands.AreaAsync(ReqStr(paramsEl, "layer_name", true));
                            break;

                        case "geometry_length":
                            resultData = await GeometryCommands.LengthAsync(ReqStr(paramsEl, "layer_name", true));
                            break;

                        case "set_camera_3d":
                            {
                                double? camRoll = HasProp(paramsEl, "roll") ? (double?)OptDouble(paramsEl, "roll", 0) : (double?)null;
                                double? camScale = HasProp(paramsEl, "scale") ? (double?)OptDouble(paramsEl, "scale", 0) : (double?)null;
                                resultData = await GeometryCommands.SetCamera3DAsync(
                                    ReqDouble(paramsEl, "heading"),
                                    ReqDouble(paramsEl, "pitch"),
                                    camRoll, camScale);
                                break;
                            }

                        // Full Symbology Commands
                        case "apply_graduated_symbology":
                            resultData = await SymbologyCommands.ApplyGraduatedSymbologyAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqStr(paramsEl, "field_name", true),
                                OptInt(paramsEl, "break_count", 5),
                                OptStr(paramsEl, "classification_method", "NaturalBreaks"),
                                OptStr(paramsEl, "color_ramp", "Yellow-Orange-Red"));
                            break;

                        case "apply_unique_value_symbology":
                            resultData = await SymbologyCommands.ApplyUniqueValueSymbologyAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                ReqStr(paramsEl, "field_name", true),
                                OptStr(paramsEl, "color_ramp", "Default"),
                                OptInt(paramsEl, "values_limit", 100));
                            break;

                        case "apply_raster_colorizer":
                            resultData = await SymbologyCommands.ApplyRasterColorizerAsync(
                                ReqStr(paramsEl, "raster_layer", true),
                                OptStr(paramsEl, "symbology_layer", ""),
                                OptStr(paramsEl, "color_ramp", "Default"));
                            break;

                        case "set_layer_symbol":
                            resultData = await SymbologyCommands.SetLayerSymbolAsync(
                                ReqStr(paramsEl, "layer_name", true),
                                OptInt(paramsEl, "r", 0),
                                OptInt(paramsEl, "g", 0),
                                OptInt(paramsEl, "b", 0),
                                OptDouble(paramsEl, "width", 0),
                                OptDouble(paramsEl, "alpha", 100));
                            break;

                        default:
                            return SerializeError("Unsupported command: '" + command + "'");
                    }

                    stopwatch.Stop();
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        data = resultData,
                        error_code = "",
                        message = "",
                        elapsed_ms = stopwatch.ElapsedMilliseconds
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error executing MCP command: " + ex.ToString());
                stopwatch.Stop();
                return SerializeError(ex.Message, ex.GetType().Name, stopwatch.ElapsedMilliseconds);
            }
        }

        private static bool HasProp(JsonElement paramsEl, string name)
        {
            JsonElement discard;
            return paramsEl.ValueKind == JsonValueKind.Object && paramsEl.TryGetProperty(name, out discard);
        }

        private static string OptStr(JsonElement paramsEl, string name, string fallback)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? fallback;
            }
            return fallback;
        }

        private static string ReqStr(JsonElement paramsEl, string name, bool required)
        {
            string value = OptStr(paramsEl, name, null);
            if (required && string.IsNullOrEmpty(value)) throw new ArgumentException("Parameter '" + name + "' is required.");
            return value;
        }

        private static bool OptBool(JsonElement paramsEl, string name, bool fallback)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
            }
            return fallback;
        }

        private static bool ReqBool(JsonElement paramsEl, string name)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;
            }
            throw new ArgumentException("Parameter '" + name + "' is required.");
        }

        private static int OptInt(JsonElement paramsEl, string name, int fallback)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop) && prop.ValueKind == JsonValueKind.Number)
            {
                int value;
                if (prop.TryGetInt32(out value)) return value;
            }
            return fallback;
        }

        private static double OptDouble(JsonElement paramsEl, string name, double fallback)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop) && prop.ValueKind == JsonValueKind.Number)
            {
                double value;
                if (prop.TryGetDouble(out value)) return value;
            }
            return fallback;
        }

        private static double ReqDouble(JsonElement paramsEl, string name)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop) && prop.ValueKind == JsonValueKind.Number)
            {
                double value;
                if (prop.TryGetDouble(out value)) return value;
            }
            throw new ArgumentException("Parameter '" + name + "' is required.");
        }

        private static string[] OptStrArray(JsonElement paramsEl, string name)
        {
            var list = new List<string>();
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    list.Add(item.GetString() ?? "");
                }
            }
            return list.ToArray();
        }

        private static long ReqInt64(JsonElement paramsEl, string name)
        {
            JsonElement prop;
            if (paramsEl.ValueKind != JsonValueKind.Undefined && paramsEl.TryGetProperty(name, out prop) && prop.ValueKind == JsonValueKind.Number)
            {
                long value;
                if (prop.TryGetInt64(out value)) return value;
            }
            throw new ArgumentException("Parameter '" + name + "' is required.");
        }

        private static Dictionary<string, object> DeserializeAttributes(JsonElement attributes)
        {
            var result = new Dictionary<string, object>();
            if (attributes.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in attributes.EnumerateObject())
            {
                result[property.Name] = JsonElementToObject(property.Value);
            }

            return result;
        }

        private static List<Dictionary<string, object>> DeserializeFeatureList(JsonElement array)
        {
            var list = new List<Dictionary<string, object>>();
            if (array.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var item in array.EnumerateArray())
            {
                var dict = new Dictionary<string, object>();
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in item.EnumerateObject())
                    {
                        if (property.Name == "attributes" && property.Value.ValueKind == JsonValueKind.Object)
                        {
                            var nested = new Dictionary<string, object>();
                            foreach (var nestedProp in property.Value.EnumerateObject())
                            {
                                nested[nestedProp.Name] = JsonElementToObject(nestedProp.Value);
                            }
                            dict["attributes"] = nested;
                        }
                        else
                        {
                            dict[property.Name] = JsonElementToObject(property.Value);
                        }
                    }
                }
                list.Add(dict);
            }
            return list;
        }

        private static object JsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.Number:
                    long integer;
                    if (element.TryGetInt64(out integer)) return integer;
                    double number;
                    if (element.TryGetDouble(out number)) return number;
                    return element.ToString();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;
                default: return element.ToString();
            }
        }

        internal static string SerializeError(string message, string errorCode = "INVALID_REQUEST", long elapsedMs = 0)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error_code = errorCode,
                message,
                error = message,
                data = (object)null,
                elapsed_ms = elapsedMs
            });
        }
    }
}
