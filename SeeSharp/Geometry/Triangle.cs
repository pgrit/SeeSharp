namespace SeeSharp.Geometry;

/// <summary>
/// Represents a single triangle inside a mesh. This is a view on a <see cref="Mesh"/> object, any
/// modifications to the mesh will be reflected. The view becomes invalid if the original mesh is modified.
/// </summary>
public struct Triangle {
    public Mesh Mesh { get; init; }
    public int FaceIndex { get; init; }

    float invSurfaceArea;
    Vector3 v1, v2, v3;

    const float MinSolidAngle = 2e-2f;
    const float MaxSolidAngle = 5f;

    /// <summary>
    /// Creates a new view for a triangle inside an existing mesh
    /// </summary>
    /// <param name="mesh">The mesh that contains this triangle</param>
    /// <param name="faceIndex">0-based index of the triangle within the mesh</param>
    public Triangle(Mesh mesh, int faceIndex) {
        Mesh = mesh;
        FaceIndex = faceIndex;

        v1 = Mesh.Vertices[Mesh.Indices[faceIndex * 3 + 0]];
        v2 = Mesh.Vertices[Mesh.Indices[faceIndex * 3 + 1]];
        v3 = Mesh.Vertices[Mesh.Indices[faceIndex * 3 + 2]];
        Vector3 n = Vector3.Cross(v2 - v1, v3 - v1);
        float area = n.Length() * 0.5f;

        if (area == 0.0f)
            throw new ArgumentException("Zero-area triangles cannot be emitters");

        invSurfaceArea = 1.0f / area;
    }

    /// <summary>
    /// Transforms a primary sample to a uniform distribution over the triangle area.
    /// </summary>
    /// <param name="primary">A uniform sample in [0,1]x[0,1]</param>
    public SurfaceSample SampleUniformArea(Vector2 primary) {
        var barycentric = SampleWarp.ToUniformTriangle(primary);
        return new SurfaceSample {
            Point = new SurfacePoint {
                BarycentricCoords = barycentric,
                PrimId = (uint)FaceIndex,
                Normal = Mesh.FaceNormals[FaceIndex],
                Position = Mesh.ComputePosition(FaceIndex, barycentric),
                ErrorOffset = Mesh.ComputeErrorOffset(FaceIndex, barycentric),
                Mesh = Mesh
            },
            Pdf = invSurfaceArea
        };
    }

    /// <summary>
    /// Performs the inverse transformation that is done by <see cref="SampleUniformArea"/>
    /// </summary>
    /// <param name="point">A point on a surface of the emissive mesh</param>
    /// <returns>The uniform sample in [0,1]x[0,1]</returns>
    public Vector2 SampleUniformAreaInverse(in SurfacePoint point)
    => SampleWarp.FromUniformTriangle(point.BarycentricCoords);

    /// <param name="point">A point on the surface of the triangle</param>
    /// <returns>The PDF of sampling this point</returns>
    public float PdfUniformArea(in SurfacePoint point) => invSurfaceArea;

    /// <summary>
    /// Computes the solid angle of the projection of this triangle onto the unit sphere around a given point.
    /// </summary>
    public float ComputeSolidAngle(Vector3 p) {
        var a = Vector3.Normalize(v1 - p);
        var b = Vector3.Normalize(v2 - p);
        var c = Vector3.Normalize(v3 - p);

        return Math.Abs(2 * MathF.Atan2(
            Vector3.Dot(a, Vector3.Cross(b, c)),
            1 + Vector3.Dot(a, b) + Vector3.Dot(a, c) + Vector3.Dot(b, c)
        ));
    }

    /// <summary>
    /// Transforms a primary sample to the projection of the triangle onto the sphere of directions
    /// around a point.
    /// </summary>
    /// <param name="observerPosition">The point onto which we project this triangle</param>
    /// <param name="primary">A uniform sample in [0,1]x[0,1]</param>
    public SurfaceSample SampleSolidAngle(Vector3 observerPosition, Vector2 primary) {
        var a = Vector3.Normalize(v1 - observerPosition);
        var b = Vector3.Normalize(v2 - observerPosition);
        var c = Vector3.Normalize(v3 - observerPosition);

        float solidAngle = Math.Abs(2 * MathF.Atan2(
            Vector3.Dot(a, Vector3.Cross(b, c)),
            1 + Vector3.Dot(a, b) + Vector3.Dot(a, c) + Vector3.Dot(b, c)
        ));

        if (solidAngle < MinSolidAngle || solidAngle > MaxSolidAngle)
            return SampleUniformArea(primary);

        var dir = SampleWarp.ToSphericalTriangle(a, b, c, primary);

        // Compute the barycentric coordinates on the original triangle
        var e1 = v2 - v1;
        var e2 = v3 - v1;
        var s1 = Vector3.Cross(dir.Direction, e2);
        float divisor = Vector3.Dot(s1, e1);
        float invDivisor = 1 / divisor;

        Debug.Assert(divisor != 0);

        Vector3 s = observerPosition - v1;
        float b1 = Vector3.Dot(s, s1) * invDivisor;
        float b2 = Vector3.Dot(dir.Direction, Vector3.Cross(s, e1)) * invDivisor;

        b1 = Math.Clamp(b1, 0, 1);
        b2 = Math.Clamp(b2, 0, 1);
        if (b1 + b2 > 1) {
            float tmp = b1 + b2;
            b1 /= tmp;
            b2 /= tmp;
        }

        Vector2 barycentric = new(b1, b2);
        var point = new SurfacePoint {
            BarycentricCoords = barycentric,
            PrimId = (uint)FaceIndex,
            Normal = Mesh.FaceNormals[FaceIndex],
            Position = Mesh.ComputePosition(FaceIndex, barycentric),
            ErrorOffset = Mesh.ComputeErrorOffset(FaceIndex, barycentric),
            Mesh = Mesh
        };
        float jacobian = SampleWarp.SurfaceAreaToSolidAngle(observerPosition, point);

        return new SurfaceSample {
            Point = point,
            Pdf = jacobian / solidAngle
        };
    }

    /// <summary>
    /// Performs the inverse transformation that is done by <see cref="SampleSolidAngle"/>
    /// </summary>
    /// <param name="observerPosition">The point onto which we project this triangle</param>
    /// <param name="point">A point on a surface of the emissive mesh</param>
    /// <returns>The uniform sample in [0,1]x[0,1]</returns>
    public Vector2 SampleSolidAngleInverse(Vector3 observerPosition, in SurfacePoint point) {
        float solidAngle = ComputeSolidAngle(observerPosition);
        if (solidAngle < MinSolidAngle || solidAngle > MaxSolidAngle)
            return SampleUniformAreaInverse(point);

        throw new NotImplementedException();
    }

    /// <param name="observerPosition">The point onto which we project this triangle</param>
    /// <param name="point">A point on the surface of the triangle</param>
    /// <returns>The surface PDF of sampling this point. (unit: 1/m^2)</returns>
    public float PdfSolidAngle(Vector3 observerPosition, in SurfacePoint point) {
        float solidAngle = ComputeSolidAngle(observerPosition);

        if (solidAngle < MinSolidAngle || solidAngle > MaxSolidAngle)
            return PdfUniformArea(point);

        float jacobian = SampleWarp.SurfaceAreaToSolidAngle(observerPosition, point);
        return jacobian / solidAngle;
    }
}
