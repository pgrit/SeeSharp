using SimpleImageIO;

namespace SeeSharp.Image {
    /// <summary>
    /// Base class for all filtering operations
    /// </summary>
    public abstract class Filter {
        /// <summary>
        /// Applies the filter to an image and writes the result to another image. Depending on the filter,
        /// it may or may not be alright to use the same image for both parameters.
        /// </summary>
        public abstract void Apply(ImageBase original, ImageBase target);
    }
}