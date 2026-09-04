"""Minimal MCP stdio driver: talks to arcgis-mcp-server.exe exactly like
opencode does (JSON-RPC over stdio), so work visibly goes through the MCP
tool layer instead of the raw named pipe.
Usage:
  python mcp_driver.py list
  python mcp_driver.py call <tool_name> '<json_args>'
"""
import json
import subprocess
import sys

VENV_PY = r"D:\Rstudio\05_herramientas\ArcGeekLibre.Addin\py-server\.venv\Scripts\python.exe"
BRIDGE = r"D:\Rstudio\05_herramientas\ArcGeekLibre.Addin\py-server\tcp_bridge.py"


class Mcp:
    def __init__(self):
        self.p = subprocess.Popen([VENV_PY, BRIDGE], stdin=subprocess.PIPE,
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                  text=True, bufsize=1)
        self._id = 0
        self.req("initialize", {"protocolVersion": "2024-11-05",
                                "capabilities": {},
                                "clientInfo": {"name": "mcp-driver", "version": "1"}})
        self.notify("notifications/initialized", {})

    def _send(self, obj):
        self.p.stdin.write(json.dumps(obj) + "\n")
        self.p.stdin.flush()

    def _recv(self):
        line = self.p.stdout.readline()
        if not line:
            raise OSError("MCP server closed stdout")
        return json.loads(line)

    def req(self, method, params):
        self._id += 1
        self._send({"jsonrpc": "2.0", "id": self._id,
                    "method": method, "params": params})
        while True:
            r = self._recv()
            if r.get("id") == self._id:
                if "error" in r:
                    raise RuntimeError(json.dumps(r["error"])[:500])
                return r.get("result")

    def notify(self, method, params):
        self._send({"jsonrpc": "2.0", "method": method, "params": params})

    def close(self):
        try:
            self.p.stdin.close()
        except Exception:
            pass
        self.p.wait(timeout=30)


def main():
    m = Mcp()
    try:
        if sys.argv[1] == "list":
            tools = m.req("tools/list", {}).get("tools", [])
            print(f"{len(tools)} tools")
            for t in tools:
                print("-", t["name"])
        elif sys.argv[1] == "call":
            name = sys.argv[2]
            raw = sys.argv[3]
            if raw.startswith("@"):
                with open(raw[1:], encoding="utf-8") as f:
                    raw = f.read()
            args = json.loads(raw)
            res = m.req("tools/call", {"name": name, "arguments": args})
            print(json.dumps(res, ensure_ascii=False)[:3000])
    finally:
        m.close()


if __name__ == "__main__":
    main()
