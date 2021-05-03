using System.Numerics;
using SeeSharp.Geometry;
using SeeSharp.Shading.Bsdfs;
using SimpleImageIO;

namespace SeeSharp.Shading.Materials {
    public abstract class Material {
        /// <summary>
        /// Computes the surface roughness, a value between 0 and 1.
        /// 0 is perfectly specular, 1 is perfectly diffuse.
        /// The exact value can differ between materials, as
        /// this is not a well-defined quantity from a physical point of view.
        /// </summary>
        public abstract float GetRoughness(SurfacePoint hit);

        /// <summary>
        /// Computes the ratio of exterior (hemisphere of the outgoing direction) and interior (hemisphere
        /// of the incoming direction) index of refraction at a given surface point.
        /// </summary>
        /// <param name="hit">The query point in case the material is spatially varying</param>
        /// <param name="outDir">The outgoing direction (towards where the path is coming from)</param>
        /// <param name="inDir">The incoming direction (towards where we continue next)</param>
        /// <returns>(interior IOR / exterior IOR) at the query point</returns>
        public abstract float GetIndexOfRefractionRatio(SurfacePoint hit, Vector3 outDir, Vector3 inDir);

        /// <summary>
        /// Computes the sum of reflectance and transmittance.
        /// Can be an approximation, with the accuracy depending on the material.
        /// </summary>
        public abstract RgbColor GetScatterStrength(SurfacePoint hit);

        public abstract RgbColor Evaluate(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

        public virtual RgbColor EvaluateWithCosine(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            var bsdf = Evaluate(hit, outDir, inDir, isOnLightSubpath);
            inDir = ShadingSpace.WorldToShading(hit.ShadingNormal, inDir);
            return bsdf * ShadingSpace.AbsCosTheta(inDir);
        }

        public abstract BsdfSample Sample(SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample);

        public abstract (float, float) Pdf(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

        /// <summary>
        /// Tests whether the incoming and outgoing direction are on the same or different sides of the
        /// actual geometry, based on the actual normal, not the shading normal.
        /// The directions do not have to be normalized.
        /// </summary>
        /// <param name="hit">The surface point</param>
        /// <param name="outDir">Outgoing direction in world space, away from the surface</param>
        /// <param name="inDir">Incoming direction in world space, away from the surface</param>
        /// <returns>True, if they are on the same side, i.e., only reflection should be evaluated.</returns>
        public bool ShouldReflect(SurfacePoint hit, Vector3 outDir, Vector3 inDir) {
            // Prevent light leaks based on the actual geometric normal
            float geoCosOut = Vector3.Dot(outDir, hit.Normal);
            float geoCosIn = Vector3.Dot(inDir, hit.Normal);
            if (geoCosIn * geoCosOut >= 0)
                return true;
            return false;
        }
    }
}
