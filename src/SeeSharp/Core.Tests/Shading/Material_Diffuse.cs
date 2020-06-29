using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Materials;
using System;
using System.Numerics;
using Xunit;

namespace SeeSharp.Core.Tests {
    public class Material_Diffuse {
        [Fact]
        public void NoLightLeaks() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(new ColorRGB(1, 1, 1))
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

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, -1);

            var (fwd, rev) = bsdf.Pdf(outDir, inDir, false);
            var val = bsdf.EvaluateWithCosine(outDir, inDir, false);

            Assert.Equal(0, rev);
            Assert.Equal(0, fwd);
            Assert.Equal(0, val.R);
            Assert.Equal(0, val.G);
            Assert.Equal(0, val.B);
        }

        [Fact]
        public void ForwardAndReverse_ShouldMatch() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(new ColorRGB(1, 1, 1))
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

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 1, 1);

            var (fwd1, rev1) = bsdf.Pdf(outDir, inDir, false);
            var (rev2, fwd2) = bsdf.Pdf(inDir, outDir, false);

            Assert.Equal(rev1, rev2, 3);
            Assert.Equal(fwd1, fwd2, 3);

            var sample = bsdf.Sample(outDir, false, new Vector2(0.2f, 0.7f));
            var (fwdS, revS) = bsdf.Pdf(outDir, sample.direction, false);

            Assert.Equal(sample.pdf, fwdS, 3);
            Assert.Equal(sample.pdfReverse, revS, 3);
        }

        [Fact]
        public void Albedo_ShouldBeWhite() {
            Material mtl = new DiffuseMaterial(new DiffuseMaterial.Parameters {
                baseColor = Image.Constant(new ColorRGB(1, 1, 1))
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

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, 1);
            var retro = bsdf.EvaluateWithCosine(outDir, inDir, false);

            var primary = new Vector2(0.25f, 0.8f);
            var sample = bsdf.Sample(outDir, false, primary);

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
                baseColor = Image.Constant(new ColorRGB(1, 0, 0))
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

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, 1);
            var retro = bsdf.EvaluateWithCosine(outDir, inDir, false);

            var primary = new Vector2(0.25f, 0.8f);
            var sample = bsdf.Sample(outDir, false, primary);

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
                baseColor = Image.Constant(new ColorRGB(1, 1, 1))
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

            var bsdf = mtl.GetBsdf(hit);

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
                var sample = bsdf.Sample(outDir, false, prim);

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
