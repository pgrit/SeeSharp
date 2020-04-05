using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ground
{
    public class Emitter {
        public int MeshId {
            get => this.id;
        }

        public Emitter(int id) {
            this.id = id;
        }

        public SurfaceSample WrapPrimaryToSurface(float u, float v) {
            return WrapPrimarySampleToSurface(this.id, u, v);
        }

        public Ray WrapPrimaryToRay(Vector2 primaryPos, Vector2 primaryDir) {
            return new Ray {};
        }

        public float Jacobian(SurfacePoint point) {
            Debug.Assert(point.meshId == this.id,
                "Attempted to compute the jacobian for the wrong light source.");

            return ComputePrimaryToSurfaceJacobian(ref point);
        }

        public ColorRGB ComputeEmission(SurfacePoint point, Vector3 outDir) {
            Debug.Assert(point.meshId == this.id,
                "Attempted to compute emission for the wrong light source.");

            return ComputeEmission(ref point, outDir);
        }

        private int id;

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern SurfaceSample WrapPrimarySampleToSurface(
            int meshId, float u, float v);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern float ComputePrimaryToSurfaceJacobian(
            [In] ref SurfacePoint point);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern ColorRGB ComputeEmission([In] ref SurfacePoint point,
            Vector3 outDir);
    }
}