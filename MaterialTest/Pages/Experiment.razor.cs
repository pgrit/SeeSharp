using Microsoft.AspNetCore.Components;

namespace MaterialTest.Pages;

public partial class Experiment : ComponentBase
{
    const int Width = 320;
    const int Height = 240;

    float roughness = 1, indexOfRefraction = 1.4f, transmit = 1.0f, metallic = 0.0f;

    GenericMaterial mtl;
    Mesh mesh;
    SurfacePoint point;
    Vector3 outDir;

    void RunExperiment()
    {
        flip = new FlipBook(660, 580)
            .SetZoom(FlipBook.InitialZoom.FillWidth)
            .SetToolVisibility(false);

        mtl = new(new() {
            BaseColor = new(RgbColor.White),
            Anisotropic = 0,
            IndexOfRefraction = indexOfRefraction,
            Metallic = metallic,
            Roughness = new(roughness),
            SpecularTintStrength = 0.0f,
            SpecularTransmittance = transmit,
        });

        mesh = MeshFactory.MakeAABB(new(Vector3.Zero, Vector3.One));
        mesh.Material = mtl;
        point = mesh.Sample(new(0.2f, 0.8f)).Point;

        var dirLocal = SampleWarp.SphericalToCartesian(float.Sin(theta), float.Cos(theta), phi);
        outDir = ShadingSpace.ShadingToWorld(point.ShadingNormal, dirLocal);

        SurfaceShader shader = new(point, outDir, false);

        MonochromeImage pdfFwdImg = new(Width, Height);
        MonochromeImage pdfRevImg = new(Width, Height);
        MonochromeImage bsdfImg = new(Width, Height);
        Parallel.For(0, Width, i => {
            // for (int i = 0; i < Width; ++i) {
            for (int j = 0; j < Height; ++j) {
                float p = i / (float)Width * 2.0f * float.Pi;
                float t = j / (float)Height * float.Pi;
                var dirLocalIn = SampleWarp.SphericalToCartesian(float.Sin(t), float.Cos(t), p);
                var inDir = ShadingSpace.ShadingToWorld(point.ShadingNormal, dirLocalIn);

                var bsdf = shader.Evaluate(inDir);
                var (pdfFwd, pdfRev) = shader.Pdf(inDir);

                pdfFwdImg[i, j] = pdfFwd;
                pdfRevImg[i, j] = pdfRev;
                bsdfImg[i, j] = bsdf.Average;
            }
        });

        flip.Add("pdfFwd", pdfFwdImg).Add("pdfRev", pdfRevImg).Add("bsdf", bsdfImg);

        MonochromeImage fwdHist = new(Width, Height);
        MonochromeImage revHist = new(Width, Height);
        Parallel.For(0, Width, i => {
            RNG rng = new(1337, (uint)i, 1);
            for (int j = 0; j < Height; ++j) {
                var worldDir = shader.Sample(rng.NextFloat2D()).Direction;
                var dir = shader.Context.WorldToShading(worldDir);
                var sph = SampleWarp.CartesianToSpherical(dir);
                int x = (int)(sph.X / (2.0f * float.Pi) * Width);
                int y = (int)(sph.Y / float.Pi * Height);
                fwdHist.AtomicAdd(x, y, 1);
            }
        });

        flip.Add("fwd hist", Filter.RepeatedBox(fwdHist, 3));
    }

    void OnFlipClick(FlipViewer.OnClickEventArgs args)
    {
        if (args.CtrlKey)
        {
            float p = args.X / (float)Width * 2.0f * float.Pi;
            float t = args.Y / (float)Height * float.Pi;
            var dirLocalIn = SampleWarp.SphericalToCartesian(float.Sin(t), float.Cos(t), p);
            var inDir = ShadingSpace.ShadingToWorld(point.ShadingNormal, dirLocalIn);

            SurfaceShader shader = new(point, outDir, false);
            var bsdf = shader.Evaluate(inDir);
            var (pdfFwd, pdfRev) = shader.Pdf(inDir);
        }
    }
}