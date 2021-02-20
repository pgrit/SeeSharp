using SeeSharp.Sampling;
using Xunit;

namespace SeeSharp.Tests.Core.Sampling {
    public class SampleWarp_Triangle {
        [Fact]
        public void Inverse_ShouldBeZero() {
            var bary = SampleWarp.ToUniformTriangle(new(0.1f, 0.1f));
            var prim = SampleWarp.FromUniformTriangle(bary);
            Assert.Equal(0.1f, prim.X, 4);
            Assert.Equal(0.1f, prim.Y, 4);
        }

        [Fact]
        public void Inverse_ShouldBeOne() {
            var bary = SampleWarp.ToUniformTriangle(new(0.1f, 0.9f));
            var prim = SampleWarp.FromUniformTriangle(bary);
            Assert.Equal(0.1f, prim.X, 4);
            Assert.Equal(0.9f, prim.Y, 4);
        }

        [Fact]
        public void Inverse_ShouldBeQuarter() {
            var bary = SampleWarp.ToUniformTriangle(new(0.25f, 0.25f));
            var prim = SampleWarp.FromUniformTriangle(bary);
            Assert.Equal(0.25f, prim.X, 4);
            Assert.Equal(0.25f, prim.Y, 4);
        }
    }
}