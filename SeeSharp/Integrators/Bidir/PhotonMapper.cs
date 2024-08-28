namespace SeeSharp.Integrators.Bidir;

/// <summary>
/// A pure photon mapper in its most naive form: merging at the first camera vertex with a fixed radius
/// computed from a fraction of the scene size.
/// </summary>
public class PhotonMapper : Integrator {
    /// <summary>
    /// Number of iterations to render.
    /// </summary>
    public int NumIterations = 2;

    /// <summary>
    /// Maximum number of nearest neighbor photons to search for.
    /// </summary>
    public int MaxNumPhotons = 10;

    /// <summary>
    /// Number of light paths in each iteration.
    /// </summary>
    public int NumLightPaths = 0;

    /// <summary>
    /// Seed for the random samples used to generate the photons
    /// </summary>
    public uint BaseSeedLight = 0xC030114u;

    /// <summary>
    /// Seed for the random samples used to generate the camera rays
    /// </summary>
    public uint BaseSeedCamera = 0x13C0FEFEu;

    /// <summary>
    /// The scene that is currently rendered
    /// </summary>
    protected Scene scene;

    /// <summary>
    /// Generates and stores the light paths / photons
    /// </summary>
    protected LightPathCache lightPaths;

    TinyEmbree.NearestNeighborSearch photonMap;

    /// <inheritdoc />
    public override void Render(Scene scene) {
        this.scene = scene;

        if (NumLightPaths <= 0) {
            NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
        }

        lightPaths = new LightPathCache {
            MaxDepth = MaxDepth,
            NumPaths = NumLightPaths,
            Scene = scene,
        };

        if (photonMap == null) photonMap = new();

        for (uint iter = 0; iter < NumIterations; ++iter) {
            scene.FrameBuffer.StartIteration();
            lightPaths.TraceAllPaths(BaseSeedLight, iter, null);
            ProcessPathCache();
            TraceAllCameraPaths(iter);
            scene.FrameBuffer.EndIteration();
            photonMap.Clear();
        }

        photonMap.Dispose();
        photonMap = null;
    }

    List<(int PathIndex, int VertexIndex)> photons = new();

    /// <summary>
    /// Builds the photon map from the cached light paths
    /// </summary>
    protected virtual void ProcessPathCache() {
        int index = 0;
        photons = new();
        for (int i = 0; i < lightPaths.NumPaths; ++i) {
            for (int k = 1; k < lightPaths.Length(i); ++k) {
                var vertex = lightPaths[i, k];
                if (vertex.Depth >= 1 && vertex.Weight != RgbColor.Black) {
                    photonMap.AddPoint(vertex.Point.Position, index++);
                    photons.Add((i, k));
                }
            }
        }
        photonMap.Build();
    }

    RgbColor Merge(float radius, SurfacePoint hit, Vector3 outDir, int pathIdx, int vertIdx, float distSqr,
                   float radiusSquared) {
        // Compute the contribution of the photon
        var photon = lightPaths[pathIdx, vertIdx];
        var ancestor = lightPaths[pathIdx, vertIdx - 1];
        var dirToAncestor = Vector3.Normalize(ancestor.Point.Position - photon.Point.Position);
        var bsdfValue = photon.Point.Material.Evaluate(hit, outDir, dirToAncestor, false);
        var photonContrib = photon.Weight * bsdfValue / NumLightPaths;

        // Epanechnikov kernel
        photonContrib *= (radiusSquared - distSqr) * 2.0f / (radiusSquared * radiusSquared * MathF.PI);

        return photonContrib;
    }

    /// <summary>
    /// Computes the estimated radiance travelling along a sampled camera ray
    /// </summary>
    /// <param name="pixel">Position on the image plane</param>
    /// <param name="ray">Ray sampled from the camera</param>
    /// <param name="weight">Contribution of the ray to the image, multiplied with the radiance</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>Pixel value estimate</returns>
    protected virtual RgbColor EstimatePixelValue(Vector2 pixel, Ray ray, RgbColor weight, ref RNG rng) {
        // Trace the primary ray into the scene
        var hit = scene.Raytracer.Trace(ray);
        if (!hit)
            return scene.Background?.EmittedRadiance(ray.Direction) ?? RgbColor.Black;

        // Gather nearby photons
        float radius = scene.Radius / 1000.0f;
        float footprint = hit.Distance * MathF.Tan(0.1f * MathF.PI / 180);
        radius = MathF.Min(footprint, radius);

        RgbColor estimate = RgbColor.Black;
        photonMap.ForAllNearest(hit.Position, int.MaxValue, radius, (position, idx, distance, numFound, maxDist) => {
            float radiusSquared = numFound == MaxNumPhotons ? maxDist * maxDist : radius * radius;
            estimate += Merge(radius, hit, -ray.Direction, photons[idx].PathIndex, photons[idx].VertexIndex,
                distance * distance, radius * radius);
        });

        // Add contribution from directly visible light sources
        var light = scene.QueryEmitter(hit);
        if (light != null) {
            estimate += light.EmittedRadiance(hit, -ray.Direction);
        }

        return estimate;
    }

    private void RenderPixel(uint row, uint col, ref RNG rng) {
        // Sample a ray from the camera
        var offset = rng.NextFloat2D();
        var filmSample = new Vector2(col, row) + offset;
        var cameraRay = scene.Camera.GenerateRay(filmSample, ref rng);
        var value = EstimatePixelValue(filmSample, cameraRay.Ray, cameraRay.Weight, ref rng);
        scene.FrameBuffer.Splat((int)col, (int)row, value);
    }

    private void TraceAllCameraPaths(uint iter) {
        Parallel.For(0, scene.FrameBuffer.Height,
            row => {
                var rng = new RNG(BaseSeedCamera, (uint)row, iter);
                for (uint col = 0; col < scene.FrameBuffer.Width; ++col) {
                    RenderPixel((uint)row, col, ref rng);
                }
            }
        );
    }
}
