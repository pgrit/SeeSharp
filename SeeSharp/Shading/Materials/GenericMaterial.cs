using SeeSharp.Shading.MicrofacetDistributions;

namespace SeeSharp.Shading.Materials;

/// <summary>
/// Basic uber-shader surface material that should suffice for most integrator experiments.
/// Simplified version of the Disney BSDF without sheen and clearcoat and limited texturing capabilities.
/// </summary>
public class GenericMaterial : Material {
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

        /// <summary>
        /// If set to true, transmission will also happen diffusely, i.e., this is a translucent
        /// material like paper.
        /// </summary>
        public bool Thin = false;

        /// <summary>
        /// Scaling factor for how much light is diffusely transmitted through the surface. Only
        /// relvant if <see cref="Thin"/> is true.
        /// </summary>
        public float DiffuseTransmittance = 0.0f;
    }

    /// <param name="parameters">Properties of the material</param>
    public GenericMaterial(Parameters parameters) {
        this.MaterialParameters = parameters;
    }

    /// <returns>The textured roughness value at the hit point </returns>
    public override float GetRoughness(in SurfacePoint hit) => GetRoughness(hit.TextureCoordinates);

    /// <returns>True if the material is "thin" or the specular transmittance is not zero</returns>
    public override bool IsTransmissive(in SurfacePoint hit)
    => (MaterialParameters.Thin && MaterialParameters.DiffuseTransmittance > 0) || MaterialParameters.SpecularTransmittance > 0;

    float GetRoughness(Vector2 texCoords)
    => MaterialParameters.Roughness.Lookup(texCoords);

    /// <returns>Interior IOR, assumes outside is vacuum</returns>
    public override float GetIndexOfRefractionRatio(in SurfacePoint hit)
    => MaterialParameters.IndexOfRefraction;

    /// <returns>The base color, which is a crude approximation of the scattering strength</returns>
    public override RgbColor GetScatterStrength(in SurfacePoint hit)
    => MaterialParameters.BaseColor.Lookup(hit.TextureCoordinates);

    /// <returns>BSDF value</returns>
    public override RgbColor Evaluate(in ShadingContext context, Vector3 inDir) {
        var ctx = MakeContext(context);
        var evalCtx = MakeEvalContext(context, ctx, inDir);
        return Evaluate(ctx, evalCtx);
    }

    RgbColor Evaluate(in GenericMaterialContext context, in EvalContext evalContext) {
        ShadingStats.NotifyEvaluate();

        // Evaluate all components
        bool isOnLightSubpath = context.ShadingContext.IsOnLightSubpath;
        var result = RgbColor.Black;
        if (context.LocalParameters.diffuseWeight > 0 && evalContext.SameHemisphere) {
            result += context.DiffuseComponent.Evaluate(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
            result += context.RetroComponent.Evaluate(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
        }
        if (MaterialParameters.SpecularTransmittance > 0 && !evalContext.SameHemisphere) {
            result += context.MicroTransmitComponent.Evaluate(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
        }
        if (MaterialParameters.Thin && !evalContext.SameHemisphere) {
            result += context.DiffuseTransmitComponent.Evaluate(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
        }
        if (evalContext.SameHemisphere) {
            result += context.MicroReflectComponent.Evaluate(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
        }

        Debug.Assert(float.IsFinite(result.Average) && result.Average >= 0);

        return result;
    }

    public override void PopulateContext(ref ShadingContext context) {
    }

    GenericMaterialContext MakeContext(in ShadingContext shadingContext) {
        GenericMaterialContext context = new();
        context.ShadingContext = shadingContext;
        context.LocalParameters = ComputeLocalParams(shadingContext.Point.TextureCoordinates);
        context.SelectionWeightsForward = ComputeSelectWeights(context.LocalParameters, shadingContext.OutDir);
        context.RetroComponent = new DisneyRetroReflection(context.LocalParameters.retroReflectance, context.LocalParameters.roughness);
        context.DiffuseComponent = new DisneyDiffuse(context.LocalParameters.diffuseReflectance);
        context.DiffuseTransmitComponent = new DiffuseTransmission(context.LocalParameters.baseColor * MaterialParameters.DiffuseTransmittance);
        context.MicroReflectComponent = new MicrofacetReflection<DisneyFresnel>(context.LocalParameters.microfacetDistrib, context.LocalParameters.fresnel, context.LocalParameters.specularTint);
        context.MicroTransmitComponent = new MicrofacetTransmission(context.LocalParameters.specularTransmittance, context.LocalParameters.transmissionDistribution, 1, MaterialParameters.IndexOfRefraction);

        return context;
    }

    EvalContext MakeEvalContext(in ShadingContext shadingContext, in GenericMaterialContext context, Vector3 inDir) {
        EvalContext evalContext = new() {
            InDirWorldSpace = inDir,
            InDirShadingSpace = shadingContext.WorldToShading(inDir)
        };
        var (_, r) = ComputeSelectWeights(context.LocalParameters, shadingContext.OutDir, evalContext.InDirShadingSpace);
        evalContext.SelectionWeightsReverse = r;
        evalContext.SameHemisphere = ShouldReflect(shadingContext.Point, shadingContext.OutDirWorld, inDir);
        return evalContext;
    }

    /// <summary>Crudely importance samples the combined BSDFs</summary>
    public override BsdfSample Sample(in ShadingContext shadingContext, Vector2 primarySample, ref ComponentWeights componentWeights) {
        ShadingStats.NotifySample();

        var context = MakeContext(shadingContext);
        bool isOnLightSubpath = context.ShadingContext.IsOnLightSubpath;

        // Select a component to sample from
        // TODO this can be done in a loop if the selection probs are in an array
        Vector3? sample = null;
        float offset = 0;
        if (primarySample.X < offset + context.SelectionWeightsForward.DiffTrans) {
            var remapped = primarySample;
            remapped.X = Math.Min((primarySample.X - offset) / context.SelectionWeightsForward.DiffTrans, 1);
            sample = context.DiffuseTransmitComponent.Sample(context.OutDirShadingSpace, isOnLightSubpath, remapped);
        }
        offset += context.SelectionWeightsForward.DiffTrans;

        if (primarySample.X > offset && primarySample.X < offset + context.SelectionWeightsForward.Diff) {
            var remapped = primarySample;
            remapped.X = Math.Min((primarySample.X - offset) / context.SelectionWeightsForward.Diff, 1);
            sample = context.DiffuseComponent.Sample(context.OutDirShadingSpace, isOnLightSubpath, remapped);
        }
        offset += context.SelectionWeightsForward.Diff;

        if (primarySample.X > offset && primarySample.X < offset + context.SelectionWeightsForward.Reflect) {
            var remapped = primarySample;
            remapped.X = Math.Min((primarySample.X - offset) / context.SelectionWeightsForward.Reflect, 1);
            sample = context.MicroReflectComponent.Sample(context.OutDirShadingSpace, isOnLightSubpath, remapped);
        }
        offset += context.SelectionWeightsForward.Reflect;

        if (primarySample.X > offset && primarySample.X < offset + context.SelectionWeightsForward.Trans) {
            var remapped = primarySample;
            remapped.X = Math.Min((primarySample.X - offset) / context.SelectionWeightsForward.Trans, 1);
            sample = context.MicroTransmitComponent.Sample(context.OutDirShadingSpace, isOnLightSubpath, remapped);
            Debug.Assert(!sample.HasValue || float.IsFinite(sample.Value.X));
        }
        offset += context.SelectionWeightsForward.Trans;

        if (!sample.HasValue) return BsdfSample.Invalid;

        var evalContext = MakeEvalContext(context.ShadingContext, context, context.ShadingContext.ShadingToWorld(sample.Value));
        var value = Evaluate(context, evalContext) * AbsCosTheta(evalContext.InDirShadingSpace);

        var (pdfFwd, pdfRev) = Pdf(context, evalContext, ref componentWeights);
        if (pdfFwd == 0) return BsdfSample.Invalid;

        Debug.Assert(float.IsFinite(value.Average / pdfFwd) && pdfFwd > 0);
        // Debug.Assert(value == RgbColor.Black || pdfRev != 0);

        // Combine results with balance heuristic MIS
        return new BsdfSample {
            Pdf = pdfFwd,
            PdfReverse = pdfRev,
            Weight = value / pdfFwd,
            Direction = evalContext.InDirWorldSpace
        };
    }

    (float, float) Pdf(in GenericMaterialContext context, in EvalContext evalContext, ref ComponentWeights components) {
        ShadingStats.NotifyPdfCompute();
        bool isOnLightSubpath = context.ShadingContext.IsOnLightSubpath;

        // Compute the sum of all pdf values
        float pdfFwd = 0, pdfRev = 0;
        float fwd, rev;
        int idxFwd = 0;
        int idxRev = 0;
        if (context.SelectionWeightsForward.Diff > 0 || evalContext.SelectionWeightsReverse.Diff > 0) {
            (fwd, rev) = context.DiffuseComponent.Pdf(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
            pdfFwd += fwd * context.SelectionWeightsForward.Diff;
            pdfRev += rev * evalContext.SelectionWeightsReverse.Diff;

            if (context.SelectionWeightsForward.Diff > 0) {
                if (components.Pdfs != null) components.Pdfs[idxFwd] = fwd;
                if (components.Weights != null) components.Weights[idxFwd] = context.SelectionWeightsForward.Diff;
                idxFwd++;
            }
            if (evalContext.SelectionWeightsReverse.Diff > 0) {
                if (components.PdfsReverse != null) components.PdfsReverse[idxRev] = rev;
                if (components.WeightsReverse != null) components.WeightsReverse[idxRev] = evalContext.SelectionWeightsReverse.Diff;
                idxRev++;
            }
        }
        if (context.SelectionWeightsForward.Trans > 0 || evalContext.SelectionWeightsReverse.Trans > 0) {
            (fwd, rev) = context.MicroTransmitComponent.Pdf(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
            pdfFwd += fwd * context.SelectionWeightsForward.Trans;
            pdfRev += rev * evalContext.SelectionWeightsReverse.Trans;

            if (context.SelectionWeightsForward.Trans > 0) {
                if (components.Pdfs != null) components.Pdfs[idxFwd] = fwd;
                if (components.Weights != null) components.Weights[idxFwd] = context.SelectionWeightsForward.Trans;
                idxFwd++;
            }
            if (evalContext.SelectionWeightsReverse.Trans > 0) {
                if (components.PdfsReverse != null) components.PdfsReverse[idxRev] = rev;
                if (components.WeightsReverse != null) components.WeightsReverse[idxRev] = evalContext.SelectionWeightsReverse.Trans;
                idxRev++;
            }
        }
        if (context.SelectionWeightsForward.DiffTrans > 0 || evalContext.SelectionWeightsReverse.DiffTrans > 0) {
            (fwd, rev) = context.DiffuseTransmitComponent.Pdf(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
            pdfFwd += fwd * context.SelectionWeightsForward.DiffTrans;
            pdfRev += rev * evalContext.SelectionWeightsReverse.DiffTrans;

            if (context.SelectionWeightsForward.DiffTrans > 0) {
                if (components.Pdfs != null) components.Pdfs[idxFwd] = fwd;
                if (components.Weights != null) components.Weights[idxFwd] = context.SelectionWeightsForward.DiffTrans;
                idxFwd++;
            }
            if (evalContext.SelectionWeightsReverse.DiffTrans > 0) {
                if (components.PdfsReverse != null) components.PdfsReverse[idxRev] = rev;
                if (components.WeightsReverse != null) components.WeightsReverse[idxRev] = evalContext.SelectionWeightsReverse.DiffTrans;
                idxRev++;
            }
        }
        if (context.SelectionWeightsForward.Reflect > 0 || evalContext.SelectionWeightsReverse.Reflect > 0) {
            (fwd, rev) = context.MicroReflectComponent.Pdf(context.OutDirShadingSpace, evalContext.InDirShadingSpace, isOnLightSubpath);
            pdfFwd += fwd * context.SelectionWeightsForward.Reflect;
            pdfRev += rev * evalContext.SelectionWeightsReverse.Reflect;

            if (context.SelectionWeightsForward.Reflect > 0) {
                if (components.Pdfs != null) components.Pdfs[idxFwd] = fwd;
                if (components.Weights != null) components.Weights[idxFwd] = context.SelectionWeightsForward.Reflect;
                idxFwd++;
            }
            if (evalContext.SelectionWeightsReverse.Reflect > 0) {
                if (components.PdfsReverse != null) components.PdfsReverse[idxRev] = rev;
                if (components.WeightsReverse != null) components.WeightsReverse[idxRev] = evalContext.SelectionWeightsReverse.Reflect;
                idxRev++;
            }
        }
        Debug.Assert(float.IsFinite(pdfFwd) && pdfFwd >= 0);

        components.NumComponents = idxFwd;
        components.NumComponentsReverse = idxRev;

        return (pdfFwd, pdfRev);
    }

    public override (float, float) Pdf(in ShadingContext context, Vector3 inDir, ref ComponentWeights components) {
        var ctx = MakeContext(context);
        var evalCtx = MakeEvalContext(context, ctx, inDir);
        return Pdf(ctx, evalCtx, ref components);
    }

    public override int MaxSamplingComponents => 5;

    struct LocalParams {
        public RgbColor baseColor, colorTint, specularTint;
        public float roughness;
        public TrowbridgeReitzDistribution microfacetDistrib;
        public TrowbridgeReitzDistribution transmissionDistribution;
        public DisneyFresnel fresnel;
        public float diffuseWeight;
        public RgbColor diffuseReflectance;
        public RgbColor retroReflectance;
        public RgbColor specularTransmittance;
    }

    LocalParams ComputeLocalParams(Vector2 texCoords) {
        LocalParams result = new();

        GetColorAndTints(texCoords, out result.baseColor, out result.colorTint, out result.specularTint);
        result.roughness = GetRoughness(texCoords);
        result.microfacetDistrib = CreateMicrofacetDistribution(result.roughness);
        result.transmissionDistribution = CreateTransmissionDistribution(result.roughness);

        CreateFresnel(result.baseColor, result.specularTint, out result.fresnel);

        result.diffuseWeight = (1 - MaterialParameters.Metallic) * (1 - MaterialParameters.SpecularTransmittance);
        result.diffuseReflectance = result.baseColor;
        if (MaterialParameters.Thin) {
            result.diffuseReflectance *= (1 - MaterialParameters.DiffuseTransmittance);
            result.specularTransmittance = MaterialParameters.SpecularTransmittance * RgbColor.Sqrt(result.baseColor);
        } else {
            result.diffuseReflectance *= result.diffuseWeight;
            result.specularTransmittance = MaterialParameters.SpecularTransmittance * result.baseColor;
        }
        result.retroReflectance = result.baseColor * result.diffuseWeight;

        return result;
    }

    void GetColorAndTints(Vector2 texCoords, out RgbColor baseColor, out RgbColor colorTint, out RgbColor specularTint) {
        baseColor = MaterialParameters.BaseColor.Lookup(texCoords);
        float luminance = baseColor.Luminance;
        colorTint = luminance > 0 ? (baseColor / luminance) : RgbColor.White;
        specularTint = RgbColor.Lerp(MaterialParameters.SpecularTintStrength, RgbColor.White, colorTint);
    }

    TrowbridgeReitzDistribution CreateMicrofacetDistribution(float roughness) {
        float aspect = MathF.Sqrt(1 - MaterialParameters.Anisotropic * .9f);
        float ax = Math.Max(.001f, roughness * roughness / aspect);
        float ay = Math.Max(.001f, roughness * roughness * aspect);
        return new TrowbridgeReitzDistribution { AlphaX = ax, AlphaY = ay };
    }

    TrowbridgeReitzDistribution CreateTransmissionDistribution(float roughness) {
        if (MaterialParameters.Thin) {
            // Scale roughness based on IOR (Burley 2015, Figure 15).
            float aspect = MathF.Sqrt(1 - MaterialParameters.Anisotropic * .9f);
            float rscaled = (0.65f * MaterialParameters.IndexOfRefraction - 0.35f) * roughness;
            float axT = Math.Max(.001f, rscaled * rscaled / aspect);
            float ayT = Math.Max(.001f, rscaled * rscaled * aspect);
            return new TrowbridgeReitzDistribution { AlphaX = axT, AlphaY = ayT };
        } else
            return CreateMicrofacetDistribution(roughness);
    }

    void CreateFresnel(RgbColor baseColor, RgbColor specularTint, out DisneyFresnel target) {
        var specularReflectanceAtNormal = RgbColor.Lerp(MaterialParameters.Metallic,
            FresnelSchlick.SchlickR0FromEta(MaterialParameters.IndexOfRefraction) * specularTint,
            baseColor);
        target.IndexOfRefraction = MaterialParameters.IndexOfRefraction;
        target.Metallic = MaterialParameters.Metallic;
        target.ReflectanceAtNormal = specularReflectanceAtNormal;
    }

    struct SelectionWeights {
        public float DiffTrans;
        public float Diff;
        public float Reflect;
        public float Trans;
    }

    struct GenericMaterialContext {
        public ShadingContext ShadingContext;
        public Vector3 OutDirShadingSpace => ShadingContext.OutDir;
        public SelectionWeights SelectionWeightsForward;
        public LocalParams LocalParameters;
        public DisneyDiffuse DiffuseComponent;
        public DisneyRetroReflection RetroComponent;
        public MicrofacetTransmission MicroTransmitComponent;
        public DiffuseTransmission DiffuseTransmitComponent;
        public MicrofacetReflection<DisneyFresnel> MicroReflectComponent;
    }

    struct EvalContext {
        public Vector3 InDirShadingSpace;
        public Vector3 InDirWorldSpace;
        public bool SameHemisphere;
        public SelectionWeights SelectionWeightsReverse;
    }

    (SelectionWeights, SelectionWeights) ComputeSelectWeights(LocalParams p, Vector3 outDir, Vector3 inDir) {
        var selectFwd = ComputeSelectWeights(p, outDir);

        float fRev = p.fresnel.Evaluate(CosTheta(inDir)).Average;
        fRev = Math.Clamp(fRev, 0.2f, 0.8f);
        float metallicBRDF = MaterialParameters.Metallic;
        float specularBSDF = (1.0f - MaterialParameters.Metallic) * MaterialParameters.SpecularTransmittance;
        float dielectricBRDF = (1.0f - MaterialParameters.SpecularTransmittance) * (1.0f - MaterialParameters.Metallic);

        float diffuseWeight = p.diffuseWeight;
        float difftransWeight = MaterialParameters.Thin ? MaterialParameters.DiffuseTransmittance : 0;
        float specularWeightRev = fRev * (metallicBRDF + dielectricBRDF + specularBSDF);
        float transmissionWeightRev = (1 - fRev) * specularBSDF;
        float normRev = 1.0f / (specularWeightRev + transmissionWeightRev + diffuseWeight + difftransWeight);

        return (
            selectFwd,
            new() {
                Diff = diffuseWeight * normRev,
                DiffTrans = difftransWeight * normRev,
                Reflect = specularWeightRev * normRev,
                Trans = transmissionWeightRev * normRev
            }
        );
    }

    SelectionWeights ComputeSelectWeights(LocalParams p, Vector3 outDir) {
        float f = p.fresnel.Evaluate(CosTheta(outDir)).Average;
        f = Math.Clamp(f, 0.2f, 0.8f);

        float metallicBRDF = MaterialParameters.Metallic;
        float specularBSDF = (1.0f - MaterialParameters.Metallic) * MaterialParameters.SpecularTransmittance;
        float dielectricBRDF = (1.0f - MaterialParameters.SpecularTransmittance) * (1.0f - MaterialParameters.Metallic);

        float specularWeight = f * (metallicBRDF + dielectricBRDF + specularBSDF);
        float transmissionWeight = (1 - f) * specularBSDF;
        float diffuseWeight = p.diffuseWeight;
        float difftransWeight = MaterialParameters.Thin ? MaterialParameters.DiffuseTransmittance : 0;

        float norm = 1.0f / (specularWeight + transmissionWeight + diffuseWeight + difftransWeight);

        return new() {
            Diff = diffuseWeight * norm,
            DiffTrans = difftransWeight * norm,
            Reflect = specularWeight * norm,
            Trans = transmissionWeight * norm
        };
    }

    /// <summary>
    /// The parameters used to create this material
    /// </summary>
    public readonly Parameters MaterialParameters;
}
