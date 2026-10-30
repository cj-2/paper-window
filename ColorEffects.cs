namespace PaperWindow;

/// <summary>
/// A screen color filter: luminance is mapped onto a ramp from <see cref="Ink"/> (black) to <see cref="Paper"/> (white).
/// <see cref="Saturation"/> keeps part of the original color (0 = pure grayscale ramp).
/// </summary>
internal sealed record ColorEffect(string Id, string Name, float[] Ink, float[] Paper, float Saturation);

internal static class ColorEffects
{
    public static readonly ColorEffect[] All =
    [
        new("eink", "E-ink quente", [0.16f, 0.15f, 0.14f], [0.93f, 0.91f, 0.86f], 0f),
        new("neutro", "E-ink neutro (cinza)", [0.15f, 0.15f, 0.15f], [0.90f, 0.90f, 0.90f], 0f),
        new("sepia", "Sépia", [0.20f, 0.13f, 0.08f], [0.96f, 0.89f, 0.76f], 0f),
        new("jornal", "Jornal", [0.12f, 0.12f, 0.13f], [0.86f, 0.85f, 0.80f], 0f),
        new("kraft", "Papel kraft", [0.22f, 0.16f, 0.10f], [0.84f, 0.73f, 0.56f], 0f),
        new("colorido", "Papel colorido (cores suaves)", [0.12f, 0.11f, 0.10f], [0.95f, 0.93f, 0.88f], 0.45f),
    ];

    public static ColorEffect Find(string id) => All.FirstOrDefault(e => e.Id == id) ?? All[0];

    /// <summary>5x5 Magnification matrix ([r g b a 1] * M), blended with identity by <paramref name="strength"/> (0..1).</summary>
    public static float[] Matrix(ColorEffect e, float strength)
    {
        float[] luma = [0.299f, 0.587f, 0.114f];
        var m = Identity();
        for (int i = 0; i < 3; i++)         // input channel (row)
        for (int j = 0; j < 3; j++)         // output channel (column)
        {
            float mix = (1 - e.Saturation) * luma[i] + (i == j ? e.Saturation : 0);
            float effect = mix * (e.Paper[j] - e.Ink[j]);
            m[i * 5 + j] += strength * (effect - m[i * 5 + j]);
        }
        for (int j = 0; j < 3; j++) m[4 * 5 + j] = strength * e.Ink[j]; // offset row
        return m;
    }

    /// <summary>
    /// Warms the output by scaling down the green and (mostly) blue output columns, like a night-light.
    /// <paramref name="amount"/> 0..1; at 1 the white point is roughly a 2700 K incandescent tone.
    /// </summary>
    public static float[] ReduceBlueLight(float[] m, float amount)
    {
        float[] scale = [1f, 1f - 0.25f * amount, 1f - 0.65f * amount];
        for (int row = 0; row < 5; row++)
        for (int col = 0; col < 3; col++)
            m[row * 5 + col] *= scale[col];
        return m;
    }

    public static float[] Identity()
    {
        var m = new float[25];
        for (int i = 0; i < 5; i++) m[i * 5 + i] = 1;
        return m;
    }
}
