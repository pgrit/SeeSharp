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

    public override BsdfSample Sample(in ShadingContext context, Vector2 primarySample, ref ComponentWeights componentWeights)
    {
        ShadingStatCounter.NotifySample();

        if (context.OutDir.Z == 0)
        {
            // early exit to prevent NaNs in the microfacet code
            return BsdfSample.Invalid;
        }

        var localParams = ComputeLocalParams(context);

        TrowbridgeReitzDistribution normalDistribution = new()
        {
            AlphaX = localParams.alphaX,
            AlphaY = localParams.alphaY
        };
        float eta = context.OutDir.Z > 0 ? (1 / parameters.IndexOfRefraction) : parameters.IndexOfRefraction;

        // Select a component to sample from and remap the primary sample coordinate
        int c;
        if (primarySample.X <= localParams.SelectionWeightsForward[0])
        {
            c = 0;
            primarySample.X = Math.Min(primarySample.X / localParams.SelectionWeightsForward[0], 1);
        }
        else if (primarySample.X <= localParams.SelectionWeightsForward[0] + localParams.SelectionWeightsForward[1])
        {
            c = 1;
            primarySample.X = Math.Min((primarySample.X - localParams.SelectionWeightsForward[0]) / localParams.SelectionWeightsForward[1], 1);
        }
        else
        {
            c = 2;
            primarySample.X = Math.Min((primarySample.X - localParams.SelectionWeightsForward[0] - localParams.SelectionWeightsForward[1]) / localParams.SelectionWeightsForward[2], 1);
        }

        // Sample a direction from the selected component
        Vector3 inDir;
        if (c == 0)
        {
            // Sample diffuse
            inDir = SampleWarp.ToCosHemisphere(primarySample).Direction;
            if (context.OutDir.Z < 0)
                inDir.Z *= -1;
        }
        else if (c == 1)
        {
            // Sample specular reflection
            Vector3 halfVector = normalDistribution.Sample(context.OutDir, primarySample);
            // Debug.Assert(normalDistribution.Pdf(context.OutDir, halfVector) > 0);
            inDir = Reflect(context.OutDir, halfVector);
        }
        else
        {
            // Sample specular transmission
            Vector3 halfVector = normalDistribution.Sample(context.OutDir, primarySample);
            if (Vector3.Dot(context.OutDir, halfVector) < 0)
                return BsdfSample.Invalid; // prevent NaN
            var i = Refract(context.OutDir, halfVector, eta);
            if (!i.HasValue)
                i = Reflect(context.OutDir, halfVector);
            inDir = i.Value;
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
        SelectionWeights selectionWeightsReverse = new();
        ComputeSelectWeights(localParams, inDir, ref selectionWeightsReverse);

        bool sameGeometricHemisphere = ShouldReflect(context.Point, context.OutDirWorld, context.ShadingToWorld(inDir));

        RgbColor bsdfValue = RgbColor.Black;

        Span<float> pdfsFwd = stackalloc float[3];
        Span<float> pdfsRev = stackalloc float[3];

        // Diffuse component
        float fresnelOut = FresnelSchlick.SchlickWeight(cosThetaO);
        float fresnelIn = FresnelSchlick.SchlickWeight(cosThetaI);
        if (SameHemisphere(context.OutDir, inDir))
        {
            if (sameGeometricHemisphere)
                bsdfValue += localParams.diffuseReflectance / MathF.PI * (1 - fresnelOut * 0.5f) * (1 - fresnelIn * 0.5f);
            pdfsFwd[0] = cosThetaI / MathF.PI;
            pdfsRev[0] = cosThetaO / MathF.PI;
        }

        // Microfacet reflection and retro reflection
        var halfVector = context.OutDir + inDir;
        if (halfVector != Vector3.Zero) // Skip degenerate cases
        {
            halfVector = Vector3.Normalize(halfVector);

            // Retro-reflectance; Burley 2015, eq (4).
            float cosThetaD = Vector3.Dot(inDir, halfVector);
            float Rr = 2 * localParams.roughness * cosThetaD * cosThetaD;
            if (SameHemisphere(context.OutDir, inDir) && sameGeometricHemisphere)
                bsdfValue += localParams.retroReflectance / MathF.PI * Rr * (fresnelOut + fresnelIn + fresnelOut * fresnelIn * (Rr - 1));

            // Microfacet reflection
            if (cosThetaI != 0 && cosThetaO != 0) // Skip degenerate cases
            {
                // The microfacet model only contributes in the upper hemisphere
                if (SameHemisphere(context.OutDir, inDir) && sameGeometricHemisphere)
                {
                    // For the Fresnel computation only, make sure that wh is in the same hemisphere as the surface normal,
                    // so that total internal reflection is handled correctly.
                    var cosHalfVectorTIR = Vector3.Dot(inDir, (halfVector.Z < 0) ? -halfVector : halfVector);
                    var diel = new RgbColor(FresnelDielectric.Evaluate(cosHalfVectorTIR, 1, parameters.IndexOfRefraction));
                    var schlick = FresnelSchlick.Evaluate(localParams.specularReflectanceAtNormal, cosHalfVectorTIR);
                    var fresnel = RgbColor.Lerp(parameters.Metallic, diel, schlick);

                    bsdfValue += localParams.specularTint
                        * normalDistribution.NormalDistribution(halfVector) * normalDistribution.MaskingShadowing(context.OutDir, inDir)
                        * fresnel / (4 * cosThetaI * cosThetaO);
                }

                // but its PDF "leaks" to the other side -- we compute the PDF for those samples for completeness
                // (required for fancy stuff like optimal MIS weight computation)
                float reflectJacobianFwd = Math.Abs(4 * Vector3.Dot(context.OutDir, halfVector));
                float reflectJacobianRev = Math.Abs(4 * cosThetaD);
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

        if (cosThetaO != 0 && cosThetaI != 0 && halfVector != Vector3.Zero && halfVectorTransmit != Vector3.Zero && parameters.SpecularTransmittance > 0) // Skip degenerate cases
        {
            halfVectorTransmit = Vector3.Normalize(halfVectorTransmit);
            // Flip the half vector to the upper hemisphere for consistent computations
            halfVectorTransmit = (halfVectorTransmit.Z < 0) ? -halfVectorTransmit : halfVectorTransmit;

            float sqrtDenom = Vector3.Dot(context.OutDir, halfVectorTransmit) + eta * Vector3.Dot(inDir, halfVectorTransmit);

            // The BSDF value for transmission is only non-zero if the directions are in different hemispheres
            if (!SameHemisphere(context.OutDir, inDir))
            {
                var F = new RgbColor(FresnelDielectric.Evaluate(Vector3.Dot(context.OutDir, halfVectorTransmit), 1, parameters.IndexOfRefraction));
                float factor = context.IsOnLightSubpath ? (1 / eta) : 1;

                var numerator = normalDistribution.NormalDistribution(halfVectorTransmit) * normalDistribution.MaskingShadowing(context.OutDir, inDir);
                numerator *= eta * eta * Math.Abs(Vector3.Dot(inDir, halfVectorTransmit)) * Math.Abs(Vector3.Dot(context.OutDir, halfVectorTransmit));
                numerator *= factor * factor;

                var denom = inDir.Z * context.OutDir.Z * sqrtDenom * sqrtDenom;
                Debug.Assert(float.IsFinite(denom));
                bsdfValue += (RgbColor.White - F) * localParams.specularTransmittance * Math.Abs(numerator / denom);
            }

            // If total reflection occured, we switch to reflection sampling
            float cos = Vector3.Dot(halfVector, context.OutDir);
            if (1 / (eta * eta) * MathF.Max(0, 1 - cos * cos) >= 1) // Total internal reflection occurs for this outgoing direction
            {
                pdfsFwd[2] = pdfsFwd[1];
            }

            // Same for reversed sampling
            float cosIn = Vector3.Dot(halfVector, inDir);
            float etaIn = inDir.Z > 0 ? parameters.IndexOfRefraction : (1 / parameters.IndexOfRefraction);
            if (1 / (etaIn * etaIn) * MathF.Max(0, 1 - cosIn * cosIn) >= 1) // Total internal reflection occurs for this outgoing direction
            {
                pdfsRev[2] = pdfsRev[1];
            }

            // The transmission PDF
            if (sqrtDenom != 0)  // Prevent NaN in corner case
            {
                var wh = (!SameHemisphere(context.OutDir, halfVectorTransmit)) ? -halfVectorTransmit : halfVectorTransmit;
                float jacobian = eta * eta * Math.Max(0, Vector3.Dot(inDir, -wh)) / (sqrtDenom * sqrtDenom);
                pdfsFwd[2] += normalDistribution.Pdf(context.OutDir, wh) * jacobian;
            }

            // For the reverse PDF, we first need to compute the corresponding half vector
            Vector3 halfVectorRev = inDir + context.OutDir * etaIn;
            if (halfVectorRev != Vector3.Zero)  // Prevent NaN if outDir and inDir exactly align
            {
                halfVectorRev = Vector3.Normalize(halfVectorRev);
                halfVectorRev = (!SameHemisphere(inDir, halfVectorRev)) ? -halfVectorRev : halfVectorRev;

                float sqrtDenomIn = Vector3.Dot(inDir, halfVectorRev) + etaIn * Vector3.Dot(context.OutDir, halfVectorRev);
                if (sqrtDenomIn != 0)  // Prevent NaN in corner case
                {
                    float jacobian = etaIn * etaIn * Math.Max(0, Vector3.Dot(context.OutDir, -halfVectorRev)) / (sqrtDenomIn * sqrtDenomIn);
                    pdfsRev[2] += normalDistribution.Pdf(inDir, halfVectorRev) * jacobian;
                }
            }
        }

        float pdf = pdfsFwd[0] * localParams.SelectionWeightsForward[0] + pdfsFwd[1] * localParams.SelectionWeightsForward[1] + pdfsFwd[2] * localParams.SelectionWeightsForward[2];
        float pdfReverse = pdfsRev[0] * selectionWeightsReverse[0] + pdfsRev[1] * selectionWeightsReverse[1] + pdfsRev[2] * selectionWeightsReverse[2];

        components.NumComponents = 3;
        components.NumComponentsReverse = 3;
        if (components.Pdfs != null)
        {
            components.Pdfs[0] = pdfsFwd[0];
            components.Pdfs[1] = pdfsFwd[1];
            components.Pdfs[2] = pdfsFwd[2];
            components.Weights[0] = localParams.SelectionWeightsForward[0];
            components.Weights[1] = localParams.SelectionWeightsForward[1];
            components.Weights[2] = localParams.SelectionWeightsForward[2];
        }
        if (components.PdfsReverse != null)
        {
            components.PdfsReverse[0] = pdfsRev[0];
            components.PdfsReverse[1] = pdfsRev[1];
            components.PdfsReverse[2] = pdfsRev[2];
            components.WeightsReverse[0] = selectionWeightsReverse[0];
            components.WeightsReverse[1] = selectionWeightsReverse[1];
            components.WeightsReverse[2] = selectionWeightsReverse[2];
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
        public float diffuseWeight;
        public RgbColor diffuseReflectance;
        public RgbColor retroReflectance;
        public RgbColor specularTransmittance;
        public RgbColor specularReflectanceAtNormal;
        public SelectionWeights SelectionWeightsForward;
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

        result.diffuseWeight = (1 - parameters.Metallic) * (1 - parameters.SpecularTransmittance);
        result.diffuseReflectance = baseColor * result.diffuseWeight;
        result.specularTransmittance = parameters.SpecularTransmittance * baseColor;
        result.retroReflectance = baseColor * result.diffuseWeight;

        ComputeSelectWeights(result, shadingContext.OutDir, ref result.SelectionWeightsForward);

        return result;
    }

    void ComputeSelectWeights(LocalParams p, Vector3 outDir, ref SelectionWeights weights)
    {
        var diel = new RgbColor(FresnelDielectric.Evaluate(outDir.Z, 1, parameters.IndexOfRefraction));
        var schlick = FresnelSchlick.Evaluate(p.specularReflectanceAtNormal, outDir.Z);
        float f = Math.Clamp(RgbColor.Lerp(parameters.Metallic, diel, schlick).Average, 0.2f, 0.8f);

        float metallicBRDF = parameters.Metallic;
        float specularBSDF = (1.0f - parameters.Metallic) * parameters.SpecularTransmittance;
        float dielectricBRDF = (1.0f - parameters.SpecularTransmittance) * (1.0f - parameters.Metallic);

        float specularWeight = f * (metallicBRDF + dielectricBRDF + specularBSDF);
        float transmissionWeight = (1 - f) * specularBSDF;
        float diffuseWeight = p.diffuseWeight;

        float norm = 1.0f / (specularWeight + transmissionWeight + diffuseWeight);

        weights[0] = diffuseWeight * norm;
        weights[1] = specularWeight * norm;
        weights[2] = transmissionWeight * norm;

        Debug.Assert(float.IsFinite(weights[0]));
    }
}