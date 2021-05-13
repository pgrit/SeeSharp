using SimpleImageIO;

namespace SeeSharp.Image {
    /// <summary>
    /// A layer in the frame buffer, an image to hold AOVs
    /// </summary>
    public abstract class Layer {
        /// <summary>
        /// The image buffer
        /// </summary>
        public ImageBase Image { get; set; }

        /// <summary>
        /// Called once before the first rendering iteration
        /// </summary>
        /// <param name="width">The width of the frame buffer</param>
        /// <param name="height">The height of the frame buffer</param>
        public abstract void Init(int width, int height);

        /// <summary>
        /// Called at the beginning of each new rendering iteration. Derived classes should always call
        /// this function to achieve proper normalization
        /// </summary>
        /// <param name="curIteration">The 0-based index of the iteration that starts now</param>
        public virtual void OnStartIteration(int curIteration) {
            if (curIteration > 1)
                Image.Scale((curIteration - 1.0f) / curIteration);
            this.curIteration = curIteration;
        }

        /// <summary>
        /// Called at the end of each rendering iteration
        /// </summary>
        /// <param name="curIteration">The 0-based index of the iteration that just finished</param>
        public virtual void OnEndIteration(int curIteration) { }

        /// <summary>
        /// The 0-based index of the iteration that is currently being rendered
        /// </summary>
        protected int curIteration;
    }
}