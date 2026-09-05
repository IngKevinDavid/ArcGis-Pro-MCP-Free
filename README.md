# MCP Free Bridge for ArcGIS Pro

*Versión en español: [README.es.md](README.es.md)*

<img src="Images/mcp_green.png" alt="MCP Free Bridge" width="120"/>

A **free, local** bridge between [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) and **ArcGIS Pro 3.5 or newer**: 68 commands (reading, geometry, editing, symbology, layouts, geoprocessing and arcpy code execution) that any MCP-capable assistant can use directly on your open project.

No paid licenses, no cloud: the Add-In listens on `127.0.0.1:PORT` (your PC only) and the launcher exposes the 167 MCP tools.

## Architecture

```text
Assistant (opencode, Claude, ...)  <--stdio JSON-RPC-->  tcp_bridge.py
        <--TCP 127.0.0.1:PORT, length-prefixed JSON-->  Add-In (Pro)
```

| Piece | What it is |
|---|---|
| `ArcGisProMcpFree.esriAddinX` (`package/`) | Compiled Add-In: 68 commands + manual control window (EN/ES) |
| `py-server/tcp_bridge.py` | MCP launcher: reuses the 167 tools from `arcgis-mcp-server`, swapping only the transport to TCP |
| `*.cs`, `Config.daml` | Add-In sources (C# .NET 8) |
| `tests/` | Maintainer verification suites |

## Requirements

- **ArcGIS Pro 3.5 or newer** (tested on 3.5.4; `desktopVersion: 3.5` loads forward).
- **Python 3.12+** with `pip install -r requirements.txt`.
- To compile the Add-In (optional): [.NET 8 SDK](https://dotnet.microsoft.com/download).

## Install (5 minutes)

### 1. Install the Add-In

Double-click **`package/ArcGisProMcpFree.esriAddinX`** — it installs itself. Or copy it to `Documents\ArcGIS\AddIns\ArcGISPro\`.

### 2. Install the MCP launcher (global Python 3.12+)

```powershell
cd <repo>
py -m pip install -r requirements.txt   # vendored wheel, no venv needed
```

### 3. Configure the MCP (opencode)

```json
"arcgis_mcp_addin": {
  "type": "local",
  "command": [
    "PATH\\TO\\py-server\\.venv\\Scripts\\python.exe",
    "PATH\\TO\\py-server\\tcp_bridge.py"
  ],
  "environment": {}
}
```

Without `PORT`, everything uses **port 5876**. For another port (e.g. `8791`): type it in the Add-In window **and** add `"environment": {"PORT": "8791"}` (reload opencode so it picks it up).

## Use

1. Open your project in Pro. Nothing listens on its own: tab **MCP Free Bridge** → button **MCP Free Bridge** → **Start** (shows `RUNNING 127.0.0.1:5876`). The ribbon icon is red while stopped, green while running. UI in English, **Español** button switches to Spanish.
2. Use the tools from your assistant: `list_layers`, `query_layer`, `run_gp_tool`, `label_layer`, `apply_graduated_symbology`, `geometry_area`, `create_feature`, `export_layout`, ...
3. Arbitrary arcpy code via the `run_gp_tool` tool with the bundled `ArcPyExec.pyt` → `ExecPython`.
4. When done: **Stop** in the window.

## MCP tools (167)

Coverage: 165 of 167 tools run against the Add-in — 68 dedicated commands plus client-side wrappers that delegate to the tested generic `run_gp_tool`. `publish_web_layer` and `stage_service_definition` need a staged server connection and answer a clean error until implemented.

### Core (3)

| Tool | Description |
|---|---|
| `check_license` | Returns the current ArcGIS Pro license level and the list of licensed extensions. |
| `get_capabilities` | Lists available Add-in command capabilities. |
| `health_check` | Checks MCP/Add-in/ArcGIS Pro pipe health and active project state. |

### Project & maps (7)

| Tool | Description |
|---|---|
| `consolidate_project` | Consolidates a project and its data into a folder (no compression). |
| `get_active_map` | Returns the name of the active map and scene view in ArcGIS Pro. |
| `list_maps` | Lists maps in the current ArcGIS Pro project. |
| `list_project_items` | Lists main project items in the current ArcGIS Pro project. |
| `open_map` | Opens a map in ArcGIS Pro by name. |
| `save_project` | Saves the current ArcGIS Pro project. |
| `save_project_as` | Saves the current ArcGIS Pro project to a new APRX path. |

### Layers (29)

| Tool | Description |
|---|---|
| `add_layer_to_group` | Moves existing layers into an existing group layer in the active map. |
| `add_layer_to_map` | Adds a dataset, layer file, or service URL to the active map. |
| `add_subtypes` | Adds a subtype to a subtype definition. |
| `apply_graduated_symbology` | Applies graduated color symbology to a feature layer. |
| `apply_raster_colorizer` | Applies raster symbology from a layer file or existing layer. |
| `apply_symbology_from_layer` | Applies symbology from an existing layer or .lyrx file. |
| `apply_unique_value_symbology` | Applies unique value symbology to a feature layer. |
| `clear_selection` | Clears selection in one feature layer or in the whole active map. |
| `count_features` | Counts the number of features in a specified layer. Optional SQL filter (e.g. "POPULATION > 100000"). |
| `create_group_layer` | Creates a group layer in the active map, optionally moving existing layers into it. |
| `export_layer` | Exports a feature layer to a dataset path. |
| `get_count` | Returns the total number of rows for a feature class, table or layer. |
| `get_layer_fields` | Gets the schema/fields of a layer, listing names, aliases, and data types of all attributes. |
| `get_layer_symbology` | Returns renderer metadata for a layer. |
| `get_selected_features` | Retrieves the attribute records for the currently selected features in a layer. |
| `label_layer` | Enables labels on a feature layer using a field-based expression. Optionally applies a text halo. |
| `list_layers` | Lists all layers in the active map, showing names, types, visibility, and total features. |
| `load_layer_file` | Loads a .lyrx file into the active map. |
| `query_layer` | Queries attribute rows from an active-map feature layer. |
| `remove_layer` | Removes a layer from the active map. |
| `save_layer_file` | Saves an active-map layer to a .lyrx file. |
| `select_features` | Selects features in a layer using a SQL attribute query. combination: NEW, ADD, REMOVE, SUBTRACT, XOR. |
| `set_definition_query` | Sets or clears a definition query on a feature layer. |
| `set_layer_symbol` | Sets the color (RGB 0-255) and optionally the width of a feature layer's simple renderer via direct CIM manipulation. |
| `set_layer_transparency` | Sets layer transparency from 0 to 100. |
| `set_map_extent` | Sets the active map view extent to the specified bounding box coordinates (default WKID 4326). |
| `toggle_layer_visibility` | Toggles the visibility of a layer in the active map by name. |
| `update_class_breaks` | Rebuilds graduated class breaks for a feature layer. |
| `zoom_to_layer` | Zooms the active map view to the spatial extent of a specific layer by name. |

### Bookmarks (4)

| Tool | Description |
|---|---|
| `create_bookmark` | Creates a spatial bookmark from the current active map view extent. |
| `delete_bookmark` | Deletes a bookmark by name from the active map. |
| `list_bookmarks` | Lists bookmarks for the active map or a named map. |
| `zoom_to_bookmark` | Zooms the active map to a named bookmark. |

### Layouts (9)

| Tool | Description |
|---|---|
| `add_dynamic_text` | Adds a text element to a layout. ArcGIS dynamic text tags are accepted. |
| `create_basic_layout` | Creates a basic layout from the active map with map frame and cartographic surrounds. |
| `create_map_series` | Creates a spatial map series for a layout. |
| `export_active_map` | Exports the active map view to an image file. |
| `export_all_layouts` | Exports every layout in the active ArcGIS Pro project to one output directory. |
| `export_layout` | Exports a print layout to the specified output file path. format_type: PDF, PNG, JPEG. Resolution in DPI. |
| `export_map_series` | Exports a configured map series. |
| `list_layouts` | Lists all the layouts (print layouts/map layouts) defined in the current ArcGIS Pro project. |
| `update_layout_element` | Updates text or visibility of a layout element. |

### Editing (7)

| Tool | Description |
|---|---|
| `create_feature` | Creates a point feature in a feature layer. |
| `delete_features` | Deletes features identified by the given ObjectIDs. |
| `delete_selected_features` | Deletes selected features from a feature layer. |
| `insert_features` | Inserts multiple point features in a single edit operation (arcpy.da-style batch). |
| `undo_last_edit` | Undoes the last MCP edit operation. |
| `update_attributes` | Updates attributes for a feature by ObjectID. |
| `update_features` | Updates multiple features by ObjectID in a single edit operation. |

### Data access (12)

| Tool | Description |
|---|---|
| `add_join` | Joins a table to a layer or table view based on a common field. |
| `copy_features` | Copies features to a new feature class. |
| `copy_rows` | Copies the rows of a table, table view or feature class to a new table. |
| `export_features` | Exports a feature class or layer to a new feature class (shapefile, file geodatabase, etc.). |
| `export_table` | Exports a table or attribute table to a new standalone table. |
| `frequency` | Reads a table and produces a new table containing unique field values and their counts. |
| `make_feature_layer` | Creates a feature layer from an input feature class or layer file. |
| `make_table_view` | Creates a table view from an input table or feature class. |
| `remove_join` | Removes a join from a feature layer or table view. |
| `select` | Extracts features from input based on a SQL expression. |
| `summary_statistics` | Computes summary statistics for fields in a table. |
| `table_select` | Extracts rows from a table based on an expression. |

### Data management & analysis (27)

| Tool | Description |
|---|---|
| `add_field` | Adds a new field to a table or feature class. field_type: TEXT, LONG, SHORT, DOUBLE, FLOAT, DATE, BLOB. |
| `append` | Appends multiple input datasets into an existing target dataset. schema_type: TEST or NO_TEST. |
| `buffer_analysis` | Creates buffer polygons around input features to a specified distance. Example: "100 Meters". |
| `calculate_field` | Performs field calculations on feature classes or tables. expression_type: PYTHON3, ARCADE, SQL, VB. |
| `check_geometry` | Produces a report of geometry problems in a feature class. |
| `clip_analysis` | Clips/extracts input features that overlay clip features. |
| `create_feature_class` | Creates an empty feature class in a geodatabase or folder. geometry_type: POINT, MULTIPOINT, POLYLINE, POLYGON. |
| `create_file_gdb` | Creates a file geodatabase in the specified folder. |
| `create_table` | Creates an empty table in a geodatabase or dBASE workspace. |
| `define_projection` | Defines the projection of a dataset without transforming its coordinates. |
| `delete` | Permanently deletes a dataset. |
| `delete_field` | Deletes one or more fields from a table or feature class. |
| `dissolve` | Aggregates features based on specified attributes. |
| `erase` | Removes features (and portions of features) that overlap the erase features. |
| `find_identical` | Reports records with identical values in a list of fields. |
| `generate_near_table` | Produces a table of distances between input and near features. |
| `intersect` | Computes the geometric intersection of feature classes. output_type: INPUT, LINE, POINT. |
| `merge` | Combines multiple input datasets into a new output dataset. |
| `multiple_ring_buffer` | Creates multiple buffers at specified distances around inputs. |
| `near` | Adds distance, location and angle from in_features to the nearest near_features. |
| `project` | Projects spatial data from one coordinate system to another. |
| `rename` | Renames a dataset. |
| `repair_geometry` | Repairs problematic geometry errors in a feature class. |
| `select_layer_by_location` | Selects features in a layer based on their spatial relationship to features in another layer. |
| `spatial_join` | Joins attributes from one feature class to another based on spatial relationship. |
| `split` | Splits input features into many feature classes by the unique values of a split field. |
| `union` | Computes the geometric union of polygon feature classes. |

### Conversion (12)

| Tool | Description |
|---|---|
| `bim_to_geodatabase` | Converts Revit/BIM data (.rvt/.ifc/.dgn) into feature classes. |
| `cad_to_geodatabase` | Converts CAD data (.dwg/.dgn/.dxf) into feature classes in a geodatabase. |
| `excel_to_table` | Imports an Excel workbook (.xlsx/.xls) into a geodatabase table or dBASE. |
| `feature_class_to_shapefile` | Converts one or more feature classes to shapefiles in a folder. |
| `features_to_json` | Converts features to ArcGIS JSON or GeoJSON. |
| `json_to_features` | Creates a feature class from Esri JSON or GeoJSON. |
| `kml_to_layer` | Converts a KML/KMZ file into a feature class and layer file in a file geodatabase. |
| `layer_to_kml` | Converts a map layer, layer file or feature class to a KMZ file. |
| `point_to_raster` | Converts point features to a raster dataset. |
| `polygon_to_raster` | Converts polygon features to a raster dataset. |
| `raster_to_polygon` | Converts a raster dataset to polygon features. |
| `table_to_excel` | Exports a table or feature class attribute table to Excel (.xlsx). |

### Network analysis (4)

| Tool | Description |
|---|---|
| `find_closest_facilities` | Finds the closest facility(ies) for each incident. Requires Network Analyst extension. |
| `find_routes` | Finds the best route through ordered stops (Network Analyst). Requires Network Analyst extension. |
| `generate_od_cost_matrix` | Generates an Origin-Destination cost matrix. Requires Network Analyst extension. |
| `generate_service_areas` | Generates travel-time/distance service areas (isochrones). Requires Network Analyst extension. |

### Spatial Analyst (raster) (8)

| Tool | Description |
|---|---|
| `aspect` | Derives aspect (compass direction of slope) from a surface raster. Requires Spatial Analyst extension. |
| `extract_by_mask` | Extracts the cells of a raster corresponding to mask features. Requires Spatial Analyst extension. |
| `hillshade` | Generates a hillshade from a surface raster. Requires Spatial Analyst extension. |
| `kernel_density` | Calculates kernel density from point/polyline features. Requires Spatial Analyst extension. |
| `raster_calculator` | Builds and executes a Map Algebra expression (Raster Calculator). Requires Spatial Analyst extension. |
| `reclassify` | Reclassifies values in a raster. Requires Spatial Analyst extension. |
| `slope` | Derives slope from a surface raster. Requires Spatial Analyst extension. |
| `weighted_overlay` | Overlays several rasters using a common scale and weights. Requires Spatial Analyst extension. |

### Spatial statistics (7)

| Tool | Description |
|---|---|
| `cluster_and_outlier_analysis` | Identifies clusters and outliers using Anselin Local Moran's I. |
| `emerging_hot_spot_analysis` | Identifies trends in spatial clustering from a space-time cube. |
| `generalized_linear_regression` | Performs Generalized Linear Regression (GLR). model_type: CONTINUOUS, BINARY, COUNT. |
| `geographically_weighted_regression` | Performs Geographically Weighted Regression (GWR) to model spatially varying relationships. |
| `hot_spot_analysis` | Identifies statistically significant hot/cold spots using Getis-Ord Gi*. |
| `optimized_hot_spot_analysis` | Creates a map of statistically significant hot/cold trends, choosing parameters automatically. |
| `spatial_autocorrelation` | Measures spatial autocorrelation (Global Moran's I). |

### Geometry (7)

| Tool | Description |
|---|---|
| `geometry_area` | Returns the area of the first selected polygon feature in the layer. |
| `geometry_contains` | Returns True if the selected feature of layer_a contains that of layer_b. |
| `geometry_intersects` | Returns True if the two selected geometries intersect. |
| `geometry_length` | Returns the length of the first selected polyline feature in the layer. |
| `geometry_within_distance` | Returns True if the two selected geometries are within a distance of each other. |
| `measure_distance` | Geodesic distance (meters) between the first selected feature of each layer. |
| `set_camera_3d` | Sets the camera orientation of the active view (3D scenes). heading 0-360, pitch -90 to 90. |

### Geocoding (4)

| Tool | Description |
|---|---|
| `create_locator` | Creates a geocoding locator from reference data. |
| `geocode_addresses` | Geocodes a table of addresses using a locator. |
| `rematch_addresses` | Re-matches addresses in a geocoded feature class. |
| `reverse_geocode` | Creates addresses from point locations (reverse geocoding). |

### Geodatabase & topology (8)

| Tool | Description |
|---|---|
| `add_feature_class_to_topology` | Adds a feature class to a topology. |
| `add_rule_to_topology` | Adds a rule to a topology. rule_type: e.g. "Must Not Overlap", "Must Be Inside". |
| `create_domain` | Creates a geodatabase domain. |
| `create_topology` | Creates a new topology in a feature dataset. |
| `describe_dataset` | Describes an active-map layer or geodatabase dataset. |
| `list_domains` | Lists geodatabase domains. |
| `list_feature_classes` | Lists feature classes in a file geodatabase. |
| `validate_topology` | Validates the specified topology. |

### Packaging & sharing (9)

| Tool | Description |
|---|---|
| `create_mobile_map_package` | Packages maps for offline use in Field Maps / Navigator apps into a .mmpk file. |
| `create_vector_tile_package` | Creates a vector tile package (.vtpk) from a map. |
| `package_layer` | Packages a layer and its data into a single compressed .lpkx file. |
| `package_map` | Consolidates a map and all referenced data sources into a .mpkx package. |
| `package_project` | Consolidates a project (.aprx) and its data into a portable .ppkx package. |
| `publish_web_layer` | Publishes an ArcGIS .sd file or stages and publishes an .sddraft file. |
| `replace_web_layer` | Replaces the layers and data of an existing web layer with an updated .sd file. |
| `share_package` | Shares a package (.mpkx, .lpkx, .ppkx, .mmpk, .vtpk) to ArcGIS Online or Enterprise. |
| `stage_service_definition` | Stages an ArcGIS service definition draft (.sddraft) into a service definition (.sd). |

### Portal & services (8)

| Tool | Description |
|---|---|
| `connect_portal` | Sets the MCP REST portal URL and optional token. |
| `describe_portal_item` | Describes an ArcGIS portal item. |
| `export_service_geojson` | Exports a REST feature layer query result to GeoJSON. |
| `get_active_portal` | Returns the active ArcGIS Pro portal and MCP REST portal. |
| `get_layer_schema` | Returns schema metadata for a REST feature layer. |
| `get_service_layers` | Lists layers and tables exposed by a REST service. |
| `query_feature_service` | Queries a REST feature layer. |
| `search_portal_items` | Searches ArcGIS Online or ArcGIS Enterprise portal items. |

### Geoprocessing & docs (2)

| Tool | Description |
|---|---|
| `run_gp_tool` | Executes any ArcGIS Pro geoprocessing tool by name with list of parameters. |
| `search_arcgis_docs` | Searches local SDK docs and returns official online documentation links. |

## Verify (maintainer)

```powershell
$V = "py-server\.venv\Scripts\python.exe"
& $V tests\contract_check.py   # 67 raw checks over TCP
& $V tests\mcp_suite.py        # 15 checks through the real MCP path
```

Tests use `ARCGIS_TEST_GDB` (a file GDB with fixture data) and `PORT`; defaults run in the author's environment.

## Troubleshooting

| Symptom | Typical cause |
|---|---|
| No ribbon tab | `BlockAddins=1` policy at `HKCU\SOFTWARE\ESRI\ArcGISPro\Settings` → set it to `0` |
| `ConnectionRefused` | Bridge not started (window → Start) or `PORT` mismatch on both sides |
| Port busy on Start | Another process owns it; pick another port in the window |
| `allow_delete` | Destructive tools require explicit `"allow_delete": true` (safety) |

## Technical notes

- Honest limits: no online portal (3 commands fail cleanly).
- `count_features` counts the source and ignores *definition queries* (queries honor them).
- Deleting a GDB in Explorer while Pro holds it leaves it locked; use the `Delete` tool with `allow_delete`.

## License

MIT — see `LICENSE`. Protocol compatible with the `arcgis-mcp` project (MIT); clean implementation without its code or binaries.

## Credits

<img src="docs/creator.jpg" alt="Ing. Kevin David Condori Q." width="160"/>

**Ing. Kevin David Condori Q.**
📧 ingkevindavid@gmail.com
💼 [LinkedIn](https://www.linkedin.com/in/kevin-david-condori-quispe/)
