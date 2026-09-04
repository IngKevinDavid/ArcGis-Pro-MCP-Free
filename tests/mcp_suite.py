"""Representative sweep through the REAL MCP layer (stdio JSON-RPC ->
tcp_bridge.py -> TCP 127.0.0.1:PORT -> Add-In). Not exhaustive; the raw
contract suites cover breadth, this proves the MCP path end to end.
Usage: python mcp_suite.py
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mcp_driver import Mcp  # noqa: E402

PASS, FAIL = [], []


def tcall(m, name, tool, args):
    try:
        res = m.req("tools/call", {"name": tool, "arguments": args})
    except Exception as e:  # noqa: BLE001
        FAIL.append(f"{name}: TRANSPORT {e}")
        print(f"FAIL {name}: TRANSPORT {e}")
        return None
    if res.get("isError"):
        FAIL.append(f"{name}: isError {json.dumps(res)[:200]}")
        print(f"FAIL {name}: isError {json.dumps(res)[:200]}")
        return None
    PASS.append(name)
    print(f"PASS {name}")
    return res


def main():
    m = Mcp()
    try:
        r = tcall(m, "health", "health_check", {})
        tcall(m, "caps", "get_capabilities", {})
        tcall(m, "license", "check_license", {})
        tcall(m, "maps", "list_maps", {})
        tcall(m, "active_map", "get_active_map", {})
        lr = tcall(m, "layers", "list_layers", {"include_hidden": True})
        buf = "CUENCA_PROYECTO_Buffer"
        tcall(m, "count", "count_features", {"layer_name": buf})
        tcall(m, "query", "query_layer", {"layer_name": buf, "where_clause": "1=1",
                                          "fields": "OBJECTID", "limit": 2})
        tcall(m, "measure", "measure_distance", {"layer_a": buf, "layer_b": buf})
        tcall(m, "intersects", "geometry_intersects", {"layer_a": buf, "layer_b": buf})
        tcall(m, "gp_count", "run_gp_tool", {"tool_name": "GetCount_management",
                                             "parameters": [buf], "add_outputs_to_map": False})
        tcall(m, "symb", "get_layer_symbology", {"layer_name": buf})
        tcall(m, "bm_create", "create_bookmark", {"name": "MCP_W3_BM"})
        tcall(m, "bm_zoom", "zoom_to_bookmark", {"name": "MCP_W3_BM"})
        tcall(m, "bm_del", "delete_bookmark", {"name": "MCP_W3_BM"})
        print(f"\n== MCP {len(PASS)} PASS, {len(FAIL)} FAIL ==")
        if FAIL:
            sys.exit(1)
    finally:
        m.close()


if __name__ == "__main__":
    main()
