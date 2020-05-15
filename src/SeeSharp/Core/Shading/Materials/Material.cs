using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading.Bsdfs;

namespace SeeSharp.Core.Shading.Materials {
    public interface Material {
        Bsdf GetBsdf(SurfacePoint hit);
    }
}
