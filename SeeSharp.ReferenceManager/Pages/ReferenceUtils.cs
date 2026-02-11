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
                Timestamp = File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm:ss")
            };
            ReadMetadataFromJson(info, f);
            ParseExrName(info, f);
            referenceFiles.Add(info);
        }
    }

    public static void ParseExrName(ReferenceInfo info, string filePath) {
        string filename = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(filename, @"(?:MinDepth(?<min>\d+)-)?MaxDepth(?<max>\d+)-Width(?<w>\d+)-Height(?<h>\d+)", RegexOptions.IgnoreCase);

        if (match.Success) {
            info.MinDepth = match.Groups["min"].Success ? int.Parse(match.Groups["min"].Value) : 1;
            info.MaxDepth = int.Parse(match.Groups["max"].Value);
            info.Resolution = $"{match.Groups["w"].Value}x{match.Groups["h"].Value}";
        } else {
            if (string.IsNullOrEmpty(info.Resolution))
                Logger.Warning($"Can't find resolution.");
            else if (info.MaxDepth <= 0)
                Logger.Warning($"The maximum depth is wrong.");
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
        }

        string integratorName = root["Name"]?.GetValue<string>();
        info.IntegratorName = integratorName?.Split('.')?.Last() ?? "Unknown";
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

    public static Integrator CloneIntegrator(Integrator source) {
        var type = source.GetType();
        var clone = (Integrator)Activator.CreateInstance(type);
        CopyValues(clone, source);
        return clone;
    }

    static T GetFieldOrProperty<T>(object instance, string name)
    {
        var type = instance.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance;

        var propSpp = type.GetProperty(name, flags);
        if (propSpp != null)
            return (T)propSpp.GetValue(instance);

        var fieldSpp = type.GetField(name, flags);
        if (fieldSpp != null)
            return (T)fieldSpp.GetValue(instance);

        throw new ArgumentException($"No field or property named '{name}' in '{instance.GetType()}'");
    }

    static void SetFieldOrProperty<T>(object instance, string name, T value)
    {
        var type = instance.GetType();
        var flags = BindingFlags.Public | BindingFlags.Instance;

        var prop = type.GetProperty(name, flags);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(instance, value);
            return;
        }

        var field = type.GetField(name, flags);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }

        throw new ArgumentException($"No field or property named '{name}' in '{instance.GetType()}'");
    }

    public static void SaveConfig(string folder, Integrator integrator) {
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        var rootNode = new JsonObject();
        rootNode.Add("Name", integrator.GetType().Name);
        var settingsNode = JsonSerializer.SerializeToNode(integrator, integrator.GetType(), options);
        rootNode.Add("Settings", settingsNode);
        File.WriteAllText(Path.Combine(folder, "Config.json"), rootNode.ToJsonString(options));
    }

    public static string CurrentSeeSharpVersion { get; } =
        typeof(Scene).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "Unknown";
}