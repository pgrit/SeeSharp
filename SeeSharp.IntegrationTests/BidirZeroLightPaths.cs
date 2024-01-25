using SeeSharp.Geometry;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;
using SeeSharp.Shading.Emitters;
using SimpleImageIO;
using System.Diagnostics;
using System.Numerics;

namespace SeeSharp.IntegrationTests {
    class Dummy : VertexConnectionAndMerging {
        protected override void OnStartIteration(uint iteration) {
            NumLightPaths = 0;
            LightPaths.NumPaths = 0;
        }

        protected override void OnNextEventSample(RgbColor weight, float misWeight, CameraPath cameraPath, float pdfNextEvent, float pdfHit, in BidirPathPdfs pathPdfs, Emitter emitter, Vector3 lightToSurface, SurfacePoint lightPoint) {
            Debug.Assert(float.IsFinite(misWeight));
        }
    }

    static class BidirZeroLightPaths {
        public static void Run() {
            var scene = Scene.LoadFromFile("Data/Scenes/CornellBox/CornellBox.json");
            scene.FrameBuffer = new Images.FrameBuffer(512, 512, "test.exr",
                Images.FrameBuffer.Flags.SendToTev);
            scene.Prepare();

            var integrator = new Dummy() {
                NumIterations = 4,
                MaxDepth = 5
            };
            integrator.Render(scene);

            scene.FrameBuffer = new Images.FrameBuffer(512, 512, "test.exr",
                Images.FrameBuffer.Flags.SendToTev);
            integrator.Render(scene);
        }
    }
}