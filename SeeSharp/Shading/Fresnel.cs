namespace SeeSharp.Shading;

public static class Fresnel {
    public static float Dielectric(float cosThetaI, float etaI, float etaT) {
        cosThetaI = Math.Clamp(cosThetaI, -1, 1);
        // Potentially swap indices of refraction
        bool entering = cosThetaI > 0;
        if (!entering) {
            (etaT, etaI) = (etaI, etaT);
            cosThetaI = Math.Abs(cosThetaI);
        }

        // Compute _cosThetaT_ using Snell's law
        float sinThetaI = MathF.Sqrt(Math.Max(0, 1 - cosThetaI * cosThetaI));
        float sinThetaT = etaI / etaT * sinThetaI;

        // Handle total internal reflection
        if (sinThetaT >= 1) return 1;
        float cosThetaT = MathF.Sqrt(Math.Max(0, 1 - sinThetaT * sinThetaT));
        float Rparl = (etaT * cosThetaI - etaI * cosThetaT) /
              (etaT * cosThetaI + etaI * cosThetaT);
        float Rperp = (etaI * cosThetaI - etaT * cosThetaT) /
              (etaI * cosThetaI + etaT * cosThetaT);
        return (Rparl * Rparl + Rperp * Rperp) / 2;
    }

    // https://seblagarde.wordpress.com/2013/04/29/memo-on-fresnel-equations/
    //
    // The Schlick Fresnel approximation is:
    //
    // R = R(0) + (1 - R(0)) (1 - cos theta)^5,
    //
    // where R(0) is the reflectance at normal indicence.
    public static float SchlickWeight(float cosTheta) {
        float m = Math.Clamp(1 - cosTheta, 0, 1);
        return (m * m) * (m * m) * m;
    }

    public static RgbColor SchlickFresnel(RgbColor R0, float cosTheta) {
        return RgbColor.Lerp(SchlickWeight(cosTheta), R0, RgbColor.White);
    }

    // For a dielectric, R(0) = (eta - 1)^2 / (eta + 1)^2, assuming we're
    // coming from air..
    public static float SchlickR0FromEta(float eta) {
        var ratio = (eta - 1) / (eta + 1);
        return ratio * ratio;
    }
}
