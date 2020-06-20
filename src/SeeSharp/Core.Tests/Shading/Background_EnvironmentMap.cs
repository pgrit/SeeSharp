using System.Numerics;
using SeeSharp.Core.Shading;
using SeeSharp.Core.Shading.Background;
using Xunit;

namespace SeeSharp.Core.Tests.Shading {
    public class Background_EnvironmentMap {
        Background MakeSimpleMap() {
            // The basis is a black image.
            Image image = new Image(512, 256);

            // Create a "sun".
            image.Splat(128, 64, ColorRGB.White * 10);

            return new EnvironmentMap(image);
        }

        [Fact]
        public void Radiance_ShouldBeTen() {
            var map = MakeSimpleMap();

            var val = map.EmittedRadiance(Vector3.UnitY - Vector3.UnitX);

            Assert.Equal(10.0f, val.r);
            Assert.Equal(10.0f, val.g);
            Assert.Equal(10.0f, val.b);
        }

        [Fact]
        public void Radiance_ShouldBeZero() {
            var map = MakeSimpleMap();

            var valTop = map.EmittedRadiance(-Vector3.UnitY);
            var valLeft = map.EmittedRadiance(-Vector3.UnitX);

            Assert.Equal(0.0f, valTop.r);
            Assert.Equal(0.0f, valTop.g);
            Assert.Equal(0.0f, valTop.b);

            Assert.Equal(0.0f, valLeft.r);
            Assert.Equal(0.0f, valLeft.g);
            Assert.Equal(0.0f, valLeft.b);
        }

        [Fact]
        public void SampleDirection_ShouldBeUpwardsLeft() {
            var map = MakeSimpleMap();

            var sample = map.SampleDirection(Vector2.One * 0.42f);

            // The direction should be the diagonal of the XY plane
            Assert.True(sample.Direction.X < 0);
            Assert.True(sample.Direction.Y > 0);
            Assert.Equal(0.0f, sample.Direction.Z, 2);
            Assert.Equal(sample.Direction.X, -sample.Direction.Y, 1);

            // It should also be normalized
            Assert.Equal(1.0f, sample.Direction.Length(), 4);
        }

        [Fact]
        public void DirectionSampling_PdfShouldBeConsistent() {
            var map = MakeSimpleMap();

            var sample = map.SampleDirection(Vector2.One * 0.2f);
            float pdf = map.DirectionPdf(sample.Direction);

            Assert.Equal(sample.Pdf, pdf, 3);
        }

        [Fact]
        public void RaySampling_PdfShouldBeConsistent() {
            var map = MakeSimpleMap();
        }

        [Fact]
        public void DirectionSampling_EstimatorShouldBeCorrect() {
            var map = MakeSimpleMap();

            var sample = map.SampleDirection(Vector2.One * 0.2f);

            float pdf = map.DirectionPdf(sample.Direction);
            var radiance = map.EmittedRadiance(sample.Direction);
            var expectedWeight = radiance / pdf;

            Assert.Equal(expectedWeight.r, sample.Weight.r);
            Assert.Equal(expectedWeight.g, sample.Weight.g);
            Assert.Equal(expectedWeight.b, sample.Weight.b);
        }
    }
}