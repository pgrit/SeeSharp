
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
                        average += (rgb.r + rgb.b + rgb.g) / (3 * img.Width * img.Height);
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

        static List<FrameBuffer> RenderImages(Scene scene, List<Integrator> algorithms, List<string> names, 
                                        string testname) {
            var images = new List<FrameBuffer>();

            var stopwatch  = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < algorithms.Count; ++i) {
                stopwatch.Restart();
                algorithms[i].Render(scene);
                stopwatch.Stop();
                Console.WriteLine($"Done with {names[i]} after {stopwatch.ElapsedMilliseconds}ms.");

                images.Add(scene.FrameBuffer);
                scene.FrameBuffer.WriteToFile(System.IO.Path.Join($"{testname}", $"{names[i]}.exr"));
                scene.FrameBuffer = new FrameBuffer(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
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
                    MaxDepth = sceneFactory.MaxDepth
                },
                new VertexCacheBidir() {
                    NumIterations = sceneFactory.SamplesPerPixel / 2,
                    MaxDepth = sceneFactory.MaxDepth,
                    NumLightPaths = 1000,
                    NumConnections = 4,
                    NumShadowRays = 4
                }
            };
            var names = new List<string> {
                "PathTracer",
                "ClassicBidir",
                "VertexCacheBidir"
            };

            var images = RenderImages(scene, algorithms, names, sceneFactory.Name);

            if (!ValidateImages(images)) {
                Console.WriteLine("Validation error: Average image values too far appart!");
            }
        }
    }
}