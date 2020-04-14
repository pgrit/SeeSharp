using GroundWrapper.GroundMath;
using System.Diagnostics;

namespace GroundWrapper {
    public class Mesh {
        public Mesh(Vector3[] vertices, int[] indices, Vector3[] shadingNormals = null,
                    Vector2[] textureCoordinates = null) {
            Vertices = vertices;
            Indices = indices;

            Debug.Assert(indices.Length % 3 == 0, "Triangle mesh indices must be a multiple of three.");
            int numFaces = indices.Length / 3;

            // Compute face normals and triangle areas
            FaceNormals = new Vector3[numFaces];
            var surfaceAreas = new float[numFaces];
            SurfaceArea = 0;
            for (int face = 0; face < numFaces; ++face) {
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
                for (int face = 0; face < numFaces; ++face) {
                    this.shadingNormals[indices[face * 3 + 0]] = FaceNormals[face];
                    this.shadingNormals[indices[face * 3 + 1]] = FaceNormals[face];
                    this.shadingNormals[indices[face * 3 + 2]] = FaceNormals[face];
                }
            } else {
                // Ensure normalization
                for (int i = 0; i < this.shadingNormals.Length; ++i)
                    this.shadingNormals[i] = this.shadingNormals[i].Normalized();
            }
        }

        float ComputeErrorOffset(int faceIdx, Vector2 barycentricCoords) {
            // Compute the error offset: approximated radius of the sphere within which the actual intersection lies
            // This is used to avoid self-intersections throughout the renderer.
            // The computations here are based on PBRTv3.
            const float epsilon = float.Epsilon * 0.5f;
            const float gamma6 = (6 * epsilon) / (1 - 6 * epsilon);

            var v1 = Vertices[Indices[faceIdx * 3 + 0]];
            var v2 = Vertices[Indices[faceIdx * 3 + 1]];
            var v3 = Vertices[Indices[faceIdx * 3 + 2]];

            Vector3 errorDiagonal = Vector3.Abs(barycentricCoords.x * v2)
                + Vector3.Abs(barycentricCoords.y * v3)
                + Vector3.Abs((1 - barycentricCoords.x - barycentricCoords.y) * v1);

            return errorDiagonal.Length() * gamma6;
        }

        public SurfaceSample Sample(Vector2 primarySample) {
            var (faceIdx, newX) = triangleDistribution.Sample(primarySample.x);
            var barycentric = SampleWrap.ToUniformTriangle(new Vector2(newX, primarySample.y));

            return new SurfaceSample {
                point = new SurfacePoint {
                    barycentricCoords = barycentric,
                    primId = (uint)faceIdx,
                    normal = FaceNormals[faceIdx],
                    position = ComputePosition(faceIdx, barycentric),
                    errorOffset = ComputeErrorOffset(faceIdx, barycentric)
                },
                jacobian = 1.0f / SurfaceArea
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

            return barycentric.x * v2
                +  barycentric.y * v3
                + (1 - barycentric.x - barycentric.y) * v1;
        }

        public Vector3 ComputeShadingNormal(int faceIdx, Vector2 barycentric) {
            var v1 = shadingNormals[Indices[faceIdx * 3 + 0]];
            var v2 = shadingNormals[Indices[faceIdx * 3 + 1]];
            var v3 = shadingNormals[Indices[faceIdx * 3 + 2]];

            return barycentric.x * v2
                +  barycentric.y * v3
                + (1 - barycentric.x - barycentric.y) * v1;
        }

        public Vector3 ComputePosition(int faceIdx, Vector2 barycentric) {
            var v1 = Vertices[Indices[faceIdx * 3 + 0]];
            var v2 = Vertices[Indices[faceIdx * 3 + 1]];
            var v3 = Vertices[Indices[faceIdx * 3 + 2]];

            return barycentric.x * v2
                +  barycentric.y * v3 
                + (1 - barycentric.x - barycentric.y) * v1;
        }

        public Vector3[] Vertices;
        public int[] Indices;
        public Vector3[] FaceNormals;
        public float SurfaceArea;

        Sampling.PiecewiseConstant triangleDistribution;

        // per-vertex attributes
        Vector3[] shadingNormals;
        Vector2[] textureCoordinates;
    }
}
