using System.Numerics;

namespace GroundWrapper {
    public interface Bsdf {
        ColorRGB EvaluateWithCosine(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
        ColorRGB EvaluateBsdfOnly(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
        BsdfSample Sample(Vector3 outDir, bool isOnLightSubpath, Vector2 primarySample);
        (float, float) Pdf(Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
    }
}
