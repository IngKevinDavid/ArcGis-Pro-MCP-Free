"""Wave 2: remaining add-in commands not covered by contract_check.py.
Self-cleaning: every mutation is restored/removed; leftovers are reported.
Usage: python wave2_check.py (same venv as contract_check).
"""
import json
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from contract_check import call, check, PASS, FAIL  # noqa: E402

TMP = r"C:\Users\Kevin\AppData\Local\Temp\opencode"
BUF = "CUENCA_PROYECTO_Buffer"
GDB = os.environ.get("ARCGIS_TEST_GDB") or (
    "C:\\Users\\Kevin\\OneDrive - Universidad Aut\u00f3noma Juan Misael Saracho"
    "\\Documentos\\ArcGIS\\Projects\\MyProject3\\MyProject3.gdb")


def layer_names():
    lr = call("list_layers", {"include_hidden": True})
    try:
        return [(l.get("name") or "") for l in lr["data"].get("layers", [])]
    except Exception:
        return []


def safe_remove(path):
    """Best-effort delete: Pro locks files it just wrote (.aprx)."""
    try:
        if os.path.exists(path):
            os.remove(path)
            return True
    except OSError as e:
        print(f"SKIP cleanup locked, delete by hand later: {path} ({e})")
    return False


def main():
    # --- visibility / transparency / zoom / extent (all restored) ---
    check("w2_hide", "toggle_layer_visibility", {"layer_name": BUF, "visible": False})
    check("w2_show", "toggle_layer_visibility", {"layer_name": BUF, "visible": True})
    check("w2_transp", "set_layer_transparency", {"layer_name": BUF, "transparency": 15})
    check("w2_transp0", "set_layer_transparency", {"layer_name": BUF, "transparency": 0})
    check("w2_zoom", "zoom_to_layer", {"layer_name": BUF})
    check("w2_extent", "set_map_extent", {"xmin": 600000, "ymin": 8170000,
          "xmax": 606000, "ymax": 8175000, "wkid": 32719})
    check("w2_zoom_back", "zoom_to_layer", {"layer_name": BUF})
    # --- definition query + selection cycle (cleared) ---
    check("w2_defq", "set_definition_query", {"layer_name": BUF,
          "sql_filter": "OBJECTID > 0"})
    check("w2_defq_clear", "set_definition_query", {"layer_name": BUF, "sql_filter": ""})
    check("w2_select", "select_features", {"layer_name": BUF,
          "sql_filter": "OBJECTID = 1", "selection_combination": "NEW"})
    check("w2_clear", "clear_selection", {"layer_name": BUF})
    # --- group cycle with self-heal (Buffer always restored to root) ---
    check("w2_grp", "create_group_layer", {"group_name": "MCP_GRP"})
    check("w2_grp_add", "add_layer_to_group", {"group_name": "MCP_GRP",
          "layer_names": [BUF]})
    check("w2_grp_rm", "remove_layer", {"layer_name": "MCP_GRP"})
    if BUF not in layer_names():
        check("w2_buf_restore", "add_layer_to_map",
              {"data_path": GDB + "\\" + BUF, "layer_name": BUF})
    else:
        print("PASS w2_buf_still_present")
        PASS.append("w2_buf_still_present")
    # --- layout export on the MCP_TEST layout wave 1 left behind ---
    pdf = os.path.join(TMP, "w2_layout.pdf")
    if os.path.exists(pdf):
        os.remove(pdf)
    check("w2_export_layout", "export_layout", {"layout_name": "MCP_TEST",
          "output_path": pdf, "format": "PDF", "resolution": 150})
    if os.path.exists(pdf):
        print("PASS w2_layout_file_on_disk")
        PASS.append("w2_layout_file_on_disk")
        os.remove(pdf)
    else:
        FAIL.append("w2_layout_file_on_disk: pdf missing")
        print("FAIL w2_layout_file_on_disk: pdf missing")
    # --- map frame name via stock exec, then map series (cleaned files) ---
    r = call("run_gp_tool", {"tool_name":
             r"D:\Rstudio\05_herramientas\ArcGeekLibre.Addin\ArcPyExec.pyt\ExecPython",
             "parameters": ["import arcpy; p=arcpy.mp.ArcGISProject('CURRENT'); "
                            "lo=[l for l in p.listLayouts('MCP_TEST')][0]; "
                            "print('FRAMES='+'|'.join(e.name for e in lo.listElements('MAPFRAME_ELEMENT')))"],
             "add_outputs_to_map": False})
    print("w2 frames probe:", json.dumps(r)[:200])
    # --- export all layouts, then clean the outputs ---
    alldir = os.path.join(TMP, "w2_all")
    if os.path.isdir(alldir):
        shutil.rmtree(alldir, ignore_errors=True)
    check("w2_export_all", "export_all_layouts", {"output_directory": alldir,
          "format": "PDF", "resolution": 150}, timeout_ms=180000, attempts=1)
    got = []
    for root, _, files in os.walk(alldir):
        got += [f for f in files if f.lower().endswith(".pdf")]
    if got:
        print(f"PASS w2_all_files ({len(got)} pdf)")
        PASS.append("w2_all_files")
        shutil.rmtree(alldir, ignore_errors=True)
    else:
        FAIL.append("w2_all_files: no pdf exported")
        print("FAIL w2_all_files: no pdf exported")
    # --- scratch GDB: domains, export, layer files, apply back ---
    gdb5 = os.path.join(TMP, "MCP_EDIT5.gdb")
    try:
        call("run_gp_tool", {"tool_name": "Delete_management",
             "parameters": [gdb5], "add_outputs_to_map": False, "allow_delete": True})
    except Exception:
        pass
    check("w2_gdb", "run_gp_tool", {"tool_name": "CreateFileGDB_management",
          "parameters": [TMP, "MCP_EDIT5"], "add_outputs_to_map": False})
    check("w2_domain", "create_domain", {"workspace_path": gdb5,
          "domain_name": "W2_DOM", "description": "wave2 probe",
          "field_type": "TEXT", "domain_type": "CODED"})
    d = check("w2_domains", "list_domains", {"workspace_path": gdb5})
    try:
        names = json.dumps(d)
        assert "W2_DOM" in names
        print("PASS w2_domain_listed")
        PASS.append("w2_domain_listed")
    except Exception:
        FAIL.append("w2_domain_listed: W2_DOM absent")
        print("FAIL w2_domain_listed: W2_DOM absent")
    check("w2_export_layer", "export_layer", {"layer_name": BUF,
          "output_path": gdb5 + "\\W2_EXP"})
    f = check("w2_fcs", "list_feature_classes", {"workspace_path": gdb5})
    try:
        assert "W2_EXP" in json.dumps(f)
        print("PASS w2_fc_listed")
        PASS.append("w2_fc_listed")
    except Exception:
        FAIL.append("w2_fc_listed: W2_EXP absent")
        print("FAIL w2_fc_listed: W2_EXP absent")
    lyrx = os.path.join(TMP, "w2_buffer.lyrx")
    safe_remove(lyrx)
    check("w2_save_lyrx", "save_layer_file", {"layer_name": BUF, "output_path": lyrx})
    check("w2_apply_lyrx", "apply_symbology_from_layer",
          {"target_layer": BUF, "symbology_layer": lyrx})
    check("w2_load_lyrx", "load_layer_file", {"layer_file_path": lyrx})
    _names2 = [n for n in layer_names() if BUF in (n or "")]
    if len(_names2) >= 2:
        # loaded copy duplicates the name; removing one leaves a single Buffer
        check("w2_rm_loaded", "remove_layer", {"layer_name": _names2[0]})
        _names3 = [n for n in layer_names() if BUF in (n or "")]
        if len(_names3) == 1:
            print("PASS w2_single_buffer_left")
            PASS.append("w2_single_buffer_left")
        else:
            FAIL.append(f"w2_single_buffer_left: {_names3}")
            print(f"FAIL w2_single_buffer_left: {_names3}")
    else:
        print(f"SKIP w2_rm_loaded: no duplicate ({_names2})")
        PASS.append("w2_rm_loaded_skipped")
    if os.path.exists(lyrx):
        safe_remove(lyrx)
    check("w2_del_gdb5", "run_gp_tool", {"tool_name": "Delete_management",
          "parameters": [gdb5], "add_outputs_to_map": False, "allow_delete": True})
    # --- project copy (never touches the live .aprx) ---
    aprx = os.path.join(TMP, "w2_copy.aprx")
    if not safe_remove(aprx) and os.path.exists(aprx):
        aprx = os.path.join(TMP, "w2_copy_b.aprx")
        safe_remove(aprx)
    check("w2_save_as", "save_project_as", {"output_path": aprx, "overwrite": True})
    if os.path.exists(aprx):
        print("PASS w2_aprx_on_disk")
        PASS.append("w2_aprx_on_disk")
        try:
            call("run_gp_tool", {"tool_name": "Delete_management",
                 "parameters": [aprx], "add_outputs_to_map": False, "allow_delete": True})
        except Exception:
            pass
        if not os.path.exists(aprx):
            print("PASS w2_aprx_cleaned")
            PASS.append("w2_aprx_cleaned")
    else:
        FAIL.append("w2_aprx_on_disk: copy missing")
        print("FAIL w2_aprx_on_disk: copy missing")
    # --- documented negatives: clean failures, never a pipe crash ---
    check("w2_length_neg", "geometry_length", {"layer_name": BUF},
          expect_success=False)  # polygon layer, needs selected polyline
    print(f"\n== WAVE2 {len(PASS)} PASS, {len(FAIL)} FAIL ==")
    if FAIL:
        print("Failures:")
        for x in FAIL:
            print(" -", x)
        sys.exit(1)
    print("NOTE: save_project skipped on purpose (would persist scratch state to your live .aprx).")


if __name__ == "__main__":
    main()
