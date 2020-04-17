using System;
using GroundWrapper;
using GroundWrapper.Geometry;
using System.Numerics;
using Integrators;

namespace Renderer {

    class Program {
        static void Main(string[] args) {

            var vertices = new Vector3[] {
                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3( 1, 0,  1),
                new Vector3(-1, 0,  1)
            };

            var indices = new int[] {
                0, 1, 2,
                0, 2, 3
            };

            Mesh mesh = new Mesh(vertices, indices);

            var rt = new Raytracer();
            rt.AddMesh(mesh);
            rt.CommitScene();

            Hit hit = rt.Intersect(new Ray {
                origin = new Vector3(-0.5f, -10, 0),
                direction = new Vector3(0, 1, 0),
                minDistance = 1.0f
            });

            System.Console.WriteLine(hit.distance);

            //var scene = new GroundWrapper.Scene();
            //scene.SetupFrameBuffer(1024, 1024);
            //scene.LoadSceneFile("../../data/scenes/cbox.json");
            //// scene.LoadSceneFile("../../data/scenes/furnacebox.json");
            ////scene.LoadSceneFile("../../data/scenes/simpledi.json");

            //var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ////Integrator algorithm;
            ////var algorithm = new PathTracer();
            //var algorithm = new ClassicBidir();
            //algorithm.NumIterations = 2;
            ////var algorithm = new LightTracer();
            //algorithm.Render(scene);

            //stopwatch.Stop();
            //Console.WriteLine(stopwatch.ElapsedMilliseconds);

            //scene.frameBuffer.WriteToFile("renderCS.exr");
        }
    }

}
