using SeeSharp.Geometry;
using SeeSharp.Sampling;
using SimpleImageIO;
using System;
using System.Numerics;

namespace SeeSharp.Shading.Emitters {
    /// <summary>
    /// Emits a constant amount of radiance in all directions of the upper hemisphere, defined by the
    /// shading normal of the emissive mesh surface.
    /// </summary>
    public class DiffuseEmitter : Emitter {
        /// <param name="mesh">The emissive mesh</param>
        /// <param name="radiance">Emitted radiance in all directions</param>
        public DiffuseEmitter(Mesh mesh, RgbColor radiance) {
            Mesh = mesh;
            Radiance = radiance;
        }

        public override RgbColor EmittedRadiance(SurfacePoint point, Vector3 direction) {
            if (Vector3.Dot(point.ShadingNormal, direction) < 0)
                return RgbColor.Black;
            return Radiance;
        }

        public override float PdfArea(SurfacePoint point) => Mesh.Pdf(point);
        public override SurfaceSample SampleArea(Vector2 primary) => Mesh.Sample(primary);
        public override Vector2 SampleAreaInverse(SurfacePoint point) => Mesh.SampleInverse(point);

        public override float PdfRay(SurfacePoint point, Vector3 direction) {
            float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
            return PdfArea(point) * MathF.Max(cosine, 0) / MathF.PI;
        }

        public override EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
            var posSample = SampleArea(primaryPos);

            // Transform primary to cosine hemisphere (z is up)
            var local = SampleWarp.ToCosHemisphere(primaryDir);

            // Transform to world space direction
            var normal = posSample.Point.ShadingNormal;
            Vector3 tangent, binormal;
            SampleWarp.ComputeBasisVectors(normal, out tangent, out binormal);
            Vector3 dir = local.Direction.Z * normal
                        + local.Direction.X * tangent
                        + local.Direction.Y * binormal;

            return new EmitterSample {
                Point = posSample.Point,
                Direction = dir,
                Pdf = local.Pdf * posSample.Pdf,
                Weight = Radiance / posSample.Pdf * MathF.PI // cosine cancels out with the directional pdf
            };
        }

        public override (Vector2, Vector2) SampleRayInverse(SurfacePoint point, Vector3 direction) {
            var posPrimary = SampleAreaInverse(point);

            // Transform from world space to sampling space
            var normal = point.ShadingNormal;
            Vector3 tangent, binormal;
            SampleWarp.ComputeBasisVectors(normal, out tangent, out binormal);
            float z = Vector3.Dot(normal, direction);
            float x = Vector3.Dot(tangent, direction);
            float y = Vector3.Dot(binormal, direction);

            var dirPrimary = SampleWarp.FromCosHemisphere(new(x, y, z));
            return (posPrimary, dirPrimary);
        }

        public override RgbColor ComputeTotalPower()
        => Radiance * 2.0f * MathF.PI * Mesh.SurfaceArea;

        /// <summary>
        /// The radiance that is emitted in all directions
        /// </summary>
        public readonly RgbColor Radiance;
    }
}