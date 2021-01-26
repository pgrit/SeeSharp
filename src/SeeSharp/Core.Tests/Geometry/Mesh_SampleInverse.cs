using SeeSharp.Core.Geometry;
using System.Numerics;
using Xunit;

namespace SeeSharp.Core.Tests.Geometry {
    public class Mesh_SampleInverse {
        [Fact]
        public void SingleTriangle() {
            Mesh tri = new(new Vector3[] {
                new(4, -1, 4),
                new(1, 3, 6),
                new(-7, 0, 3)
            }, new[] {
                0, 1, 2
            });

            var s = tri.Sample(new(0.1f, 0.8f));
            var p = tri.SampleInverse(s.Point);

            Assert.Equal(0.1f, p.X, 4);
            Assert.Equal(0.8f, p.Y, 4);
        }

        [Fact]
        public void TwoTriangles() {
            Mesh tri = new(new Vector3[] {
                new(4, -1, 4),
                new(1, 3, 6),
                new(-7, 0, 3),

                new(0, 0, 0),
                new(1, 0, 0),
                new(1, 1, 1),
            }, new[] {
                0, 1, 2, 
                3, 4, 5
            });

            var s = tri.Sample(new(0.1f, 0.8f));
            var p = tri.SampleInverse(s.Point);

            Assert.Equal(0.1f, p.X, 2);
            Assert.Equal(0.8f, p.Y, 2);

            var s2 = tri.Sample(new(0.75f, 0.8f));
            var p2 = tri.SampleInverse(s2.Point);

            Assert.Equal(0.75f, p2.X, 4);
            Assert.Equal(0.8f, p2.Y, 4);
        }
    }
}