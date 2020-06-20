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

        Vector3 OffsetPoint(SurfacePoint from, Vector3 dir) {
            float sign = Vector3.Dot(dir, from.normal) < 0.0f ? -1.0f : 1.0f;
            return from.position + sign * from.errorOffset * from.normal;
        }

        public bool IsOccluded(SurfacePoint from, SurfacePoint to) {
            const float shadowEpsilon = 1e-5f;

            // Compute the ray with proper offsets
            var dir = to.position - from.position;
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

            bool occluded = p.mesh != null && p.distance < 1 - shadowEpsilon;

            return occluded;
        }

        public bool IsOccluded(SurfacePoint from, Vector3 target) {
            // Compute the ray with proper offsets
            var dir = target - from.position;
            var dist = dir.Length();
            dir /= dist;

            var ray = SpawnRay(from, dir);

            // TODO use a proper optimized method here that does not compute the actual closest hit.
            var p = Trace(ray);

            bool occluded = p.mesh != null
                && p.distance < dist - from.errorOffset;

            return occluded;
        }

        public bool LeavesScene(SurfacePoint from, Vector3 direction) {
            var ray = SpawnRay(from, direction);

            // TODO use a proper optimized method here that does not compute the actual closest hit.
            var p = Trace(ray);

            bool occluded = p.mesh != null;

            return !occluded;
        }

        public Ray SpawnRay(SurfacePoint from, Vector3 dir) {
            float sign = Vector3.Dot(dir, from.normal) < 0.0f ? -1.0f : 1.0f;
            return new Ray {
                Origin = from.position + sign * from.errorOffset * from.normal,
                Direction = dir,
                MinDistance = from.errorOffset,
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
