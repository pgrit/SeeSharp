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
    }
}
