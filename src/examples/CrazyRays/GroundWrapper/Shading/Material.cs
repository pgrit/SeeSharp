using GroundWrapper.Geometry;

namespace GroundWrapper {
    public interface Material {
        Bsdf GetBsdf(SurfacePoint hit);
    }
}
