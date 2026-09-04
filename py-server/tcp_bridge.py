"""MCP stdio bridge speaking TCP to the Libre Add-In (127.0.0.1:PORT).

Same 167 tools as the stock arcgis-mcp-server: this launcher reuses every
tool definition from the installed arcgis_mcp package and only swaps the
transport (named pipe -> TCP loopback), so the package itself is untouched.

  PORT env var selects the port (default 5876, same default as the Add-In).
  Run with the py-server venv python so arcgis_mcp is importable.
"""
import json
import os
import socket
import struct
import sys
import time

HOST = "127.0.0.1"


def _port():
    try:
        p = int(os.environ.get("PORT", "5876") or 5876)
        return p if 1 <= p <= 65535 else 5876
    except ValueError:
        return 5876


PORT = _port()

from arcgis_mcp import pipe_client as pc  # noqa: E402


def _read_exactly(sock, size):
    chunks = bytearray()
    while len(chunks) < size:
        chunk = sock.recv(size - len(chunks))
        if not chunk:
            raise OSError("ArcGIS Pro TCP bridge closed before the full response was read.")
        chunks += chunk
    return bytes(chunks)


class TcpBridgeClient(pc.ArcGisPipeClient):
    """Drop-in replacing only the transport: same framing over TCP."""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.host = HOST
        self.port = PORT

    def _send_once(self, command, params, timeout_ms):
        request = json.dumps({"command": command, "params": params or {}}).encode("utf-8")
        sock = socket.create_connection((self.host, self.port), timeout=timeout_ms / 1000)
        try:
            sock.sendall(struct.pack("<I", len(request)) + request)
            resp_len = struct.unpack("<I", _read_exactly(sock, 4))[0]
            return json.loads(_read_exactly(sock, resp_len).decode("utf-8"))
        finally:
            sock.close()


pc.ArcGisPipeClient = TcpBridgeClient

from arcgis_mcp.server import main  # noqa: E402

if __name__ == "__main__":
    sys.stderr.write(f"libre tcp bridge -> {HOST}:{PORT}\n")
    main()
