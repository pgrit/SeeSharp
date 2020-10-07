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

        float Material.GetRoughness(SurfacePoint hit) {
            return parameters.roughness;
        }

        ColorRGB Material.GetScatterStrength(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;
            var baseColor = parameters.baseColor[tex.X * parameters.baseColor.Width, tex.Y * parameters.baseColor.Height];
            return baseColor;
        }

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;

            // Evaluate textures // TODO make those actual textures
            var baseColor = parameters.baseColor.TextureLookup(tex.X, tex.Y);
            float roughness = parameters.roughness;
            float anisotropic = parameters.anisotropic;
            float specularTintStrength = parameters.specularTintStrength;
            float metallic = parameters.metallic;
            float IOR = parameters.indexOfRefraction;
            float specularTransmittance = parameters.specularTransmittance;
            float diffuseTransmittance = parameters.diffuseTransmittance;

            // Isolate hue and saturation from the base color
            float luminance = baseColor.Luminance;
            var colorTint = luminance > 0 ? (baseColor / luminance) : ColorRGB.White;

            // Create the microfacet distribution for metallic and/or specular
            float aspect = MathF.Sqrt(1 - anisotropic * .9f);
            float ax = Math.Max(.001f, roughness * roughness / aspect);
            float ay = Math.Max(.001f, roughness * roughness * aspect);
            var microfacetDistrib = new TrowbridgeReitzDistribution { AlphaX = ax, AlphaY = ay };

            // Compute the modified Fresnel term, interpolate between dielectric and conductor
            var specularTint = ColorRGB.Lerp(specularTintStrength, ColorRGB.White, colorTint);
            var specularReflectanceAtNormal = ColorRGB.Lerp(metallic,
                FresnelSchlick.SchlickR0FromEta(IOR) * specularTint,
                baseColor);
            var fresnel = new DisneyFresnel {
                IndexOfRefraction = IOR,
                Metallic = metallic,
                ReflectanceAtNormal = specularReflectanceAtNormal
            };

            // Compute the weight of the diffuse component
            float diffuseWeight = (1 - metallic) * (1 - specularTransmittance);

            // Determine the number of components
            int numComponents = 1 + (diffuseWeight > 0 ? 2 : 0) + (specularTransmittance > 0 ? 1 : 0) + (parameters.thin ? 1 : 0);
            var bsdf = new Bsdf { Components = new BsdfComponent[numComponents] };
            int idx = 0;

            // Add the retro-reflection and diffuse terms
            if (diffuseWeight > 0) {
                if (parameters.thin) {
                    bsdf.Components[idx++] = new DisneyDiffuse { Reflectance = baseColor * (1 - diffuseTransmittance) };
                } else {
                    bsdf.Components[idx++] = new DisneyDiffuse { Reflectance = baseColor * diffuseWeight };
                }

                bsdf.Components[idx++] = new DisneyRetroReflection {
                    Reflectance = baseColor * diffuseWeight,
                    Roughness = roughness
                };
            }

            if (specularTransmittance > 0) {
                // Walter et al's model, with the provided transmissive term scaled
                // by sqrt(color), so that after two refractions, we're back to the
                // provided color.
                ColorRGB T = specularTransmittance * ColorRGB.Sqrt(baseColor);
                if (parameters.thin) {
                    // Scale roughness based on IOR (Burley 2015, Figure 15).
                    float rscaled = (0.65f * IOR - 0.35f) * roughness;
                    float axT = Math.Max(.001f, rscaled * rscaled / aspect);
                    float ayT = Math.Max(.001f, rscaled * rscaled * aspect);
                    var scaledDistrib = new TrowbridgeReitzDistribution { AlphaX = axT, AlphaY = ayT };
                    bsdf.Components[idx++] = new MicrofacetTransmission {
                        Distribution = scaledDistrib,
                        Transmittance = T,
                        outsideIOR = 1,
                        insideIOR = IOR
                    };
                } else {
                    bsdf.Components[idx++] = new MicrofacetTransmission {
                        Distribution = microfacetDistrib,
                        Transmittance = T,
                        outsideIOR = 1,
                        insideIOR = IOR
                    };
                }
            }

            if (parameters.thin) {
                bsdf.Components[idx++] = new DiffuseTransmission { Transmittance = baseColor * diffuseTransmittance };
            }

            bsdf.Components[idx++] = new MicrofacetReflection {
                distribution = microfacetDistrib,
                fresnel = fresnel,
                tint = specularTint
            };

            Debug.Assert(idx == bsdf.Components.Length);

            return bsdf;
        }

        Parameters parameters;
    }
}
