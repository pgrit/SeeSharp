using SeeSharp.Experiments;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;
using System.Collections.Generic;

namespace SeeSharp.Examples {
    /// <summary>
    /// Configures the experiment we want to run: test scene, maximum depth,
    /// best reference integrator, which methods to compare.
    /// </summary>
    class PathVsVcm : ExperimentFactory {
        // Specify the scene and how to render references
        public override Scene MakeScene() => Scene.LoadFromFile("Data/Scenes/cbox.json");
        public override Integrator MakeReferenceIntegrator()
        => new PathTracer { MaxDepth = 5, TotalSpp = 128 };

        // Specify the methods (named integrators) to compare
        public override List<Method> MakeMethods() => new() {
            new("PathTracer", new PathTracer() { MaxDepth = 5, TotalSpp = 4 }),
            new("Vcm", new VertexConnectionAndMerging() { MaxDepth = 5, NumIterations = 2 })
        };
    }
}