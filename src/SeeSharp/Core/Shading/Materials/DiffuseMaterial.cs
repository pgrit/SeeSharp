using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading.Bsdfs;
using SeeSharp.Core.Image;

namespace SeeSharp.Core.Shading.Materials {
    public class DiffuseMaterial : Material {
        public class Parameters {
            public Image<ColorRGB> baseColor = Image<ColorRGB>.Constant(ColorRGB.White);
            public bool transmitter = false;
        }

        public DiffuseMaterial(Parameters parameters) => this.parameters = parameters;

        float Material.GetRoughness(SurfacePoint hit) => 1;

        ColorRGB Material.GetScatterStrength(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;
            var baseColor = parameters.baseColor[tex.X * parameters.baseColor.Width, tex.Y * parameters.baseColor.Height];
            return baseColor;
        }

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            // Evaluate textures
            var tex = hit.TextureCoordinates;
            var baseColor = parameters.baseColor[tex.X * parameters.baseColor.Width, tex.Y * parameters.baseColor.Height];

            if (parameters.transmitter) {
                return new Bsdf {
                    Components = new BsdfComponent[] {
                        new DiffuseTransmission { Transmittance = baseColor }
                    }, shadingNormal = hit.ShadingNormal
                };
            } else {
                return new Bsdf {
                    Components = new BsdfComponent[] {
                        new DiffuseBsdf { reflectance = baseColor }
                    }, shadingNormal = hit.ShadingNormal
                };
            }
        }

        Parameters parameters;
    }
}
