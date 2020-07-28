using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading.Bsdfs;

namespace SeeSharp.Core.Shading.Materials {
    public interface Material {
        Bsdf GetBsdf(SurfacePoint hit);

        /// <summary>
        /// Computes the surface roughness, a value between 0 and 1.
        /// 0 is perfectly specular, 1 is perfectly diffuse.
        /// The exact value can differ between materials, as
        /// this is not a well-defined quantity from a physical point of view.
        /// </summary>
        float GetRoughness(SurfacePoint hit);

        /// <summary>
        /// Computes the sum of reflectance and transmittance.
        /// Can be an approximation, with the accuracy depending on the material.
        /// </summary>
        ColorRGB GetScatterStrength(SurfacePoint hit);
    }
}
