using System.Drawing.Drawing2D;

namespace PaperWindow;

/// <summary>
/// Drawing of the ink drops: where the user clicks, a drop spreads into a small blot with a few splatters, then fades.
/// The state and the window live in <see cref="InkLayer"/>.
/// </summary>
internal static class InkDrops
{
    const float Radius = 6f;    // px at 96 dpi, of a fully spread blot
    const int SpreadMs = 140;   // time to spread out
    const int HoldMs = 260;     // stays fully inked until here
    const int LifeMs = 900;     // gone by here

    internal readonly record struct Drop(Point Pos, long Time, int Seed)
    {
        public bool Alive(long now) => now - Time < LifeMs;

        /// <summary>Current blot radius in screen px: grows with an ease-out while spreading.</summary>
        public float CurrentRadius(long now, float scale) =>
            Radius * scale * (1 - MathF.Pow(1 - Math.Clamp((now - Time) / (float)SpreadMs, 0, 1), 3));

        /// <summary>1 while fully inked, then down to 0.</summary>
        public float Strength(long now)
        {
            float age = now - Time;
            return age <= HoldMs ? 1 : Math.Clamp(1 - (age - HoldMs) / (LifeMs - HoldMs), 0, 1);
        }
    }

    /// <summary>Radius of a fully spread blot, in screen px.</summary>
    internal static float FullRadius(float scale) => Radius * scale;

    /// <summary>How far from its center a drop can reach (farthest splatter), in screen px.</summary>
    internal static float Reach(float scale) => Radius * scale * 2.3f;

    /// <summary>
    /// Draws <paramref name="drop"/> opaque with its strength as the gray level, for <see cref="InkLayer"/> to turn into ink.
    /// <paramref name="offset"/> is the canvas' top-left on screen.
    /// </summary>
    internal static void DrawLife(Graphics g, Drop drop, long now, float scale, Point offset)
    {
        float r = drop.CurrentRadius(now, scale);
        int level = (int)(drop.Strength(now) * 255);
        if (r < 0.5f || level <= 0) return;
        var center = new PointF(drop.Pos.X - offset.X, drop.Pos.Y - offset.Y);
        var rnd = new Random(drop.Seed);   // the same seed always gives the same shape
        using var brush = new SolidBrush(Color.FromArgb(255, level, level, level));

        // An uneven round blot...
        const int lobes = 12;
        float turn = (float)(rnd.NextDouble() * Math.PI * 2);
        var edge = new PointF[lobes];
        for (int i = 0; i < lobes; i++)
        {
            float a = turn + i * MathF.PI * 2 / lobes;
            float d = r * (0.84f + 0.24f * (float)rnd.NextDouble());
            edge[i] = new PointF(center.X + d * MathF.Cos(a), center.Y + d * MathF.Sin(a));
        }
        g.FillClosedCurve(brush, edge, FillMode.Winding, 0.5f);

        // ...plus a few splatter dots that fly out as it spreads.
        int dots = 3 + rnd.Next(3);
        for (int i = 0; i < dots; i++)
        {
            float a = (float)(rnd.NextDouble() * Math.PI * 2);
            float d = r * (1.3f + 0.7f * (float)rnd.NextDouble());
            float s = r * (0.12f + 0.16f * (float)rnd.NextDouble());
            g.FillEllipse(brush, center.X + d * MathF.Cos(a) - s, center.Y + d * MathF.Sin(a) - s, 2 * s, 2 * s);
        }
    }
}
