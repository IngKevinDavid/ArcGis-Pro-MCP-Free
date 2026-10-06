import asyncio
import json
import os
import re
import struct
import tempfile
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch

from arcgis_lab_mcp import client, server, workflow


class WorkflowTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.args = {"tool_name": "management.GetCount", "parameters": ["sample.shp"], "evidence_dir": self.temp.name}
        self.events = []
        self.health = patch.object(client, "health", AsyncMock(return_value={"window_handle": 1, "process_id": 2}))
        self.health.start()
        self.addCleanup(self.health.stop)
        self.delay = patch.object(workflow.asyncio, "sleep", AsyncMock())
        self.delay.start()
        self.addCleanup(self.delay.stop)

    async def request(self, command, arguments):
        self.events.append(command)
        if command == "prepare_gp_tool":
            return {**arguments, "status": "prepared"}
        return {"status": "succeeded", "result": {"outputs": ["11"]}}

    async def capture(self, state, path):
        self.events.append(path.name)
        path.write_bytes(b"capture fixture")
        return {"path": str(path)}

    async def test_gp_execution_is_between_parameter_and_result_captures(self):
        with patch.object(client, "request", self.request), patch.object(workflow, "screenshot", self.capture):
            record = await workflow.perform("run_gp_tool", self.args)
        self.assertEqual(self.events, ["prepare_gp_tool", "01_parameters.png", "execute_prepared_tool", "02_result.png"])
        self.assertEqual(record["status"], "succeeded")
        saved = json.loads(Path(record["record_path"]).read_text(encoding="utf-8"))
        self.assertEqual(saved["result"]["result"]["outputs"], ["11"])
        self.assertTrue(saved["evidence_complete"])

    async def test_failed_parameter_capture_prevents_execution(self):
        with patch.object(client, "request", self.request), patch.object(workflow, "screenshot", AsyncMock(side_effect=RuntimeError("capture unavailable"))):
            record = await workflow.perform("run_gp_tool", self.args)
        self.assertNotIn("execute_prepared_tool", self.events)
        self.assertEqual(record["status"], "not_executed")

    async def test_failed_result_capture_preserves_successful_execution(self):
        count = 0
        async def screenshot(state, path):
            nonlocal count
            count += 1
            if count == 2:
                raise RuntimeError("capture unavailable")
            return await self.capture(state, path)
        with patch.object(client, "request", self.request), patch.object(workflow, "screenshot", screenshot):
            record = await workflow.perform("run_gp_tool", self.args)
        self.assertEqual(record["status"], "succeeded")
        self.assertFalse(record["evidence_complete"])
        self.assertEqual(self.events.count("execute_prepared_tool"), 1)

    async def test_native_failure_keeps_messages_and_after_screenshot(self):
        async def request(command, arguments):
            if command == "execute_prepared_tool":
                return {"status": "failed", "result": {"success": False, "messages": ["input not found"]}}
            return await self.request(command, arguments)
        with patch.object(client, "request", request), patch.object(workflow, "screenshot", self.capture):
            record = await workflow.perform("run_gp_tool", self.args)
        self.assertEqual(record["status"], "failed")
        self.assertEqual(len(record["screenshots"]), 2)
        self.assertIn("input not found", record["result"]["result"]["messages"])

    async def test_interrupted_execution_is_unknown_and_never_retried(self):
        async def request(command, arguments):
            if command == "execute_prepared_tool":
                self.events.append(command)
                raise client.BridgeError("connection interrupted")
            return await self.request(command, arguments)
        with patch.object(client, "request", request), patch.object(workflow, "screenshot", self.capture):
            record = await workflow.perform("run_gp_tool", self.args)
        self.assertEqual(record["status"], "outcome_unknown")
        self.assertEqual(self.events.count("execute_prepared_tool"), 1)
        self.assertEqual(len(record["operation_id"]), 32)

    async def test_preview_does_not_execute(self):
        with patch.object(client, "request", self.request), patch.object(workflow, "screenshot", self.capture):
            record = await workflow.perform("preview_gp_tool", self.args)
        self.assertEqual(record["status"], "preview_only")
        self.assertNotIn("execute_prepared_tool", self.events)

    async def test_explicit_native_failure_is_known_and_has_after_capture(self):
        with patch.object(client, "request", AsyncMock(side_effect=client.NativeCommandError("Layer not found"))), patch.object(workflow, "screenshot", self.capture):
            record = await workflow.perform("open_attribute_table", {"layer_name": "missing", "evidence_dir": self.temp.name})
        self.assertEqual(record["status"], "failed")
        self.assertTrue(record["evidence_complete"])
        self.assertEqual(len(record["screenshots"]), 2)
        self.assertIn("Layer not found", record["result"]["error"])

    async def test_missing_evidence_directory_fails_before_mutation(self):
        with patch.dict(os.environ, {}, clear=True), patch.object(client, "request", AsyncMock()) as request:
            with self.assertRaises(ValueError):
                await workflow.perform("run_gp_tool", {"parameters": []})
            request.assert_not_called()


class SchemaTests(unittest.IsolatedAsyncioTestCase):
    def test_public_parameters_match_native_dispatcher(self):
        source = (Path(__file__).resolve().parents[1] / "CommandHandler.cs").read_text(encoding="utf-8-sig")
        cases = dict(re.findall(r'case "(\w+)":(.*?)(?=case "|default:)', source, re.S))
        python_only = {"list_gp_tools", "preview_gp_tool", "run_gp_tool", "capture_step"}
        for tool in server.TOOLS:
            name = tool["name"].removeprefix("arcgis_")
            if name in python_only:
                continue
            self.assertIn(name, cases)
            consumed = set(re.findall(r'(?:paramsEl,\s*|GetProperty\()"([^"\n]+)"', cases[name]))
            public = set(tool["inputSchema"]["properties"]) - {"evidence_dir", "step_title"}
            self.assertEqual(consumed, public, name)

    async def test_geometry_type_and_explicit_coordinate_system_are_required(self):
        for args in [
            {"layer_name": "points", "geometry": {"type": "Point", "coordinates": [1, 2]}},
            {"layer_name": "points", "wkid": 4326, "geometry": {"type": "Point", "coordinates": [1]}},
            {"layer_name": "lines", "wkid": 4326, "geometry": {"type": "Polyline", "coordinates": [[1, 2]]}},
        ]:
            with patch.object(workflow, "perform", AsyncMock()) as perform:
                result = await server.call_tool("arcgis_create_feature", args)
            self.assertTrue(result.isError)
            perform.assert_not_called()

    async def test_optional_camera_scale_is_not_replaced_with_zero(self):
        with patch.object(workflow, "perform", AsyncMock(return_value={"success": True})) as perform:
            result = await server.call_tool("arcgis_set_camera_3d", {"heading": 30, "pitch": -45})
        self.assertFalse(result.isError)
        self.assertNotIn("scale", perform.call_args.args[1])

    async def test_custom_toolbox_and_python_expression_entry_points_are_rejected(self):
        for name in [r"C:\custom.pyt\ExecPython", "management.CalculateField", "sa.RasterCalculator"]:
            result = await server.call_tool("arcgis_run_gp_tool", {"tool_name": name, "parameters": []})
            self.assertTrue(result.isError)

    async def test_numeric_parameter_is_rejected_before_dispatch(self):
        with patch.object(workflow, "perform", AsyncMock()) as perform:
            result = await server.call_tool("arcgis_run_gp_tool", {"tool_name": "management.GetCount", "parameters": [11]})
        self.assertTrue(result.isError)
        perform.assert_not_called()

    async def test_incomplete_evidence_is_mcp_error_even_if_gp_succeeded(self):
        with patch.object(workflow, "perform", AsyncMock(return_value={"status": "succeeded", "evidence_complete": False})):
            result = await server.call_tool("arcgis_run_gp_tool", {"tool_name": "management.GetCount", "parameters": ["points"]})
        self.assertTrue(result.isError)
        self.assertEqual(result.structuredContent["status"], "succeeded")

    def test_schema_names_match_native_dispatcher(self):
        source = (Path(__file__).resolve().parents[1] / "CommandHandler.cs").read_text(encoding="utf-8-sig")
        for tool in json.loads((server.ROOT / "tools.json").read_text(encoding="utf-8")):
            self.assertIn('case "' + tool["name"].removeprefix("arcgis_") + '"', source)
        self.assertEqual(len(server.BY_NAME), len(server.TOOLS))


class TransportTests(unittest.IsolatedAsyncioTestCase):
    async def test_old_loaded_addin_is_rejected_even_with_same_protocol(self):
        with patch.object(client, "request", AsyncMock(return_value={"protocol_version": 2, "addin_version": "0.2.0-lab"})):
            with self.assertRaisesRegex(client.BridgeError, "Install the matching add-in"):
                await client.health()

    async def test_native_rejection_is_distinct_from_transport_interruption(self):
        async def serve(reader, writer):
            size = struct.unpack("<I", await reader.readexactly(4))[0]
            await reader.readexactly(size)
            body = json.dumps({"success": False, "error_code": "ArgumentException", "message": "Invalid geometry"}).encode()
            writer.write(struct.pack("<I", len(body)) + body)
            await writer.drain()
            writer.close()
        listener = await asyncio.start_server(serve, "127.0.0.1", 0)
        async with listener:
            with patch.dict(os.environ, {"PORT": str(listener.sockets[0].getsockname()[1])}):
                with self.assertRaisesRegex(client.NativeCommandError, "Invalid geometry"):
                    await client.request("create_feature")

    async def test_fragmented_utf8_response_is_read_exactly(self):
        received = []
        async def serve(reader, writer):
            size = struct.unpack("<I", await reader.readexactly(4))[0]
            received.append(json.loads(await reader.readexactly(size)))
            body = json.dumps({"success": True, "data": {"name": "地形图"}}, ensure_ascii=False).encode()
            packet = struct.pack("<I", len(body)) + body
            for value in packet:
                writer.write(bytes([value]))
                await writer.drain()
            writer.close()
        listener = await asyncio.start_server(serve, "127.0.0.1", 0)
        async with listener:
            with patch.dict(os.environ, {"PORT": str(listener.sockets[0].getsockname()[1])}):
                result = await client.request("test", {"title": "截图"})
        self.assertEqual(result["name"], "地形图")
        self.assertEqual(received[0]["params"]["title"], "截图")

    async def test_disconnect_after_send_does_not_retry(self):
        count = 0
        async def serve(reader, writer):
            nonlocal count
            count += 1
            await reader.read(4096)
            writer.close()
        listener = await asyncio.start_server(serve, "127.0.0.1", 0)
        async with listener:
            with patch.dict(os.environ, {"PORT": str(listener.sockets[0].getsockname()[1])}):
                with self.assertRaisesRegex(client.BridgeError, "may still be running"):
                    await client.request("execute_prepared_tool")
        self.assertEqual(count, 1)


if __name__ == "__main__":
    unittest.main()
