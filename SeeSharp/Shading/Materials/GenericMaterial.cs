using SeeSharp.Shading.MicrofacetDistributions;

namespace SeeSharp.Shading.Materials;

public partial class GenericMaterial  : Material
{
    Parameters parameters;
    public Parameters MaterialParameters
    {
        get => parameters;
        set
        {
            parameters = value;
            if (parameters.IndexOfRefraction < 1.01f)
            {
                parameters.IndexOfRefraction = 1.01f;
                Logger.Warning($"Changed IOR to the minimum allowed value of 1.01 for material {Name}");
            }
        }
    }

    public GenericMaterial(Parameters parameters) => MaterialParameters = parameters;

    /// <summary>
    /// Parameters of the generic material
    /// </summary>
    public class Parameters {
        /// <summary>
        /// Textured or constant surface color. Affects diffuse reflections and also specular ones,
        /// if <see cref="SpecularTintStrength"/> is greater than zero.
        /// </summary>
        public TextureRgb BaseColor = new(RgbColor.White);

        /// <summary>
        /// Textured or constant surface roughness. Perfect mirrors would be 0 and purely diffuse 1.
        /// </summary>
        public TextureMono Roughness = new(0.5f);

        /// <summary>
        /// Interpolates between conductor and dielectric Fresnel.
        /// </summary>
        public float Metallic = 0.0f;

        /// <summary>
        /// How much the base color affects specular reflections. If 0, specular reflections will be white.
        /// </summary>
        public float SpecularTintStrength = 1.0f;

        /// <summary>
        /// Amount of anisotropy in the glossy microfacet components
        /// </summary>
        public float Anisotropic = 0.0f;

        /// <summary>
        /// If greater than one, the surface transmits with a rough GGX dielectric
        /// </summary>
        public float SpecularTransmittance = 0.0f;

        /// <summary>
        /// IOR of the material "below" the surface. Assumes that the exterior is always vacuum (1).
        /// </summary>
        public float IndexOfRefraction = 1.45f;
    }

    public override float GetIndexOfRefractionRatio(in SurfacePoint hit) => parameters.IndexOfRefraction;
    public override float GetRoughness(in SurfacePoint hit) => parameters.Roughness.Lookup(hit.TextureCoordinates);
    public override RgbColor GetScatterStrength(in SurfacePoint hit) => parameters.BaseColor.Lookup(hit.TextureCoordinates);
    public override bool IsTransmissive(in SurfacePoint hit) => parameters.SpecularTransmittance > 0;
    public override int MaxSamplingComponents => 3;

    public override RgbColor Evaluate(in ShadingContext context, Vector3 inDir)
    {
        ShadingStatCounter.NotifyEvaluate();
        inDir = context.WorldToShading(inDir);
        ComponentWeights c = new();
        return ComputeValueAndPdf(context, inDir, ComputeLocalParams(context), ref c).Value;
    }

    public override RgbColor EvaluateWithCosine(in ShadingContext context, Vector3 inDir) {
        ShadingStatCounter.NotifyEvaluate();
        inDir = context.WorldToShading(inDir);
        ComponentWeights c = new();
        return ComputeValueAndPdf(context, inDir, ComputeLocalParams(context), ref c).Value * float.Abs(inDir.Z);
    }

    public override (float Pdf, float PdfReverse) Pdf(in ShadingContext context, Vector3 inDir, ref ComponentWeights componentWeights)
    {
        ShadingStatCounter.NotifyPdfCompute();
        inDir = context.WorldToShading(inDir);
        var v = ComputeValueAndPdf(context, inDir, ComputeLocalParams(context), ref componentWeights);
        return (v.Pdf, v.PdfReverse);
    }

    public override BsdfSample Sample(in ShadingContext context, float primaryComponent, Vector2 primaryDirection, ref ComponentWeights componentWeights) {
        ShadingStatCounter.NotifySample();

        if (context.OutDir.Z == 0) {
            // early exit to prevent NaNs in the microfacet code
            return BsdfSample.Invalid;
        }

        var localParams = ComputeLocalParams(context);

        TrowbridgeReitzDistribution normalDistribution = new() {
            AlphaX = localParams.alphaX,
            AlphaY = localParams.alphaY
        };
        float eta = context.OutDir.Z > 0 ? (1 / parameters.IndexOfRefraction) : parameters.IndexOfRefraction;

        // Select a component to sample from and remap the primary sample coordinate
        float schlickFresnel = FresnelSchlick.Evaluate(localParams.specularReflectanceAtNormal, float.Abs(context.OutDir.Z)).Average;
        float diffuseBias = float.Max(1 - schlickFresnel, 0.75f);
        float diffuseWeight = diffuseBias * (1 - parameters.Metallic) * (1 - parameters.SpecularTransmittance);

        // Sample a direction from the selected component
        Vector3 inDir;
        if (primaryComponent <= diffuseWeight) {
            // Sample diffuse
            inDir = SampleWarp.ToCosHemisphere(primaryDirection).Direction;
            if (context.OutDir.Z < 0)
                inDir.Z *= -1;
        } else {
            Vector3 halfVector = normalDistribution.Sample(context.OutDir, primaryDirection);

            var cOut = Vector3.Dot(context.OutDir, (halfVector.Z < 0) ? -halfVector : halfVector);
            var fresnel = FresnelDielectric.Evaluate(cOut, 1, parameters.IndexOfRefraction);
            float selectTransmit = (1 - fresnel) * parameters.SpecularTransmittance;
            if ((primaryComponent - diffuseWeight) / (1 - diffuseWeight) < selectTransmit) {
                // Transmission
                if (Vector3.Dot(context.OutDir, halfVector) < 0)
                    return BsdfSample.Invalid; // prevent NaN
                var i = Refract(context.OutDir, halfVector, eta);
                if (!i.HasValue)
                    return BsdfSample.Invalid; // TODO check if / how often this occurs
                inDir = i.Value;
            } else {
                // Reflection
                inDir = Reflect(context.OutDir, halfVector);
            }
        }

        var eval = ComputeValueAndPdf(context, inDir, localParams, ref componentWeights);

        // Debug.Assert(eval.Value == RgbColor.Black || float.IsFinite((eval.Value * float.Abs(inDir.Z) / eval.Pdf).Average));

        return new BsdfSample {
            Pdf = eval.Pdf,
            PdfReverse = eval.PdfReverse,
            Weight = eval.Value * float.Abs(inDir.Z) / eval.Pdf,
            Direction = context.ShadingToWorld(inDir)
        };
    }

    (RgbColor Value, float Pdf, float PdfReverse) ComputeValueAndPdf(in ShadingContext context, in Vector3 inDir, in LocalParams localParams, ref ComponentWeights components)
    {
        TrowbridgeReitzDistribution normalDistribution = new()
        {
            AlphaX = localParams.alphaX,
            AlphaY = localParams.alphaY
        };

        float cosThetaO = MathF.Abs(context.OutDir.Z);
        float cosThetaI = MathF.Abs(inDir.Z);

        bool sameGeometricHemisphere = ShouldReflect(context.Point, context.OutDirWorld, context.ShadingToWorld(inDir));

        RgbColor bsdfValue = RgbColor.Black;

        Span<float> pdfsFwd = stackalloc float[3];
        Span<float> pdfsRev = stackalloc float[3];

        // Diffuse component
        float fresnelOut = FresnelSchlick.SchlickWeight(cosThetaO);
        float fresnelIn = FresnelSchlick.SchlickWeight(cosThetaI);
        if (SameHemisphere(context.OutDir, inDir))
        {
            if (sameGeometricHemisphere) {
                var diffuse = localParams.diffuseReflectance / MathF.PI * (1 - fresnelOut * 0.5f) * (1 - fresnelIn * 0.5f);
                bsdfValue += diffuse;
                if (!components.Values.IsEmpty)
                    components.Values[0] = diffuse;
            }
            pdfsFwd[0] = cosThetaI / MathF.PI;
            pdfsRev[0] = cosThetaO / MathF.PI;
        }

        // Microfacet reflection and retro reflection
        var halfVector = context.OutDir + inDir;
        if (halfVector != Vector3.Zero) // Skip degenerate cases
        {
            halfVector = Vector3.Normalize(halfVector);

            // Retro-reflectance; Burley 2015, eq (4).
            float cIn = Vector3.Dot(inDir, halfVector);
            float Rr = 2 * localParams.roughness * cIn * cIn;
            if (SameHemisphere(context.OutDir, inDir) && sameGeometricHemisphere) {
                var retro = localParams.retroReflectance / MathF.PI * Rr * (fresnelOut + fresnelIn + fresnelOut * fresnelIn * (Rr - 1));
                bsdfValue += retro;
                if (!components.Values.IsEmpty)
                    components.Values[0] += retro; // we don't sample retro, so it is only covered by cos hemisphere sampling
            }

            // Microfacet reflection
            if (cosThetaI != 0 && cosThetaO != 0) // Skip degenerate cases
            {
                float cOut = Vector3.Dot(context.OutDir, halfVector);
                // The microfacet model only contributes in the upper hemisphere
                if (SameHemisphere(context.OutDir, inDir) && sameGeometricHemisphere && cIn * cOut > 0) {
                    // For the Fresnel computation only, make sure that wh is in the same hemisphere as the surface normal,
                    // so that total internal reflection is handled correctly.
                    var cosHalfVectorTIR = Vector3.Dot(inDir, (halfVector.Z < 0) ? -halfVector : halfVector);

                    var diel = new RgbColor(FresnelDielectric.Evaluate(cosHalfVectorTIR, 1, parameters.IndexOfRefraction));
                    var schlick = FresnelSchlick.Evaluate(localParams.specularReflectanceAtNormal, cosHalfVectorTIR);
                    var fresnel = RgbColor.Lerp(parameters.Metallic, diel, schlick);

                    var reflect = localParams.specularTint
                        * normalDistribution.NormalDistribution(halfVector) * normalDistribution.MaskingShadowing(context.OutDir, inDir)
                        * fresnel / (4 * cosThetaI * cosThetaO);

                    bsdfValue += reflect;

                    if (!components.Values.IsEmpty)
                        components.Values[1] += reflect;
                }

                // but its PDF "leaks" to the other side -- we compute the PDF for those samples for completeness
                // (required for fancy stuff like optimal MIS weight computation)
                float reflectJacobianFwd = Math.Abs(4 * cOut);
                float reflectJacobianRev = Math.Abs(4 * cIn);
                if (reflectJacobianFwd != 0.0f && reflectJacobianRev != 0.0f) // Prevent NaNs from degenerate cases
                {
                    pdfsFwd[1] = normalDistribution.Pdf(context.OutDir, halfVector) / reflectJacobianFwd;
                    pdfsRev[1] = normalDistribution.Pdf(inDir, halfVector) / reflectJacobianRev;
                }
            }
        }

        // Microfacet transmission
        float eta = context.OutDir.Z > 0 ? parameters.IndexOfRefraction : (1 / parameters.IndexOfRefraction);
        var halfVectorTransmit = context.OutDir + inDir * eta;
        Vector3 halfVectorTransmitIn = Vector3.Zero;

        if (cosThetaO != 0 && cosThetaI != 0 && halfVectorTransmit != Vector3.Zero && parameters.SpecularTransmittance > 0) // Skip degenerate cases
        {
            halfVectorTransmit = Vector3.Normalize(halfVectorTransmit);
            // Flip the half vector to the upper hemisphere for consistent computations
            halfVectorTransmit = (halfVectorTransmit.Z < 0) ? -halfVectorTransmit : halfVectorTransmit;

            float cOut = Vector3.Dot(context.OutDir, halfVectorTransmit);
            float cIn = Vector3.Dot(inDir, halfVectorTransmit);
            float sqrtDenom = cOut + eta * cIn;

            // The BSDF value for transmission is only non-zero if the directions are in different hemispheres
            if (!SameHemisphere(context.OutDir, inDir) && cOut * cIn < 0) {
                var F = new RgbColor(FresnelDielectric.Evaluate(cOut, 1, parameters.IndexOfRefraction));
                float factor = context.IsOnLightSubpath ? (1 / eta) : 1;

                var wh = (!SameHemisphere(context.OutDir, halfVectorTransmit)) ? -halfVectorTransmit : halfVectorTransmit;

                var numerator = normalDistribution.NormalDistribution(wh) * normalDistribution.MaskingShadowing(context.OutDir, inDir);
                numerator *= eta * eta * Math.Max(0, Vector3.Dot(inDir, -wh)) * Math.Max(0, Vector3.Dot(context.OutDir, wh));
                numerator *= factor * factor;

                var denom = inDir.Z * context.OutDir.Z * sqrtDenom * sqrtDenom;
                Debug.Assert(float.IsFinite(denom));
                var transmit = (RgbColor.White - F) * localParams.specularTransmittance * Math.Abs(numerator / denom);
                bsdfValue += transmit;

                if (!components.Values.IsEmpty)
                    components.Values[2] += transmit;
            }

            // The transmission PDF
            if (sqrtDenom != 0)  // Prevent NaN in corner case
            {
                var wh = (!SameHemisphere(context.OutDir, halfVectorTransmit)) ? -halfVectorTransmit : halfVectorTransmit;
                float jacobian = eta * eta * Math.Max(0, Vector3.Dot(inDir, -wh)) / (sqrtDenom * sqrtDenom);
                pdfsFwd[2] += normalDistribution.Pdf(context.OutDir, wh) * jacobian;
            }

            // For the reverse PDF, we first need to compute the corresponding half vector
            float etaIn = inDir.Z > 0 ? parameters.IndexOfRefraction : (1 / parameters.IndexOfRefraction);
            halfVectorTransmitIn = context.OutDir * etaIn + inDir;
            halfVectorTransmitIn = Vector3.Normalize(halfVectorTransmitIn);
            halfVectorTransmitIn = (halfVectorTransmitIn.Z < 0) ? -halfVectorTransmitIn : halfVectorTransmitIn;

            if (halfVectorTransmitIn != Vector3.Zero)  // Prevent NaN if outDir and inDir exactly align
            {
                var wh = (!SameHemisphere(inDir, halfVectorTransmitIn)) ? -halfVectorTransmitIn : halfVectorTransmitIn;
                float sqrtDenomIn = Vector3.Dot(halfVectorTransmitIn, inDir) + etaIn * Vector3.Dot(context.OutDir, halfVectorTransmitIn);
                if (sqrtDenomIn != 0)  // Prevent NaN in corner case
                {
                    float jacobian = etaIn * etaIn * Math.Max(0, Vector3.Dot(context.OutDir, -wh)) / (sqrtDenomIn * sqrtDenomIn);
                    pdfsRev[2] += normalDistribution.Pdf(inDir, wh) * jacobian;
                }
            }
        }

        // Compute the component selection probabilities
        float schlickFresnel = FresnelSchlick.Evaluate(localParams.specularReflectanceAtNormal, float.Abs(context.OutDir.Z)).Average;
        float diffuseBias = float.Max(1 - schlickFresnel, 0.75f);
        float diffuseWeight = diffuseBias * (1 - parameters.Metallic) * (1 - parameters.SpecularTransmittance);
        var fresnelR = FresnelDielectric.Evaluate(Vector3.Dot(context.OutDir, (halfVector.Z < 0) ? -halfVector : halfVector), 1, parameters.IndexOfRefraction);
        float selectReflect = (1 - diffuseWeight) * (1 - (1 - fresnelR) * parameters.SpecularTransmittance);

        var fresnelT = FresnelDielectric.Evaluate(Vector3.Dot(context.OutDir, halfVectorTransmit), 1, parameters.IndexOfRefraction);
        float selectTransmit = (1 - diffuseWeight) * (1 - fresnelT) * parameters.SpecularTransmittance;

        var fresnelTIn = FresnelDielectric.Evaluate(Vector3.Dot(inDir, halfVectorTransmitIn), 1, parameters.IndexOfRefraction);
        float selectTransmitIn = (1 - diffuseWeight) * (1 - fresnelT) * parameters.SpecularTransmittance;

        float pdf = pdfsFwd[0] * diffuseWeight + pdfsFwd[1] * selectReflect + pdfsFwd[2] * selectTransmit;
        float pdfReverse = pdfsRev[0] * diffuseWeight + pdfsRev[1] * selectReflect + pdfsRev[2] * selectTransmitIn;

        components.NumComponents = 3;
        components.NumComponentsReverse = 3;
        if (!components.Pdfs.IsEmpty)
        {
            components.Pdfs[0] = pdfsFwd[0];
            components.Pdfs[1] = pdfsFwd[1];
            components.Pdfs[2] = pdfsFwd[2];
            components.Weights[0] = diffuseWeight;
            components.Weights[1] = selectReflect;
            components.Weights[2] = selectTransmit;
        }
        if (!components.PdfsReverse.IsEmpty)
        {
            components.PdfsReverse[0] = pdfsRev[0];
            components.PdfsReverse[1] = pdfsRev[1];
            components.PdfsReverse[2] = pdfsRev[2];
            components.WeightsReverse[0] = diffuseWeight;
            components.WeightsReverse[1] = selectReflect;
            components.WeightsReverse[2] = selectTransmitIn;
        }

        // Debug.Assert(bsdfValue == RgbColor.Black || float.IsFinite((bsdfValue * float.Abs(inDir.Z) / pdf).Average));

        return (bsdfValue, pdf, pdfReverse);
    }

    [System.Runtime.CompilerServices.InlineArray(3)]
    struct SelectionWeights { float _first; }

    struct LocalParams
    {
        public RgbColor colorTint, specularTint;
        public float roughness;
        public float alphaX, alphaY;
        public RgbColor diffuseReflectance;
        public RgbColor retroReflectance;
        public RgbColor specularTransmittance;
        public RgbColor specularReflectanceAtNormal;
    }

    LocalParams ComputeLocalParams(in ShadingContext shadingContext)
    {
        LocalParams result = new();

        // Compute colors and tints
        var baseColor = parameters.BaseColor.Lookup(shadingContext.Point.TextureCoordinates);
        float luminance = baseColor.Luminance;
        result.colorTint = luminance > 0 ? (baseColor / luminance) : RgbColor.White;
        result.specularTint = RgbColor.Lerp(parameters.SpecularTintStrength, RgbColor.White, result.colorTint);

        // Microfacet distribution parameters
        result.roughness = parameters.Roughness.Lookup(shadingContext.Point.TextureCoordinates);
        float aspect = MathF.Sqrt(1 - parameters.Anisotropic * .9f);
        result.alphaX = Math.Max(.001f, result.roughness * result.roughness / aspect);
        result.alphaY = Math.Max(.001f, result.roughness * result.roughness * aspect);

        // Fresnel term parameters
        result.specularReflectanceAtNormal = RgbColor.Lerp(parameters.Metallic,
            FresnelSchlick.SchlickR0FromEta(parameters.IndexOfRefraction) * result.specularTint,
            baseColor);

        float diffuseWeight = (1 - parameters.Metallic) * (1 - parameters.SpecularTransmittance);
        result.diffuseReflectance = baseColor * diffuseWeight;
        result.specularTransmittance = parameters.SpecularTransmittance * baseColor;
        result.retroReflectance = baseColor * diffuseWeight;

        return result;
    }
}