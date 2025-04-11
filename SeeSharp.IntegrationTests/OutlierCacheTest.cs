using SeeSharp.Experiments;
using SeeSharp.Images;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;
using SeeSharp.Integrators.Util;

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
        var q = scene.FrameBuffer.OutlierCache.GetPixelOutlier(pixel);
        float best = 0;
        int iteration = -1;
        foreach (var i in q.UnorderedItems) {
            if (i.Priority > best) {
                best = i.Priority;
                iteration = i.Element.Iteration;
            }
        }

        var graph = integrator.ReplayPixel(scene, pixel, iteration);

        scene.FrameBuffer = new(640, 480, "path.exr", FrameBuffer.Flags.SendToTev);
        PathGraphRenderer graphVis = new() {};
        graphVis.Render(scene, graph);

        // TODO UI:
        // - click on path to show data
        // - modify path coloration parameters
    }

    public static void RenderVCM() {
        SceneRegistry.AddSourceRelativeToScript("../Data/Scenes");
        using var scene = SceneRegistry.LoadScene("GlassCubes").MakeScene();

        scene.FrameBuffer = new FrameBuffer(640, 480, "testVCM.exr", FrameBuffer.Flags.SendToTev);
        scene.Prepare();

        var integrator = new CameraStoringVCM<byte>() {
            NumIterations = 1,
            MaxDepth = 10,
        };
        integrator.Render(scene);

        Pixel pixel = new(628, 428);

        // Get the strongest path sample in this pixel
        var q = scene.FrameBuffer.OutlierCache.GetPixelOutlier(pixel);
        float best = 0;
        int iteration = -1;
        foreach (var i in q.UnorderedItems) {
            if (i.Priority > best) {
                best = i.Priority;
                iteration = i.Element.Iteration;
            }
        }

        var graph = integrator.ReplayPixel(scene, pixel, iteration);

        scene.FrameBuffer = new(640, 480, "pathVCM.exr", FrameBuffer.Flags.SendToTev);
        PathGraphRenderer graphVis = new() {};
        graphVis.Render(scene, graph);

        System.IO.File.WriteAllText("VCMpaths.ply", graph.ConvertToPLY());
    }
}