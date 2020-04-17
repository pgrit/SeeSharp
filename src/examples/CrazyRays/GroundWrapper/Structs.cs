using System.Runtime.InteropServices;
using System.Numerics;

namespace GroundWrapper
{

    

    [StructLayout(LayoutKind.Sequential)]
    public struct UberShaderParams {
        public int baseColorTexture;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CameraSampleInfo {
        public Vector2 filmSample;
        public Vector2 lensSample;
        public float time;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GeometryTerms {
        public float cosineFrom;
        public float cosineTo;
        public float squaredDistance;
        public float geomTerm;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct EmitterSample
    {
        public SurfaceSample surface;
        public Vector3 direction;
        public float jacobian;
        public float shadingCosine;
    };
}