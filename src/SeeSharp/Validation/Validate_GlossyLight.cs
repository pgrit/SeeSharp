using SeeSharp.Core;
using SeeSharp.Core.Cameras;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Emitters;
using SeeSharp.Core.Shading.Materials;
using SeeSharp.Validation;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Validation {
    class Validate_GlossyLight : ValidationSceneFactory {
        public override int SamplesPerPixel => 10;

        public override int MaxDepth => 2;

        public override string Name => "GlossyLight";

        public override Scene MakeScene() {
            var scene = new Scene();

            // Ground plane
            scene.Meshes.Add(new Mesh(new Vector3[] {
                new Vector3(-10, -10, 0),
                new Vector3( 10, -10, 0),
                new Vector3( 10,  10, 0),
                new Vector3(-10,  10, 0),
            }, new int[] {
                0, 1, 2, 0, 2, 3
            }));
            scene.Meshes[^1].Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.White),
                transmitter = true
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
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 1),
            }));
            scene.Meshes[^1].Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(ColorRGB.Black)
            });
            scene.Emitters.Add(new GlossyEmitter(scene.Meshes[^1], ColorRGB.White * 1000, 200));
            //scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[^1], ColorRGB.White * 1000));

            var matrix = Matrix4x4.CreateLookAt(Vector3.UnitZ * 2,
                                                -Vector3.UnitZ,
                                                Vector3.UnitY);
            scene.Camera = new PerspectiveCamera(matrix, 40, null);
            scene.FrameBuffer = new FrameBuffer(512, 512, "");

            scene.Prepare();

            return scene;
        }
    }
}