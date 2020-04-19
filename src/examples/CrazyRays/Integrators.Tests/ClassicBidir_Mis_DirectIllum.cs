using Xunit;
using Integrators;
using System;
using System.Collections.Generic;
using GroundWrapper;
using GroundWrapper.GroundMath;
using System.Numerics;

namespace Integrators.Tests {
    public class ClassicBidir_Mis_DirectIllum {

        Helpers.MisDummyPath dummyPath = new Helpers.MisDummyPath(
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
            var computer = new ClassicBidirMisComputer (
                lightPathCache: dummyPath.pathCache,
                numLightPaths: dummyPath.numLightPaths
            );

            var cameraPath = new CameraPath {
                vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..2])
            };

            float pdfReverse = dummyPath.cameraVertices[^2].pdfToAncestor;
            // Set a guard value to make sure that the correct pdf is used!
            var dummyVert = cameraPath.vertices[^1];
            dummyVert.pdfToAncestor = -1000.0f;
            cameraPath.vertices[^1] = dummyVert;

            return computer.NextEvent(cameraPath,
                pdfEmit: dummyPath.pathCache[1].pdfFromAncestor,
                pdfNextEvent: 1.0f / dummyPath.lightArea,
                pdfHit: dummyPath.cameraVertices[2].pdfFromAncestor,
                pdfReverse: pdfReverse);
        }

        float LightTracerWeight() {
            var computer = new ClassicBidirMisComputer (
                lightPathCache: dummyPath.pathCache,
                numLightPaths: dummyPath.numLightPaths
            );

            return computer.LightTracer(dummyPath.pathCache[dummyPath.lightEndpointIdx],
                pdfCamToPrimary: dummyPath.cameraVertices[1].pdfFromAncestor,
                pdfReverse: dummyPath.pathCache[dummyPath.lightEndpointIdx].pdfToAncestor);
        }

        float HitWeight() {
            var computer = new ClassicBidirMisComputer (
                lightPathCache: dummyPath.pathCache,
                numLightPaths: 500
            );

            var cameraPath = new CameraPath {
                vertices = new List<PathPdfPair>(dummyPath.cameraVertices[1..3])
            };

            return computer.Hit(cameraPath,
                pdfEmit: dummyPath.pathCache[1].pdfFromAncestor,
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
            float pdfHit = verts[1].pdfFromAncestor * verts[2].pdfFromAncestor;
            float pdfNextEvt = verts[1].pdfFromAncestor * (1.0f / dummyPath.lightArea);

            var lightVerts = dummyPath.pathCache;
            float pdfLightTracer = lightVerts[1].pdfFromAncestor * dummyPath.numLightPaths;

            float pdfSum = pdfHit + pdfNextEvt + pdfLightTracer;

            float expectedWeightNextEvt = pdfNextEvt / pdfSum;
            float expectedWeightHit = pdfHit / pdfSum;
            float expectedWeightLightTracer = pdfLightTracer / pdfSum;

            Assert.Equal(weightNextEvt, expectedWeightNextEvt, 3);
            Assert.Equal(weightBsdf, expectedWeightHit, 3);
            Assert.Equal(weightLightTracer, expectedWeightLightTracer, 3);
        }
    }
}
