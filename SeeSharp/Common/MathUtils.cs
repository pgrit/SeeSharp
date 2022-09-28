namespace SeeSharp.Common;

public partial class MathUtils {
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => t * a + (1 - t) * b;
    public static float Lerp(float a, float b, float t) => t * a + (1 - t) * b;
}