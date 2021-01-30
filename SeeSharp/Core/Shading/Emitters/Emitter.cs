using SeeSharp.Core.Geometry;
using System.Numerics;

namespace SeeSharp.Core.Shading.Emitters {
    public abstract class Emitter {
        public Mesh Mesh;

        public abstract SurfaceSample SampleArea(Vector2 primary);
        public abstract Vector2 SampleAreaInverse(SurfacePoint point);
        public abstract float PdfArea(SurfacePoint point);
        public abstract EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir);
        public abstract (Vector2, Vector2) SampleRayInverse(SurfacePoint point, Vector3 direction);
        public abstract float PdfRay(SurfacePoint point, Vector3 direction);
        public abstract ColorRGB EmittedRadiance(SurfacePoint point, Vector3 direction);

        public abstract ColorRGB ComputeTotalPower();
    }
}