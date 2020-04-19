using System;
using System.Numerics;
using GroundWrapper.Geometry;

namespace GroundWrapper {
    public class DiffuseEmitter : Emitter {
        public DiffuseEmitter(Mesh mesh, ColorRGB radiance) {
            Mesh = mesh;
            this.radiance = radiance;
        }

        public override ColorRGB EmittedRadiance(SurfacePoint point, Vector3 direction) {
            if (Vector3.Dot(point.ShadingNormal, direction) < 0)
                return ColorRGB.Black;
            return radiance;
        }

        public override float PdfArea(SurfacePoint point) => Mesh.Pdf(point);
        public override SurfaceSample SampleArea(Vector2 primary) => Mesh.Sample(primary);

        public override float PdfRay(SurfacePoint point, Vector3 direction) {
            float cosine = Vector3.Dot(point.ShadingNormal, direction) / direction.Length();
            return PdfArea(point) * MathF.Max(cosine, 0) / MathF.PI;
        }

        public override EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
            var posSample = SampleArea(primaryPos);

            // Transform primary to cosine hemisphere (z is up)
            var local = GroundMath.SampleWrap.ToCosHemisphere(primaryDir);

            // Transform to world space direction
            var normal = posSample.point.ShadingNormal;
            var (tangent, binormal) = GroundMath.SampleWrap.ComputeBasisVectors(normal);
            Vector3 dir = local.direction.Z * normal
                        + local.direction.X * tangent
                        + local.direction.Y * binormal;

            return new EmitterSample {
                point = posSample.point,
                direction = dir,
                pdf = local.pdf * posSample.pdf,
                weight = radiance / posSample.pdf * MathF.PI // cosine cancels out with the directional pdf
            };
        }

        ColorRGB radiance;
    }
}