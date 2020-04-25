using GroundWrapper;
using Integrators;
using System;

namespace Renderer {

    class Program {

        static void Main(string[] args) {
            //var scene = Scene.LoadFromFile("../../data/scenes/breakfast/breakfast.json");
            var scene = Scene.LoadFromFile(@"G:\OneDrive\Graphics\TestScenes\Blender-CC0\CleanLivingRoom\clean-living-room.json");
            scene.FrameBuffer = new Image(512, 512);
            scene.Prepare();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            //{
            //    var algorithm = new DebugVisualizer();
            //    algorithm.Render(scene);
            //    scene.FrameBuffer.WriteToFile("CboxPT.exr");
            //}
            //{
            //    var algorithm = new PathTracer();
            //    algorithm.TotalSpp = 2;
            //    algorithm.MaxDepth = 3;
            //    algorithm.MinDepth = 1;
            //    algorithm.Render(scene);
            //    scene.FrameBuffer.WriteToFile("CboxPT.exr");
            //}
            scene.FrameBuffer = new Image(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
            {
                var algorithm = new ClassicBidir();
                algorithm.NumIterations = 2;
                algorithm.MaxDepth = 3;
                algorithm.Render(scene);
                scene.FrameBuffer.WriteToFile("CboxClassicBidir.exr");
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

        }
    }

}
