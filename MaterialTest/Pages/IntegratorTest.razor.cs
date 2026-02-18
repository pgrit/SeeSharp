using Microsoft.AspNetCore.Components;

namespace MaterialTest.Pages;

public partial class IntegratorTest : ComponentBase
{
    const int Width = 640;
    const int Height = 480;
    const int MaxDepth = 10;

    int NumSamples = 1;

    SceneSelector sceneSelector;
    Scene scene;
    bool readyToRun = false;
    bool running = false;
    bool sceneJustLoaded = false;
    bool resultsAvailable = false;
    ElementReference runButton;

    SimpleImageIO.FlipBook flip;

    async Task OnSceneLoaded(SceneDirectory sceneDir)
    {
        await Task.Run(() => scene = sceneDir.SceneLoader.Scene);
        flip = null;
        resultsAvailable = false;
        readyToRun = true;
        sceneJustLoaded = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (readyToRun && sceneJustLoaded)
        {
            await runButton.FocusAsync();
        }

        sceneJustLoaded = false;
    }

    async Task OnRunClick()
    {
        readyToRun = false;
        resultsAvailable = false;
        running = true;
        await Task.Run(() => RunExperiment());
        readyToRun = true;
        running = false;
        resultsAvailable = true;
    }

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
            NumIterations = (uint)NumSamples,
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
        if (args.Control)
        {
            RNG rng = new(1241512);
            var ray = scene.Camera.GenerateRay(new Vector2(args.MouseX + 0.5f, args.MouseY + 0.5f), ref rng).Ray;
            selected = (SurfacePoint)scene.Raytracer.Trace(ray);

            SurfaceShader shader = new(selected.Value, -ray.Direction, false);
            var s = shader.Sample(rng.NextFloat(), rng.NextFloat2D());
            Console.WriteLine(s);
        }
    }
}