using SeeSharp.Geometry;
using SeeSharp.Shading.Materials;
using SimpleImageIO;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Core.Shading;

public class Material_Generic {
    [Fact]
    public void Pdfs_ShouldBeConsistent() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(0.2f),
            SpecularTransmittance = 0.8f,
            DiffuseTransmittance = 0.3f
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
        var inDir = Vector3.Normalize(new Vector3(0, 1, 1));

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
    public void Pdf_ShouldBeNonZero() {
        Material mtl = new GenericMaterial(new GenericMaterial.Parameters {
            BaseColor = new(new RgbColor(1, 1, 1)),
            Roughness = new(1.0f),
            SpecularTransmittance = 1.0f,
            DiffuseTransmittance = 1.0f,
            Anisotropic = 0.0f,
            IndexOfRefraction = 1.4500000476837158f,
            Metallic = 0.0f,
            SpecularTintStrength = 0.0f,
            Thin = true,
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
            Normal = new Vector3(0.092231974f, -0.13183558f, 0.9869715f),
            Mesh = mesh,
            PrimId = 0,
            Position = new Vector3(0, 0, 0),
        };

        var outDir = Vector3.Normalize(new Vector3(-0.297268f, 0.35681713f, 0.8856147f));
        var inDir = Vector3.Normalize(new Vector3(-0.38466394f, -0.8108599f, -0.44106674f));

        var (fwd1, rev1) = mtl.Pdf(hit, outDir, inDir, false);
        var (rev2, fwd2) = mtl.Pdf(hit, inDir, outDir, false);

        var value = mtl.EvaluateWithCosine(hit, outDir, inDir, false);
        Assert.NotEqual(value, RgbColor.Black);

        Assert.NotEqual(0.0f, rev1, 3);
        Assert.NotEqual(0.0f, rev2, 3);
        Assert.NotEqual(0.0f, fwd1, 3);
        Assert.NotEqual(0.0f, fwd2, 3);
    }
}