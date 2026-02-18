using Microsoft.AspNetCore.Components;

namespace MaterialTest.Pages;

public partial class Experiment : ComponentBase {
    const int Width = 320;
    const int Height = 240;

    float roughness = 0.3f, indexOfRefraction = 1.4f, transmit = 0.0f, metallic = 0.0f;

    GenericMaterial mtl;
    Mesh mesh;
    SurfacePoint point;
    Vector3 outDir;

    float topRadiance = 1.0f;
    float[] select;
    float[] totals;
    void RunExperiment() {
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
        MonochromeImage[] pdfs = [
            new(Width, Height),
            new(Width, Height),
            new(Width, Height)
        ];
        RgbImage[] values = [
            new(Width, Height),
            new(Width, Height),
            new(Width, Height)
        ];
        select = new float[3];
        totals = new float[3];
        Parallel.For(0, Width, i => {
            Material.ComponentWeights components = new() {
                Pdfs = stackalloc float[3],
                PdfsReverse = stackalloc float[3],
                Values = stackalloc RgbColor[3],
                Weights = stackalloc float[3],
                WeightsReverse = stackalloc float[3],
                NumComponents = 3,
                NumComponentsReverse = 3
            };
            // for (int i = 0; i < Width; ++i) {
            for (int j = 0; j < Height; ++j) {
                for (int c = 0; c < 3; ++c) {
                    components.Pdfs[c] = 0;
                    components.Values[c] = RgbColor.Black;
                }

                float p = i / (float)Width * 2.0f * float.Pi;
                float t = j / (float)Height * float.Pi;
                var dirLocalIn = SampleWarp.SphericalToCartesian(float.Sin(t), float.Cos(t), p);
                var inDir = ShadingSpace.ShadingToWorld(point.ShadingNormal, dirLocalIn);

                var bsdf = shader.EvaluateWithCosine(inDir);
                var (pdfFwd, pdfRev) = shader.Pdf(inDir, ref components);

                pdfFwdImg[i, j] = pdfFwd;
                pdfRevImg[i, j] = pdfRev;
                bsdfImg[i, j] = bsdf.Average;

                for (int c = 0; c < 3; ++c) {
                    pdfs[c][i, j] = components.Pdfs[c];
                    values[c][i, j] = components.Values[c];
                    totals[c] += components.Values[c].Average / (Width * Height);
                }
            }
            float sum = 0;
            for (int c = 0; c < 3; ++c) {
                select[c] = components.Weights[c];
                sum += totals[c];
            }
            for (int c = 0; c < 3; ++c) {
                totals[c] /= sum;
            }
        });

        flip
            .Add("pdfFwd", pdfFwdImg)
            // .Add("pdfRev", pdfRevImg)
            .Add("bsdf", bsdfImg)
            .Add("p1", pdfs[0]).Add("p2", pdfs[1]).Add("p3", pdfs[2])
            .Add("v1", values[0]).Add("v2", values[1]).Add("v3", values[2]);

        MonochromeImage fwdHist = new(Width, Height);
        MonochromeImage revHist = new(Width, Height);
        Parallel.For(0, Width, i => {
            RNG rng = new(1337, (uint)i, 1);
            for (int j = 0; j < Height; ++j) {
                var worldDir = shader.Sample(rng.NextFloat(), rng.NextFloat2D()).Direction;
                var dir = shader.Context.WorldToShading(worldDir);
                var sph = SampleWarp.CartesianToSpherical(dir);
                int x = (int)(sph.X / (2.0f * float.Pi) * Width);
                int y = (int)(sph.Y / float.Pi * Height);
                fwdHist.AtomicAdd(x, y, 1);
            }
        });

        flip.Add("fwd hist", Filter.RepeatedBox(fwdHist, 3));

        RunRenderTest();
    }

    void OnFlipClick(FlipViewer.OnEventArgs args) {
        if (args.Control) {
            float p = args.MouseX / (float)Width * 2.0f * float.Pi;
            float t = args.MouseY / (float)Height * float.Pi;
            var dirLocalIn = SampleWarp.SphericalToCartesian(float.Sin(t), float.Cos(t), p);
            var inDir = ShadingSpace.ShadingToWorld(point.ShadingNormal, dirLocalIn);

            SurfaceShader shader = new(point, outDir, false);
            var bsdf = shader.Evaluate(inDir);
            var (pdfFwd, pdfRev) = shader.Pdf(inDir);

            // TODO output these
        }
    }

    void OnFlipRenderClick(FlipViewer.OnEventArgs args) {
        if (args.Control) {
            RNG rng = new(1337, (uint)args.MouseX, 1);
            for (int j = 0; j < args.MouseY; ++j) {
                rng.NextFloat();
                rng.NextFloat2D();
            }
            SurfaceShader shader = new(point, outDir, false);
            var s = shader.Sample(rng.NextFloat(), rng.NextFloat2D());

            var sp = SampleWarp.CartesianToSpherical(shader.Context.WorldToShading(s.Direction));
            thetaOut = sp.Y;
            phiOut = sp.X;

            var y = thetaOut / float.Pi * Height;
            var x = phiOut * 0.5f / float.Pi * Width;
            Console.WriteLine($"{x}, {y}");
        }
    }

    void RunRenderTest() {
        RgbImage img = new(Width, Height);
        RgbImage refimg = new(Width, Height);
        Parallel.For(0, Width, i => {
            RNG rng = new(1337, (uint)i, 1);
            RNG rngref = new(1337, (uint)i, 1);
            SurfaceShader shader = new(point, outDir, false);
            for (int j = 0; j < Height; ++j) {
                var s = shader.Sample(rng.NextFloat(), rng.NextFloat2D());
                img[i, j] = s.Weight * (s.Direction.Y < 0 ? 1.0f : topRadiance);

                for (int k = 0; k < 10; ++k) {
                    var sample = SampleWarp.ToUniformSphere(rngref.NextFloat2D());
                    refimg[i, j] = shader.EvaluateWithCosine(sample.Direction) * 4.0f * float.Pi;
                }
            }
        });

        fliprender = new FlipBook(660, 580)
            .SetZoom(FlipBook.InitialZoom.FillWidth)
            .SetToolVisibility(false)
            .SetToneMapper(FlipBook.InitialTMO.Exposure(-1));

        fliprender.Add("img", img).Add("ref", refimg);
    }

    async Task Download() {
        HtmlReport report = new();
        report.AddFlipBook(flip);
        report.AddFlipBook(fliprender);
        await report.DownloadAsFile(JS, "result.html");
    }
}