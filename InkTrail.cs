using System.Drawing.Drawing2D;

namespace PaperWindow;

/// <summary>
/// Drawing of the ink trail: a stroke per press of the primary button, thick while held, thinning after release,
/// fading with age, and flowing out of the ink drop of the same click so the two read as one splash of ink.
/// The state and the window live in <see cref="InkLayer"/>.
/// </summary>
internal static class InkTrail
{
    const float ReleasedWidth = 0.3f;   // fraction of the width a stroke thins down to after the button is released
    const int ThinMs = 220;             // how long the thinning takes

    const float MinStep = 3f;   // px at 96 dpi the smoothed position must move before it becomes a new point
    const int LaunchMs = 80;    // how long the start of a stroke is watched to measure its speed

    static float Distance(PointF a, PointF b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// The points of one press; a new press does not join the previous stroke while that one is still fading.
    /// Cursor samples go through a <see cref="OneEuroFilter"/> and are kept only every few pixels, which takes out
    /// hand tremor and the pixel staircase of slow diagonals. While the button is held, <see cref="Tip"/> joins the
    /// smoothed stroke to the real cursor so the ink never trails behind it.
    /// </summary>
    internal sealed class Stroke
    {
        public readonly List<(PointF Pos, long Time)> Points = new();
        public (PointF Pos, long Time)? Tip;
        public long? ReleasedAt;
        /// <summary>The drop of the same click, which the stroke flows out of; null when drops are off.</summary>
        public InkDrops.Drop? Origin;
        /// <summary>Speed (screen px/s) over the first moments of the stroke: a fast start stretches the flow out of the drop.</summary>
        public float LaunchSpeed { get; private set; }
        /// <summary>Length of the start that has already faded away, so the flow out of the drop stays anchored at the click.</summary>
        public float Trimmed { get; private set; }
        readonly OneEuroFilter _filter = new();
        (PointF Pos, long Time)? _start;

        /// <summary>Takes a cursor sample while the button is held.</summary>
        public void Add(Point cursor, long now, float scale)
        {
            Tip = (cursor, now);
            var smoothed = _filter.Next(cursor, now);
            _start ??= (cursor, now);
            long elapsed = now - _start.Value.Time;
            if (elapsed > 0 && elapsed <= LaunchMs) LaunchSpeed = Distance(_start.Value.Pos, cursor) / (elapsed / 1000f);
            if (Points.Count == 0) { Points.Add((cursor, now)); return; }   // start exactly where the button went down
            var last = Points[^1].Pos;
            float dx = smoothed.X - last.X, dy = smoothed.Y - last.Y;
            if (dx * dx + dy * dy >= MinStep * scale * MinStep * scale) Points.Add((smoothed, now));
        }

        /// <summary>Ends the stroke where the cursor really stopped.</summary>
        public void Release(long now)
        {
            if (ReleasedAt != null) return;
            ReleasedAt = now;
            if (Tip is { } tip && (Points.Count == 0 || Points[^1].Pos != tip.Pos)) Points.Add(tip);
            Tip = null;
        }

        /// <summary>Drops the points older than <paramref name="durationMs"/>, keeping count of the length they covered.</summary>
        public void Expire(long now, int durationMs)
        {
            while (Points.Count > 0 && now - Points[0].Time > durationMs)
            {
                if (Points.Count > 1) Trimmed += Distance(Points[0].Pos, Points[1].Pos);
                Points.RemoveAt(0);
            }
        }

        /// <summary>The points to draw: the kept ones plus, while held, the real cursor.</summary>
        public List<(PointF Pos, long Time)> PathPoints()
        {
            var pts = new List<(PointF Pos, long Time)>(Points);
            if (Tip is { } tip && (pts.Count == 0 || pts[^1].Pos != tip.Pos)) pts.Add(tip);
            return pts;
        }

        /// <summary>Width factor: full while held, then easing down to <see cref="ReleasedWidth"/> after release.</summary>
        public float Pressure(long now)
        {
            if (ReleasedAt is not long released) return 1f;
            float t = Math.Clamp((now - released) / (float)ThinMs, 0, 1);
            float eased = 1 - (1 - t) * (1 - t);
            return 1 - (1 - ReleasedWidth) * eased;
        }
    }

    /// <summary>
    /// The "1€ filter" (Casiez, Roussel and Vogel, CHI 2012): a low-pass filter whose cutoff rises with speed, so slow
    /// movement (where tremor shows) is smoothed hard while fast flicks follow the cursor with little lag.
    /// </summary>
    internal sealed class OneEuroFilter
    {
        const float MinCutoff = 1.5f;   // Hz, smoothing when the cursor is still or slow
        const float Beta = 0.01f;       // cutoff added per px/s of speed
        const float SpeedCutoff = 1f;   // Hz, smoothing of the speed estimate itself
        PointF _value, _speed;
        long _time;
        bool _started;

        public PointF Next(PointF sample, long now)
        {
            if (!_started)
            {
                (_value, _time, _started) = (sample, now, true);
                return sample;
            }
            float dt = Math.Max(1, now - _time) / 1000f;
            _time = now;
            var raw = new PointF((sample.X - _value.X) / dt, (sample.Y - _value.Y) / dt);
            _speed = Lerp(_speed, raw, Alpha(SpeedCutoff, dt));
            float speed = MathF.Sqrt(_speed.X * _speed.X + _speed.Y * _speed.Y);
            _value = Lerp(_value, sample, Alpha(MinCutoff + Beta * speed, dt));
            return _value;
        }

        static float Alpha(float cutoff, float dt) => 1 / (1 + 1 / (2 * MathF.PI * cutoff * dt));
        static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>
    /// How a stroke flows out of its drop: the drop's current radius (px) and strength (0..1), and the length (px, along
    /// the stroke from the click) over which the stroke goes from the drop's width and ink to its own.
    /// </summary>
    internal readonly record struct Neck(float Radius, float Strength, float Length)
    {
        /// <summary>Flow length for a drop of full radius <paramref name="dropRadius"/> (px) and a stroke starting at <paramref name="launchSpeed"/> (px/s).</summary>
        public static float LengthFor(float dropRadius, float launchSpeed) => 3 * dropRadius + 0.035f * launchSpeed;
    }

    /// <summary>Widest a stroke gets as it leaves its drop, as a multiple of the drop's radius (also the room to leave around it).</summary>
    internal const float NeckWidth = 1.5f;

    /// <summary>
    /// Draws <paramref name="path"/> (from <see cref="Smooth"/>) opaque, with each segment's remaining life (0..1) as its
    /// gray level, for <see cref="InkLayer"/> to turn into ink. Newer segments are drawn last so they win at the joints.
    /// <paramref name="maxWidth"/> is in screen px; <paramref name="startArc"/> is how far along the stroke (px) the path's
    /// first point lies from the click; <paramref name="offset"/> is the canvas' top-left on screen.
    /// </summary>
    internal static void DrawLife(Graphics g, List<(float X, float Y, float Time)> path, long now, int durationMs,
        float maxWidth, float pressure, Neck? neck, float startArc, Point offset)
    {
        float arc = startArc;
        for (int i = 1; i < path.Count; i++)
        {
            float length = MathF.Sqrt((path[i].X - path[i - 1].X) * (path[i].X - path[i - 1].X)
                                      + (path[i].Y - path[i - 1].Y) * (path[i].Y - path[i - 1].Y));
            float mid = arc + length / 2;
            arc += length;
            float life = 1f - (now - path[i].Time) / durationMs;
            if (life <= 0) continue;
            float width = Math.Max(0.6f, maxWidth * pressure * (0.4f + 0.6f * life));

            // Leaving its drop, the stroke starts with the drop's width and ink and eases into its own along the way,
            // so it reads as ink pulled out of the blot rather than a faded tail stuck to a dark spot.
            if (neck is { } n && n.Radius >= 0.5f && mid < n.Length)
            {
                float t = mid / n.Length;
                float flow = 1 - t * t * (3 - 2 * t);
                life = Math.Max(life, MathF.Sqrt(n.Strength) * flow);   // sqrt: the trail squares life into ink
                width = Math.Max(width, NeckWidth * n.Radius * n.Strength * flow);
            }

            int level = (int)(Math.Clamp(life, 0f, 1f) * 255);
            using var pen = new Pen(Color.FromArgb(255, level, level, level), width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawLine(pen, path[i - 1].X - offset.X, path[i - 1].Y - offset.Y, path[i].X - offset.X, path[i].Y - offset.Y);
        }
    }

    /// <summary>
    /// Catmull-Rom curve through the sampled points, subdivided every few pixels, so fast flicks
    /// (where samples are far apart) draw a rounded stroke instead of a zigzag of straight lines.
    /// </summary>
    internal static List<(float X, float Y, float Time)> Smooth(IReadOnlyList<(PointF Pos, long Time)> pts, float scale)
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
}
