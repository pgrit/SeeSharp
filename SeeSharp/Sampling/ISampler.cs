namespace SeeSharp.Sampling;

public interface ISampler {
    float NextFloat();
    Vector2 NextFloat2D();
}
