using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading.Bsdfs;

namespace SeeSharp.Core.Shading.Materials {
    public class DiffuseMaterial : Material {
        public class Parameters {
            public Image baseColor = Image.Constant(ColorRGB.White);
        }
        public DiffuseMaterial(Parameters parameters) => this.parameters = parameters;

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;

            // Evaluate textures // TODO make those actual textures
            var baseColor = parameters.baseColor[tex.X * parameters.baseColor.Width, tex.Y * parameters.baseColor.Height];

            return new Bsdf { Components = new BsdfComponent[] {
                new DiffuseBsdf { reflectance = baseColor }
            }, shadingNormal = hit.ShadingNormal };
        }

        Parameters parameters;
    }
}
