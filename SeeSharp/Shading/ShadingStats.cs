using System.Linq;

namespace SeeSharp.Shading;

public struct ShadingStats {
    public ulong NumMaterialEval { get; set; }
    public ulong NumMaterialSample { get; set; }
    public ulong NumMaterialPdf { get; set; }
}

public class ShadingStatCounter {
    static ShadingStatCounter currentCounter = new();

    public static void Reset() {
        currentCounter = new();
    }

    public static ShadingStats Current => new() {
        NumMaterialEval = (ulong)currentCounter.numEval.Values.Sum(v => (long)v),
        NumMaterialSample = (ulong)currentCounter.numSample.Values.Sum(v => (long)v),
        NumMaterialPdf = (ulong)currentCounter.numPdf.Values.Sum(v => (long)v),
    };

    readonly ThreadLocal<ulong> numEval = new(true);
    readonly ThreadLocal<ulong> numSample = new(true);
    readonly ThreadLocal<ulong> numPdf = new(true);

    public static void NotifyEvaluate() => currentCounter.numEval.Value++;

    public static void NotifySample() => currentCounter.numSample.Value++;

    public static void NotifyPdfCompute() => currentCounter.numPdf.Value++;
}
