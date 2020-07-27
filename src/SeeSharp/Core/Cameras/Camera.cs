using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using System.Numerics;

namespace SeeSharp.Core.Cameras {
    public abstract class Camera {
        public Vector3 Position => Vector3.Transform(new Vector3(0, 0, 0), cameraToWorld);
        public Vector3 Direction => Vector3.Transform(new Vector3(0, 0, -1), cameraToWorld);

        /// <exception cref="System.ArgumentException">If the world to camera transform is not invertible.</exception>
        public Camera(Matrix4x4 worldToCamera) {
            this.worldToCamera = worldToCamera;
            var succ = Matrix4x4.Invert(worldToCamera, out cameraToWorld);
            if (!succ)
                throw new System.ArgumentException("World to camera transform must be invertible.", "worldToCamera");
        }

        public abstract void UpdateFrameBuffer(Image.FrameBuffer value);

        public abstract (Ray, float, ColorRGB, SurfacePoint) GenerateRay(Vector2 filmPos);

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
        /// <param name="dir">A direction (from the camera to a point).</param>
        /// <returns>The jacobian.</returns>
        public abstract float SolidAngleToPixelJacobian(Vector3 dir);

        public abstract float SurfaceAreaToSolidAngleJacobian(Vector3 point, Vector3 normal);

        protected Matrix4x4 worldToCamera;
        protected Matrix4x4 cameraToWorld;
    }
}
