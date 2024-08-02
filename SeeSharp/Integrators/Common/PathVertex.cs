namespace SeeSharp.Integrators.Common;

/// <summary>
/// Stores the info of a single vertex of a cached light path
/// </summary>
public struct PathVertex {
    /// <summary>
    /// The surface intersection. If this is the first vertex of a background path, this point is not actually
    /// on a surface but somewhere in free space outside the scene.
    /// </summary>
    public SurfacePoint Point;

    /// <summary>
    /// Surface area pdf to sample this vertex from the previous one, i.e., the actual density this vertex
    /// was sampled from
    /// </summary>
    public float PdfFromAncestor;

    /// <summary> Surface area pdf to sample the ancestor of the previous vertex. </summary>
    public float PdfReverseAncestor;

    /// <summary> Surface area pdf of next event estimation at the ancestor (if applicable) </summary>
    public float PdfNextEventAncestor;

    /// <summary>
    /// Accumulated Monte Carlo weight of the sub-path up to and including this vertex
    /// </summary>
    public RgbColor Weight;

    /// <summary>
    /// 0-based index of the path this vertex belongs to
    /// </summary>
    public int PathId;

    /// <summary>
    /// The number of edges along the path.
    /// </summary>
    public byte Depth;

    /// <summary>
    /// Maximum roughness of the materials at any of the previous vertices and this one.
    /// </summary>
    public float MaximumRoughness;

    /// <summary>
    /// True if the path behind this vertex originated from the background rather than an emissive surface
    /// </summary>
    public bool FromBackground;
}
