using System.Runtime.InteropServices;

namespace Ground
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3 {
        public float x, y, z;

        public static Vector3 operator +(Vector3 a, Vector3 b)
        => new Vector3 {x = a.x + b.x, y = a.y + b.y, z = a.z + b.z};

        public static Vector3 operator -(Vector3 a)
        => new Vector3 {x = -a.x, y = -a.y, z = -a.z};

        public static Vector3 operator -(Vector3 a, Vector3 b)
        => a + (-b);

        public static float Dot(Vector3 a, Vector3 b)
        => a.x * b.x + a.y * b.y + a.z * b.z;

        public float LengthSquared() => Dot(this, this);

        public float Length() => (float)System.Math.Sqrt(LengthSquared());
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector2 {
        public float x, y;
    }

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
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Hit {
        public SurfacePoint point;
        public float distance;
        public float errorOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SurfaceSample {
        public SurfacePoint point;
        public float jacobian;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BsdfSample {
        public Vector3 direction;
        public float jacobian;
        public float reverseJacobian;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ColorRGB {
        public float r, g, b;

        public static ColorRGB operator *(ColorRGB a, ColorRGB b)
        => new ColorRGB {r = a.r * b.r, g = a.g * b.g, b = a.b * b.b};

        public static ColorRGB operator *(ColorRGB a, float b)
        => new ColorRGB {r = a.r * b, g = a.g * b, b = a.b * b};

        public static ColorRGB operator *(float a, ColorRGB b)
        => b * a;

        public static ColorRGB operator +(ColorRGB a, ColorRGB b)
        => new ColorRGB {r = a.r + b.r, g = a.g + b.g, b = a.b + b.b};

        public static ColorRGB Black =
            new ColorRGB { r = 0.0f, g = 0.0f, b = 0.0f };

        public static ColorRGB White =
            new ColorRGB { r = 1.0f, g = 1.0f, b = 1.0f };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UberShaderParams {
        public int baseColorTexture;
        public int emissionTexture;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathVertex {
        public SurfacePoint point; // TODO could be a "CompressedSurfacePoint"

        // Surface area pdf to sample this vertex from the previous one,
        // i.e., the actual density this vertex was sampled from
        public float pdfFromAncestor;

        // Surface area pdf to sample the previous vertex from this one,
        // i.e., the reverse direction of the path.
        public float pdfToAncestor;

        public ColorRGB weight; // TODO support other spectral resolutions
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
}