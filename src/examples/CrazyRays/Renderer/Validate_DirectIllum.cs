using GroundWrapper;
using GroundWrapper.Cameras;
using GroundWrapper.Geometry;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using GroundWrapper.Shading.Materials;
using Integrators;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Renderer {
    public static class Validate_DirectIllum {
        static Scene SetupQuadLightScene() {
            var scene = new Scene();

            // Ground plane
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new Vector3(-10, -10, -2),
                new Vector3( 10, -10, -2),
                new Vector3( 10,  10, -2),
                new Vector3(-10,  10, -2),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }));
            scene.Meshes[^1].Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.White)
            });

            // Emitter
            float emitterSize = 0.1f;
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new Vector3(-emitterSize, -emitterSize, -1.9f),
                new Vector3( emitterSize, -emitterSize, -1.9f),
                new Vector3( emitterSize,  emitterSize, -1.9f),
                new Vector3(-emitterSize,  emitterSize, -1.9f),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }, new Vector3[] {
                new Vector3(0, 0, -1),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, -1),
            }));
            scene.Meshes[^1].Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.Black)
            });
            scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[^1], ColorRGB.White * 1000));

            scene.Camera = new PerspectiveCamera(Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY), 40, null);
            scene.FrameBuffer = new Image(512, 512);

            scene.Prepare();

            return scene;
        }

        static bool ValidateImages(List<Image> images) {
            // Compute all mean values
            var means = new List<float>();
            foreach (var img in images) {
                float average = 0;
                for (int r = 0; r < img.Height; ++r) {
                    for (int c = 0; c < img.Width; ++c) {
                        var rgb = img[c, r];
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

        static List<Image> RenderImages(Scene scene, List<Integrator> algorithms, List<string> names) {
            var images = new List<Image>();

            for (int i = 0; i < algorithms.Count; ++i) {
                algorithms[i].Render(scene);
                images.Add(scene.FrameBuffer);
                scene.FrameBuffer.WriteToFile($"{names[i]}.exr");
                scene.FrameBuffer = new Image(scene.FrameBuffer.Width, scene.FrameBuffer.Height);
            }

            return images;
        }

        public static void Validate() {
            var scene = SetupQuadLightScene();

            var algorithms = new List<Integrator>() {
                new PathTracer() {
                    TotalSpp = 10,
                    MaxDepth = 2,
                    MinDepth = 1
                },
                new ClassicBidir() {
                    NumIterations = 10,
                    MaxDepth = 2
                }
            };
            var names = new List<string> {"PathTracer", "ClassicBidir"};

            var images = RenderImages(scene, algorithms, names);

            if (!ValidateImages(images)) {
                Console.WriteLine("Validation error: Average image values too far appart!");
            }
        }
    }
}
