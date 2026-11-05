namespace PaperWindow;

/// <summary>
/// The mouse ink effects, the ink trail and the ink drops, drawn together on one click-through window.
/// Sharing one layer lets them blend like a single ink: where they meet, the stronger ink wins instead of the two
/// adding up into a darker seam, and a stroke flows out of the drop of its own click.
/// Polls the primary button and the cursor on a timer (no global mouse hook) and draws only the area the ink covers.
/// The window goes through the screen color filter, so the ink takes the profile's colors by itself.
/// </summary>
internal sealed class InkLayer : IDisposable
{
    static readonly Color Ink = Color.FromArgb(34, 30, 26);
    const int MaxAlpha = 210;

    readonly LayeredWindow _window = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    readonly List<InkTrail.Stroke> _strokes = new();
    readonly List<InkDrops.Drop> _drops = new();
    readonly Random _random = new();
    bool _trailOn, _dropsOn, _wasDown, _hideFromCapture;
    int _durationMs = 600;
    float _width = 6f;   // px at 96 dpi, at the newest end of a stroke while the button is held

    public InkLayer()
    {
        _timer.Tick += (_, _) => Tick();
        _window.HandleCreated += (_, _) => _window.SetCaptureExcluded(_hideFromCapture);
    }

    /// <summary>
    /// Turns the trail and the drops on/off. <paramref name="durationMs"/> is how long a stroke takes to fade and
    /// <paramref name="width"/> how thick it is while held (px at 96 dpi).
    /// </summary>
    public void Configure(bool trail, int durationMs, float width, bool drops, bool hideFromCapture)
    {
        _durationMs = Math.Max(1, durationMs);
        _width = Math.Max(1, width);
        _hideFromCapture = hideFromCapture;
        if (_window.IsHandleCreated) _window.SetCaptureExcluded(hideFromCapture);
        if (!trail) _strokes.Clear();
        if (!drops) _drops.Clear();
        bool start = (trail || drops) && !_timer.Enabled;
        _trailOn = trail;
        _dropsOn = drops;
        _timer.Enabled = trail || drops;
        if (start) _wasDown = Native.PrimaryButtonDown();   // a button already held when turning on is not a press
        if (!_timer.Enabled) _window.Hide();
    }

    public void KeepOnTop()
    {
        if (_window.Visible) _window.KeepOnTop();
    }

    void Tick()
    {
        long now = Environment.TickCount64;
        float scale = _window.DeviceDpi / 96f;
        bool down = Native.PrimaryButtonDown();
        var pos = Cursor.Position;
        if (down && !_wasDown)
        {
            InkDrops.Drop? drop = _dropsOn ? new(pos, now, _random.Next()) : null;
            if (drop is { } d) _drops.Add(d);
            if (_trailOn) _strokes.Add(new() { Origin = drop });
        }
        else if (!down && _wasDown && _strokes.Count > 0)
        {
            _strokes[^1].Release(now);
        }
        var current = down && _strokes.Count > 0 && _strokes[^1].ReleasedAt == null ? _strokes[^1] : null;
        current?.Add(pos, now, scale);   // every tick, even when still: the smoothing filter needs steady samples
        _wasDown = down;

        foreach (var s in _strokes) s.Expire(now, _durationMs);
        // Keep the stroke being drawn even when empty (button held still); drop finished strokes once faded.
        _strokes.RemoveAll(s => s.Points.Count == 0 && s != current);
        _drops.RemoveAll(d => !d.Alive(now));

        var strokes = _strokes.Where(s => s.PathPoints().Count >= 2).ToList();
        if (strokes.Count == 0 && _drops.Count == 0)
        {
            if (_window.Visible) _window.Hide();
            return;
        }
        using var bmp = Render(strokes, _drops, now, _durationMs, _width, scale, out var origin);
        _window.Present(bmp, origin);
        if (!_window.Visible) _window.Show();
    }

    /// <summary>
    /// Draws the strokes (each with at least two points to draw) and the drops as ink. Returns a bitmap covering just them;
    /// <paramref name="origin"/> is its top-left on screen. <paramref name="width"/> is the stroke width at 96 dpi.
    /// </summary>
    internal static Bitmap Render(IReadOnlyList<InkTrail.Stroke> strokes, IReadOnlyList<InkDrops.Drop> drops, long now,
        int durationMs, float width, float scale, out Point origin)
    {
        var paths = strokes.Select(s => (Stroke: s, Path: InkTrail.Smooth(s.PathPoints(), scale))).ToList();

        // Bounds: every stroke (with room for its width, or for swelling into a drop) and every drop with its splatters.
        float strokePad = Math.Max(width * scale, InkTrail.NeckWidth * InkDrops.FullRadius(scale)) + 2;
        float dropPad = InkDrops.Reach(scale) + 2;
        var boxes = paths.SelectMany(p => p.Path.Select(q => (q.X, q.Y, Pad: strokePad)))
            .Concat(drops.Select(d => ((float)d.Pos.X, (float)d.Pos.Y, Pad: dropPad)))
            .ToList();
        int left = (int)MathF.Floor(boxes.Min(b => b.Item1 - b.Pad)), top = (int)MathF.Floor(boxes.Min(b => b.Item2 - b.Pad));
        int right = (int)MathF.Ceiling(boxes.Max(b => b.Item1 + b.Pad)), bottom = (int)MathF.Ceiling(boxes.Max(b => b.Item2 + b.Pad));
        origin = new Point(left, top);
        int w = right - left + 1, h = bottom - top + 1;

        // Pass 1: trail and drops each on their own canvas, opaque, with life/strength as the gray level.
        // Opaque overlaps don't add up, so joints and crossings don't turn into darker spots.
        var trail = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var blots = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(trail))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (var (stroke, path) in paths)
            {
                InkTrail.Neck? neck = stroke.Origin is { } d && d.Alive(now)
                    ? new(d.CurrentRadius(now, scale), d.Strength(now),
                        InkTrail.Neck.LengthFor(InkDrops.FullRadius(scale), stroke.LaunchSpeed))
                    : null;
                InkTrail.DrawLife(g, path, now, durationMs, width * scale, stroke.Pressure(now), neck, stroke.Trimmed, origin);
            }
        }
        using (var g = Graphics.FromImage(blots))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (var d in drops) InkDrops.DrawLife(g, d, now, scale, origin);
        }

        // Pass 2: coverage x life into ink. Older parts of a stroke fade faster (life squared); where trail and drop meet,
        // the stronger ink wins, so the stroke flows into the blot instead of being laid on top of it.
        var rect = new Rectangle(0, 0, w, h);
        var td = trail.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var bd = blots.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        unsafe
        {
            uint ink = (uint)((Ink.R << 16) | (Ink.G << 8) | Ink.B);
            for (int y = 0; y < h; y++)
            {
                uint* trow = (uint*)((byte*)td.Scan0 + y * td.Stride);
                uint* brow = (uint*)((byte*)bd.Scan0 + y * bd.Stride);
                for (int x = 0; x < w; x++)
                {
                    uint t = trow[x], b = brow[x];
                    float life = ((t >> 16) & 0xFF) / 255f;
                    float trailInk = (t >> 24) / 255f * life * life;
                    float dropInk = (b >> 24) / 255f * (((b >> 16) & 0xFF) / 255f);
                    uint alpha = (uint)(MathF.Max(trailInk, dropInk) * MaxAlpha);
                    trow[x] = alpha == 0 ? 0 : (alpha << 24) | ink;
                }
            }
        }
        trail.UnlockBits(td);
        blots.UnlockBits(bd);
        return trail;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _window.Dispose();
    }
}
