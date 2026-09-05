# MCP Free Bridge para ArcGIS Pro

*English version: [README.md](README.md)*

<img src="Images/mcp_green.png" alt="MCP Free Bridge" width="120"/>

Puente **libre y local** entre [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) y **ArcGIS Pro 3.5 o superior**: 68 comandos (lectura, geometría, edición, simbología, layouts, geoprocesamiento y ejecución de código arcpy) que cualquier asistente con MCP puede usar directamente sobre tu proyecto abierto.

Sin licencias pagas, sin nube: el Add-In escucha en `127.0.0.1:PORT` (solo tu PC) y el launcher expone los 167 tools MCP.

## Arquitectura

```text
Asistente (opencode, Claude, ...)  <--stdio JSON-RPC-->  tcp_bridge.py
        <--TCP 127.0.0.1:PORT, JSON con length-prefix-->  Add-In (Pro)
```

| Pieza | Qué es |
|---|---|
| `ArcGisProMcpFree.esriAddinX` (`package/`) | Add-In compilado: 68 comandos + ventana de control manual (EN/ES) |
| `py-server/tcp_bridge.py` | Launcher MCP: reutiliza los 167 tools de `arcgis-mcp-server` cambiando solo el transporte a TCP |
| `*.cs`, `Config.daml` | Fuentes del Add-In (C# .NET 8) |
| `tests/` | Suites de verificación del mantenedor |

## Requisitos

- **ArcGIS Pro 3.5 o superior** (probado en 3.5.4; `desktopVersion: 3.5` carga hacia adelante).
- **Python 3.12+** con `pip install -r requirements.txt`.
- Para compilar el Add-In (opcional): [.NET 8 SDK](https://dotnet.microsoft.com/download).

## Instalación (5 minutos)

### 1. Instalar el Add-In

Doble clic en **`package/ArcGisProMcpFree.esriAddinX`** → se instala solo. O copialo a `Documentos\ArcGIS\AddIns\ArcGISPro\`.

### 2. Instalar el launcher MCP — opción A: uvx (sin instalar nada a mano)

```json
"arcgis_mcp_addin": {
  "type": "local",
  "command": [
    "uvx",
    "--from",
    "git+https://github.com/IngKevinDavid/ArcGis-Pro-MCP-Free",
    "arcgis-pro-mcp-free"
  ],
  "environment": {}
}
```

uvx trae el launcher desde este repo la primera vez (el wheel pineado sale de los assets del Release). Nada que descargar a mano salvo el Add-In.

### 2b. Opción B: Python global

```powershell
cd <repo>
py -m pip install -r requirements.txt   # wheel incluido, sin venv
```

```json
"arcgis_mcp_addin": {
  "type": "local",
  "command": [
    "C:\\Users\\TU\\AppData\\Local\\Programs\\Python\\Python313\\python.exe",
    "RUTA\\A\\py-server\\tcp_bridge.py"
  ],
  "environment": {}
}
```

### 3. Elección de puerto

Sin `PORT`, todo usa el **puerto 5876**. Para otro puerto (ej. `8791`): escribilo en la ventana del Add-In **y** agregá `"environment": {"PORT": "8791"}` a la entrada MCP (recargá el cliente para que lo tome).

## Uso

1. Abrí tu proyecto en Pro. Nada escucha solo: pestaña **MCP Free Bridge** → botón **MCP Free Bridge** → **Iniciar** (muestra `ACTIVO 127.0.0.1:5876`). El ícono es rojo detenido, verde activo. Interfaz en inglés, botón **Español** la pasa a español.
2. Usá los tools desde tu asistente: `list_layers`, `query_layer`, `run_gp_tool`, `label_layer`, `apply_graduated_symbology`, `geometry_area`, `create_feature`, `export_layout`, ...
3. Código arcpy arbitrario vía el tool `run_gp_tool` con la toolbox incluida `ArcPyExec.pyt` → `ExecPython`.
4. Al terminar: **Detener** en la ventana.

## Tools MCP (167)

Cobertura: 165 de 167 tools corren contra el Add-in — 68 comandos dedicados más wrappers del lado cliente que delegan al genérico `run_gp_tool` (probado). `publish_web_layer` y `stage_service_definition` necesitan staging contra un servidor y responden error limpio hasta implementarse.

### Núcleo (3)

| Tool | Descripción |
|---|---|
| `check_license` | Nivel de licencia y extensiones. |
| `get_capabilities` | Capacidades disponibles del Add-in. |
| `health_check` | Salud del puente MCP y proyecto activo. |

### Proyecto y mapas (7)

| Tool | Descripción |
|---|---|
| `consolidate_project` | Consolida proyecto y datos en carpeta. |
| `get_active_map` | Mapa activo y su vista. |
| `list_maps` | Mapas del proyecto actual. |
| `list_project_items` | Ítems principales del proyecto. |
| `open_map` | Abre un mapa por nombre. |
| `save_project` | Guarda el proyecto actual. |
| `save_project_as` | Guarda copia del proyecto. |

### Capas (29)

| Tool | Descripción |
|---|---|
| `add_layer_to_group` | Mueve capas a un grupo. |
| `add_layer_to_map` | Agrega un dataset al mapa. |
| `add_subtypes` | Agrega subtipo a tabla. |
| `apply_graduated_symbology` | Simbología graduada. |
| `apply_raster_colorizer` | Coloreado de ráster. |
| `apply_symbology_from_layer` | Aplica simbología de otra capa. |
| `apply_unique_value_symbology` | Simbología por valores únicos. |
| `clear_selection` | Limpia la selección. |
| `count_features` | Cuenta features con filtro SQL. |
| `create_group_layer` | Crea capa de grupo. |
| `export_layer` | Exporta capa a dataset. |
| `get_count` | N° total de filas. |
| `get_layer_fields` | Campos de una capa. |
| `get_layer_symbology` | Render actual de la capa. |
| `get_selected_features` | Atributos de lo seleccionado. |
| `label_layer` | Etiquetas con halo opcional. |
| `list_layers` | Capas del mapa activo. |
| `load_layer_file` | Carga un .lyrx al mapa. |
| `query_layer` | Consulta filas y geometría. |
| `remove_layer` | Quita capa del mapa. |
| `save_layer_file` | Guarda .lyrx de una capa. |
| `select_features` | Selecciona por atributo SQL. |
| `set_definition_query` | Filtro SQL de la capa. |
| `set_layer_symbol` | Símbolo simple RGB. |
| `set_layer_transparency` | Transparencia 0–100. |
| `set_map_extent` | Setea la extensión del mapa. |
| `toggle_layer_visibility` | Muestra/oculta una capa. |
| `update_class_breaks` | Recalcula cortes graduados. |
| `zoom_to_layer` | Zoom a la extensión de una capa. |

### Marcadores (4)

| Tool | Descripción |
|---|---|
| `create_bookmark` | Crea marcador espacial. |
| `delete_bookmark` | Borra un marcador. |
| `list_bookmarks` | Lista marcadores. |
| `zoom_to_bookmark` | Zoom a un marcador. |

### Diseños (9)

| Tool | Descripción |
|---|---|
| `add_dynamic_text` | Texto dinámico en layout. |
| `create_basic_layout` | Crea layout básico. |
| `create_map_series` | Crea serie de mapas. |
| `export_active_map` | Exporta la vista a imagen. |
| `export_all_layouts` | Exporta todos los layouts. |
| `export_layout` | Exporta un layout (PDF/PNG). |
| `export_map_series` | Exporta la serie de mapas. |
| `list_layouts` | Layouts del proyecto. |
| `update_layout_element` | Edita elemento de layout. |

### Edición (7)

| Tool | Descripción |
|---|---|
| `create_feature` | Crea feature puntual. |
| `delete_features` | Borra por ObjectIDs. |
| `delete_selected_features` | Borra lo seleccionado. |
| `insert_features` | Inserta puntos en lote. |
| `undo_last_edit` | Deshace última edición. |
| `update_attributes` | Edita atributos por OID. |
| `update_features` | Actualiza varios por OID. |

### Acceso a datos (12)

| Tool | Descripción |
|---|---|
| `add_join` | Une tabla por campo común. |
| `copy_features` | Copia features. |
| `copy_rows` | Copia filas. |
| `export_features` | Exporta con filtro SQL. |
| `export_table` | Exporta tabla standalone. |
| `frequency` | Valores únicos y conteos. |
| `make_feature_layer` | Crea capa temporal. |
| `make_table_view` | Crea vista de tabla. |
| `remove_join` | Quita un join. |
| `select` | Extrae por expresión SQL. |
| `summary_statistics` | Estadísticas de campos. |
| `table_select` | Filtra filas a tabla. |

### Gestión y análisis de datos (27)

| Tool | Descripción |
|---|---|
| `add_field` | Agrega campo. |
| `append` | Anexa a dataset existente. |
| `buffer_analysis` | Buffer a distancia. |
| `calculate_field` | Calcula un campo. |
| `check_geometry` | Reporte de geometrías. |
| `clip_analysis` | Recorta por otra capa. |
| `create_feature_class` | Crea feature class vacía. |
| `create_file_gdb` | Crea file geodatabase. |
| `create_table` | Crea tabla vacía. |
| `define_projection` | Define proyección. |
| `delete` | Borra un dataset. |
| `delete_field` | Borra campos. |
| `dissolve` | Agrega por atributos. |
| `erase` | Resta features. |
| `find_identical` | Registros idénticos. |
| `generate_near_table` | Tabla de cercanías. |
| `intersect` | Intersección geométrica. |
| `merge` | Combina datasets. |
| `multiple_ring_buffer` | Buffers múltiples. |
| `near` | Distancia al más cercano. |
| `project` | Reproyecta datos. |
| `rename` | Renombra un dataset. |
| `repair_geometry` | Repara geometrías. |
| `select_layer_by_location` | Selecciona por ubicación. |
| `spatial_join` | Join por relación espacial. |
| `split` | Divide por valores únicos. |
| `union` | Unión de polígonos. |

### Conversión (12)

| Tool | Descripción |
|---|---|
| `bim_to_geodatabase` | BIM a geodatabase. |
| `cad_to_geodatabase` | CAD a geodatabase. |
| `excel_to_table` | Excel a tabla. |
| `feature_class_to_shapefile` | A shapefiles. |
| `features_to_json` | Features a JSON/GeoJSON. |
| `json_to_features` | JSON a feature class. |
| `kml_to_layer` | KML/KMZ a capa. |
| `layer_to_kml` | Capa a KMZ. |
| `point_to_raster` | Puntos a ráster. |
| `polygon_to_raster` | Polígonos a ráster. |
| `raster_to_polygon` | Ráster a polígonos. |
| `table_to_excel` | Tabla a Excel. |

### Análisis de redes (4)

| Tool | Descripción |
|---|---|
| `find_closest_facilities` | Facility más cercana. |
| `find_routes` | Mejor ruta (Network Analyst). |
| `generate_od_cost_matrix` | Matriz origen-destino. |
| `generate_service_areas` | Isócronas de viaje. |

### Spatial Analyst (ráster) (8)

| Tool | Descripción |
|---|---|
| `aspect` | Orientación de ladera. |
| `extract_by_mask` | Extrae por máscara. |
| `hillshade` | Sombreado de relieve. |
| `kernel_density` | Densidad kernel. |
| `raster_calculator` | Álgebra de mapas. |
| `reclassify` | Reclasifica ráster. |
| `slope` | Pendiente de superficie. |
| `weighted_overlay` | Superposición ponderada. |

### Estadística espacial (7)

| Tool | Descripción |
|---|---|
| `cluster_and_outlier_analysis` | Clusters (Moran I). |
| `emerging_hot_spot_analysis` | Tendencias espacio-temporales. |
| `generalized_linear_regression` | Regresión GLR. |
| `geographically_weighted_regression` | Regresión GWR. |
| `hot_spot_analysis` | Puntos calientes (Gi*). |
| `optimized_hot_spot_analysis` | Hot spots automático. |
| `spatial_autocorrelation` | Moran I global. |

### Geometría (7)

| Tool | Descripción |
|---|---|
| `geometry_area` | Área del polígono. |
| `geometry_contains` | ¿A contiene a B? |
| `geometry_intersects` | ¿Se intersectan? |
| `geometry_length` | Largo de la línea. |
| `geometry_within_distance` | ¿A distancia dada? |
| `measure_distance` | Distancia geodésica (m). |
| `set_camera_3d` | Cámara 3D (heading/pitch). |

### Geocodificación (4)

| Tool | Descripción |
|---|---|
| `create_locator` | Crea localizador. |
| `geocode_addresses` | Geocodifica direcciones. |
| `rematch_addresses` | Re-matchea direcciones. |
| `reverse_geocode` | Puntos a direcciones. |

### Geodatabase y topología (8)

| Tool | Descripción |
|---|---|
| `add_feature_class_to_topology` | Agrega clase a topología. |
| `add_rule_to_topology` | Agrega regla. |
| `create_domain` | Crea dominio. |
| `create_topology` | Crea topología. |
| `describe_dataset` | Describe un dataset. |
| `list_domains` | Dominios de GDB. |
| `list_feature_classes` | Feature classes de GDB. |
| `validate_topology` | Valida topología. |

### Empaquetado y publicación (9)

| Tool | Descripción |
|---|---|
| `create_mobile_map_package` | Paquete móvil (.mmpk). |
| `create_vector_tile_package` | Teselas vectoriales (.vtpk). |
| `package_layer` | Empaqueta capa (.lpkx). |
| `package_map` | Empaqueta mapa (.mpkx). |
| `package_project` | Empaqueta proyecto (.ppkx). |
| `publish_web_layer` | Publica servicio. |
| `replace_web_layer` | Reemplaza web layer. |
| `share_package` | Comparte paquete en portal. |
| `stage_service_definition` | Prepara .sd. |

### Portal y servicios (8)

| Tool | Descripción |
|---|---|
| `connect_portal` | Setea portal/MCP REST. |
| `describe_portal_item` | Describe ítem. |
| `export_service_geojson` | REST a GeoJSON. |
| `get_active_portal` | Portal activo. |
| `get_layer_schema` | Esquema REST. |
| `query_feature_service` | Consulta capa REST. |
| `search_portal_items` | Busca ítems del portal. |

### Geoprocesamiento y docs (2)

| Tool | Descripción |
|---|---|
| `run_gp_tool` | Cualquier tool de GP. |
| `search_arcgis_docs` | Busca en docs Esri. |

## Verificación (mantenedor)

```powershell
$V = "py-server\.venv\Scripts\python.exe"
& $V tests\contract_check.py   # 67 checks crudos por TCP
& $V tests\mcp_suite.py        # 15 checks por la vía MCP real
```

Los tests usan `ARCGIS_TEST_GDB` (una file GDB con datos de prueba) y `PORT`; con defaults corren en el entorno del autor.

## Solución de problemas

| Síntoma | Causa típica |
|---|---|
| No aparece la pestaña | Política `BlockAddins=1` en `HKCU\SOFTWARE\ESRI\ArcGISPro\Settings` → ponelo en `0` |
| `ConnectionRefused` | El puente no está iniciado (ventana → Iniciar) o el `PORT` no coincide en ambos lados |
| Puerto en uso al Iniciar | Otro proceso lo ocupa; elegí otro puerto en la ventana |
| `allow_delete` | Los tools destructivos exigen `"allow_delete": true` explícito (seguridad) |

## Notas técnicas

- Límites honestos: sin portal en línea (3 comandos responden error limpio).
- `count_features` cuenta el origen e ignora *definition queries* (las queries sí las respetan).
- Borrar una GDB desde el explorador con Pro abierto la deja bloqueada; usá el tool `Delete` con `allow_delete`.

## Licencia

MIT — ver `LICENSE`. Protocolo compatible con el proyecto `arcgis-mcp` (MIT); implementación limpia sin su código ni binarios.

## Créditos

<img src="docs/creator.jpg" alt="Ing. Kevin David Condori Q." width="160"/>

**Ing. Kevin David Condori Q.**
📧 ingkevindavid@gmail.com
💼 [LinkedIn](https://www.linkedin.com/in/kevin-david-condori-quispe/)
