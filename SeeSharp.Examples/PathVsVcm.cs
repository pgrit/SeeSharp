namespace SeeSharp.Examples;

/// <summary>
/// Renders a scene with a path tracer and with VCM.
/// </summary>
class PathVsVcm : Experiment {
    public override List<Method> MakeMethods() => [
        new("PathTracer", new PathTracer() { NumIterations = 4 }),
        new("Vcm", new VertexConnectionAndMerging() { NumIterations = 2 })
    ];
}