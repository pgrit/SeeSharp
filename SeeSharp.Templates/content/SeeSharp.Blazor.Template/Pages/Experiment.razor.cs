using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SeeSharp.Blazor;

namespace SeeSharp.Blazor.Template.Pages;

public partial class Experiment : BaseExperiment {
    public SimpleImageIO.FlipBook flip;

    long renderTimePT, renderTimeVCM;

    //Methods
    PathTracer pathTracer;
    VertexConnectionAndMerging vcm;

    /// <summary>
    /// Initializes all flipbooks
    /// </summary>
    void InitFlipbooks() {
        flip = new FlipBook(FlipWidth, FlipHeight)
            .SetZoom(FlipBook.InitialZoom.FillWidth)
            .SetToneMapper(FlipBook.InitialTMO.Exposure(scene.RecommendedExposure))
            .SetToolVisibility(false);
    }

    public override void RunExperiment() {
        InitFlipbooks();

        scene.FrameBuffer = new(Width, Height, "");
        scene.Prepare();

        pathTracer = new() {
            TotalSpp = NumSamples,
            MaxDepth = MaxDepth,
        };
        pathTracer.Render(scene);
        flip.Add($"PT", scene.FrameBuffer.Image);
        renderTimePT = scene.FrameBuffer.RenderTimeMs;

        scene.FrameBuffer = new(Width, Height, "");
        vcm = new() {
            NumIterations = NumSamples,
            MaxDepth = MaxDepth,
        };
        vcm.Render(scene);
        flip.Add($"VCM", scene.FrameBuffer.Image);
        renderTimeVCM = scene.FrameBuffer.RenderTimeMs;
    }

    /// <summary>
    /// Safes HTML of experiment
    /// </summary>
    /// <returns></returns>
    public override async Task OnDownloadClick() {
        HtmlReport report = new();
        report.AddMarkdown("""
        # Example experiment
        $$ L_\mathrm{o} = \int_\Omega L_\mathrm{i} f_\mathrm{r} |\cos\theta_\mathrm{i}| \, d\omega_\mathrm{i} $$
        """);
        report.AddFlipBook(flip);
        await SeeSharp.Blazor.Scripts.DownloadAsFile(JS, "report.html", report.ToString());
    }
}