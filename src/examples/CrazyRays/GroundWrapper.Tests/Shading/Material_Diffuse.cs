using GroundWrapper.Geometry;
using System.Numerics;
using System;
using Xunit;
using GroundWrapper.GroundMath;

namespace GroundWrapper.Tests {
    public class Material_Diffuse {
        [Fact]
        public void NoLightLeaks() {
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
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

            Hit hit = new Hit {
                barycentricCoords = new Vector2(0.5f, 0.2f),
                normal = new Vector3(0, 0, 1),
                mesh = mesh,
                primId = 0,
                position = new Vector3(0, 0, 0),
            };

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, -1);

            var (fwd, rev) = bsdf.Pdf(outDir, inDir, false);
            var val = bsdf.Evaluate(outDir, inDir, false);

            Assert.Equal(0, rev);
            Assert.Equal(0, fwd);
            Assert.Equal(0, val.r);
            Assert.Equal(0, val.g);
            Assert.Equal(0, val.b);
        }

        [Fact]
        public void ForwardAndReverse_ShouldMatch() {
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
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

            Hit hit = new Hit {
                barycentricCoords = new Vector2(0.5f, 0.2f),
                normal = new Vector3(0, 0, 1),
                mesh = mesh,
                primId = 0,
                position = new Vector3(0, 0, 0),
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
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
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

            Hit hit = new Hit {
                barycentricCoords = new Vector2(0.5f, 0.2f),
                normal = new Vector3(0, 0, 1),
                mesh = mesh, primId = 0,
                position = new Vector3(0, 0, 0),
            };

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, 1);
            var retro = bsdf.Evaluate(outDir, inDir, false);

            var primary = new Vector2(0.25f, 0.8f);
            var sample = bsdf.Sample(outDir, false, primary);

            Assert.Equal(1.0f / MathF.PI, retro.r, 3);
            Assert.Equal(1.0f / MathF.PI, retro.g, 3);
            Assert.Equal(1.0f / MathF.PI, retro.b, 3);

            Assert.Equal(1.0f, sample.weight.r, 3);
            Assert.Equal(1.0f, sample.weight.g, 3);
            Assert.Equal(1.0f, sample.weight.b, 3);
        }

        [Fact]
        public void Albedo_ShouldBeRed() {
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
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

            Hit hit = new Hit {
                barycentricCoords = new Vector2(0.5f, 0.2f),
                normal = new Vector3(0, 0, 1),
                mesh = mesh,
                primId = 0,
                position = new Vector3(0, 0, 0),
            };

            var bsdf = mtl.GetBsdf(hit);

            var outDir = new Vector3(0, 0, 1);
            var inDir = new Vector3(0, 0, 1);
            var retro = bsdf.Evaluate(outDir, inDir, false);

            var primary = new Vector2(0.25f, 0.8f);
            var sample = bsdf.Sample(outDir, false, primary);

            Assert.Equal(1.0f / MathF.PI, retro.r, 3);
            Assert.Equal(0.0f / MathF.PI, retro.g, 3);
            Assert.Equal(0.0f / MathF.PI, retro.b, 3);

            Assert.Equal(1.0f, sample.weight.r, 3);
            Assert.Equal(0.0f, sample.weight.g, 3);
            Assert.Equal(0.0f, sample.weight.b, 3);
        }

        [Fact]
        public void EdgeCases_ShouldNotCauseOutliers() {
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
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

            Hit hit = new Hit {
                barycentricCoords = new Vector2(0.5f, 0.2f),
                normal = new Vector3(0, 0, 1),
                mesh = mesh,
                primId = 0,
                position = new Vector3(0, 0, 0),
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
                    Assert.Equal(0.0f, sample.weight.r, 3);
                    Assert.Equal(0.0f, sample.weight.g, 3);
                    Assert.Equal(0.0f, sample.weight.b, 3);
                } else {
                    Assert.Equal(1.0f, sample.weight.r, 3);
                    Assert.Equal(1.0f, sample.weight.g, 3);
                    Assert.Equal(1.0f, sample.weight.b, 3);
                }
            }
        }
    }
}
