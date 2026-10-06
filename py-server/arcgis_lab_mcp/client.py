"""Local, single-request TCP client. Mutations are never retried automatically."""
import asyncio
import json
import os
import struct
from typing import Any
from . import __version__


class BridgeError(RuntimeError):
    pass


class NativeCommandError(BridgeError):
    """The bridge returned an explicit failure; this is not a lost response."""


async def request(command: str, parameters: dict[str, Any] | None = None, timeout: float = 600) -> dict:
    port = int(os.environ.get("PORT", "5876"))
    if not 1 <= port <= 65535:
        raise BridgeError("PORT must be between 1 and 65535.")
    payload = json.dumps({"command": command, "params": parameters or {}}, ensure_ascii=False).encode("utf-8")
    if len(payload) > 10 * 1024 * 1024:
        raise BridgeError("Request exceeds the bridge's 10 MiB limit.")
    writer = None
    sent = False
    try:
        async with asyncio.timeout(timeout):
            reader, writer = await asyncio.open_connection("127.0.0.1", port)
            writer.write(struct.pack("<I", len(payload)) + payload)
            sent = True
            await writer.drain()
            length = struct.unpack("<I", await reader.readexactly(4))[0]
            if not 0 < length <= 16 * 1024 * 1024:
                raise BridgeError("Invalid response length from ArcGIS Pro.")
            response = json.loads(await reader.readexactly(length))
            if not response.get("success"):
                raise NativeCommandError(f"{response.get('error_code')}: {response.get('message')}")
            return response["data"]
    except (OSError, TimeoutError, asyncio.IncompleteReadError) as exc:
        if sent:
            raise BridgeError("Connection ended after the request was sent. The operation may still be running; read its status and do not repeat it automatically.") from exc
        raise BridgeError("Cannot connect to ArcGIS Pro. Open a project and start GIS 实验 MCP on the configured port.") from exc
    finally:
        if writer is not None:
            writer.close()
            await writer.wait_closed()


async def health() -> dict:
    result = await request("health_check", timeout=20)
    if result.get("protocol_version") != 2 or result.get("addin_version") != __version__ + "-lab":
        raise BridgeError(f"Expected lab add-in {__version__}; Pro reports {result.get('addin_version', 'unknown')}. Install the matching add-in, restart Pro, and click Start.")
    return result
