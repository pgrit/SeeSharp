using System;
using System.Numerics;
using GroundWrapper.Geometry;

namespace GroundWrapper
{
    public abstract class Emitter {
        public Mesh Mesh;

        public abstract SurfaceSample SampleArea(Vector2 primary);
        public abstract float PdfArea(SurfacePoint point);
        public abstract (Ray, float) SampleRay(Vector2 primaryPos, Vector2 primaryDir);
        public abstract float PdfRay(SurfacePoint point, Vector3 direction);
        public abstract ColorRGB EmittedRadiance(SurfacePoint point, Vector3 direction);
    }

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

        public override (Ray, float) SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
            var posSample = SampleArea(primaryPos);

            // Transform primary to cosine hemisphere (z is up)
            var local = GroundMath.SampleWrap.ToCosHemisphere(primaryDir);

            // Transform to world space direction
            var normal = posSample.point.ShadingNormal;
            var (tangent, binormal) = GroundMath.SampleWrap.ComputeBasisVectors(normal);
            Vector3 dir = local.direction.Z * normal
                        + local.direction.X * tangent
                        + local.direction.Y * binormal;

            return (
                new Ray { 
                    origin = posSample.point.position, 
                    minDistance = posSample.point.errorOffset, 
                    direction = dir 
                }, 
                local.pdf * posSample.pdf
            );
        }

        ColorRGB radiance;
    }

    //public class Emitter {
    //    public int MeshId { get; }

    //    public int EmitterId { get; }

    //    public Emitter(int meshId, int emitterId) {
    //        MeshId = meshId;
    //        EmitterId = EmitterId;
    //    }

    //    public SurfaceSample WrapPrimaryToSurface(float u, float v) {
    //        return WrapPrimarySampleToEmitterSurface(EmitterId, u, v);
    //    }

    //    public EmitterSample WrapPrimaryToRay(Vector2 primaryPos, Vector2 primaryDir) {
    //        return WrapPrimarySampleToEmitterRay(EmitterId, primaryPos, primaryDir);
    //    }

    //    public float Jacobian(SurfacePoint point) {
    //        Debug.Assert(point.meshId == this.MeshId,
    //            "Attempted to compute the jacobian for the wrong light source.");

    //        return ComputePrimaryToEmitterSurfaceJacobian(ref point);
    //    }

    //    public ColorRGB ComputeEmission(SurfacePoint point, Vector3 outDir) {
    //        Debug.Assert(point.meshId == this.MeshId,
    //            "Attempted to compute emission for the wrong light source.");

    //        return ComputeEmission(ref point, outDir);
    //    }

    //    public float RayJacobian(SurfacePoint origin, Vector3 direction)
    //        => ComputePrimaryToEmitterRayJacobian(origin, direction);

    //    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    //    static extern SurfaceSample WrapPrimarySampleToEmitterSurface(
    //        int emitterId, float u, float v);

    //    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    //    static extern float ComputePrimaryToEmitterSurfaceJacobian(
    //        [In] ref SurfacePoint point);

    //    [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
    //    static extern EmitterSample WrapPrimarySampleToEmitterRay(int emitterId,
    //        Vector2 primaryPos, Vector2 primaryDir);

    //    [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
    //    static extern ColorRGB ComputeEmission([In] ref SurfacePoint point,
    //        Vector3 outDir);

    //    [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
    //    static extern float ComputePrimaryToEmitterRayJacobian(SurfacePoint origin,
    //        Vector3 direction);
    //}
}