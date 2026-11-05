using System.Drawing.Drawing2D;

namespace PaperWindow;

/// <summary>
/// A drop of ink where the user clicks: it spreads into a small blot with a few splatters, then fades.
/// Like <see cref="InkTrail"/>, it polls (the primary button, on a timer; no global mouse hook), draws only the area
/// the drops cover and goes through the screen color filter, so the ink takes the profile's colors by itself.
/// </summary>
internal sealed class InkDrops : IDisposable
{
    static readonly Color Ink = Color.FromArgb(34, 30, 26);
    const float Radius = 6f;    // px at 96 dpi, of a fully spread blot
    const int SpreadMs = 140;   // time to spread out
    const int HoldMs = 260;     // stays fully inked until here
    const int LifeMs = 900;     // gone by here

    readonly LayeredWindow _window = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    readonly List<(Point Pos, long Time, int Seed)> _drops = new();
    readonly Random _random = new();
    bool _wasDown;
    bool _hideFromCapture;

    public InkDrops()
    {
        _timer.Tick += (_, _) => Tick();
        _window.HandleCreated += (_, _) => _window.SetCaptureExcluded(_hideFromCapture);
    }

    public void Configure(bool enabled, bool hideFromCapture)
    {
        _hideFromCapture = hideFromCapture;
        if (_window.IsHandleCreated) _window.SetCaptureExcluded(hideFromCapture);
        if (enabled == _timer.Enabled) return;
        _timer.Enabled = enabled;
        _wasDown = PrimaryDown();   // a button already held when turning on is not a click
        if (!enabled)
        {
            _drops.Clear();
            _window.Hide();
        }
    }

    public void KeepOnTop()
    {
        if (_window.Visible) _window.KeepOnTop();
    }

    /// <summary>GetAsyncKeyState reads the physical buttons, so follow the "swap buttons" setting by hand.</summary>
    static bool PrimaryDown() =>
        (Native.GetAsyncKeyState(SystemInformation.MouseButtonsSwapped ? Native.VK_RBUTTON : Native.VK_LBUTTON) & 0x8000) != 0;

    void Tick()
    {
        long now = Environment.TickCount64;
        bool down = PrimaryDown();
        if (down && !_wasDown) _drops.Add((Cursor.Position, now, _random.Next()));
        _wasDown = down;
        _drops.RemoveAll(d => now - d.Time >= LifeMs);

        if (_drops.Count == 0)
        {
            if (_window.Visible) _window.Hide();
            return;
        }
        using var bmp = RenderDrops(_drops, now, _window.DeviceDpi / 96f, out var origin);
        _window.Present(bmp, origin);
        if (!_window.Visible) _window.Show();
    }

    /// <summary>Draws the live drops; returns a bitmap covering just them, <paramref name="origin"/> being its top-left on screen.</summary>
    internal static Bitmap RenderDrops(IReadOnlyList<(Point Pos, long Time, int Seed)> drops, long now, float scale, out Point origin)
    {
        float r = Radius * scale;
        int pad = (int)Math.Ceiling(r * 2.3f) + 2;   // room for the farthest splatter
        int left = drops.Min(d => d.Pos.X) - pad, top = drops.Min(d => d.Pos.Y) - pad;
        int width = drops.Max(d => d.Pos.X) - left + pad + 1, height = drops.Max(d => d.Pos.Y) - top + pad + 1;
        origin = new Point(left, top);

        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var (pos, time, seed) in drops)
        {
            float age = now - time;
            float spread = 1 - MathF.Pow(1 - Math.Clamp(age / SpreadMs, 0, 1), 3);   // ease-out
            float fade = age <= HoldMs ? 1 : 1 - (age - HoldMs) / (LifeMs - HoldMs);
            int alpha = (int)(215 * Math.Clamp(fade, 0, 1));
            if (alpha <= 0) continue;
            DrawBlot(g, new PointF(pos.X - left, pos.Y - top), r * spread, seed, Color.FromArgb(alpha, Ink));
        }
        return bmp;
    }

    /// <summary>An uneven round blot plus a few splatter dots; the same seed always gives the same shape.</summary>
    static void DrawBlot(Graphics g, PointF center, float r, int seed, Color color)
    {
        if (r < 0.5f) return;
        var rnd = new Random(seed);
        using var brush = new SolidBrush(color);

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

        int dots = 3 + rnd.Next(3);
        for (int i = 0; i < dots; i++)
        {
            float a = (float)(rnd.NextDouble() * Math.PI * 2);
            float d = r * (1.3f + 0.7f * (float)rnd.NextDouble());
            float s = r * (0.12f + 0.16f * (float)rnd.NextDouble());
            g.FillEllipse(brush, center.X + d * MathF.Cos(a) - s, center.Y + d * MathF.Sin(a) - s, 2 * s, 2 * s);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _window.Dispose();
    }
}
