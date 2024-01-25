using SeeSharp.Geometry;
using SeeSharp.Integrators.Common;
using System;
using System.Collections.Generic;
using System.Numerics;
using static SeeSharp.Integrators.Bidir.BidirBase;

namespace SeeSharp.Tests.Integrators.Helpers {
    public struct MisDummyPath {
        public float lightArea;
        public int numLightPaths;
        public Vector3[] positions;
        public Vector3[] normals;
        public PathCache pathCache;
        public int lightEndpointIdx;
        public PathPdfPair[] cameraVertices;
        public List<float> Distances {
            get {
                List<float> result = new();
                for (int i = 1; i < positions.Length; ++i) {
                    result.Add((positions[i] - positions[i-1]).Length());
                }
                return result;
            }
        }

        public MisDummyPath(float lightArea, int numLightPaths, Vector3[] positions, Vector3[] normals) {
            this.lightArea = lightArea;
            this.numLightPaths = numLightPaths;
            this.positions = positions;
            this.normals = normals;

            // Create the light path
            pathCache = new PathCache(1, 10);

            var emitterVertex = new PathVertex {
                Point = new SurfacePoint {
                    Position = positions[0],
                    Normal = normals[0],
                },
                PdfFromAncestor = -10000.0f,
                PdfReverseAncestor = -10000.0f, // Guard value: this is unused!
                AncestorId = -1,
                Depth = 0
            };
            int emVertexId = pathCache.AddVertex(emitterVertex);

            // Add all intermediate surface vertices
            var prevLightVertex = emitterVertex;
            int prevLightVertexIdx = emVertexId;
            float lastReverse = 0.0f;
            float lastNee = 0.0f;
            for (int idx = 1; idx < normals.Length; ++idx) {
                // Add the vertex on the first surface
                var surfaceVertex = new PathVertex {
                    Point = new SurfacePoint {
                        Position = positions[idx],
                        Normal = normals[idx],
                    },
                    AncestorId = prevLightVertexIdx,
                    Depth = (byte)idx
                };

                // Compute the geometry terms
                Vector3 dirToLight = prevLightVertex.Point.Position - surfaceVertex.Point.Position;
                float distSqr = dirToLight.LengthSquared();
                dirToLight = Vector3.Normalize(dirToLight);
                float cosSurfToLight = Vector3.Dot(dirToLight, surfaceVertex.Point.Normal);
                float cosLightToSurf = Vector3.Dot(-dirToLight, prevLightVertex.Point.Normal);

                // pdf for diffuse sampling of the emission direction
                surfaceVertex.PdfFromAncestor = (cosLightToSurf / MathF.PI) * (cosSurfToLight / distSqr);
                surfaceVertex.PdfReverseAncestor = lastReverse;
                surfaceVertex.PdfNextEventAncestor = lastNee;

                lastReverse = (cosSurfToLight / MathF.PI) * (cosLightToSurf / distSqr);
                if (idx == 1) {
                    surfaceVertex.PdfFromAncestor *= 1.0f / lightArea; // emission surface sampling pdf
                    lastNee = 1.0f / lightArea; // Next event
                } else {
                    lastNee = 0.0f;
                }

                int lightVertexIndex = pathCache.AddVertex(surfaceVertex);

                prevLightVertex = surfaceVertex;
                prevLightVertexIdx = lightVertexIndex;
            }

            lightEndpointIdx = prevLightVertexIdx;

            pathCache.Prepare(false);

            // Create the camera path
            cameraVertices = new PathPdfPair[positions.Length];

            // sampling the camera itself / a point on the lens
            cameraVertices[0] = new PathPdfPair {
                PdfFromAncestor = 1.0f, // lens / sensor sampling is deterministic
                PdfToAncestor = 0.0f
            };

            // primary surface vertex
            cameraVertices[1] = new PathPdfPair {
                PdfFromAncestor = numLightPaths * 0.8f, // surface area pdf of sampling this vertex from the camera
                PdfToAncestor = 1.0f
            };

            // All other surface vertices
            float nextReverse = lastReverse;
            int lightVertIdx = lightEndpointIdx;
            for (int idx = 2; idx < positions.Length; ++idx) {
                var lightVert = pathCache[0, lightVertIdx];

                cameraVertices[idx] = new PathPdfPair {
                    PdfFromAncestor = nextReverse,
                    PdfToAncestor = lightVert.PdfFromAncestor
                };

                nextReverse = lightVert.PdfReverseAncestor;
                lightVertIdx = lightVert.AncestorId;
            }

            // The last camera path vertex is special: it should not already contain the NEE pdf
            var dir = pathCache[0, 0].Point.Position - pathCache[0, 1].Point.Position;
            var cossurf = Vector3.Dot(Vector3.Normalize(dir), pathCache[0, 1].Point.Normal);
            var coslight = Vector3.Dot(Vector3.Normalize(-dir), pathCache[0, 0].Point.Normal);
            var distsqr = dir.LengthSquared();
            cameraVertices[^1].PdfFromAncestor = cossurf * coslight / distsqr / MathF.PI;
        }
    }
}
