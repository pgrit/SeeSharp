using GroundWrapper;
using GroundWrapper.Geometry;
using GroundWrapper.Sampling;
using GroundWrapper.Shading;
using GroundWrapper.Shading.Emitters;
using Integrators.Common;

namespace Integrators.Bidir {
    public class ClassicBidir : BidirBase {
        public override void Render(Scene scene) {
            // Classic Bidir requires exactly one light path for every camera path.
            NumLightPaths = scene.FrameBuffer.Width * scene.FrameBuffer.Height;
            base.Render(scene);
        }

        public override void ProcessPathCache() {
            SplatLightVertices();
        }

        public override ColorRGB OnCameraHit(CameraPath path, RNG rng, int pixelIndex, Ray ray, SurfacePoint hit,
                                             float pdfFromAncestor, float pdfToAncestor, ColorRGB throughput, 
                                             int depth, float toAncestorJacobian) {
            ColorRGB value = ColorRGB.Black;

            // Was a light hit?
            Emitter light = scene.QueryEmitter(hit);
            if (light != null) {
                value += throughput * OnEmitterHit(light, hit, ray, path, toAncestorJacobian);
            }

            // Perform connections if the maximum depth has not yet been reached
            if (depth < MaxDepth) {
                value += throughput * BidirConnections(pixelIndex, hit, -ray.direction, rng, path, toAncestorJacobian);
                value += throughput * PerformNextEventEstimation(ray, hit, rng, path, toAncestorJacobian);
            }

            return value;
        }

        public override float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent) {
            int numPdfs = cameraPath.vertices.Count;
            int lastCameraVertexIdx = numPdfs - 1;

            if (numPdfs == 1) return 1.0f; // sole technique for rendering directly visible lights.

            var pathPdfs = new BidirPathPdfs(lightPaths.pathCache, numPdfs);
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

        public override float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse) {
            int numPdfs = lightVertex.depth + 1;
            int lastCameraVertexIdx = -1;

            var pathPdfs = new BidirPathPdfs(lightPaths.pathCache, numPdfs);

            pathPdfs.GatherLightPdfs(lightVertex, lastCameraVertexIdx, numPdfs);

            pathPdfs.pdfsCameraToLight[0] = pdfCamToPrimary;
            pathPdfs.pdfsCameraToLight[1] = pdfReverse;

            // Compute the actual weight
            float sumReciprocals = LightPathReciprocals(lastCameraVertexIdx, numPdfs, pathPdfs);
            sumReciprocals /= NumLightPaths;
            sumReciprocals += 1;
            return 1 / sumReciprocals;
        }

        public override float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                              float pdfCameraToLight, float pdfLightReverse, float pdfLightToCamera) {
            int numPdfs = cameraPath.vertices.Count + lightVertex.depth + 1;
            int lastCameraVertexIdx = cameraPath.vertices.Count - 1;

            var pathPdfs = new BidirPathPdfs(lightPaths.pathCache, numPdfs);
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
        
        public override float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse) {
            int numPdfs = cameraPath.vertices.Count + 1;
            int lastCameraVertexIdx = numPdfs - 2; // TODO ?? why only -1 here?

            var pathPdfs = new BidirPathPdfs(lightPaths.pathCache, numPdfs);

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

        private float CameraPathReciprocals(int lastCameraVertexIdx, BidirPathPdfs pdfs) {
            float sumReciprocals = 0.0f;
            float nextReciprocal = 1.0f;
            for (int i = lastCameraVertexIdx; i > 0; --i) { // all bidir connections
                nextReciprocal *= pdfs.pdfsLightToCamera[i] / pdfs.pdfsCameraToLight[i];
                sumReciprocals += nextReciprocal;
            }
            // Light tracer
            sumReciprocals += nextReciprocal * pdfs.pdfsLightToCamera[0] / pdfs.pdfsCameraToLight[0] * NumLightPaths;
            return sumReciprocals;
        }

        private float LightPathReciprocals(int lastCameraVertexIdx, int numPdfs, BidirPathPdfs pdfs) {
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
