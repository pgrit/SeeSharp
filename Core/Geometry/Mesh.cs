using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading.Materials;
using System.Numerics;

namespace SeeSharp.Core.Geometry {
    /// <summary>
    /// A simple triangle mesh with methods to uniformly sample its area.
    /// </summary>
    public class Mesh : TinyEmbree.TriangleMesh {
        public Material Material;

        public Mesh(Vector3[] vertices, int[] indices, Vector3[] shadingNormals = null,
                    Vector2[] textureCoordinates = null) 
            : base(vertices, indices, shadingNormals, textureCoordinates) {
            // Compute the uniform area sampling distribution
            var surfaceAreas = new float[NumFaces];
            for (int face = 0; face < NumFaces; ++face) {
                var v1 = vertices[indices[face * 3 + 0]];
                var v2 = vertices[indices[face * 3 + 1]];
                var v3 = vertices[indices[face * 3 + 2]];
                Vector3 n = Vector3.Cross(v2 - v1, v3 - v1);
                surfaceAreas[face] = n.Length() * 0.5f;
            }
            triangleDistribution = new PiecewiseConstant(surfaceAreas);
        }

        float ComputeErrorOffset(int faceIdx, Vector2 barycentricCoords) {
            var v1 = Vertices[Indices[faceIdx * 3 + 0]];
            var v2 = Vertices[Indices[faceIdx * 3 + 1]];
            var v3 = Vertices[Indices[faceIdx * 3 + 2]];

            Vector3 errorDiagonal = Vector3.Abs(barycentricCoords.X * v2)
                + Vector3.Abs(barycentricCoords.Y * v3)
                + Vector3.Abs((1 - barycentricCoords.X - barycentricCoords.Y) * v1);

            return errorDiagonal.Length() * 32.0f * 1.19209e-07f;
        }

        public SurfaceSample Sample(Vector2 primarySample) {
            var (faceIdx, newX) = triangleDistribution.Sample(primarySample.X);
            var barycentric = SampleWarp.ToUniformTriangle(new Vector2(newX, primarySample.Y));

            return new SurfaceSample {
                Point = new SurfacePoint {
                    BarycentricCoords = barycentric,
                    PrimId = (uint)faceIdx,
                    Normal = FaceNormals[faceIdx],
                    Position = ComputePosition(faceIdx, barycentric),
                    ErrorOffset = ComputeErrorOffset(faceIdx, barycentric),
                    Mesh = this
                },
                Pdf = 1.0f / SurfaceArea
            };
        }

        public Vector2 SampleInverse(SurfacePoint point) {
            var local = SampleWarp.FromUniformTriangle(point.BarycentricCoords);
            float x = triangleDistribution.SampleInverse((int)point.PrimId, local.X);
            return new(x, local.Y);
        }

        public float Pdf(SurfacePoint point) {
            return 1.0f / SurfaceArea;
        }

        PiecewiseConstant triangleDistribution;
    }
}
