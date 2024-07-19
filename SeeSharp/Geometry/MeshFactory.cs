namespace SeeSharp.Geometry;

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
        ComputeBasisVectors(Vector3.Normalize(to - from), out var tan, out var binorm);

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

    public static Mesh MakeAABB(BoundingBox boundingBox) {
        Vector3 x = new(boundingBox.Diagonal.X, 0, 0);
        Vector3 y = new(0, boundingBox.Diagonal.Y, 0);
        Vector3 z = new(0, 0, boundingBox.Diagonal.Z);
        Vector3[] vertices = [
            boundingBox.Min,
            boundingBox.Min + x,
            boundingBox.Min + x + y,
            boundingBox.Min + y,
            boundingBox.Min + z,
            boundingBox.Min + z + x,
            boundingBox.Min + z + x + y,
            boundingBox.Min + z + y,
        ];
        int[] indices = [
            0, 1, 2, 0, 2, 3, // front
            4, 6, 5, 4, 7, 6, // back
            4, 0, 3, 4, 3, 7, // left
            1, 5, 2, 5, 6, 2, // right
            3, 2, 6, 3, 6, 7, // top
            0, 5, 1, 0, 4, 5, // bottom
        ];
        return new(vertices, indices);
    }

    /// <summary>
    /// Creates a triangulated sphere around a point.
    /// </summary>
    /// <param name="center">Center of the sphere</param>
    /// <param name="radius">Radius of the sphere</param>
    /// <param name="numRings">Number of rings that form the sphere</param>
    public static Mesh MakeSphere(Vector3 center, float radius, int numRings) {
        Vector3 tan = Vector3.UnitX;
        Vector3 binorm = Vector3.UnitZ;
        Vector3 normal = Vector3.UnitY;

        List<Vector3> vertices = new();
        List<int> indices = new();

        // Create the vertices in a ring-by-ring ordering
        int numQuads = numRings * 2;
        vertices.Add(center + normal * radius);
        for (int j = 1; j < numRings; ++j) {
            float angleV = j * MathF.PI / numRings;
            float r = MathF.Sin(angleV) * radius;
            float h = MathF.Cos(angleV) * radius;

            for (int i = 0; i < numQuads; ++i) {
                float angleH = i * 2 * MathF.PI / numQuads;

                float y = MathF.Sin(angleH) * r;
                float x = MathF.Cos(angleH) * r;

                vertices.Add(center + normal * h + tan * x + binorm * y);
            }
        }
        vertices.Add(center - normal * radius);

        // First ring is special (degenerate)
        for (int i = 0; i < numQuads-1; ++i)
            indices.AddRange(new[] { 0, i+2, i+1 });
        indices.AddRange(new[] { 0, 1, numQuads });

        // Center rings
        for (int j = 1; j < numRings-1; ++j) {
            // Offsets to the first vertex in each ring that we connect here
            int o1 = 1 + numQuads * (j - 1);
            int o2 = 1 + numQuads * j;
            for (int i = 0; i < numQuads-1; ++i) {
                indices.AddRange(new[] {
                    o1 + i, o2 + i + 1, o2 + i,
                    o1 + i, o1 + i + 1, o2 + i + 1
                });
            }
            indices.AddRange(new[] {
                o1 + numQuads - 1, o2, o2 + numQuads - 1,
                o1 + numQuads - 1, o1, o2
            });
        }

        // Last ring is just like the first
        for (int i = 0; i < numQuads-1; ++i)
            indices.AddRange(new[] { vertices.Count - 1, vertices.Count - i - 3, vertices.Count - i - 2 });
        indices.AddRange(new[] { vertices.Count - 1, vertices.Count - 2, vertices.Count - numQuads - 1 });

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
        ComputeBasisVectors(Vector3.Normalize(tip - baseCenter), out var tan, out var binorm);

        List<Vector3> vertices = new();
        List<int> indices = new();
        vertices.Add(tip);
        for (int i = 0; i < numSegments; ++i) {
            float angle = i * 2 * MathF.PI / numSegments;
            float y = MathF.Sin(angle) * radius;
            float x = MathF.Cos(angle) * radius;

            vertices.Add(baseCenter + tan * x + binorm * y);

            indices.AddRange(new[] {
                0, i, (i == numSegments - 1) ? 1 : (i + 1)
            });
        }

        // Base disk
        for (int i = 2; i < vertices.Count - 1; ++i) {
            indices.AddRange(new[] { 1, i, i+1 });
        }

        Mesh result = new(vertices.ToArray(), indices.ToArray());
        return result;
    }
}