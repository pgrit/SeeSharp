using SeeSharp.Cameras;
using SeeSharp.Integrators.Bidir;
using SeeSharp.Sampling;
using System.Numerics;
using System.Threading.Tasks;
using TinyEmbree;

namespace SeeSharp.IntegrationTests {
    class LightProbeTest {
        public static void WhiteImage() {
            // A simple plane with the z axis as its normal
            Raytracer raytracer = new();
            raytracer.AddMesh(new(new Vector3[] {
                new(-1, -1, 0),
                new( 1, -1, 0),
                new( 1,  1, 0),
                new(-1,  1, 0)
            }, new[] { 0, 1, 2, 0, 2, 3 }));

            // The same plane again, behind this time, to catch leaked rays
            raytracer.AddMesh(new(new Vector3[] {
                new(-1, -1, -1),
                new( 1, -1, -1),
                new( 1,  1, -1),
                new(-1,  1, -1)
            }, new[] { 0, 1, 2, 0, 2, 3 }));
            raytracer.CommitScene();

            // Find the center point via ray tracing so we get a good error offset
            Hit hit = raytracer.Trace(new Ray {
                Origin = Vector3.UnitZ,
                Direction = -Vector3.UnitZ,
                MinDistance = 0
            });

            var camera = new LightProbeCamera(hit.Position, hit.Normal, hit.ErrorOffset, Vector3.UnitY);
            var framebuffer = new Images.FrameBuffer(512, 256, "WhiteProbe.exr", Images.FrameBuffer.Flags.SendToTev);
            camera.UpdateResolution(framebuffer.Width, framebuffer.Height);

            int numIter = 10;
            for (int i = 0; i < numIter; ++i) {
                framebuffer.StartIteration();
                Parallel.For(0, 256, row => {
                    for (int col = 0; col < 512; ++col) {
                        RNG rng = new(1890481209, (uint)numIter, (uint)(row * 256 * col));

                        var pixel = rng.NextFloat2D();
                        pixel.X += col;
                        pixel.Y += row;

                        var sample = camera.GenerateRay(pixel, ref rng);
                        if (!raytracer.Trace(sample.Ray)) {
                            framebuffer.Splat(col, row, sample.Weight);
                        } else {
                            framebuffer.Splat(col, row, new(1, 0, 1));
                        }
                    }
                });
                framebuffer.EndIteration();
            }

            framebuffer.WriteToFile();
        }

        public static void CornellProbe() {
            var scene = Scene.LoadFromFile("../Data/Scenes/CornellBox/CornellBox.json");
            scene.FrameBuffer = new(512, 512, "CornellProbe.exr", Images.FrameBuffer.Flags.SendToTev);
            scene.Prepare();

            RNG rng = new();
            var sample = scene.Camera.GenerateRay(new(250, 50), ref rng);
            var point = scene.Raytracer.Trace(sample.Ray);

            // Make sure the normal is on the right side of the surface
            var normal = point.Normal;
            if (Vector3.Dot(point.Normal, -sample.Ray.Direction) < 0)
                normal *= -1;

            scene.Camera = new LightProbeCamera(point.Position, normal, point.ErrorOffset, Vector3.UnitY);
            scene.Prepare();

            ClassicBidir integrator = new() {
                NumIterations = 4,
                RenderTechniquePyramid = true
            };
            integrator.Render(scene);

            scene.FrameBuffer.WriteToFile();
        }
    }
}
