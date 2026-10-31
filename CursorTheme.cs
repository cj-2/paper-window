using System.Drawing.Drawing2D;

namespace PaperWindow;

internal sealed record CursorStyle(string Id, string Name);

internal static class CursorStyles
{
    public static readonly CursorStyle[] All =
    [
        new("seta", "Seta de tinta"),
        new("lapis", "Lápis"),
        new("pena", "Pena"),
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
    static string? _applied;   // style + matrix currently installed; null when the system cursors are in use

    /// <summary>Installs the themed cursors for <paramref name="style"/>, or restores the system ones when null.</summary>
    public static void Apply(CursorStyle? style, float[] matrix)
    {
        if (style == null)
        {
            if (_applied != null) Restore();
            return;
        }
        string key = style.Id + "|" + string.Join(",", matrix.Select(v => MathF.Round(v, 3)));
        if (key == _applied) return;   // nothing changed; avoid re-installing on every slider tick

        int size = Math.Max(32, Native.GetSystemMetrics(Native.SM_CXCURSOR));
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
        public Color Ink => C(52, 46, 40);
        public Color InkLight => C(104, 94, 84);
        public Color Paper => C(252, 249, 240);
        public Color Wood => C(232, 198, 150);
        public Color WoodDark => C(178, 136, 88);
        public Color Graphite => C(60, 60, 66);
        public Color Metal => C(214, 214, 220);
        public Color MetalDark => C(118, 118, 126);
        public Color Eraser => C(232, 146, 138);
        public Color EraserDark => C(170, 92, 88);
        public Color Shaft => C(240, 230, 208);
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
            default: DrawArrow(g, pal); break;
        }
        return bmp;
    }

    static void DrawArrow(Graphics g, Palette pal)
    {
        using var arrow = new GraphicsPath();
        arrow.AddPolygon(new PointF[]
        {
            new(1.5f, 1.5f), new(1.5f, 23), new(6.5f, 18), new(10.5f, 26.5f),
            new(14, 25), new(10, 16.5f), new(17, 16.5f),
        });
        DropShadow(g, arrow);
        using (var outline = new Pen(pal.Paper, 1.9f) { LineJoin = LineJoin.Round })
            g.DrawPath(outline, arrow);
        using (var fill = new LinearGradientBrush(new PointF(1.5f, 1.5f), new PointF(13, 26), pal.InkLight, pal.InkDark))
            g.FillPath(fill, arrow);
        // A thin gleam along the left edge gives the ink some volume.
        using (var gleam = new Pen(Color.FromArgb(120, pal.Paper), 0.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(gleam, 3, 5.5f, 3, 19);
    }

    static void DrawPencil(Graphics g, Palette pal)
    {
        // Built along +x from the tip, then turned 45° so the tip points to the top-left corner.
        using var place = new Matrix();
        place.Translate(1.5f, 1.5f);
        place.Rotate(45);

        using var silhouette = Shape(place, new PointF[]
        {
            new(0, 0), new(8, -3.6f), new(29.2f, -3.6f), new(30, -2.6f), new(30, 2.6f), new(29.2f, 3.6f), new(8, 3.6f),
        });
        DropShadow(g, silhouette);
        using (var outline = new Pen(pal.Paper, 1.9f) { LineJoin = LineJoin.Round })
            g.DrawPath(outline, silhouette);

        // Sharpened wood, lighter in the middle like a cone catching the light.
        using (var wood = Shape(place, new PointF[] { new(2.8f, -1.25f), new(8, -3.6f), new(8, 3.6f), new(2.8f, 1.25f) }))
        using (var woodBrush = Across(place, -3.6f, 3.6f, pal.WoodDark, pal.Wood, pal.WoodDark))
            g.FillPath(woodBrush, wood);
        using (var tip = Shape(place, new PointF[] { new(0, 0), new(2.8f, -1.25f), new(2.8f, 1.25f) }))
        using (var graphite = new SolidBrush(pal.Graphite))
            g.FillPath(graphite, tip);

        // Hexagonal painted body: three facets in different tones, with the scalloped edge left by the sharpener.
        using (var body = Shape(place, new PointF[]
               {
                   new(8, -3.6f), new(9.6f, -2.4f), new(8.4f, -1.2f), new(9.6f, 0), new(8.4f, 1.2f), new(9.6f, 2.4f),
                   new(8, 3.6f), new(23, 3.6f), new(23, -3.6f),
               }))
        {
            var clip = g.Clip;
            g.SetClip(body);
            FillLocal(g, place, new RectangleF(7, -3.6f, 17, 2.4f), pal.InkLight);
            FillLocal(g, place, new RectangleF(7, -1.2f, 17, 2.4f), pal.Ink);
            FillLocal(g, place, new RectangleF(7, 1.2f, 17, 2.4f), pal.InkDark);
            g.Clip = clip;
        }

        // Metal band with two grooves, then the eraser.
        using (var band = Shape(place, new PointF[] { new(23, -3.6f), new(26, -3.6f), new(26, 3.6f), new(23, 3.6f) }))
        using (var metal = Across(place, -3.6f, 3.6f, pal.MetalDark, pal.Metal, pal.MetalDark))
            g.FillPath(metal, band);
        using (var groove = new Pen(pal.MetalDark, 0.35f))
        {
            foreach (float x in new[] { 24f, 25f })
            {
                var line = new[] { new PointF(x, -3.6f), new PointF(x, 3.6f) };
                place.TransformPoints(line);
                g.DrawLine(groove, line[0], line[1]);
            }
        }
        using (var eraser = Shape(place, new PointF[]
               {
                   new(26, -3.6f), new(29.2f, -3.6f), new(30, -2.6f), new(30, 2.6f), new(29.2f, 3.6f), new(26, 3.6f),
               }))
        using (var rubber = Across(place, -3.6f, 3.6f, pal.Eraser, pal.Eraser, pal.EraserDark))
            g.FillPath(rubber, eraser);
    }

    static void DrawQuill(Graphics g, Palette pal)
    {
        using var place = new Matrix();
        place.Translate(1.5f, 1.5f);
        place.Rotate(45);

        // Feather vane: wide on the upper side, narrow below, tapering to a rounded end.
        using var vane = new GraphicsPath();
        vane.AddBezier(6, 0, 10, -7.5f, 21, -9, 31, -5.2f);
        vane.AddBezier(31, -5.2f, 27, -1.6f, 18, 2.8f, 6, 0);
        vane.CloseFigure();
        vane.Transform(place);
        using var nib = Shape(place, new PointF[] { new(0, 0), new(6.5f, -1.4f), new(6.5f, 1.4f) });
        using var silhouette = new GraphicsPath();
        silhouette.AddPath(vane, false);
        silhouette.AddPath(nib, false);

        DropShadow(g, silhouette);
        using (var outline = new Pen(pal.Paper, 1.9f) { LineJoin = LineJoin.Round })
            g.DrawPath(outline, silhouette);

        // Dark at the base, lighter towards the end of the feather.
        using (var featherBrush = Along(place, 6, 31, pal.InkDark, pal.InkLight))
            g.FillPath(featherBrush, vane);

        // Barbs: fine light strokes from the shaft out to the edge, kept inside the vane.
        var clip = g.Clip;
        g.SetClip(vane);
        using (var barb = new Pen(Color.FromArgb(70, pal.Paper), 0.35f))
        {
            for (float x = 8.5f; x < 30; x += 1.7f)
            {
                float y = ShaftY(x);
                var up = new[] { new PointF(x, y), new PointF(x + 3.4f, y - 6f) };
                var down = new[] { new PointF(x, y), new PointF(x + 2.4f, y + 3.2f) };
                place.TransformPoints(up);
                place.TransformPoints(down);
                g.DrawLine(barb, up[0], up[1]);
                g.DrawLine(barb, down[0], down[1]);
            }
        }
        g.Clip = clip;

        // Shaft: thick near the nib, thinning out.
        foreach (var (from, to, width) in new[] { (6f, 16f, 1.1f), (15f, 30f, 0.6f) })
        {
            var pts = Enumerable.Range(0, 9).Select(i => from + (to - from) * i / 8f).Select(x => new PointF(x, ShaftY(x))).ToArray();
            place.TransformPoints(pts);
            using var shaft = new Pen(pal.Shaft, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawCurve(shaft, pts);
        }

        // Metal nib with its slit and breather hole.
        using (var metal = Across(place, -1.4f, 1.4f, pal.MetalDark, pal.Metal, pal.MetalDark))
            g.FillPath(metal, nib);
        var slit = new[] { new PointF(1.2f, 0), new PointF(4.6f, 0) };
        place.TransformPoints(slit);
        using (var slitPen = new Pen(pal.InkDark, 0.4f))
            g.DrawLine(slitPen, slit[0], slit[1]);
        var hole = new[] { new PointF(4.8f, 0) };
        place.TransformPoints(hole);
        using (var holeBrush = new SolidBrush(pal.InkDark))
            g.FillEllipse(holeBrush, hole[0].X - 0.45f, hole[0].Y - 0.45f, 0.9f, 0.9f);

        static float ShaftY(float x) => -4.8f * MathF.Pow(Math.Clamp((x - 6) / 25f, 0, 1), 1.4f);
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

    static void FillLocal(Graphics g, Matrix place, RectangleF local, Color color)
    {
        using var path = new GraphicsPath();
        path.AddRectangle(local);
        path.Transform(place);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    /// <summary>Gradient across the shape's width (local y), e.g. to round a cylinder: edge, middle, edge.</summary>
    static LinearGradientBrush Across(Matrix place, float y0, float y1, Color edge0, Color middle, Color edge1)
    {
        var brush = new LinearGradientBrush(new PointF(0, y0 - 0.01f), new PointF(0, y1 + 0.01f), edge0, edge1)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = new[] { edge0, middle, edge1 },
                Positions = new[] { 0f, 0.45f, 1f },
            },
        };
        brush.MultiplyTransform(place);
        return brush;
    }

    /// <summary>Gradient along the shape's length (local x).</summary>
    static LinearGradientBrush Along(Matrix place, float x0, float x1, Color from, Color to)
    {
        var brush = new LinearGradientBrush(new PointF(x0 - 0.01f, 0), new PointF(x1 + 0.01f, 0), from, to);
        brush.MultiplyTransform(place);
        return brush;
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
