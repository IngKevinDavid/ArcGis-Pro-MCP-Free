"""Record actual inputs, results, and unmodified window screenshots per step."""
import asyncio
import json
import os
import re
import sys
from datetime import datetime
from pathlib import Path
from uuid import uuid4

from . import client


def write_record(folder: Path, record: dict) -> None:
    temporary = folder / "record.tmp"
    temporary.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(folder / "record.json")


def new_record(arguments: dict, tool: str) -> tuple[Path, dict]:
    root = arguments.get("evidence_dir") or os.environ.get("ARCGIS_LAB_EVIDENCE_DIR", "")
    if not root or not Path(root).is_absolute():
        raise ValueError("Provide an absolute evidence_dir or set ARCGIS_LAB_EVIDENCE_DIR before performing a lab step.")
    operation_id = uuid4().hex
    title = arguments.get("step_title") or tool
    slug = re.sub(r'[^\w\-]', '_', title)[:60]
    folder = Path(root) / f"{datetime.now():%Y%m%d_%H%M%S}_{slug}_{operation_id[:8]}"
    folder.mkdir(parents=True, exist_ok=False)
    record = {"operation_id": operation_id, "tool": tool, "title": title,
              "started_at": datetime.now().astimezone().isoformat(), "status": "preparing",
              "inputs": {k: v for k, v in arguments.items() if k not in {"evidence_dir", "step_title"}},
              "screenshots": [], "record_path": str(folder / "record.json")}
    write_record(folder, record)
    return folder, record


async def screenshot(state: dict, output: Path) -> dict:
    process = await asyncio.create_subprocess_exec(
        sys.executable, "-m", "arcgis_lab_mcp.capture", str(state["window_handle"]),
        str(state["process_id"]), str(output), stdin=asyncio.subprocess.DEVNULL,
        stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.PIPE)
    try:
        stdout, stderr = await asyncio.wait_for(process.communicate(), timeout=25)
    except TimeoutError as exc:
        if process.returncode is None:
            process.kill()
        await process.wait()
        raise RuntimeError("Window capture process exceeded 25 seconds; geoprocessing is not retried.") from exc
    except BaseException:
        if process.returncode is None:
            process.kill()
        await process.wait()
        raise
    if process.returncode:
        raise RuntimeError("Window capture failed: " + stderr.decode("utf-8", errors="replace")[-1200:])
    return json.loads(stdout)


async def perform(tool: str, arguments: dict) -> dict:
    folder, record = new_record(arguments, tool)
    sent = False
    outcome_known = False
    try:
        state = await client.health()
        record["application"] = state
        inputs = record["inputs"]
        if tool in {"preview_gp_tool", "run_gp_tool"}:
            prepared = await client.request("prepare_gp_tool", {**inputs, "operation_id": record["operation_id"]})
            record["prepared"] = prepared
            # Opening the SDK pane is asynchronous internally; allow it to render.
            await asyncio.sleep(1.2)
            record["screenshots"].append(await screenshot(state, folder / "01_parameters.png"))
            record["status"] = "prepared"
            write_record(folder, record)
            if tool == "preview_gp_tool":
                outcome_known = True
                record["status"] = "preview_only"
            else:
                record["status"] = "running"
                write_record(folder, record)
                sent = True
                result = await client.request("execute_prepared_tool", {"operation_id": record["operation_id"]})
                outcome_known = True
                record["result"] = result
                record["status"] = result["status"]
                write_record(folder, record)
                await asyncio.sleep(1.0)
                record["screenshots"].append(await screenshot(state, folder / "02_result.png"))
        elif tool == "capture_step":
            record["screenshots"].append(await screenshot(state, folder / "01_window.png"))
            record["status"] = "captured"
            outcome_known = True
        else:
            record["screenshots"].append(await screenshot(state, folder / "01_before.png"))
            record["status"] = "running"
            write_record(folder, record)
            sent = True
            try:
                result = await client.request(tool, inputs)
            except client.NativeCommandError as exc:
                result = {"success": False, "error": str(exc), "state_changes_may_exist": True}
            outcome_known = True
            record["result"] = result
            record["status"] = "failed" if isinstance(result, dict) and result.get("success") is False else "succeeded"
            write_record(folder, record)
            await asyncio.sleep(0.8)
            record["screenshots"].append(await screenshot(state, folder / "02_after.png"))
        record["evidence_complete"] = True
    except (Exception, asyncio.CancelledError) as exc:
        record["error"] = "Client cancelled this operation." if isinstance(exc, asyncio.CancelledError) else (str(exc) or type(exc).__name__)
        record["evidence_complete"] = False
        if not outcome_known:
            record["status"] = "outcome_unknown" if sent else "not_executed"
        if sent and not outcome_known:
            record["next_action"] = ("Read arcgis_get_gp_status with operation_id before repeating an interrupted GP operation."
                                     if tool == "run_gp_tool" else "Inspect the project and actual outputs before repeating the interrupted operation.")
        else:
            record["next_action"] = "Review the saved record and the failed stage."
        if isinstance(exc, asyncio.CancelledError):
            write_record(folder, record)
            raise
    finally:
        record["finished_at"] = datetime.now().astimezone().isoformat()
        write_record(folder, record)
    return record
