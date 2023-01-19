using System.Text.Json.Serialization;

namespace SeeSharp.Shading;

public class ShadingStats {
    public static void Reset() {
        Current = new();
    }

    public static ShadingStats Current { get; private set; } = new();

    [JsonInclude] public uint NumMaterialEval;
    [JsonInclude] public uint NumMaterialSample;
    [JsonInclude] public uint NumMaterialPdf;
}
