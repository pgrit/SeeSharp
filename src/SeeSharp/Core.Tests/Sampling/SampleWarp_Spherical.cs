using Xunit;
using SeeSharp.Core.Sampling;
using System.Numerics;

namespace SeeSharp.Core.Tests.Sampling {
    public class SampleWarp_Spherical {
        [Fact]
        public void SphericalInverse() {
            // Y axis
            var spherical = SampleWarp.CartesianToSpherical(Vector3.UnitY);
            var dir = SampleWarp.SphericalToCartesian(spherical);
            Assert.Equal(0, dir.X, 4);
            Assert.Equal(1, dir.Y, 4);
            Assert.Equal(0, dir.Z, 4);

            spherical = SampleWarp.CartesianToSpherical(-Vector3.UnitY);
            dir = SampleWarp.SphericalToCartesian(spherical);
            Assert.Equal(0, dir.X, 4);
            Assert.Equal(-1, dir.Y, 4);
            Assert.Equal(0, dir.Z, 4);

            // x axis
            spherical = SampleWarp.CartesianToSpherical(Vector3.UnitX);
            dir = SampleWarp.SphericalToCartesian(spherical);
            Assert.Equal(1, dir.X, 4);
            Assert.Equal(0, dir.Y, 4);
            Assert.Equal(0, dir.Z, 4);

            spherical = SampleWarp.CartesianToSpherical(-Vector3.UnitX);
            dir = SampleWarp.SphericalToCartesian(spherical);
            Assert.Equal(-1, dir.X, 4);
            Assert.Equal(0, dir.Y, 4);
            Assert.Equal(0, dir.Z, 4);

            // z axis
            spherical = SampleWarp.CartesianToSpherical(Vector3.UnitZ);
            dir = SampleWarp.SphericalToCartesian(spherical);
            Assert.Equal(0, dir.X, 4);
            Assert.Equal(0, dir.Y, 4);
            Assert.Equal(1, dir.Z, 4);

            spherical = SampleWarp.CartesianToSpherical(-Vector3.UnitZ);
            dir = SampleWarp.SphericalToCartesian(spherical);
            Assert.Equal(0, dir.X, 4);
            Assert.Equal(0, dir.Y, 4);
            Assert.Equal(-1, dir.Z, 4);
        }

        [Fact]
        public void FromSphere_ShouldBeInverse() {
            var primary = new Vector2(0.41f, 0.123f);
            var dir = SampleWarp.ToUniformSphere(primary).Direction;
            var p2 = SampleWarp.FromUniformSphere(dir);

            Assert.Equal(primary.X, p2.X, 4);
            Assert.Equal(primary.Y, p2.Y, 4);

            primary = new Vector2(0.91f, 0.00123f);
            dir = SampleWarp.ToUniformSphere(primary).Direction;
            p2 = SampleWarp.FromUniformSphere(dir);

            Assert.Equal(primary.X, p2.X, 4);
            Assert.Equal(primary.Y, p2.Y, 4);

            primary = new Vector2(0.091f, 0.00123f);
            dir = SampleWarp.ToUniformSphere(primary).Direction;
            p2 = SampleWarp.FromUniformSphere(dir);

            Assert.Equal(primary.X, p2.X, 4);
            Assert.Equal(primary.Y, p2.Y, 4);

            primary = new Vector2(0.91f, 0.823f);
            dir = SampleWarp.ToUniformSphere(primary).Direction;
            p2 = SampleWarp.FromUniformSphere(dir);

            Assert.Equal(primary.X, p2.X, 4);
            Assert.Equal(primary.Y, p2.Y, 4);
        }
    }
}