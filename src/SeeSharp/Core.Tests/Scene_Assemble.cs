using SeeSharp.Core.Cameras;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Core.Shading.Materials;
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace SeeSharp.Core.Tests {
    public class Scene_Assemble {
        Scene MakeDummyScene() {
            var scene = new Scene();

            scene.Meshes.Add(new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                }
            ));

            scene.Meshes[^1].Material = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = Image.Constant(new ColorRGB(0, 0, 1))
            });

            scene.Meshes.Add(new Mesh(
                new Vector3[] {
                    new Vector3(-1, -10, -1),
                    new Vector3( 1, -10, -1),
                    new Vector3( 1, -10,  1),
                    new Vector3(-1, -10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                }
            ));

            scene.Meshes[^1].Material = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = Image.Constant(new ColorRGB(1, 0, 0))
            });

            scene.Camera = new PerspectiveCamera(Matrix4x4.CreateLookAt(new Vector3(0, 0, 0),
                new Vector3(0, 5, 0), new Vector3(0, 0, 1)), 90, null);
            scene.FrameBuffer = new FrameBuffer(1, 1, "");

            return scene;
        }

        [Fact]
        public void TwoQuads_CorrectIntersections() {
            var scene = MakeDummyScene();

            scene.Prepare();
            var hit = scene.Raytracer.Trace(scene.Camera.GenerateRay(new Vector2(0.5f, 0.5f)));

            Assert.True(hit);
            Assert.Equal(10.0f, hit.distance, 4);
        }

        [Fact]
        public void TwoQuads_EmitterShouldBeFound() {
            var scene = MakeDummyScene();

            scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[0], new ColorRGB(1, 1, 1)));
            scene.Prepare();

            Assert.Single(scene.Emitters);

            SurfacePoint dummyHit = new SurfacePoint {
                mesh = scene.Meshes[0]
            };
            Assert.Same(scene.Emitters[0], scene.QueryEmitter(dummyHit));

            dummyHit = new SurfacePoint {
                mesh = scene.Meshes[1]
            };
            Assert.Null(scene.QueryEmitter(dummyHit));
        }

        [Fact]
        public void TwoQuads_NoMaterial_ShouldBeInvalid() {
            var scene = MakeDummyScene();
            scene.Meshes[0].Material = null;

            Assert.False(scene.IsValid);
            Assert.Throws<System.InvalidOperationException>(scene.Prepare);
        }

        [Fact]
        public void Framebuffer_ShouldUpdateCamera() {
            var scene = MakeDummyScene();
            var cam = new PerspectiveCamera(Matrix4x4.CreateLookAt(new Vector3(0, 0, 0),
                new Vector3(1,0,0), new Vector3(0, 1, 0)), 90, null);
            scene.Camera = cam;
            scene.FrameBuffer = new FrameBuffer(10, 20, "");

            scene.Prepare();

            Assert.Equal(10, cam.Width);
            Assert.Equal(20, cam.Height);
        }

        [Fact]
        public void CornellBox_ShouldBeLoaded() {
            // Find the correct files
            var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            var codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
            var dirPath = Path.GetDirectoryName(codeBasePath);
            var path = Path.Combine(dirPath, "../../../../../../data/scenes/cbox.json");
            bool e = System.IO.File.Exists(path);
            Assert.True(e);

            var scene = Scene.LoadFromFile(path);

            // No frame buffer is set after loading, so this should evaluate to false
            Assert.False(scene.IsValid);
            Assert.Single(scene.ValidationErrorMessages);
            Assert.Contains("framebuffer", scene.ValidationErrorMessages[0].ToLower());

            // Scene should be correct after setting a framebuffer
            scene.FrameBuffer = new FrameBuffer(256, 128, "");

            Assert.True(scene.IsValid);
        }
    }
}
