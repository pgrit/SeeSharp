namespace SeeSharp.Integrators.Common;

/// <summary>
/// Performs a recursive random walk, invoking virtual callbacks for events along the path.
/// </summary>
public class RandomWalk {
    /// <summary>
    /// Initializes a new random walk
    /// </summary>
    /// <param name="scene">The scene</param>
    /// <param name="rng">RNG used to sample the walk</param>
    /// <param name="maxDepth">Maximum number of edges along the path</param>
    public RandomWalk(Scene scene, RNG rng, int maxDepth) {
        this.scene = scene;
        this.rng = rng;
        this.maxDepth = maxDepth;
    }

    // TODO replace parameter list by a single CameraRaySample object
    public virtual RgbColor StartFromCamera(Pixel filmPosition, SurfacePoint cameraPoint,
                                            float pdfFromCamera, Ray primaryRay, RgbColor initialWeight) {
        isOnLightSubpath = false;
        return ContinueWalk(primaryRay, cameraPoint, pdfFromCamera, initialWeight, 1);
    }

    public virtual RgbColor StartFromEmitter(EmitterSample emitterSample, RgbColor initialWeight) {
        isOnLightSubpath = true;
        Ray ray = Raytracer.SpawnRay(emitterSample.Point, emitterSample.Direction);
        return ContinueWalk(ray, emitterSample.Point, emitterSample.Pdf, initialWeight, 1);
    }

    public virtual RgbColor StartFromBackground(Ray ray, RgbColor initialWeight, float pdf) {
        isOnLightSubpath = true;

        // Find the first actual hitpoint on scene geometry
        var hit = scene.Raytracer.Trace(ray);
        if (!hit)
            return OnInvalidHit(ray, pdf, initialWeight, 1);

        SurfaceShader shader = new(hit, -ray.Direction, isOnLightSubpath);

        // Sample the next direction (required to know the reverse pdf)
        var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(shader, initialWeight, 1);

        // Both pdfs have unit sr-1
        float pdfFromAncestor = pdf;
        float pdfToAncestor = pdfReverse;

        RgbColor estimate = OnHit(shader, pdfFromAncestor, initialWeight, 1, 1.0f);
        OnContinue(pdfToAncestor, 1);

        // Terminate if the maximum depth has been reached
        if (maxDepth <= 1)
            return estimate;

        // Terminate absorbed paths and invalid samples
        if (pdfNext == 0 || weight == RgbColor.Black)
            return estimate;

        // Continue the path with the next ray
        ray = Raytracer.SpawnRay(hit, direction);
        return estimate + ContinueWalk(ray, hit, pdfNext, initialWeight * weight, 2);
    }

    protected virtual RgbColor OnInvalidHit(Ray ray, float pdfFromAncestor, RgbColor throughput, int depth) {
        return RgbColor.Black;
    }

    protected virtual RgbColor OnHit(in SurfaceShader shader, float pdfFromAncestor, RgbColor throughput,
                                     int depth, float toAncestorJacobian) {
        return RgbColor.Black;
    }

    protected virtual void OnContinue(float pdfToAncestor, int depth) { }

    protected virtual void OnTerminate() { }

    protected virtual (float, float, RgbColor, Vector3) SampleNextDirection(in SurfaceShader shader,
                                                                            RgbColor throughput,
                                                                            int depth) {
        var bsdfSample = shader.Sample(rng.NextFloat2D());
        return (
            bsdfSample.Pdf,
            bsdfSample.PdfReverse,
            bsdfSample.Weight,
            bsdfSample.Direction
        );
    }

    protected virtual float ComputeSurvivalProbability(SurfacePoint hit, Ray ray, RgbColor throughput, int depth)
    => 1.0f;

    RgbColor ContinueWalk(Ray ray, SurfacePoint previousPoint, float pdfDirection, RgbColor throughput, int depth) {
        RgbColor estimate = RgbColor.Black;
        while (depth < maxDepth) {
            var hit = scene.Raytracer.Trace(ray);
            if (!hit) {
                estimate += OnInvalidHit(ray, pdfDirection, throughput, depth);
                break;
            }

            SurfaceShader shader = new(hit, -ray.Direction, isOnLightSubpath);

            // Convert the PDF of the previous hemispherical sample to surface area
            float pdfFromAncestor = pdfDirection * SampleWarp.SurfaceAreaToSolidAngle(previousPoint, hit);

            // Geometry term might be zero due to, e.g., shading normal issues
            // Avoid NaNs in that case by terminating early
            if (pdfFromAncestor == 0) break;

            float jacobian = SampleWarp.SurfaceAreaToSolidAngle(hit, previousPoint);
            estimate += OnHit(shader, pdfFromAncestor, throughput, depth, jacobian);

            // Don't sample continuations if we are going to terminate anyway
            if (depth + 1 >= maxDepth)
                break;

            // Terminate with Russian roulette
            float survivalProb = ComputeSurvivalProbability(hit, ray, throughput, depth);
            if (rng.NextFloat() > survivalProb)
                break;

            // Sample the next direction and convert the reverse pdf
            var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(shader, throughput, depth);
            float pdfToAncestor = pdfReverse * SampleWarp.SurfaceAreaToSolidAngle(hit, previousPoint);

            OnContinue(pdfToAncestor, depth);

            if (pdfNext == 0 || weight == RgbColor.Black)
                break;

            // Continue the path with the next ray
            throughput *= weight / survivalProb;
            depth++;
            pdfDirection = pdfNext * survivalProb;
            previousPoint = hit;
            ray = Raytracer.SpawnRay(hit, direction);
        }

        OnTerminate();
        return estimate;
    }

    protected readonly Scene scene;
    protected readonly RNG rng;
    protected readonly int maxDepth;
    protected bool isOnLightSubpath;
}