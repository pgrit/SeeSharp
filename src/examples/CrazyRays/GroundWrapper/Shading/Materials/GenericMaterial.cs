using GroundWrapper.Geometry;
using GroundWrapper.Shading.Bsdfs;
using GroundWrapper.Shading.MicrofacetDistributions;
using System;
using System.Collections.Generic;

namespace GroundWrapper.Shading.Materials {
    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public class Parameters { // TODO support textured roughness etc
            public Image baseColor = Image.Constant(ColorRGB.White);
            public float roughness = 0.2f;
            public float metallic = 0.0f;
            public float specularTintStrength = 1.0f;
            public float anisotropic = 0.0f;
            public float specularTransmittance = 1.0f;
            public float indexOfRefraction = 5.6f;
            public bool thin = false;
            public float diffuseTransmittance = 1.0f;
        }

        public GenericMaterial(Parameters parameters) => this.parameters = parameters;

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;

            // Evaluate textures // TODO make those actual textures
            var baseColor = parameters.baseColor[tex.X, tex.Y];
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

            var componentList = new List<BsdfComponent>(4);

            // Add the retro-reflection and diffuse terms
            float diffuseWeight = (1 - metallic) * (1 - specularTransmittance);
            if (diffuseWeight > 0) {
                if (parameters.thin) {
                    var diffuse = new DisneyDiffuse { Reflectance = baseColor * (1 - diffuseTransmittance) };
                    componentList.Add(diffuse);
                } else {
                    var diffuse = new DisneyDiffuse { Reflectance = baseColor * diffuseWeight };
                    componentList.Add(diffuse);
                }

                var retro = new DisneyRetroReflection { Reflectance = baseColor * diffuseWeight, Roughness = roughness };
                componentList.Add(retro);
            }

            if (specularTransmittance > 0) {
                // Walter et al's model, with the provided transmissive term scaled
                // by sqrt(color), so that after two refractions, we're back to the
                // provided color.
                ColorRGB T = specularTransmittance * ColorRGB.Sqrt(baseColor);
                BsdfComponent specularTransmit;
                if (parameters.thin) {
                    // Scale roughness based on IOR (Burley 2015, Figure 15).
                    float rscaled = (0.65f * IOR - 0.35f) * roughness;
                    float axT = Math.Max(.001f, rscaled * rscaled / aspect);
                    float ayT = Math.Max(.001f, rscaled * rscaled * aspect);
                    var scaledDistrib = new TrowbridgeReitzDistribution { AlphaX = axT, AlphaY = ayT };
                    specularTransmit = new MicrofacetTransmission { 
                        Distribution = scaledDistrib, 
                        Transmittance = T, 
                        outsideIOR = 1, insideIOR = IOR 
                    };
                } else
                    specularTransmit = new MicrofacetTransmission {
                        Distribution = microfacetDistrib,
                        Transmittance = T,
                        outsideIOR = 1,
                        insideIOR = IOR
                    };
                componentList.Add(specularTransmit);
            }

            if (parameters.thin) {
                var diffuse = new DiffuseTransmission { Transmittance = baseColor * diffuseTransmittance };
                componentList.Add(diffuse);
            }

            var specularReflect = new MicrofacetReflection { distribution = microfacetDistrib, fresnel = fresnel, tint = specularTint };
            componentList.Add(specularReflect);

            return new Bsdf { point = hit, Components = componentList.ToArray() };
        }

        Parameters parameters;
    }
}
