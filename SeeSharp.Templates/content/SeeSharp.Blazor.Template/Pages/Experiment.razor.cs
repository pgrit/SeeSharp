using Microsoft.AspNetCore.Components;

namespace SeeSharp.Blazor.Template.Pages;

public partial class Experiment : ComponentBase
{
    const int Width = 1280;
    const int Height = 720;
    const int MaxDepth = 10;

    int NumSamples = 2;

    long renderTimePT, renderTimeVCM;

    void RunExperiment()
    {
        flip = new FlipBook(660, 580)
            .SetZoom(FlipBook.InitialZoom.FillWidth)
            .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
            .SetToolVisibility(false);

        scene.FrameBuffer = new(Width, Height, "");
        scene.Prepare();

        PathTracer pathTracer = new()
        {
            TotalSpp = NumSamples,
            MaxDepth = MaxDepth,
        };
        pathTracer.Render(scene);
        flip.Add($"PT", scene.FrameBuffer.Image);
        renderTimePT = scene.FrameBuffer.RenderTimeMs;

        scene.FrameBuffer = new(Width, Height, "");
        VertexConnectionAndMerging vcm = new()
        {
            NumIterations = NumSamples,
            MaxDepth = MaxDepth,
        };
        vcm.Render(scene);
        flip.Add($"VCM", scene.FrameBuffer.Image);
        renderTimeVCM = scene.FrameBuffer.RenderTimeMs;
    }

    SurfacePoint? selected;

    void OnFlipClick(FlipViewer.OnClickEventArgs args)
    {
        if (args.CtrlKey)
        {
            selected = scene.RayCast(new(args.X, args.Y));
        }
    }

    async Task OnDownloadClick()
    {
        HtmlReport report = new();
        report.AddMarkdown("""
        # Example experiment
        $$ L_\mathrm{o} = \int_\Omega L_\mathrm{i} f_\mathrm{r} |\cos\theta_\mathrm{i}| \, d\omega_\mathrm{i} $$
        """);
        report.AddFlipBook(flip);
        await SeeSharp.Blazor.Scripts.DownloadAsFile(JS, "report.html", report.ToString());
    }
}