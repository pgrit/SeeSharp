using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SeeSharp.Core.Geometry {
    public class Raytracer {
        int sceneId;

        public Raytracer() {
            sceneId = SeeCoreApi.InitScene();
        }

        public void AddMesh(Mesh mesh) {
            uint meshId = (uint)SeeCoreApi.AddTriangleMesh(sceneId, mesh.Vertices, mesh.NumVertices, mesh.Indices, mesh.NumFaces * 3);
            meshMap[meshId] = mesh;
        }

        public void CommitScene() {
            SeeCoreApi.FinalizeScene(sceneId);
        }

        public SurfacePoint Trace(Ray ray) {
            var minHit = SeeCoreApi.TraceSingle(sceneId, ray);

            if (minHit.meshId == uint.MaxValue)
                return new SurfacePoint();

            SurfacePoint hit = new SurfacePoint {
                BarycentricCoords = new Vector2(minHit.u, minHit.v),
                Distance = minHit.distance,
                Mesh = meshMap[minHit.meshId],
                PrimId = minHit.primId
            };

            // Compute the position and face normal from the barycentric coordinates
            hit.Position = hit.Mesh.ComputePosition((int)hit.PrimId, hit.BarycentricCoords);
            hit.Normal = hit.Mesh.FaceNormals[hit.PrimId];

            // Compute the error offset (formula taken from Embree example renderer)
            hit.ErrorOffset = Math.Max(
                Math.Max(Math.Abs(hit.Position.X), Math.Abs(hit.Position.Y)),
                Math.Max(Math.Abs(hit.Position.Z), hit.Distance)
            ) * 32.0f * 1.19209e-07f;

            return hit;
        }

        Vector3 OffsetPoint(SurfacePoint from, Vector3 dir) {
            float sign = Vector3.Dot(dir, from.Normal) < 0.0f ? -1.0f : 1.0f;
            return from.Position + sign * from.ErrorOffset * from.Normal;
        }

        public bool IsOccluded(SurfacePoint from, SurfacePoint to) {
            const float shadowEpsilon = 1e-5f;

            // Compute the ray with proper offsets
            var dir = to.Position - from.Position;
            var p0 = OffsetPoint(from, dir);
            var p1 = OffsetPoint(to, -dir);
            dir = p1 - p0;

            var ray = new Ray {
                Origin = p0,
                Direction = dir,
                MinDistance = shadowEpsilon,
            };

            // TODO use a proper optimized method here that does not compute the actual closest hit.
            var p = Trace(ray);

            bool occluded = p.Mesh != null && p.Distance < 1 - shadowEpsilon;

            return occluded;
        }

        public bool IsOccluded(SurfacePoint from, Vector3 target) {
            // Compute the ray with proper offsets
            var dir = target - from.Position;
            var dist = dir.Length();
            dir /= dist;

            var ray = SpawnRay(from, dir);

            // TODO use a proper optimized method here that does not compute the actual closest hit.
            var p = Trace(ray);

            bool occluded = p.Mesh != null
                && p.Distance < dist - from.ErrorOffset;

            return occluded;
        }

        public bool LeavesScene(SurfacePoint from, Vector3 direction) {
            var ray = SpawnRay(from, direction);

            // TODO use a proper optimized method here that does not compute the actual closest hit.
            var p = Trace(ray);

            bool occluded = p.Mesh != null;

            return !occluded;
        }

        public Ray SpawnRay(SurfacePoint from, Vector3 dir) {
            float sign = Vector3.Dot(dir, from.Normal) < 0.0f ? -1.0f : 1.0f;
            return new Ray {
                Origin = from.Position + sign * from.ErrorOffset * from.Normal,
                Direction = dir,
                MinDistance = from.ErrorOffset,
            };
        }

        Dictionary<uint, Mesh> meshMap = new Dictionary<uint, Mesh>();

        private static class SeeCoreApi {
            [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
            public static extern int InitScene();

            [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
            public static extern void FinalizeScene(int scene);

            [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
            public static extern int AddTriangleMesh(int scene, Vector3[] vertices, int numVerts, int[] indices,
                                                     int numIdx, float[] texCoords = null, float[] shadingNormals = null);

#pragma warning disable CS0649 // The field is never assigned to (only returned from native function call)
            public readonly struct MinimalHitInfo {
                public readonly uint meshId;
                public readonly uint primId;
                public readonly float u;
                public readonly float v;
                public readonly float distance;
            }
#pragma warning restore CS0649 // The field is never assigned to

            [DllImport("SeeCore", CallingConvention = CallingConvention.Cdecl)]
            public static extern MinimalHitInfo TraceSingle(int scene, Ray ray);
        }
    }
}
