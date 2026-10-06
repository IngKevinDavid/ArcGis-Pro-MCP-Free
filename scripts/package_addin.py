"""Package only our add-in assembly, DAML, and icons; never bundle Esri DLLs."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
build = root / "bin/Release/win-x64"
target = root / "package/ArcGisProMcpFree.esriAddinX"
target.parent.mkdir(exist_ok=True)
with ZipFile(target, "w", ZIP_DEFLATED) as archive:
    archive.write(root / "Config.daml", "Config.daml")
    for name in ["LICENSE", "THIRD_PARTY_NOTICES.md"]:
        archive.write(root / name, name)
    for path in (root / "Images").glob("*.png"):
        archive.write(path, f"Images/{path.name}")
    for name in ["ArcGisProMcpFree.dll", "ArcGisProMcpFree.deps.json"]:
        archive.write(build / name, f"Install/{name}")
print(target)
