using SeeSharp.Sampling;
using Xunit;

namespace Tests.Sampling {
    public class SampleWarp_Sphere {
        [Fact]
        public void CosHemisphere_Inverse() {
            var sample = SampleWarp.ToCosHemisphere(new(0.43f, 0.793f));
            var prim = SampleWarp.FromCosHemisphere(sample.Direction);

            Assert.Equal(0.43f, prim.X, 3);
            Assert.Equal(0.793f, prim.Y, 3);
        }
    }
}
