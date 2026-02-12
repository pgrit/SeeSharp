using Microsoft.AspNetCore.Components;
using SeeSharp.Blazor;

namespace MaterialTest.Pages;

public partial class IntegratorTest : ComponentBase
{
    const int Width = 640;
    const int Height = 480;
    const int MaxDepth = 10;

    int NumSamples = 1;

    void RunExperiment()
    {
        flip = new FlipBook(660, 580)
            .SetZoom(FlipBook.InitialZoom.FillWidth)
            .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
            .SetToolVisibility(false);

        scene.FrameBuffer = new(Width, Height, null);
        scene.Prepare();
        VertexConnectionAndMerging vcm = new()
        {
            NumIterations = NumSamples,
            MaxDepth = MaxDepth,
            RenderTechniquePyramid = true
        };
        vcm.Render(scene);
        flip.Add($"VCM", scene.FrameBuffer.Image);

        flip.AddAll(vcm.TechPyramidRaw.GetImagesForPathLength(2));
    }

    SurfacePoint? selected;

    void OnFlipClick(FlipViewer.OnEventArgs args)
    {
        if (args.CtrlKey)
        {
            RNG rng = new(1241512);
            var ray = scene.Camera.GenerateRay(new Vector2(args.X + 0.5f, args.Y + 0.5f), ref rng).Ray;
            selected = (SurfacePoint)scene.Raytracer.Trace(ray);

            SurfaceShader shader = new(selected.Value, -ray.Direction, false);
            var s = shader.Sample(rng.NextFloat(), rng.NextFloat2D());
            Console.WriteLine(s);
        }
    }
}