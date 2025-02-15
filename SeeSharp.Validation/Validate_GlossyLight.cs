﻿using SeeSharp;
using SeeSharp.Cameras;
using SeeSharp.Geometry;
using SeeSharp.Shading;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SeeSharp.Images;
using System.Numerics;
using SimpleImageIO;

namespace SeeSharp.Validation {
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
                BaseColor = new TextureRgb(RgbColor.White),
                Transmitter = true
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
                BaseColor = new TextureRgb(RgbColor.Black)
            });
            scene.Emitters.AddRange(GlossyEmitter.MakeFromMesh(scene.Meshes[^1], RgbColor.White * 1000, 200));
            //scene.Emitters.Add(new DiffuseEmitter(scene.Meshes[^1], RgbColor.White * 1000));

            var matrix = Matrix4x4.CreateLookAt(Vector3.UnitZ * 2,
                                                -Vector3.UnitZ,
                                                Vector3.UnitY);
            scene.Camera = new PerspectiveCamera(matrix, 40);
            scene.FrameBuffer = new FrameBuffer(512, 512, "");

            scene.Prepare();

            return scene;
        }
    }
}