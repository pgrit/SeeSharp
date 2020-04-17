using Xunit;
using GroundWrapper.GroundMath;
using System;
using System.Numerics;

namespace GroundWrapper.Tests.Geometry {
    public class Mesh_Attributes {
        [Fact]
        public void ShadingNormals_ShouldBeSet() {
            var vertices = new Vector3[] {
                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3( 1, 0,  1),

                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3(-1, 0,  1)
            };

            var indices = new int[] {
                0, 1, 2,
                3, 4, 5
            };

            var normals = new Vector3[] {
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 0),

                new Vector3(1, 0, -1),
                new Vector3(1, 0, -1),
                new Vector3(1, 0, -1),
            };

            Mesh mesh = new Mesh(vertices, indices, shadingNormals: normals);

            var n1 = mesh.ComputeShadingNormal(0, new Vector2(0.5f, 0.5f));
            var n2 = mesh.ComputeShadingNormal(1, new Vector2(0.5f, 0.5f));

            Assert.Equal(1, n1.X, 3);
            Assert.Equal(0, n1.Y, 3);
            Assert.Equal(0, n1.Z, 3);

            Assert.Equal(1.0f / MathF.Sqrt(2), n2.X, 3);
            Assert.Equal(0, n2.Y, 3);
            Assert.Equal(-1.0f / MathF.Sqrt(2), n2.Z, 3);
        }

        [Fact]
        public void ShadingNormals_ShouldBeInitialized_CCW() {
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

            var n1 = mesh.ComputeShadingNormal(0, new Vector2(0.5f, 0.25f));
            var n2 = mesh.ComputeShadingNormal(0, new Vector2(0.25f, 0.5f));

            Assert.Equal(0, n1.X, 3);
            Assert.Equal(-1, n1.Y, 3);
            Assert.Equal(0, n1.Z, 3);

            Assert.Equal(0, n2.X, 3);
            Assert.Equal(-1, n2.Y, 3);
            Assert.Equal(0, n2.Z, 3);
        }
    }
}
