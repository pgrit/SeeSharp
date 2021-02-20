using System.Numerics;
using SeeSharp;
using SeeSharp.Cameras;
using SeeSharp.Geometry;
using SeeSharp.Shading;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SeeSharp.Image;
using SimpleImageIO;

namespace SeeSharp.Validation {
    class Validate_MultiLight : ValidationSceneFactory {
        public override int SamplesPerPixel => 10;

        public override int MaxDepth => 2;

        public override string Name => "MultiLight";

        public override Scene MakeScene() {
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
                baseColor = new TextureRgb(RgbColor.White)
            });

            // Three emitters
            float emitterSize = 0.1f;
            float emitterDepth = -1.0f;

            void AddEmitter(float offsetX, float offsetY) {
                scene.Meshes.Add(new Mesh(new Vector3[] {
                    new Vector3(-emitterSize + offsetX, -emitterSize + offsetY, emitterDepth),
                    new Vector3( emitterSize + offsetX, -emitterSize + offsetY, emitterDepth),
                    new Vector3( emitterSize + offsetX,  emitterSize + offsetY, emitterDepth),
                    new Vector3(-emitterSize + offsetX,  emitterSize + offsetY, emitterDepth),
                }, new int[] {
                    0, 1, 2, 0, 2, 3
                }, new Vector3[] {
                    new Vector3(0, 0, -1),
                    new Vector3(0, 0, -1),
                    new Vector3(0, 0, -1),
                    new Vector3(0, 0, -1),
                }));
                scene.Meshes[^1].Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                    baseColor = new TextureRgb(RgbColor.Black)
                });
                scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[^1], RgbColor.White * 1000));
            }
            AddEmitter(-1, 0);
            AddEmitter(1, 0);

            scene.Camera = new PerspectiveCamera(Matrix4x4.CreateLookAt(
                Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY), 40, null);
            scene.FrameBuffer = new FrameBuffer(512, 512, "");

            scene.Prepare();

            return scene;
        }
    }
}