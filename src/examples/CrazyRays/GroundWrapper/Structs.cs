using System.Runtime.InteropServices;
using GroundWrapper.GroundMath;

namespace GroundWrapper
{

    [StructLayout(LayoutKind.Sequential)]
    public struct Ray {
        public Vector3 origin;
        public Vector3 direction;
        public float minDistance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SurfacePoint {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 barycentricCoords;
        public uint meshId;
        public uint primId;
        public float errorOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Hit {
        public SurfacePoint point;
        public float distance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SurfaceSample {
        public SurfacePoint point;
        public float jacobian;
    }

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