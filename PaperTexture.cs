using System.Drawing.Imaging;

namespace PaperWindow;

/// <summary>
/// Look of a paper texture: weights of each noise octave (coarse blotches, mid fibers, fine tooth, white-noise grain),
/// a gamma that turns the mix into sparse dark specks as it grows, and the tint of those specks.
/// </summary>
internal sealed record TextureStyle(string Id, string Name,
    float Coarse, float Mid, float Fine, float Grain, float Gamma, byte R, byte G, byte B);

internal static class TextureStyles
{
    public static readonly TextureStyle[] All =
    [
        new("fibras", "Fibras", 0.30f, 0.35f, 0.20f, 0.15f, 1.6f, 70, 58, 44),
        new("grao", "Grão fino", 0.10f, 0.15f, 0.35f, 0.40f, 1.4f, 60, 55, 50),
        new("jornal", "Jornal (pontilhado)", 0.10f, 0.15f, 0.25f, 0.50f, 2.6f, 40, 40, 44),
        new("aquarela", "Aquarela", 0.60f, 0.30f, 0.05f, 0.05f, 1.2f, 90, 72, 50),
    ];

    public static TextureStyle Find(string id) => All.FirstOrDefault(s => s.Id == id) ?? All[0];
}

/// <summary>Procedural paper grain: a seamless tile per style, tiled to any size.</summary>
internal static class PaperTexture
{
    const int Tile = 512;

    // Opacity at full texture strength; the user's opacity slider scales the whole window below this.
    const float MaxAlpha = 64f;

    static readonly Dictionary<string, int[]> Tiles = new();

    public static Bitmap Create(TextureStyle style, int width, int height)
    {
        if (!Tiles.TryGetValue(style.Id, out var tile))
            Tiles[style.Id] = tile = BuildTile(style);

        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        unsafe
        {
            for (int y = 0; y < height; y++)
            {
                int* row = (int*)((byte*)data.Scan0 + y * data.Stride);
                int tileRow = (y % Tile) * Tile;
                for (int x = 0; x < width; x++)
                    row[x] = tile[tileRow + x % Tile];
            }
        }
        bmp.UnlockBits(data);
        return bmp;
    }

    static int[] BuildTile(TextureStyle s)
    {
        var rng = new Random(1337);
        var coarse = Lattice(rng, 8);
        var mid = Lattice(rng, 32);
        var fine = Lattice(rng, 128);
        var pixels = new int[Tile * Tile];

        for (int y = 0; y < Tile; y++)
        for (int x = 0; x < Tile; x++)
        {
            float n = s.Coarse * Sample(coarse, 8, x, y)
                    + s.Mid * Sample(mid, 32, x, y)
                    + s.Fine * Sample(fine, 128, x, y)
                    + s.Grain * (float)rng.NextDouble();
            n = MathF.Pow(Math.Clamp(n, 0f, 1f), s.Gamma);
            byte a = (byte)(n * MaxAlpha);
            // Layered windows expect premultiplied alpha; GDI+ GetHbitmap premultiplies for us.
            pixels[y * Tile + x] = (a << 24) | (s.R << 16) | (s.G << 8) | s.B;
        }
        return pixels;
    }

    static float[] Lattice(Random rng, int cells)
    {
        var g = new float[cells * cells];
        for (int i = 0; i < g.Length; i++) g[i] = (float)rng.NextDouble();
        return g;
    }

    // Smooth value noise on a wrapping lattice, so the tile is seamless.
    static float Sample(float[] g, int cells, int x, int y)
    {
        float fx = x * cells / (float)Tile, fy = y * cells / (float)Tile;
        int x0 = (int)fx, y0 = (int)fy;
        float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
        float a = g[y0 * cells + x0], b = g[y0 * cells + x1];
        float c = g[y1 * cells + x0], d = g[y1 * cells + x1];
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    static float Smooth(float t) => t * t * (3 - 2 * t);
    static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
