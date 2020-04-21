using GroundWrapper.Geometry;
using System.Numerics;

namespace GroundWrapper.Shading.Emitters {
    public struct EmitterSample {
        public SurfacePoint point;
        public Vector3 direction;
        public float pdf;

        // Sample weight for an MC estimate of the total emitted power
        public ColorRGB weight;
    }
}