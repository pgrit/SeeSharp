namespace SeeSharp.Geometry;

/// <summary>
/// Represents a point on the surface of a mesh in the scene. Wrapper around <see cref="Hit"/> with
/// additional SeeSharp specific material information.
/// </summary>
public struct SurfacePoint {
    /// <summary>
    /// Position in world space
    /// </summary>
    public Vector3 Position { get => hit.Position; set => hit.Position = value; }

    /// <summary>
    /// Face normal at the point (i.e., actual geometric normal, not the shading normal)
    /// </summary>
    public Vector3 Normal { get => hit.Normal; set => hit.Normal = value; }

    /// <summary>
    /// Barycentric coordinates within the primitive
    /// </summary>
    public Vector2 BarycentricCoords { get => hit.BarycentricCoords; set => hit.BarycentricCoords = value; }

    /// <summary>
    /// The mesh on which this point lies
    /// </summary>
    public Mesh Mesh { get => hit.Mesh as Mesh; set => hit.Mesh = value; }

    /// <summary>
    /// Index of the primitive within the mesh
    /// </summary>
    public uint PrimId { get => hit.PrimId; set => hit.PrimId = value; }

    /// <summary>
    /// Offset that should be used to avoid self-intersection during ray tracing
    /// </summary>
    public float ErrorOffset { get => hit.ErrorOffset; set => hit.ErrorOffset = value; }

    /// <summary>
    /// Distance from a previous point if this is a ray intersection
    /// </summary>
    public float Distance { get => hit.Distance; set => hit.Distance = value; }

    /// <summary>
    /// Checks if the point is valid
    /// </summary>
    public static implicit operator bool(SurfacePoint point) => point.hit;

    /// <summary>
    /// Implicit cast to a TinyEmbree hit object for convenience
    /// </summary>
    public static implicit operator Hit(SurfacePoint point) => point.hit;

    /// <summary>
    /// Implicit cast from a TinyEmbree hit object for convenience
    /// </summary>
    /// <param name="hit"></param>
    public static implicit operator SurfacePoint(Hit hit) {
        return new SurfacePoint { hit = hit };
    }

    /// <summary>
    /// Computes the shading normal on the fly, can be expensive
    /// </summary>
    public Vector3 ShadingNormal => hit.ShadingNormal;

    /// <summary>
    /// Computes / looks up the texture coordinates on-the-fly
    /// </summary>
    public Vector2 TextureCoordinates => hit.TextureCoordinates;

    /// <summary>
    /// The material of the intersected mesh
    /// </summary>
    public Material Material => Mesh.Material;

    Hit hit;
}
