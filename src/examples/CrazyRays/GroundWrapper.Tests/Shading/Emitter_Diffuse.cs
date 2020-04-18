using GroundWrapper.Geometry;
using System;
using System.Numerics;
using Xunit;

namespace GroundWrapper.Tests.Shading {
    public class Emitter_Diffuse {
        [Fact]
        public void EmittedRays_ShouldHaveOffset() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                }
            );
            var emitter = new DiffuseEmitter(mesh, ColorRGB.White);

            var (ray, _) = emitter.SampleRay(new Vector2(0.3f, 0.8f), new Vector2(0.56f, 0.03f));

            Assert.True(ray.minDistance > 0);
        }

        [Fact]
        public void EmittedRays_Sidedness_ShouldBePositive() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                }, 
                shadingNormals: new Vector3[] {
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0)
                }
            );
            var emitter = new DiffuseEmitter(mesh, ColorRGB.White);

            var (ray, _) = emitter.SampleRay(new Vector2(0.3f, 0.8f), new Vector2(0.56f, 0.03f));

            Assert.True(ray.direction.Y > 0);
        }

        [Fact]
        public void EmittedRays_Sidedness_ShouldBeNegative() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                },
                shadingNormals: new Vector3[] {
                    new Vector3(0, -1, 0),
                    new Vector3(0, -1, 0),
                    new Vector3(0, -1, 0),
                    new Vector3(0, -1, 0)
                }
            );
            var emitter = new DiffuseEmitter(mesh, ColorRGB.White);

            var (ray, _) = emitter.SampleRay(new Vector2(0.3f, 0.8f), new Vector2(0.56f, 0.03f));

            Assert.True(ray.direction.Y < 0);
        }

        [Fact]
        public void Emission_ShouldBeOneSided() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                },
                shadingNormals: new Vector3[] {
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0)
                }
            );
            var emitter = new DiffuseEmitter(mesh, ColorRGB.White);

            var dummyHit = new SurfacePoint {
                mesh = mesh
            };

            var r1 = emitter.EmittedRadiance(dummyHit, new Vector3(0, 1, 1));
            var r2 = emitter.EmittedRadiance(dummyHit, new Vector3(0, -1, 1));

            Assert.True(r1.r > 0);
            Assert.Equal(0, r2.r);
        }

        [Fact]
        public void EmittedRays_Pdf_ShouldBeOneSided() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                },
                shadingNormals: new Vector3[] {
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0)
                }
            );
            var emitter = new DiffuseEmitter(mesh, ColorRGB.White);

            var dummyHit = new SurfacePoint {
                mesh = mesh
            };

            var p1 = emitter.PdfRay(dummyHit, new Vector3(0, 1, 1));
            var p2 = emitter.PdfRay(dummyHit, new Vector3(0, -1, 1));

            Assert.True(p1 > 0);
            Assert.Equal(0, p2);
        }

        [Fact]
        public void EmittedRays_Pdf_ShouldBeCosHemisphere() {
            var mesh = new Mesh(
                new Vector3[] {
                    new Vector3(-1, 10, -1),
                    new Vector3( 1, 10, -1),
                    new Vector3( 1, 10,  1),
                    new Vector3(-1, 10,  1)
                }, new int[] {
                    0, 1, 2,
                    0, 2, 3
                },
                shadingNormals: new Vector3[] {
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 1, 0)
                }
            );
            var emitter = new DiffuseEmitter(mesh, ColorRGB.White);

            var (ray, pdf) = emitter.SampleRay(new Vector2(0.3f, 0.8f), new Vector2(0.56f, 0.03f));

            float c = Vector3.Dot(ray.direction, new Vector3(0, 1, 0));
            Assert.Equal(0.25f * c / MathF.PI, pdf);
        }
    }
}
