namespace SeeSharp.Shading;

public class ShadingStats {
    public static void Reset() {
        Current = new();
    }

    /// <summary>
    /// If set to true, material operations will be counted using atomics.
    /// Increases render time by about 10%, should be disabled for reference rendering.
    /// </summary>
    public static bool Enabled { get; set; } = false;

    public static ShadingStats Current { get; private set; } = new();

    public uint NumMaterialEval => numEval;
    public uint NumMaterialSample => numSample;
    public uint NumMaterialPdf => numPdf;

    uint numEval;
    uint numSample;
    uint numPdf;

    public static void NotifyEvaluate() {
        if (Enabled) Interlocked.Increment(ref Current.numEval);
    }

    public static void NotifySample() {
        if (Enabled) Interlocked.Increment(ref Current.numSample);
    }

    public static void NotifyPdfCompute() {
        if (Enabled) Interlocked.Increment(ref Current.numPdf);
    }
}
