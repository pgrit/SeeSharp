using GroundWrapper.Geometry;
using GroundWrapper.Shading.Bsdfs;

namespace GroundWrapper.Shading.Materials {
    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public class Parameters { // TODO support textured roughness etc
            public Image baseColor = Image.Constant(ColorRGB.White);
            public float roughness = 0.5f;
            public float metallic = 0;
            public Image specularTint = Image.Constant(ColorRGB.Black);
            public float anisotropic = 0;
            public float specularTransmittance = 0;
        }

        public GenericMaterial(Parameters parameters) => this.parameters = parameters;

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;
            return new DiffuseBsdf { point = hit, reflectance = parameters.baseColor[tex.X, tex.Y] };
        }

        Parameters parameters;
    }
}
