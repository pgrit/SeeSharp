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
                value += throughput * BidirConnections(pixelIndex, hit, -ray.direction, path, toAncestorJacobian);
                value += throughput * PerformNextEventEstimation(ray, hit, rng, path, toAncestorJacobian);
            }

            return value;
        }

        public override float EmitterHitMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent) {
            // MIS weight
            var computer = new ClassicBidirMisComputer(
                lightPathCache: lightPaths.pathCache,
                numLightPaths: NumLightPaths
            );
            float misWeight = computer.Hit(cameraPath, pdfEmit, pdfNextEvent);
            return misWeight;
        }

        public override float LightTracerMis(PathVertex lightVertex, float pdfCamToPrimary, float pdfReverse) {
            // Compute MIS weight
            var computer = new ClassicBidirMisComputer(
                    lightPathCache: lightPaths.pathCache,
                    numLightPaths: NumLightPaths
                );
            float misWeight = computer.LightTracer(lightVertex, pdfCamToPrimary, pdfReverse);
            return misWeight;
        }

        public override float BidirConnectMis(CameraPath cameraPath, PathVertex lightVertex, float pdfCameraReverse,
                                              float pdfCameraToLight, float pdfLightReverse, float pdfLightToCamera) {
            // Compute the MIS weight
            var computer = new ClassicBidirMisComputer(
                    lightPathCache: lightPaths.pathCache,
                    numLightPaths: NumLightPaths
                );
            float misWeight = computer.BidirConnect(cameraPath, lightVertex, pdfCameraReverse, pdfCameraToLight, pdfLightReverse, pdfLightToCamera);
            return misWeight;
        }

        
        public override float NextEventMis(CameraPath cameraPath, float pdfEmit, float pdfNextEvent, float pdfHit, float pdfReverse) {
            var computer = new ClassicBidirMisComputer(
                    lightPathCache: lightPaths.pathCache,
                    numLightPaths: NumLightPaths
                );
            float misWeight = computer.NextEvent(cameraPath, pdfEmit, pdfNextEvent, pdfHit, pdfReverse);
            return misWeight;
        }
    }
}
