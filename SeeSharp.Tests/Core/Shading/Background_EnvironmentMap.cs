using SeeSharp.Shading.Background;
using SimpleImageIO;
using System.Numerics;
using Xunit;

namespace SeeSharp.Tests.Core.Shading {
    public class Background_EnvironmentMap {
        Background MakeSimpleMap() {
            // The basis is a black image.
            RgbImage image = new(512, 256);

            // Create a "sun".
            image.AtomicAdd(128, 64, RgbColor.White * 10);

            // Create the background object
            var bgn = new EnvironmentMap(image);

            // Specify a fake scene bounding sphere
            bgn.SceneCenter = new Vector3(1, 2, 3);
            bgn.SceneRadius = 42;

            return bgn;
        }

        [Fact]
        public void Radiance_ShouldBeTen() {
            var map = MakeSimpleMap();

            var val = map.EmittedRadiance(Vector3.UnitY + Vector3.UnitZ);

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
        public void Rotation_ShouldBeCW() {
            RgbImage image = new(512, 256);

            // Create two markers: one should be along positive x, the other along positive z
            image.AtomicAdd(0, 128, RgbColor.White * 10); // x
            image.AtomicAdd(128, 128, RgbColor.White * 5); // z

            var bgn = new EnvironmentMap(image);
            bgn.SceneCenter = new Vector3(1, 2, 3);
            bgn.SceneRadius = 42;

            var xclr = bgn.EmittedRadiance(new(1, 0, 0));
            var zclr = bgn.EmittedRadiance(new(0, 0, 1));

            Assert.Equal(RgbColor.White * 10, xclr);
            Assert.Equal(RgbColor.White * 5, zclr);
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

            var (ray, _, pdfSample) = map.SampleRay(Vector2.One * 0.741f, Vector2.One * 0.2f);
            float pdfEval = map.RayPdf(Vector3.Zero, ray.Direction);

            Assert.Equal(pdfEval, pdfSample, 3);
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

            var (ray, weight, pdf) = map.SampleRay(Vector2.One * 0.741f, Vector2.One * 0.2f);

            var radiance = map.EmittedRadiance(-ray.Direction);
            var expectedWeight = radiance / pdf;

            Assert.Equal(expectedWeight.R, weight.R);
            Assert.Equal(expectedWeight.G, weight.G);
            Assert.Equal(expectedWeight.B, weight.B);
        }

        [Fact]
        public void RaySampling_SampleInverse() {
            var map = MakeSimpleMap();
            var (ray, _, _) = map.SampleRay(Vector2.One * 0.741f, Vector2.One * 0.2f);
            var (posP, dirP) = map.SampleRayInverse(ray.Direction, ray.Origin);

            Assert.Equal(0.741f, posP.X, 3);
            Assert.Equal(0.741f, posP.Y, 3);
            Assert.Equal(0.2f, dirP.X, 3);
            Assert.Equal(0.2f, dirP.Y, 3);
        }

        [Fact]
        public void RaySampling_SampleInverseOffset() {
            var map = MakeSimpleMap();
            var (ray, _, _) = map.SampleRay(Vector2.One * 0.741f, Vector2.One * 0.2f);
            var (posP, dirP) = map.SampleRayInverse(ray.Direction, ray.Origin + 42.124f * ray.Direction);

            Assert.Equal(0.741f, posP.X, 3);
            Assert.Equal(0.741f, posP.Y, 3);
            Assert.Equal(0.2f, dirP.X, 3);
            Assert.Equal(0.2f, dirP.Y, 3);
        }
    }
}