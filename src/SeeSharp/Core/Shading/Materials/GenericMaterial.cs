using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading.Bsdfs;
using SeeSharp.Core.Shading.MicrofacetDistributions;
using SeeSharp.Core.Image;
using System;
using System.Diagnostics;
using System.Numerics;

namespace SeeSharp.Core.Shading.Materials {
    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public class Parameters { // TODO support textured roughness etc
            public Image<ColorRGB> baseColor = Image<ColorRGB>.Constant(ColorRGB.White);
            public float roughness = 0.5f;
            public float metallic = 0.0f;
            public float specularTintStrength = 1.0f;
            public float anisotropic = 0.0f;
            public float specularTransmittance = 0.0f;
            public float indexOfRefraction = 1.45f;
            public bool thin = false;
            public float diffuseTransmittance = 0.0f;
        }

        public GenericMaterial(Parameters parameters) => this.parameters = parameters;

        public override float GetRoughness(SurfacePoint hit) => parameters.roughness;

        public override ColorRGB GetScatterStrength(SurfacePoint hit) =>
            parameters.baseColor.TextureLookup(hit.TextureCoordinates);

        public override ColorRGB Evaluate(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            // Compute parameters // TODO this should probably be one struct to reduce code duplication
            var (baseColor, colorTint, specularTint) = GetColorAndTints(hit);
            var microfacetDistrib = CreateMicrofacetDistribution();
            var transmissionDistribution = CreateTransmissionDistribution();
            var fresnel = CreateFresnel(baseColor, specularTint);
            float diffuseWeight = (1 - parameters.metallic) * (1 - parameters.specularTransmittance);
            var diffuseReflectance = baseColor;
            if (parameters.thin)
                diffuseReflectance *= (1 - parameters.diffuseTransmittance);
            else
                diffuseReflectance *= diffuseWeight;
            var retroReflectance = baseColor * diffuseWeight;
            var specularTransmittance = parameters.specularTransmittance * ColorRGB.Sqrt(baseColor);

            // Evaluate all components
            var result = ColorRGB.Black;
            if (diffuseWeight > 0) {
                result += new DisneyDiffuse(diffuseReflectance).Evaluate(outDir, inDir, isOnLightSubpath);
                result += new DisneyRetroReflection(retroReflectance, parameters.roughness)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }
            if (parameters.specularTransmittance > 0) {
                result += new MicrofacetTransmission(specularTransmittance, transmissionDistribution, 1, parameters.indexOfRefraction)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }
            if (parameters.thin) {
                result += new DiffuseTransmission(baseColor * parameters.diffuseTransmittance)
                    .Evaluate(outDir, inDir, isOnLightSubpath);
            }
            result += new MicrofacetReflection(microfacetDistrib, fresnel, specularTint)
                .Evaluate(outDir, inDir, isOnLightSubpath);

            return result;
        }

        public override BsdfSample Sample(SurfacePoint hit, Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);

            // Compute parameters
            var (baseColor, colorTint, specularTint) = GetColorAndTints(hit);
            var microfacetDistrib = CreateMicrofacetDistribution();
            var transmissionDistribution = CreateTransmissionDistribution();
            var fresnel = CreateFresnel(baseColor, specularTint);
            float diffuseWeight = (1 - parameters.metallic) * (1 - parameters.specularTransmittance);
            var diffuseReflectance = baseColor;
            if (parameters.thin)
                diffuseReflectance *= (1 - parameters.diffuseTransmittance);
            else
                diffuseReflectance *= diffuseWeight;
            var retroReflectance = baseColor * diffuseWeight;
            var specularTransmittance = parameters.specularTransmittance * ColorRGB.Sqrt(baseColor);

            // Evaluate the fresnel term for transmittance importance sampling (ignores microfacet orientation)
            float f = fresnel.Evaluate(ShadingSpace.CosTheta(outDir)).Luminance;

            // Compute selection probabilities // TODO this should be gathered in an array to reduce code duplication
            float selProbDiffTrans = 0;
            float selProbRetro = diffuseWeight * 0.5f;
            float selProbDiff = diffuseWeight * 0.5f;
            if (parameters.thin) {
                selProbDiffTrans = diffuseWeight * parameters.diffuseTransmittance;
                selProbRetro = diffuseWeight * (1 - parameters.diffuseTransmittance) * 0.5f;
                selProbDiff = diffuseWeight * (1 - parameters.diffuseTransmittance) * 0.5f;
            }
            float selProbReflect = (1 - diffuseWeight) * f;
            float selProbTrans = (1 - diffuseWeight) * (1 - f);

            // Make sure they sum to one
            Debug.Assert(MathF.Abs(selProbDiffTrans + selProbRetro + selProbDiff + selProbReflect + selProbTrans - 1.0f) < 0.001f);

            // Select a component to sample from // TODO this can be done in a loop if the selection probs are in an array
            Vector3? sample = null;
            float offset = 0;
            if (primarySample.X < offset + selProbDiffTrans) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / selProbDiffTrans, 1);
                sample = new DiffuseTransmission(baseColor * parameters.diffuseTransmittance)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += selProbDiffTrans;

            if (primarySample.X > offset && primarySample.X < offset + selProbRetro) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / selProbRetro, 1);
                sample = new DisneyRetroReflection(retroReflectance, parameters.roughness)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += selProbRetro;

            if (primarySample.X > offset && primarySample.X < offset + selProbDiff) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / selProbDiff, 1);
                sample = new DisneyDiffuse(diffuseReflectance)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += selProbDiff;

            if (primarySample.X > offset && primarySample.X < offset + selProbReflect) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / selProbReflect, 1);
                sample = new MicrofacetReflection(microfacetDistrib, fresnel, specularTint)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += selProbReflect;

            if (primarySample.X > offset && primarySample.X < offset + selProbTrans) {
                var remapped = primarySample;
                remapped.X = Math.Min((primarySample.X - offset) / selProbTrans, 1);
                sample = new MicrofacetTransmission(specularTransmittance, transmissionDistribution, 1, parameters.indexOfRefraction)
                    .Sample(outDir, isOnLightSubpath, remapped);
            }
            offset += selProbTrans;

            // Terminate if no valid direction was sampled
            if (!sample.HasValue)
                return BsdfSample.Invalid;
            var sampledDir = ShadingSpace.ShadingToWorld(hit.ShadingNormal, sample.Value);

            // Evaluate all components
            var outWorld = ShadingSpace.ShadingToWorld(hit.ShadingNormal, outDir);
            var value = EvaluateWithCosine(hit, outWorld, sampledDir, isOnLightSubpath);

            // Compute all pdfs
            var (pdfFwd, pdfRev) = Pdf(hit, outWorld, sampledDir, isOnLightSubpath);
            if (pdfFwd == 0)
                return BsdfSample.Invalid;

            // Combine results with balance heuristic MIS
            return new BsdfSample {
                pdf = pdfFwd,
                pdfReverse = pdfRev,
                weight = value / pdfFwd,
                direction = sampledDir
            };
        }

        public override (float, float) Pdf(SurfacePoint hit, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            // Transform directions to shading space and normalize
            var normal = hit.ShadingNormal;
            outDir = ShadingSpace.WorldToShading(normal, outDir);
            inDir = ShadingSpace.WorldToShading(normal, inDir);

            // Compute parameters
            var (baseColor, colorTint, specularTint) = GetColorAndTints(hit);
            var microfacetDistrib = CreateMicrofacetDistribution();
            var transmissionDistribution = CreateTransmissionDistribution();
            var fresnel = CreateFresnel(baseColor, specularTint);
            float diffuseWeight = (1 - parameters.metallic) * (1 - parameters.specularTransmittance);
            var diffuseReflectance = baseColor;
            if (parameters.thin)
                diffuseReflectance *= (1 - parameters.diffuseTransmittance);
            else
                diffuseReflectance *= diffuseWeight;
            var retroReflectance = baseColor * diffuseWeight;
            var specularTransmittance = parameters.specularTransmittance * ColorRGB.Sqrt(baseColor);

            // Evaluate the fresnel term for transmittance importance sampling (ignores microfacet orientation)
            float f = fresnel.Evaluate(ShadingSpace.CosTheta(outDir)).Luminance;
            float fRev = fresnel.Evaluate(ShadingSpace.CosTheta(inDir)).Luminance;

            // Compute selection probabilities
            float selProbDiffTrans = 0;
            float selProbRetro = diffuseWeight * 0.5f;
            float selProbDiff = diffuseWeight * 0.5f;
            if (parameters.thin) {
                selProbDiffTrans = diffuseWeight * parameters.diffuseTransmittance;
                selProbRetro = diffuseWeight * (1 - parameters.diffuseTransmittance) * 0.5f;
                selProbDiff = diffuseWeight * (1 - parameters.diffuseTransmittance) * 0.5f;
            }
            float selProbReflect = (1 - diffuseWeight) * f;
            float selProbTrans = (1 - diffuseWeight) * (1 - f);
            float selProbReflectRev = (1 - diffuseWeight) * fRev;
            float selProbTransRev = (1 - diffuseWeight) * (1 - fRev);

            // Compute the sum of all pdf values
            float pdfFwd = 0, pdfRev = 0;
            float fwd, rev;
            if (selProbDiff > 0) {
                (fwd, rev) = new DisneyDiffuse(diffuseReflectance).Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * selProbDiff;
                pdfRev += rev * selProbDiff;
            }
            if (selProbRetro > 0) {
                (fwd, rev) = new DisneyRetroReflection(retroReflectance, parameters.roughness)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * selProbRetro;
                pdfRev += rev * selProbRetro;
            }
            if (selProbTrans > 0) {
                (fwd, rev) = new MicrofacetTransmission(specularTransmittance, transmissionDistribution, 1, parameters.indexOfRefraction)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * selProbTrans;
                pdfRev += rev * selProbTransRev;
            }
            if (selProbDiffTrans > 0) {
                (fwd, rev) = new DiffuseTransmission(baseColor * parameters.diffuseTransmittance)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * selProbDiffTrans;
                pdfRev += rev * selProbDiffTrans;
            }
            if (selProbReflect > 0) {
                (fwd, rev) = new MicrofacetReflection(microfacetDistrib, fresnel, specularTint)
                    .Pdf(outDir, inDir, isOnLightSubpath);
                pdfFwd += fwd * selProbReflect;
                pdfRev += rev * selProbReflectRev;
            }

            return (pdfFwd, pdfRev);
        }

        (ColorRGB, ColorRGB, ColorRGB) GetColorAndTints(SurfacePoint hit) {
            // Isolate hue and saturation from the base color
            var baseColor = parameters.baseColor.TextureLookup(hit.TextureCoordinates);
            float luminance = baseColor.Luminance;
            var colorTint = luminance > 0 ? (baseColor / luminance) : ColorRGB.White;
            var specularTint = ColorRGB.Lerp(parameters.specularTintStrength, ColorRGB.White, colorTint);
            return (baseColor, colorTint, specularTint);
        }

        TrowbridgeReitzDistribution CreateMicrofacetDistribution() {
            float aspect = MathF.Sqrt(1 - parameters.anisotropic * .9f);
            float ax = Math.Max(.001f, parameters.roughness * parameters.roughness / aspect);
            float ay = Math.Max(.001f, parameters.roughness * parameters.roughness * aspect);
            return new TrowbridgeReitzDistribution { AlphaX = ax, AlphaY = ay };
        }

        TrowbridgeReitzDistribution CreateTransmissionDistribution() {
            if (parameters.thin) {
                // Scale roughness based on IOR (Burley 2015, Figure 15).
                float aspect = MathF.Sqrt(1 - parameters.anisotropic * .9f);
                float rscaled = (0.65f * parameters.indexOfRefraction - 0.35f) * parameters.roughness;
                float axT = Math.Max(.001f, rscaled * rscaled / aspect);
                float ayT = Math.Max(.001f, rscaled * rscaled * aspect);
                return new TrowbridgeReitzDistribution { AlphaX = axT, AlphaY = ayT };
            } else
                return CreateMicrofacetDistribution();
        }

        Fresnel CreateFresnel(ColorRGB baseColor, ColorRGB specularTint) {
            var specularReflectanceAtNormal = ColorRGB.Lerp(parameters.metallic,
                FresnelSchlick.SchlickR0FromEta(parameters.indexOfRefraction) * specularTint,
                baseColor);
            return new DisneyFresnel {
                IndexOfRefraction = parameters.indexOfRefraction,
                Metallic = parameters.metallic,
                ReflectanceAtNormal = specularReflectanceAtNormal
            };
        }

        Parameters parameters;
    }
}
