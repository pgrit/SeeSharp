using SeeSharp.Core.Shading;
using System.Numerics;
using Xunit;

namespace SeeSharp.Core.Tests.Shading {
    public class ShadingSpace_Setup_Transform {
        [Fact]
        public void WorldToShade_ShouldBeZAxis() {
            var normal = new Vector3(1, 1, 0);
            var worldDir = new Vector3(-1, -1, 0);
            var shadeDir = ShadingSpace.WorldToShading(normal, worldDir);

            Assert.Equal(0.0f, shadeDir.X);
            Assert.Equal(0.0f, shadeDir.Y);
            Assert.Equal(-1.0f, shadeDir.Z, 4);
        }

        [Fact]
        public void WorldToShade_ShouldBeXY() {
            var normal = new Vector3(1, 1, 0);
            var worldDir = new Vector3(0, 0, 1);
            var shadeDir = ShadingSpace.WorldToShading(normal, worldDir);

            Assert.Equal(0.0f, shadeDir.Z);
        }

        [Fact]
        public void WorldToShade_AndBack_ShouldBeOriginalNormalized() {
            var normal = new Vector3(1, 5, 0);
            var worldDir = new Vector3(1, 6, 2);
            var shadeDir = ShadingSpace.WorldToShading(normal, worldDir);
            var worldDir2 = ShadingSpace.ShadingToWorld(normal, shadeDir);

            Assert.Equal(worldDir.X / worldDir.Length(), worldDir2.X, 4);
            Assert.Equal(worldDir.Y / worldDir.Length(), worldDir2.Y, 4);
            Assert.Equal(worldDir.Z / worldDir.Length(), worldDir2.Z, 4);
        }
    }
}
