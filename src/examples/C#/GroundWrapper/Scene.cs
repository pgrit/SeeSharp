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

            CApiImports.InitScene();

            bool loaded = CApiImports.LoadSceneFromFile(filename, this.frameBuffer.id);
            Debug.Assert(loaded, "Error loading the scene.");

            CApiImports.FinalizeScene();

            FindEmitters();
        }

        public Vector3 CameraPosition {
            get {
                if (cameraPosition.HasValue)
                    return cameraPosition.Value;

                cameraPosition = SampleCamera(0, 0, 0.0f, 0.0f).Item1.origin;
                return cameraPosition.Value;
            }
        }

        private Vector3? cameraPosition;

        public (Ray, Vector2) SampleCamera(uint row, uint col, float u, float v) {
            CameraSampleInfo camSample = new CameraSampleInfo() {
                filmSample = new Vector2 {
                    x = col + u,
                    y = row + v
                }
            };
            // TODO support multiple cameras, specified by id here
            var ray = CApiImports.GenerateCameraRay(0, camSample);
            return (ray, camSample.filmSample);
        }

        /// <summary>
        /// Projects the given world space coordinates onto the active camera's film.
        /// </summary>
        /// <param name="worldPos">A position in world space to project.</param>
        /// <returns>The (possibly imaginary) raster position and a bool indicating whether the point is within the view frustum.</returns>
        public (Vector2, bool) ProjectOntoFilm(Vector3 worldPos) {
            // TODO support multiple cameras, specified by id here
            Vector3 projected = CApiImports.MapWorldSpaceToCameraFilm(0, worldPos);

            bool isOutside =  projected.x < 0 || projected.x > frameBuffer.width  ||
                projected.y < 0 || projected.y > frameBuffer.height ||
                projected.z < 0;

            return (new Vector2{ x=projected.x, y=projected.y}, !isOutside);
        }

        public float ComputeCamaraSolidAngleToPixelJacobian(Vector3 worldPos) {
            // TODO support multiple cameras, specified by id here
            return CApiImports.ComputeSolidAngleToPixelJacobian(0, worldPos);
        }

        public Hit TraceRay(Ray ray) {
            return CApiImports.TraceSingle(ray);
        }

        public bool IsValid(Hit hit) {
            return hit.point.meshId < uint.MaxValue;
        }

        public bool IsOccluded(SurfacePoint from, Vector3 to) {
            return CApiImports.IsOccluded(ref from, to);
        }

        private void FindEmitters() {
            int numEmitters = CApiImports.GetNumberEmitters();
            for (int i = 0; i < numEmitters; i++) {
                Emitters.Add(new Emitter(CApiImports.GetEmitterMesh(i), i));
            }
        }

        public Emitter QueryEmitter(SurfacePoint point) {
            // TODO implement a mesh to emitter map
            if (point.meshId == Emitters[0].MeshId)
                return Emitters[0];
            return null;
        }

        public List<Emitter> Emitters { get; } = new List<Emitter>();

        public Ray SpawnRay(SurfacePoint from, Vector3 direction) {
            return CApiImports.SpawnRay(from, direction);
        }

        public (ColorRGB, float) EvaluateBsdf(SurfacePoint point,
            Vector3 outDir, Vector3 inDir, bool isOnLightSubpath)
        {
            var bsdfValue = CApiImports.EvaluateBsdf(ref point, outDir, inDir, isOnLightSubpath);
            float shadingCosine = CApiImports.ComputeShadingCosine(ref point, outDir, inDir, isOnLightSubpath);
            return (bsdfValue, shadingCosine);
        }

        public BsdfSample WrapPrimarySampleToBsdf(
            SurfacePoint point, Vector3 outDir,
            float u, float v, bool isOnLightSubpath)
        {
            return CApiImports.WrapPrimarySampleToBsdf(ref point, outDir, u, v, isOnLightSubpath);
        }

        public BsdfSample ComputePrimaryToBsdfJacobian(
            SurfacePoint point, Vector3 outDir, Vector3 inDir,
            bool isOnLightSubpath)
        {
            return CApiImports.ComputePrimaryToBsdfJacobian(ref point, outDir, inDir,
                isOnLightSubpath);
        }

        public GeometryTerms ComputeGeometryTerms(SurfacePoint from, SurfacePoint to) {
            return CApiImports.ComputeGeometryTerms(ref from, ref to);
        }

        struct CApiImports {
            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern void InitScene();

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern void FinalizeScene();

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool LoadSceneFromFile([In] string filename,
                int frameBufferId);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern int GetNumberEmitters();

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern int GetEmitterMesh(int id);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern Ray GenerateCameraRay(int camera,
                CameraSampleInfo sampleInfo);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern Vector3 MapWorldSpaceToCameraFilm(int camera, Vector3 worldSpacePoint);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern float ComputeSolidAngleToPixelJacobian(int camera, Vector3 worldSpacePoint);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern Hit TraceSingle(Ray ray);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            public static extern bool IsOccluded([In] ref SurfacePoint from, Vector3 to);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern Ray SpawnRay(SurfacePoint from, Vector3 direction);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern ColorRGB EvaluateBsdf([In] ref SurfacePoint point,
                Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern float ComputeShadingCosine([In] ref SurfacePoint point,
                Vector3 outDir, Vector3 inDir, bool isOnLightSubpath);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern BsdfSample WrapPrimarySampleToBsdf(
                [In] ref SurfacePoint point, Vector3 outDir,
                float u, float v, bool isOnLightSubpath);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern BsdfSample ComputePrimaryToBsdfJacobian(
                [In] ref SurfacePoint point, Vector3 outDir, Vector3 inDir,
                bool isOnLightSubpath);

            [DllImport("Ground", CallingConvention = CallingConvention.Cdecl)]
            public static extern GeometryTerms ComputeGeometryTerms(
                [In] ref SurfacePoint from, [In] ref SurfacePoint to);
        }
    }
}