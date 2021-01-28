using SeeSharp.Core.Shading;
using SeeSharp.Core.Image;
using Xunit;

namespace SeeSharp.Core.Tests {
    public class Image_ReadWrite {
        [Fact]
        public void Default_ShouldBeBlack() {
            Image<ColorRGB> image = new Image<ColorRGB>(2, 2);
            Assert.Equal(0.0f, image[0, 0].R);
            Assert.Equal(0.0f, image[0, 0].G);
            Assert.Equal(0.0f, image[0, 0].B);

            Assert.Equal(0.0f, image[1, 0].R);
            Assert.Equal(0.0f, image[1, 0].G);
            Assert.Equal(0.0f, image[1, 0].B);

            Assert.Equal(0.0f, image[0, 1].R);
            Assert.Equal(0.0f, image[0, 1].G);
            Assert.Equal(0.0f, image[0, 1].B);

            Assert.Equal(0.0f, image[1, 1].R);
            Assert.Equal(0.0f, image[1, 1].G);
            Assert.Equal(0.0f, image[1, 1].B);
        }

        [Fact]
        public void Written_ShouldBeRead() {
            Image<ColorRGB> image = new Image<ColorRGB>(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            Assert.Equal(0.0f, image[0, 0].R);
            Assert.Equal(0.0f, image[0, 0].G);
            Assert.Equal(0.0f, image[0, 0].B);

            Assert.Equal(1.0f, image[1, 0].R);
            Assert.Equal(0.0f, image[1, 0].G);
            Assert.Equal(0.0f, image[1, 0].B);

            Assert.Equal(0.0f, image[0, 1].R);
            Assert.Equal(1.0f, image[0, 1].G);
            Assert.Equal(0.0f, image[0, 1].B);

            Assert.Equal(0.0f, image[1, 1].R);
            Assert.Equal(0.0f, image[1, 1].G);
            Assert.Equal(1.0f, image[1, 1].B);
        }

        // TODO enable this test once the border mode is actually available and configurable
        //[Fact]
        //public void BorderHandling_ShouldBeClamp() {
        //    Image<ColorRGB> image = new Image<ColorRGB>(2, 2);
        //    image[0, 0] = new ColorRGB(0, 0, 0);
        //    image[1, 0] = new ColorRGB(1, 0, 0);
        //    image[0, 1] = new ColorRGB(0, 1, 0);
        //    image[1, 1] = new ColorRGB(0, 0, 1);

        //    Assert.Equal(0.0f, image[-2, 0].R);
        //    Assert.Equal(0.0f, image[-2, 0].G);
        //    Assert.Equal(0.0f, image[-2, 0].B);

        //    Assert.Equal(1.0f, image[4, 0].R);
        //    Assert.Equal(0.0f, image[4, 0].G);
        //    Assert.Equal(0.0f, image[4, 0].B);

        //    Assert.Equal(0.0f, image[0, 10].R);
        //    Assert.Equal(1.0f, image[0, 10].G);
        //    Assert.Equal(0.0f, image[0, 10].B);

        //    Assert.Equal(0.0f, image[15, 15].R);
        //    Assert.Equal(0.0f, image[15, 15].G);
        //    Assert.Equal(1.0f, image[15, 15].B);
        //}

        [Fact]
        public void Interpolation_ShouldBeNearest() {
            Image<ColorRGB> image = new Image<ColorRGB>(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            Assert.Equal(0.0f, image[0.5f, 0.5f].R);
            Assert.Equal(0.0f, image[0.5f, 0.5f].G);
            Assert.Equal(0.0f, image[0.5f, 0.5f].B);
        }

        [Fact]
        public void WrittenFile_ShouldBeReadBack() {
            Image<ColorRGB> image = new Image<ColorRGB>(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            Image<ColorRGB>.WriteToFile(image, "test.exr");
            Image<ColorRGB> read = Image<ColorRGB>.LoadFromFile("test.exr");

            Assert.NotNull(read);
            Assert.Equal(0, read[0, 0].R);
            Assert.Equal(0, read[0, 0].G);
            Assert.Equal(0, read[0, 0].B);

            Assert.Equal(1, read[1, 0].R);
            Assert.Equal(0, read[1, 0].G);
            Assert.Equal(0, read[1, 0].B);

            Assert.Equal(0, read[0, 1].R);
            Assert.Equal(1, read[0, 1].G);
            Assert.Equal(0, read[0, 1].B);

            Assert.Equal(0, read[1, 1].R);
            Assert.Equal(0, read[1, 1].G);
            Assert.Equal(1, read[1, 1].B);
        }
    }
}
