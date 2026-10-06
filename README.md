# ArcGIS Pro Lab MCP

面向 GIS 课程实验的 ArcGIS Pro MCP。它打开软件自带的工具参数面板，通过公开 SDK 调用原生工具，并为操作保存真实窗口截图和运行记录。

本项目 fork 自 [IngKevinDavid/ArcGis-Pro-MCP-Free](https://github.com/IngKevinDavid/ArcGis-Pro-MCP-Free)。目前提供 43 个 MCP 工具入口，原生地理处理目录包含 44 个工具。

## 实验流程

调用 `arcgis_run_gp_tool` 后，服务器依次执行：

1. 在 ArcGIS Pro 中打开工具参数面板，填入参数和环境设置。
2. 用 Windows Graphics Capture 保存参数页截图。
3. 用同一组参数调用 `Geoprocessing.ExecuteToolAsync`，请求写入原生地理处理历史。
4. 保存真实的返回消息、输出路径和结果窗口截图。

执行方式是公开 SDK 调用，截图中的参数面板来自 ArcGIS Pro。SDK 不会模拟点击面板里的“运行”按钮，也不会将面板按钮或成功标记改造成已点击的样子。参数页、SDK 返回结果和实际输出共同构成记录。软件的地理处理设置可能影响是否写入历史。

`arcgis_preview_gp_tool` 只打开面板并截图；后续运行需要再次传入完整参数。`arcgis_capture_step` 可用于补充地图、属性或制图步骤的截图。地图、编辑、符号化等修改类接口也会自动保存操作前后的截图。

`arcgis_create_feature` 接受点、折线或单环面坐标，必须显式提供 `wkid`。面环需要首尾闭合。编辑完成后调用 `arcgis_save_edits` 保存数据；`arcgis_save_project` 保存工程配置。前者会保存当前工程的全部待保存编辑，应在确认编辑范围后调用。`arcgis_open_attribute_table` 和 `arcgis_open_layout` 打开相应原生视图并截图。

## 保留与删除

保留地图管理、属性查询、要素编辑、符号化、标注、布局和三维场景接口。原生工具目录覆盖建库、点线面转换、栅格配准、焦点统计、重分类、细化、等高线转换和 DEM/TIN 处理等实验步骤。

三维方向通过 SDK 的 `ZoomTo` 设置，并返回实际相机参数。工具调用采用 SDK 操作，需要在 ArcGIS Pro 中查看与核对；不提供自动点击界面的入口。

删除外部工具 wheel 和传输猴子补丁，直接使用 MCP Python SDK；删除 ArcPyExec、任意 Python 工具箱执行入口、在线发布、网络分析和统计建模等本实验不需要的 MCP 入口。`CalculateField` 和 `RasterCalculator` 不在本版目录中；属性赋值使用原生编辑接口，条件栅格处理使用 `Con`、`SetNull` 或 `Reclassify`。

每个接口返回结构化结果。参数截图失败时不执行地理处理；执行后截图失败时保留真实的运行结果，并单独标记证据不完整。连接中断不会自动重试操作。

## 安装

需要 Windows、ArcGIS Pro 3.5、Python 3.12 或更新版本。ArcGIS Pro 本身及相应扩展仍需可用许可。源码构建使用 .NET 8 SDK 和本机 ArcGIS Pro 的 SDK 程序集。

安装 Python 服务：

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install .
```

关闭所有 ArcGIS Pro 窗口后安装插件：

```powershell
.\scripts\install.ps1
```

脚本将安装包放入当前用户文档目录下的 `ArcGIS\AddIns\ArcGISPro`，同时备份旧安装包与本插件的程序集缓存。缓存按本插件 GUID 精确定位，不处理其他插件。Pro 仍在运行时，脚本会停止安装，避免新安装包和旧缓存混用。

本 fork 沿用上游插件 ID，作为其升级版本安装。安装后打开 Pro 和工程，在“GIS 实验 MCP”选项卡中点击按钮，再点击“启动 Start”。默认监听 `127.0.0.1:5876`。Python 服务会检查插件版本是否匹配。

Codex 配置示例，路径需替换为本机实际位置：

```toml
[mcp_servers.arcgis_lab]
command = 'C:\Tools\ArcGIS-Pro-Lab-MCP\.venv\Scripts\python.exe'
args = ['-m', 'arcgis_lab_mcp.server']
startup_timeout_sec = 30
tool_timeout_sec = 660

[mcp_servers.arcgis_lab.env]
PORT = '5876'
ARCGIS_LAB_EVIDENCE_DIR = 'C:\GISLab\Evidence'
PYTHONIOENCODING = 'utf-8'
```

使用其他 MCP 客户端时，设置同样的启动命令和环境变量即可。

## 调用示例

先用 `arcgis_health_check` 确认连接，再用 `arcgis_list_gp_tools` 查询原生工具名称。原生工具的参数数组遵循 Esri 工具帮助中的顺序，可选空值填写空字符串。

只展示参数，不执行：

```json
{
  "tool_name": "analysis.Buffer",
  "parameters": ["C:\\GISLab\\Data.gdb\\peak", "C:\\GISLab\\Results.gdb\\peak_buffer", "50 Meters", "FULL", "ROUND", "NONE"],
  "step_title": "设置缓冲区参数"
}
```

将以上参数传给 `arcgis_preview_gp_tool`。执行并完整记录时，传给 `arcgis_run_gp_tool`。

计数验证：

```json
{
  "tool_name": "management.GetCount",
  "parameters": ["C:\\GISLab\\Data.gdb\\peak"],
  "step_title": "检查高程点数量"
}
```

显式指定环境和记录目录：

```json
{
  "tool_name": "sa.Hillshade",
  "parameters": ["C:\\GISLab\\Results.gdb\\dem", "C:\\GISLab\\Results.gdb\\shade", "315", "45", "NO_SHADOWS", "1"],
  "environments": {"cellSize": "5"},
  "evidence_dir": "C:\\GISLab\\Evidence",
  "step_title": "生成山体阴影"
}
```

这些是参数格式示例，路径和许可需按实际工程填写。已有输出不会被覆盖，重新运行时应使用新的输出名称。

## 记录与截图

每个步骤生成独立目录，包含 `record.json` 和原始 PNG。GP 步骤使用 `01_parameters.png` 与 `02_result.png`，其他修改使用 `01_before.png` 与 `02_after.png`。

记录包括输入、环境、操作 ID、时间、Pro 工程、截图路径和实际返回消息。地图导出与窗口截图使用不同接口，不能将导出的地图图片当作软件操作截图。窗口不能最小化；截图只捕获与桥接进程匹配的 Pro 窗口，不录制桌面或其他应用。

长任务连接中断后，使用 `arcgis_get_gp_status` 和记录中的 `operation_id` 查询状态。插件在当前 Pro 会话中保存最近 32 次 GP 操作；重启后应结合已经写入磁盘的记录和实际输出判断。不得把超时当作失败后直接重跑。

## 构建与测试

```powershell
.\scripts\build.ps1 -ArcGISProDir 'C:\Program Files\ArcGIS\Pro'
python -m unittest discover -s tests -v
```

非默认位置的 .NET 或 Python 可通过 `-Dotnet` 和 `-Python` 参数指定。构建脚本只打包本项目程序集、DAML 和图标，不包含 Esri 程序集。

自动检查覆盖执行前后截图的顺序、失败状态、连接中断、重复请求风险、输入约束、中文传输和 MCP/native 接口对应关系。真实窗口、参数面板和地理处理结果需要在 Pro 中另外验证，见 `docs/validation.md`。

## 许可与来源

遵循 MIT 许可，保留上游与 ArcGIS MCP Contributors 的版权声明。详见 [LICENSE](LICENSE) 和 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
