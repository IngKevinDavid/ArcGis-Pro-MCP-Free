"""Pipe-level contract suite for LibreMcpAddin (Pro 3.5 backport).
Runs against \\\\.\\pipe\\ArcGisMcpBridge with length-prefixed JSON framing.
Usage (venv python with pywin32):
  python contract_check.py
Safe by design: read-only probes first; mutations are self-cleaning scratch
except label_layer on CUENCA_PROYECTO_Buffer (left visible, reported) and one
MCP_TEST layout left behind for manual deletion.
"""
import json
import os
import socket
import struct
import sys
import time

# TCP bridge endpoint: PORT env (default 5876), accepts "5876" or "host:port".
_PORT_ENV = (os.environ.get("PORT", "") or "").strip()
if ":" in _PORT_ENV:
    _HOST, _PORT = _PORT_ENV.rsplit(":", 1)
else:
    _HOST, _PORT = "127.0.0.1", (_PORT_ENV or "5876")
try:
    ENDPOINT = (_HOST or "127.0.0.1", int(_PORT))
except ValueError:
    ENDPOINT = ("127.0.0.1", 5876)
TMP = r"C:\Users\Kevin\AppData\Local\Temp\opencode"
PASS, FAIL = [], []


def _read_sock(s, size):
    chunks, left = [], size
    while left > 0:
        chunk = s.recv(left)
        if not chunk:
            raise OSError("bridge closed mid-response")
        chunks.append(chunk)
        left -= len(chunk)
    return b"".join(chunks)


def call(command, params=None, timeout_ms=15000):
    req = json.dumps({"command": command, "params": params or {}}).encode("utf-8")
    last = None
    for _ in range(5):
        s = None
        try:
            s = socket.create_connection(ENDPOINT, timeout=3)
            s.sendall(struct.pack("<I", len(req)) + req)
            n = struct.unpack("<I", _read_sock(s, 4))[0]
            return json.loads(_read_sock(s, n).decode("utf-8"))
        except Exception as e:  # noqa: BLE001 - brief backoff, bridge may be starting
            last = e
            time.sleep(0.2)
        finally:
            if s is not None:
                try:
                    s.close()
                except OSError:
                    pass
    raise last


def check(name, command, params=None, expect_success=True, key=None):
    try:
        r = call(command, params)
    except Exception as e:  # noqa: BLE001 - report transport faults plainly
        FAIL.append(f"{name}: TRANSPORT {type(e).__name__} {e}")
        print(f"FAIL {name}: transport {e}")
        return None
    ok = r.get("success") is True
    if ok != expect_success:
        FAIL.append(f"{name}: success={ok} r={json.dumps(r)[:200]}")
        print(f"FAIL {name}: {json.dumps(r)[:200]}")
        return None
    if expect_success and key and not r.get("data", {}).get(key):
        FAIL.append(f"{name}: missing data.{key}")
        print(f"FAIL {name}: missing data.{key}")
        return None
    PASS.append(name)
    print(f"PASS {name}")
    return r


def main():
    # --- infra ---
    check("ping", "ping", key=None)
    check("health_check", "health_check")
    check("get_capabilities", "get_capabilities")
    check("check_license", "check_license")
    # --- project/maps ---
    info = check("list_maps", "list_maps")
    check("list_project_items", "list_project_items")
    check("list_bookmarks", "list_bookmarks", {"map_name": ""})
    check("get_active_map", "get_active_map")
    lr = check("list_layers", "list_layers", {"include_hidden": True})
    # --- data reads on the known-good Buffer layer ---
    buf = "CUENCA_PROYECTO_Buffer"
    GDB = os.environ.get("ARCGIS_TEST_GDB") or (
           "C:\\Users\\Kevin\\OneDrive - Universidad Aut\u00f3noma Juan Misael Saracho"
           "\\Documentos\\ArcGIS\\Projects\\MyProject3\\MyProject3.gdb")
    try:
        _names = [(l.get("name") or l.get("title") or l.get("longName") or "")
                  for l in (lr or {}).get("data", {}).get("layers", [])]
    except Exception:
        _names = []
    if buf not in _names:
        check("ensure_buf", "add_layer_to_map",
              {"data_path": GDB + "\\CUENCA_PROYECTO_Buffer", "layer_name": buf})
    check("count_features", "count_features", {"layer_name": buf, "sql_filter": ""})
    check("get_layer_fields", "get_layer_fields", {"layer_name": buf})
    check("query_layer", "query_layer", {"layer_name": buf, "where_clause": "1=1",
                                         "fields": "OBJECTID", "limit": 2})
    check("get_selected_features", "get_selected_features", {"layer_name": buf})
    # --- labels (the point of this backport) ---
    check("label_layer", "label_layer", {"layer_name": buf, "field_name": "LAYER",
                                         "visible": True, "expression_engine": "Arcade"})
    check("get_layer_symbology", "get_layer_symbology", {"layer_name": buf})
    # --- layouts ---
    lo = check("list_layouts", "list_layouts")
    try:
        _lonames = [(l.get("name") or l.get("title") or "") for l in
                    (lo or {}).get("data", {}).get("layouts", [])]
    except Exception:
        _lonames = []
    if _lonames:
        check("export_layout", "export_layout", {"layout_name": _lonames[0],
              "output_path": TMP + r"\contract_layout.pdf", "format": "PDF", "resolution": 150})
    else:
        print("SKIP export_layout: project has no layouts")
    check("export_active_map", "export_active_map", {"output_path": TMP + r"\contract_map.png",
          "format": "PNG", "width": 800, "height": 600, "resolution": 96})
    if "MCP_TEST" not in _lonames:
        check("create_basic_layout", "create_basic_layout", {"layout_name": "MCP_TEST",
              "title": "MCP contract test - safe to delete", "page_width": 11.0, "page_height": 8.5})
    else:
        print("SKIP create_basic_layout: MCP_TEST already exists")
        PASS.append("create_basic_layout (exists)")
    check("add_dynamic_text", "add_dynamic_text", {"layout_name": "MCP_TEST",
          "text": "contract probe", "x": 1.0, "y": 1.0, "width": 4.0,
          "height": 0.5, "element_name": "MCP Probe"})
    check("update_layout_element", "update_layout_element", {"layout_name": "MCP_TEST",
          "element_name": "MCP Probe", "text": "contract probe v2", "visible": True})
    # --- generic GP + gdb ---
    check("run_gp_tool", "run_gp_tool", {"tool_name": "GetCount_management",
          "parameters": ["CUENCA_PROYECTO_Buffer"], "add_outputs_to_map": False})
    check("describe_dataset", "describe_dataset", {"dataset_path": buf})
    # --- self-cleaning bookmark cycle ---
    check("create_bookmark", "create_bookmark", {"name": "MCP_TEST_BM"})
    check("zoom_to_bookmark", "zoom_to_bookmark", {"name": "MCP_TEST_BM"})
    check("delete_bookmark", "delete_bookmark", {"name": "MCP_TEST_BM"})
    # --- generic GP selection + geometry engine (needs one selected row) ---
    check("gp_select", "run_gp_tool", {"tool_name": "SelectLayerByAttribute_management",
          "parameters": [buf, "NEW_SELECTION", "OBJECTID = 1"], "add_outputs_to_map": False})
    check("geometry_area", "geometry_area", {"layer_name": buf})
    check("measure_distance", "measure_distance", {"layer_a": buf, "layer_b": buf})
    check("geometry_intersects", "geometry_intersects", {"layer_a": buf, "layer_b": buf})
    check("geometry_contains", "geometry_contains", {"layer_a": buf, "layer_b": buf})
    check("geometry_within_distance", "geometry_within_distance",
          {"layer_a": buf, "layer_b": buf, "distance": 1000000})
    check("set_camera_3d", "set_camera_3d", {"heading": 0, "pitch": -90})
    check("gp_clear", "run_gp_tool", {"tool_name": "SelectLayerByAttribute_management",
          "parameters": [buf, "CLEAR_SELECTION"], "add_outputs_to_map": False})
    # --- scratch polygon FC for edit + symbology tests (self-cleaning) ---
    gdb = TMP + r"\MCP_EDIT3.gdb"
    try:
        call("run_gp_tool", {"tool_name": "Delete_management",
             "parameters": [gdb], "add_outputs_to_map": False, "allow_delete": True})
    except Exception:
        pass
    print("PASS scratch preclean attempted")
    PASS.append("scratch preclean attempted")
    check("edit_gdb", "run_gp_tool", {"tool_name": "CreateFileGDB_management",
          "parameters": [TMP, "MCP_EDIT3"], "add_outputs_to_map": False})
    check("edit_copy", "run_gp_tool", {"tool_name": "CopyFeatures_management",
          "parameters": [GDB + "\\CUENCA_PROYECTO_Buffer",
                         gdb + r"\MCP_POLY"], "add_outputs_to_map": False})
    check("edit_add", "add_layer_to_map", {"data_path": gdb + r"\MCP_POLY",
          "layer_name": "MCP_POLY"})
    q0 = check("edit_orig", "query_layer", {"layer_name": "MCP_POLY",
               "where_clause": "OBJECTID = 1", "fields": "LAYER", "limit": 2})
    try:
        _orig = q0["data"]["rows"][0]["LAYER"]
    except Exception:
        _orig = None
    check("edit_update", "update_attributes", {"layer_name": "MCP_POLY",
          "object_id": 1, "attributes": {"LAYER": "MCP"}})
    q = check("edit_verify", "query_layer", {"layer_name": "MCP_POLY",
              "where_clause": "OBJECTID = 1", "fields": "LAYER", "limit": 2})
    if q and q["data"].get("rows") and q["data"]["rows"][0]["LAYER"] == "MCP":
        print("PASS edit value applied")
        PASS.append("edit value applied")
    else:
        FAIL.append(f"edit value not applied: {json.dumps(q)[:160]}")
        print("FAIL edit value not applied")
    check("edit_undo", "undo_last_edit")
    q = check("edit_restored", "query_layer", {"layer_name": "MCP_POLY",
              "where_clause": "OBJECTID = 1", "fields": "LAYER", "limit": 2})
    if q and _orig is not None and q["data"].get("rows") and q["data"]["rows"][0]["LAYER"] == _orig:
        print("PASS undo restored value")
        PASS.append("undo restored value")
    else:
        FAIL.append(f"undo did not restore {(_orig)!r}: {json.dumps(q)[:160]}")
        print("FAIL undo did not restore value")
    check("sym_symbol", "set_layer_symbol", {"layer_name": "MCP_POLY",
          "r": 200, "g": 30, "b": 30})
    check("sym_graduated", "apply_graduated_symbology", {"layer_name": "MCP_POLY",
          "field_name": "BUFF_DIST", "break_count": 3})
    check("sym_unique", "apply_unique_value_symbology", {"layer_name": "MCP_POLY",
          "field_name": "LAYER"})
    check("sym_raster_neg", "apply_raster_colorizer", {"raster_layer": "Extract_tif21.tif",
          "symbology_layer": "C:\\Users\\Kevin\\OneDrive - Universidad Aut\u00f3noma Juan Misael Saracho"
                             "\\Documentos\\ArcGIS\\Projects\\MyProject3\\curvas_nivel_100m.lyrx",
          "color_ramp": "Default"}, expect_success=False)
    # --- scratch point FC for create/insert/delete tests ---
    check("pts_fc", "run_gp_tool", {"tool_name": "CreateFeatureclass_management",
          "parameters": [gdb, "MCP_PTS", "POINT", "", "", "", "32719"],
          "add_outputs_to_map": False})
    check("pts_add", "add_layer_to_map", {"data_path": gdb + r"\MCP_PTS",
          "layer_name": "MCP_PTS"})
    check("edit_create", "create_feature", {"layer_name": "MCP_PTS", "x": 603164.0,
          "y": 8172539.0, "wkid": 32719, "attributes": {}})
    check("edit_insert", "insert_features", {"layer_name": "MCP_PTS",
          "features": [{"x": 603200.0, "y": 8172600.0, "wkid": 32719}]})
    q = check("edit_count2", "query_layer", {"layer_name": "MCP_PTS",
              "where_clause": "1=1", "fields": "OBJECTID", "limit": 10})
    try:
        oids = [r["OBJECTID"] for r in q["data"]["rows"]] if q else []
    except Exception:
        oids = []
    if len(oids) == 2:
        print("PASS 2 point rows present")
        PASS.append("2 point rows present")
        check("edit_upd_batch", "update_features", {"layer_name": "MCP_PTS",
              "updates": [{"objectid": o, "attributes": {}} for o in oids]})
        check("edit_del_batch", "delete_features", {"layer_name": "MCP_PTS",
              "object_ids": oids})
        q = check("edit_empty", "query_layer", {"layer_name": "MCP_PTS",
                  "where_clause": "1=1", "fields": "OBJECTID", "limit": 10})
        if q and q["data"].get("returned") == 0:
            print("PASS scratch points removed")
            PASS.append("scratch points removed")
        else:
            FAIL.append(f"scratch points remain: {json.dumps(q)[:160]}")
            print("FAIL scratch points remain")
    else:
        FAIL.append(f"expected 2 point rows, got: {oids}")
        print(f"FAIL expected 2 point rows, got: {oids}")
    check("edit_del_sel", "delete_selected_features", {"layer_name": "MCP_POLY"},
          expect_success=False)  # nothing selected -> clean error
    check("edit_rm_poly", "remove_layer", {"layer_name": "MCP_POLY"})
    check("edit_rm_pts", "remove_layer", {"layer_name": "MCP_PTS"})
    check("edit_del_gdb", "run_gp_tool", {"tool_name": "Delete_management",
          "parameters": [gdb], "add_outputs_to_map": False, "allow_delete": True})
    # --- exec via stock run_gp_tool + our ArcPyExec.pyt (no server fork) ---
    r = check("exec_py", "run_gp_tool", {"tool_name":
          r"D:\Rstudio\05_herramientas\ArcGeekLibre.Addin\ArcPyExec.pyt\ExecPython",
          "parameters": ['print("exec-ok")'], "add_outputs_to_map": False})
    if r and "exec-ok" in json.dumps(r):
        print("PASS exec output round-trips")
        PASS.append("exec output round-trips")
    else:
        FAIL.append(f"exec output missing: {json.dumps(r)[:160]}")
        print("FAIL exec output missing")
    # --- documented boundaries (must fail CLEANLY, never crash the pipe) ---
    check("open_map_35_gap", "open_map", {"map_name": "Layers"}, expect_success=False)
    check("portal_gap", "get_active_portal", expect_success=False)
    check("geometry_gap", "geometry_area", expect_success=False)
    print(f"\n== {len(PASS)} PASS, {len(FAIL)} FAIL ==")
    if FAIL:
        print("Failures:")
        for f in FAIL:
            print(" -", f)
        sys.exit(1)
    print("NOTE: MCP_TEST layout + labels-ON left behind on purpose; delete/toggle in Pro.")


if __name__ == "__main__":
    main()
