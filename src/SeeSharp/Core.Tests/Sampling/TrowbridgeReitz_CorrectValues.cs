using SeeSharp.Core.Shading.MicrofacetDistributions;
using System;
using Xunit;

namespace SeeSharp.Core.Tests.Sampling {
    public class TrowbridgeReitz_CorrectValues {
        [Fact]
        public void NDF_Orthogonal() {
            float ax = 0.5f;
            float ay = 0.3f;
            MicrofacetDistribution dist = new TrowbridgeReitzDistribution() { AlphaX = ax, AlphaY = ay };
            var ndf = dist.NormalDistribution(new System.Numerics.Vector3(0, 0, 1));

            float expectedNdf = 1 / (MathF.PI * ax * ay);

            Assert.Equal(expectedNdf, ndf, 4);
        }

        [Fact]
        public void NDF_GrazingAngle() {
            float ax = 0.5f;
            float ay = 0.3f;
            MicrofacetDistribution dist = new TrowbridgeReitzDistribution() { AlphaX = ax, AlphaY = ay };
            var ndf = dist.NormalDistribution(new System.Numerics.Vector3(1, 0, 0));

            float expectedNdf = 0;

            Assert.Equal(expectedNdf, ndf, 4);
        }
    }
}
