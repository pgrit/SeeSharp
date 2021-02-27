using SeeSharp.Geometry;
using System;
using System.Numerics;

namespace SeeSharp.Sampling {
    public static class SampleWarp {
        public static void ComputeBasisVectors(Vector3 normal, out Vector3 tangent, out Vector3 binormal) {
            if (Math.Abs(normal.X) > Math.Abs(normal.Y))
                tangent = new Vector3(-normal.Z, 0.0f, normal.X) / MathF.Sqrt(normal.X * normal.X + normal.Z * normal.Z);
            else
                tangent = new Vector3(0.0f, normal.Z, -normal.Y) / MathF.Sqrt(normal.Y * normal.Y + normal.Z * normal.Z);
            binormal = Vector3.Cross(normal, tangent);
        }

        public static Vector2 ToUniformTriangle(Vector2 primary) {
            float sqrtRnd1 = MathF.Sqrt(primary.X);
            float u = 1.0f - sqrtRnd1;
            float v = primary.Y * sqrtRnd1;
            return new Vector2(u, v);
        }

        public static Vector2 FromUniformTriangle(Vector2 barycentric) {
            if (barycentric.X == 1) return Vector2.Zero; // Prevent NaN
            return new(
                (1 - barycentric.X) * (1 - barycentric.X),
                barycentric.Y / (1 - barycentric.X)
            );
        }

        public static Vector3 SphericalToCartesian(float sintheta, float costheta, float phi) {
            return new Vector3(
                sintheta * MathF.Cos(phi),
                sintheta * MathF.Sin(phi),
                costheta
            );
        }

        public static Vector3 SphericalToCartesian(Vector2 spherical)
        => SphericalToCartesian(MathF.Sin(spherical.Y), MathF.Cos(spherical.Y), spherical.X);

        /// <summary>
        /// Maps the cartensian coordinates (z is up) to spherical coordinates.
        /// </summary>
        /// <param name="dir">Direction in cartesian coordinates</param>
        /// <returns>A vector where X is the longitude (phi) and Y the latitude (theta)</returns>
        public static Vector2 CartesianToSpherical(Vector3 dir) {
            var sp = new Vector2(
                MathF.Atan2(dir.Y, dir.X),
                MathF.Atan2(MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y), dir.Z)
            );
            if (sp.X < 0) sp.X += MathF.PI * 2.0f;
            return sp;
        }

        public struct DirectionSample {
            public Vector3 Direction;
            public float Pdf;
        }

        // Warps the primary sample space on the cosine weighted hemisphere.
        // The hemisphere is centered about the positive "z" axis.
        public static DirectionSample ToCosHemisphere(Vector2 primary) {
            Vector3 localDir = SphericalToCartesian(
                MathF.Sqrt(1 - primary.Y),
                MathF.Sqrt(primary.Y),
                2.0f * MathF.PI * primary.X);

            return new DirectionSample { Direction = localDir, Pdf = localDir.Z / MathF.PI };
        }

        public static Vector2 FromCosHemisphere(Vector3 localDir) {
            var spherical = CartesianToSpherical(localDir);
            float x = spherical.X / (2.0f * MathF.PI);
            float cosTheta = MathF.Cos(spherical.Y);
            float y = cosTheta * cosTheta;
            return new(x, y);
        }

        public static float ToCosHemisphereJacobian(float cosine) {
            return Math.Abs(cosine) / MathF.PI;
        }


        public static DirectionSample ToCosineLobe(float power, Vector2 primary) {
            float phi = MathF.PI * 2.0f * primary.X;
            float cosTheta = MathF.Pow(primary.Y, 1.0f / (power + 1.0f));
            float sinTheta = MathF.Sqrt(1.0f - (cosTheta * cosTheta)); // cosTheta cannot be >= 1

            Vector3 localDir = SphericalToCartesian(sinTheta, cosTheta, phi);

            return new DirectionSample {
                Direction = localDir,
                Pdf = (power + 1.0f) * MathF.Pow(cosTheta, power) * 1.0f / (2.0f * MathF.PI)
            };
        }

        public static Vector2 FromCosineLobe(float power, Vector3 localDir) {
            var spherical = CartesianToSpherical(localDir);
            float x = spherical.X / (2.0f * MathF.PI);
            float cosTheta = MathF.Cos(spherical.Y);
            float y = MathF.Pow(cosTheta, power + 1.0f);
            return new(x, y);
        }

        public static float ToCosineLobeJacobian(float power, float cos) {
            return cos > 0.0f ? ((power + 1.0f) * MathF.Pow(cos, power) * 1.0f / (2.0f * MathF.PI)) : 0.0f;
        }

        public static DirectionSample ToUniformSphere(Vector2 primary) {
            var a = 2 * MathF.PI * primary.Y;
            var b = 2 * MathF.Sqrt(primary.X - primary.X * primary.X);
            return new DirectionSample {
                Direction = SphericalToCartesian(b, 1 - 2 * primary.X, a),
                Pdf = 1 / (4 * MathF.PI)
            };
        }

        public static Vector2 FromUniformSphere(Vector3 dir) {
            var spherical = CartesianToSpherical(Vector3.Normalize(dir));
            var phi = spherical.X;
            var theta = spherical.Y;
            float x = (1 - MathF.Cos(theta)) / 2;
            float y = phi / (2 * MathF.PI);
            return new Vector2(x, y);
        }

        public static float ToUniformSphereJacobian() => 1 / (4 * MathF.PI);

        /// <summary>
        /// Warps a primary sample to a position on the unit disc.
        /// </summary>
        public static Vector2 ToConcentricDisc(Vector2 primary) {
            float phi, r;

            float a = 2 * primary.X - 1;
            float b = 2 * primary.Y - 1;
            if (a > -b) {
                if (a > b) {
                    r = a;
                    phi = (MathF.PI * 0.25f) * (b / a);
                } else {
                    r = b;
                    phi = (MathF.PI * 0.25f) * (2 - (a / b));
                }
            } else {
                if (a < b) {
                    r = -a;
                    phi = (MathF.PI * 0.25f) * (4 + (b / a));
                } else {
                    r = -b;
                    if (b != 0)
                        phi = (MathF.PI * 0.25f) * (6 - (a / b));
                    else
                        phi = 0;
                }
            }

            return new Vector2(r * MathF.Cos(phi), r * MathF.Sin(phi));
        }

        public static Vector2 FromConcentricDisc(Vector2 pos) {
            float r = pos.Length();
            float phi = MathF.Atan2(pos.Y, pos.X);
            phi += phi < -MathF.PI / 4.0f ? 2.0f * MathF.PI : 0.0f;
            float a, b;
            if (phi < MathF.PI / 4.0f) {
                a = r;
                b = phi * a / (MathF.PI / 4.0f);
            } else if (phi < 3.0f * MathF.PI / 4.0f) {
                b = r;
                a = -(phi - MathF.PI / 2.0f) * b / (MathF.PI / 4.0f);
            } else if (phi < 5.0f * MathF.PI / 4.0f) {
                a = -r;
                b = (phi - MathF.PI) * a / (MathF.PI / 4.0f);
            } else {
                b = -r;
                a = -(phi - 3.0f * MathF.PI / 2.0f) * b / (MathF.PI / 4.0f);
            }
            return new((a + 1.0f) / 2.0f, (b + 1.0f) / 2.0f);
        }

        public static float ToConcentricDiscJacobian() {
            return 1.0f / MathF.PI;
        }


        /// <summary>
        /// Computes the inverse jacobian for the mapping from surface area around "to" to the sphere around "from".
        ///
        /// Required for integrals that perform this change of variables (e.g., next event estimation).
        ///
        /// Multiplying solid angle pdfs by this value computes the corresponding surface area density.
        /// Dividing surface area pdfs by this value computes the corresponding solid angle density.
        ///
        /// This function simply computes the cosine formed by the normal at "to" and the direction from "to" to "from".
        /// The absolute value of that cosine is then divided by the squared distance between the two points:
        ///
        /// result = cos(normal_to, from - to) / ||from - to||^2
        /// </summary>
        /// <param name="from">The position at which the hemispherical distribution is defined.</param>
        /// <param name="to">The point on the surface area that is projected onto the hemisphere.</param>
        /// <returns>Inverse jacobian, multiply solid angle densities by this value.</returns>
        public static float SurfaceAreaToSolidAngle(SurfacePoint from, SurfacePoint to) {
            var dir = to.Position - from.Position;
            var distSqr = dir.LengthSquared();
            return MathF.Abs(Vector3.Dot(to.Normal, -dir)) / (distSqr * MathF.Sqrt(distSqr));
        }
    }
}
