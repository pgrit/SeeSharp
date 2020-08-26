using SeeSharp.Integrators.Bidir;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using static SeeSharp.Integrators.Bidir.BidirBase;

namespace SeeSharp.Integrators.Tests {

    public class ClassicBidir_Mis_SingleBounce {

        Helpers.MisDummyPath dummyPath = new Helpers.MisDummyPath(
            lightArea: 2.0f,
            numLightPaths: 500,
            positions: new Vector3[] {
                new Vector3 { X = 0, Y = 2, Z = 0 }, // light
                new Vector3 { X = 0, Y = 0, Z = 0 }, // surface A
                new Vector3 { X = 2, Y = 1, Z = 0 }, // surface B
                new Vector3 { X = 2, Y = 0, Z = 0 }  // camera
            },
            normals: new Vector3[] {
                Vector3.Normalize(new Vector3 { X =    0, Y = -1, Z = 0 }), // light
                Vector3.Normalize(new Vector3 { X = 0.3f, Y =  1, Z = 0.2f }), // surface A
                Vector3.Normalize(new Vector3 { X =   -1, Y = -1, Z = 0 }), // surface B
            });

        float NextEventWeight() {
            var computer = new ClassicBidir();
            computer.lightPaths = new LightPathCache();
            computer.lightPaths.PathCache = dummyPath.pathCache;
            computer.NumLightPaths = dummyPath.numLightPaths;

            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..3])
            };

            float pdfReverse = dummyPath.cameraVertices[^2].PdfToAncestor;
            // Set a guard value to make sure that the correct pdf is used!
            var dummyVert = cameraPath.Vertices[^1];
            dummyVert.PdfToAncestor = -1000.0f;
            cameraPath.Vertices[^1] = dummyVert;

            return computer.NextEventMis(cameraPath,
                pdfEmit: dummyPath.pathCache[1].PdfFromAncestor,
                pdfNextEvent: 1.0f / dummyPath.lightArea,
                pdfHit: dummyPath.cameraVertices[^1].PdfFromAncestor,
                pdfReverse: pdfReverse);
        }

        float LightTracerWeight() {
            var computer = new ClassicBidir();
            computer.lightPaths = new LightPathCache();
            computer.lightPaths.PathCache = dummyPath.pathCache;
            computer.NumLightPaths = dummyPath.numLightPaths;

            return computer.LightTracerMis(dummyPath.pathCache[dummyPath.lightEndpointIdx],
                pdfCamToPrimary: dummyPath.cameraVertices[1].PdfFromAncestor,
                pdfReverse: dummyPath.cameraVertices[2].PdfFromAncestor,
                pdfNextEventAncestor: 0,
                pixel: Vector2.Zero);
        }

        float HitWeight() {
            var computer = new ClassicBidir();
            computer.lightPaths = new LightPathCache();
            computer.lightPaths.PathCache = dummyPath.pathCache;
            computer.NumLightPaths = dummyPath.numLightPaths;

            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..4])
            };

            return computer.EmitterHitMis(cameraPath,
                pdfEmit: dummyPath.pathCache[1].PdfFromAncestor,
                pdfNextEvent: 1.0f / dummyPath.lightArea);
        }

        float ConnectFirstToSecondWeight() {
            var computer = new ClassicBidir();
            computer.lightPaths = new LightPathCache();
            computer.lightPaths.PathCache = dummyPath.pathCache;
            computer.NumLightPaths = dummyPath.numLightPaths;

            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..2])
            };

            var lightVertex = dummyPath.pathCache[dummyPath.pathCache[dummyPath.lightEndpointIdx].AncestorId];

            return computer.BidirConnectMis(cameraPath, lightVertex,
                pdfCameraReverse: 1, // light tracer connections are deterministic
                pdfCameraToLight: dummyPath.cameraVertices[2].PdfFromAncestor,
                pdfLightReverse: dummyPath.cameraVertices[3].PdfFromAncestor,
                pdfNextEventAncestor: 1 / dummyPath.lightArea,
                pdfLightToCamera: dummyPath.cameraVertices[2].PdfToAncestor);
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
        public void ConnectFirst_ShouldBeValid() {
            float weightConnect = ConnectFirstToSecondWeight();
            Assert.True(weightConnect <= 1.0f);
            Assert.True(weightConnect >= 0.0f);
        }

        [Fact]
        public void ShouldSumToOne() {
            float weightNextEvt = NextEventWeight();
            float weightLightTracer = LightTracerWeight();
            float weightBsdf = HitWeight();
            float weightConnect = ConnectFirstToSecondWeight();

            float weightSum = weightNextEvt + weightBsdf + weightLightTracer + weightConnect;

            Assert.Equal(1.0f, weightSum, 2);
        }

        [Fact]
        public void ShouldBeCorrectBalanceWeight() {
            float weightNextEvt = NextEventWeight();
            float weightLightTracer = LightTracerWeight();
            float weightBsdf = HitWeight();
            float weightConnect = ConnectFirstToSecondWeight();

            // Compute the ground truth values
            var verts = dummyPath.cameraVertices;
            float pdfHit = verts[1].PdfFromAncestor * verts[2].PdfFromAncestor * verts[3].PdfFromAncestor;
            float pdfNextEvt = verts[1].PdfFromAncestor * verts[2].PdfFromAncestor *  (1.0f / dummyPath.lightArea);

            var lightVerts = dummyPath.pathCache;
            float pdfLightTracer = lightVerts[1].PdfFromAncestor * lightVerts[2].PdfFromAncestor * dummyPath.numLightPaths;

            float pdfConnectFirst = verts[1].PdfFromAncestor * lightVerts[1].PdfFromAncestor;

            float pdfSum = pdfHit + pdfNextEvt + pdfLightTracer + pdfConnectFirst;

            float expectedWeightNextEvt = pdfNextEvt / pdfSum;
            float expectedWeightHit = pdfHit / pdfSum;
            float expectedWeightLightTracer = pdfLightTracer / pdfSum;
            float expectedWeightConnect = pdfConnectFirst / pdfSum;

            Assert.Equal(expectedWeightHit, weightBsdf, 3);
            Assert.Equal(expectedWeightNextEvt, weightNextEvt, 3);
            Assert.Equal(expectedWeightConnect, weightConnect, 3);
            Assert.Equal(expectedWeightLightTracer, weightLightTracer, 3);
        }
    }
}
