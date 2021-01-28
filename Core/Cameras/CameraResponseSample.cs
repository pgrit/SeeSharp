using SeeSharp.Core.Shading;
using System.Numerics;

namespace SeeSharp.Core.Cameras {
    public readonly struct CameraResponseSample {
            public readonly Vector2 Pixel;
            public readonly ColorRGB Weight;
            public readonly float PdfConnect;
            public readonly float PdfEmit;

            public CameraResponseSample(Vector2 pixel, ColorRGB weight, float pdfConnect, float pdfEmit) {
                Pixel = pixel;
                Weight = weight;
                PdfConnect = pdfConnect;
                PdfEmit = pdfEmit;
            }
        }
}
