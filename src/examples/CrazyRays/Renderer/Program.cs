using System;
using Integrators;

namespace Renderer {

    class Program {
        static void Main(string[] args) {
            var scene = new GroundWrapper.Scene();
            scene.SetupFrameBuffer(1024, 1024);
            scene.LoadSceneFile("../../data/scenes/cbox.json");
            // scene.LoadSceneFile("../../data/scenes/furnacebox.json");
            //scene.LoadSceneFile("../../data/scenes/simpledi.json");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //Integrator algorithm;
            //var algorithm = new PathTracer();
            var algorithm = new ClassicBidir();
            algorithm.NumIterations = 2;
            //var algorithm = new LightTracer();
            algorithm.Render(scene);

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

            scene.frameBuffer.WriteToFile("renderCS.exr");
        }
    }

}
