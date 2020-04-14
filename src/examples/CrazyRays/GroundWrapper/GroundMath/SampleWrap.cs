using System;

namespace GroundWrapper.GroundMath {
    public static class SampleWrap {
        public static (Vector3, Vector3) ComputeBasisVectors(Vector3 normal) {
            int   id0 = (Math.Abs(normal.x) > Math.Abs(normal.y)) ?     0 : 1;
            int   id1 = (Math.Abs(normal.x) > Math.Abs(normal.y)) ?     1 : 0;
            float sig = (Math.Abs(normal.x) > Math.Abs(normal.y)) ? -1.0f : 1.0f;

            float invLen = 1.0f / MathF.Sqrt(normal[id0] * normal[id0] + normal.z * normal.z);

            var tangentOut  = new Vector3();
            tangentOut[id0] = normal.z * sig * invLen;
            tangentOut[id1] = 0.0f;
            tangentOut.z    = normal[id0] * -1.0f * sig * invLen;

            var binormalOut = Vector3.Cross(normal, tangentOut);

            return (tangentOut.Normalized(), binormalOut.Normalized());
        }

        public static Vector2 ToUniformTriangle(Vector2 primary) {
            float sqrtRnd1 = MathF.Sqrt(primary.x);
            float u = 1.0f - sqrtRnd1;
            float v = primary.y * sqrtRnd1;
            return new Vector2(u, v);
        }

        public static Vector3 SphericalToCartesian(float sintheta, float costheta, float phi) {
            return new Vector3(
                sintheta * MathF.Cos(phi),
                sintheta * MathF.Sin(phi),
                costheta
            );
        }

        public struct DirectionSample {
            public Vector3 direction;
            public float jacobian;
        }

        // Wraps the primary sample space on the cosine weighted hemisphere.
        // The hemisphere is centered about the positive "z" axis.
        public static DirectionSample ToCosHemisphere(Vector2 primary) {
            Vector3 local_dir = SphericalToCartesian(
                MathF.Sqrt(1 - primary.y),
                MathF.Sqrt(primary.y),
                2.0f * MathF.PI * primary.x);

            return new DirectionSample { direction = local_dir, jacobian = local_dir.z / MathF.PI };
        }

        public static float ToCosHemisphereJacobian(float cosine) {
            return Math.Abs(cosine) / MathF.PI;
        }
    }
}
