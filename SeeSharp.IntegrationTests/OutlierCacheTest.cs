using SeeSharp.Experiments;
using SeeSharp.Images;
using SeeSharp.Integrators;

namespace SeeSharp.IntegrationTests;

static class OutlierCacheTest {
    public static void RenderPT() {
        SceneRegistry.AddSourceRelativeToScript("../Data/Scenes");
        using var scene = SceneRegistry.LoadScene("GlassCubes").MakeScene();

        scene.FrameBuffer = new FrameBuffer(640, 480, "test.exr", FrameBuffer.Flags.SendToTev);
        scene.Prepare();

        var integrator = new PathTracer() {
            TotalSpp = 4,
            MaxDepth = 10,
            BaseSeed = 1234,
        };
        integrator.Render(scene);

        Pixel pixel = new(628, 428);

        // Get the strongest path sample in this pixel
        var q = integrator.OutlierCache.GetPixelOutlier(pixel);
        float best = 0;
        int iteration = -1;
        foreach (var i in q.UnorderedItems) {
            if (i.Priority > best) {
                best = i.Priority;
                iteration = i.Element.LocalReplayInfo;
            }
        }

        var graph = integrator.ReplayPath(scene, pixel, 640, integrator.OutlierCache.GlobalReplayInfo, iteration);

        scene.FrameBuffer = new(640, 480, "path.exr", FrameBuffer.Flags.SendToTev);
        PathGraphRenderer graphVis = new() {};
        graphVis.Render(scene, graph);

        // TODO UI:
        // - click on path to show data
        // - modify path coloration parameters


    }
}