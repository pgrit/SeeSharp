using SeeSharp.Common;
using SeeSharp.Experiments;
using SeeSharp.Integrators;

namespace SeeSharp.IntegrationTests;

class Program {
    public static void PathTracerTimeBudget() {
        var scene = SeeSharp.Scene.LoadFromFile("Data/Scenes/CornellBox/CornellBox.json");
        scene.FrameBuffer = new SeeSharp.Images.FrameBuffer(512, 512, "test.exr",
            SeeSharp.Images.FrameBuffer.Flags.SendToTev);
        scene.Prepare();

        var integrator = new PathTracer() {
            TotalSpp = 498989,
            MaximumRenderTimeMs = 4500,
            MaxDepth = 5
        };
        integrator.Render(scene);
        scene.FrameBuffer.WriteToFile();
    }

    static void BlenderAutoImport() {
        Logger.Verbosity = Verbosity.Debug;
        SceneRegistry.AddSourceRelativeToScript("../Data/Scenes");
        SceneRegistry.LoadScene("ExportTest");
    }

    static void Main(string[] args) {
        // BidirPathLogger_HomeOffice.Run();
        // BidirPathLogger_IndirectRoom.Run();

        // ConsoleUtils.TestProgressBar();
        // ConsoleUtils.TestLogger();

        // LightProbeTest.WhiteImage();
        // LightProbeTest.CornellProbe();

        // BidirZeroLightPaths.Run();
        // PathTracerTimeBudget();

        // BlenderAutoImport();
        // OutlierCacheTest.RenderPT();
        OutlierCacheTest.RenderVCM();
    }
}
