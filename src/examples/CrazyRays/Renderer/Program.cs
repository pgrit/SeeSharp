using System;
using Integrators;

namespace Renderer {

    class Program {
        static void Main(string[] args) {
            var scene = new GroundWrapper.Scene();
            scene.SetupFrameBuffer(1024, 1024);
            scene.LoadSceneFile("../../data/scenes/cbox.json");
            // scene.LoadSceneFile("../../data/scenes/furnacebox.json");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //var algorithm = new PathTracer();
            Integrator algorithm = new ClassicBidir();
            algorithm.Render(scene);

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

            scene.frameBuffer.WriteToFile("renderCS.exr");
        }
    }

}
