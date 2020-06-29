using SeeSharp.Core.Geometry;
using System.Numerics;
using Xunit;

namespace SeeSharp.Core.Tests.Geometry {
    public class Raytracer_Simple {
        [Fact]
        public void SimpleQuad_ShouldBeIntersected() {

            var vertices = new Vector3[] {
                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3( 1, 0,  1),
                new Vector3(-1, 0,  1)
            };

            var indices = new int[] {
                0, 1, 2,
                0, 2, 3
            };

            Mesh mesh = new Mesh(vertices, indices);

            var rt = new Raytracer();
            rt.AddMesh(mesh);
            rt.CommitScene();

            SurfacePoint hit = rt.Trace(new Ray {
                Origin = new Vector3(-0.5f, -10, 0),
                Direction = new Vector3(0, 1, 0),
                MinDistance = 1.0f
            });

            Assert.Equal(10.0f, hit.Distance, 0);
            Assert.Equal(1u, hit.PrimId);
            Assert.Equal(mesh, hit.Mesh);
        }

        [Fact]
        public void SimpleQuad_ShouldBeMissed() {
            var vertices = new Vector3[] {
                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3( 1, 0,  1),
                new Vector3(-1, 0,  1)
            };

            var indices = new int[] {
                0, 1, 2,
                0, 2, 3
            };

            Mesh mesh = new Mesh(vertices, indices);

            var rt = new Raytracer();
            rt.AddMesh(mesh);
            rt.CommitScene();

            SurfacePoint hit = rt.Trace(new Ray {
                Origin = new Vector3(-0.5f, -10, 0),
                Direction = new Vector3(0, -1, 0),
                MinDistance = 1.0f
            });

            Assert.False(hit);
        }
    }
}
