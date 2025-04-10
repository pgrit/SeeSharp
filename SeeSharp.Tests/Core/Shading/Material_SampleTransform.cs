using static SeeSharp.Sampling.SampleWarp;

namespace SeeSharp.Tests.Core.Shading;

/// <summary>
/// Asserts that the sample transformation defined by each material yields correct estimates.
/// Computes the albedo of different materials with a Riemann sum. Once with cosine hemisphere transformation,
/// once with the materials sample transformation.
/// </summary>
public class Material_SampleTransform {
    static SurfacePoint MakeDummyHit() {
        Raytracer rt = new();
        rt.AddMesh(new(new Vector3[] {
            new(-10, -10, -2),
            new( 10, -10, -2),
            new( 10,  10, -2),
            new(-10,  10, -2),
        }, new int[] {
            0, 1, 2, 0, 2, 3
        }));
        rt.CommitScene();
        return rt.Trace(new() { Origin = Vector3.Zero, Direction = -Vector3.UnitZ });
    }

    (float, float) ConfidenceInterval(int numSamples, float mean, float variance) {
        var offset = MathF.Sqrt(variance) / MathF.Sqrt(numSamples);
        return (mean - 2.576f * offset, mean + 2.576f * offset); // 99% confidence interval, assuming normal distrib.
    }

    /// <summary>
    /// Generic MC estimator for an integral over directions.
    /// </summary>
    /// <param name="numSamples">Number of samples</param>
    /// <param name="integrand">The integrand, R^3 -> R</param>
    /// <param name="transform">
    /// A sample transformation that transforms uniform 2D numbers and returns the direction and its PDF.
    /// </param>
    /// <returns>99% confidence interval of the result, assuming normal distribution.</returns>
    (float, float) Integrate(int numSamples, Func<Vector3, float> integrand,
                             Func<float, Vector2, (Vector3, float)> transform) {
        float total = 0;
        float totalSquares = 0;
        object mutex = new object();

        // round the number of samples
        int numBatches = 32;
        numSamples = (numSamples / numBatches) * numBatches;

        float invNum = 1f / numSamples;
        Parallel.For(0, numBatches, i => {
            float result = 0;
            float squares = 0;
            RNG rng = new(18273912, (uint)i, 1313);
            for (int j = 0; j < numSamples / numBatches; ++j) {
                var primary = rng.NextFloat2D();
                var (dir, pdf) = transform(rng.NextFloat(), primary);
                if (pdf == 0) continue;
                float val = integrand(dir) / pdf;
                result += val * invNum;
                squares += val * val * invNum;
            }
            lock(mutex) {
                total += result;
                totalSquares += squares;
            }
        });
        float variance = totalSquares - total * total;
        if (variance <= 0)
            return (total, total);
        return ConfidenceInterval(numSamples, total, variance);
    }

    void IntegratePdf(Material mtl, Vector3 outDir) {
        var hit = MakeDummyHit();

        var uniformTask = Task<float>.Run(() => Integrate(1_000_000,
            dir => mtl.Pdf(hit, outDir, dir, false).Item1,
            (_, primary) => {
                var sample = ToUniformSphere(primary);
                return (sample.Direction, sample.Pdf);
            }
        ));

        uniformTask.Wait();

        Assert.True(Overlap(uniformTask.Result, (1.0f, 1.0f)));
    }

    /// <summary>
    /// Checks if two intervals overlap.
    /// </summary>
    bool Overlap((float lo, float hi) a, (float lo, float hi) b) => b.lo < a.hi && b.hi > a.lo;

    void IntegrateBSDF(Material mtl, Vector3 outDir) {
        var hit = MakeDummyHit();

        var uniformTask = Task<float>.Run(() => Integrate(1_000_000,
            dir => mtl.EvaluateWithCosine(hit, outDir, dir, false).Average,
            (_, primary) => {
                var sample = ToUniformSphere(primary);
                return (sample.Direction, sample.Pdf);
            }
        ));

        var importanceSamplingTask = Task<float>.Run(() => Integrate(1_000_000,
            dir => mtl.EvaluateWithCosine(hit, outDir, dir, false).Average,
            (u, primary) => {
                var sample = mtl.Sample(hit, outDir, false, u, primary);
                return (sample.Direction, sample.Pdf);
            }
        ));

        uniformTask.Wait();
        importanceSamplingTask.Wait();

        Assert.True(Overlap(uniformTask.Result, importanceSamplingTask.Result));
    }

    [Fact]
    public void Diffuse_AlbedoShouldMatch() {
        Material mtl = new DiffuseMaterial(new() {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Transmitter = true
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(1, 0, 0.1f)));
    }

    [Fact]
    public void Diffuse_PDFShouldBeDensity() {
        Material mtl = new DiffuseMaterial(new() {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Transmitter = true
        });
        IntegratePdf(mtl, Vector3.Normalize(new Vector3(1, 0, 0.1f)));
    }

    [Fact]
    public void GenericRough_AlbedoShouldMatch() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.9f),
            IndexOfRefraction = 1.1f
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(0, 0, 1f)));
    }

    [Fact]
    public void GenericRough_PDFShouldBeDensity() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.9f),
            IndexOfRefraction = 1.1f
        });
        IntegratePdf(mtl, Vector3.Normalize(new Vector3(0, 0, 1f)));
    }

    [Fact]
    public void GenericGlossy_AlongNormal_AlbedoShouldMatch() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            Metallic = 0.6f
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(0, 0, 1f)));
    }

    [Fact]
    public void GenericGlossy_AlongNormal_PDFShouldBeDensity() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            Metallic = 0.6f
        });
        IntegratePdf(mtl, new Vector3(0, 0, 1f));
    }

    [Fact]
    public void GenericGlossy_AlongNormal_Underneath_AlbedoShouldMatch() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            Metallic = 0.6f
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(0, 0, -1f)));
    }

    [Fact]
    public void GenericGlossy_AlongNormal_Underneath_PDFShouldBeDensity() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            Metallic = 0.6f
        });
        IntegratePdf(mtl, new Vector3(0, 0, -1f));
    }

    [Fact]
    public void GenericGlossy_GrazingAngle_AlbedoShouldMatch() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            Metallic = 0.6f
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(1f, 0, 0.01f)));
    }

    [Fact]
    public void GenericGlossy_GrazingAngle_PDFShouldBeDensity() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            Metallic = 0.6f
        });
        IntegratePdf(mtl, Vector3.Normalize(new Vector3(1f, 0, 0.01f)));
    }

    [Fact]
    public void GenericGlass_AlongNormal_AlbedoShouldMatch() {
        GenericMaterial mtl = new(new() {
            Roughness = new(0.1f),
            Anisotropic = 0.0f,
            IndexOfRefraction = 1.45f,
            Metallic = 0.0f,
            SpecularTintStrength = 0.0f,
            SpecularTransmittance = 1.0f,
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(0, 0, 1f)));
    }

    [Fact]
    public void GenericGlass_GrazingAngle_AlbedoShouldMatch() {
        GenericMaterial mtl = new(new() {
            Roughness = new(0.1f),
            Anisotropic = 0.0f,
            IndexOfRefraction = 1.45f,
            Metallic = 0.0f,
            SpecularTintStrength = 0.0f,
            SpecularTransmittance = 1.0f,
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(1f, 0, 0.01f)));
    }

    [Fact]
    public void GenericGlass_GrazingAngle_PDFShouldBeDensity() {
        GenericMaterial mtl = new(new() {
            Roughness = new(0.1f),
            Anisotropic = 0.0f,
            IndexOfRefraction = 1.45f,
            Metallic = 0.0f,
            SpecularTintStrength = 0.0f,
            SpecularTransmittance = 1.0f,
        });
        IntegratePdf(mtl, Vector3.Normalize(new Vector3(1f, 0, 0.01f)));
    }

    [Fact]
    public void GenericGlass_GrazingAngle_Underneath_AlbedoShouldMatch() {
        GenericMaterial mtl = new(new() {
            Roughness = new(0.1f),
            Anisotropic = 0.0f,
            IndexOfRefraction = 1.45f,
            Metallic = 0.0f,
            SpecularTintStrength = 0.0f,
            SpecularTransmittance = 1.0f,
        });
        IntegrateBSDF(mtl, Vector3.Normalize(new Vector3(1f, 0, -1.1f)));
    }

    [Fact]
    public void GenericGlass_GrazingAngle_Underneath_PDFShouldBeDensity() {
        GenericMaterial mtl = new(new() {
            Roughness = new(0.1f),
            Anisotropic = 0.0f,
            IndexOfRefraction = 1.45f,
            Metallic = 0.0f,
            SpecularTintStrength = 0.0f,
            SpecularTransmittance = 1.0f,
        });
        IntegratePdf(mtl, Vector3.Normalize(new Vector3(1f, 0, -1.1f)));
    }

    [Theory]
    [InlineData(0.9f, 1f, 0f, 0.1f)]
    [InlineData(0.1f, 1f, 0f, 0.1f)]
    [InlineData(0.9f, 1f, 0f, 1f)]
    [InlineData(0.1f, 1f, 0f, 1f)]
    public void TrowbridgeReitzDistribution_ShouldBeValidPDF(float roughness, float ox, float oy, float oz) {
        var outDir = Vector3.Normalize(new(ox, oy, oz));

        float ax = Math.Max(.001f, roughness * roughness);
        float ay = Math.Max(.001f, roughness * roughness);
        var microfacetDistrib = new TrowbridgeReitzDistribution { AlphaX = ax, AlphaY = ay };

        var result = Integrate(1_000_000,
            dir => microfacetDistrib.Pdf(outDir, dir),
            (_, primary) => {
                var sample = ToUniformSphere(primary);
                return (sample.Direction, sample.Pdf);
            }
        );

        Assert.True(Overlap(result, (1.0f, 1.0f)));
    }
}