namespace SeeSharp.Geometry;

/// <summary>
/// A simple triangle mesh with methods to uniformly sample its area.
/// </summary>
public class Mesh : TinyEmbree.TriangleMesh {
    public string Name { get; set; }

    /// <summary>
    /// The assigned material
    /// </summary>
    public Material Material;

    /// <summary>
    /// Creates a new mesh based on the given list of vertices, indices, and optional parameters
    /// </summary>
    /// <param name="vertices">List of vertices</param>
    /// <param name="indices">
    ///     Three integers for each triangle that identify which vertices form that triangle
    /// </param>
    /// <param name="shadingNormals">Shading normals for each vertex</param>
    /// <param name="textureCoordinates">Texture coordinates for each vertex</param>
    public Mesh(Vector3[] vertices, int[] indices, Vector3[] shadingNormals = null,
                Vector2[] textureCoordinates = null)
        : base(vertices, indices, shadingNormals, textureCoordinates) {
        // Compute the uniform area sampling distribution
        var surfaceAreas = new float[NumFaces];
        for (int face = 0; face < NumFaces; ++face) {
            var v1 = vertices[indices[face * 3 + 0]];
            var v2 = vertices[indices[face * 3 + 1]];
            var v3 = vertices[indices[face * 3 + 2]];
            Vector3 n = Vector3.Cross(v2 - v1, v3 - v1);
            surfaceAreas[face] = n.Length() * 0.5f;
        }
        triangleDistribution = new PiecewiseConstant(surfaceAreas);
    }

    /// <summary>
    /// Computes a sufficient offset around a point on the surface that ensures no self-intersections will
    /// be reported when tracing a ray from this point.
    /// </summary>
    /// <param name="faceIdx">Index of a triangle within the mesh</param>
    /// <param name="barycentricCoords">Position on the triangle</param>
    /// <returns>Required offset</returns>
    public float ComputeErrorOffset(int faceIdx, Vector2 barycentricCoords) {
        var v1 = Vertices[Indices[faceIdx * 3 + 0]];
        var v2 = Vertices[Indices[faceIdx * 3 + 1]];
        var v3 = Vertices[Indices[faceIdx * 3 + 2]];

        Vector3 errorDiagonal = Vector3.Abs(barycentricCoords.X * v2)
            + Vector3.Abs(barycentricCoords.Y * v3)
            + Vector3.Abs((1 - barycentricCoords.X - barycentricCoords.Y) * v1);

        return errorDiagonal.Length() * 32.0f * 1.19209e-07f;
    }

    /// <summary>
    /// Samples a point uniformly distributed on the mesh surface
    /// </summary>
    /// <param name="primarySample">
    ///     A primary sample space value that is projected onto the surface of the mesh.
    /// </param>
    /// <returns>A point and associated surface area pdf</returns>
    public SurfaceSample Sample(Vector2 primarySample) {
        var (faceIdx, newX) = triangleDistribution.Sample(primarySample.X);
        var barycentric = SampleWarp.ToUniformTriangle(new Vector2(newX, primarySample.Y));

        return new SurfaceSample {
            Point = new SurfacePoint {
                BarycentricCoords = barycentric,
                PrimId = (uint)faceIdx,
                Normal = FaceNormals[faceIdx],
                Position = ComputePosition(faceIdx, barycentric),
                ErrorOffset = ComputeErrorOffset(faceIdx, barycentric),
                Mesh = this
            },
            Pdf = 1.0f / SurfaceArea
        };
    }

    /// <summary>
    /// Performs the inverse of the projection done by <see cref="Sample"/>
    /// </summary>
    /// <param name="point">A point on the surface of this mesh</param>
    /// <returns>The primary sample space point that would have been projected there</returns>
    public Vector2 SampleInverse(SurfacePoint point) {
        var local = SampleWarp.FromUniformTriangle(point.BarycentricCoords);
        float x = triangleDistribution.SampleInverse((int)point.PrimId, local.X);
        return new(x, local.Y);
    }

    /// <returns>The surface area pdf of sampling the given point on the surface of this mesh.</returns>
    public float Pdf(SurfacePoint point) {
        return 1.0f / SurfaceArea;
    }

    readonly PiecewiseConstant triangleDistribution;
}
