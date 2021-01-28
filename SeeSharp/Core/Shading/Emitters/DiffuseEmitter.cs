using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;
using System;
using System.Numerics;

namespace SeeSharp.Core.Shading.Emitters {
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
            var (tangent, binormal) = SampleWarp.ComputeBasisVectors(normal);
            Vector3 dir = local.Direction.Z * normal
                        + local.Direction.X * tangent
                        + local.Direction.Y * binormal;

            return new EmitterSample {
                Point = posSample.Point,
                Direction = dir,
                Pdf = local.Pdf * posSample.Pdf,
                Weight = radiance / posSample.Pdf * MathF.PI // cosine cancels out with the directional pdf
            };
        }

        public override (Vector2, Vector2) SampleRayInverse(SurfacePoint point, Vector3 direction) {
            var posPrimary = SampleAreaInverse(point);

            // Transform from world space to sampling space
            var normal = point.ShadingNormal;
            var (tangent, binormal) = SampleWarp.ComputeBasisVectors(normal);
            float z = Vector3.Dot(normal, direction);
            float x = Vector3.Dot(tangent, direction);
            float y = Vector3.Dot(binormal, direction);

            var dirPrimary = SampleWarp.FromCosHemisphere(new(x, y, z));
            return (posPrimary, dirPrimary);
        }

        ColorRGB radiance;
    }
}