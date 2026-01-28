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
    public static void ScanReferences(string sceneDir, List<ReferenceInfo> referenceFiles) {
        referenceFiles.Clear();
        if (string.IsNullOrEmpty(sceneDir)) return;

        string refDir = Path.Combine(sceneDir, "References");
        if (!Directory.Exists(refDir)) return;

        var exrFiles = Directory.GetFiles(refDir, "*.exr")
                                .OrderByDescending(f => File.GetLastWriteTime(f));

        foreach (var f in exrFiles) {
            var info = new ReferenceInfo {
                FilePath = f,
                Resolution = GetResolution(f),
                Timestamp = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss")
            };
            ReadMetadataFromJson(info, f);
            referenceFiles.Add(info);
        }
    }

    public static void ReadMetadataFromJson(ReferenceInfo info, string exrPath) {
        string folder = Path.GetDirectoryName(exrPath);
        string fileNameNoExt = Path.GetFileNameWithoutExtension(exrPath);
        string jsonPath = Path.Combine(folder, $"{fileNameNoExt}.json");

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
            info.MaxDepth = settingsNode["MaxDepth"]?.GetValue<int>() ?? 0;
            info.MinDepth = settingsNode["MinDepth"]?.GetValue<int>() ?? 0;
        }

        string integratorName = root["Name"]?.GetValue<string>();
        info.IntegratorName = integratorName?.Split('.')?.Last() ?? "Unknown";
    }

    public static string GetResolution(string filePath) {
        string filename = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(filename, @"Width(\d+)-Height(\d+)", RegexOptions.IgnoreCase);
        if (match.Success) return $"{match.Groups[1].Value}x{match.Groups[2].Value}";
        return "Unknown";
    }

    public static void CopyValues(object target, object source) {
        if (target == null || source == null || target.GetType() != source.GetType()) return;
        var type = target.GetType();

        bool IsConfigParam(Type t) {
            return t == typeof(string) || t.IsValueType;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite)) {
            if (IsConfigParam(prop.PropertyType))
                prop.SetValue(target, prop.GetValue(source));
        }
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            if (IsConfigParam(field.FieldType))
                field.SetValue(target, field.GetValue(source));
        }
    }

    public static void CopyImage(RgbImage target, RgbImage source) {
        Parallel.For(0, target.Height, y => {
            for (int x = 0; x < target.Width; ++x)
                target.SetPixel(x, y, source.GetPixel(x, y));
        });
    }

    public static Integrator CloneIntegrator(Integrator source) {
        var type = source.GetType();
        var clone = (Integrator)Activator.CreateInstance(type);
        CopyValues(clone, source);
        return clone;
    }

    public static void SetMaxDepth(Integrator integrator, int depth) {
        var type = integrator.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var prop = type.GetProperty("MaxDepth", flags);
        if (prop != null && prop.CanWrite) {
            prop.SetValue(integrator, depth);
            return;
        }
        var field = type.GetField("MaxDepth", flags);
        if (field != null) {
            field.SetValue(integrator, depth);
        }
    }

    public static void SetMinDepth(Integrator integrator, int depth) {
        var type = integrator.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var prop = type.GetProperty("MinDepth", flags);
        if (prop != null && prop.CanWrite) {
            prop.SetValue(integrator, depth);
            return;
        }
        var field = type.GetField("MinDepth", flags);
        if (field != null) {
            field.SetValue(integrator, depth);
        }
    }

    public static int GetTargetSpp(Integrator integrator) {
        var type = integrator.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var propSpp = type.GetProperty("TotalSpp", flags);
        if (propSpp != null) return (int)propSpp.GetValue(integrator);

        var fieldSpp = type.GetField("TotalSpp", flags);
        if (fieldSpp != null) return (int)fieldSpp.GetValue(integrator);

        var propIter = type.GetProperty("NumIterations", flags);
        if (propIter != null) return (int)propIter.GetValue(integrator);

        var fieldIter = type.GetField("NumIterations", flags);
        if (fieldIter != null) return (int)fieldIter.GetValue(integrator);
        return 16;
    }

    public static void SaveConfig(string folder, Integrator integrator) {
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        var rootNode = new JsonObject();
        rootNode.Add("Name", integrator.GetType().Name);
        var settingsNode = JsonSerializer.SerializeToNode(integrator, integrator.GetType(), options);
        rootNode.Add("Settings", settingsNode);
        File.WriteAllText(Path.Combine(folder, "Config.json"), rootNode.ToJsonString(options));
    }

    public static void SetBatchSpp(Integrator integrator, int batchCount) {
        var type = integrator.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var targetNames = new[] { "TotalSpp", "NumIterations"};
        foreach (var name in targetNames) {
            var prop = type.GetProperty(name, flags);
            if (prop != null && prop.CanWrite) {
                prop.SetValue(integrator, batchCount);
                return;
            }
            var field = type.GetField(name, flags);
            if (field != null) {
                field.SetValue(integrator, batchCount);
                return;
            }
        }
    }

    public static uint GetBaseSeed(Integrator integrator) {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var seedProp = integrator.GetType().GetProperty("BaseSeed", flags);
        var seedField = integrator.GetType().GetField("BaseSeed", flags);
        if (seedProp != null) return (uint)(seedProp.GetValue(integrator) ?? 0u);
        if (seedField != null) return (uint)(seedField.GetValue(integrator) ?? 0u);
        return 0;
    }

    public static void SetBaseSeed(Integrator integrator, uint seed) {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var seedProp = integrator.GetType().GetProperty("BaseSeed", flags);
        var seedField = integrator.GetType().GetField("BaseSeed", flags);
        if (seedProp != null) seedProp.SetValue(integrator, seed);
        else if (seedField != null) seedField.SetValue(integrator, seed);
    }

    public static void PrepareFrameBuffer(Scene scene, int width, int height, string finalPath, Integrator integrator, FrameBuffer.Flags flags) {
        if (scene == null) return;

        scene.FrameBuffer = new FrameBuffer(width, height, finalPath, flags);

        var options = new JsonSerializerOptions {
            IncludeFields = true,
            WriteIndented = true
        };
        var fullSettingsNode = JsonSerializer.SerializeToNode(integrator, integrator.GetType(), options);

        scene.FrameBuffer.MetaData["Name"] = integrator.GetType().Name;
        scene.FrameBuffer.MetaData["Settings"] = fullSettingsNode;
    }

    public static string CurrentSeeSharpVersion { get; } =
        typeof(Scene).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "Unknown";
}