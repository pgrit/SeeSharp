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
        public readonly PathCache LightPathCache;

        public readonly Span<float> PdfsLightToCamera;
        public readonly Span<float> PdfsCameraToLight;

        public BidirPathPdfs(PathCache cache, Span<float> lightToCam, Span<float> camToLight) {
            PdfsCameraToLight = camToLight;
            PdfsLightToCamera = lightToCam;
            LightPathCache = cache;
        }

        public void GatherCameraPdfs(CameraPath cameraPath, int lastCameraVertexIdx) {
            // Gather the pdf values along the camera sub-path
            for (int i = 0; i < lastCameraVertexIdx; ++i) {
                PdfsCameraToLight[i] = cameraPath.Vertices[i].PdfFromAncestor;
                if (i < lastCameraVertexIdx - 1)
                    PdfsLightToCamera[i] = cameraPath.Vertices[i + 1].PdfToAncestor;
            }
        }

        public void GatherLightPdfs(PathVertex lightVertex, int lastCameraVertexIdx, int numPdfs) {
            var nextVert = lightVertex;
            for (int i = lastCameraVertexIdx + 1; i < numPdfs - 2; ++i) {
                PdfsLightToCamera[i] = nextVert.PdfFromAncestor;
                PdfsCameraToLight[i + 2] = nextVert.PdfReverseAncestor + nextVert.PdfNextEventAncestor;
                nextVert = LightPathCache[nextVert.PathId, nextVert.AncestorId];
            }
            PdfsLightToCamera[^2] = nextVert.PdfFromAncestor;
            PdfsLightToCamera[^1] = 1;
        }
    }
}
