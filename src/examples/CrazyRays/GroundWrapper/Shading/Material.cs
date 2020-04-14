using GroundWrapper.GroundMath;

namespace GroundWrapper {
    public struct BsdfSample {
        public Vector3 direction;
        public float pdf;
        public float pdfReverse;

        /// <summary>
        /// Sample weight of the reflectance estimate, i.e., the product of 
        /// BSDF and shading cosine divided by the pdf.
        /// </summary>
        public ColorRGB weight;
    }

    public interface Material {
        ColorRGB Evaluate(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
        ColorRGB Sample(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath, Vector2 primarySample);
        (float, float) Pdf(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);
    }

    public struct DiffuseBsdf {
        public ColorRGB reflectance;

        public ColorRGB Evaluate(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            return ColorRGB.Black;
        }

        public ColorRGB Sample(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath, Vector2 primarySample) {
            return ColorRGB.Black;
        }

        public (float, float) Pdf(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) {
            return (0, 0);
        }
    }

    /// <summary>
    /// Basic uber-shader surface material that should suffice for most integrator experiments.
    /// </summary>
    public class GenericMaterial : Material {
        public struct Parameters {
            Image baseColor;
        }

        public GenericMaterial(Parameters parameters) => this.parameters = parameters;

        ColorRGB Material.Evaluate(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) 
            => throw new System.NotImplementedException();
        (float, float) Material.Pdf(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath) 
            => throw new System.NotImplementedException();
        ColorRGB Material.Sample(SurfacePoint point, Vector3 outDir, Vector3 inDir, bool isOnLightSubpath, Vector2 primarySample) 
            => throw new System.NotImplementedException();

        Parameters parameters;
    }
}
