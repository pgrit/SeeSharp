using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace GroundWrapper.Geometry {
    public struct Ray {
        public Vector3 origin;
        public Vector3 direction;
        public float minDistance;
    }

    public struct SurfacePoint {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 barycentricCoords;
        public Mesh mesh;
        public uint primId;
        public float errorOffset;
        public float distance;

        public static implicit operator bool(SurfacePoint hit)
            => hit.mesh != null;

        public Vector3 ShadingNormal => mesh.ComputeShadingNormal((int)primId, barycentricCoords);
        public Vector2 TextureCoordinates => mesh.ComputeTextureCoordinates((int)primId, barycentricCoords);
    }

    public class Raytracer {
        public Raytracer() {
            GroundApi.InitScene();
        }

        public void AddMesh(Mesh mesh) {
            uint meshId = (uint)GroundApi.AddTriangleMesh(mesh.Vertices, mesh.NumVertices, mesh.Indices, mesh.NumFaces * 3);
            meshMap[meshId] = mesh;
        }

        public void CommitScene() {
            GroundApi.FinalizeScene();
        }

        public SurfacePoint Intersect(Ray ray) {
            var minHit = GroundApi.TraceSingle(ray);

            if (minHit.meshId == uint.MaxValue)
                return new SurfacePoint();

            SurfacePoint hit = new SurfacePoint {
                barycentricCoords = new Vector2(minHit.u, minHit.v),
                distance = minHit.distance,
                mesh = meshMap[minHit.meshId],
                primId = minHit.primId
            };

            // Compute the position and face normal from the barycentric coordinates
            hit.position = hit.mesh.ComputePosition((int)hit.primId, hit.barycentricCoords);
            hit.normal = hit.mesh.FaceNormals[hit.primId];

            // Compute the error offset (formula taken from Embree example renderer)
            hit.errorOffset = Math.Max(
                Math.Max(Math.Abs(hit.position.X), Math.Abs(hit.position.Y)),
                Math.Max(Math.Abs(hit.position.Z), hit.distance)
            ) * 32.0f * 1.19209e-07f;

            return hit;
        }

        Dictionary<uint, Mesh> meshMap = new Dictionary<uint, Mesh>();

        private static class GroundApi {
            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern void InitScene();

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern void FinalizeScene();

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern int AddTriangleMesh(Vector3[] vertices, int numVerts,
                int[] indices, int numIdx, float[] texCoords = null, float[] shadingNormals = null);

#pragma warning disable CS0649 // The field is never assigned to (only returned from native function call)
            public readonly struct MinimalHitInfo {
                public readonly uint meshId;
                public readonly uint primId;
                public readonly float u;
                public readonly float v;
                public readonly float distance;
            }
#pragma warning restore CS0649 // The field is never assigned to

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern MinimalHitInfo TraceSingle(Ray ray);
        }
    }
}
