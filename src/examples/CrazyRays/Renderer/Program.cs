using GroundWrapper;
using Integrators;
using Integrators.Bidir;
using System;

namespace Renderer {

    class Program {
        static void Main(string[] args) {
            //Validate_DirectIllum.Validate();
            //Validate_SingleBounce.Validate();
            //return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            //var scene = Scene.LoadFromFile("../../data/scenes/breakfast/breakfast.json");
            var scene = Scene.LoadFromFile(@"G:\OneDrive\Graphics\TestScenes\Blender-Custom\SimpleTests\GlassBallDiffuser.json");

            stopwatch.Stop();
            Console.WriteLine($"Loading scene: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();

            scene.FrameBuffer = new Image(512, 512);
            scene.Prepare();

            stopwatch.Stop();
            Console.WriteLine($"Preparing scene: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            //{
            //    var algorithm = new DebugVisualizer();
            //    algorithm.Render(scene);
            //    scene.FrameBuffer.WriteToFile("DebugVis.exr");
            //}
            //scene.FrameBuffer = new Image(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
            //{
            //    var algorithm = new PathTracer();
            //    algorithm.TotalSpp = 2;
            //    algorithm.MaxDepth = 4;
            //    algorithm.MinDepth = 1;
            //    algorithm.Render(scene);
            //    scene.FrameBuffer.WriteToFile("PathTracer.exr");
            //}
            //scene.FrameBuffer = new Image(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
            {
                var algorithm = new ClassicBidir();
                algorithm.NumIterations = 1;
                algorithm.MaxDepth = 4;
                algorithm.Render(scene);
                scene.FrameBuffer.WriteToFile("ClassicBidir.exr");
            }
            scene.FrameBuffer = new Image(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
            {
                var algorithm = new VertexCacheBidir() {
                    NumIterations = 1,
                    NumConnections = 10,
                    MaxDepth = 4
                };
                algorithm.Render(scene);
                scene.FrameBuffer.WriteToFile("VertexCacheBidir.exr");
            }
            stopwatch.Stop();
            Console.WriteLine($"Rendering: {stopwatch.ElapsedMilliseconds}ms");
        }
    }

}
