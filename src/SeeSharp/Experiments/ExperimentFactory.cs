using GroundWrapper;
using Integrators;
using System.Collections.Generic;

namespace Experiments {
    public abstract class ExperimentFactory {
        public abstract Dictionary<string, Integrator> MakeMethods();
        public abstract Scene MakeScene();
        public abstract Integrator MakeReferenceIntegrator();
    }
}
