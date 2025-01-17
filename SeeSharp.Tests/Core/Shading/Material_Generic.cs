namespace SeeSharp.Tests.Core.Shading;

public class Material_Generic {
    [Theory]
    [InlineData(0, 0, 1, 0, 1, 1)]
    [InlineData(1, 0, 1, 0, 1, 1)]
    [InlineData(1, 0, 1, 1, 0, -1)]
    public void Pdfs_ShouldBeConsistent(float ox, float oy, float oz, float ix, float iy, float iz) {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            SpecularTransmittance = 0.8f,
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

        var outDir = Vector3.Normalize(new Vector3(ox, oy, oz));
        var inDir = Vector3.Normalize(new Vector3(ix, iy, iz));

        var (fwd1, rev1) = mtl.Pdf(hit, outDir, inDir, false);
        var (rev2, fwd2) = mtl.Pdf(hit, inDir, outDir, false);

        Assert.Equal(rev1, rev2, 3);
        Assert.Equal(fwd1, fwd2, 3);

        var sample = mtl.Sample(hit, outDir, false, new Vector2(0.2f, 0.7f));
        var (fwdS, revS) = mtl.Pdf(hit, outDir, sample.Direction, false);

        Assert.Equal(sample.Pdf, fwdS, 3);
        Assert.Equal(sample.PdfReverse, revS, 3);
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 1, 1)]
    [InlineData(1, 0, 1, 0, 1, 1)]
    [InlineData(1, 0, 1, 1, 0, -1)]
    public void ComponentPdfs_ShouldBeConsistent(float ox, float oy, float oz, float ix, float iy, float iz) {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            SpecularTransmittance = 0.8f,
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

        var outDir = Vector3.Normalize(new Vector3(ox, oy, oz));
        var inDir = Vector3.Normalize(new Vector3(ix, iy, iz));

        Material.ComponentWeights componentWeights = new() {
            Pdfs = stackalloc float[mtl.MaxSamplingComponents],
            Weights = stackalloc float[mtl.MaxSamplingComponents],
            PdfsReverse = stackalloc float[mtl.MaxSamplingComponents],
            WeightsReverse = stackalloc float[mtl.MaxSamplingComponents],
        };

        var (fwd, rev) = mtl.Pdf(hit, outDir, inDir, false, ref componentWeights);

        float fwdRecompute = 0;
        for (int i = 0; i < componentWeights.NumComponents; ++i) {
            fwdRecompute += componentWeights.Pdfs[i] * componentWeights.Weights[i];
        }
        float revRecompute = 0;
        for (int i = 0; i < componentWeights.NumComponents; ++i) {
            revRecompute += componentWeights.PdfsReverse[i] * componentWeights.WeightsReverse[i];
        }

        Assert.Equal(rev, revRecompute, 3);
        Assert.Equal(fwd, fwdRecompute, 3);

        var sample = mtl.Sample(hit, outDir, false, new Vector2(0.2f, 0.7f), ref componentWeights);
        float fwdRecomputeS = 0;
        for (int i = 0; i < componentWeights.NumComponents; ++i) {
            fwdRecomputeS += componentWeights.Pdfs[i] * componentWeights.Weights[i];
        }
        float revRecomputeS = 0;
        for (int i = 0; i < componentWeights.NumComponents; ++i) {
            revRecomputeS += componentWeights.PdfsReverse[i] * componentWeights.WeightsReverse[i];
        }

        Assert.Equal(sample.Pdf, fwdRecomputeS, 3);
        Assert.Equal(sample.PdfReverse, revRecomputeS, 3);
    }

    // [Fact]
    // public void Pdf_ShouldBeNonZero() {
    //     Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
    //         BaseColor = new(new RgbColor(1, 1, 1)),
    //         Roughness = new(1.0f),
    //         SpecularTransmittance = 1.0f,
    //         Anisotropic = 0.0f,
    //         IndexOfRefraction = 1.4500000476837158f,
    //         Metallic = 0.0f,
    //         SpecularTintStrength = 0.0f,
    //     });

    //     var mesh = new Mesh(new Vector3[] {
    //         new Vector3(-1, -1, 0),
    //         new Vector3( 1, -1, 0),
    //         new Vector3( 1,  1, 0),
    //         new Vector3(-1,  1, 0)
    //     }, new int[] {
    //         0, 1, 2,
    //         0, 2, 3
    //     });

    //     SurfacePoint hit = new SurfacePoint {
    //         BarycentricCoords = new Vector2(0.5f, 0.2f),
    //         Normal = new Vector3(0.092231974f, -0.13183558f, 0.9869715f),
    //         Mesh = mesh,
    //         PrimId = 0,
    //         Position = new Vector3(0, 0, 0),
    //     };

    //     var outDir = Vector3.Normalize(new Vector3(-0.297268f, 0.35681713f, 0.8856147f));
    //     var inDir = Vector3.Normalize(new Vector3(-0.38466394f, -0.8108599f, -0.44106674f));

    //     var (fwd1, rev1) = mtl.Pdf(hit, outDir, inDir, false);
    //     var (rev2, fwd2) = mtl.Pdf(hit, inDir, outDir, true);

    //     var value = mtl.EvaluateWithCosine(hit, outDir, inDir, false);
    //     Assert.NotEqual(value, RgbColor.Black);

    //     Assert.NotEqual(0.0f, fwd1, 3);
    //     Assert.NotEqual(0.0f, fwd2, 3);
    //     Assert.Equal(fwd1, fwd2, 3);
    // }
}