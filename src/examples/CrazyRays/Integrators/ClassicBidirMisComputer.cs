using Integrators.Common;
using System;
using System.Collections.Generic;

namespace Integrators {
    public struct PathPdfPair {
        public float pdfFromAncestor;
        public float pdfToAncestor;
    }
    public struct CameraPath {
        public List<PathPdfPair> vertices;
    }

    public readonly ref struct ClassicBidirMisComputer {
        public readonly int numLightPaths;
        public readonly PathCache lightPathCache;

        public ClassicBidirMisComputer(int numLightPaths, PathCache lightPathCache) {
            this.numLightPaths = numLightPaths;
            this.lightPathCache = lightPathCache;
        }

        /// <summary>
        /// Computes the balance heuristic value for the light tracer technique
        /// (splatting a light path vertex onto the image).
        /// </summary>
        /// <param name="lightVertex">The vertex that got splatte</param>
        /// <param name="pdfCamToPrimary">
        ///     The pdf value of sampling the light vertex as the primary vertex of a camera path.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <param name="pdfReverse">
        ///     The pdf value of sampling the ancestor of the light vertex when coming from the camera.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <returns>Balance heuristic weight</returns>
        public float LightTracer(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse) {
            int numPdfs = lightVertex.depth + 1;

            // Assemble the pdf values in two arrays. The elements of each array
            // correspond to the pdf values of sampling each vertex along the path.
            // [0] is the primary vertex after the camera
            // [numPdfs] is the last vertex, the one on the light source itself.
            Span<float> pdfsLightToCamera = stackalloc float[numPdfs];
            Span<float> pdfsCameraToLight = stackalloc float[numPdfs];

            pdfsCameraToLight[0] = pdfCamToPrimary;
            pdfsCameraToLight[1] = pdfReverse;
            pdfsLightToCamera[0] = lightVertex.pdfFromAncestor;

            // Iterate over the light path and gather the pdfs
            var nextVert = lightPathCache[lightVertex.ancestorId];
            for (int i = 1; i < numPdfs - 1; ++i) {
                pdfsLightToCamera[i] = nextVert.pdfFromAncestor;
                pdfsCameraToLight[i + 1] = nextVert.pdfToAncestor;

                nextVert = lightPathCache[nextVert.ancestorId];
            }

            pdfsLightToCamera[^1] = 1;//nextVert.pdfFromAncestor;

            // Compute the actual weight
            float sumReciprocals = 1.0f;
            float nextReciprocal = 1.0f / numLightPaths;
            for (int i = 0; i < numPdfs; ++i) {
                nextReciprocal *= pdfsCameraToLight[i] / pdfsLightToCamera[i];
                if (i < numPdfs - 2) // Connections to the emitter (next event) are treated separately
                    sumReciprocals += nextReciprocal;
            }
            sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly

            return 1 / sumReciprocals;
        }

        /// <summary>
        /// Computes the balance heuristic value for the next event technique.
        /// </summary>
        /// <param name="cameraPath">The camera path at the end of which next event estimation happened.</param>
        /// <param name="pdfEmit">
        ///     The probability of sampling the next event edge bidirectionally. 
        ///     Measure: product surface area [m^4]
        /// </param>
        /// <param name="pdfNextEvent">
        ///     The probability of sampling the vertex on the light for next event estimation (current technique). 
        ///     Measure: surface area [m^2]
        /// </param>
        /// <param name="pdfHit">
        ///     The probability of sampling the vertex on the light by importance sampling the BSDF.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <param name="pdfReverse">
        ///     The probability of sampling the previous vertex along the camera path, when coming from the light.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <returns>Balance heuristic weight</returns>
        public float NextEvent(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse) {
            int numPdfs = cameraPath.vertices.Count + 1;

            // Assemble the pdf values in two arrays. The elements of each array
            // correspond to the pdf values of sampling each vertex along the path.
            // [0] is the primary vertex after the camera
            // [numPdfs] is the last vertex, the one on the light source itself.
            Span<float> pdfsLightToCamera = stackalloc float[numPdfs];
            Span<float> pdfsCameraToLight = stackalloc float[numPdfs];

            for (int i = 0; i < numPdfs - 2; ++i) {
                pdfsCameraToLight[i] = cameraPath.vertices[i].pdfFromAncestor;
                pdfsLightToCamera[i] = cameraPath.vertices[i + 1].pdfToAncestor;
            }
            pdfsCameraToLight[^2] = cameraPath.vertices[^1].pdfFromAncestor;
            pdfsLightToCamera[^2] = pdfEmit;
            if (numPdfs > 2) // not for direct illumination
                pdfsLightToCamera[^3] = pdfReverse;

            // Compute the actual weight
            float sumReciprocals = 1.0f;

            // Hitting the light source
            sumReciprocals += pdfHit / pdfNextEvent;

            // All bidirectional connections
            float nextReciprocal = 1 / pdfNextEvent;
            for (int i = numPdfs - 2; i > 0; --i) {
                nextReciprocal *= pdfsLightToCamera[i] / pdfsCameraToLight[i];
                sumReciprocals += nextReciprocal;
            }

            // Light tracer
            sumReciprocals += nextReciprocal * numLightPaths * pdfsLightToCamera[0] / pdfsCameraToLight[0];

            return 1 / sumReciprocals;
        }

        /// <summary>
        /// Computes the balance heuristic value for hitting the light with a BSDF sample.
        /// </summary>
        /// <param name="cameraPath">The camera path, the last vertex of which found the light</param>
        /// <param name="pdfEmit">
        ///     The pdf value of sampling the vertex on the light and the previous one bidirectionally.
        ///     Measure: product surface area [m^4]
        /// </param>
        /// <param name="pdfNextEvent">
        ///     The pdf value of sampling the vertex on the light via next event estimation.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <returns>Balance heuristic weight</returns>
        public float Hit(CameraPath cameraPath, float pdfEmit, float pdfNextEvent) {
            int numPdfs = cameraPath.vertices.Count;

            if (numPdfs == 1) return 1.0f; // sole technique for rendering directly visible lights.

            // Assemble the pdf values in two arrays. The elements of each array
            // correspond to the pdf values of sampling each vertex along the path.
            // [0] is the primary vertex after the camera
            // [numPdfs] is the last vertex, the one on the light source itself.
            Span<float> pdfsLightToCamera = stackalloc float[numPdfs];
            Span<float> pdfsCameraToLight = stackalloc float[numPdfs];

            for (int i = 0; i < numPdfs - 2; ++i) {
                pdfsCameraToLight[i] = cameraPath.vertices[i].pdfFromAncestor;
                pdfsLightToCamera[i] = cameraPath.vertices[i + 1].pdfToAncestor;
            }
            pdfsCameraToLight[^2] = cameraPath.vertices[^2].pdfFromAncestor;
            pdfsLightToCamera[^2] = pdfEmit;

            float pdfThis = cameraPath.vertices[^1].pdfFromAncestor;

            // Compute the actual weight
            float sumReciprocals = 1.0f;

            // Next event estimation
            sumReciprocals += pdfNextEvent / pdfThis;

            // All connections along the camera path
            float nextReciprocal = 1.0f / pdfThis;
            for (int i = numPdfs - 2; i > 0; --i) {
                nextReciprocal *= pdfsLightToCamera[i] / pdfsCameraToLight[i];
                sumReciprocals += nextReciprocal;
            }

            // Light tracer
            sumReciprocals += nextReciprocal * pdfsLightToCamera[0] / pdfsCameraToLight[0] * numLightPaths;

            return 1 / sumReciprocals;
        }

        /// <summary>
        /// Computes the balance heuristic value for an "inner" connection.
        /// </summary>
        /// <param name="cameraPath">The camera path the endpoint of which is connected to a light path vertex.</param>
        /// <param name="lightVertex">The light path vertex we are connecting to.</param>
        /// <param name="pdfCameraReverse">
        ///     The pdf value of sampling the previous vertex on the camera path, when coming from the light.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <param name="pdfCameraToLight">
        ///     The pdf value of sampling the connection by continuing the camera path instead (usually via BSDF importance sampling).
        ///     Measure: surface area [m^2]
        /// </param>
        /// <param name="pdfLightReverse">
        ///     The pdf value of sampling the previous vertex on the light sub-path, when coming from the camera.
        ///     Measure: surface area [m^2]
        /// </param>
        /// <param name="pdfLightToCamera">
        ///     The pdf value of sampling the connection by continuing the light sub-path instead (usually via BSDF importance sampling).
        ///     Measure: surface area [m^2]
        /// </param>
        /// <returns>The balance heuristic weight.</returns>
        public float BidirConnect(CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                  float pdfCameraToLight, float pdfLightReverse, float pdfLightToCamera) {
            int numPdfs = cameraPath.vertices.Count + lightVertex.depth + 1;
            int lastCameraVertexIdx = cameraPath.vertices.Count - 1;

            // Assemble the pdf values in two arrays. The elements of each array
            // correspond to the pdf values of sampling each vertex along the path.
            // [0] is the primary vertex after the camera
            // [numPdfs] is the last vertex, the one on the light source itself.
            Span<float> pdfsLightToCamera = stackalloc float[numPdfs];
            Span<float> pdfsCameraToLight = stackalloc float[numPdfs];

            // Gather the pdf values along the camera sub-path
            for (int i = 0; i < lastCameraVertexIdx; ++i) {
                pdfsCameraToLight[i] = cameraPath.vertices[i].pdfFromAncestor;
                if (i < lastCameraVertexIdx - 1)
                    pdfsLightToCamera[i] = cameraPath.vertices[i + 1].pdfToAncestor;
            }

            // Gather the pdf values along the light sub-path
            pdfsLightToCamera[lastCameraVertexIdx + 1] = lightVertex.pdfFromAncestor;
            var nextVert = lightPathCache[lightVertex.ancestorId];
            for (int i = lastCameraVertexIdx + 2; i < numPdfs - 1; ++i) {
                pdfsLightToCamera[i] = nextVert.pdfFromAncestor;
                pdfsCameraToLight[i + 1] = nextVert.pdfToAncestor;

                nextVert = lightPathCache[nextVert.ancestorId];
            }
            pdfsLightToCamera[^1] = 1;//nextVert.pdfFromAncestor;

            // Set the pdf values that are unique to this combination of paths
            if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
                pdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
            pdfsCameraToLight[lastCameraVertexIdx] = cameraPath.vertices[lastCameraVertexIdx].pdfFromAncestor;
            pdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
            pdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
            pdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse;

            // Compute reciprocals for hypothetical connections along the camera sub-path
            float sumReciprocals = 1.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx; i > 0; --i) { // all bidir connections
                nextReciprocal *= pdfsLightToCamera[i] / pdfsCameraToLight[i];
                sumReciprocals += nextReciprocal;
            }
            // Light tracer
            float lightTracerReciprocal = pdfsLightToCamera[0] / pdfsCameraToLight[0] * numLightPaths;
            sumReciprocals += lightTracerReciprocal;

            // Compute the reciprocals for hypothetical connections along the light sub-path
            nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx + 1; i < numPdfs; ++i) {
                nextReciprocal *= pdfsCameraToLight[i] / pdfsLightToCamera[i];
                if (i < numPdfs - 2) // Connections to the emitter (next event) are treated separately
                    sumReciprocals += nextReciprocal;
            }
            sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly

            return 1 / sumReciprocals;
        }
    }
}
