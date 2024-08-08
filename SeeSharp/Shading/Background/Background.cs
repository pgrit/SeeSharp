namespace SeeSharp.Shading.Background;

/// <summary>
/// Base class for all sorts of sky models, image based lighting, etc.
/// </summary>
public abstract class Background {
    /// <summary>
    /// Computes the emitted radiance from a given direction. All backgrounds are invariant with respect to the position.
    /// </summary>
    public abstract RgbColor EmittedRadiance(Vector3 direction);

    public abstract RgbColor ComputeTotalPower();

    public abstract BackgroundSample SampleDirection(Vector2 primary);
    public abstract Vector2 SampleDirectionInverse(Vector3 Direction);

    /// <summary>
    /// Computes the PDF for sampling a given direction from the background
    /// </summary>
    /// <param name="Direction">Direction from the scene towards the background</param>
    /// <returns>Solid angle PDF</returns>
    public abstract float DirectionPdf(Vector3 Direction);
    public abstract (Ray, RgbColor, float) SampleRay(Vector2 primaryPos, Vector2 primaryDir);
    public abstract (Vector2, Vector2) SampleRayInverse(Vector3 dir, Vector3 pos);

    /// <summary>
    /// Computes the pdf value for sampling a ray from the background towards the scene.
    /// </summary>
    /// <param name="point">A point along the ray. Could be the start, end, or some other point.</param>
    /// <param name="direction">Direction of the ray (i.e., from the background to the scene).</param>
    /// <returns></returns>
    public abstract float RayPdf(Vector3 point, Vector3 direction);

    public Vector3 SceneCenter;
    public float SceneRadius;
}