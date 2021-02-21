using SeeSharp.Integrators.Bidir;
using SeeSharp.Integrators.Util;
using System.Collections.Generic;
using SimpleImageIO;

namespace SeeSharp.IntegrationTests {
    static class BidirPathLogger_HomeOffice {
        public static void Run() {
            var scene = SeeSharp.Scene.LoadFromFile("../Data/Scenes/HomeOffice/office.json");
            scene.FrameBuffer = new SeeSharp.Image.FrameBuffer(640, 480, "test.exr",
                SeeSharp.Image.FrameBuffer.Flags.SendToTev);
            scene.Prepare();

            var integrator = new ClassicBidir() {
                NumIterations = 512,
                MaxDepth = 3,
                RenderTechniquePyramid = true,
                BaseSeedCamera = 971612, BaseSeedLight = 175037
            };
            integrator.PathLogger = new(640, 480);
            integrator.Render(scene);

            var paths = integrator.PathLogger.GetAllInPixel(150, 253, RgbColor.White * 0.1f);
            paths.AddRange(integrator.PathLogger.GetAllInPixel(148, 127, RgbColor.White * 100.0f));

            scene.FrameBuffer = new SeeSharp.Image.FrameBuffer(640, 480, "test-paths.exr",
                SeeSharp.Image.FrameBuffer.Flags.SendToTev);
            new PathVisualizer() {
                Radius = 0.0025f, HeadHeight = 0.005f,
                TypeToColor = new Dictionary<int, RgbColor> {
                    { 1, new RgbColor(0.9f, 0.01f, 0.01f) }
                },
                Paths = paths,
                TotalSpp = 4
            }.Render(scene);
        }
    }

    static class BidirPathLogger_IndirectRoom {
        public static void Run() {
            var scene = SeeSharp.Scene.LoadFromFile("../Data/Scenes/IndirectRoom/IndirectRoom.json");
            scene.FrameBuffer = new SeeSharp.Image.FrameBuffer(640, 480, "test.exr",
                SeeSharp.Image.FrameBuffer.Flags.SendToTev);
            scene.Prepare();

            var integrator = new ClassicBidir() {
                NumIterations = 4,
                MaxDepth = 3,
                RenderTechniquePyramid = true,
                BaseSeedCamera = 971612, BaseSeedLight = 175037
            };
            integrator.PathLogger = new(640, 480);
            integrator.Render(scene);

            var paths = integrator.PathLogger.GetAllInPixel(263, 294, RgbColor.White * 1.1f);
            paths.AddRange(integrator.PathLogger.GetAllInPixel(453, 323, RgbColor.White * 0.5f));

            scene.FrameBuffer = new SeeSharp.Image.FrameBuffer(640, 480, "test-paths.exr",
                SeeSharp.Image.FrameBuffer.Flags.SendToTev);
            new PathVisualizer() {
                Radius = 0.0025f, HeadHeight = 0.005f,
                TypeToColor = new Dictionary<int, RgbColor> {
                    { 1, new RgbColor(0.9f, 0.01f, 0.01f) }
                },
                Paths = paths,
                TotalSpp = 4
            }.Render(scene);
        }
    }
}