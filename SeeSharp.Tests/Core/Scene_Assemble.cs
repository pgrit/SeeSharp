﻿using System.Reflection;

namespace SeeSharp.Tests.Core;

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
            BaseColor = new(new RgbColor(0, 0, 1))
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
            BaseColor = new(new RgbColor(1, 0, 0))
        });

        scene.Camera = new PerspectiveCamera(Matrix4x4.CreateLookAt(new Vector3(0, 0, 0),
            new Vector3(0, 5, 0), new Vector3(0, 0, 1)), 90);
        scene.FrameBuffer = new FrameBuffer(1, 1, "");

        scene.Emitters.AddRange(DiffuseEmitter.MakeFromMesh(scene.Meshes[0], new RgbColor(1, 1, 1)));

        return scene;
    }

    [Fact]
    public void TwoQuads_CorrectIntersections() {
        var scene = MakeDummyScene();

        scene.Prepare();
        RNG rng = new();
        var hit = scene.Raytracer.Trace(scene.Camera.GenerateRay(
            new Vector2(0.5f, 0.5f),
            ref rng
        ).Ray);

        Assert.True(hit);
        Assert.Equal(10.0f, hit.Distance, 4);
    }

    [Fact]
    public void TwoQuads_EmitterShouldBeSubdivided() {
        var scene = MakeDummyScene();

        scene.Prepare();

        Assert.True(scene.Emitters.Count == 2);

        SurfacePoint dummyHit = new SurfacePoint {
            Mesh = scene.Meshes[0]
        };
        Assert.Same(scene.Emitters[0], scene.QueryEmitter(dummyHit));

        dummyHit = new SurfacePoint {
            Mesh = scene.Meshes[1]
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
            new Vector3(1, 0, 0), new Vector3(0, 1, 0)), 90);
        scene.Camera = cam;
        scene.FrameBuffer = new FrameBuffer(10, 20, "");

        scene.Prepare();

        Assert.Equal(10, cam.Width);
        Assert.Equal(20, cam.Height);
    }

    [Fact]
    public void CornellBox_ShouldBeLoaded() {
        // Find the correct files
        var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().Location);
        var codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
        var dirPath = Path.GetDirectoryName(codeBasePath);
        var path = Path.Combine(dirPath, "../../../../Data/Scenes/CornellBox/CornellBox.json");
        bool e = File.Exists(path);
        Assert.True(e);

        ProgressBar.Silent = true;
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
