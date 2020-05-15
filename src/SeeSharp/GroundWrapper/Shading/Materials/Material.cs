using GroundWrapper.Geometry;
using GroundWrapper.Shading.Bsdfs;

namespace GroundWrapper.Shading.Materials {
    public interface Material {
        Bsdf GetBsdf(SurfacePoint hit);
    }
}
