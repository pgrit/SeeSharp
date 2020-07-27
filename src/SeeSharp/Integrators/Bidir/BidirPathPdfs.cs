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
                pdfsCameraToLight[i] = cameraPath.Vertices[i].PdfFromAncestor;
                if (i < lastCameraVertexIdx - 1)
                    pdfsLightToCamera[i] = cameraPath.Vertices[i + 1].PdfToAncestor;
            }
        }

        public void GatherLightPdfs(PathVertex lightVertex, int lastCameraVertexIdx, int numPdfs) {
            var nextVert = lightVertex;
            for (int i = lastCameraVertexIdx + 1; i < numPdfs - 2; ++i) {
                pdfsLightToCamera[i] = nextVert.PdfFromAncestor;
                pdfsCameraToLight[i + 2] = nextVert.PdfReverseAncestor;
                nextVert = lightPathCache[nextVert.AncestorId];
            }
            pdfsLightToCamera[^2] = nextVert.PdfFromAncestor;
            pdfsLightToCamera[^1] = 1;
        }
    }
}
