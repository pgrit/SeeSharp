using System;
using System.Collections.Generic;
using SeeSharp.Core;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;

namespace SeeSharp.Validation {
    class Validator {
        static bool ValidateImages(List<FrameBuffer> images) {
            // Compute all mean values
            var means = new List<float>();
            foreach (var img in images) {
                float average = 0;
                for (int r = 0; r < img.Height; ++r) {
                    for (int c = 0; c < img.Width; ++c) {
                        var rgb = img.image[c, r];
                        average += (rgb.R + rgb.B + rgb.G) / (3 * img.Width * img.Height);
                    }
                }
                means.Add(average);
            }

            // Check that they are within a small margin of error (1%)
            foreach (var m in means)
                if (Math.Abs(m - means[0]) > means[0] * 0.01)
                    return false;

            return true;
        }

        static List<FrameBuffer> RenderImages(Scene scene, List<Integrator> algorithms,
                                              List<string> names, string testname) {
            var images = new List<FrameBuffer>();
            Console.WriteLine($"Running test '{testname}'");

            var stopwatch  = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < algorithms.Count; ++i) {
                // Create a new empty frame buffer with the desired output filename
                scene.FrameBuffer = new FrameBuffer(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                                                    System.IO.Path.Join($"{testname}", $"{names[i]}.exr"));

                stopwatch.Restart();
                algorithms[i].Render(scene);
                stopwatch.Stop();
                Console.WriteLine($"Done with {names[i]} after {stopwatch.ElapsedMilliseconds}ms.");

                images.Add(scene.FrameBuffer);
                scene.FrameBuffer.WriteToFile();
            }

            return images;
        }

        public static void Validate(ValidationSceneFactory sceneFactory) {
            var scene = sceneFactory.MakeScene();

            var algorithms = new List<Integrator>() {
                new PathTracer() {
                    TotalSpp = sceneFactory.SamplesPerPixel,
                    MaxDepth = (uint)sceneFactory.MaxDepth,
                    MinDepth = 1
                },
                new ClassicBidir() {
                    NumIterations = sceneFactory.SamplesPerPixel / 2,
                    MaxDepth = sceneFactory.MaxDepth,
                    RenderTechniquePyramid = true
                },
                //new VertexCacheBidir() {
                //    NumIterations = sceneFactory.SamplesPerPixel / 2,
                //    MaxDepth = sceneFactory.MaxDepth,
                //    NumLightPaths = 1000,
                //    NumConnections = 4,
                //    NumShadowRays = 4,
                //    RenderTechniquePyramid = true
                //}
            };
            var names = new List<string> {
                "PathTracer",
                "ClassicBidir",
                //"VertexCacheBidir"
            };

            var images = RenderImages(scene, algorithms, names, sceneFactory.Name);

            if (!ValidateImages(images)) {
                Console.WriteLine("Validation error: Average image values too far appart!");
            }
        }
    }
}