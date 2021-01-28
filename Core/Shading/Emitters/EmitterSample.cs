using SeeSharp.Core.Geometry;
using System.Numerics;

namespace SeeSharp.Core.Shading.Emitters {
    public struct EmitterSample {
        public SurfacePoint Point;
        public Vector3 Direction;
        public float Pdf;

        // Sample weight for an MC estimate of the total emitted power
        public ColorRGB Weight;
    }
}