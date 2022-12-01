using SeeSharp.Shading.Bsdfs;

namespace SeeSharp.Shading.Materials;

/// <summary>
/// A simple Lambertian material that either only reflects or only transmits.
/// </summary>
public class DiffuseMaterial : Material {
    /// <summary>
    /// Specifies the color and whether the material transmits or reflects
    /// </summary>
    public class Parameters {
        /// <summary>
        /// Scattering color
        /// </summary>
        public TextureRgb BaseColor = new TextureRgb(RgbColor.White);

        /// <summary>
        /// If true, only transmittance happens, if false, only reflection
        /// </summary>
        public bool Transmitter = false;
    }

    /// <returns>True if we only transmit</returns>
    public override bool IsTransmissive(in SurfacePoint hit) => MaterialParameters.Transmitter;

    /// <summary>
    /// Creates a new diffuse material with the given parameters
    /// </summary>
    public DiffuseMaterial(Parameters parameters) => MaterialParameters = parameters;

    /// <returns>Always 1</returns>
    public override float GetRoughness(in SurfacePoint hit) => 1;

    /// <returns>Always 1</returns>
    public override float GetIndexOfRefractionRatio(in SurfacePoint hit) => 1;

    /// <returns>The base color</returns>
    public override RgbColor GetScatterStrength(in SurfacePoint hit)
    => MaterialParameters.BaseColor.Lookup(hit.TextureCoordinates);

    /// <returns>1/pi * baseColor, or zero if the directions are not in the right hemispheres</returns>
    public override RgbColor Evaluate(in SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        bool shouldReflect = ShouldReflect(hit, outDir, inDir);

        // Transform directions to shading space and normalize
        var normal = hit.ShadingNormal;
        outDir = WorldToShading(normal, outDir);
        inDir = WorldToShading(normal, inDir);

        var baseColor = MaterialParameters.BaseColor.Lookup(hit.TextureCoordinates);
        if (MaterialParameters.Transmitter && !shouldReflect) {
            return new DiffuseTransmission(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
        } else if (shouldReflect) {
            return new DiffuseBsdf(baseColor).Evaluate(outDir, inDir, isOnLightSubpath);
        } else
            return RgbColor.Black;
    }

    public override int GetNumSamplingLobes(in SurfacePoint hit, Vector3 outDir, Vector3 inDir) => 1;

    public override float GetSamplingLobeWeight(int idx, in SurfacePoint hit, Vector3 outDir, Vector3 inDir) => 1.0f;

    public override (float, float) GetSamplingLobePdf(int idx, in SurfacePoint hit, Vector3 outDir, Vector3 inDir,
                                             bool isOnLightSubpath)
    => Pdf(hit, outDir, inDir, isOnLightSubpath);

    /// <summary>
    /// Importance samples the cosine hemisphere
    /// </summary>
    public override BsdfSample Sample(in SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath,
                                      Vector2 primarySample) {
        var normal = hit.ShadingNormal;
        outDir = WorldToShading(normal, outDir);

        var baseColor = MaterialParameters.BaseColor.Lookup(hit.TextureCoordinates);
        Vector3? sample;
        if (MaterialParameters.Transmitter) {
            // Pick either transmission or reflection
            if (primarySample.X < 0.5f) {
                var remapped = primarySample;
                remapped.X = Math.Min(primarySample.X / 0.5f, 1);
                sample = new DiffuseTransmission(baseColor).Sample(outDir, isOnLightSubpath, remapped);
            } else {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - 0.5f) / 0.5f, 1);
                sample = new DiffuseBsdf(baseColor).Sample(outDir, isOnLightSubpath, remapped);
            }
        } else {
            sample = new DiffuseBsdf(baseColor).Sample(outDir, isOnLightSubpath, primarySample);
        }

        // Terminate if no valid direction was sampled
        if (!sample.HasValue)
            return BsdfSample.Invalid;

        var sampledDir = ShadingToWorld(normal, sample.Value);

        // Evaluate all components
        var outWorld = ShadingToWorld(normal, outDir);
        var value = EvaluateWithCosine(hit, outWorld, sampledDir, isOnLightSubpath);

        // Compute all pdfs
        var (pdfFwd, pdfRev) = Pdf(hit, outWorld, sampledDir, isOnLightSubpath);
        if (pdfFwd == 0)
            return BsdfSample.Invalid;

        // Combine results with balance heuristic MIS
        return new BsdfSample {
            Pdf = pdfFwd,
            PdfReverse = pdfRev,
            Weight = value / pdfFwd,
            Direction = sampledDir
        };
    }

    /// <returns>PDF value used by <see cref="Sample"/></returns>
    public override (float, float) Pdf(in SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
        // Transform directions to shading space and normalize
        var normal = hit.ShadingNormal;
        outDir = WorldToShading(normal, outDir);
        inDir = WorldToShading(normal, inDir);

        var baseColor = MaterialParameters.BaseColor.Lookup(hit.TextureCoordinates);
        var reflectPdf = new DiffuseBsdf(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
        if (MaterialParameters.Transmitter) {
            var transmitPdf = new DiffuseTransmission(baseColor).Pdf(outDir, inDir, isOnLightSubpath);
            float pdfFwd = (reflectPdf.Item1 + transmitPdf.Item1) * 0.5f;
            float pdfRev = (reflectPdf.Item2 + transmitPdf.Item2) * 0.5f;
            return (pdfFwd, pdfRev);
        } else {
            return reflectPdf;
        }
    }

    /// <summary>
    /// Material parameters
    /// </summary>
    public readonly Parameters MaterialParameters;
}
