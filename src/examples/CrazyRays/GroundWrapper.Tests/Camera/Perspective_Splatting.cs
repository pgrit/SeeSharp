using System;
using System.Numerics;
using Xunit;

namespace GroundWrapper.Tests.Camera {
    public class Perspective_Splatting {
        Cameras.Camera MakeTestCamera() {
            var frameBuffer = new Image(3, 3);

            var camTransform = Matrix4x4.CreateLookAt(
                cameraPosition: new Vector3(0, 0, 0),
                cameraTarget: new Vector3(0, 0, 10),
                cameraUpVector: new Vector3(0, 1, 0));
            float fov = 90;

            return new Cameras.PerspectiveCamera(camTransform, fov, frameBuffer);
        }

        [Fact]
        public void Center_CorrectPixel() {
            var cam = MakeTestCamera();
            var raster = cam.WorldToFilm(new Vector3(0, 0, 3.5f)).Value;

            Assert.Equal(1.5f, raster.X, 4);
            Assert.Equal(1.5f, raster.Y, 4);
            Assert.Equal(3.5f, raster.Z, 4);
        }

        [Fact]
        public void BottomLeft_CorrectPixel() {
            var cam = MakeTestCamera();

            var c = MathF.Cos(MathF.PI / 4.0f);
            var len = MathF.Sqrt(c * c * 3);
            var xyz = c / len;

            var raster = cam.WorldToFilm(new Vector3(xyz, -xyz, xyz)).Value;

            Assert.Equal(0.0f, raster.X, 4);
            Assert.Equal(0.0f, raster.Y, 4);
            Assert.Equal(1.0f, raster.Z, 4);
        }

        [Fact]
        public void Center_Behind_ShouldBeNull() {
            var cam = MakeTestCamera();
            var result = cam.WorldToFilm(new Vector3(0, 0, -3.5f));
            Assert.False(result.HasValue);
        }
    }
}
