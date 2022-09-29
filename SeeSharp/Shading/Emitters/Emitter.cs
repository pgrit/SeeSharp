namespace SeeSharp.Shading.Emitters;

/// <summary>
/// Base class for all types of area emitters
/// </summary>
public abstract class Emitter {
    /// <summary>
    /// The mesh that is emitting light
    /// </summary>
    public Mesh Mesh => Triangle.Mesh;

    /// <summary>
    /// The triangle that is emitting light
    /// </summary>
    public Triangle Triangle { get; init; }

    /// <summary>
    /// Samples a point on the mesh
    /// </summary>
    /// <param name="primary">A uniform sample in [0,1]x[0,1]</param>
    public SurfaceSample SampleUniformArea(Vector2 primary) => Triangle.SampleUniformArea(primary);

    /// <summary>
    /// Performs the inverse transformation that is done by <see cref="SampleUniformArea"/>
    /// </summary>
    /// <param name="point">A point on a surface of the emissive mesh</param>
    /// <returns>The uniform sample in [0,1]x[0,1]</returns>
    public Vector2 SampleUniformAreaInverse(in SurfacePoint point) => Triangle.SampleUniformAreaInverse(point);

    /// <param name="point">A point on the surface of the emissive mesh</param>
    /// <returns>The PDF of sampling this point</returns>
    public float PdfUniformArea(in SurfacePoint point) => Triangle.PdfUniformArea(point);

    /// <summary>
    /// Samples a point on the mesh
    /// </summary>
    /// <param name="observer">A point from which the emitter is seen</param>
    /// <param name="primary">A uniform sample in [0,1]x[0,1]</param>
    public SurfaceSample SampleSolidAngle(in SurfacePoint observer, Vector2 primary)
    => Triangle.SampleSolidAngle(observer.Position, primary);

    /// <summary>
    /// Performs the inverse transformation that is done by <see cref="SampleSolidAngle"/>
    /// </summary>
    /// <param name="observer">A point from which the emitter is seen</param>
    /// <param name="point">A point on a surface of the emissive mesh</param>
    /// <returns>The uniform sample in [0,1]x[0,1]</returns>
    public Vector2 SampleSolidAngleInverse(in SurfacePoint observer, in SurfacePoint point)
    => Triangle.SampleSolidAngleInverse(observer.Position, point);

    /// <param name="observer">A point from which the emitter is seen</param>
    /// <param name="point">A point on the surface of the emissive mesh</param>
    /// <returns>The PDF of sampling this point</returns>
    public float PdfSolidAngle(in SurfacePoint observer, in SurfacePoint point)
    => Triangle.PdfSolidAngle(observer.Position, point);

    /// <summary>
    /// Samples an emitted ray by sampling a point on the mesh and a direction for that point
    /// </summary>
    /// <param name="primaryPos">Uniform sample in [0,1]x[0,1] used to sample the point</param>
    /// <param name="primaryDir">Uniform sample in [0,1]x[0,1] used to sample the direction</param>
    /// <returns>Sampled ray and associated weights</returns>
    public abstract EmitterSample SampleRay(Vector2 primaryPos, Vector2 primaryDir);

    /// <summary>
    /// Performs the inverse transformation that is done by <see cref="SampleRayInverse"/>
    /// </summary>
    /// <param name="point">A position on the emissive mesh</param>
    /// <param name="direction">The direction of the ray from that position</param>
    /// <returns>Uniform random numbers mapped to these coordinates: (pos, dir)</returns>
    public abstract (Vector2, Vector2) SampleRayInverse(SurfacePoint point, Vector3 direction);

    /// <returns>The PDF value used by <see cref="SampleRay"/></returns>
    public abstract float PdfRay(SurfacePoint point, Vector3 direction);

    /// <returns>The amount of radiance emitted by the point in the given direction</returns>
    public abstract RgbColor EmittedRadiance(SurfacePoint point, Vector3 direction);

    /// <summary>
    /// Computes or estimates the total emissive power
    /// </summary>
    public abstract RgbColor ComputeTotalPower();
}