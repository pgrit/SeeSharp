namespace SeeSharp.Geometry;

/// <summary>
/// A point on a surface that was sampled randomly
/// </summary>
public struct SurfaceSample {
    /// <summary>
    /// The sampled point
    /// </summary>
    public SurfacePoint Point;

    /// <summary>
    /// Probability density at this point, per surface area
    /// </summary>
    public float Pdf;
}
