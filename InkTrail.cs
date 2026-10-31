using System.Drawing.Drawing2D;

namespace PaperWindow;

/// <summary>
/// A fading ink stroke that follows the mouse, like a pen on paper or e-ink ghosting.
/// Polls the cursor position on a timer (no global mouse hook) and draws only the area the stroke covers.
/// The window goes through the screen color filter, so the ink takes the profile's colors by itself.
/// </summary>
internal sealed class InkTrail : IDisposable
{
    static readonly Color Ink = Color.FromArgb(34, 30, 26);
    const float MaxWidth = 4f;   // px at 96 dpi, at the newest end of the stroke

    readonly LayeredWindow _window = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    readonly List<(Point Pos, long Time)> _points = new();
    int _durationMs = 600;
    bool _hideFromCapture;

    public InkTrail()
    {
        _timer.Tick += (_, _) => Tick();
        _window.HandleCreated += (_, _) => _window.SetCaptureExcluded(_hideFromCapture);
    }

    /// <summary>Turns the trail on/off and sets how long a stroke takes to fade (ms).</summary>
    public void Configure(bool enabled, int durationMs, bool hideFromCapture)
    {
        _durationMs = Math.Max(1, durationMs);
        _hideFromCapture = hideFromCapture;
        if (_window.IsHandleCreated) _window.SetCaptureExcluded(hideFromCapture);
        if (enabled == _timer.Enabled) return;
        _timer.Enabled = enabled;
        if (!enabled)
        {
            _points.Clear();
            _window.Hide();
        }
    }

    public void KeepOnTop()
    {
        if (_window.Visible) _window.KeepOnTop();
    }

    void Tick()
    {
        long now = Environment.TickCount64;
        var pos = Cursor.Position;
        if (_points.Count == 0 || _points[^1].Pos != pos) _points.Add((pos, now));
        _points.RemoveAll(p => now - p.Time > _durationMs);

        if (_points.Count < 2)
        {
            if (_window.Visible) _window.Hide();
            return;
        }
        Render(now);
    }

    void Render(long now)
    {
        using var bmp = RenderStroke(_points, now, _durationMs, _window.DeviceDpi / 96f, out var origin);
        _window.Present(bmp, origin);
        if (!_window.Visible) _window.Show();
    }

    /// <summary>
    /// Draws the stroke through <paramref name="points"/> (screen pixels, with the time each was sampled).
    /// Returns a bitmap covering just the stroke; <paramref name="origin"/> is its top-left on screen.
    /// </summary>
    internal static Bitmap RenderStroke(IReadOnlyList<(Point Pos, long Time)> points, long now, int durationMs, float scale,
        out Point origin)
    {
        var path = Smooth(points, scale);
        int pad = (int)Math.Ceiling(MaxWidth * scale) + 2;
        int left = (int)Math.Floor(path.Min(p => p.X)) - pad, top = (int)Math.Floor(path.Min(p => p.Y)) - pad;
        int width = (int)Math.Ceiling(path.Max(p => p.X)) - left + pad + 1;
        int height = (int)Math.Ceiling(path.Max(p => p.Y)) - top + pad + 1;
        origin = new Point(left, top);

        // Pass 1: draw the stroke opaque, encoding each segment's remaining life (0..1) in its gray level.
        // Opaque overlaps don't add up, so the joints between segments no longer turn into darker dots,
        // and newer (livelier) segments are drawn last so they win at the joints.
        var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 1; i < path.Count; i++)
            {
                float life = 1f - (now - path[i].Time) / durationMs;
                if (life <= 0) continue;
                int level = (int)(Math.Clamp(life, 0f, 1f) * 255);
                using var pen = new Pen(Color.FromArgb(255, level, level, level), Math.Max(0.6f, MaxWidth * scale * life))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                g.DrawLine(pen, path[i - 1].X - left, path[i - 1].Y - top, path[i].X - left, path[i].Y - top);
            }
        }

        // Pass 2: turn coverage + life into ink: older parts fade out, the coverage keeps the antialiased edges.
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        unsafe
        {
            int ink = (Ink.R << 16) | (Ink.G << 8) | Ink.B;
            for (int y = 0; y < height; y++)
            {
                uint* row = (uint*)((byte*)data.Scan0 + y * data.Stride);
                for (int x = 0; x < width; x++)
                {
                    uint px = row[x];
                    uint coverage = px >> 24;
                    if (coverage == 0) continue;
                    float life = ((px >> 16) & 0xFF) / 255f;
                    uint alpha = (uint)(coverage * 210 / 255 * life * life);
                    row[x] = (alpha << 24) | (uint)ink;
                }
            }
        }
        bmp.UnlockBits(data);
        return bmp;
    }

    /// <summary>
    /// Catmull-Rom curve through the sampled points, subdivided every few pixels, so fast flicks
    /// (where samples are far apart) draw a rounded stroke instead of a zigzag of straight lines.
    /// </summary>
    static List<(float X, float Y, float Time)> Smooth(IReadOnlyList<(Point Pos, long Time)> pts, float scale)
    {
        var result = new List<(float X, float Y, float Time)> { (pts[0].Pos.X, pts[0].Pos.Y, pts[0].Time) };
        for (int i = 1; i < pts.Count; i++)
        {
            var p0 = pts[Math.Max(i - 2, 0)].Pos;
            var p1 = pts[i - 1].Pos;
            var p2 = pts[i].Pos;
            var p3 = pts[Math.Min(i + 1, pts.Count - 1)].Pos;
            float dist = MathF.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            int steps = Math.Max(1, (int)MathF.Ceiling(dist / (3f * scale)));
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                float CatmullRom(float a, float b, float c, float d) =>
                    0.5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (3 * b - a - 3 * c + d) * t3);
                result.Add((CatmullRom(p0.X, p1.X, p2.X, p3.X), CatmullRom(p0.Y, p1.Y, p2.Y, p3.Y),
                    pts[i - 1].Time + (pts[i].Time - pts[i - 1].Time) * t));
            }
        }
        return result;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _window.Dispose();
    }
}
