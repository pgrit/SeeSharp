using SeeSharp.Core;
using SeeSharp.Integrators;
using System.Collections.Generic;

namespace SeeSharp.Experiments {
    public abstract class ExperimentFactory {
        public virtual FrameBuffer.Flags FrameBufferFlags => FrameBuffer.Flags.None;

        public readonly struct Method {
            public readonly string name;
            public readonly Integrator integrator;
            public readonly List<string> files;

            public Method(string name, Integrator integrator, List<string> files) {
                this.name = name;
                this.integrator = integrator;
                this.files = files;
            }
        }

        public abstract List<Method> MakeMethods();

        public abstract Scene MakeScene();

        public abstract Integrator MakeReferenceIntegrator();
    }
}
