using GroundWrapper;
using GroundWrapper.Cameras;
using GroundWrapper.Geometry;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using GroundWrapper.Shading.Materials;
using Integrators;
using Integrators.Bidir;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Renderer {
    public class Validate_SingleBounce {
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
            scene.Meshes[^1].Material = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.White)
            });

            // Emitter
            float emitterSize = 0.1f;
            float emitterDepth = 1.0f;
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new Vector3(-emitterSize, -emitterSize, emitterDepth),
                new Vector3( emitterSize, -emitterSize, emitterDepth),
                new Vector3( emitterSize,  emitterSize, emitterDepth),
                new Vector3(-emitterSize,  emitterSize, emitterDepth),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }, new Vector3[] {
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
            }));
            scene.Meshes[^1].Material = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.Black)
            });
            scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[^1], ColorRGB.White * 1000));

            // Reflector plane
            float reflectorDepth = 2.0f;
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new Vector3(-10, -10, reflectorDepth),
                new Vector3( 10, -10, reflectorDepth),
                new Vector3( 10,  10, reflectorDepth),
                new Vector3(-10,  10, reflectorDepth),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }, new Vector3[] {
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
            }));
            scene.Meshes[^1].Material = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.White)
            });

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

            var stopwatch  = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < algorithms.Count; ++i) {
                stopwatch.Restart();
                algorithms[i].Render(scene);
                stopwatch.Stop();
                Console.WriteLine($"Done with {names[i]} after {stopwatch.ElapsedMilliseconds}ms.");

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
                    MaxDepth = 3,
                    MinDepth = 1
                },
                new ClassicBidir() {
                    NumIterations = 10,
                    MaxDepth = 3
                },
                new StratifiedMultiConnect() {
                    NumIterations = 10,
                    MaxDepth = 3,
                    NumConnections = 4,
                    NumLightPaths = 10
                }
            };
            var names = new List<string> {
                "PathTracer",
                "ClassicBidir",
                "StratMultiBidir"
            };

            var images = RenderImages(scene, algorithms, names);

            if (!ValidateImages(images)) {
                Console.WriteLine("Validation error: Average image values too far appart!");
            }
        }
    }
}
