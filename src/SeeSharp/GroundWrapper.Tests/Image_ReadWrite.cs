using GroundWrapper.Shading;
using Xunit;

namespace GroundWrapper.Tests {
    public class Image_ReadWrite {
        [Fact]
        public void Default_ShouldBeBlack() {
            Image image = new Image(2, 2);
            Assert.Equal(0.0f, image[0, 0].r);
            Assert.Equal(0.0f, image[0, 0].g);
            Assert.Equal(0.0f, image[0, 0].b);

            Assert.Equal(0.0f, image[1, 0].r);
            Assert.Equal(0.0f, image[1, 0].g);
            Assert.Equal(0.0f, image[1, 0].b);

            Assert.Equal(0.0f, image[0, 1].r);
            Assert.Equal(0.0f, image[0, 1].g);
            Assert.Equal(0.0f, image[0, 1].b);

            Assert.Equal(0.0f, image[1, 1].r);
            Assert.Equal(0.0f, image[1, 1].g);
            Assert.Equal(0.0f, image[1, 1].b);
        }

        [Fact]
        public void Written_ShouldBeRead() {
            Image image = new Image(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            Assert.Equal(0.0f, image[0, 0].r);
            Assert.Equal(0.0f, image[0, 0].g);
            Assert.Equal(0.0f, image[0, 0].b);

            Assert.Equal(1.0f, image[1, 0].r);
            Assert.Equal(0.0f, image[1, 0].g);
            Assert.Equal(0.0f, image[1, 0].b);

            Assert.Equal(0.0f, image[0, 1].r);
            Assert.Equal(1.0f, image[0, 1].g);
            Assert.Equal(0.0f, image[0, 1].b);

            Assert.Equal(0.0f, image[1, 1].r);
            Assert.Equal(0.0f, image[1, 1].g);
            Assert.Equal(1.0f, image[1, 1].b);
        }

        [Fact]
        public void BorderHandling_ShouldBeClamp() {
            Image image = new Image(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            Assert.Equal(0.0f, image[-2, 0].r);
            Assert.Equal(0.0f, image[-2, 0].g);
            Assert.Equal(0.0f, image[-2, 0].b);

            Assert.Equal(1.0f, image[4, 0].r);
            Assert.Equal(0.0f, image[4, 0].g);
            Assert.Equal(0.0f, image[4, 0].b);

            Assert.Equal(0.0f, image[0, 10].r);
            Assert.Equal(1.0f, image[0, 10].g);
            Assert.Equal(0.0f, image[0, 10].b);

            Assert.Equal(0.0f, image[15, 15].r);
            Assert.Equal(0.0f, image[15, 15].g);
            Assert.Equal(1.0f, image[15, 15].b);
        }

        [Fact]
        public void Interpolation_ShouldBeNearest() {
            Image image = new Image(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            Assert.Equal(0.0f, image[0.5f, 0.5f].r);
            Assert.Equal(0.0f, image[0.5f, 0.5f].g);
            Assert.Equal(0.0f, image[0.5f, 0.5f].b);
        }

        [Fact]
        public void WrittenFile_ShouldBeReadBack() {
            Image image = new Image(2, 2);
            image[0, 0] = new ColorRGB(0, 0, 0);
            image[1, 0] = new ColorRGB(1, 0, 0);
            image[0, 1] = new ColorRGB(0, 1, 0);
            image[1, 1] = new ColorRGB(0, 0, 1);

            image.WriteToFile("test.exr");
            Image read = Image.LoadFromFile("test.exr");

            Assert.NotNull(read);
            Assert.Equal(0, read[0, 0].r);
            Assert.Equal(0, read[0, 0].g);
            Assert.Equal(0, read[0, 0].b);

            Assert.Equal(1, read[1, 0].r);
            Assert.Equal(0, read[1, 0].g);
            Assert.Equal(0, read[1, 0].b);

            Assert.Equal(0, read[0, 1].r);
            Assert.Equal(1, read[0, 1].g);
            Assert.Equal(0, read[0, 1].b);

            Assert.Equal(0, read[1, 1].r);
            Assert.Equal(0, read[1, 1].g);
            Assert.Equal(1, read[1, 1].b);
        }
    }
}
