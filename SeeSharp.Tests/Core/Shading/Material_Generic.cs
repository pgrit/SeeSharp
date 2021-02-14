using SeeSharp.Geometry;
using SeeSharp.Shading.Materials;
using SimpleImageIO;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Shading {
    public class Material_Generic {
        [Fact]
        public void Pdfs_ShouldBeConsistent() {
            Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
                baseColor = new(new RgbColor(1, 1, 1)),
                roughness = new(0.2f),
                specularTransmittance = 0.8f,
                diffuseTransmittance = 0.3f
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
    }
}
