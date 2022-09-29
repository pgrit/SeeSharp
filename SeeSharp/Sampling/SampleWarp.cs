using static SeeSharp.Common.MathUtils;

namespace SeeSharp.Sampling;

/// <summary>
/// Offers functions to transform uniform samples to other distributions or spaces.
/// </summary>
public static class SampleWarp {
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

    /// <summary>
    /// Transforms a primary sample to the spherical triangle formed by three direction vectors.
    /// From "Stratified Sampling of Spherical Triangles", Arvo 1995, 10.1145/218380.218500
    /// </summary>
    /// <param name="a">First triangle vertex projected onto the sphere</param>
    /// <param name="b">Second triangle vertex projected onto the sphere</param>
    /// <param name="c">Third triangle vertex projected onto the sphere</param>
    /// <param name="primary">Primary sample</param>
    public static DirectionSample ToSphericalTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 primary) {
        // Compute the angles at each corner
        var nAB = Vector3.Normalize(Vector3.Cross(a, b));
        var nBC = Vector3.Normalize(Vector3.Cross(b, c));
        var nCA = Vector3.Normalize(Vector3.Cross(c, a));
        float alpha = AngleBetween(nAB, -nCA);
        float beta = AngleBetween(nBC, -nAB);
        float gamma = AngleBetween(nCA, -nBC);

        float cosAlpha = MathF.Cos(alpha);
        float sinAlpha = MathF.Sin(alpha);

        // Cosine of the arc length of the curved line between v1 and v2
        float cosLenC = (MathF.Cos(gamma) + MathF.Cos(beta) * cosAlpha) / (MathF.Sin(beta) * sinAlpha);

        Debug.Assert(float.IsFinite(cosLenC));

        float area = alpha + beta + gamma - MathF.PI;

        // Randomly select a sub-triangle
        float sampleA = primary.X * area;

        float s = MathF.Sin(sampleA - alpha);
        float t = MathF.Cos(sampleA - alpha);

        float u = t - cosAlpha;
        float v = s + sinAlpha * cosLenC;

        float num = (v * t - u * s) * cosAlpha - v;
        float denom = (v * s + u * t) * sinAlpha;
        float q = num / denom;

        // Prevent NaN from numerical precision error
        Debug.Assert(q < 1.1f);
        q = Math.Min(q, 1.0f);

        Vector3 NormComp(Vector3 x, Vector3 y) => Vector3.Normalize(x - Vector3.Dot(x, y) * y);

        var sampleC = q * a + MathF.Sqrt(1 - q * q) * NormComp(c, a);

        float z = 1 - primary.Y * (1 - Vector3.Dot(sampleC, b));

        // Prevent NaN from numerical precision error
        Debug.Assert(z < 1.1f);
        z = Math.Min(z, 1.0f);

        var dir = z * b + MathF.Sqrt(1 - z * z) * NormComp(sampleC, b);

        Debug.Assert(float.IsFinite(dir.X));
        Debug.Assert(float.IsFinite(dir.Y));
        Debug.Assert(float.IsFinite(dir.Z));

        return new() {
            Direction = dir,
            Pdf = 1.0f / area
        };
    }

    public static Vector3 SphericalToCartesian(float sintheta, float costheta, float phi) {
        return new Vector3(
            sintheta * MathF.Cos(phi),
            sintheta * MathF.Sin(phi),
            costheta
        );
    }

    /// <summary>
    /// Converts a direction vector from spherical coordinates to cartesian coordinates.
    /// </summary>
    /// <param name="spherical">A vector where X is the longitude (phi) and Y the latitude (theta)</param>
    /// <returns>Cartesian coordinates in a right handed system (z is up)</returns>
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
    public static float SurfaceAreaToSolidAngle(in SurfacePoint from, in SurfacePoint to) {
        var dir = to.Position - from.Position;
        var distSqr = dir.LengthSquared();
        return MathF.Abs(Vector3.Dot(to.Normal, -dir)) / (distSqr * MathF.Sqrt(distSqr));
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
    public static float SurfaceAreaToSolidAngle(Vector3 from, in SurfacePoint to) {
        var dir = to.Position - from;
        var distSqr = dir.LengthSquared();
        return MathF.Abs(Vector3.Dot(to.Normal, -dir)) / (distSqr * MathF.Sqrt(distSqr));
    }
}
