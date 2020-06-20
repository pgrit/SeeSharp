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

            Assert.Equal(10.0f, val.R);
            Assert.Equal(10.0f, val.G);
            Assert.Equal(10.0f, val.B);
        }

        [Fact]
        public void Radiance_ShouldBeZero() {
            var map = MakeSimpleMap();

            var valTop = map.EmittedRadiance(-Vector3.UnitY);
            var valLeft = map.EmittedRadiance(-Vector3.UnitX);

            Assert.Equal(0.0f, valTop.R);
            Assert.Equal(0.0f, valTop.G);
            Assert.Equal(0.0f, valTop.B);

            Assert.Equal(0.0f, valLeft.R);
            Assert.Equal(0.0f, valLeft.G);
            Assert.Equal(0.0f, valLeft.B);
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
        public void DirectionSampling_EstimatorShouldBeCorrect() {
            var map = MakeSimpleMap();

            var sample = map.SampleDirection(Vector2.One * 0.2f);

            float pdf = map.DirectionPdf(sample.Direction);
            var radiance = map.EmittedRadiance(sample.Direction);
            var expectedWeight = radiance / pdf;

            Assert.Equal(expectedWeight.R, sample.Weight.R);
            Assert.Equal(expectedWeight.G, sample.Weight.G);
            Assert.Equal(expectedWeight.B, sample.Weight.B);
        }

        [Fact]
        public void RaySampling_PdfShouldBeConsistent() {
            var map = MakeSimpleMap();

            var sample = map.SampleRay(Vector2.One * 0.741f, Vector2.One * 0.2f);

        }

        [Fact]
        public void RaySampling_DirectionShouldBeOpposite() {
            var map = MakeSimpleMap();

            var dirPrimary = Vector2.One * 0.2f;
            var (ray, _, _) = map.SampleRay(Vector2.One * 0.741f, dirPrimary);
            var dirSample = map.SampleDirection(dirPrimary);

            Assert.Equal(dirSample.Direction.X, -ray.Direction.X, 3);
            Assert.Equal(dirSample.Direction.Y, -ray.Direction.Y, 3);
            Assert.Equal(dirSample.Direction.Z, -ray.Direction.Z, 3);
        }

        [Fact]
        public void RaySampling_EstimatorShouldBeCorrect() {
            var map = MakeSimpleMap();

            var sample = map.SampleRay(Vector2.One * 0.741f, Vector2.One * 0.2f);
        }
    }
}