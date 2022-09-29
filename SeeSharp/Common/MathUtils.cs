namespace SeeSharp.Common;

public partial class MathUtils {
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => t * a + (1 - t) * b;
    public static float Lerp(float a, float b, float t) => t * a + (1 - t) * b;

    public static float AngleBetween(Vector3 a, Vector3 b) {
        if (Vector3.Dot(a, b) < 0)
            return MathF.PI - 2 * MathF.Asin((a + b).Length() / 2);
        else
            return 2 * MathF.Asin((b - a).Length() / 2);
    }
}