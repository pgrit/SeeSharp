using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using TinyEmbree;

namespace SeeSharp.Core.Cameras {
    public readonly struct CameraRaySample {
        public readonly Ray Ray;
        public readonly ColorRGB Weight;
        public readonly SurfacePoint Point;
        public readonly float PdfRay;
        public readonly float PdfConnect;
        public CameraRaySample(Ray ray, ColorRGB weight, SurfacePoint point, float pdfRay,
                               float pdfConnect) {
            Ray = ray;
            Weight = weight;
            Point = point;
            PdfRay = pdfRay;
            PdfConnect = pdfConnect;
        }
    }
}
