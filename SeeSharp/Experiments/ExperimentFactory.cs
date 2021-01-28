using SeeSharp.Core;
using SeeSharp.Integrators;
using SeeSharp.Core.Image;
using System.Collections.Generic;

namespace SeeSharp.Experiments {
    public abstract class ExperimentFactory {
        public virtual FrameBuffer.Flags FrameBufferFlags => FrameBuffer.Flags.SendToTev;

        public readonly struct Method {
            public readonly string name;
            public readonly Integrator integrator;

            public Method(string name, Integrator integrator) {
                this.name = name;
                this.integrator = integrator;
            }
        }

        public abstract List<Method> MakeMethods();

        public abstract Scene MakeScene();

        public abstract Integrator MakeReferenceIntegrator();
    }
}
