import json
import os
import shutil
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
from contract_check import check, PASS, FAIL  # noqa: E402

TMP = r"C:\Users\Kevin\AppData\Local\Temp\opencode"


def main():
    check("w2_series", "create_map_series", {"layout_name": "MCP_TEST",
          "map_frame_name": "MCP Map Frame",
          "index_layer_name": "CUENCA_PROYECTO_Buffer", "name_field": "LAYER"})
    sdir = os.path.join(TMP, "w2_series")
    if os.path.isdir(sdir):
        shutil.rmtree(sdir, ignore_errors=True)
    check("w2_series_export", "export_map_series", {"layout_name": "MCP_TEST",
          "output_path": sdir, "format": "PDF", "resolution": 150})
    got = []
    for root, _, files in os.walk(TMP):
        got += [os.path.join(root, f) for f in files
                if f.lower().startswith("w2_series") and f.lower().endswith(".pdf")]
    # w2_series* only (never touch 02PET.pdf or other user files)
    if got:
        print(f"PASS w2_series_files ({len(got)} pdf)")
        PASS.append("w2_series_files")
        for p in got:
            try:
                os.remove(p)
            except OSError:
                pass
        shutil.rmtree(sdir, ignore_errors=True)
    else:
        FAIL.append("w2_series_files: no pdf exported")
        print("FAIL w2_series_files: no pdf exported")
    print(f"\n== SERIES {len(PASS)} PASS, {len(FAIL)} FAIL ==")
    if FAIL:
        sys.exit(1)


if __name__ == "__main__":
    main()
