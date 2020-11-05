using System.Numerics;
using System.Threading.Tasks;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading;

namespace SeeSharp.Integrators.Wavefront {
    public struct BsdfQuery {
        public Vector3 OutDir;
        public Vector3 InDir;
        public SurfacePoint Hit;
        public bool IsOnLightSubpath;
        public RNG Rng;
        public bool IsActive;
    }
}