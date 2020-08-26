using SeeSharp.Core;
using SeeSharp.Integrators.Bidir;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using static SeeSharp.Integrators.Bidir.BidirBase;

namespace SeeSharp.Integrators.Tests {
    public class Vcm_MIS_SingleBounce {

        static Helpers.MisDummyPath dummyPath = new Helpers.MisDummyPath(
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

        // We don't create an actual scene, so we need to set the radius somehow for MIS
        class FixedRadiusVcm : VertexConnectionAndMerging {
            public override void InitializeRadius(Scene scene) => Radius = 0.1f;
        }

        static VertexConnectionAndMerging dummyVcm = new FixedRadiusVcm {
            lightPaths = new LightPathCache() { PathCache = dummyPath.pathCache },
            NumLightPaths = dummyPath.numLightPaths
        };

        float NextEventWeight() {
            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..3])
            };

            float pdfReverse = dummyPath.cameraVertices[^2].PdfToAncestor;

            return dummyVcm.NextEventMis(cameraPath,
                pdfEmit: dummyPath.pathCache[1].PdfFromAncestor,
                pdfNextEvent: 1.0f / dummyPath.lightArea,
                pdfHit: dummyPath.cameraVertices[^1].PdfFromAncestor,
                pdfReverse: pdfReverse);
        }

        float LightTracerWeight() {
            return dummyVcm.LightTracerMis(dummyPath.pathCache[dummyPath.lightEndpointIdx],
                pdfCamToPrimary: dummyPath.cameraVertices[1].PdfFromAncestor,
                pdfReverse: dummyPath.cameraVertices[2].PdfFromAncestor,
                pdfNextEvent: 0,
                pixel: Vector2.Zero);
        }

        float HitWeight() {
            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..4])
            };

            return dummyVcm.EmitterHitMis(cameraPath,
                pdfEmit: dummyPath.pathCache[1].PdfFromAncestor,
                pdfNextEvent: 1.0f / dummyPath.lightArea);
        }

        float ConnectFirstToSecondWeight() {
            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..2])
            };

            var lightVertex = dummyPath.pathCache[dummyPath.pathCache[dummyPath.lightEndpointIdx].AncestorId];
            return dummyVcm.BidirConnectMis(cameraPath, lightVertex,
                pdfCameraReverse: 1, // light tracer connections are deterministic
                pdfCameraToLight: dummyPath.cameraVertices[2].PdfFromAncestor,
                pdfLightReverse: dummyPath.cameraVertices[3].PdfFromAncestor,
                pdfNextEvent: 1 / dummyPath.lightArea,
                pdfLightToCamera: dummyPath.cameraVertices[2].PdfToAncestor);
        }

        float MergeFirstWeight() {
            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..2])
            };

            var photon = dummyPath.pathCache[dummyPath.lightEndpointIdx];
            return dummyVcm.MergeMis(cameraPath, photon,
                                     pdfCameraReverse: dummyPath.cameraVertices[^3].PdfToAncestor,
                                     pdfLightReverse: dummyPath.cameraVertices[^2].PdfFromAncestor + 1 / dummyPath.lightArea,
                                     pdfNextEvent: 0);
        }

        float MergeSecondWeight() {
            var cameraPath = new CameraPath {
                Vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..3])
            };

            var photon = dummyPath.pathCache[dummyPath.pathCache[dummyPath.lightEndpointIdx].AncestorId];
            return dummyVcm.MergeMis(cameraPath, photon,
                                     pdfCameraReverse: dummyPath.cameraVertices[^2].PdfToAncestor,
                                     pdfLightReverse: dummyPath.cameraVertices[^1].PdfFromAncestor,
                                     pdfNextEvent: 1 / dummyPath.lightArea);
        }

        [Fact]
        public void MergeFirst_ShouldBeValid() {
            float weightMerge = MergeFirstWeight();
            Assert.True(weightMerge <= 1.0f);
            Assert.True(weightMerge >= 0.0f);
        }

        [Fact]
        public void MergeSecond_ShouldBeValid() {
            float weightMerge = MergeSecondWeight();
            Assert.True(weightMerge <= 1.0f);
            Assert.True(weightMerge >= 0.0f);
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
        public void Connect_ShouldBeValid() {
            float weightConnect = ConnectFirstToSecondWeight();
            Assert.True(weightConnect <= 1.0f);
            Assert.True(weightConnect >= 0.0f);
        }

        [Fact]
        public void ShouldSumToOne() {
            float weightNextEvt = NextEventWeight();
            float weightLightTracer = LightTracerWeight();
            float weightBsdf = HitWeight();
            float weightMergeFirst = MergeFirstWeight();
            float weightMergeSecond = MergeSecondWeight();
            float weightConnect = ConnectFirstToSecondWeight();

            float weightSum = weightNextEvt + weightBsdf + weightLightTracer + weightMergeFirst
                            + weightMergeSecond + weightConnect;

            Assert.Equal(1.0f, weightSum, 2);
        }

        [Fact]
        public void ShouldBeCorrectBalanceWeight() {
            float weightNextEvt = NextEventWeight();
            float weightLightTracer = LightTracerWeight();
            float weightBsdf = HitWeight();
            float weightMergeFirst = MergeFirstWeight();
            float weightMergeSecond = MergeSecondWeight();
            float weightConnect = ConnectFirstToSecondWeight();

            // Compute the ground truth values
            var verts = dummyPath.cameraVertices;
            float pdfHit = verts[1].PdfFromAncestor * verts[2].PdfFromAncestor * verts[3].PdfFromAncestor;
            float pdfNextEvt = verts[1].PdfFromAncestor * verts[2].PdfFromAncestor * (1.0f / dummyPath.lightArea);

            var lightVerts = dummyPath.pathCache;
            float pdfLightTracer = lightVerts[1].PdfFromAncestor * lightVerts[2].PdfFromAncestor * dummyPath.numLightPaths;

            float pdfConnectFirst = verts[1].PdfFromAncestor * lightVerts[1].PdfFromAncestor;

            float pdfMergeFirst = verts[1].PdfFromAncestor * lightVerts[1].PdfFromAncestor * lightVerts[2].PdfFromAncestor
                * dummyPath.numLightPaths * System.MathF.PI * dummyVcm.Radius * dummyVcm.Radius;

            float pdfMergeSecond = verts[1].PdfFromAncestor * verts[2].PdfFromAncestor * lightVerts[1].PdfFromAncestor
                * dummyPath.numLightPaths * System.MathF.PI * dummyVcm.Radius * dummyVcm.Radius;

            float pdfSum = pdfHit + pdfNextEvt + pdfLightTracer + pdfMergeFirst + pdfMergeSecond + pdfConnectFirst;

            float expectedWeightNextEvt = pdfNextEvt / pdfSum;
            float expectedWeightHit = pdfHit / pdfSum;
            float expectedWeightLightTracer = pdfLightTracer / pdfSum;
            float expectedWeightMergeFirst = pdfMergeFirst / pdfSum;
            float expectedWeightMergeSecond = pdfMergeSecond / pdfSum;
            float expectedWeightConnect = pdfConnectFirst / pdfSum;

            Assert.Equal(weightNextEvt, expectedWeightNextEvt, 3);
            Assert.Equal(weightBsdf, expectedWeightHit, 3);
            Assert.Equal(weightLightTracer, expectedWeightLightTracer, 3);
            Assert.Equal(weightMergeFirst, expectedWeightMergeFirst, 3);
            Assert.Equal(weightMergeSecond, expectedWeightMergeSecond, 3);
            Assert.Equal(weightConnect, expectedWeightConnect, 3);
        }
    }
}
