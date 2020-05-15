using SeeSharp.Core.Sampling;
using SeeSharp.Core.Shading.Materials;
using System.Diagnostics;
using System.Numerics;

namespace SeeSharp.Core.Geometry {

    public class Mesh {
        public Material Material;

        public Mesh(Vector3[] vertices, int[] indices, Vector3[] shadingNormals = null,
                    Vector2[] textureCoordinates = null) {
            Vertices = vertices;
            Indices = indices;

            Debug.Assert(indices.Length % 3 == 0, "Triangle mesh indices must be a multiple of three.");
            NumFaces = indices.Length / 3;
            NumVertices = vertices.Length;

            // Compute face normals and triangle areas
            FaceNormals = new Vector3[NumFaces];
            var surfaceAreas = new float[NumFaces];
            SurfaceArea = 0;
            for (int face = 0; face < NumFaces; ++face) {
                var v1 = vertices[indices[face * 3 + 0]];
                var v2 = vertices[indices[face * 3 + 1]];
                var v3 = vertices[indices[face * 3 + 2]];

                // Compute the normal. Winding order is CCW always.
                Vector3 n = Vector3.Cross(v2 - v1, v3 - v1);
                float len = n.Length();
                FaceNormals[face] = n / len;
                surfaceAreas[face] = len * 0.5f;

                SurfaceArea += surfaceAreas[face];
            }

            triangleDistribution = new Sampling.PiecewiseConstant(surfaceAreas);

            this.shadingNormals = shadingNormals;
            this.textureCoordinates = textureCoordinates;

            // Compute shading normals from face normals if not set
            if (this.shadingNormals == null) {
                this.shadingNormals = new Vector3[vertices.Length];
                for (int face = 0; face < NumFaces; ++face) {
                    this.shadingNormals[indices[face * 3 + 0]] = FaceNormals[face];
                    this.shadingNormals[indices[face * 3 + 1]] = FaceNormals[face];
                    this.shadingNormals[indices[face * 3 + 2]] = FaceNormals[face];
                }
            } else {
                // Ensure normalization
                for (int i = 0; i < this.shadingNormals.Length; ++i)
                    this.shadingNormals[i] = Vector3.Normalize(this.shadingNormals[i]);
            }
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
            var barycentric = SampleWrap.ToUniformTriangle(new Vector2(newX, primarySample.Y));

            return new SurfaceSample {
                point = new SurfacePoint {
                    barycentricCoords = barycentric,
                    primId = (uint)faceIdx,
                    normal = FaceNormals[faceIdx],
                    position = ComputePosition(faceIdx, barycentric),
                    errorOffset = ComputeErrorOffset(faceIdx, barycentric),
                    mesh = this
                },
                pdf = 1.0f / SurfaceArea
            };
        }

        public float Pdf(SurfacePoint point) {
            return 1.0f / SurfaceArea;
        }

        public Vector2 ComputeTextureCoordinates(int faceIdx, Vector2 barycentric) {
            if (textureCoordinates == null)
                return new Vector2(0, 0);

            var v1 = textureCoordinates[Indices[faceIdx * 3 + 0]];
            var v2 = textureCoordinates[Indices[faceIdx * 3 + 1]];
            var v3 = textureCoordinates[Indices[faceIdx * 3 + 2]];

            return barycentric.X * v2
                +  barycentric.Y * v3
                + (1 - barycentric.X - barycentric.Y) * v1;
        }

        public Vector3 ComputeShadingNormal(int faceIdx, Vector2 barycentric) {
            var v1 = shadingNormals[Indices[faceIdx * 3 + 0]];
            var v2 = shadingNormals[Indices[faceIdx * 3 + 1]];
            var v3 = shadingNormals[Indices[faceIdx * 3 + 2]];

            return barycentric.X * v2
                +  barycentric.Y * v3
                + (1 - barycentric.X - barycentric.Y) * v1;
        }

        public Vector3 ComputePosition(int faceIdx, Vector2 barycentric) {
            var v1 = Vertices[Indices[faceIdx * 3 + 0]];
            var v2 = Vertices[Indices[faceIdx * 3 + 1]];
            var v3 = Vertices[Indices[faceIdx * 3 + 2]];

            return barycentric.X * v2
                +  barycentric.Y * v3
                + (1 - barycentric.X - barycentric.Y) * v1;
        }

        public Vector3[] Vertices;
        public int[] Indices;
        public Vector3[] FaceNormals;
        public float SurfaceArea;

        public int NumVertices { get; private set; }
        public int NumFaces { get; private set; }

        Sampling.PiecewiseConstant triangleDistribution;

        // per-vertex attributes
        Vector3[] shadingNormals;
        Vector2[] textureCoordinates;
    }
}
