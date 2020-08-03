using SeeSharp.Core.Geometry;
using SeeSharp.Core.Shading;
using System;
using System.Numerics;

namespace SeeSharp.Core.Cameras {
    public class PerspectiveCamera : Camera {
        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>
        /// Creates a new perspective camera.
        /// </summary>
        /// <param name="worldToCamera">
        /// Transformation from (right handed) world space to (right handed) camera space.
        /// The camera is centered at the origin, looking along negative Z. X is right, Y is up.
        /// </param>
        /// <param name="verticalFieldOfView">The full vertical opening angle in degrees.</param>
        /// <param name="frameBuffer">Frame buffer that will be used for rendering (only resolution is relevant).</param>
        public PerspectiveCamera(Matrix4x4 worldToCamera, float verticalFieldOfView, Image.FrameBuffer frameBuffer) : base(worldToCamera) {
            fovRadians = verticalFieldOfView * MathF.PI / 180;
            UpdateFrameBuffer(frameBuffer);
        }

        public override void UpdateFrameBuffer(Image.FrameBuffer frameBuffer) {
            if (frameBuffer == null) return;

            Width = frameBuffer.Width;
            Height = frameBuffer.Height;
            aspectRatio = Width / (float)Height;

            cameraToView = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 0.001f, 1000.0f);
            Matrix4x4.Invert(cameraToView, out viewToCamera);

            float tanHalf = MathF.Tan(fovRadians * 0.5f);
            imagePlaneDistance = Height / (2 * tanHalf);
        }

        public override (Ray, float, ColorRGB, SurfacePoint) GenerateRay(Vector2 filmPos) {
            // Transform the direction from film to world space
            var view = new Vector3(filmPos.X / Width * 2 - 1, filmPos.Y / Height * 2 - 1, 0);
            var localDir = Vector3.Transform(view, viewToCamera);
            var dirHomo = Vector4.Transform(new Vector4(localDir, 0), cameraToWorld);
            var dir = new Vector3(dirHomo.X, dirHomo.Y, dirHomo.Z);

            // Compute the camera position
            var pos = Vector3.Transform(new Vector3(0, 0, 0), cameraToWorld);

            var ray = new Ray { Direction = Vector3.Normalize(dir), MinDistance = 0, Origin = pos };

            return (
                ray,
                SolidAngleToPixelJacobian(pos + dir),
                ColorRGB.White,
                new SurfacePoint { Position = Position, Normal = Direction }
            );
        }

        public override Vector3? WorldToFilm(Vector3 pos) {
            var local = Vector3.Transform(pos, worldToCamera);

            // Check that the point is on the correct side of the camera
            if (local.Z > 0) return null;

            var view = Vector4.Transform(local, cameraToView);
            var film = new Vector3((view.X / view.W + 1) / 2 * Width, (view.Y / view.W + 1) / 2 * Height, local.Length());

            // Check that the point is within the frustum
            if (film.X < 0 || film.X > Width || film.Y < 0 || film.Y > Height)
                return null;

            return film;
        }

        public override float SolidAngleToPixelJacobian(Vector3 pos) {
            // Compute the cosine
            var local = Vector3.Transform(pos, worldToCamera);
            var cosine = local.Z / local.Length();

            // Distance to the image plane point:
            // computed based on the right-angled triangle it forms with the view direction
            // cosine = adjacentSide / d <=> d = adjacentSide / cosine
            float d = imagePlaneDistance / cosine;

            // The jacobian from solid angle to surface area is:
            float jacobian = d * d / MathF.Abs(cosine);

            return jacobian;
        }

        public override float SurfaceAreaToSolidAngleJacobian(Vector3 point, Vector3 normal) {
            var dirToCam = Position - point;
            float distToCam = dirToCam.Length();
            float cosToCam = Math.Abs(Vector3.Dot(normal, dirToCam)) / distToCam;
            return cosToCam / (distToCam * distToCam);
        }

        Matrix4x4 cameraToView;
        Matrix4x4 viewToCamera;
        float aspectRatio;
        float fovRadians;

        // Distance from the camera position to the virtual image plane s.t. each pixel has area one
        float imagePlaneDistance;
    }
}
