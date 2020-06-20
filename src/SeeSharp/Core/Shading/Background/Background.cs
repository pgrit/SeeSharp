using System.Numerics;
using SeeSharp.Core.Geometry;

namespace SeeSharp.Core.Shading.Background {
    /// <summary>
    /// Base class for all sorts of sky models, image based lighting, etc.
    /// </summary>
    public abstract class Background {
        /// <summary>
        /// Computes the emitted radiance in a given direction. All backgrounds are invariant with respect to the position.
        /// </summary>
        public abstract ColorRGB EmittedRadiance(Vector3 direction);

        public abstract BackgroundSample SampleDirection(Vector2 primary);
        public abstract float DirectionPdf(Vector3 Direction);
        public abstract void SampleRay(Vector2 primaryPos, Vector2 primaryDir);
        public abstract float RayPdf(SurfacePoint point, Vector3 direction);

        public Vector3 SceneCenter;
        public float SceneRadius;
    }
}