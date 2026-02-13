using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SeeSharp.ReferenceManager.Pages;

public class ReferenceInfo {
    public string FilePath { get; set; } = "";
    public string Resolution { get; set; } = "";
    public int MaxDepth { get; set; }
    public int MinDepth { get; set; }
    public string IntegratorName { get; set; } = "";
    public int Spp { get; set; }
    public string RenderTimeDisplay { get; set; } = "";
    public string Version { get; set; } = "";
    public string StartTimeDisplay { get; set; } = "";
    public string WriteTimeDisplay { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string RawJsonConfig { get; set; } = "";
    public List<RenderStep> RenderSteps { get; set; } = new();
}

public class RenderStep {
    public string Type { get; set; } = "";
    public double DurationMs { get; set; }
    public string StartTime { get; set; } = "";
    public string WriteTime { get; set; } = "";
}

public static class ReferenceUtils {
    public static IEnumerable<ReferenceInfo> ScanReferences(SceneConfig scene) {
        List<ReferenceInfo> refFiles = [];
        foreach (var f in scene.AvailableReferences)
        {
            var info = new ReferenceInfo {
                FilePath = f.Filename,
                Timestamp = File.GetLastWriteTime(f.Filename).ToString("yyyy-MM-dd HH:mm:ss")
            };

            info.MinDepth = f.MinDepth;
            info.MaxDepth = f.MaxDepth;
            info.Resolution = $"{f.Width}x{f.Height}";

            ReadMetadataFromJson(info, Path.ChangeExtension(f.Filename, ".json"));

            refFiles.Add(info);
        }
        return refFiles;
    }

    public static void ReadMetadataFromJson(ReferenceInfo info, string jsonPath) {
        if (!File.Exists(jsonPath)) return;

        string jsonContent = File.ReadAllText(jsonPath);
        var root = JsonNode.Parse(jsonContent);
        if (root == null) return;

        info.Version = root["SeeSharpVersion"]?.ToString() ?? "";
        info.StartTimeDisplay = root["RenderStartTime"]?.ToString() ?? "";
        info.WriteTimeDisplay = root["RenderWriteTime"]?.ToString() ?? "";

        if (root["RenderTime"] != null) {
            double ms = root["RenderTime"].GetValue<double>();
            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            if (t.TotalMinutes >= 1) info.RenderTimeDisplay = $"{(int)t.TotalMinutes:D2} m {t.Seconds:D2} s";
            else if (t.TotalSeconds >= 1) info.RenderTimeDisplay = $"{t.TotalSeconds:F1} s";
            else info.RenderTimeDisplay = $"{ms:F0} ms";
        }

        if (root["NumIterations"] != null) info.Spp = root["NumIterations"].GetValue<int>();

        var stepsNode = root["RenderSteps"];
        if (stepsNode is JsonArray arr) {
            foreach (var step in arr) {
                if (step == null) continue;
                info.RenderSteps.Add(new RenderStep {
                    Type = step["Type"]?.ToString() ?? "Unknown",
                    DurationMs = step["DurationMs"]?.GetValue<double>() ?? 0,
                    StartTime = step["StartTime"]?.ToString() ?? "",
                    WriteTime = step["WriteTime"]?.ToString() ?? ""
                });
            }
        }

        var settingsNode = root["Settings"];
        if (settingsNode != null) {
            var options = new JsonSerializerOptions { WriteIndented = true };
            info.RawJsonConfig = settingsNode.ToJsonString(options);
        }

        string integratorName = root["Integrator"]?.GetValue<string>();
        info.IntegratorName = integratorName?.Split('.')?.Last() ?? "Unknown";
    }

    public static string CurrentSeeSharpVersion { get; } =
        typeof(Scene).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "Unknown";
}