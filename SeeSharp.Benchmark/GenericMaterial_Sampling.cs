using SeeSharp.Cameras;
using SeeSharp.Geometry;
using SeeSharp.Image;
using SeeSharp.Integrators;
using SeeSharp.Shading.Background;
using SeeSharp.Shading.Materials;
using SimpleImageIO;
using System;
using System.Numerics;
using System.Threading;

namespace SeeSharp.Benchmark {
    class GenericMaterial_Sampling {
        static Scene MakeScene(GenericMaterial.Parameters parameters) {
            Scene scene = new();

            // Create a quad
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new(-10, -10, -2),
                new( 10, -10, -2),
                new( 10,  10, -2),
                new(-10,  10, -2),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }));
            scene.Meshes[^1].Material = new GenericMaterial(parameters);

            // Set white background
            RgbImage whiteImage = new(1, 1);
            whiteImage.SetPixel(0, 0, RgbColor.White);
            scene.Background = new EnvironmentMap(whiteImage);

            // Set camera to look at quad
            scene.Camera = new PerspectiveCamera(
                Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY), 40);

            return scene;
        }

        static string MakeName(GenericMaterial.Parameters parameters) 
        => $"Results/r{parameters.Roughness.Lookup(new(0.5f, 0.5f))}-" +
            $"m{parameters.Metallic}-" +
            $"s{parameters.SpecularTransmittance}-" +
            $"ior{parameters.IndexOfRefraction}-" +
            $"d{parameters.DiffuseTransmittance}-" +
            $"thin{parameters.Thin}.exr";

        static float TestRender(GenericMaterial.Parameters parameters, string name) {
            var scene = MakeScene(parameters);
            scene.FrameBuffer = new(512, 512, name, FrameBuffer.Flags.SendToTev);
            scene.Prepare();

            PathTracer integrator = new() {
                TotalSpp = 1,
                NumShadowRays = 0,
                MaxDepth = 2,
            };

            integrator.Render(scene);
            scene.FrameBuffer.WriteToFile();

            var error = scene.FrameBuffer.PixelVariance.Average;
            Console.WriteLine($"{name}: {error}");
            return error;
        }

        public static void QuickTest() {
            GenericMaterial.Parameters highIOR = new() {
                Roughness = new(0.3199999928474426f),
                Anisotropic = 0.0f,
                DiffuseTransmittance = 0.0f,
                IndexOfRefraction = 1.4500000476837158f,
                Metallic = 0.0f,
                SpecularTintStrength = 0.0f,
                SpecularTransmittance = 0.0f,
                Thin = false,
            };

            TestRender(highIOR, "highIOR");

            GenericMaterial.Parameters lowIOR = new() {
                Roughness = new(0.3199999928474426f),
                Anisotropic = 0.0f,
                DiffuseTransmittance = 0.0f,
                IndexOfRefraction = 1.01f,
                Metallic = 0.0f,
                SpecularTintStrength = 0.0f,
                SpecularTransmittance = 0.0f,
                Thin = false,
            };

            TestRender(lowIOR, "lowIOR");

            GenericMaterial.Parameters glass = new() {
                Roughness = new(0.003199999928474426f),
                Anisotropic = 0.0f,
                DiffuseTransmittance = 0.0f,
                IndexOfRefraction = 1.4500000476837158f,
                Metallic = 0.0f,
                SpecularTintStrength = 0.0f,
                SpecularTransmittance = 1.0f,
                Thin = false,
            };

            TestRender(glass, "glass");

            GenericMaterial.Parameters mirror = new() {
                Roughness = new(0.003199999928474426f),
                Anisotropic = 0.0f,
                DiffuseTransmittance = 0.0f,
                IndexOfRefraction = 1.4500000476837158f,
                Metallic = 0.8f,
                SpecularTintStrength = 0.0f,
                SpecularTransmittance = 0.0f,
                Thin = false,
            };

            TestRender(mirror, "mirror");
        }

        public static void Benchmark(int numSteps=2) {
            GenericMaterial.Parameters parameters = new() {
                BaseColor = new(RgbColor.White),
                SpecularTintStrength = 0
            };

            // Generate all combinations
            int numTests = 0;
            float totalError = 0;
            float minError = 1000;
            float maxError = 0;
            for (int i = 0; i < numSteps; ++i) {
                parameters.Roughness = new((0.5f + i) / numSteps);
                for (int j = 0; j < numSteps; ++j) {
                    parameters.Metallic = (0.5f + j) / numSteps;
                    for (int l = 0; l < numSteps; ++l) {
                        parameters.SpecularTransmittance = (0.5f + l) / numSteps;
                        for (int m = 0; m < numSteps + 1; ++m) {
                            float t = m / (numSteps + 1.0f);
                            parameters.IndexOfRefraction = t * 1 + (1 - t) * 2;
                            for (int n = 0; n < numSteps; ++n) {
                                parameters.DiffuseTransmittance = (0.5f * n) / numSteps;
                                parameters.Thin = false;
                                float e = TestRender(parameters, MakeName(parameters));
                                totalError += e;
                                minError = Math.Min(e, minError);
                                maxError = Math.Max(e, maxError);
                                numTests++;

                                parameters.Thin = true;
                                e = TestRender(parameters, MakeName(parameters));
                                totalError += e;
                                minError = Math.Min(e, minError);
                                maxError = Math.Max(e, maxError);
                                numTests++;
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"Total: {totalError}");
            Console.WriteLine($"Average: {totalError / numTests}");
            Console.WriteLine($"Min: {minError}");
            Console.WriteLine($"Max: {maxError}");
        }
    }
}