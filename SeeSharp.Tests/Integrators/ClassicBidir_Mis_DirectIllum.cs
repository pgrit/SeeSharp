using SeeSharp.Tests.Integrators.Helpers;
using static SeeSharp.Integrators.Bidir.BidirBase;

namespace SeeSharp.Tests.Integrators;

public class ClassicBidir_Mis_DirectIllum {

    MisDummyPath dummyPath = new MisDummyPath(
        lightArea: 2.0f,
        numLightPaths: 500,
        positions: new Vector3[] {
            new Vector3 { X = 0, Y = 2, Z = 0 }, // light
            new Vector3 { X = 0, Y = 0, Z = 0 }, // surface
            new Vector3 { X = 2, Y = 0, Z = 0 }  // camera
        },
        normals: new Vector3[] {
            Vector3.Normalize(new Vector3 { X = 0, Y = -1, Z = 0 }), // light
            Vector3.Normalize(new Vector3 { X = 1, Y =  1, Z = 0 }), // surface
        });

    float NextEventWeight() {
        var computer = new ClassicBidir();
        computer.LightPaths = new LightPathCache();
        computer.LightPaths.PathCache = dummyPath.pathCache;
        computer.NumLightPaths = dummyPath.numLightPaths;

        var cameraPath = new CameraPath {
            Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..2]),
            Distances = dummyPath.Distances
        };

        float pdfReverse = dummyPath.cameraVertices[^2].PdfToAncestor;
        // Set a guard value to make sure that the correct pdf is used!
        var dummyVert = cameraPath.Vertices[^1];
        dummyVert.PdfToAncestor = -1000.0f;
        cameraPath.Vertices[^1] = dummyVert;

        return computer.NextEventMis(cameraPath,
            pdfEmit: dummyPath.pathCache[0, 1].PdfFromAncestor,
            pdfNextEvent: 1.0f / dummyPath.lightArea,
            pdfHit: dummyPath.cameraVertices[2].PdfFromAncestor,
            pdfReverse: pdfReverse,
            isBackground: false);
    }

    float LightTracerWeight() {
        var computer = new ClassicBidir();
        computer.LightPaths = new LightPathCache();
        computer.LightPaths.PathCache = dummyPath.pathCache;
        computer.NumLightPaths = dummyPath.numLightPaths;

        return computer.LightTracerMis(dummyPath.pathCache[0, dummyPath.lightEndpointIdx],
            pdfCamToPrimary: dummyPath.cameraVertices[1].PdfFromAncestor,
            pdfReverse: dummyPath.cameraVertices[2].PdfFromAncestor,
            pdfNextEvent: 1 / dummyPath.lightArea,
            pixel: new(),
            distToCam: dummyPath.Distances[^1]);
    }

    float HitWeight() {
        var computer = new ClassicBidir();
        computer.LightPaths = new LightPathCache();
        computer.LightPaths.PathCache = dummyPath.pathCache;
        computer.NumLightPaths = dummyPath.numLightPaths;

        var cameraPath = new CameraPath {
            Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..3]),
            Distances = dummyPath.Distances
        };

        return computer.EmitterHitMis(cameraPath,
            pdfEmit: dummyPath.pathCache[0, 1].PdfFromAncestor,
            pdfNextEvent: 1.0f / dummyPath.lightArea);
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

    [Fact]
    public void ShouldBeCorrectBalanceWeight() {
        float weightNextEvt = NextEventWeight();
        float weightLightTracer = LightTracerWeight();
        float weightBsdf = HitWeight();

        // Compute the ground truth values
        var verts = dummyPath.cameraVertices;
        float pdfHit = verts[1].PdfFromAncestor * verts[2].PdfFromAncestor;
        float pdfNextEvt = verts[1].PdfFromAncestor * (1.0f / dummyPath.lightArea);

        var lightVerts = dummyPath.pathCache;
        float pdfLightTracer = lightVerts[0, 1].PdfFromAncestor * dummyPath.numLightPaths;

        float pdfSum = pdfHit + pdfNextEvt + pdfLightTracer;

        float expectedWeightNextEvt = pdfNextEvt / pdfSum;
        float expectedWeightHit = pdfHit / pdfSum;
        float expectedWeightLightTracer = pdfLightTracer / pdfSum;

        Assert.Equal(weightNextEvt, expectedWeightNextEvt, 3);
        Assert.Equal(weightBsdf, expectedWeightHit, 3);
        Assert.Equal(weightLightTracer, expectedWeightLightTracer, 3);
    }
}
