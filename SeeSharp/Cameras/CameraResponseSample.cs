using SimpleImageIO;
using System.Numerics;

namespace SeeSharp.Cameras {
    public readonly struct CameraResponseSample {
            public readonly Vector2 Pixel;
            public readonly RgbColor Weight;
            public readonly float PdfConnect;
            public readonly float PdfEmit;

            public CameraResponseSample(Vector2 pixel, RgbColor weight, float pdfConnect, float pdfEmit) {
                Pixel = pixel;
                Weight = weight;
                PdfConnect = pdfConnect;
                PdfEmit = pdfEmit;
            }
        }
}
