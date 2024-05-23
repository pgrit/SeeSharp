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

    public struct DiffuseBsdf {
        RgbColor reflectance;

        public DiffuseBsdf(RgbColor reflectance) => this.reflectance = reflectance;

        public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!SameHemisphere(outDir, inDir))
                return RgbColor.Black;
            return reflectance / MathF.PI;
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWarp.ToCosHemisphere(primarySample);

            // Make sure it ends up on the same hemisphere as the outgoing direction
            if (CosTheta(outDir) < 0)
                local.Direction.Z *= -1;

            return local.Direction;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No transmission
            if (!SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }

    public struct DiffuseTransmission {
        RgbColor transmittance;

        public DiffuseTransmission(RgbColor transmittance) => this.transmittance = transmittance;

        public RgbColor Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No reflection
            if (SameHemisphere(outDir, inDir))
                return RgbColor.Black;
            return transmittance / MathF.PI;
        }

        public Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform primary sample to cosine hemisphere
            var local = SampleWarp.ToCosHemisphere(primarySample);

            // Make sure the sample is in the other hemisphere as the outgoing direction
            if (CosTheta(outDir) > 0)
                local.Direction.Z *= -1;

            return local.Direction;
        }

        public (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // No reflection
            if (SameHemisphere(outDir, inDir))
                return (0, 0);

            float pdf = AbsCosTheta(inDir) / MathF.PI;
            float pdfReverse = AbsCosTheta(outDir) / MathF.PI;
            return (pdf, pdfReverse);
        }
    }

    /// <returns>1/pi * baseColor, or zero if the directions are not in the right hemispheres</returns>
    public override RgbColor Evaluate(in ShadingContext context, Vector3 inDir) {
        ShadingStatCounter.NotifyEvaluate();

        bool shouldReflect = ShouldReflect(context.Point, context.OutDirWorld, inDir);
        inDir = context.WorldToShading(inDir);

        var baseColor = MaterialParameters.BaseColor.Lookup(context.Point.TextureCoordinates);
        if (MaterialParameters.Transmitter && !shouldReflect) {
            return new DiffuseTransmission(baseColor).Evaluate(context.OutDir, inDir, context.IsOnLightSubpath);
        } else if (shouldReflect) {
            return new DiffuseBsdf(baseColor).Evaluate(context.OutDir, inDir, context.IsOnLightSubpath);
        } else
            return RgbColor.Black;
    }

    /// <summary>
    /// Importance samples the cosine hemisphere
    /// </summary>
    public override BsdfSample Sample(in ShadingContext context, Vector2 primarySample, ref ComponentWeights componentWeights) {
        ShadingStatCounter.NotifySample();

        var baseColor = MaterialParameters.BaseColor.Lookup(context.Point.TextureCoordinates);
        Vector3? sample;
        if (MaterialParameters.Transmitter) {
            // Pick either transmission or reflection
            if (primarySample.X < 0.5f) {
                var remapped = primarySample;
                remapped.X = Math.Min(primarySample.X / 0.5f, 1);
                sample = new DiffuseTransmission(baseColor).Sample(context.OutDir, context.IsOnLightSubpath, remapped);
            } else {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - 0.5f) / 0.5f, 1);
                sample = new DiffuseBsdf(baseColor).Sample(context.OutDir, context.IsOnLightSubpath, remapped);
            }
        } else {
            sample = new DiffuseBsdf(baseColor).Sample(context.OutDir, context.IsOnLightSubpath, primarySample);
        }

        // Terminate if no valid direction was sampled
        if (!sample.HasValue)
            return BsdfSample.Invalid;

        var sampledDir = ShadingToWorld(context.Normal, sample.Value);

        // Evaluate all components
        var value = EvaluateWithCosine(context, sampledDir);

        // Compute all pdfs
        var (pdfFwd, pdfRev) = Pdf(context, sampledDir, ref componentWeights);
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
    public override (float, float) Pdf(in ShadingContext context, Vector3 inDir, ref ComponentWeights componentWeights) {
        ShadingStatCounter.NotifyPdfCompute();

        inDir = context.WorldToShading(inDir);

        var baseColor = MaterialParameters.BaseColor.Lookup(context.Point.TextureCoordinates);
        var reflectPdf = new DiffuseBsdf(baseColor).Pdf(context.OutDir, inDir, context.IsOnLightSubpath);
        if (MaterialParameters.Transmitter) {
            var transmitPdf = new DiffuseTransmission(baseColor).Pdf(context.OutDir, inDir, context.IsOnLightSubpath);
            float pdfFwd = (reflectPdf.Item1 + transmitPdf.Item1) * 0.5f;
            float pdfRev = (reflectPdf.Item2 + transmitPdf.Item2) * 0.5f;

            if (componentWeights.Pdfs != null) componentWeights.Pdfs[0] = pdfFwd * 2.0f;
            if (componentWeights.Weights != null) componentWeights.Weights[0] = 0.5f;
            if (componentWeights.PdfsReverse != null) componentWeights.PdfsReverse[0] = pdfRev * 2.0f;
            if (componentWeights.WeightsReverse != null) componentWeights.WeightsReverse[0] = 0.5f;

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
