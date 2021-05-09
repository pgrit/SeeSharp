using SeeSharp.Sampling;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Geometry {
    /// <summary>
    /// Provides utility functions to generate some simple meshes with basic shapes
    /// </summary>
    public static class MeshFactory {
        /// <summary>
        /// Creates a cylinder that connects two given points like a pipe
        /// </summary>
        /// <param name="from">The first point, the center of one cylinder disc</param>
        /// <param name="to">The second point, the center of the other cylinder disc</param>
        /// <param name="radius">Radius of the cylinder</param>
        /// <param name="numSegments">Number of quads used to build the outer surface</param>
        public static Mesh MakeCylinder(Vector3 from, Vector3 to, float radius, int numSegments) {
            SampleWarp.ComputeBasisVectors(Vector3.Normalize(to - from), out var tan, out var binorm);

            List<Vector3> vertices = new();
            List<int> indices = new();
            for (int i = 0; i < numSegments; ++i) {
                float angle = i * 2 * MathF.PI / numSegments;
                float y = MathF.Sin(angle) * radius;
                float x = MathF.Cos(angle) * radius;

                vertices.Add(from + tan * x + binorm * y);
                vertices.Add(to + tan * x + binorm * y);

                // Indices of the last edge are the first one (close the circle)
                int nextA = 2 * i + 2;
                int nextB = 2 * i + 3;
                if (i == numSegments - 1) {
                    nextA = 0;
                    nextB = 1;
                }

                indices.AddRange(new List<int>() {
                    2 * i,
                    2 * i + 1,
                    nextA,
                    nextA,
                    2 * i + 1,
                    nextB
                });
            }

            Mesh result = new(vertices.ToArray(), indices.ToArray());
            return result;
        }

        /// <summary>
        /// Creates a cone oriented such that it connects two points like an arrow tip
        /// </summary>
        /// <param name="baseCenter">The point in the center of the cone's base</param>
        /// <param name="tip">The position of the tip in world space</param>
        /// <param name="radius">Radius at the base of the cone</param>
        /// <param name="numSegments">Number of triangles used to build the side surface</param>
        public static Mesh MakeCone(Vector3 baseCenter, Vector3 tip, float radius, int numSegments) {
            SampleWarp.ComputeBasisVectors(Vector3.Normalize(tip - baseCenter), out var tan, out var binorm);

            List<Vector3> vertices = new();
            List<int> indices = new();
            vertices.Add(tip);
            for (int i = 0; i < numSegments; ++i) {
                float angle = i * 2 * MathF.PI / numSegments;
                float y = MathF.Sin(angle) * radius;
                float x = MathF.Cos(angle) * radius;

                vertices.Add(baseCenter + tan * x + binorm * y);

                indices.AddRange(new List<int>(){
                    0, i, (i == numSegments - 1) ? 1 : (i + 1)
                });
            }

            Mesh result = new(vertices.ToArray(), indices.ToArray());
            return result;
        }
    }
}