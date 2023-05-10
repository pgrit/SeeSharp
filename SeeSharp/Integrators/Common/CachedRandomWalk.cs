namespace SeeSharp.Integrators.Common;

/// <summary>
/// Performs a random walk and stores all vertices along the path in a <see cref="PathCache" />.
/// </summary>
public class CachedRandomWalk : RandomWalk {
    /// <summary>
    /// The cache storing the generated path
    /// </summary>
    public readonly PathCache Cache;

    /// <summary>
    /// Index of the last vertex along the path that was generated
    /// </summary>
    public int LastId { get; protected set; }

    /// <summary>
    /// Index of the generated path in the cache
    /// </summary>
    public readonly int PathIdx;

    protected float nextReversePdf = 0.0f;

    /// <summary>
    /// Prepares an object that can be used to perform one random walk.
    /// </summary>
    /// <param name="scene">The scene being rendered</param>
    /// <param name="rng">RNG used for sampling the path</param>
    /// <param name="maxDepth">Maximum number of edges along the path</param>
    /// <param name="cache">The cache to store the path in</param>
    /// <param name="pathIdx">Index of this path in the cache</param>
    public CachedRandomWalk(Scene scene, RNG rng, int maxDepth, PathCache cache, int pathIdx)
        : base(scene, rng, maxDepth) {
        this.Cache = cache;
        this.PathIdx = pathIdx;
    }

    /// <inheritdoc />
    public override RgbColor StartFromEmitter(EmitterSample emitterSample, RgbColor initialWeight) {
        nextReversePdf = 0.0f;
        // Add the vertex on the light source
        LastId = Cache.AddVertex(new PathVertex {
            // TODO are any of these actually useful? Only the point right now, but only because we do not pre-compute
            //      the next event weight (which would be more efficient to begin with)
            Point = emitterSample.Point,
            PdfFromAncestor = 0.0f, // unused
            PdfReverseAncestor = 0.0f, // unused
            Weight = RgbColor.Black, // the first known weight is that at the first hit point
            AncestorId = -1,
            Depth = 0,
            MaximumRoughness = 0
        }, PathIdx);
        return base.StartFromEmitter(emitterSample, initialWeight);
    }

    /// <inheritdoc />
    public override RgbColor StartFromBackground(Ray ray, RgbColor initialWeight, float pdf) {
        nextReversePdf = 0.0f;
        // Add the vertex on the light source
        LastId = Cache.AddVertex(new PathVertex {
            // TODO are any of these actually useful? Only the point right now, but only because we do not pre-compute
            //      the next event weight (which would be more efficient to begin with)
            Point = new SurfacePoint { Position = ray.Origin },
            PdfFromAncestor = 0.0f, // unused
            PdfReverseAncestor = 0.0f, // unused
            Weight = RgbColor.Black, // the first known weight is that at the first hit point
            AncestorId = -1,
            Depth = 0,
            MaximumRoughness = 0
        }, PathIdx);
        return base.StartFromBackground(ray, initialWeight, pdf);
    }

    /// <inheritdoc />
    protected override RgbColor OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, RgbColor throughput,
                                      int depth, float toAncestorJacobian) {
        // Add the next vertex
        float lastRougness = Cache[PathIdx, LastId].MaximumRoughness;
        float roughness = hit.Material.GetRoughness(hit);
        LastId = Cache.AddVertex(new PathVertex {
            Point = hit,
            PdfFromAncestor = pdfFromAncestor,
            PdfReverseAncestor = nextReversePdf,
            Weight = throughput,
            AncestorId = LastId,
            Depth = (byte)depth,
            MaximumRoughness = MathF.Max(roughness, lastRougness)
        }, PathIdx);
        return RgbColor.Black;
    }

    /// <inheritdoc />
    protected override void OnContinue(float pdfToAncestor, int depth) {
        nextReversePdf = pdfToAncestor;
    }
}