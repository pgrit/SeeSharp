using SeeSharp.Geometry;
using SeeSharp.Image;
using SeeSharp.Sampling;
using SimpleImageIO;
using System;
using System.Numerics;
using TinyEmbree;

namespace SeeSharp.Cameras {
    /// <summary>
    /// Visualizes the illumination at a surface point in the scene in spherical coordinates.
    /// </summary>
    public class LightProbeCamera : Camera {
        Vector3 upVector;
        Vector3 position;
        Vector3 normal;
        float errorOffset;
        int width, height;

        /// <summary>
        /// Initializes a light probe camera at a surface point
        /// </summary>
        /// <param name="position">Point on a scene surface</param>
        /// <param name="normal">Surface normal at the query point, must point "outside" so it can be used
        /// to avoid self intersection problems with shadow rays</param>
        /// <param name="errorOffset">How far to move rays from the surface to avoid self-intersection</param>
        /// <param name="upVector">Defines the up direction of the probe in world space</param>
        public LightProbeCamera(Vector3 position, Vector3 normal, float errorOffset, Vector3 upVector) 
        : base(Matrix4x4.Identity) {
            this.upVector = upVector;
            this.position = position;
            this.normal = normal;
            this.errorOffset = errorOffset;

            // Define the world to camera transform (image center is the forward direction)
            var forward = SampleWarp.SphericalToCartesian(new(MathF.PI, MathF.PI / 2.0f));
            WorldToCamera = Matrix4x4.CreateLookAt(position + errorOffset * normal, position + forward, upVector);
        }

        public override CameraRaySample GenerateRay(Vector2 filmPos, RNG rng) {
            // Convert image position to spherical coordinates
            float phi = 2 * MathF.PI * (filmPos.X / width);
            float theta = MathF.PI * (filmPos.Y / height);

            var dir = SampleWarp.SphericalToCartesian(new(phi, theta));
            dir = Shading.ShadingSpace.ShadingToWorld(upVector, dir);

            float sign = Vector3.Dot(dir, normal) < 0.0f ? -1.0f : 1.0f;
            Ray ray = new() {
                Origin = position + sign * errorOffset * normal,
                Direction = dir,
                MinDistance = errorOffset,
            };

            return new() {
                Point = new() { Position = position, Normal = normal, ErrorOffset = errorOffset },
                Ray = ray,
                Weight = RgbColor.White,
                PdfRay = SolidAngleToPixelJacobian(position + dir),
                PdfConnect = 1,
            };
        }

        public override CameraResponseSample SampleResponse(SurfacePoint scenePoint, RNG rng) {
            throw new System.NotImplementedException();
        }

        public override float SolidAngleToPixelJacobian(Vector3 pos) {
            var dir = pos - position;
            dir = Shading.ShadingSpace.WorldToShading(upVector, dir);
            float theta = SampleWarp.CartesianToSpherical(dir).Y;
            return 1 / (2 * MathF.PI * MathF.PI * MathF.Sin(theta)) * width * height;
        }

        public override void UpdateFrameBuffer(FrameBuffer value) {
            width = value.Width;
            height = value.Height;
        }

        public override Vector3? WorldToFilm(Vector3 pos) {
            var dir = pos - position;
            float distance = dir.Length();
            dir = Shading.ShadingSpace.WorldToShading(upVector, dir);
            var spherical = SampleWarp.CartesianToSpherical(dir);
            return new(
                width * spherical.X / (2 * MathF.PI),
                height * spherical.Y / MathF.PI,
                distance
            );
        }
    }
}