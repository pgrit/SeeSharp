namespace SeeSharp.Geometry;

/// <summary>
/// Represents a single triangle inside a mesh. This is a view on a <see cref="Mesh"/> object, any
/// modifications to the mesh will be reflected. The view becomes invalid if the original mesh is modified.
/// </summary>
public struct Triangle {
    public Mesh Mesh { get; init; }
    public int FaceIndex { get; init; }

    float invSurfaceArea;

    /// <summary>
    /// Creates a new view for a triangle inside an existing mesh
    /// </summary>
    /// <param name="mesh">The mesh that contains this triangle</param>
    /// <param name="faceIndex">0-based index of the triangle within the mesh</param>
    public Triangle(Mesh mesh, int faceIndex) {
        Mesh = mesh;
        FaceIndex = faceIndex;

        var v1 = Mesh.Vertices[Mesh.Indices[faceIndex * 3 + 0]];
        var v2 = Mesh.Vertices[Mesh.Indices[faceIndex * 3 + 1]];
        var v3 = Mesh.Vertices[Mesh.Indices[faceIndex * 3 + 2]];
        Vector3 n = Vector3.Cross(v2 - v1, v3 - v1);
        invSurfaceArea = 1.0f / (n.Length() * 0.5f);
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
    public Vector2 SampleUniformAreaInverse(SurfacePoint point)
    => SampleWarp.FromUniformTriangle(point.BarycentricCoords);

    /// <param name="point">A point on the surface of the triangle</param>
    /// <returns>The PDF of sampling this point</returns>
    public float PdfUniformArea(SurfacePoint point) => invSurfaceArea;

    /// <summary>
    /// Transforms a primary sample to the projection of the triangle onto the sphere of directions
    /// around a point.
    /// </summary>
    /// <param name="observerPosition">The point onto which we project this triangle</param>
    /// <param name="primary">A uniform sample in [0,1]x[0,1]</param>
    public SurfaceSample SampleProjectedArea(Vector3 observerPosition, Vector2 primary) {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Performs the inverse transformation that is done by <see cref="SampleProjectedArea"/>
    /// </summary>
    /// <param name="observerPosition">The point onto which we project this triangle</param>
    /// <param name="point">A point on a surface of the emissive mesh</param>
    /// <returns>The uniform sample in [0,1]x[0,1]</returns>
    public Vector2 SampleProjectedAreaInverse(Vector3 observerPosition, SurfacePoint point) {
        throw new NotImplementedException();
    }

    /// <param name="point">A point on the surface of the triangle</param>
    /// <returns>The PDF of sampling this point</returns>
    public float PdfProjectedArea(SurfacePoint point) {
        throw new NotImplementedException();
    }
}
