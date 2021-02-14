using System;
using System.Collections.Generic;
using System.Numerics;

namespace SeeSharp.Geometry {
    public static class MeshFactory {
        public static Mesh MakeCylinder(Vector3 from, Vector3 to, float radius, int numSegments) {
            var (tan, binorm) = Sampling.SampleWarp.ComputeBasisVectors(Vector3.Normalize(to - from));

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

        public static Mesh MakeCone(Vector3 baseCenter, Vector3 tip, float radius, int numSegments) {
            var (tan, binorm) = Sampling.SampleWarp.ComputeBasisVectors(Vector3.Normalize(tip - baseCenter));

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