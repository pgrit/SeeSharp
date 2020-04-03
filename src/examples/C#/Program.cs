using System;

namespace Experiments {

    class Program {
        static void Main(string[] args) {
            var scene = new Ground.Scene();
            scene.SetupFrameBuffer(512, 512);
            scene.LoadSceneFile("../../data/scenes/cbox.json");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var algorithm = new PathTracer();
            algorithm.Render(scene);

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

            scene.frameBuffer.WriteToFile("renderCS.exr");
        }
    }

}
