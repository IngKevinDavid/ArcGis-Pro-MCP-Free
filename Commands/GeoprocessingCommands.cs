using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Core.Geoprocessing;
namespace ArcGisProMcpFree.Commands
{
    public static class GeoprocessingCommands
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ToolRun> Runs = new Dictionary<string, ToolRun>();
        private static readonly HashSet<string> AllowedTools = LoadTools();
        private static ToolRun _prepared;
        private static bool _running;
        public static bool IsRunning { get { lock (Sync) return _running; } }
        public class ToolRun
        {
            public string operation_id { get; set; }
            public string tool_name { get; set; }
            public string[] parameters { get; set; }
            public Dictionary<string, string> environments { get; set; }
            public string status { get; set; }
            public string prepared_at { get; set; }
            public string started_at { get; set; }
            public string finished_at { get; set; }
            public bool add_outputs_to_map { get; set; }
            public object result { get; set; }
        }
        private static HashSet<string> LoadTools()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("gp_tools.json"));
            using var stream = assembly.GetManifestResourceStream(resource);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        public static async Task<object> PrepareAsync(JsonElement args)
        {
            string name = args.GetProperty("tool_name").GetString();
            if (name == null || !AllowedTools.Contains(name)) throw new ArgumentException("Tool is not in the lab's built-in catalog. Use arcgis_list_gp_tools; script/toolbox paths are not accepted.");
            string id = args.GetProperty("operation_id").GetString();
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid operation_id.");
            var env = args.TryGetProperty("environments", out var envJson)
                ? envJson.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()) : new Dictionary<string, string>();
            env["overwriteOutput"] = "false";
            var run = new ToolRun
            {
                operation_id = id, tool_name = name,
                parameters = args.GetProperty("parameters").EnumerateArray().Select(p => p.GetString() ?? "").ToArray(),
                environments = env, status = "opening", prepared_at = DateTimeOffset.Now.ToString("o"),
                add_outputs_to_map = !args.TryGetProperty("add_outputs_to_map", out var add) || add.GetBoolean()
            };
            lock (Sync)
            {
                if (_running) throw new InvalidOperationException("A geoprocessing operation is running. Read its status before starting another.");
                if (Runs.ContainsKey(id)) throw new InvalidOperationException("operation_id already exists; read its status instead of repeating it.");
                if (_prepared != null && _prepared.status == "prepared") _prepared.status = "superseded";
                _prepared = run; Runs.Add(id, run);
                foreach (var key in Runs.Keys.Take(Math.Max(0, Runs.Count - 32)).ToArray()) Runs.Remove(key);
            }
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() => Geoprocessing.OpenToolDialog(name, run.parameters, run.environments, false));
                lock (Sync) run.status = "prepared";
                return run;
            }
            catch { lock (Sync) run.status = "prepare_failed"; throw; }
        }
        public static object GetStatus(string id)
        {
            lock (Sync)
            {
                if (!Runs.TryGetValue(id, out var run)) throw new ArgumentException("Unknown operation_id. Only the latest 32 operations in this Pro session are retained.");
                return run;
            }
        }
        public static async Task<object> ExecutePreparedAsync(string id)
        {
            ToolRun run;
            lock (Sync)
            {
                if (!Runs.TryGetValue(id, out run)) throw new ArgumentException("Unknown operation_id.");
                if (run.status != "prepared" || !ReferenceEquals(run, _prepared)) throw new InvalidOperationException("Not the current prepared tool. Read its status; completed operations cannot be executed twice.");
                if (_running) throw new InvalidOperationException("Another geoprocessing operation is running.");
                _running = true; run.status = "running"; run.started_at = DateTimeOffset.Now.ToString("o");
            }
            try
            {
                var flags = GPExecuteToolFlags.AddToHistory | GPExecuteToolFlags.RefreshProjectItems | GPExecuteToolFlags.GPThread;
                if (run.add_outputs_to_map) flags |= GPExecuteToolFlags.AddOutputsToMap;
                var result = await Geoprocessing.ExecuteToolAsync(run.tool_name, run.parameters, run.environments, null, null, flags);
                lock (Sync)
                {
                    run.result = new {
                        success = !result.IsFailed,
                        messages = result.Messages?.Select(m => new { type = m.Type.ToString(), text = m.Text }).ToArray(),
                        outputs = result.Values?.Select(v => v?.ToString() ?? "").ToArray(), return_value = result.ReturnValue
                    };
                    run.status = result.IsFailed ? "failed" : "succeeded";
                }
            }
            catch (Exception ex) { lock (Sync) { run.status = "failed"; run.result = new { success = false, error = ex.Message }; } }
            finally { lock (Sync) { run.finished_at = DateTimeOffset.Now.ToString("o"); _running = false; } }
            return run;
        }
    }
}
