using GroundWrapper.Geometry;
using System;
using System.Numerics;

namespace GroundWrapper.Cameras {
    public abstract class Camera {
        /// <exception cref="System.ArgumentException">If the world to camera transform is not invertible.</exception>
        public Camera(Matrix4x4 worldToCamera) {
            this.worldToCamera = worldToCamera;
            var succ = Matrix4x4.Invert(worldToCamera, out cameraToWorld);
            if (!succ)
                throw new System.ArgumentException("World to camera transform must be invertible.", "worldToCamera");
        }

        public abstract void UpdateFrameBuffer(Image value);

        public abstract Ray GenerateRay(Vector2 filmPos);

        /// <summary>
        /// Projects the given world space point onto the camera.
        /// </summary>
        /// <param name="pos">A position in world space.</param>
        /// <returns>A 3D vector with the film coordinates in (x,y) and the distance from the camera in (z).</returns>
        public abstract Vector3 WorldToFilm(Vector3 pos);

        /// <summary>
        /// Computes the jacobian determinant for the change of variables from film to solid angle about the view direction.
        /// That is, the change of variables performed by the <see cref="GenerateRay(Vector2)"/> method.
        /// </summary>
        /// <param name="dir">A direction (from the camera to a point).</param>
        /// <returns>The jacobian.</returns>
        public abstract float SolidAngleToPixelJacobian(Vector3 dir);

        protected Matrix4x4 worldToCamera;
        protected Matrix4x4 cameraToWorld;
    }

    public class PerspectiveCamera : Camera {
        public int Width { get => width; }
        public int Height { get => height; }

        /// <summary>
        /// Creates a new perspective camera.
        /// </summary>
        /// <param name="worldToCamera">
        /// Transformation from (right handed) world space to (right handed) camera space.
        /// The camera is centered at the origin, looking along negative Z. X is right, Y is up.
        /// </param>
        /// <param name="verticalFieldOfView">The full vertical opening angle in degrees.</param>
        /// <param name="frameBuffer">Frame buffer that will be used for rendering (only resolution is relevant).</param>
        public PerspectiveCamera(Matrix4x4 worldToCamera, float verticalFieldOfView, Image frameBuffer) : base(worldToCamera) {
            fovRadians = verticalFieldOfView * MathF.PI / 180;
            UpdateFrameBuffer(frameBuffer);
        }

        public override void UpdateFrameBuffer(Image frameBuffer) {
            if (frameBuffer == null) return;

            width = frameBuffer.Width;
            height = frameBuffer.Height;
            aspectRatio = width / (float)height;

            cameraToView = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 0.001f, 1000.0f);
            Matrix4x4.Invert(cameraToView, out viewToCamera);

            float tanHalf = MathF.Tan(fovRadians * 0.5f);
            imagePlaneDistance = height / (2 * tanHalf);
        }

        public override Ray GenerateRay(Vector2 filmPos) {
            // Transform the direction from film to world space
            var view = new Vector3(filmPos.X / width * 2 - 1, filmPos.Y / height * 2 - 1, 0);
            var localDir = Vector3.Transform(view, viewToCamera);
            var dir = Vector3.Transform(localDir, cameraToWorld);

            // Compute the camera position
            var pos = Vector3.Transform(new Vector3(0, 0, 0), cameraToWorld);

            return new Ray { direction = Vector3.Normalize(dir), minDistance = 0, origin = pos };
        }

        public override Vector3 WorldToFilm(Vector3 pos) {
            var local = Vector3.Transform(pos, worldToCamera);
            var view = Vector4.Transform(local, cameraToView);
            return new Vector3((view.X / view.W + 1) / 2 * width, (view.Y / view.W + 1) / 2 * height, local.Length());
        }

        public override float SolidAngleToPixelJacobian(Vector3 dir) {
            // Compute the cosine between the image plane normal (= view direction) and the direction in question
            var local = Vector3.Transform(dir, worldToCamera);
            var cosine = local.Z / local.Length();

            // Distance to the image plane point: 
            // computed based on the right-angled triangle it forms with the view direction
            // cosine = adjacentSide / d <=> d = adjacentSide / cosine
            float d = imagePlaneDistance / cosine;

            // The jacobian from solid angle to surface area is:
            float jacobian = d * d / MathF.Abs(cosine);

            return jacobian;
        }

        Matrix4x4 cameraToView;
        Matrix4x4 viewToCamera;
        int width, height;
        float aspectRatio;
        float fovRadians;

        // Distance from the camera position to the virtual image plane s.t. each pixel has area one
        float imagePlaneDistance;
    }
}
