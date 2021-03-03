using SeeSharp.Integrators;
using System.Collections.Generic;

namespace SeeSharp.Experiments {
    /// <summary>
    /// Describes an experiment with a list of named integrators.
    /// </summary>
    public abstract class Experiment {
        public readonly struct Method {
            public readonly string name;
            public readonly Integrator integrator;

            public Method(string name, Integrator integrator) {
                this.name = name;
                this.integrator = integrator;
            }
        }

        public abstract List<Method> MakeMethods();
    }
}
