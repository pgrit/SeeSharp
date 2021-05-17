using SeeSharp.Shading.MicrofacetDistributions;
using System;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Core.Sampling {
    public class TrowbridgeReitz_CorrectValues {
        [Fact]
        public void NDF_Orthogonal() {
            float ax = 0.5f;
            float ay = 0.3f;
            var dist = new TrowbridgeReitzDistribution() { AlphaX = ax, AlphaY = ay };
            var ndf = dist.NormalDistribution(new System.Numerics.Vector3(0, 0, 1));

            float expectedNdf = 1 / (MathF.PI * ax * ay);

            Assert.Equal(expectedNdf, ndf, 4);
        }

        [Fact]
        public void NDF_GrazingAngle() {
            float ax = 0.5f;
            float ay = 0.3f;
            var dist = new TrowbridgeReitzDistribution() { AlphaX = ax, AlphaY = ay };
            var ndf = dist.NormalDistribution(new System.Numerics.Vector3(1, 0, 0));

            float expectedNdf = 0;

            Assert.Equal(expectedNdf, ndf, 4);
        }

        [Fact]
        public void SampleCornerCases_ShouldBeFinite() {
            float ax = 0.5f;
            float ay = 0.3f;
            var dist = new TrowbridgeReitzDistribution() { AlphaX = ax, AlphaY = ay };

            var dir1 = dist.Sample(Vector3.UnitZ, new(0, 1));
            var dir2 = dist.Sample(new(MathF.Sqrt(2), 0, MathF.Sqrt(2)), new(1, 0));
            var dir3 = dist.Sample(Vector3.UnitZ, new(1, 0));

            Assert.True(float.IsFinite(dir1.X));
            Assert.True(float.IsFinite(dir1.Y));
            Assert.True(float.IsFinite(dir1.Z));

            Assert.True(float.IsFinite(dir2.X));
            Assert.True(float.IsFinite(dir2.Y));
            Assert.True(float.IsFinite(dir2.Z));

            Assert.True(float.IsFinite(dir3.X));
            Assert.True(float.IsFinite(dir3.Y));
            Assert.True(float.IsFinite(dir3.Z));
        }
    }
}
