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
| `LibreMcpAddin.esriAddinX` (`package/`) | Compiled Add-In: 68 commands + manual control window (EN/ES) |
| `py-server/tcp_bridge.py` | MCP launcher: reuses the 167 tools from `arcgis-mcp-server`, swapping only the transport to TCP |
| `*.cs`, `Config.daml` | Add-In sources (C# .NET 8) |
| `tests/` | Maintainer verification suites |

## Requirements

- **ArcGIS Pro 3.5 or newer** (tested on 3.5.4; `desktopVersion: 3.5` loads forward).
- **Python 3.12+** with `pip install -r requirements.txt`.
- To compile the Add-In (optional): [.NET 8 SDK](https://dotnet.microsoft.com/download).

## Install (5 minutes)

### 1. Install the Add-In

Double-click **`package/LibreMcpAddin.esriAddinX`** — it installs itself. Or copy it to `Documents\ArcGIS\AddIns\ArcGISPro\`.

### 2. Install the MCP launcher

```powershell
py -3.12 -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt   # arcgis-mcp-server==0.6.0
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
