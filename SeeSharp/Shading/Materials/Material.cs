namespace SeeSharp.Shading.Materials;

public struct ShadingContext {
    public SurfacePoint Point;
    public bool IsOnLightSubpath;
    public Vector3 Normal;
    public Vector3 Tangent;
    public Vector3 Binormal;

    /// <summary>
    /// Outgoing direction in shading space
    /// </summary>
    public Vector3 OutDir;

    public Vector3 OutDirWorld;

    public ShadingContext(in SurfacePoint point, in Vector3 outDir, bool isOnLightSubpath) {
        Point = point;
        IsOnLightSubpath = isOnLightSubpath;
        Normal = point.ShadingNormal;
        ComputeBasisVectors(Normal, out Tangent, out Binormal);
        OutDir = WorldToShading(outDir);
        OutDirWorld = outDir;
    }

    public Vector3 WorldToShading(in Vector3 dir) => ShadingSpace.WorldToShading(Normal, Tangent, Binormal, dir);
    public Vector3 ShadingToWorld(in Vector3 dir) => ShadingSpace.ShadingToWorld(Normal, Tangent, Binormal, dir);

    // public object ShaderData;
}

/// <summary>
/// Precomputes the relevant context information to evaluate or sample a material at a surface point,
/// given an outgoing ray direction.
/// </summary>
public struct SurfaceShader {
    readonly Material material;

    public ShadingContext Context;
    public SurfacePoint Point => Context.Point;

    public SurfaceShader(in SurfacePoint point, in Vector3 outDir, bool isOnLightSubpath) {
        material = point.Material;
        Context = new(point, outDir, isOnLightSubpath);
        material.PopulateContext(ref Context);
    }

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
    /// <param name="primarySample">A uniform sample in [0,1]x[0,1] that should be transformed</param>
    /// <returns>Sampled direction and associated weights</returns>
    public BsdfSample Sample(Vector2 primarySample) {
        Material.ComponentWeights c = new();
        return Sample(primarySample, ref c);
    }

    /// <returns>The pdf of sampling the incoming direction via <see cref="Sample(Vector2)"/></returns>
    public (float Pdf, float PdfReverse) Pdf(Vector3 inDir) {
        Material.ComponentWeights c = new();
        return Pdf(inDir, ref c);
    }

    public BsdfSample Sample(Vector2 primarySample, ref Material.ComponentWeights componentWeights)
    => material.Sample(Context, primarySample, ref componentWeights);

    public (float Pdf, float PdfReverse) Pdf(Vector3 inDir, ref Material.ComponentWeights componentWeights)
    => material.Pdf(Context, inDir, ref componentWeights);

    public int MaxSamplingComponents => material.MaxSamplingComponents;
}

/// <summary>
/// Base class for all surface materials
/// </summary>
public abstract class Material {
    public string Name { get; init; }

    /// <summary>
    /// Tracks buffers to receive per-component PDF values and mixture weights
    /// </summary>
    public ref struct ComponentWeights {
        public Span<float> Pdfs;
        public Span<float> Weights;
        public Span<float> PdfsReverse;
        public Span<float> WeightsReverse;
        public int NumComponents;
        public int NumComponentsReverse;
    }

    public virtual void PopulateContext(ref ShadingContext context) { }

    /// <summary>
    /// Computes the surface roughness, a value between 0 and 1.
    /// 0 is perfectly specular, 1 is perfectly diffuse.
    /// The exact value can differ between materials, as
    /// this is not a well-defined quantity from a physical point of view.
    /// </summary>
    public abstract float GetRoughness(in SurfacePoint hit);

    /// <summary>
    /// Computes the ratio of interior and exterior index of refraction. Exterior is defined as the
    /// hemisphere of the shading normal.
    /// </summary>
    /// <param name="hit">The query point in case the material is spatially varying</param>
    /// <returns>(interior IOR / exterior IOR) at the query point</returns>
    public abstract float GetIndexOfRefractionRatio(in SurfacePoint hit);

    /// <summary>
    /// Computes the sum of reflectance and transmittance.
    /// Can be an approximation, with the accuracy depending on the material.
    /// </summary>
    public abstract RgbColor GetScatterStrength(in SurfacePoint hit);

    /// <summary>
    /// False if the material only reflects light (a BRDF), otherwise true (a BSDF)
    /// </summary>
    public abstract bool IsTransmissive(in SurfacePoint hit);

    /// <summary>
    /// Evaluates the BSDF of the material
    /// </summary>
    /// <param name="context">Shading context that defines (and caches) the shading point, normal, outgoing direction, etc.</param>
    /// <param name="inDir">Normalized world-space incoming direction away from the surface (towards light in a path tracer)</param>
    /// <returns>BSDF value</returns>
    public abstract RgbColor Evaluate(in ShadingContext context, Vector3 inDir);

    /// <summary>
    /// Evaluates the BSDF of the material
    /// </summary>
    /// <param name="hit">Surface point</param>
    /// <param name="outDir">Normalized outgoing direction away from the surface (towards camera in a path tracer)</param>
    /// <param name="inDir">Normalized incoming direction away from the surface (towards light in a path tracer)</param>
    /// <param name="isOnLightSubpath">True for paths originating from a light source</param>
    /// <returns>BSDF value</returns>
    public RgbColor Evaluate(in SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
    => Evaluate(new(hit, outDir, isOnLightSubpath), inDir);

    /// <summary>
    /// Computes product of the BSDF and the cosine between the incoming direction and the surface
    /// shading normal. Usually more efficient / numerically stable than computing individually.
    /// </summary>
    /// <param name="hit">Surface point</param>
    /// <param name="outDir">Normalized outgoing direction away from the surface (towards camera in a path tracer)</param>
    /// <param name="inDir">Normalized incoming direction away from the surface (towards light in a path tracer)</param>
    /// <param name="isOnLightSubpath">True for paths originating from a light source</param>
    /// <returns>BSDF * cosine</returns>
    public virtual RgbColor EvaluateWithCosine(in SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
    => EvaluateWithCosine(new(hit, outDir, isOnLightSubpath), inDir);

    public virtual RgbColor EvaluateWithCosine(in ShadingContext context, Vector3 inDir) {
        var bsdf = Evaluate(context, inDir);
        return bsdf * AbsCosTheta(context.WorldToShading(inDir));
    }

    /// <summary>
    /// Importance samples the product of BSDF and cosine
    /// </summary>
    /// <param name="hit">Surface point</param>
    /// <param name="outDir">Normalized outgoing direction away from the surface (towards camera in a path tracer)</param>
    /// <param name="isOnLightSubpath">True for paths originating from a light source</param>
    /// <param name="primarySample">A uniform sample in [0,1]x[0,1] that should be transformed</param>
    /// <returns>Sampled direction and associated weights</returns>
    public BsdfSample Sample(in SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
        ComponentWeights c = new();
        return Sample(hit, outDir, isOnLightSubpath, primarySample, ref c);
    }

    /// <returns>The pdf of sampling the incoming direction via <see cref="Sample(in SurfacePoint, Vector3, bool, Vector2)"/></returns>
    public (float Pdf, float PdfReverse) Pdf(in SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        ComponentWeights c = new();
        return Pdf(hit, outDir, inDir, isOnLightSubpath, ref c);
    }

    public BsdfSample Sample(in SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath,
                             Vector2 primarySample, ref ComponentWeights componentWeights)
    => Sample(new(hit, outDir, isOnLightSubpath), primarySample, ref componentWeights);

    public (float Pdf, float PdfReverse) Pdf(in SurfacePoint hit, Vector3 outDir, Vector3 inDir,
                                             bool isOnLightSubpath, ref ComponentWeights componentWeights)
    => Pdf(new(hit, outDir, isOnLightSubpath), inDir, ref componentWeights);

    public abstract BsdfSample Sample(in ShadingContext context, Vector2 primarySample, ref ComponentWeights componentWeights);
    public abstract (float Pdf, float PdfReverse) Pdf(in ShadingContext context, Vector3 inDir, ref ComponentWeights componentWeights);

    public virtual int MaxSamplingComponents => 1;

    /// <summary>
    /// Tests whether the incoming and outgoing direction are on the same or different sides of the
    /// actual geometry, based on the actual normal, not the shading normal.
    /// The directions do not have to be normalized.
    /// </summary>
    /// <param name="hit">The surface point</param>
    /// <param name="outDir">Normalized outgoing direction in world space, away from the surface</param>
    /// <param name="inDir">Normalized incoming direction in world space, away from the surface</param>
    /// <returns>True, if they are on the same side, i.e., only reflection should be evaluated.</returns>
    public static bool ShouldReflect(in SurfacePoint hit, Vector3 outDir, Vector3 inDir) {
        // Prevent light leaks based on the actual geometric normal
        float geoCosOut = Vector3.Dot(outDir, hit.Normal);
        float geoCosIn = Vector3.Dot(inDir, hit.Normal);
        if (geoCosIn * geoCosOut >= 0)
            return true;
        return false;
    }
}
