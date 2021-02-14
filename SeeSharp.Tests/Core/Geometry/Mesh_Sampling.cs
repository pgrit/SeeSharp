using SeeSharp.Geometry;
using System;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Geometry {
    public class Mesh_Sampling {
        [Fact]
        public void Distribution_TwoTriangles_ShouldBeUniform() {
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

            // Count the number of samples that end up on the first primitive
            int numSteps = 100;
            int samplesOnFirst = 0;
            for (float u = 0.0f; u <= 1.0f; u += 1.0f / numSteps) {
                for (float v = 0.0f; v <= 1.0f; v += 1.0f / numSteps) {
                    var sample = mesh.Sample(new Vector2(u, v));
                    if (sample.Point.PrimId == 0)
                        samplesOnFirst++;
                }
            }

            // Number of samples should be roughly half at the end
            // We allow a 5% error margin
            int numSamples = numSteps * numSteps;
            float ratio = (float)samplesOnFirst / numSamples;
            Assert.True(ratio < 0.55f);
        }

        [Fact]
        public void Distribution_TwoTriangles_ShouldBeProportional() {
            var vertices = new Vector3[] {
                new Vector3(-1, 0, -1),
                new Vector3( 1, 0, -1),
                new Vector3( 1, 0,  1),

                new Vector3(-1, 0, -1),
                new Vector3( 0, 0, -1),
                new Vector3( 0, 0,  0)
            };

            var indices = new int[] {
                0, 1, 2,
                3, 4, 5
            };

            Mesh mesh = new Mesh(vertices, indices);

            // Count the number of samples that end up on the first primitive
            int numSteps = 100;
            int samplesOnFirst = 0;
            for (float u = 0.0f; u <= 1.0f; u += 1.0f / numSteps) {
                for (float v = 0.0f; v <= 1.0f; v += 1.0f / numSteps) {
                    var sample = mesh.Sample(new Vector2(u, v));
                    if (sample.Point.PrimId == 0)
                        samplesOnFirst++;
                    Assert.Equal(1.0f / 2.5f, sample.Pdf, 2);
                }
            }

            // Number of samples should be four times higher in the first, larger, triangle
            int numSamples = numSteps * numSteps;
            float delta = samplesOnFirst - numSamples * 3.0f / 4.0f;
            Assert.True(delta < numSamples * 0.1);
        }

        [Fact]
        public void Distribution_Points_ShouldBeUniform() {
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

            // Count the number of samples that end up in each cell of a uniform grid
            int numSteps = 100;
            int res = 10;
            var grid = new int[res, res];
            void Splat(SurfaceSample s) {
                var relX = (s.Point.Position.X + 1.0f) / 2.0f;
                var xIdx = (int)Math.Max(Math.Min(relX * res, res - 1), 0);

                var relY = (s.Point.Position.Z + 1.0f) / 2.0f;
                var yIdx = (int)Math.Max(Math.Min(relY * res, res - 1), 0);

                grid[xIdx, yIdx]++;
            }

            for (float u = 0.0f; u <= 1.0f; u += 1.0f / numSteps) {
                for (float v = 0.0f; v <= 1.0f; v += 1.0f / numSteps) {
                    var sample = mesh.Sample(new Vector2(u, v));
                    Splat(sample);
                    Assert.Equal(0.25f, sample.Pdf, 3);
                }
            }

            // With some small margin of error, all cells should now have exactly one value
            int numFilled = 0;
            foreach (int count in grid) {
                if (count > 0)
                    numFilled++;
            }

            // At most 10 percent empty cells is acceptable
            Assert.True(Math.Abs(numFilled - res * res) < 10);
        }
    }
}
