using GroundWrapper.Geometry;
using System.Collections.Generic;

namespace GroundWrapper {
    public class Scene {
        public Image FrameBuffer;
        public Cameras.Camera Camera;

        public List<Mesh> Meshes = new List<Mesh>();
        public Raytracer Raytracer { get; private set; }

        public List<Emitter> Emitters { get; private set; } = new List<Emitter>();

        public List<string> ValidationErrorMessages { get; private set; } = new List<string>();

        public static Scene LoadFromFile(string path) {
            return new Scene();
        }

        public void Prepare() {
            if (!IsValid)
                throw new System.InvalidOperationException("Cannot finalize an invalid scene.");

            // Prepare the scene geometry for ray tracing.
            Raytracer = new Raytracer();
            for (int idx = 0; idx < Meshes.Count; ++idx) {
                Raytracer.AddMesh(Meshes[idx]);
            }
            Raytracer.CommitScene();

            // Make sure the camera is set for the correct resolution.
            Camera.UpdateFrameBuffer(FrameBuffer);

            // Build the mesh to emitter mapping
            meshToEmitter.Clear();
            foreach (var emitter in Emitters) {
                meshToEmitter.Add(emitter.Mesh, emitter);
            }
        }

        /// <summary>
        /// True, if the scene is valid. Any errors found whil accessing the property will be
        /// reported in the <see cref="ValidationErrorMessages"/> list.
        /// </summary>
        public bool IsValid {
            get {
                ValidationErrorMessages.Clear();

                if (FrameBuffer == null)
                    ValidationErrorMessages.Add("Framebuffer not set.");
                if (Camera == null)
                    ValidationErrorMessages.Add("Camera not set.");
                if (Meshes == null || Meshes.Count == 0)
                    ValidationErrorMessages.Add("No meshes in the scene.");

                int idx = 0;
                foreach (var m in Meshes) {
                    if (m.Material == null)
                        ValidationErrorMessages.Add($"Mesh[{idx}] does not have a material.");
                    idx++;
                }

                return ValidationErrorMessages.Count == 0;
            }
        }

        /// <summary>
        /// Returns the emitter attached to the mesh on which a <see cref="SurfacePoint"/> lies.
        /// </summary>
        /// <param name="point">A point on a mesh surface.</param>
        /// <returns>The attached emitter reference, or null.</returns>
        public Emitter QueryEmitter(SurfacePoint point) {
            Emitter emitter;
            if (!meshToEmitter.TryGetValue(point.mesh, out emitter))
                return null;
            return emitter;
        }

        Dictionary<Mesh, Emitter> meshToEmitter = new Dictionary<Mesh, Emitter>();
    }
}