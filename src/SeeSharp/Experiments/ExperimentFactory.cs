using SeeSharp.Core;
using SeeSharp.Integrators;
using System.Collections.Generic;

namespace SeeSharp.Experiments {
    public abstract class ExperimentFactory {
        public abstract Dictionary<string, Integrator> MakeMethods();
        public abstract Scene MakeScene();
        public abstract Integrator MakeReferenceIntegrator();
    }
}
