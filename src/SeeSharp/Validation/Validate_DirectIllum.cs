using SeeSharp.Core;
using SeeSharp.Core.Cameras;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Core.Shading.Materials;
using SeeSharp.Core.Image;
using System.Numerics;

namespace SeeSharp.Validation {
    class Validate_DirectIllum : ValidationSceneFactory {
        public override int SamplesPerPixel => 10;

        public override int MaxDepth => 2;

        public override string Name => "DirectIllumination";

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
                baseColor = Image<ColorRGB>.Constant(ColorRGB.White)
            });

            // Emitter
            float emitterSize = 0.1f;
            //float emitterDepth = -1.9f;
            float emitterDepth = -1.0f;
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new Vector3(-emitterSize, -emitterSize, emitterDepth),
                new Vector3( emitterSize, -emitterSize, emitterDepth),
                new Vector3( emitterSize,  emitterSize, emitterDepth),
                new Vector3(-emitterSize,  emitterSize, emitterDepth),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }, new Vector3[] {
                new Vector3(0, 0, -1),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, -1),
                new Vector3(0, 0, -1),
            }));
            scene.Meshes[^1].Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image<ColorRGB>.Constant(ColorRGB.Black)
            });
            scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[^1], ColorRGB.White * 1000));

            scene.Camera = new PerspectiveCamera(Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY), 40, null);
            scene.FrameBuffer = new FrameBuffer(512, 512, "");

            scene.Prepare();

            return scene;
        }
    }
}
