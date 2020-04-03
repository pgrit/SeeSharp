using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Ground
{
    public class Scene {
        public Image frameBuffer {
            get; private set;
        }

        public void SetupFrameBuffer(uint width, uint height) {
            this.frameBuffer = new Image(width, height);
        }

        public void LoadSceneFile(string filename) {
            Debug.Assert(this.frameBuffer != null,
                "Framebuffer needs to be initialized before loading the scene");

            InitScene();
            bool b = LoadSceneFromFile(filename, this.frameBuffer.id);
            FinalizeScene();

            FindEmitters();
        }

        public (Ray, Vector2) SampleCamera(uint row, uint col, float u, float v) {
            CameraSampleInfo camSample = new CameraSampleInfo() {
                filmSample = new Vector2 {
                    x = col + u,
                    y = row + v
                }
            };
            // TODO support multiple cameras, specified by id here
            var ray = GenerateCameraRay(0, camSample);
            return (ray, camSample.filmSample);
        }

        public Hit TraceRay(Ray ray) {
            return TraceSingle(ray);
        }

        public bool IsOccluded(Hit from, Vector3 to) {
            return IsOccluded(ref from, to);
        }

        private void FindEmitters() {
            int numEmitters = GetNumberEmitters();
            for (int i = 0; i < numEmitters; i++) {
                emitters.Add(new Emitter(GetEmitterMesh(i)));
            }
        }

        public Emitter QueryEmitter(SurfacePoint point) {
            // TODO implement a mesh to emitter map
            if (point.meshId == Emitters[0].MeshId)
                return Emitters[0];
            return null;
        }

        public List<Emitter> Emitters {
            get => emitters;
        }

        public Ray SpawnRay(Hit from, Vector3 direction) {
            return SpawnRay(ref from, direction);
        }

        public ColorRGB EvaluateBsdf(SurfacePoint point,
            Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
        {
            return EvaluateBsdf(ref point, outDir, inDir, isOnLightSubpath);
        }

        public BsdfSample WrapPrimarySampleToBsdf(
            SurfacePoint point, Vector3 outDir,
            float u, float v, bool isOnLightSubpath)
        {
            return WrapPrimarySampleToBsdf(ref point, outDir, u, v, isOnLightSubpath);
        }

        public BsdfSample ComputePrimaryToBsdfJacobian(
            SurfacePoint point, Vector3 outDir, Vector3 inDir,
            bool isOnLightSubpath)
        {
            return ComputePrimaryToBsdfJacobian(ref point, outDir, inDir,
                isOnLightSubpath);
        }

        public GeometryTerms ComputeGeometryTerms(SurfacePoint from, SurfacePoint to) {
            return ComputeGeometryTerms(ref from, ref to);
        }

        private List<Emitter> emitters = new List<Emitter>();

#region C-API-IMPORTS
        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern void InitScene();

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern void FinalizeScene();

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        [return:MarshalAs(UnmanagedType.I1)]
        private static extern bool LoadSceneFromFile([In] string filename,
            int frameBufferId);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern int GetNumberEmitters();

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern int GetEmitterMesh(int id);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern Ray GenerateCameraRay(int camera,
            CameraSampleInfo sampleInfo);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern Hit TraceSingle(Ray ray);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        [return:MarshalAs(UnmanagedType.I1)]
        private static extern bool IsOccluded([In] ref Hit from, Vector3 to);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern Ray SpawnRay([In] ref Hit from, Vector3 direction);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern ColorRGB EvaluateBsdf([In] ref SurfacePoint point,
            Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern BsdfSample WrapPrimarySampleToBsdf(
            [In] ref SurfacePoint point, Vector3 outDir,
            float u, float v, bool isOnLightSubpath);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern BsdfSample ComputePrimaryToBsdfJacobian(
            [In] ref SurfacePoint point, Vector3 outDir, Vector3 inDir,
            bool isOnLightSubpath);

        [DllImport("Ground", CallingConvention=CallingConvention.Cdecl)]
        private static extern GeometryTerms ComputeGeometryTerms(
            [In] ref SurfacePoint from, [In] ref SurfacePoint to);

#endregion C-API-IMPORTS
    }
}