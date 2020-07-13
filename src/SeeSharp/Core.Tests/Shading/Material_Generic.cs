using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Materials;
using System.Numerics;
using Xunit;

namespace SeeSharp.Core.Tests.Shading {
    public class Material_Generic {
        [Fact]
        public void Pdfs_ShouldBeConsistent() {
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
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
    }
}
