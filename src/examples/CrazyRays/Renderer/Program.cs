using System;
using GroundWrapper;
using GroundWrapper.Geometry;
using System.Numerics;
using Integrators;

namespace Renderer {

    class Program {
        static void Main(string[] args) {
            var scene = Scene.LoadFromFile("../../data/scenes/cbox.json");
            scene.FrameBuffer = new Image(512, 512);
            scene.Prepare();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //var algorithm = new PathTracer();
            //algorithm.TotalSpp = 10;
            //algorithm.MaxDepth = 3;

            var algorithm = new ClassicBidir();
            algorithm.NumIterations = 10;
            algorithm.MaxDepth = 3;

            algorithm.Render(scene);

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

            scene.FrameBuffer.WriteToFile("renderCS.exr");
        }
    }

}
