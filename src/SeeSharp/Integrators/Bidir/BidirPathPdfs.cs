using SeeSharp.Integrators.Common;
using System;
using static SeeSharp.Integrators.Bidir.BidirBase;

namespace SeeSharp.Integrators {
    /// <summary>
    /// Assembles the pdf values in two arrays. The elements of each array
    /// correspond to the pdf values of sampling each vertex along the path.
    /// [0] is the primary vertex after the camera
    /// [numPdfs] is the last vertex, the one on the light source itself.
    /// </summary>
    public ref struct BidirPathPdfs {
        public readonly PathCache lightPathCache;

        public readonly Span<float> pdfsLightToCamera;
        public readonly Span<float> pdfsCameraToLight;

        public BidirPathPdfs(PathCache cache, int numPdfs) {
            pdfsCameraToLight = new float[numPdfs];
            pdfsLightToCamera = new float[numPdfs];
            lightPathCache = cache;
        }

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
}
