using System;
using System.Drawing;
using System.Numerics;
using SeeSharp.Core.Geometry;
using SeeSharp.Core.Sampling;

namespace SeeSharp.Core.Shading.Background {
    /// <summary>
    /// Background that provides image based lighting to the scene.
    ///
    /// The image is interpreted as a lat-long map.
    /// The vertical axis corresponds to the latidude, the horizontal axis to the longitude.
    /// The "up" direction (top center pixel of the image) corresponds to the worldspace
    /// positive y direction.
    /// The longitude is measured such that an angle of zero indicates a direction along the
    /// worldspace positive z axis. Rotation is CCW about the y axis.
    /// </summary>
    public class EnvironmentMap : Background {
        public EnvironmentMap(Image image) {
            this.image = image;
            directionSampler = BuildSamplingGrid(image);
        }

        public override ColorRGB EmittedRadiance(Vector3 direction) {
            var sphericalDir = WorldToSpherical(direction);
            var pixelCoords = SphericalToPixel(sphericalDir);

            var col = pixelCoords.X * image.Width;
            var row = pixelCoords.Y * image.Height;
            return image[col, row];
        }

        public override BackgroundSample SampleDirection(Vector2 primary) {
            // Wrap the primary sample to a position within one of the pixels.
            var pixelPrimary = directionSampler.Sample(primary);
            var pdf = directionSampler.Pdf(pixelPrimary);

            // Wrap the pixel coordinates to the sphere of directions.
            var sphericalDir = PixelToSpherical(pixelPrimary);
            var direction = SphericalToWorld(sphericalDir);

            // Multiply the pdfs with the jacobian of the change of variables from unit square to sphere
            float jacobian = MathF.Sin(sphericalDir.Y);
            if (jacobian == 0.0f) {
                // Prevent infs / nans in this rare edge case
                return new BackgroundSample {
                    Direction = direction,
                    Pdf = 0,
                    Weight = ColorRGB.Black
                };
            }
            pdf /= jacobian; 
            // TODO we could (and should) pre-multiply the pdf by the sine, to avoid oversampling regions that will receive zero weight

            // Compute the sample weight
            var weight = image[pixelPrimary.X * image.Width, pixelPrimary.Y * image.Height] / pdf;

            return new BackgroundSample {
                Direction = direction,
                Pdf = pdf,
                Weight = weight
            };
        }

        public override float DirectionPdf(Vector3 direction) {
            var sphericalDir = WorldToSpherical(direction);
            var pixelCoords = SphericalToPixel(sphericalDir);
            return directionSampler.Pdf(pixelCoords) * MathF.Sin(sphericalDir.Y);
        }

        public override (Ray, ColorRGB, float) SampleRay(Vector2 primaryPos, Vector2 primaryDir) {
            throw new NotImplementedException();
        }

        public override float RayPdf(SurfacePoint point, Vector3 direction) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Forms a tabulated pdf to importance sample the given image.
        /// </summary>
        /// <param name="image">The source image to importance sample</param>
        /// <returns>Tabulated pdf for the image.</returns>
        public virtual RegularGrid2d BuildSamplingGrid(Image image) {
            var result = new RegularGrid2d(image.Width, image.Height);

            for (int row = 0; row < image.Height; ++row) {
                for (int col = 0; col < image.Width; ++col) {
                    var x = col / (float)image.Width;
                    var y = row / (float)image.Height;
                    result.Splat(x, y, image[col, row].Luminance);
                }
            }

            result.Normalize();
            return result;
        }

        Vector2 WorldToSpherical(Vector3 direction) {
            var dir = ShadingSpace.WorldToShading(Vector3.UnitY, direction);
            return SampleWrap.CartesianToSpherical(dir);
        }

        Vector3 SphericalToWorld(Vector2 spherical) {
            var dir = SampleWrap.SphericalToCartesian(MathF.Sin(spherical.Y), MathF.Cos(spherical.Y), spherical.X);
            return ShadingSpace.ShadingToWorld(Vector3.UnitY, dir);
        }

        Vector2 SphericalToPixel(Vector2 sphericalDir) 
            => new Vector2(sphericalDir.X / (2 * MathF.PI), sphericalDir.Y / MathF.PI);

        Vector2 PixelToSpherical(Vector2 pixel)
            => new Vector2(pixel.X * 2 * MathF.PI, pixel.Y * MathF.PI);

        readonly Image image;
        readonly RegularGrid2d directionSampler;
    }
}