using GroundWrapper.Geometry;
using System.Numerics;

namespace GroundWrapper.Shading.Bsdfs {
    public interface BsdfComponent {
        ColorRGB Evaluate(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
        Vector3? Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample);
        (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
    }
}
