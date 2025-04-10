using SeeSharp.Cameras;
using SeeSharp.Geometry;
using SeeSharp.Shading.Emitters;
using SeeSharp.Shading.Materials;
using SeeSharp.Images;
using System;
using System.Numerics;
using SimpleImageIO;

namespace SeeSharp.Validation;

class Validate_SingleBounceGlossy : ValidationSceneFactory {
    public override int SamplesPerPixel => 32;
    public override int MaxDepth => 3;

    public override string Name => "SingleBounceGlossy";

    public override Scene MakeScene() {
        var scene = new Scene();

        // Create the camera
        var worldToCamera = Matrix4x4.CreateLookAt(-1 * Vector3.UnitZ,
                                                    Vector3.Zero,
                                                    Vector3.UnitY);
        scene.Camera = new PerspectiveCamera(worldToCamera, 45);

        // Create the two transmissive planes in-between
        // first plane
        var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0),
            }, new int[] {
                0, 1, 2,
                0, 2, 3,
            });
        mesh.Material = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new TextureRgb(RgbColor.White),
            Roughness = new TextureMono(0.5f),
            SpecularTransmittance = 1,
        });
        scene.Meshes.Add(mesh);
        // second plane
        mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 1),
                new Vector3( 1, -1, 1),
                new Vector3( 1,  1, 1),
                new Vector3(-1,  1, 1),
            }, new int[] {
                0, 1, 2,
                0, 2, 3,
            });
        mesh.Material = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new TextureRgb(RgbColor.White),
            Roughness = new TextureMono(0.5f),
        });
        scene.Meshes.Add(mesh);

        // Create the light source
        var lightArea = 0.1f;
        var pos = MathF.Sqrt(lightArea) / 2;
        var lightMesh = new Mesh(new Vector3[] {
                new Vector3(-pos, -pos, 1 + 10),
                new Vector3( pos, -pos, 1 + 10),
                new Vector3( pos,  pos, 1 + 10),
                new Vector3(-pos,  pos, 1 + 10),
            }, new int[] {
                0, 1, 2,
                0, 2, 3,
            }, new Vector3[] {
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitZ,
                -Vector3.UnitZ,
            });
        lightMesh.Material = new DiffuseMaterial(new DiffuseMaterial.Parameters {
            BaseColor = new TextureRgb(RgbColor.Black)
        });
        scene.Meshes.Add(lightMesh);
        var lightPower = 500;
        var radiance = lightPower / (MathF.PI * lightArea);
        var emitter = DiffuseEmitter.MakeFromMesh(lightMesh, RgbColor.White * radiance);
        scene.Emitters.AddRange(emitter);

        scene.FrameBuffer = new FrameBuffer(512, 512, "");

        scene.Prepare();

        return scene;
    }
}