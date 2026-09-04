# -*- coding: utf-8 -*-
"""ArcPyExec toolbox: generic Python execution for the Libre MCP Add-In.

Called through the stock arcgis-mcp `run_gp_tool` wrapper (no server fork
needed) with tool_name pointing at this toolbox, e.g.:

    run_gp_tool(
        tool_name=r"D:\\Rstudio\\05_herramientas\\ArcGeekLibre.Addin\\ArcPyExec.pyt\\ExecPython",
        parameters=['print("hola desde Pro")'])

If Pro rejects the toolbox-path form, add this .pyt once to the project
toolboxes and call it as "arcpyexec.ExecPython" instead.

The tool exec()s the given code with `arcpy` preloaded, captures stdout and
an optional `result` variable, and reports everything as a JSON string both
in the derived output parameter and in the GP messages (which is what
run_gp_tool forwards back to the MCP client).
"""

import contextlib
import io
import json
import traceback

import arcpy


class Toolbox:
    def __init__(self):
        self.label = "ArcPyExec"
        self.alias = "arcpyexec"
        self.tools = [ExecPython]


class ExecPython:
    def __init__(self):
        self.label = "Execute Python code"
        self.description = (
            "Executes arbitrary arcpy Python code inside ArcGIS Pro and "
            "returns stdout plus an optional `result` variable as JSON."
        )

    def getParameterInfo(self):
        code = arcpy.Parameter(
            displayName="Python code",
            name="code",
            datatype="GPString",
            parameterType="Required",
            direction="Input")
        output = arcpy.Parameter(
            displayName="Output (JSON)",
            name="output",
            datatype="GPString",
            parameterType="Derived",
            direction="Output")
        return [code, output]

    def isLicensed(self):
        return True

    def execute(self, parameters, messages):
        code = parameters[0].valueAsText or ""
        buf = io.StringIO()
        namespace = {"arcpy": arcpy}
        try:
            with contextlib.redirect_stdout(buf):
                exec(code, namespace)  # noqa: S102 - this IS the feature
            payload = {"success": True, "output": buf.getvalue()}
            if "result" in namespace:
                try:
                    payload["return"] = repr(namespace["result"])
                except Exception:
                    payload["return"] = "<unrepresentable>"
        except Exception:
            payload = {
                "success": False,
                "output": buf.getvalue(),
                "error": traceback.format_exc(),
            }
        encoded = json.dumps(payload)
        try:
            parameters[1].value = encoded
        except Exception:
            pass
        messages.addMessage(encoded)
        return
