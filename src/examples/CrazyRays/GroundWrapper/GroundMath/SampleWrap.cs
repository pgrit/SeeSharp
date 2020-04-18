using System;
using System.Numerics;

namespace GroundWrapper.GroundMath {
    public static class SampleWrap {
        public static (Vector3, Vector3) ComputeBasisVectors(Vector3 normal) {
            int   id0 = (Math.Abs(normal.X) > Math.Abs(normal.Y)) ?     0 : 1;
            int   id1 = (Math.Abs(normal.X) > Math.Abs(normal.Y)) ?     1 : 0;
            float sig = (Math.Abs(normal.X) > Math.Abs(normal.Y)) ? -1.0f : 1.0f;
            
            ref float GetByIdx(ref Vector3 v, int idx) {
                if (idx == 0) return ref v.X;
                else if (idx == 1) return ref v.Y;
                else return ref v.Z;
            }

            float invLen = 1.0f / MathF.Sqrt(GetByIdx(ref normal, id0) * GetByIdx(ref normal, id0) + normal.Z * normal.Z);

            var tangentOut = new Vector3();
            GetByIdx(ref tangentOut, id0) = normal.Z * sig * invLen;
            GetByIdx(ref tangentOut, id1) = 0.0f;
            tangentOut.Z = GetByIdx(ref normal, id0) * -1.0f * sig * invLen;

            var binormalOut = Vector3.Cross(normal, tangentOut);

            return (Vector3.Normalize(tangentOut), Vector3.Normalize(binormalOut));
        }

        public static Vector2 ToUniformTriangle(Vector2 primary) {
            float sqrtRnd1 = MathF.Sqrt(primary.X);
            float u = 1.0f - sqrtRnd1;
            float v = primary.Y * sqrtRnd1;
            return new Vector2(u, v);
        }

        public static Vector3 SphericalToCartesian(float sintheta, float costheta, float phi) {
            return new Vector3(
                sintheta * MathF.Cos(phi),
                sintheta * MathF.Sin(phi),
                costheta
            );
        }

        public static Vector2 CartesianToSpherical(Vector3 dir) {
            var sp = new Vector2(
                MathF.Atan2(dir.Y, dir.X),
                MathF.Atan2(MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y), dir.Z)
            );
            if (sp.X < 0) sp.X += MathF.PI * 2.0f;
            return sp;
        }

        public struct DirectionSample {
            public Vector3 direction;
            public float pdf;
        }

        // Wraps the primary sample space on the cosine weighted hemisphere.
        // The hemisphere is centered about the positive "z" axis.
        public static DirectionSample ToCosHemisphere(Vector2 primary) {
            Vector3 local_dir = SphericalToCartesian(
                MathF.Sqrt(1 - primary.Y),
                MathF.Sqrt(primary.Y),
                2.0f * MathF.PI * primary.X);

            return new DirectionSample { direction = local_dir, pdf = local_dir.Z / MathF.PI };
        }

        public static float ToCosHemisphereJacobian(float cosine) {
            return Math.Abs(cosine) / MathF.PI;
        }
    }
}
