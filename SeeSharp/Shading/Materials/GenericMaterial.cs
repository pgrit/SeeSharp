using SeeSharp.Geometry;
using SeeSharp.Shading.Bsdfs;
using SeeSharp.Shading.MicrofacetDistributions;
using SeeSharp.Image;
using System;
using System.Diagnostics;
using System.Numerics;
using SimpleImageIO;
using System.Threading;
using SeeSharp.Sampling;

namespace SeeSharp.Shading.Materials {
    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public class Parameters {
            public TextureRgb BaseColor = new(RgbColor.White);
            public TextureMono Roughness = new(0.5f);
            public float Metallic = 0.0f;
            public float SpecularTintStrength = 1.0f;
            public float Anisotropic = 0.0f;
            public float SpecularTransmittance = 0.0f;
            public float IndexOfRefraction = 1.45f;
            public bool Thin = false;
            public float DiffuseTransmittance = 0.0f;
        }

        float diffuseReflectance;
        float diffuseTransmittance;
        float microfacetReflectance;
        float microfacetTransmittance;
        float totalReflectance;

        public GenericMaterial(Parameters parameters) {
            this.parameters = parameters;

            // TODO create look-up table with n roughness values between minimum and maximum in texture

            diffuseReflectance = ComputeDiffuseReflectance(100);
            diffuseTransmittance = ComputeDiffuseTransmittance(100);
            microfacetReflectance = ComputeMicrofacetReflectance(100);
            microfacetTransmittance = ComputeMicrofacetTransmittance(100);

            totalReflectance = diffuseReflectance + microfacetReflectance + diffuseTransmittance + microfacetTransmittance;
        }

        public override float GetRoughness(SurfacePoint hit) => GetRoughness(hit.TextureCoordinates);

        public float GetRoughness(Vector2 texCoords)
        => parameters.Roughness.Lookup(texCoords);

        public override float GetIndexOfRefractionRatio(SurfacePoint hit, Vector3 outDir, Vector3 inDir) {
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            if (ShadingSpace.SameHemisphere(outDir, inDir))
                return 1; // does not change

            float insideIOR = parameters.IndexOfRefraction;
            float outsideIOR = 1;
            return ShadingSpace.CosTheta(outDir) > 0 ? (insideIOR / outsideIOR) : (outsideIOR / insideIOR);
        }

        public override RgbColor GetScatterStrength(SurfacePoint hit)
        => parameters.BaseColor.Lookup(hit.TextureCoordinates);

        public override RgbColor Evaluate(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            bool shouldReflect = ShouldReflect(hit, outDir, inDir);

            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            var local = ComputeLocalParams(hit.TextureCoordinates);

            // Evaluate all components
            var result = RgbColor.Black;
            if (local.diffuseWeight > 0 && shouldReflect) {
                result += new DisneyDiffuse(local.diffuseReflectance).Evaluate(outDir, inDir, isOnLightSubpath);
                result += new DisneyRetroReflection(local.retroReflectance, local.roughness)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }
            if (parameters.SpecularTransmittance > 0 && !shouldReflect) {
                result += new MicrofacetTransmission(local.specularTransmittance,
                    local.transmissionDistribution, 1, parameters.IndexOfRefraction)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }
            if (parameters.Thin && !shouldReflect) {
                result += new DiffuseTransmission(local.baseColor * parameters.DiffuseTransmittance)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }
            if (shouldReflect) {
                result += new MicrofacetReflection(local.microfacetDistrib, local.fresnel, local.specularTint)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }

            Debug.Assert(float.IsFinite(result.Average) && result.Average >= 0);

            return result;
        }

        public float ComputeMicrofacetTransmittance(int numSamples) {
            if (parameters.SpecularTransmittance <= 0) return 0;

            var local = ComputeLocalParams(new(0.5f, 0.5f));

            RNG rng = new();
            float total = 0;
            for (int i = 0; i < numSamples; ++i) {
                var primaryOut = rng.NextFloat2D();
                var sampleOut = SampleWarp.ToCosHemisphere(primaryOut);

                var distrib = new MicrofacetTransmission(local.specularTransmittance,
                    local.transmissionDistribution, 1, parameters.IndexOfRefraction);

                var primaryIn = rng.NextFloat2D();
                var sampleIn = distrib.Sample(sampleOut.Direction, false, primaryIn);
                if (!sampleIn.HasValue) continue;

                (float pdf, _) = distrib.Pdf(sampleOut.Direction, sampleIn.Value, false);
                if (pdf == 0) continue;

                var value = distrib.Evaluate(sampleOut.Direction, sampleIn.Value, false);
                total += value.Average / pdf / sampleOut.Pdf / numSamples * 
                    sampleOut.Direction.Z * Math.Abs(sampleIn.Value.Z);
            }

            return total;
        }

        public float ComputeDiffuseTransmittance(int numSamples) {
            if (!parameters.Thin) return 0;

            var local = ComputeLocalParams(new(0.5f, 0.5f));

            RNG rng = new();
            float total = 0;
            for (int i = 0; i < numSamples; ++i) {
                var primaryIn = rng.NextFloat2D();
                var sampleIn = SampleWarp.ToCosHemisphere(primaryIn);
                sampleIn.Direction.Z *= -1;

                var primaryOut = rng.NextFloat2D();
                var sampleOut = SampleWarp.ToCosHemisphere(primaryOut);

                var value = new DiffuseTransmission(local.baseColor * parameters.DiffuseTransmittance)
                    .Evaluate(sampleOut.Direction, sampleIn.Direction, false);
                total += value.Average / sampleIn.Pdf / sampleOut.Pdf / numSamples * sampleOut.Direction.Z * 
                    Math.Abs(sampleIn.Direction.Z);
            }

            return total;
        }

        public float ComputeMicrofacetReflectance(int numSamples) {
            var local = ComputeLocalParams(new(0.5f, 0.5f));

            RNG rng = new();
            float total = 0;
            for (int i = 0; i < numSamples; ++i) {
                var primaryOut = rng.NextFloat2D();
                var sampleOut = SampleWarp.ToCosHemisphere(primaryOut);

                var distrib = 
                    new MicrofacetReflection(local.microfacetDistrib, local.fresnel, local.specularTint);

                var primaryIn = rng.NextFloat2D();
                var sampleIn = distrib.Sample(sampleOut.Direction, false, primaryIn);
                if (!sampleIn.HasValue) continue;

                (float pdf, _) = distrib.Pdf(sampleOut.Direction, sampleIn.Value, false);
                if (pdf == 0) continue;

                var value = distrib.Evaluate(sampleOut.Direction, sampleIn.Value, false);
                total += value.Average / pdf / sampleOut.Pdf / numSamples *
                    sampleOut.Direction.Z * Math.Abs(sampleIn.Value.Z);
            }

            return total;
        }

        public float ComputeDiffuseReflectance(int numSamples) {
            // TODO create look-up table with n roughness values between minimum and maximum in texture
            var local = ComputeLocalParams(new(0.5f, 0.5f));

            RNG rng = new();
            float total = 0;
            for (int i = 0; i < numSamples; ++i) {
                var primaryIn = rng.NextFloat2D();
                var sampleIn = SampleWarp.ToCosHemisphere(primaryIn);

                var primaryOut = rng.NextFloat2D();
                var sampleOut = SampleWarp.ToCosHemisphere(primaryOut);

                var value = new DisneyDiffuse(local.diffuseReflectance)
                    .Evaluate(sampleOut.Direction, sampleIn.Direction, false);

                value += new DisneyRetroReflection(local.retroReflectance, local.roughness)
                    .Evaluate(sampleOut.Direction, sampleIn.Direction, false);

                total += value.Average / sampleIn.Pdf / sampleOut.Pdf / numSamples * sampleOut.Direction.Z * 
                    sampleIn.Direction.Z;
            }

            return total;
        }

        public override BsdfSample Sample(SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath,
                                          Vector2 primarySample) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);

            // Compute parameters
            var local = ComputeLocalParams(hit.TextureCoordinates);
            var select = ComputeSelectWeights(local, outDir, outDir);

            // Select a component to sample from
            // TODO this can be done in a loop if the selection probs are in an array
            Vector3? sample = null;
            float offset = 0;
            if (primarySample.X < offset + select.DiffTrans) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / select.DiffTrans, 1);
                sample = new DiffuseTransmission(local.baseColor * parameters.DiffuseTransmittance)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += select.DiffTrans;

            if (primarySample.X > offset && primarySample.X < offset + select.Retro) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / select.Retro, 1);
                sample = new DisneyRetroReflection(local.retroReflectance, local.roughness)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += select.Retro;

            if (primarySample.X > offset && primarySample.X < offset + select.Diff) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / select.Diff, 1);
                sample = new DisneyDiffuse(local.diffuseReflectance)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += select.Diff;

            if (primarySample.X > offset && primarySample.X < offset + select.Reflect) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / select.Reflect, 1);
                sample = new MicrofacetReflection(local.microfacetDistrib, local.fresnel, local.specularTint)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += select.Reflect;

            if (primarySample.X > offset && primarySample.X < offset + select.Trans) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / select.Trans, 1);
                sample = new MicrofacetTransmission(local.specularTransmittance, local.transmissionDistribution,
                    1, parameters.IndexOfRefraction).Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += select.Trans;

            // Terminate if no valid direction was sampled
            if (!sample.HasValue) return BsdfSample.Invalid;
            var sampledDir = ShadingSpace.ShadingToWorld(normal, sample.Value);

            // Evaluate all components
            var outWorld = ShadingSpace.ShadingToWorld(normal, outDir);
            var value = EvaluateWithCosine(hit, outWorld, sampledDir, isOnLightSubpath);

            // Compute all pdfs
            var (pdfFwd, pdfRev) = Pdf(hit, outWorld, sampledDir, isOnLightSubpath);
            if (pdfFwd == 0) return BsdfSample.Invalid;

            Debug.Assert(float.IsFinite(value.Average / pdfFwd) && pdfFwd > 0);

            // Combine results with balance heuristic MIS
            return new BsdfSample {
                pdf = pdfFwd,
                pdfReverse = pdfRev,
                weight = value / pdfFwd,
                direction = sampledDir
            };
        }

        public override (float, float) Pdf(SurfacePoint hit, Vector3 outDir, Vector3 inDir,
                                           bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            // Compute parameters
            var local = ComputeLocalParams(hit.TextureCoordinates);
            var select = ComputeSelectWeights(local, outDir, inDir);

            // Compute the sum of all pdf values
            float pdfFwd = 0, pdfRev = 0;
            float fwd, rev;
            if (select.Diff > 0) {
                (fwd, rev) = new DisneyDiffuse(local.diffuseReflectance).Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * select.Diff;
                pdfRev += rev * select.Diff;
            }
            if (select.Retro > 0) {
                (fwd, rev) = new DisneyRetroReflection(local.retroReflectance, local.roughness)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * select.Retro;
                pdfRev += rev * select.Retro;
            }
            if (select.Trans > 0) {
                (fwd, rev) = new MicrofacetTransmission(local.specularTransmittance,
                    local.transmissionDistribution, 1, parameters.IndexOfRefraction)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * select.Trans;
                pdfRev += rev * select.TransRev;
            }
            if (select.DiffTrans > 0) {
                (fwd, rev) = new DiffuseTransmission(local.baseColor * parameters.DiffuseTransmittance)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * select.DiffTrans;
                pdfRev += rev * select.DiffTrans;
            }
            if (select.Reflect > 0) {
                (fwd, rev) = new MicrofacetReflection(local.microfacetDistrib, local.fresnel, local.specularTint)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * select.Reflect;
                pdfRev += rev * select.ReflectRev;
            }
            Debug.Assert(float.IsFinite(pdfFwd) && pdfFwd >= 0);

            return (pdfFwd, pdfRev);
        }

        struct LocalParams {
            public RgbColor baseColor, colorTint, specularTint;
            public float roughness;
            public TrowbridgeReitzDistribution microfacetDistrib;
            public TrowbridgeReitzDistribution transmissionDistribution;
            public Fresnel fresnel;
            public float diffuseWeight;
            public RgbColor diffuseReflectance;
            public RgbColor retroReflectance;
            public RgbColor specularTransmittance;
        }

        readonly ThreadLocal<Fresnel> fresnelPrealloc = new(() => new DisneyFresnel());

        LocalParams ComputeLocalParams(Vector2 texCoords) {
            LocalParams result = new();

            (result.baseColor, result.colorTint, result.specularTint) = GetColorAndTints(texCoords);
            result.roughness = GetRoughness(texCoords);
            result.microfacetDistrib = CreateMicrofacetDistribution(result.roughness);
            result.transmissionDistribution = CreateTransmissionDistribution(result.roughness);

            CreateFresnel(result.baseColor, result.specularTint, ((DisneyFresnel)fresnelPrealloc.Value));
            result.fresnel = fresnelPrealloc.Value;

            result.diffuseWeight = (1 - parameters.Metallic) * (1 - parameters.SpecularTransmittance);
            result.diffuseReflectance = result.baseColor;
            if (parameters.Thin)
                result.diffuseReflectance *= (1 - parameters.DiffuseTransmittance);
            else
                result.diffuseReflectance *= result.diffuseWeight;
            result.retroReflectance = result.baseColor * result.diffuseWeight;
            result.specularTransmittance = parameters.SpecularTransmittance * RgbColor.Sqrt(result.baseColor);

            return result;
        }

        (RgbColor, RgbColor, RgbColor) GetColorAndTints(Vector2 texCoords) {
            // Isolate hue and saturation from the base color
            var baseColor = parameters.BaseColor.Lookup(texCoords);
            float luminance = baseColor.Luminance;
            var colorTint = luminance > 0 ? (baseColor / luminance) : RgbColor.White;
            var specularTint = RgbColor.Lerp(parameters.SpecularTintStrength, RgbColor.White, colorTint);
            return (baseColor, colorTint, specularTint);
        }

        TrowbridgeReitzDistribution CreateMicrofacetDistribution(float roughness) {
            float aspect = MathF.Sqrt(1 - parameters.Anisotropic * .9f);
            float ax = Math.Max(.001f, roughness * roughness / aspect);
            float ay = Math.Max(.001f, roughness * roughness * aspect);
            return new TrowbridgeReitzDistribution { AlphaX = ax, AlphaY = ay };
        }

        TrowbridgeReitzDistribution CreateTransmissionDistribution(float roughness) {
            if (parameters.Thin) {
                // Scale roughness based on IOR (Burley 2015, Figure 15).
                float aspect = MathF.Sqrt(1 - parameters.Anisotropic * .9f);
                float rscaled = (0.65f * parameters.IndexOfRefraction - 0.35f) * roughness;
                float axT = Math.Max(.001f, rscaled * rscaled / aspect);
                float ayT = Math.Max(.001f, rscaled * rscaled * aspect);
                return new TrowbridgeReitzDistribution { AlphaX = axT, AlphaY = ayT };
            } else
                return CreateMicrofacetDistribution(roughness);
        }

        void CreateFresnel(RgbColor baseColor, RgbColor specularTint, DisneyFresnel target) {
            var specularReflectanceAtNormal = RgbColor.Lerp(parameters.Metallic,
                FresnelSchlick.SchlickR0FromEta(parameters.IndexOfRefraction) * specularTint,
                baseColor);
            target.IndexOfRefraction = parameters.IndexOfRefraction;
            target.Metallic = parameters.Metallic;
            target.ReflectanceAtNormal = specularReflectanceAtNormal;
        }

        struct SelectionWeights {
            public float DiffTrans;
            public float Retro;
            public float Diff;
            public float Reflect;
            public float Trans;
            public float ReflectRev;
            public float TransRev;
        }

        SelectionWeights ComputeSelectWeights(LocalParams p, Vector3 outDir, Vector3 inDir) {
            return new() {
                Diff = diffuseReflectance * 0.5f / totalReflectance,
                Retro = diffuseReflectance * 0.5f / totalReflectance,
                Reflect = microfacetReflectance / totalReflectance,
                Trans = microfacetTransmittance / totalReflectance,
                DiffTrans = diffuseTransmittance / totalReflectance,
                ReflectRev = microfacetReflectance / totalReflectance,
                TransRev = microfacetTransmittance / totalReflectance
            };
        }

        Parameters parameters;
    }
}
