using SeeSharp.Image;
using SeeSharp.Sampling;
using System;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Core.Camera {
    public class Perspective_Rays {
        Cameras.Camera MakeTestCamera() {
            var camTransform = Matrix4x4.CreateLookAt(
                cameraPosition: new Vector3(0, 0, 0),
                cameraTarget: new Vector3(0, 0, 10),
                cameraUpVector: new Vector3(0, 1, 0));
            float fov = 90;

            var cam = new Cameras.PerspectiveCamera(camTransform, fov);
            cam.UpdateResolution(3, 3);
            return cam;
        }

        [Fact]
        public void Directions_TopLeft() {
            var cam = MakeTestCamera();

            // top left corner
            var ray = cam.GenerateRay(new Vector2(0, 0), new RNG()).Ray;

            Assert.Equal(0, ray.Origin.X);
            Assert.Equal(0, ray.Origin.Y);
            Assert.Equal(0, ray.Origin.Z);

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 3);
            var expectedXYZ = c / len;

            Assert.Equal(expectedXYZ, ray.Direction.X, 3);
            Assert.Equal(expectedXYZ, ray.Direction.Y, 3);
            Assert.Equal(expectedXYZ, ray.Direction.Z, 3);
        }

        [Fact]
        public void Directions_Center() {
            var cam = MakeTestCamera();

            // image center
            var ray = cam.GenerateRay(new Vector2(1.5f, 1.5f), new RNG()).Ray;

            Assert.Equal(0, ray.Origin.X);
            Assert.Equal(0, ray.Origin.Y);
            Assert.Equal(0, ray.Origin.Z);

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 3);

            Assert.Equal(0, ray.Direction.X, 3);
            Assert.Equal(0, ray.Direction.Y, 3);
            Assert.Equal(1, ray.Direction.Z, 3);
        }

        [Fact]
        public void Directions_Left() {
            var cam = MakeTestCamera();

            // left center
            var ray = cam.GenerateRay(new Vector2(0, 1.5f), new RNG()).Ray;

            Assert.Equal(0, ray.Origin.X);
            Assert.Equal(0, ray.Origin.Y);
            Assert.Equal(0, ray.Origin.Z);

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 2);
            var expectedXYZ = c / len;

            Assert.Equal(expectedXYZ, ray.Direction.X, 3);
            Assert.Equal(0, ray.Direction.Y, 3);
            Assert.Equal(expectedXYZ, ray.Direction.Z, 3);
        }

        [Fact]
        public void Directions_Right() {
            var cam = MakeTestCamera();

            // left center
            var ray = cam.GenerateRay(new Vector2(3, 1.5f), new RNG()).Ray;

            Assert.Equal(0, ray.Origin.X);
            Assert.Equal(0, ray.Origin.Y);
            Assert.Equal(0, ray.Origin.Z);

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 2);
            var expectedXYZ = c / len;

            Assert.Equal(-expectedXYZ, ray.Direction.X, 3);
            Assert.Equal(0, ray.Direction.Y, 3);
            Assert.Equal(expectedXYZ, ray.Direction.Z, 3);
        }

        [Fact]
        public void Directions_BottomRight() {
            var cam = MakeTestCamera();

            // bottom left corner
            var ray = cam.GenerateRay(new Vector2(3, 3), new RNG()).Ray;

            Assert.Equal(0, ray.Origin.X);
            Assert.Equal(0, ray.Origin.Y);
            Assert.Equal(0, ray.Origin.Z);

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 3);
            var expectedXYZ = c / len;

            Assert.Equal(-expectedXYZ, ray.Direction.X, 3);
            Assert.Equal(-expectedXYZ, ray.Direction.Y, 3);
            Assert.Equal(expectedXYZ, ray.Direction.Z, 3);
        }
    }
}
