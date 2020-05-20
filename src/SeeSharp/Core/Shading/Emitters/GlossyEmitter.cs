using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using System;
using System.Numerics;

namespace SeeSharp.Core.Shading.Emitters {
    public class GlossyEmitter : Emitter {
        public GlossyEmitter(Mesh mesh, ColorRGB radiance, float exponent) {
            Mesh = mesh;
            this.radiance = radiance;
            this.exponent = exponent;

            // The total power should be the same as that of a diffuse emitter
            normalizationFactor = (exponent + 1) / (2 * MathF.PI);
        }

        public override ColorRGB EmittedRadiance(SurfacePoint point, Vector3 direction) {
            float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
            if (cosine < 0) return ColorRGB.Black;
            return radiance * MathF.Pow(cosine, exponent) * normalizationFactor;
        }

        public override float PdfArea(SurfacePoint point) => Mesh.Pdf(point);
        public override SurfaceSample SampleArea(Vector2 primary) => Mesh.Sample(primary);

        public override float PdfRay(SurfacePoint point, Vector3 direction) {
            float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
            return PdfArea(point) * SampleWrap.ToCosineLobeJacobian(exponent + 1, cosine);
        }

        public override EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
            var posSample = SampleArea(primaryPos);

            // Transform primary to cosine hemisphere (z is up)
            // We add one to the exponent, to importance sample the cosine term from the jacobian also
            var local = SampleWrap.ToCosineLobe(exponent + 1, primaryDir);

            // Transform to world space direction
            var normal = posSample.point.ShadingNormal;
            var (tangent, binormal) = SampleWrap.ComputeBasisVectors(normal);
            Vector3 dir = local.direction.Z * normal
                        + local.direction.X * tangent
                        + local.direction.Y * binormal;

            float cosine = local.direction.Z;
            var weight = radiance * MathF.Pow(cosine, exponent + 1) * normalizationFactor;

            return new EmitterSample {
                point = posSample.point,
                direction = dir,
                pdf = local.pdf * posSample.pdf,
                weight = weight / posSample.pdf  / local.pdf
            };
        }

        ColorRGB radiance;
        float exponent;
        float normalizationFactor;
    }
}
