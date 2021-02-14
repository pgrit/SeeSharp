using SeeSharp.Geometry;
using SeeSharp.Shading;
using SeeSharp.Shading.Materials;
using SeeSharp.Image;
using System;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests {
    public class Material_Diffuse {
        [Fact]
        public void NoLightLeaks() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image<ColorRGB>.Constant(new ColorRGB(1, 1, 1))
            });

            var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0)
            }, new int[] {
                0, 1, 2,
                0, 2, 3
            });

            SurfacePoint hit = new SurfacePoint {
                BarycentricCoords = new Vector2(0.5f, 0.2f),
                Normal = new Vector3(0, 0, 1),
                Mesh = mesh,
                PrimId = 0,
                Position = new Vector3(0, 0, 0),
            };

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, -1);

            var (fwd, rev) = mtl.Pdf(hit, outDir, inDir, false);
            var val = mtl.EvaluateWithCosine(hit, outDir, inDir, false);

            Assert.Equal(0, rev);
            Assert.Equal(0, fwd);
            Assert.Equal(0, val.R);
            Assert.Equal(0, val.G);
            Assert.Equal(0, val.B);
        }

        [Fact]
        public void ForwardAndReverse_ShouldMatch() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image<ColorRGB>.Constant(new ColorRGB(1, 1, 1))
            });

            var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0)
            }, new int[] {
                0, 1, 2,
                0, 2, 3
            });

            SurfacePoint hit = new SurfacePoint {
                BarycentricCoords = new Vector2(0.5f, 0.2f),
                Normal = new Vector3(0, 0, 1),
                Mesh = mesh,
                PrimId = 0,
                Position = new Vector3(0, 0, 0),
            };

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 1, 1);

            var (fwd1, rev1) = mtl.Pdf(hit, outDir, inDir, false);
            var (rev2, fwd2) = mtl.Pdf(hit, inDir, outDir, false);

            Assert.Equal(rev1, rev2, 3);
            Assert.Equal(fwd1, fwd2, 3);

            var sample = mtl.Sample(hit, outDir, false, new Vector2(0.2f, 0.7f));
            var (fwdS, revS) = mtl.Pdf(hit, outDir, sample.direction, false);

            Assert.Equal(sample.pdf, fwdS, 3);
            Assert.Equal(sample.pdfReverse, revS, 3);
        }

        [Fact]
        public void Albedo_ShouldBeWhite() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image<ColorRGB>.Constant(new ColorRGB(1, 1, 1))
            });

            var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0)
            }, new int[] {
                0, 1, 2,
                0, 2, 3
            });

            SurfacePoint hit = new SurfacePoint {
                BarycentricCoords = new Vector2(0.5f, 0.2f),
                Normal = new Vector3(0, 0, 1),
                Mesh = mesh,
                PrimId = 0,
                Position = new Vector3(0, 0, 0),
            };

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, 1);
            var retro = mtl.EvaluateWithCosine(hit, outDir, inDir, false);

            var primary = new Vector2(0.25f, 0.8f);
            var sample = mtl.Sample(hit, outDir, false, primary);

            Assert.Equal(1.0f / MathF.PI, retro.R, 3);
            Assert.Equal(1.0f / MathF.PI, retro.G, 3);
            Assert.Equal(1.0f / MathF.PI, retro.B, 3);

            Assert.Equal(1.0f, sample.weight.R, 3);
            Assert.Equal(1.0f, sample.weight.G, 3);
            Assert.Equal(1.0f, sample.weight.B, 3);
        }

        [Fact]
        public void Albedo_ShouldBeRed() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image<ColorRGB>.Constant(new ColorRGB(1, 0, 0))
            });

            var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0)
            }, new int[] {
                0, 1, 2,
                0, 2, 3
            });

            SurfacePoint hit = new SurfacePoint {
                BarycentricCoords = new Vector2(0.5f, 0.2f),
                Normal = new Vector3(0, 0, 1),
                Mesh = mesh,
                PrimId = 0,
                Position = new Vector3(0, 0, 0),
            };

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, 1);
            var retro = mtl.EvaluateWithCosine(hit, outDir, inDir, false);

            var primary = new Vector2(0.25f, 0.8f);
            var sample = mtl.Sample(hit, outDir, false, primary);

            Assert.Equal(1.0f / MathF.PI, retro.R, 3);
            Assert.Equal(0.0f / MathF.PI, retro.G, 3);
            Assert.Equal(0.0f / MathF.PI, retro.B, 3);

            Assert.Equal(1.0f, sample.weight.R, 3);
            Assert.Equal(0.0f, sample.weight.G, 3);
            Assert.Equal(0.0f, sample.weight.B, 3);
        }

        [Fact]
        public void EdgeCases_ShouldNotCauseOutliers() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image<ColorRGB>.Constant(new ColorRGB(1, 1, 1))
            });

            var mesh = new Mesh(new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0)
            }, new int[] {
                0, 1, 2,
                0, 2, 3
            });

            SurfacePoint hit = new SurfacePoint {
                BarycentricCoords = new Vector2(0.5f, 0.2f),
                Normal = new Vector3(0, 0, 1),
                Mesh = mesh,
                PrimId = 0,
                Position = new Vector3(0, 0, 0),
            };

            var outDir = new Vector3(0, 0, 1);

            var prims = new Vector2[] {
                new Vector2(0,0),
                new Vector2(0,1),
                new Vector2(1,0),
                new Vector2(1,1),

                new Vector2(float.Epsilon,float.Epsilon),
                new Vector2(float.Epsilon,1-float.Epsilon),
                new Vector2(1-float.Epsilon,float.Epsilon),
                new Vector2(1-float.Epsilon,1-float.Epsilon),
            };

            foreach (var prim in prims) {
                var sample = mtl.Sample(hit, outDir, false, prim);

                if (sample.pdf == 0) {
                    Assert.Equal(0.0f, sample.weight.R, 3);
                    Assert.Equal(0.0f, sample.weight.G, 3);
                    Assert.Equal(0.0f, sample.weight.B, 3);
                } else {
                    Assert.Equal(1.0f, sample.weight.R, 3);
                    Assert.Equal(1.0f, sample.weight.G, 3);
                    Assert.Equal(1.0f, sample.weight.B, 3);
                }
            }
        }
    }
}
