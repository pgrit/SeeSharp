using System.Numerics;
using GroundWrapper.Geometry;

namespace GroundWrapper
{
    public abstract class Emitter {
        public Mesh Mesh;

        public abstract SurfaceSample SampleArea(Vector2 primary);
        public abstract float PdfArea(SurfacePoint hit);
        public abstract Ray SampleRay(Vector2 primaryPos, Vector2 primaryDir);
        public abstract float PdfRay(SurfacePoint hit, Vector3 direction);
        public abstract ColorRGB EmittedRadiance(SurfacePoint hit, Vector3 direction);
    }

    public class DiffuseEmitter : Emitter {
        public DiffuseEmitter(Mesh mesh, ColorRGB radiance) {
            Mesh = mesh;
            this.radiance = radiance;
        }

        public override ColorRGB EmittedRadiance(SurfacePoint hit, Vector3 direction) => radiance;
        public override float PdfArea(SurfacePoint hit) => throw new System.NotImplementedException();
        public override float PdfRay(SurfacePoint hit, Vector3 direction) => throw new System.NotImplementedException();
        public override SurfaceSample SampleArea(Vector2 primary) => throw new System.NotImplementedException();
        public override Ray SampleRay(Vector2 primaryPos, Vector2 primaryDir) => throw new System.NotImplementedException();

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