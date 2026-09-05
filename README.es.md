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
| `LibreMcpAddin.esriAddinX` (`package/`) | Add-In compilado: 68 comandos + ventana de control manual (EN/ES) |
| `py-server/tcp_bridge.py` | Launcher MCP: reutiliza los 167 tools de `arcgis-mcp-server` cambiando solo el transporte a TCP |
| `*.cs`, `Config.daml` | Fuentes del Add-In (C# .NET 8) |
| `tests/` | Suites de verificación del mantenedor |

## Requisitos

- **ArcGIS Pro 3.5 o superior** (probado en 3.5.4; `desktopVersion: 3.5` carga hacia adelante).
- **Python 3.12+** con `pip install -r requirements.txt`.
- Para compilar el Add-In (opcional): [.NET 8 SDK](https://dotnet.microsoft.com/download).

## Instalación (5 minutos)

### 1. Instalar el Add-In

Doble clic en **`package/LibreMcpAddin.esriAddinX`** → se instala solo. O copialo a `Documentos\ArcGIS\AddIns\ArcGISPro\`.

### 2. Instalar el launcher MCP

```powershell
py -3.12 -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt   # arcgis-mcp-server==0.6.0
```

### 3. Configurar el MCP (opencode)

```json
"arcgis_mcp_addin": {
  "type": "local",
  "command": [
    "RUTA\\A\\py-server\\.venv\\Scripts\\python.exe",
    "RUTA\\A\\py-server\\tcp_bridge.py"
  ],
  "environment": {}
}
```

Sin `PORT`, todo usa el **puerto 5876**. Para otro puerto (ej. `8791`): escribilo en la ventana del Add-In **y** agregá `"environment": {"PORT": "8791"}` (recargá opencode para que lo tome).

## Uso

1. Abrí tu proyecto en Pro. Nada escucha solo: pestaña **MCP Free Bridge** → botón **MCP Free Bridge** → **Iniciar** (muestra `ACTIVO 127.0.0.1:5876`). El ícono es rojo detenido, verde activo. Interfaz en inglés, botón **Español** la pasa a español.
2. Usá los tools desde tu asistente: `list_layers`, `query_layer`, `run_gp_tool`, `label_layer`, `apply_graduated_symbology`, `geometry_area`, `create_feature`, `export_layout`, ...
3. Código arcpy arbitrario vía el tool `run_gp_tool` con la toolbox incluida `ArcPyExec.pyt` → `ExecPython`.
4. Al terminar: **Detener** en la ventana.

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
