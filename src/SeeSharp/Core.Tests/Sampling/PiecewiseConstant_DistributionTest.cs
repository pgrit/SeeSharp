using SeeSharp.Core.Sampling;
using System;
using Xunit;

namespace SeeSharp.Core.Tests.Sampling {
    public class PiecewiseConstant_DistributionTest {
        [Fact]
        public void Pdfs_ShouldBeProportional() {
            var weights = new float[] { 1, 1, 2, 2 };
            var dist = new PiecewiseConstant(weights);

            Assert.Equal(1.0f / 6.0f, dist.Probability(0), 3);
            Assert.Equal(1.0f / 6.0f, dist.Probability(1), 3);
            Assert.Equal(2.0f / 6.0f, dist.Probability(2), 3);
            Assert.Equal(2.0f / 6.0f, dist.Probability(3), 3);
        }

        [Fact]
        public void AsymptoticDistribution() {
            var weights = new float[] { 1, 1, 2, 2 };
            var dist = new PiecewiseConstant(weights);

            var counters = new float[] { 0, 0, 0, 0 };
            int numSteps = 100;
            for (float u = 0.0f; u < 1.0f; u += 1.0f / numSteps) {
                counters[dist.Sample(u).Item1] += 6.0f / numSteps;
            }

            Assert.Equal(1.0f, counters[0], 1);
            Assert.Equal(1.0f, counters[1], 1);
            Assert.Equal(2.0f, counters[2], 1);
            Assert.Equal(2.0f, counters[3], 1);
        }

        [Fact]
        public void ShouldBeUniformWithin() {
            var weights = new float[] { 1, 1, 2, 2 };
            var dist = new PiecewiseConstant(weights);

            int numSteps = 1000;
            var counters = new float[numSteps];
            for (float u = 0.0f; u < 1.0f; u += 1.0f / numSteps) {
                var (_, pos) = dist.Sample(u);
                counters[(int)Math.Max(Math.Min(pos * numSteps, numSteps), 0)] += 1.0f / numSteps;
            }

            int nonzero = 0;
            foreach (float c in counters)
                nonzero += c > 0 ? 1 : 0;

            Assert.True(nonzero > numSteps * 0.8);
        }

        [Fact]
        public void BorderHandling_IsCorrect() {
            var weights = new float[] { 1, 1, 2, 2 };
            var dist = new PiecewiseConstant(weights);

            var (idx, rel) = dist.Sample(0.0f);
            Assert.Equal(0, idx);
            Assert.Equal(0.0f, rel, 6);

            (idx, rel) = dist.Sample(1.0f);
            Assert.Equal(3, idx);
            Assert.Equal(1.0f, rel, 6);

            (idx, rel) = dist.Sample(0.999f);
            Assert.Equal(3, idx);
            Assert.Equal(0.999f, rel, 2);

            (idx, rel) = dist.Sample(0.0001f);
            Assert.Equal(0, idx);
            Assert.Equal(0.0001f * 6.0f, rel, 6);
        }
    }
}
