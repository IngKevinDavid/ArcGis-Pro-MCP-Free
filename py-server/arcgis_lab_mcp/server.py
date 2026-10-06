"""Small, schema-described MCP surface for ArcGIS Pro experiments."""
import asyncio
import json
import logging
from pathlib import Path

from jsonschema import Draft202012Validator
from mcp import types
from mcp.server.lowlevel import Server
from mcp.server.stdio import stdio_server

from . import __version__, client, workflow

ROOT = Path(__file__).parent
CATALOG = json.loads((ROOT / "gp_tools.json").read_text(encoding="utf-8"))
TOOLS = json.loads((ROOT / "tools.json").read_text(encoding="utf-8"))
EVIDENCE = {
    "evidence_dir": {"type": "string", "description": "Absolute folder for screenshots and records; defaults to ARCGIS_LAB_EVIDENCE_DIR."},
    "step_title": {"type": "string", "maxLength": 120, "description": "实验步骤名称，用于截图文件夹和记录。"},
}


def define(name: str, description: str, properties: dict, required: list[str], read_only=False):
    TOOLS.append({"name": "arcgis_" + name, "description": description,
                  "inputSchema": {"type": "object", "properties": properties, "required": required, "additionalProperties": False},
                  "annotations": {"readOnlyHint": read_only, "destructiveHint": False,
                                  "idempotentHint": read_only, "openWorldHint": False}})


GP_INPUT = {
    "tool_name": {"type": "string", "enum": [t["name"] for t in CATALOG], "description": "原生工具名称，先用 arcgis_list_gp_tools 查看。"},
    "parameters": {"type": "array", "items": {"type": "string"}, "maxItems": 100,
                   "description": "按工具帮助列出的参数顺序填写。可选空值使用空字符串。"},
    "environments": {"type": "object", "additionalProperties": {"type": "string"},
                     "description": "原生环境设置，例如 cellSize、extent、outputCoordinateSystem；禁止覆盖已有输出。"},
    "add_outputs_to_map": {"type": "boolean", "default": True},
    **EVIDENCE,
}
define("list_gp_tools", "列出实验用原生工具目录，不执行工具。", {"query": {"type": "string", "default": ""}}, [], True)
define("preview_gp_tool", "打开真实地理处理参数面板并截图，不执行。预览后运行时仍需传入完整参数。",
       GP_INPUT, ["tool_name", "parameters"])
define("run_gp_tool", "打开真实参数面板，先截图，再通过 ArcGIS Pro SDK 执行同一组参数，最后截图并保存运行消息。无 ArcPy 任意脚本入口。",
       GP_INPUT, ["tool_name", "parameters"])
define("get_gp_status", "查询本次 Pro 会话中最近 32 次 GP 操作的状态。连接中断后先查询，避免重复执行。",
       {"operation_id": {"type": "string", "pattern": "^[a-f0-9]{32}$"}}, ["operation_id"], True)
define("capture_step", "截取当前 ArcGIS Pro 完整窗口，包含 GPU 地图画面，保存原始 PNG 和步骤记录。仅截图，不操作鼠标键盘。", EVIDENCE, [])
define("create_map", "创建并打开地图或局部三维场景，设置坐标系，自动保存前后截图。",
       {"name": {"type": "string", "minLength": 1}, "map_type": {"type": "string", "enum": ["Map", "Scene"], "default": "Map"},
        "wkid": {"type": "integer", "minimum": 1, "default": 4326}, **EVIDENCE}, ["name"])
define("open_attribute_table", "打开图层的原生属性表并保存窗口截图。",
       {"layer_name": {"type": "string", "minLength": 1}, **EVIDENCE}, ["layer_name"])
define("open_layout", "打开已有布局视图并保存窗口截图。",
       {"layout_name": {"type": "string", "minLength": 1}, **EVIDENCE}, ["layout_name"])
define("save_edits", "保存当前工程的全部待保存数据编辑。保存后无法再撤销这些编辑；自动保存前后截图。", EVIDENCE, [])
TOOLS[-1]["annotations"]["destructiveHint"] = True
BY_NAME = {tool["name"]: tool for tool in TOOLS}
server = Server("arcgis_lab_mcp", version=__version__,
                instructions="Use native tool panels and automatic evidence records. Do not repeat interrupted mutations; inspect status first. A screenshot failure is not a geoprocessing failure. Never describe a map export as an interface screenshot.")
operation_lock = asyncio.Lock()


@server.list_tools()
async def list_tools() -> list[types.Tool]:
    return [types.Tool(**tool) for tool in TOOLS]


@server.call_tool(validate_input=False)
async def call_tool(name: str, arguments: dict) -> types.CallToolResult:
    try:
        if name not in BY_NAME:
            raise ValueError(f"Unknown tool: {name}")
        definition = BY_NAME[name]
        Draft202012Validator(definition["inputSchema"]).validate(arguments)
        arguments = {**{key: prop["default"] for key, prop in definition["inputSchema"]["properties"].items() if "default" in prop}, **arguments}
        command = name.removeprefix("arcgis_")
        if command in {"preview_gp_tool", "run_gp_tool"}:
            env = arguments.get("environments", {})
            if any(k.lower() == "overwriteoutput" and v.lower() not in {"false", "0"} for k, v in env.items()):
                raise ValueError("Existing outputs cannot be overwritten. Choose a new output dataset name.")
        if command == "list_gp_tools":
            query = arguments.get("query", "").lower()
            result = {"tools": [t for t in CATALOG if query in (t["name"] + t["title"]).lower()]}
        elif command == "health_check":
            result = await client.health()
            result.update(mcp_tool_count=len(TOOLS), native_gp_tool_count=len(CATALOG))
        elif definition["annotations"]["readOnlyHint"]:
            await client.health()
            result = await client.request(command, arguments)
        else:
            async with operation_lock:
                result = await workflow.perform(command, arguments)
        failed = (isinstance(result, dict) and (result.get("evidence_complete") is False
                  or result.get("status") in {"failed", "not_executed", "outcome_unknown"}
                  or result.get("success") is False))
        return types.CallToolResult(content=[types.TextContent(type="text", text=json.dumps(result, ensure_ascii=False))],
                                    structuredContent=result, isError=failed)
    except Exception as exc:
        result = {"error": str(exc), "error_type": type(exc).__name__}
        return types.CallToolResult(content=[types.TextContent(type="text", text=json.dumps(result, ensure_ascii=False))],
                                    structuredContent=result, isError=True)


async def run() -> None:
    async with stdio_server() as (reader, writer):
        await server.run(reader, writer, server.create_initialization_options())


def main() -> None:
    logging.basicConfig(level=logging.WARNING)
    asyncio.run(run())


if __name__ == "__main__":
    main()
