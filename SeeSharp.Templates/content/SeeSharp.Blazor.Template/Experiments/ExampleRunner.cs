namespace SeeSharp.Blazor.Template.Experiments;

public class ExampleRunner : SceneBasedRunner
{
    public static FlipBook RenderedImageFlip { get; private set; }

    protected override void RunExperiment()
    {
        RenderedImageFlip = new FlipBook(660, 580)
            .SetToneMapper(FlipBook.InitialTMO.Exposure(Scene.RecommendedExposure))
            .SetToolVisibility(false);

        Scene.FrameBuffer = new(640, 480, "");
        Scene.Prepare();

        PathTracer pathTracer = new() { NumIterations = 1, MaxDepth = 10 };
        pathTracer.Render(Scene);

        RenderedImageFlip
            .Add("PT", Scene.FrameBuffer.Image)
            .Add(
                "Albedo",
                Scene
                    .FrameBuffer.LayerImages.Where(kv => kv.Name == "albedo")
                    .Select(kv => kv.Image)
                    .First()
            );
    }
}
