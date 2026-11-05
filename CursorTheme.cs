using System.Drawing.Drawing2D;

namespace PaperWindow;

internal sealed record CursorStyle(string Id, string Name);

internal static class CursorStyles
{
    public static readonly CursorStyle[] All =
    [
        new("seta", "Seta de tinta"),
        new("lapis", "Lápis"),
        new("pena", "Caneta bico de pena"),   // id kept from the old "Pena" so saved profiles keep their choice
        new("giz", "Giz de cera"),
    ];

    public static CursorStyle Find(string id) => All.FirstOrDefault(s => s.Id == id) ?? All[0];
}

/// <summary>
/// Replaces the system pointer, link hand and text I-beam with cursors drawn in the active profile's colors.
/// Windows draws the cursor on its own plane, outside the screen color filter, so every color of the artwork
/// is pushed through the same matrix here. The original cursors come back with <see cref="Restore"/> (and at logoff).
/// </summary>
internal static class CursorTheme
{
    static string? _applied;   // style + size + matrix currently installed; null when the system cursors are in use

    /// <summary>
    /// Installs the themed cursors for <paramref name="style"/>, or restores the system ones when null.
    /// <paramref name="size"/> is in px at the system's normal cursor size (32); a larger system cursor scales it up.
    /// At 32 px a thin outline on diagonals and tips has too few pixels to look smooth, hence the larger sizes.
    /// </summary>
    public static void Apply(CursorStyle? style, float[] matrix, int size)
    {
        if (style == null)
        {
            if (_applied != null) Restore();
            return;
        }
        size = size * Math.Max(32, Native.GetSystemMetrics(Native.SM_CXCURSOR)) / 32;
        string key = style.Id + "|" + size + "|" + string.Join(",", matrix.Select(v => MathF.Round(v, 3)));
        if (key == _applied) return;   // nothing changed; avoid re-installing on every slider tick

        var palette = new Palette(matrix);
        using (var pointer = DrawPointer(style, size, palette))
        {
            var hotspot = new Point((int)(1.5f * size / 32), (int)(1.5f * size / 32));
            Native.SetSystemCursor(MakeCursor(pointer, hotspot), Native.OCR_NORMAL);
            Native.SetSystemCursor(MakeCursor(pointer, hotspot), Native.OCR_HAND);
        }
        using (var beam = DrawIBeam(size, palette))
            Native.SetSystemCursor(MakeCursor(beam, new Point(size / 2, size / 2)), Native.OCR_IBEAM);
        _applied = key;
    }

    /// <summary>Reloads the user's own cursor scheme.</summary>
    public static void Restore()
    {
        Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        _applied = null;
    }

    /// <summary>The artwork's colors as they look on screen under the profile's matrix.</summary>
    internal sealed class Palette(float[] matrix)
    {
        Color C(int r, int g, int b) => ColorEffects.Transform(matrix, Color.FromArgb(r, g, b));

        public Color InkDark => C(20, 18, 16);
        public Color InkLight => C(104, 94, 84);
        public Color Paper => C(252, 249, 240);
    }

    /// <summary>Draws the pointer for a style on a 32-unit grid scaled to <paramref name="size"/> px, tip at the top-left.</summary>
    public static Bitmap DrawPointer(CursorStyle style, int size, Palette pal)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.ScaleTransform(size / 32f, size / 32f);
        switch (style.Id)
        {
            case "lapis": DrawPencil(g, pal); break;
            case "pena": DrawQuill(g, pal); break;
            case "giz": DrawCrayon(g, pal); break;
            default: DrawArrow(g, pal); break;
        }
        return bmp;
    }

    /// <summary>Flat ink arrow with a light stripe along its left edge, matching the pencil.</summary>
    static void DrawArrow(Graphics g, Palette pal)
    {
        using var arrow = new GraphicsPath();
        arrow.AddPolygon(new PointF[]
        {
            new(1.5f, 1.5f), new(1.5f, 23), new(6.5f, 18), new(10.5f, 26.5f),
            new(14, 25), new(10, 16.5f), new(17, 16.5f),
        });
        Silhouette(g, pal, arrow);
        Fill(g, pal.InkDark, arrow);
        using var stripe = new Pen(pal.Paper, 1.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(stripe, 3.9f, 7.2f, 3.9f, 17.6f);
    }

    // Pencil, dip pen and crayon are flat icons after the user's references: a dark silhouette with light details.

    /// <summary>Black pencil with a rounded end, a light stripe along the body and a light sharpened cone with a dark lead.</summary>
    static void DrawPencil(Graphics g, Palette pal)
    {
        using var place = Diagonal();
        const float h = 4.2f, coneEnd = 7.6f, end = 26.8f;
        using var body = Placed(place, p =>
        {
            p.AddLine(0, 0, coneEnd, -h);
            p.AddLine(coneEnd, -h, end, -h);
            p.AddArc(end - h, -h, 2 * h, 2 * h, -90, 180);
            p.AddLine(end, h, coneEnd, h);
            p.CloseFigure();
        });
        Silhouette(g, pal, body);
        Fill(g, pal.InkDark, body);

        // Sharpened wood: a cone whose base curves into the body.
        using (var cone = Placed(place, p =>
               {
                   p.AddLine(0.9f, -0.5f, coneEnd - 0.4f, -h + 0.75f);
                   p.AddBezier(coneEnd - 0.4f, -h + 0.75f, coneEnd + 2.2f, -h * 0.45f, coneEnd + 2.2f, h * 0.45f,
                       coneEnd - 0.4f, h - 0.75f);
                   p.AddLine(coneEnd - 0.4f, h - 0.75f, 0.9f, 0.5f);
                   p.CloseFigure();
               }))
            Fill(g, pal.Paper, cone);
        using (var lead = Shape(place, new PointF[] { new(0, 0), new(3f, -1.65f), new(3f, 1.65f) }))
            Fill(g, pal.InkDark, lead);

        var stripe = new[] { new PointF(11.8f, -2.15f), new PointF(24.6f, -2.15f) };
        place.TransformPoints(stripe);
        using var gleam = new Pen(pal.Paper, 1.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(gleam, stripe[0], stripe[1]);
    }

    /// <summary>Dip pen: a black fountain nib, a light ferrule and a long curved feather split by a light shaft, with two notches.</summary>
    static void DrawQuill(Graphics g, Palette pal)
    {
        using var place = Diagonal();
        const float vaneStart = 8.9f, vaneEnd = 32f;
        static float Shaft(float x) => -2.6f * MathF.Pow(Math.Clamp((x - 7.5f) / 24.5f, 0, 1), 1.8f);
        static float Bulge(float x, float max, float bias)
        {
            float u = Math.Clamp((x - vaneStart) / (vaneEnd - vaneStart), 0, 1);
            // MathF.Sin(MathF.PI) is a hair below zero, and a fractional power of that is NaN.
            return max * MathF.Pow(MathF.Max(0, MathF.Sin(MathF.PI * MathF.Pow(u, bias))), 0.55f);
        }
        static PointF Wide(float x, float k = 1) => new(x, Shaft(x) - k * Bulge(x, 5.4f, 1.15f));
        static PointF Narrow(float x, float k = 1) => new(x, Shaft(x) + k * Bulge(x, 2.1f, 1.0f));

        const int n = 28;
        var xs = Enumerable.Range(0, n + 1).Select(i => vaneStart + (vaneEnd - vaneStart) * i / n).ToArray();
        using var vane = Shape(place, xs.Select(x => Wide(x)).Concat(xs.Reverse().Select(x => Narrow(x))).ToArray());
        using var stem = Placed(place, p => p.AddRectangle(new RectangleF(7.9f, -0.95f, vaneStart - 7.9f, 1.9f)));
        using var ferrule = Placed(place, p => p.AddRectangle(new RectangleF(6.7f, -1.5f, 1.2f, 3f)));
        using var nib = Shape(place, new PointF[]
        {
            new(0, 0), new(3.2f, -1.45f), new(5.2f, -1.8f), new(6.7f, -1.3f), new(6.7f, 1.3f), new(5.2f, 1.8f), new(3.2f, 1.45f),
        });
        using var silhouette = new GraphicsPath();
        foreach (var part in new[] { vane, stem, ferrule, nib }) silhouette.AddPath(part, false);

        Silhouette(g, pal, silhouette);
        Fill(g, pal.InkDark, vane);
        Fill(g, pal.InkDark, stem);
        Fill(g, pal.InkDark, nib);
        Fill(g, pal.Paper, ferrule);

        // Notches cut into the wide side of the feather, pointing back towards the nib.
        foreach (float x in new[] { 18.5f, 25f })
        {
            using var notch = Shape(place, new[] { Wide(x, 1.2f), Wide(x - 1.3f, 1.2f), Wide(x - 4.2f, 0.45f) });
            Fill(g, pal.Paper, notch);
        }

        // Shaft: a light line from the ferrule almost to the tip of the feather.
        var shaft = Enumerable.Range(0, 12).Select(i => 8.2f + 21.3f * i / 11f).Select(x => new PointF(x, Shaft(x))).ToArray();
        place.TransformPoints(shaft);
        using (var pen = new Pen(pal.Paper, 0.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawCurve(pen, shaft);

        // Nib slit and breather hole.
        var slit = new[] { new PointF(1.1f, 0), new PointF(4.0f, 0) };
        var hole = new[] { new PointF(4.5f, 0) };
        place.TransformPoints(slit);
        place.TransformPoints(hole);
        using (var pen = new Pen(pal.Paper, 0.4f)) g.DrawLine(pen, slit[0], slit[1]);
        using (var b = new SolidBrush(pal.Paper)) g.FillEllipse(b, hole[0].X - 0.7f, hole[0].Y - 0.7f, 1.4f, 1.4f);
        using (var b = new SolidBrush(pal.InkDark)) g.FillEllipse(b, hole[0].X - 0.28f, hole[0].Y - 0.28f, 0.56f, 0.56f);
    }

    /// <summary>
    /// Crayon in outline style: thick dark border, light wrapper split by two bands, light cone with a blunt dark tip,
    /// and a heavier border on the shadow side.
    /// </summary>
    static void DrawCrayon(Graphics g, Palette pal)
    {
        using var place = Diagonal();
        const float h = 4.6f, coneEnd = 8f, end = 30f, line = 1.25f, side = 2.3f, r = 2.2f;
        using var body = Placed(place, p =>
        {
            p.AddLine(0.6f, -0.9f, coneEnd, -h + 0.4f);
            p.AddArc(coneEnd, -h, r, r, 180, 90);
            p.AddArc(end - r, -h, r, r, 270, 90);
            p.AddArc(end - r, h - r, r, r, 0, 90);
            p.AddArc(coneEnd, h - r, r, r, 90, 90);
            p.AddLine(coneEnd, h - 0.4f, 0.6f, 0.9f);
            p.AddArc(0, -0.9f, 1.8f, 1.8f, 90, 180);
            p.CloseFigure();
        });
        Silhouette(g, pal, body);
        Fill(g, pal.InkDark, body);

        // Light inside: the cone, then the wrapper in three parts separated by the bands.
        using (var cone = Shape(place, new PointF[]
               {
                   new(2.9f, -1.0f), new(coneEnd + 0.3f, -h + line + 0.3f), new(coneEnd + 0.3f, h - side - 0.2f), new(2.9f, 0.1f),
               }))
            Fill(g, pal.Paper, cone);
        foreach (var (x0, x1) in new[] { (coneEnd + 1.5f, 11.4f), (12.6f, 14.6f), (15.8f, end - line) })
        {
            using var part = Placed(place, p => AddRoundRect(p, x0, -h + line, x1, h - side, 0.7f));
            Fill(g, pal.Paper, part);
        }
    }

    /// <summary>Text cursor: an ink I-beam with a paper outline and a soft shadow, centered.</summary>
    public static Bitmap DrawIBeam(int size, Palette pal)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.ScaleTransform(size / 32f, size / 32f);
        using var beam = new GraphicsPath();
        beam.AddLine(12.5f, 6, 19.5f, 6);
        beam.CloseFigure();
        beam.AddLine(16, 6, 16, 26);
        beam.CloseFigure();
        beam.AddLine(12.5f, 26, 19.5f, 26);

        using var body = (GraphicsPath)beam.Clone();
        using (var widen = new Pen(Color.Black, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            body.Widen(widen);
        DropShadow(g, body);
        using (var paper = new SolidBrush(pal.Paper))
            g.FillPath(paper, body);
        using (var ink = new LinearGradientBrush(new PointF(0, 6), new PointF(0, 26), pal.InkLight, pal.InkDark))
        using (var stroke = new Pen(ink, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawPath(stroke, beam);
        return bmp;
    }

    // ---- Drawing helpers (local shapes are built along +x and placed with a matrix) ----

    static GraphicsPath Shape(Matrix place, PointF[] local)
    {
        var path = new GraphicsPath();
        path.AddPolygon(local);
        path.Transform(place);
        return path;
    }

    /// <summary>Local frame for the pen-like pointers: +x runs from the tip at the hotspot down-right at 45°.</summary>
    static Matrix Diagonal()
    {
        var m = new Matrix();
        m.Translate(1.5f, 1.5f);
        m.Rotate(45);
        return m;
    }

    static GraphicsPath Placed(Matrix place, Action<GraphicsPath> build)
    {
        var path = new GraphicsPath();
        build(path);
        path.Transform(place);
        return path;
    }

    static void Fill(Graphics g, Color color, GraphicsPath path)
    {
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    static void AddRoundRect(GraphicsPath p, float x0, float y0, float x1, float y1, float r)
    {
        float d = 2 * r;
        p.AddArc(x0, y0, d, d, 180, 90);
        p.AddArc(x1 - d, y0, d, d, 270, 90);
        p.AddArc(x1 - d, y1 - d, d, d, 0, 90);
        p.AddArc(x0, y1 - d, d, d, 90, 90);
        p.CloseFigure();
    }

    /// <summary>Shadow plus a paper outline, so a dark pointer stays visible on dark backgrounds.</summary>
    static void Silhouette(Graphics g, Palette pal, GraphicsPath shape)
    {
        DropShadow(g, shape);
        using var outline = new Pen(pal.Paper, 1.9f) { LineJoin = LineJoin.Round };
        g.DrawPath(outline, shape);
    }

    /// <summary>Soft shadow below and to the right of <paramref name="shape"/>, like the system cursors.</summary>
    static void DropShadow(Graphics g, GraphicsPath shape)
    {
        using var shadow = (GraphicsPath)shape.Clone();
        using (var offset = new Matrix())
        {
            offset.Translate(0.9f, 1.4f);
            shadow.Transform(offset);
        }
        using (var core = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            g.FillPath(core, shadow);
        foreach (var (width, alpha) in new[] { (1.6f, 28), (3.2f, 12) })
        {
            using var blur = new Pen(Color.FromArgb(alpha, 0, 0, 0), width) { LineJoin = LineJoin.Round };
            g.DrawPath(blur, shadow);
        }
    }

    static IntPtr MakeCursor(Bitmap bmp, Point hotspot)
    {
        // GetHicon keeps the alpha channel; turn that icon into a cursor with the right hotspot.
        IntPtr icon = bmp.GetHicon();
        Native.GetIconInfo(icon, out var info);
        Native.DestroyIcon(icon);
        info.fIcon = false;
        info.xHotspot = hotspot.X;
        info.yHotspot = hotspot.Y;
        IntPtr cursor = Native.CreateIconIndirect(ref info);
        Native.DeleteObject(info.hbmColor);
        Native.DeleteObject(info.hbmMask);
        return cursor;
    }
}
