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

        float diffuseReflectance;
        float diffuseTransmittance;
        float microfacetReflectance;
        float microfacetTransmittance;
        float totalReflectance;

        /// <param name="parameters">Properties of the material</param>
        public GenericMaterial(Parameters parameters) {
            this.parameters = parameters;

            // TODO create look-up table with n roughness values between minimum and maximum in texture

            diffuseReflectance = ComputeDiffuseReflectance(100);
            diffuseTransmittance = ComputeDiffuseTransmittance(100);
            microfacetReflectance = ComputeMicrofacetReflectance(100);
            microfacetTransmittance = ComputeMicrofacetTransmittance(100);

            totalReflectance = diffuseReflectance + microfacetReflectance + diffuseTransmittance + microfacetTransmittance;
        }

        /// <returns>The textured roughness value at the hit point </returns>
        public override float GetRoughness(SurfacePoint hit) => GetRoughness(hit.TextureCoordinates);

        /// <returns>True if the material is "thin" or the specular transmittance is not zero</returns>
        public override bool IsTransmissive(SurfacePoint hit)
        => (parameters.Thin && parameters.DiffuseTransmittance > 0) || parameters.SpecularTransmittance > 0;

        float GetRoughness(Vector2 texCoords)
        => parameters.Roughness.Lookup(texCoords);

        /// <returns>Interior IOR or its inverse, assumes outside is vacuum</returns>
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

        /// <returns>The base color, which is a crude approximation of the scattering strength</returns>
        public override RgbColor GetScatterStrength(SurfacePoint hit)
        => parameters.BaseColor.Lookup(hit.TextureCoordinates);

        /// <returns>BSDF value</returns>
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

        float ComputeMicrofacetTransmittance(int numSamples) {
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

        float ComputeDiffuseTransmittance(int numSamples) {
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

        float ComputeMicrofacetReflectance(int numSamples) {
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

        float ComputeDiffuseReflectance(int numSamples) {
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

        /// <summary>Crudely importance samples the combined BSDFs</summary>
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

        /// <returns>PDF used by <see cref="Sample"/></returns>
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
