namespace SeeSharp.Shading.Materials;

/// <summary>
/// Precomputes the relevant context information to evaluate or sample a material at a surface point,
/// given an outgoing ray direction.
/// </summary>
public struct SurfaceShader(in SurfacePoint point, in Vector3 outDir, bool isOnLightSubpath) {
    readonly Material material = point.Material;

    public ShadingContext Context = new(point, outDir, isOnLightSubpath);
    public readonly SurfacePoint Point => Context.Point;

    /// <summary>
    /// Computes the surface roughness, a value between 0 and 1.
    /// 0 is perfectly specular, 1 is perfectly diffuse.
    /// The exact value can differ between materials, as
    /// this is not a well-defined quantity from a physical point of view.
    /// </summary>
    public float GetRoughness() => material.GetRoughness(Context.Point);

    /// <summary>
    /// Computes the ratio of interior and exterior index of refraction. Exterior is defined as the
    /// hemisphere of the shading normal.
    /// </summary>
    /// <returns>(interior IOR / exterior IOR) at the query point</returns>
    public float GetIndexOfRefractionRatio() => material.GetIndexOfRefractionRatio(Context.Point);

    /// <summary>
    /// Computes the sum of reflectance and transmittance.
    /// Can be an approximation, with the accuracy depending on the material.
    /// </summary>
    public RgbColor GetScatterStrength() => material.GetScatterStrength(Context.Point);

    /// <summary>
    /// False if the material only reflects light (a BRDF), otherwise true (a BSDF)
    /// </summary>
    public bool IsTransmissive() => material.IsTransmissive(Context.Point);

    /// <summary>
    /// Evaluates the BSDF of the material
    /// </summary>
    /// <param name="inDir">Normalized incoming direction away from the surface (towards light in a path tracer)</param>
    /// <returns>BSDF value</returns>
    public RgbColor Evaluate(Vector3 inDir) => material.Evaluate(Context, inDir);

    /// <summary>
    /// Computes product of the BSDF and the cosine between the incoming direction and the surface
    /// shading normal. Usually more efficient / numerically stable than computing individually.
    /// </summary>
    /// <param name="inDir">Normalized incoming direction away from the surface (towards light in a path tracer)</param>
    /// <returns>BSDF * cosine</returns>
    public RgbColor EvaluateWithCosine(Vector3 inDir) => material.EvaluateWithCosine(Context, inDir);

    /// <summary>
    /// Importance samples the product of BSDF and cosine
    /// </summary>
    /// <param name="primaryComponent">A uniform sample in [0,1] for BSDF component selection</param>
    /// <param name="primaryDirection">A uniform sample in [0,1]x[0,1] that should be transformed to the 2D direction</param>
    /// <returns>Sampled direction and associated weights</returns>
    public BsdfSample Sample(float primaryComponent, Vector2 primaryDirection) {
        Material.ComponentWeights c = new();
        return Sample(primaryComponent, primaryDirection, ref c);
    }

    /// <returns>The pdf of sampling the incoming direction via <see cref="Sample(float, Vector2)"/></returns>
    public (float Pdf, float PdfReverse) Pdf(Vector3 inDir) {
        Material.ComponentWeights c = new();
        return Pdf(inDir, ref c);
    }

    public BsdfSample Sample(float primaryComponent, Vector2 primaryDirection, ref Material.ComponentWeights componentWeights)
    => material.Sample(Context, primaryComponent, primaryDirection, ref componentWeights);

    public (float Pdf, float PdfReverse) Pdf(Vector3 inDir, ref Material.ComponentWeights componentWeights)
    => material.Pdf(Context, inDir, ref componentWeights);

    public int MaxSamplingComponents => material.MaxSamplingComponents;
}
