using GroundWrapper.Geometry;
using System.Numerics;
using Xunit;

namespace GroundWrapper.Tests.Geometry {
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

            Hit hit = rt.Intersect(new Ray {
                origin = new Vector3(-0.5f, -10, 0),
                direction = new Vector3(0, 1, 0),
                minDistance = 1.0f
            });

            Assert.Equal(10.0f, hit.distance, 0);
            Assert.Equal(1u, hit.primId);
            Assert.Equal(mesh, hit.mesh);
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

            Hit hit = rt.Intersect(new Ray {
                origin = new Vector3(-0.5f, -10, 0),
                direction = new Vector3(0, -1, 0),
                minDistance = 1.0f
            });

            Assert.False(hit);
        }
    }
}
