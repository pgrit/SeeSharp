using SeeSharp.Geometry;
using SeeSharp.Sampling;
using System.Numerics;

namespace SeeSharp.Cameras {
    public abstract class Camera {
        public Vector3 Position => Vector3.Transform(new Vector3(0, 0, 0), cameraToWorld);
        public Vector3 Direction => Vector3.Transform(new Vector3(0, 0, -1), cameraToWorld);

        /// <exception cref="System.ArgumentException">
        ///     If the world to camera transform is not invertible.
        /// </exception>
        public Camera(Matrix4x4 worldToCamera) {
            WorldToCamera = worldToCamera;
        }

        public Matrix4x4 WorldToCamera {
            get => worldToCamera;
            set {
                worldToCamera = value;
                var succ = Matrix4x4.Invert(worldToCamera, out cameraToWorld);
                if (!succ)
                    throw new System.ArgumentException("World to camera transform must be invertible.", "worldToCamera");
            }
        }

        public abstract void UpdateFrameBuffer(Image.FrameBuffer value);

        public abstract CameraRaySample GenerateRay(Vector2 filmPos, RNG rng);

        public abstract CameraResponseSample SampleResponse(SurfacePoint scenePoint, RNG rng);

        /// <summary>
        /// Projects the given world space point onto the camera.
        /// </summary>
        /// <param name="pos">A position in world space.</param>
        /// <returns>A 3D vector with the film coordinates in (x,y) and the distance from the camera in (z).</returns>
        public abstract Vector3? WorldToFilm(Vector3 pos);

        /// <summary>
        /// Computes the jacobian determinant for the change of variables from film to solid angle about the view direction.
        /// That is, the change of variables performed by the <see cref="GenerateRay(Vector2)"/> method.
        /// </summary>
        /// <param name="pos">A point in world space, visible to the camera.</param>
        /// <returns>The jacobian.</returns>
        public abstract float SolidAngleToPixelJacobian(Vector3 pos);

        public abstract float SurfaceAreaToSolidAngleJacobian(Vector3 point, Vector3 normal);

        protected Matrix4x4 worldToCamera;
        protected Matrix4x4 cameraToWorld;
    }
}
