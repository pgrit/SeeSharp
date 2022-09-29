using SeeSharp.Experiments;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;
using System.Collections.Generic;

namespace SeeSharp.Examples;

/// <summary>
/// Renders a scene with a path tracer and with VCM.
/// </summary>
class PathVsVcm : Experiment {
    public override List<Method> MakeMethods() => new() {
        new("PathTracer", new PathTracer() { MaxDepth = 5, TotalSpp = 4 }),
        new("Vcm", new VertexConnectionAndMerging() { MaxDepth = 5, NumIterations = 2 })
    };
}