using SeeSharp.Sampling;
using Xunit;

namespace SeeSharp.Tests.Core.Sampling {
    public class PiecewiseConstant_SampleInverse {
        [Fact]
        public void TwoElements_EqualWeights() {
            PiecewiseConstant dist = new(new[] { 1.0f, 1.0f });

            var (idx, r) = dist.Sample(0.25f);
            float p = dist.SampleInverse(idx, r);
            Assert.Equal(0.25f, p, 3);
        }

        [Fact]
        public void TwoElements_UnevenWeights() {
            PiecewiseConstant dist = new(new[] { 1.0f, 3.0f });

            var (idx, r) = dist.Sample(0.25f);
            float p = dist.SampleInverse(idx, r);
            Assert.Equal(0.25f, p, 3);
        }
    }
}
