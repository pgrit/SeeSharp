using GroundWrapper.Geometry;

namespace GroundWrapper {
    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public struct Parameters {
            public Image baseColor;
        }

        public GenericMaterial(Parameters parameters) => this.parameters = parameters;

        Bsdf Material.GetBsdf(SurfacePoint hit) {
            var tex = hit.TextureCoordinates;
            return new DiffuseBsdf { point = hit, reflectance = parameters.baseColor[tex.X, tex.Y] };
        }

        Parameters parameters;
    }
}
