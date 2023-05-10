﻿namespace SeeSharp.Integrators.Common;

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

        // Sample the next direction (required to know the reverse pdf)
        var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(hit, ray, initialWeight, 1);

        // Both pdfs have unit sr-1
        float pdfFromAncestor = pdf;
        float pdfToAncestor = pdfReverse;

        RgbColor estimate = OnHit(ray, hit, pdfFromAncestor, initialWeight, 1, 1.0f);
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

    public RgbColor StartOnSurface(Ray ray, SurfacePoint hit, RgbColor throughput, int initialDepth,
                                   bool isOnLightSubpath) {
        this.isOnLightSubpath = isOnLightSubpath;
        var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(hit, ray, throughput, initialDepth);

        // Avoid NaNs if the surface is not reflective, or an invalid sample was generated.
        if (pdfNext == 0.0f || weight == RgbColor.Black)
            return RgbColor.Black;

        return ContinueWalk(ray, hit, pdfNext, throughput, initialDepth + 1);
    }

    protected virtual RgbColor OnInvalidHit(Ray ray, float pdfFromAncestor, RgbColor throughput, int depth) {
        return RgbColor.Black;
    }

    protected virtual RgbColor OnHit(Ray ray, SurfacePoint hit, float pdfFromAncestor, RgbColor throughput,
                                     int depth, float toAncestorJacobian) {
        return RgbColor.Black;
    }

    protected virtual void OnContinue(float pdfToAncestor, int depth) { }

    protected virtual void OnTerminate() { }

    protected virtual (float, float, RgbColor, Vector3) SampleNextDirection(SurfacePoint hit, Ray ray,
                                                                            RgbColor throughput,
                                                                            int depth) {
        // Sample the next direction from the BSDF
        var bsdfSample = hit.Material.Sample(hit, -ray.Direction, isOnLightSubpath, rng.NextFloat2D());
        return (
            bsdfSample.Pdf,
            bsdfSample.PdfReverse,
            bsdfSample.Weight,
            bsdfSample.Direction
        );
    }

    protected virtual int ComputeSplitFactor(SurfacePoint hit, Ray ray, RgbColor throughput, int depth)
    => 1;
    protected virtual float ComputeSurvivalProbability(SurfacePoint hit, Ray ray, RgbColor throughput,
                                                       int depth)
    => 1.0f;

    protected RgbColor ContinueWalk(Ray ray, SurfacePoint previousPoint, float pdfDirection, RgbColor throughput,
                          int depth) {
        // Terminate if the maximum depth has been reached
        if (depth >= maxDepth) {
            OnTerminate();
            return RgbColor.Black;
        }

        var hit = scene.Raytracer.Trace(ray);
        if (!hit) {
            var result = OnInvalidHit(ray, pdfDirection, throughput, depth);
            OnTerminate();
            return result;
        }
        return HandleHit(ray, hit, previousPoint, pdfDirection, throughput, depth);
    }

    protected RgbColor HandleHit(Ray ray, SurfacePoint hit, SurfacePoint previousPoint, float pdfDirection, RgbColor throughput,
                          int depth) {
        // Convert the PDF of the previous hemispherical sample to surface area
        float pdfFromAncestor = pdfDirection * SampleWarp.SurfaceAreaToSolidAngle(previousPoint, hit);

        // Geometry term might be zero due to, e.g., shading normal issues
        // Avoid NaNs in that case by terminating early
        if (pdfFromAncestor == 0) {
            OnTerminate();
            return RgbColor.Black;
        }

        RgbColor estimate = OnHit(ray, hit, pdfFromAncestor, throughput, depth,
                                  SampleWarp.SurfaceAreaToSolidAngle(hit, previousPoint));

        // Don't sample continuations if we are going to terminate anyway
        if (depth + 1 >= maxDepth) {
            OnTerminate();
            return estimate;
        }

        // Terminate with Russian roulette
        float survivalProb = ComputeSurvivalProbability(hit, ray, throughput, depth);
        if (rng.NextFloat() > survivalProb) {
            OnTerminate();
            return estimate;
        }

        // Continue based on the splitting factor
        int numSplits = ComputeSplitFactor(hit, ray, throughput, depth);
        for (int i = 0; i < numSplits; ++i) {
            // Sample the next direction and convert the reverse pdf
            var (pdfNext, pdfReverse, weight, direction) = SampleNextDirection(hit, ray, throughput, depth);
            float pdfToAncestor = pdfReverse * SampleWarp.SurfaceAreaToSolidAngle(hit, previousPoint);

            OnContinue(pdfToAncestor, depth);

            if (pdfNext == 0 || weight == RgbColor.Black) {
                OnTerminate();
                continue;
            }

            // Account for splitting and roulette in the weight
            weight *= 1.0f / (survivalProb * numSplits);

            // Continue the path with the next ray
            var nextRay = Raytracer.SpawnRay(hit, direction);
            estimate += ContinueWalk(nextRay, hit, pdfNext, throughput * weight, depth + 1);
        }

        return estimate;
    }

    protected readonly Scene scene;
    protected readonly RNG rng;
    protected readonly int maxDepth;
    protected bool isOnLightSubpath;
}