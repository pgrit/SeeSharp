using System;
using System.Collections.Generic;
using SeeSharp.Integrators;
using SeeSharp.Integrators.Bidir;
using SeeSharp.Images;

namespace SeeSharp.Validation;

class Validator {
    static bool ValidateImages(List<FrameBuffer> images) {
        // Compute all mean values
        var means = new List<float>();
        foreach (var img in images) {
            float average = 0;
            for (int r = 0; r < img.Height; ++r) {
                for (int c = 0; c < img.Width; ++c) {
                    var rgb = img.Image.GetPixel(c, r);
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

    static (List<FrameBuffer>, List<long>) RenderImages(Scene scene, IEnumerable<Integrator> algorithms,
                                                        IEnumerable<string> names, string testname, bool useTev) {
        var images = new List<FrameBuffer>();
        var times = new List<long>();
        Console.WriteLine($"Running test '{testname}'");

        var name = names.GetEnumerator();
        foreach (var alg in algorithms) {
            name.MoveNext();

            // Create a new empty frame buffer with the desired output filename
            scene.FrameBuffer = new FrameBuffer(scene.FrameBuffer.Width, scene.FrameBuffer.Height,
                System.IO.Path.Join("Results", $"{testname}", $"{name.Current}.exr"),
                useTev ? FrameBuffer.Flags.SendToTev : FrameBuffer.Flags.None);

            alg.Render(scene);

            Console.WriteLine($"Done with {name.Current} after {scene.FrameBuffer.RenderTimeMs}ms.");
            times.Add(scene.FrameBuffer.RenderTimeMs);

            images.Add(scene.FrameBuffer);
            scene.FrameBuffer.WriteToFile();
        }

        return (images, times);
    }

    public static List<long> Validate(ValidationSceneFactory sceneFactory, bool useTev) {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var scene = sceneFactory.MakeScene();
        stopwatch.Stop();
        var sceneLoadTime = stopwatch.ElapsedMilliseconds;

        var algorithms = new Dictionary<string, Integrator>() {
                { "PathTracer", new PathTracer() {
                   TotalSpp = sceneFactory.SamplesPerPixel,
                   MaxDepth = sceneFactory.MaxDepth,
                   RenderTechniquePyramid = false,
                }},
                { "ClassicBidir", new ClassicBidir() {
                    NumIterations = sceneFactory.SamplesPerPixel / 2,
                    MaxDepth = sceneFactory.MaxDepth,
                    RenderTechniquePyramid = false,
                }},
                { "Vcm", new VertexConnectionAndMerging() {
                    NumIterations = sceneFactory.SamplesPerPixel / 2,
                    MaxDepth = sceneFactory.MaxDepth,
                    RenderTechniquePyramid = false,
                }}
            };

        var (images, times) = RenderImages(scene, algorithms.Values, algorithms.Keys, sceneFactory.Name, useTev);

        if (!ValidateImages(images)) {
            Console.WriteLine("Validation error: Average image values too far appart!");
            throw new Exception("Validation error: Average image values too far appart!");
        }

        times.Add(sceneLoadTime);
        return times;
    }

    public static List<long> Benchmark(ValidationSceneFactory sceneFactory, int numTrials, bool useTev) {
        var totalTimes = Validate(sceneFactory, useTev);
        for (int i = 1; i < numTrials; ++i) {
            var times = Validate(sceneFactory, useTev);
            for (int k = 0; k < totalTimes.Count; ++k)
                totalTimes[k] += times[k];
        }

        // Normalize
        for (int k = 0; k < totalTimes.Count; ++k)
            totalTimes[k] /= numTrials;

        return totalTimes;
    }
}