using Integrators.Common;
using System;
using static Integrators.Bidir.BidirBase;

namespace Integrators {
    ref struct PathPdfs {
        public PathCache lightPathCache;

        // Assemble the pdf values in two arrays. The elements of each array
        // correspond to the pdf values of sampling each vertex along the path.
        // [0] is the primary vertex after the camera
        // [numPdfs] is the last vertex, the one on the light source itself.
        public Span<float> pdfsLightToCamera;
        public Span<float> pdfsCameraToLight;

        public void GatherCameraPdfs(CameraPath cameraPath, int lastCameraVertexIdx) {
            // Gather the pdf values along the camera sub-path
            for (int i = 0; i < lastCameraVertexIdx; ++i) {
                pdfsCameraToLight[i] = cameraPath.vertices[i].pdfFromAncestor;
                if (i < lastCameraVertexIdx - 1)
                    pdfsLightToCamera[i] = cameraPath.vertices[i + 1].pdfToAncestor;
            }
        }

        public void GatherLightPdfs(PathVertex lightVertex, int lastCameraVertexIdx, int numPdfs) {
            pdfsLightToCamera[lastCameraVertexIdx + 1] = lightVertex.pdfFromAncestor;
            var nextVert = lightPathCache[lightVertex.ancestorId];
            for (int i = lastCameraVertexIdx + 2; i < numPdfs - 1; ++i) {
                pdfsLightToCamera[i] = nextVert.pdfFromAncestor;
                pdfsCameraToLight[i + 1] = nextVert.pdfToAncestor;

                nextVert = lightPathCache[nextVert.ancestorId];
            }
            pdfsLightToCamera[^1] = 1;
        }
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
            int lastCameraVertexIdx = -1;

            var pathPdfs = new PathPdfs {
                pdfsCameraToLight = stackalloc float[numPdfs],
                pdfsLightToCamera = stackalloc float[numPdfs],
                lightPathCache = lightPathCache
            };

            pathPdfs.pdfsCameraToLight[0] = pdfCamToPrimary;
            pathPdfs.pdfsCameraToLight[1] = pdfReverse;

            // Iterate over the light path and gather the pdfs
            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            // Compute the actual weight
            float sumReciprocals = LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs);
            sumReciprocals /= numLightPaths;
            sumReciprocals += 1;
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
            int lastCameraVertexIdx = numPdfs - 2; // TODO ?? why only -1 here?

            var pathPdfs = new PathPdfs {
                pdfsCameraToLight = stackalloc float[numPdfs],
                pdfsLightToCamera = stackalloc float[numPdfs],
                lightPathCache = lightPathCache
            };

            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);

            pathPdfs.pdfsCameraToLight[^2] = cameraPath.vertices[^1].pdfFromAncestor;
            pathPdfs.pdfsLightToCamera[^2] = pdfEmit;
            if (numPdfs > 2) // not for direct illumination
                pathPdfs.pdfsLightToCamera[^3] = pdfReverse;

            // Compute the actual weight
            float sumReciprocals = 1.0f;

            // Hitting the light source
            sumReciprocals += pdfHit / pdfNextEvent;

            // All bidirectional connections
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs) / pdfNextEvent;

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
            int lastCameraVertexIdx = numPdfs - 1;

            if (numPdfs == 1) return 1.0f; // sole technique for rendering directly visible lights.

            var pathPdfs = new PathPdfs {
                pdfsCameraToLight = stackalloc float[numPdfs],
                pdfsLightToCamera = stackalloc float[numPdfs],
                lightPathCache = lightPathCache
            };

            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);
            pathPdfs.pdfsLightToCamera[^2] = pdfEmit;

            float pdfThis = cameraPath.vertices[^1].pdfFromAncestor;

            // Compute the actual weight
            float sumReciprocals = 1.0f;

            // Next event estimation
            sumReciprocals += pdfNextEvent / pdfThis;

            // All connections along the camera path
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx - 1, pathPdfs) / pdfThis;

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

            var pathPdfs = new PathPdfs {
                pdfsCameraToLight = stackalloc float[numPdfs],
                pdfsLightToCamera = stackalloc float[numPdfs],
                lightPathCache = lightPathCache
            };

            pathPdfs.GatherCameraPdfs(cameraPath, lastCameraVertexIdx);
            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            // Set the pdf values that are unique to this combination of paths
            if (lastCameraVertexIdx > 0) // only if this is not the primary hit point
                pathPdfs.pdfsLightToCamera[lastCameraVertexIdx - 1] = pdfCameraReverse;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx] = cameraPath.vertices[^1].pdfFromAncestor;
            pathPdfs.pdfsLightToCamera[lastCameraVertexIdx] = pdfLightToCamera;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx + 1] = pdfCameraToLight;
            pathPdfs.pdfsCameraToLight[lastCameraVertexIdx + 2] = pdfLightReverse;

            // Compute reciprocals for hypothetical connections along the camera sub-path
            float sumReciprocals = 1.0f;
            sumReciprocals += CameraPathReciprocals(lastCameraVertexIdx, pathPdfs);
            sumReciprocals += LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs);

            return 1 / sumReciprocals;
        }

        private float CameraPathReciprocals(int lastCameraVertexIdx, PathPdfs pdfs) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx; i > 0; --i) { // all bidir connections
                nextReciprocal *= pdfs.pdfsLightToCamera[i] / pdfs.pdfsCameraToLight[i];
                sumReciprocals += nextReciprocal;
            }
            // Light tracer
            sumReciprocals += nextReciprocal * pdfs.pdfsLightToCamera[0] / pdfs.pdfsCameraToLight[0] * numLightPaths;
            return sumReciprocals;
        }

        private float LightPathReciprocals(int lastCameraVertexIdx, int numPdfs, PathPdfs pdfs) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx + 1; i < numPdfs; ++i) {
                nextReciprocal *= pdfs.pdfsCameraToLight[i] / pdfs.pdfsLightToCamera[i];
                if (i < numPdfs - 2) // Connections to the emitter (next event) are treated separately
                    sumReciprocals += nextReciprocal;
            }
            sumReciprocals += nextReciprocal; // Next event and hitting the emitter directly
            return sumReciprocals;
        }
    }
}
