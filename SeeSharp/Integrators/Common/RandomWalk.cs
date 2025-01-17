namespace SeeSharp.Integrators.Common;

/// <summary>
/// Performs a random walk, invoking virtual callbacks for events along the path. The state of the walk is
/// tracked in this object, so it can only be used for one walk at a time.
/// </summary>
public ref struct RandomWalk<PayloadType> where PayloadType : new(){
    public record struct DirectionSample(
        float PdfForward,
        float PdfReverse,
        RgbColor Weight,
        Vector3 Direction,
        RgbColor ApproxReflectance
    ) {}

    public abstract class RandomWalkModifier {
        public virtual RgbColor OnInvalidHit(ref RandomWalk<PayloadType> walk, Ray ray, float pdfFromAncestor,
                                             RgbColor prefixWeight, int depth)
        => RgbColor.Black;

        public virtual RgbColor OnHit(ref RandomWalk<PayloadType> walk, in SurfaceShader shader, float pdfFromAncestor,
                                      RgbColor prefixWeight, int depth, float toAncestorJacobian)
        => RgbColor.Black;

        public virtual void OnContinue(ref RandomWalk<PayloadType> walk, float pdfToAncestor, int depth) {}

        public virtual void OnTerminate(ref RandomWalk<PayloadType> walk) {}

        public virtual void OnStartCamera(ref RandomWalk<PayloadType> walk, CameraRaySample cameraRay, Pixel filmPosition) {}
        public virtual void OnStartEmitter(ref RandomWalk<PayloadType> walk, EmitterSample emitterSample, RgbColor initialWeight) {}
        public virtual void OnStartBackground(ref RandomWalk<PayloadType> walk, Ray ray, RgbColor initialWeight, float pdf) {}

        public virtual DirectionSample SampleNextDirection(ref RandomWalk<PayloadType> walk, in SurfaceShader shader,
                                                           RgbColor prefixWeight, int depth)
        => walk.SampleBsdf(shader);

        public virtual float ComputeSurvivalProbability(ref RandomWalk<PayloadType> walk, in SurfacePoint hit, in Ray ray,
                                                        RgbColor prefixWeight, int depth)
        => walk.ComputeSurvivalProbability(depth);
    }

    public readonly RandomWalkModifier Modifier;
    public readonly Scene scene;
    public readonly int maxDepth;

    public Pixel FilmPosition;
    public bool isOnLightSubpath;
    public ref RNG rng;
    public PayloadType Payload;

    /// <summary>
    /// Tracks the product of (approximated) surface reflectances along the path. This is a more reliable
    /// quantity to use for Russian roulette than the prefix weight.
    /// </summary>
    public RgbColor ApproxThroughput = RgbColor.White;

    /// <summary>
    /// Initializes a new random walk
    /// </summary>
    /// <param name="scene">The scene</param>
    /// <param name="rng">Reference to the random number generator that is used to sample the path</param>
    /// <param name="maxDepth">Maximum number of edges along the path</param>
    /// <param name="modifier">Defines callbacks to be invoked at the scattering events</param>
    public RandomWalk(Scene scene, ref RNG rng, int maxDepth, RandomWalkModifier modifier = null) {
        this.scene = scene;
        this.maxDepth = maxDepth;
        this.rng = ref rng;
        Modifier = modifier;
    }

    public RgbColor StartFromCamera(CameraRaySample cameraRay, Pixel filmPosition, PayloadType payload) {
        isOnLightSubpath = false;
        FilmPosition = filmPosition;
        Payload = payload;
        Modifier?.OnStartCamera(ref this, cameraRay, filmPosition);

        return ContinueWalk(cameraRay.Ray, cameraRay.Point, cameraRay.PdfRay, cameraRay.Weight, 1);
    }

    public RgbColor StartFromEmitter(EmitterSample emitterSample, RgbColor initialWeight, PayloadType payload) {
        isOnLightSubpath = true;
        Payload = payload;
        Modifier?.OnStartEmitter(ref this, emitterSample, initialWeight);

        Ray ray = Raytracer.SpawnRay(emitterSample.Point, emitterSample.Direction);
        return ContinueWalk(ray, emitterSample.Point, emitterSample.Pdf, initialWeight, 1);
    }

    public RgbColor StartFromBackground(Ray ray, RgbColor initialWeight, float pdf, PayloadType payload) {
        isOnLightSubpath = true;
        Payload = payload;
        Modifier?.OnStartBackground(ref this, ray, initialWeight, pdf);

        // Find the first actual hitpoint on scene geometry
        var hit = scene.Raytracer.Trace(ray);
        if (!hit) {
            var contrib = Modifier?.OnInvalidHit(ref this, ray, pdf, initialWeight, 1) ?? RgbColor.Black;
            Modifier?.OnTerminate(ref this);
            return contrib;
        }

        SurfaceShader shader = new(hit, -ray.Direction, isOnLightSubpath);

        // Sample the next direction (required to know the reverse pdf)
        //  = SampleNextDirection(shader, initialWeight, 1);
        var dirSample = Modifier?.SampleNextDirection(ref this, shader, initialWeight, 1)
            ?? SampleBsdf(shader);
        ApproxThroughput *= dirSample.ApproxReflectance;

        // Both pdfs have unit sr-1
        float pdfFromAncestor = pdf;
        float pdfToAncestor = dirSample.PdfReverse;

        RgbColor estimate = Modifier?.OnHit(ref this, shader, pdfFromAncestor, initialWeight, 1, 1.0f) ?? RgbColor.Black;
        Modifier?.OnContinue(ref this, pdfToAncestor, 1);

        // Terminate if the maximum depth has been reached
        if (maxDepth <= 1) {
            Modifier?.OnTerminate(ref this);
            return estimate;
        }

        // Terminate absorbed paths and invalid samples
        if (dirSample.PdfForward == 0 || dirSample.Weight == RgbColor.Black) {
            Modifier?.OnTerminate(ref this);
            return estimate;
        }

        // Continue the path with the next ray
        ray = Raytracer.SpawnRay(hit, dirSample.Direction);
        return estimate + ContinueWalk(ray, hit, dirSample.PdfForward, initialWeight * dirSample.Weight, 2);
    }

    public DirectionSample SampleBsdf(in SurfaceShader shader) {
        var bsdfSample = shader.Sample(rng.NextFloat2D());
        return new(
            bsdfSample.Pdf,
            bsdfSample.PdfReverse,
            bsdfSample.Weight,
            bsdfSample.Direction,
            bsdfSample.Weight
        );
    }

    public float ComputeSurvivalProbability(int depth) {
        if (depth > 4)
            return Math.Clamp(ApproxThroughput.Average, 0.05f, 0.95f);
        else
            return 1.0f;
    }

    RgbColor ContinueWalk(Ray ray, SurfacePoint previousPoint, float pdfDirection, RgbColor prefixWeight, int depth) {
        RgbColor estimate = RgbColor.Black;
        while (depth < maxDepth) {
            var hit = scene.Raytracer.Trace(ray);
            if (!hit) {
                estimate += Modifier?.OnInvalidHit(ref this, ray, pdfDirection, prefixWeight, depth) ?? RgbColor.Black;
                break;
            }

            SurfaceShader shader = new(hit, -ray.Direction, isOnLightSubpath);

            // Convert the PDF of the previous hemispherical sample to surface area
            float pdfFromAncestor = pdfDirection * SampleWarp.SurfaceAreaToSolidAngle(previousPoint, hit);

            // Geometry term might be zero due to, e.g., shading normal issues
            // Avoid NaNs in that case by terminating early
            if (pdfFromAncestor == 0) break;

            float jacobian = SampleWarp.SurfaceAreaToSolidAngle(hit, previousPoint);
            estimate += Modifier?.OnHit(ref this, shader, pdfFromAncestor, prefixWeight, depth, jacobian) ?? RgbColor.Black;

            // Don't sample continuations if we are going to terminate anyway
            if (depth + 1 >= maxDepth)
                break;

            // Terminate with Russian roulette
            float survivalProb = Modifier?.ComputeSurvivalProbability(ref this, hit, ray, prefixWeight, depth)
                ?? ComputeSurvivalProbability(depth);
            if (rng.NextFloat() > survivalProb)
                break;

            // Sample the next direction and convert the reverse pdf
            // var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(shader, prefixWeight, depth);
            var dirSample = Modifier?.SampleNextDirection(ref this, shader, prefixWeight, depth) ?? SampleBsdf(shader);
            ApproxThroughput *= dirSample.ApproxReflectance / survivalProb;
            float pdfToAncestor = dirSample.PdfReverse * SampleWarp.SurfaceAreaToSolidAngle(hit, previousPoint);

            Modifier?.OnContinue(ref this, pdfToAncestor, depth);

            if (dirSample.PdfForward == 0 || dirSample.Weight == RgbColor.Black)
                break;

            if (isOnLightSubpath) {
                // The direction sample is multiplied by the shading cosine, but we need the geometric one
                dirSample.Weight *=
                    float.Abs(Vector3.Dot(hit.Normal, dirSample.Direction)) /
                    float.Abs(Vector3.Dot(hit.ShadingNormal, dirSample.Direction));

                // Rendering equation cosine cancels with the Jacobian, but only if geometry and shading geometry align
                dirSample.Weight *=
                    float.Abs(Vector3.Dot(hit.ShadingNormal, -ray.Direction)) /
                    float.Abs(Vector3.Dot(hit.Normal, -ray.Direction));

                SanityChecks.IsNormalized(ray.Direction);
            }

            // Continue the path with the next ray
            prefixWeight *= dirSample.Weight / survivalProb;
            depth++;
            pdfDirection = dirSample.PdfForward;
            previousPoint = hit;
            ray = Raytracer.SpawnRay(hit, dirSample.Direction);
        }

        Modifier?.OnTerminate(ref this);
        return estimate;
    }
}