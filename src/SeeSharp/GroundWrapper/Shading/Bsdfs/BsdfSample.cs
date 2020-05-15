using System.Numerics;

namespace GroundWrapper.Shading.Bsdfs {
    public struct BsdfSample {
        public Vector3 direction;
        public float pdf;
        public float pdfReverse;

        /// <summary>
        /// Sample weight of the reflectance estimate, i.e., the product of 
        /// BSDF and shading cosine divided by the pdf.
        /// </summary>
        public ColorRGB weight;

        public static BsdfSample Invalid 
            => new BsdfSample { pdf = 0, pdfReverse = 0, weight = ColorRGB.Black };

        public static implicit operator bool(BsdfSample sample)
            => sample.pdf > 0 && sample.pdfReverse > 0;
    }
}
