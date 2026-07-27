using SeeSharp.Images;
using SeeSharp.Integrators.Bidir;
using SeeSharp.SceneManagement;

namespace SeeSharp.IntegrationTests;

class RenderTest {
    public static void RenderVCM() {
        SceneRegistry.AddSourceRelativeToScript("../Data/Scenes");
        using var scene = SceneRegistry.Find("CornellBox").SceneLoader.Scene;

        scene.FrameBuffer = new FrameBuffer(640, 480, "testVCM.exr", FrameBuffer.Flags.SendToTev);
        scene.Prepare();

        // Use this to test if parameter combinations for technique flags work as intended
        var integrator = new CameraStoringVCM<byte>() {
            NumIterations = 10,
            MaxDepth = 2,
            EnableConnections = false,
            NumLightPaths = 0,
            NumShadowRays = 0,
            EnableHitting = true,
            EnableMerging = false,
            MergePrimary = false,
            EnableLightTracer = false,
        };
        integrator.Render(scene);
        scene.FrameBuffer.WriteToFile();
    }
}