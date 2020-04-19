using System;
using GroundWrapper;
using GroundWrapper.GroundMath;
using Integrators.Common;
using Integrators;
using System.Numerics;
using GroundWrapper.Geometry;

namespace Integrators.Tests.Helpers {
    public struct MisDummyPath {
        public float lightArea;
        public int numLightPaths;
        public Vector3[] positions;
        public Vector3[] normals;
        public PathCache pathCache;
        public int lightEndpointIdx;
        public PathPdfPair[] cameraVertices;

        public MisDummyPath(float lightArea, int numLightPaths, Vector3[] positions, Vector3[] normals) {
            this.lightArea = lightArea;
            this.numLightPaths = numLightPaths;
            this.positions = positions;
            this.normals = normals;

            { // Create the light path
                pathCache = new PathCache(10);

                var emitterVertex = new PathVertex {
                    point = new SurfacePoint {
                        position = positions[0],
                        normal = normals[0],
                    },
                    pdfFromAncestor = -10000.0f,
                    pdfToAncestor = -10000.0f, // Guard value: this is unused!
                    ancestorId = -1,
                    depth = 0
                };
                int emVertexId = pathCache.AddVertex(emitterVertex);

                // Add all intermediate surface vertices
                var prevLightVertex = emitterVertex;
                int prevLightVertexIdx = emVertexId;
                for (int idx = 1; idx < normals.Length; ++idx) {
                    // Add the vertex on the first surface
                    var surfaceVertex = new PathVertex {
                        point = new SurfacePoint {
                            position = positions[idx],
                            normal = normals[idx],
                        },
                        ancestorId = prevLightVertexIdx,
                        depth = (byte)idx
                    };

                    // Compute the geometry terms
                    Vector3 dirToLight = prevLightVertex.point.position - surfaceVertex.point.position;
                    float distSqr = dirToLight.LengthSquared();
                    dirToLight = Vector3.Normalize(dirToLight);
                    float cosSurfToLight = Vector3.Dot(dirToLight, surfaceVertex.point.normal);
                    float cosLightToSurf = Vector3.Dot(-dirToLight, prevLightVertex.point.normal);

                    // pdf for diffuse sampling of the emission direction
                    surfaceVertex.pdfFromAncestor = (cosLightToSurf / MathF.PI) * (cosSurfToLight / distSqr);
                    surfaceVertex.pdfToAncestor = (cosSurfToLight / MathF.PI) * (cosLightToSurf / distSqr);
                    if (idx == 1) {
                        surfaceVertex.pdfFromAncestor *= 1.0f / lightArea;
                        surfaceVertex.pdfToAncestor += 1.0f / lightArea; // Next event
                    }

                    int lightVertexIndex = pathCache.AddVertex(surfaceVertex);

                    prevLightVertex = surfaceVertex;
                    prevLightVertexIdx = lightVertexIndex;
                }

                lightEndpointIdx = prevLightVertexIdx;
            }

            { // Create the camera path
                cameraVertices = new PathPdfPair[positions.Length];

                // sampling the camera itself / a point on the lens 
                cameraVertices[0] = new PathPdfPair {
                    pdfFromAncestor = 1.0f, // lens / sensor sampling is deterministic
                    pdfToAncestor = 0.0f
                };

                // primary surface vertex
                cameraVertices[1] = new PathPdfPair {
                    pdfFromAncestor = numLightPaths * 0.8f, // surface area pdf of sampling this vertex from the camera
                    pdfToAncestor = 1.0f
                };

                // All other surface vertices
                int lightVertIdx = lightEndpointIdx;
                for (int idx = 2; idx < positions.Length; ++idx) {
                    var lightVert = pathCache[lightVertIdx];

                    cameraVertices[idx] = new PathPdfPair {
                        pdfFromAncestor = lightVert.pdfToAncestor,
                        pdfToAncestor = lightVert.pdfFromAncestor
                    };

                    lightVertIdx = lightVert.ancestorId;
                }

                // The last camera path vertex is special
                var dir = pathCache[0].point.position - pathCache[1].point.position;
                var cossurf = Vector3.Dot(Vector3.Normalize(dir), pathCache[1].point.normal);
                var coslight = Vector3.Dot(Vector3.Normalize(-dir), pathCache[0].point.normal);
                var distsqr = dir.LengthSquared();
                cameraVertices[^1].pdfFromAncestor = cossurf * coslight / distsqr / MathF.PI;
                cameraVertices[^1].pdfToAncestor = -100000.0f;
            }
        }
    }
}
