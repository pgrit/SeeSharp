using SeeSharp.Images;
using System;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Core.Camera {
    public class Perspective_Splatting {
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
        public void Center_CorrectPixel() {
            var cam = MakeTestCamera();
            RNG rng = new();
            var raster = cam.SampleResponse(new() { Position = new Vector3(0, 0, 3.5f) }, ref rng).Pixel;

            Assert.Equal(1, raster.Col);
            Assert.Equal(1, raster.Row);
        }

        [Fact]
        public void TopLeft_CorrectPixel() {
            var cam = MakeTestCamera();

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 3);
            var xyz = c / len;

            RNG rng = new();
            var raster = cam.SampleResponse(new() { Position = new Vector3(xyz, xyz, xyz) }, ref rng).Pixel;

            Assert.Equal(0, raster.Col);
            Assert.Equal(0, raster.Row);
        }

        [Fact]
        public void Center_Behind_ShouldBeNull() {
            var cam = MakeTestCamera();
            RNG rng = new();
            var result = cam.SampleResponse(new() { Position = new Vector3(0, 0, -3.5f) }, ref rng);
            Assert.False(result.IsValid);
        }
    }
}
