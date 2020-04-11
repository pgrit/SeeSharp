using Xunit;
using Integrators;
using System;
using GroundWrapper;

namespace Integrators.Tests {
    public class MisDirectIllumFixture : IDisposable {
        public float lightArea = 2.0f;

        public Vector3[] positions = new Vector3[] {
            new Vector3 { x = 0, y = 2, z = 0 }, // light
            new Vector3 { x = 0, y = 0, z = 0 }, // surface
            new Vector3 { x = 0, y = 1, z = 0 }  // camera
        };

        public Vector3[] normals = new Vector3[] {
            new Vector3 { x = 0, y = -1, z = 0 }.Normalized(), // light
            new Vector3 { x = 0, y = 1, z = 0 }.Normalized(), // surface
        };

        public PathCache pathCache;
        public int lightVertexIndex;

        public PathVertex[] cameraVertices;

        public MisDirectIllumFixture() {
            // Create a sufficiently large path cache
            pathCache = new PathCache(10);

            // Add the vertex on the light source
            var emitterVertex = new PathVertex {
                point = new SurfacePoint {
                    position = positions[0],
                    normal = normals[0],
                },
                pdfFromAncestor = 1.0f / lightArea,
                pdfToAncestor = 1.0f / lightArea, // pdf of next event is stored here!
                ancestorId = -1
            };
            int emVertexId = pathCache.AddVertex(emitterVertex);

            // Add the vertex on the surface
            var surfaceVertex = new PathVertex {
                point = new SurfacePoint {
                    position = positions[1],
                    normal = normals[1],
                },
                ancestorId = emVertexId
            };

            // Compute the geometry terms
            Vector3 dirToLight = emitterVertex.point.position - surfaceVertex.point.position;
            float distSqr = dirToLight.LengthSquared();
            dirToLight = dirToLight.Normalized();
            float cosSurfToLight = Vector3.Dot(dirToLight, surfaceVertex.point.normal);
            float cosLightToSurf = Vector3.Dot(-dirToLight, emitterVertex.point.normal);

            // pdf for diffuse sampling of the emission direction
            surfaceVertex.pdfFromAncestor = (cosLightToSurf / MathF.PI) * (cosSurfToLight / distSqr);

            // pdf for a diffuse BSDF would be:
            //surfaceVertex.pdfToAncestor = (cosSurfToLight / MathF.PI) * (cosLightToSurf / distSqr);
            // however, this is not known to the integrator at this point, as the outgoing direction
            // is missing and the vertex might not be diffuse.
            surfaceVertex.pdfToAncestor = 0.0f;

            lightVertexIndex = pathCache.AddVertex(surfaceVertex);

            // Initialize the camera path 
            cameraVertices = new PathVertex[3];

            // sampling the camera itself / a point on the lens 
            cameraVertices[0] = new PathVertex {
                pdfFromAncestor = 1.0f, // lens / sensor sampling is deterministic
                pdfToAncestor = 0.0f, 
                point = new SurfacePoint {
                    position = positions[2]
                }
            };

            // surface vertex
            cameraVertices[1] = new PathVertex {
                pdfFromAncestor = 1.0f, // surface area pdf of sampling this vertex from the camera
                pdfToAncestor = 1.0f, // lens / sensor sampling is deterministic
                point = new SurfacePoint {
                    position = positions[1],
                    normal = normals[1]
                }
            };

            // on the light source
            cameraVertices[2] = new PathVertex {
                pdfFromAncestor = (cosSurfToLight / MathF.PI) * (cosLightToSurf / distSqr),
                pdfToAncestor = surfaceVertex.pdfFromAncestor * emitterVertex.pdfFromAncestor,
                point = new SurfacePoint {
                    position = positions[0],
                    normal = normals[0]
                }
            };
        }

        void IDisposable.Dispose() {

        }
    }

    public class ClassicBidir_Mis_DirectIllum : IClassFixture<MisDirectIllumFixture> {

        MisDirectIllumFixture fixture;
        public ClassicBidir_Mis_DirectIllum(MisDirectIllumFixture fixture) {
            this.fixture = fixture;
        }

        float NextEventWeight() {
            var computer = new ClassicBidir.MisWeightComputer {
                lightPathCache = fixture.pathCache,
                numLightPaths = 500
            };

            var cameraPath = new ClassicBidir.CameraPath {
                vertices = fixture.cameraVertices.AsSpan(0, 2)
            };

            return computer.NextEvent(cameraPath,
                pdfEmit: fixture.pathCache[0].pdfFromAncestor * fixture.pathCache[1].pdfFromAncestor,
                pdfNextEvent: 1.0f / fixture.lightArea,
                pdfHit: fixture.cameraVertices[2].pdfFromAncestor);
        }

        float LightTracerWeight() {
            var computer = new ClassicBidir.MisWeightComputer {
                lightPathCache = fixture.pathCache,
                numLightPaths = 500
            };

            return computer.LightTracer(fixture.pathCache[fixture.lightVertexIndex],
                pdfCamToPrimary: fixture.cameraVertices[1].pdfFromAncestor,
                pdfReverse: fixture.cameraVertices[2].pdfFromAncestor);
        }

        float HitWeight() {
            var computer = new ClassicBidir.MisWeightComputer {
                lightPathCache = fixture.pathCache,
                numLightPaths = 500
            };

            var cameraPath = new ClassicBidir.CameraPath {
                vertices = fixture.cameraVertices.AsSpan(0, 3)
            };

            return computer.Hit(cameraPath,
                pdfEmit: fixture.pathCache[0].pdfFromAncestor * fixture.pathCache[1].pdfFromAncestor,
                pdfNextEvent: 1.0f / fixture.lightArea);
        }

        [Fact]
        public void NextEvent_ShouldBeValid() {
            float weightNextEvt = NextEventWeight();
            Assert.True(weightNextEvt <= 1.0f);
            Assert.True(weightNextEvt >= 0.0f);
        }

        [Fact]
        public void LightTracer_ShouldBeValid() {
            float weightLightTracer = LightTracerWeight();
            Assert.True(weightLightTracer <= 1.0f);
            Assert.True(weightLightTracer >= 0.0f);
        }

        [Fact]
        public void Bsdf_ShouldBeValid() {
            float weightBsdf = HitWeight();
            Assert.True(weightBsdf <= 1.0f);
            Assert.True(weightBsdf >= 0.0f);
        }

        [Fact]
        public void ShouldSumToOne() {
            float weightNextEvt = NextEventWeight();
            float weightLightTracer = LightTracerWeight();
            float weightBsdf = HitWeight();

            float weightSum = weightNextEvt + weightBsdf + weightLightTracer;

            Assert.Equal(1.0f, weightSum, 2);
        }
    }
}
