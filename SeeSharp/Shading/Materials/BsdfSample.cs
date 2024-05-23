namespace SeeSharp.Shading.Materials;

public struct BsdfSample {
    public Vector3 Direction;
    public float Pdf;
    public float PdfReverse;

    /// <summary>
    /// Sample weight of the reflectance estimate, i.e., the product of
    /// BSDF and shading cosine divided by the pdf.
    /// </summary>
    public RgbColor Weight;

    public static BsdfSample Invalid
    => new() { Pdf = 0, PdfReverse = 0, Weight = RgbColor.Black };

    public static implicit operator bool(BsdfSample sample)
    => sample.Pdf > 0 && sample.PdfReverse > 0;
}