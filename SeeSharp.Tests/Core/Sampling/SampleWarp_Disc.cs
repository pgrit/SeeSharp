using SeeSharp.Sampling;
using Xunit;

namespace SeeSharp.Tests.Core.Sampling {
    public class SampleWarp_Disc {
        [Fact]
        public void ConcentricDisc_Inverse() {
            var sample = SampleWarp.ToConcentricDisc(new(0.315f, -0.3154f));
            var prim = SampleWarp.FromConcentricDisc(sample);
            Assert.Equal(0.315f, prim.X, 3);
            Assert.Equal(-0.3154f, prim.Y, 3);
        }
    }
}
