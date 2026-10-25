using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Intuime.Core;

namespace Intuime.Rendering;

public sealed class BuiltInBackdrop : Control
{
    private readonly string _id;
    private readonly DispatcherTimer _timer;

    public BuiltInBackdrop(string id)
    {
        _id = id;
        IsHitTestVisible = false;
        ClipToBounds = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 2 || Bounds.Height < 2)
            return;
        var seconds = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        BuiltInMotion.Draw(context, _id, Bounds.Width, Bounds.Height, seconds);
    }
}

public static class BuiltInMotion
{
    public static void Draw(DrawingContext dc, string id, double w, double h, double seconds)
    {
        using (dc.PushClip(new Rect(0, 0, w, h)))
        {
            switch (BuiltInBackgrounds.Canonical(id))
            {
                case "futuristic/circuit": Circuit(dc, w, h, seconds); break;
                case "futuristic/hologram": Hologram(dc, w, h, seconds); break;
                case "futuristic/data-rain": DataRain(dc, w, h, seconds); break;
                case "space/starfield": Starfield(dc, w, h, seconds); break;
                case "space/nebula": Nebula(dc, w, h, seconds); break;
                case "space/planet": Planet(dc, w, h, seconds); break;
                case "space/aurora": Aurora(dc, w, h, seconds); break;
                case "buildings/skyline": Skyline(dc, w, h, seconds, rain: false); break;
                case "buildings/towers": Towers(dc, w, h, seconds); break;
                case "buildings/rain": Skyline(dc, w, h, seconds, rain: true); break;
                case "zodiac/wheel": Wheel(dc, w, h, seconds); break;
                case "zodiac/signs": Signs(dc, w, h, seconds); break;
                case "zodiac/chart": Chart(dc, w, h, seconds); break;
                case "2d/blobs": Blobs(dc, w, h, seconds); break;
                case "2d/waves": Paper(dc, w, h, seconds); break;
                case "2d/geometry": Geometry(dc, w, h, seconds); break;
                case "2d/pixel": Pixel(dc, w, h, seconds); break;
                case "nature/ocean": Ocean(dc, w, h, seconds); break;
                case "nature/dunes": Dunes(dc, w, h, seconds); break;
                case "nature/fireflies": Fireflies(dc, w, h, seconds); break;
                case "light/streaks": Streaks(dc, w, h, seconds); break;
                case "light/silk": Silk(dc, w, h, seconds); break;
                case "light/bokeh": Bokeh(dc, w, h, seconds); break;
                default: NeonGrid(dc, w, h, seconds); break;
            }
        }
    }

    private static void NeonGrid(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 10);
        var unit = Unit(w, h);
        var horizon = h * 0.54;
        dc.DrawRectangle(Vertical(Color.FromRgb(18, 0, 42), Color.FromRgb(86, 16, 92), Color.FromRgb(8, 0, 24)), null, new Rect(0, 0, w, h));
        var sun = new Point(w * 0.5, horizon - h * 0.02);
        var sunR = Math.Min(w, h) * 0.18;
        for (var i = 6; i >= 1; i--)
        {
            var t = i / 6.0;
            dc.DrawEllipse(Ink.Brush(Q(28 + i * 6), 255, (byte)(50 + i * 18), 150), null, sun, sunR * (0.7 + t), sunR * (0.28 + t * 0.35));
        }
        dc.DrawEllipse(Ink.Brush(255, 255, 214, 170), null, sun, sunR * 0.72, sunR * 0.3);
        dc.DrawRectangle(Ink.Brush(255, 10, 0, 22), null, new Rect(0, horizon, w, h - horizon));
        var rows = 16;
        for (var i = 0; i < rows; i++)
        {
            var z = Wrap((i + phase) / rows);
            var y = horizon + Math.Pow(z, 1.55) * (h - horizon);
            var fade = Math.Sin(Math.PI * z);
            var pen = i % 2 == 0
                ? Ink.Pen(Q(220 * fade), 255, 60, 196, 2 * unit)
                : Ink.Pen(Q(200 * fade), 64, 232, 255, 1.5 * unit);
            dc.DrawLine(pen, new Point(0, y), new Point(w, y));
        }
        var cols = 16;
        var vanish = sun.X;
        for (var i = 0; i <= cols; i++)
        {
            var x = w * i / cols;
            var bottom = vanish + (x - vanish) * 1.45;
            var pen = Ink.Pen(150, i % 2 == 0 ? (byte)255 : (byte)80, i % 2 == 0 ? (byte)70 : (byte)220, i % 2 == 0 ? (byte)190 : (byte)255, 1.4 * unit);
            dc.DrawLine(pen, new Point(vanish + (x - vanish) * 0.04, horizon), new Point(bottom, h));
        }
    }

    private static void Circuit(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 6);
        var unit = Unit(w, h);
        dc.DrawRectangle(Ink.Brush(255, 6, 16, 24), null, new Rect(0, 0, w, h));
        var cols = 8;
        var rows = 5;
        var trace = Ink.Pen(170, 36, 120, 150, 2 * unit);
        var nodes = new Point[cols, rows];
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            var x = w * (0.08 + 0.84 * c / (cols - 1));
            var y = h * (0.12 + 0.76 * r / (rows - 1));
            y += (Hash(c * 17 + r) - 0.5) * h * 0.05;
            nodes[c, r] = new Point(x, y);
        }
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            if (c + 1 < cols)
                dc.DrawLine(trace, nodes[c, r], nodes[c + 1, r]);
            if (r + 1 < rows && Hash(c * 3 + r * 9) > 0.35)
                dc.DrawLine(trace, nodes[c, r], nodes[c, r + 1]);
        }
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            if (c + 1 < cols)
                Pulse(dc, nodes[c, r], nodes[c + 1, r], Wrap(phase + Hash(c + r * 13)), unit);
            dc.DrawEllipse(Ink.Brush(255, 80, 230, 190), null, nodes[c, r], 4 * unit, 4 * unit);
            dc.DrawEllipse(null, Ink.Pen(180, 180, 255, 230, 1.2 * unit), nodes[c, r], 8 * unit, 8 * unit);
        }
    }

    private static void Pulse(DrawingContext dc, Point a, Point b, double t, double unit)
    {
        var fade = Math.Sin(Math.PI * t);
        var p = new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        dc.DrawEllipse(Ink.Brush(Q(230 * fade), 190, 255, 220), null, p, 7 * unit, 7 * unit);
    }

    private static void Hologram(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 8);
        var spin = BuiltInBackgrounds.Phase(seconds, 18);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(4, 18, 32), Color.FromRgb(8, 40, 58)), null, new Rect(0, 0, w, h));
        var center = new Point(w * 0.5, h * 0.52);
        var maxR = Math.Min(w, h) * 0.46;
        var count = 7;
        for (var i = 0; i < count; i++)
        {
            var z = Wrap(i / (double)count + phase);
            var fade = Math.Sin(Math.PI * z);
            var radius = (0.06 + z * 0.94) * maxR;
            dc.DrawEllipse(null, Ink.Pen(Q(210 * fade), 90, 230, 255, 2 * unit), center, radius, radius * 0.38);
        }
        for (var i = 0; i < 12; i++)
        {
            var a = Math.Tau * (i / 12.0 + spin);
            dc.DrawLine(Ink.Pen(160, 120, 255, 230, 1.5 * unit), Polar(center, maxR * 0.12, a), Polar(center, maxR * 0.92, a));
        }
        dc.DrawEllipse(Ink.Brush(255, 210, 255, 250), null, center, 8 * unit, 5 * unit);
    }

    private static void DataRain(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 9);
        var unit = Unit(w, h);
        dc.DrawRectangle(Ink.Brush(255, 2, 10, 8), null, new Rect(0, 0, w, h));
        var cols = 36;
        var glyph = 16 * unit;
        for (var c = 0; c < cols; c++)
        {
            var x = (c + 0.5) / cols * w;
            var speed = 0.55 + Hash(c + 4) * 0.9;
            var head = Wrap(Hash(c * 3) + phase * speed);
            var trail = 8 + (int)(Hash(c + 8) * 8);
            for (var i = 0; i < trail; i++)
            {
                var t = Wrap(head - i * 0.035);
                var y = t * (h + glyph * 2) - glyph;
                var fade = (1 - i / (double)trail) * Edge(y, h);
                var bright = i == 0;
                var brush = bright
                    ? Ink.Brush(Q(255 * fade), 210, 255, 220)
                    : Ink.Brush(Q(180 * fade), 40, (byte)(140 + Hash(c + i) * 80), 90);
                var tall = glyph * (0.45 + Hash(c * 19 + i) * 0.7);
                dc.DrawRectangle(brush, null, new Rect(x - 3 * unit, y, 6 * unit, tall), 1.5 * unit, 1.5 * unit);
            }
        }
    }

    private static void Starfield(DrawingContext dc, double w, double h, double seconds)
    {
        dc.DrawRectangle(Vertical(Color.FromRgb(4, 8, 20), Color.FromRgb(10, 18, 42), Color.FromRgb(2, 4, 12)), null, new Rect(0, 0, w, h));
        Stars(dc, w, h, seconds, 140, 0.04);
    }

    private static void Nebula(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 20);
        dc.DrawRectangle(Vertical(Color.FromRgb(8, 6, 24), Color.FromRgb(18, 10, 40)), null, new Rect(0, 0, w, h));
        Cloud(dc, w, h, 0.32, 0.42, 0.28, phase, 0.0, 90, 40, 120, 70);
        Cloud(dc, w, h, 0.68, 0.48, 0.34, phase, 0.35, 40, 90, 140, 60);
        Cloud(dc, w, h, 0.5, 0.62, 0.22, phase, 0.6, 160, 60, 120, 50);
        Stars(dc, w, h, seconds, 70, 0.02);
    }

    private static void Cloud(DrawingContext dc, double w, double h, double ax, double ay, double radius, double phase, double offset, byte r, byte g, byte b, byte alpha)
    {
        var cx = w * (ax + 0.06 * Math.Sin(Math.Tau * (phase + offset)));
        var cy = h * (ay + 0.04 * Math.Cos(Math.Tau * (phase + offset)));
        var rx = Math.Min(w, h) * radius;
        for (var i = 5; i >= 1; i--)
        {
            var t = i / 5.0;
            dc.DrawEllipse(Ink.Brush(Q(alpha * (1.15 - t)), r, g, b), null, new Point(cx, cy), rx * t, rx * t * 0.72);
        }
    }

    private static void Planet(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 14);
        var orbit = BuiltInBackgrounds.Phase(seconds, 18);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(3, 6, 16), Color.FromRgb(8, 14, 32)), null, new Rect(0, 0, w, h));
        Stars(dc, w, h, seconds, 80, 0.015);
        var r = Math.Min(w, h) * 0.26;
        var center = new Point(w * 0.58, h * 0.56);
        dc.DrawEllipse(Ink.Brush(40, 255, 180, 110), null, center, r * 1.18, r * 1.18);
        dc.DrawEllipse(Ink.Brush(255, 186, 112, 64), null, center, r, r);
        using (dc.PushGeometryClip(new EllipseGeometry(new Rect(center.X - r, center.Y - r, r * 2, r * 2))))
        {
            var bands = 8;
            var stripe = r * 2 / bands;
            for (var i = -2; i <= bands + 2; i++)
            {
                var y = center.Y - r + i * stripe - phase * stripe * 2;
                var color = Math.Abs(i) % 2 == 0
                    ? Ink.Brush(80, 120, 50, 24)
                    : Ink.Brush(70, 230, 160, 80);
                dc.DrawRectangle(color, null, new Rect(center.X - r, y, r * 2, stripe + 1));
            }
        }
        dc.DrawEllipse(null, Ink.Pen(150, 255, 210, 160, 3 * unit), center, r * 1.03, r * 1.03);
        var moon = Polar(center, r * 1.65, Math.Tau * orbit);
        var moonY = center.Y + (moon.Y - center.Y) * 0.42;
        dc.DrawEllipse(Ink.Brush(255, 232, 232, 238), null, new Point(moon.X, moonY), r * 0.16, r * 0.16);
    }

    private static void Aurora(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 12);
        dc.DrawRectangle(Vertical(Color.FromRgb(4, 10, 18), Color.FromRgb(6, 18, 28), Color.FromRgb(2, 6, 12)), null, new Rect(0, 0, w, h));
        Stars(dc, w, h, seconds, 60, 0.01);
        Ribbon(dc, w, h * 0.34, h * 0.06, 1.4, phase, h * 0.08, Ink.Brush(70, 80, 255, 170));
        Ribbon(dc, w, h * 0.42, h * 0.05, 1.1, phase + 0.2, h * 0.07, Ink.Brush(60, 140, 255, 190));
        Ribbon(dc, w, h * 0.5, h * 0.045, 1.8, Wrap(phase + 0.45), h * 0.06, Ink.Brush(50, 180, 140, 255));
    }

    private static void Skyline(DrawingContext dc, double w, double h, double seconds, bool rain)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 16);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(18, 24, 48), Color.FromRgb(232, 122, 74), Color.FromRgb(18, 16, 32)), null, new Rect(0, 0, w, h * 0.72));
        dc.DrawRectangle(Ink.Brush(255, 10, 12, 20), null, new Rect(0, h * 0.62, w, h * 0.38));
        var moon = new Point(w * 0.78, h * 0.18);
        dc.DrawEllipse(Ink.Brush(230, 255, 236, 210), null, moon, 28 * unit, 28 * unit);
        Band(dc, w, h, seconds, seed: 3, speed: 0.35, top: 0.5, depth: 0.22, windows: false, unit);
        Band(dc, w, h, seconds, seed: 11, speed: 0.7, top: 0.42, depth: 0.34, windows: true, unit);
        if (!rain)
            return;
        var drops = 70;
        for (var i = 0; i < drops; i++)
        {
            var speed = 0.7 + Hash(i + 2) * 0.8;
            var x = Wrap(Hash(i) + phase * 0.04 * (Hash(i + 6) - 0.5)) * w;
            var y = Wrap(Hash(i + 9) + phase * speed) * h;
            var fade = Edge(y, h);
            var len = (14 + Hash(i + 4) * 22) * unit;
            dc.DrawLine(Ink.Pen(Q(150 * fade), 200, 220, 240, 1.2 * unit), new Point(x, y), new Point(x + 2 * unit, y + len));
        }
        var flash = Math.Pow(Math.Max(0, Math.Sin(Math.Tau * BuiltInBackgrounds.Phase(seconds, 11))), 28);
        if (flash > 0.05)
            dc.DrawRectangle(Ink.Brush(Q(90 * flash), 230, 236, 255), null, new Rect(0, 0, w, h));
    }

    private static void Band(DrawingContext dc, double w, double h, double seconds, int seed, double speed, double top, double depth, bool windows, double unit)
    {
        var shift = BuiltInBackgrounds.Phase(seconds, 28 / Math.Max(0.2, speed)) * w;
        var count = 12;
        var span = w / count;
        for (var copy = 0; copy < 2; copy++)
        for (var i = 0; i < count; i++)
        {
            var bw = span * (0.55 + Hash(seed + i) * 0.4);
            var bh = h * (top * 0.15 + Hash(seed + i * 5) * depth);
            var x = i * span - shift + copy * w;
            var y = h * 0.72 - bh;
            var shade = (byte)(8 + Hash(seed + i) * 18);
            dc.DrawRectangle(Ink.Brush(255, shade, shade, (byte)(shade + 6)), null, new Rect(x, y, bw, h - y));
            if (!windows)
                continue;
            var cellX = 14 * unit;
            var cellY = 18 * unit;
            for (var row = 0; row * cellY < bh - 8 * unit; row++)
            for (var col = 0; col * cellX < bw - 6 * unit && row < 10 && col < 6; col++)
            {
                var on = Hash(seed + i * 40 + row * 7 + col) > 0.62;
                var twinkle = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(Math.Tau * (BuiltInBackgrounds.Phase(seconds, 4 + Hash(seed + col)) + Hash(i + row + col))));
                if (!on)
                    continue;
                dc.DrawRectangle(Ink.Brush(Q(220 * twinkle), 255, 196, 110), null,
                    new Rect(x + 4 * unit + col * cellX, y + 6 * unit + row * cellY, 6 * unit, 8 * unit));
            }
        }
    }

    private static void Towers(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 8);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(8, 16, 32), Color.FromRgb(18, 36, 64), Color.FromRgb(6, 12, 24)), null, new Rect(0, 0, w, h));
        var towers = 5;
        for (var i = 0; i < towers; i++)
        {
            var bw = w * (0.1 + Hash(i) * 0.04);
            var bh = h * (0.45 + Hash(i + 3) * 0.4);
            var x = w * (0.08 + i * 0.18);
            var y = h * 0.92 - bh;
            dc.DrawRectangle(Vertical(Color.FromRgb(28, 54, 82), Color.FromRgb(70, 120, 160)), null, new Rect(x, y, bw, bh));
            var sheen = bw * (0.15 + 0.7 * (0.5 + 0.5 * Math.Sin(Math.Tau * (phase + i * 0.13))));
            dc.DrawRectangle(Ink.Brush(50, 220, 245, 255), null, new Rect(x + sheen, y, bw * 0.12, bh));
            var cols = 4;
            var rows = 12;
            for (var row = 0; row < rows; row++)
            for (var col = 0; col < cols; col++)
            {
                var lit = Hash(i * 50 + row * 3 + col) > 0.55 || row == (int)(phase * rows + i) % rows;
                if (!lit)
                    continue;
                var wx = x + bw * (0.12 + col * 0.2);
                var wy = y + bh * (0.06 + row * 0.075);
                dc.DrawRectangle(Ink.Brush(200, 186, 230, 255), null, new Rect(wx, wy, bw * 0.1, 8 * unit));
            }
        }
        dc.DrawRectangle(Ink.Brush(255, 8, 14, 24), null, new Rect(0, h * 0.9, w, h * 0.1));
    }

    private static void Wheel(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 24);
        var unit = Unit(w, h);
        dc.DrawRectangle(Ink.Brush(255, 6, 8, 20), null, new Rect(0, 0, w, h));
        Stars(dc, w, h, seconds, 40, 0);
        var center = new Point(w * 0.5, h * 0.5);
        var radius = Math.Min(w, h) * 0.34;
        dc.DrawEllipse(null, Ink.Pen(80, 212, 168, 92, 1.5 * unit), center, radius, radius);
        var stars = new (double X, double Y)[]
        {
            (-0.1, -0.85), (0.15, -0.55), (0.4, -0.7), (0.62, -0.28), (0.3, -0.05),
            (0.55, 0.22), (0.2, 0.48), (-0.15, 0.62), (-0.48, 0.35), (-0.62, -0.05),
            (-0.4, -0.32), (-0.55, -0.6)
        };
        var lines = new (int A, int B)[]
        {
            (0, 1), (1, 2), (1, 4), (4, 5), (4, 6), (6, 7), (7, 8), (8, 9), (9, 10), (10, 11), (11, 0)
        };
        var placed = new Point[stars.Length];
        for (var i = 0; i < stars.Length; i++)
        {
            var a = Math.Atan2(stars[i].Y, stars[i].X) + Math.Tau * phase;
            var d = Math.Sqrt(stars[i].X * stars[i].X + stars[i].Y * stars[i].Y) * radius;
            placed[i] = Polar(center, d, a);
            dc.DrawEllipse(Ink.Brush(255, 244, 220, 160), null, placed[i], 4 * unit, 4 * unit);
        }
        var pen = Ink.Pen(170, 232, 190, 110, 1.4 * unit);
        foreach (var line in lines)
            dc.DrawLine(pen, placed[line.A], placed[line.B]);
    }

    private static void Signs(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 30);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(12, 10, 24), Color.FromRgb(24, 16, 40)), null, new Rect(0, 0, w, h));
        var center = new Point(w * 0.5, h * 0.5);
        var radius = Math.Min(w, h) * 0.34;
        dc.DrawEllipse(null, Ink.Pen(90, 212, 168, 92, 2 * unit), center, radius, radius);
        dc.DrawEllipse(null, Ink.Pen(50, 212, 168, 92, 1 * unit), center, radius * 0.62, radius * 0.62);
        var pen = Ink.Pen(230, 244, 214, 150, 1.8 * unit);
        var names = new[] { "Ari", "Tau", "Gem", "Cnc", "Leo", "Vir", "Lib", "Sco", "Sgr", "Cap", "Aqr", "Psc" };
        for (var i = 0; i < 12; i++)
        {
            var a = Math.Tau * (i / 12.0 - 0.25 + phase);
            var at = Polar(center, radius, a);
            Glyph(dc, i, at.X, at.Y, 16 * unit, pen);
            var labelAt = Polar(center, radius * 1.18, a);
            DrawLabel(dc, names[i], labelAt, 13 * unit, Ink.Brush(220, 244, 220, 170));
        }
    }

    private static void Chart(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 20);
        var unit = Unit(w, h);
        dc.DrawRectangle(Ink.Brush(255, 8, 12, 22), null, new Rect(0, 0, w, h));
        var center = new Point(w * 0.5, h * 0.5);
        var radius = Math.Min(w, h) * 0.4;
        for (var ring = 1; ring <= 4; ring++)
            dc.DrawEllipse(null, Ink.Pen(70, 150, 186, 220, 1 * unit), center, radius * ring / 4.0, radius * ring / 4.0);
        for (var i = 0; i < 72; i++)
        {
            var a = Math.Tau * (i / 72.0 + phase);
            var inner = radius * (i % 6 == 0 ? 0.86 : 0.93);
            dc.DrawLine(Ink.Pen(160, 186, 210, 230, (i % 6 == 0 ? 1.6 : 1) * unit), Polar(center, inner, a), Polar(center, radius, a));
        }
        var sweep = Math.Tau * phase;
        var wedge = new StreamGeometry();
        using (var ctx = wedge.Open())
        {
            ctx.BeginFigure(center, true);
            ctx.LineTo(Polar(center, radius * 0.86, sweep));
            ctx.LineTo(Polar(center, radius * 0.86, sweep + 0.35));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Brush(40, 231, 162, 58), null, wedge);
        dc.DrawLine(Ink.Pen(220, 231, 162, 58, 2 * unit), center, Polar(center, radius, sweep));
        Stars(dc, w, h, seconds, 30, 0);
    }

    private static void Blobs(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 14);
        dc.DrawRectangle(Ink.Brush(255, 244, 236, 224), null, new Rect(0, 0, w, h));
        Blob(dc, w, h, 0.3, 0.4, 0.28, phase, 0, 231, 120, 90, 180);
        Blob(dc, w, h, 0.7, 0.35, 0.24, phase, 0.3, 90, 170, 168, 170);
        Blob(dc, w, h, 0.55, 0.7, 0.26, phase, 0.55, 240, 176, 84, 160);
        Blob(dc, w, h, 0.2, 0.75, 0.18, phase, 0.15, 120, 150, 210, 150);
    }

    private static void Blob(DrawingContext dc, double w, double h, double ax, double ay, double radius, double phase, double offset, byte r, byte g, byte b, byte alpha)
    {
        var cx = w * (ax + 0.05 * Math.Sin(Math.Tau * (phase + offset)));
        var cy = h * (ay + 0.04 * Math.Cos(Math.Tau * (phase * 2 + offset)));
        var rx = Math.Min(w, h) * radius;
        dc.DrawEllipse(Ink.Brush(alpha, r, g, b), null, new Point(cx, cy), rx, rx * 0.85);
    }

    private static void Paper(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 10);
        dc.DrawRectangle(Ink.Brush(255, 244, 236, 220), null, new Rect(0, 0, w, h));
        Wave(dc, w, h, h * 0.38, h * 0.04, 1.2, phase, Ink.Brush(255, 255, 176, 120));
        Wave(dc, w, h, h * 0.52, h * 0.05, 1.0, phase + 0.15, Ink.Brush(255, 240, 122, 92));
        Wave(dc, w, h, h * 0.68, h * 0.045, 1.4, phase + 0.3, Ink.Brush(255, 90, 150, 160));
        Wave(dc, w, h, h * 0.84, h * 0.04, 0.9, phase + 0.5, Ink.Brush(255, 36, 84, 120));
    }

    private static void Geometry(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 12);
        var unit = Unit(w, h);
        dc.DrawRectangle(Ink.Brush(255, 24, 36, 56), null, new Rect(0, 0, w, h));
        Shape(dc, w * 0.25, h * 0.4, 70 * unit, 3, phase, Ink.Brush(255, 231, 162, 58));
        Shape(dc, w * 0.62, h * 0.35, 90 * unit, 4, Wrap(phase + 0.2), Ink.Brush(255, 90, 190, 180));
        Shape(dc, w * 0.48, h * 0.68, 80 * unit, 6, Wrap(phase + 0.55), Ink.Brush(255, 230, 100, 110));
        dc.DrawEllipse(null, Ink.Pen(255, 244, 236, 220, 6 * unit), new Point(w * 0.78, h * 0.7), 50 * unit, 50 * unit);
    }

    private static void Shape(DrawingContext dc, double cx, double cy, double radius, int sides, double phase, IBrush brush)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var first = Polar(new Point(cx, cy), radius, Math.Tau * phase);
            ctx.BeginFigure(first, true);
            for (var i = 1; i <= sides; i++)
                ctx.LineTo(Polar(new Point(cx, cy), radius, Math.Tau * (phase + i / (double)sides)));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static void Pixel(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 12);
        var unit = Unit(w, h);
        var cell = 18 * unit;
        var rows = Math.Max(8, (int)Math.Ceiling(h / cell));
        for (var row = 0; row < rows; row++)
        {
            var t = row / (double)Math.Max(1, rows - 1);
            var color = t < 0.45
                ? Mix(Color.FromRgb(40, 28, 72), Color.FromRgb(232, 120, 70), t / 0.45)
                : Mix(Color.FromRgb(232, 120, 70), Color.FromRgb(255, 196, 110), (t - 0.45) / 0.55);
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, row * cell, w, cell + 1));
        }
        var sunC = (int)(w * 0.7 / cell);
        var sunR = (int)(h * 0.62 / cell);
        for (var y = -6; y <= 6; y++)
        for (var x = -6; x <= 6; x++)
        {
            if (x * x + y * y > 28)
                continue;
            dc.DrawRectangle(Ink.Brush(255, 255, 214, 140), null, new Rect((sunC + x) * cell, (sunR + y) * cell, cell, cell));
        }
        for (var i = 0; i < 4; i++)
        {
            var cw = (4 + (int)(Hash(i) * 4)) * cell;
            var ch = 2 * cell;
            var x = Wrap(Hash(i + 2) + phase * (0.15 + i * 0.04)) * (w + cw) - cw;
            var y = h * (0.12 + i * 0.08);
            dc.DrawRectangle(Ink.Brush(230, 255, 228, 210), null, new Rect(x, y, cw, ch));
            dc.DrawRectangle(Ink.Brush(230, 255, 228, 210), null, new Rect(x + cw * 0.3, y - cell, cw * 0.5, ch));
        }
    }

    private static void Ocean(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 10);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(120, 176, 214), Color.FromRgb(232, 168, 96), Color.FromRgb(14, 70, 92)), null, new Rect(0, 0, w, h));
        var sun = new Point(w * 0.72, h * 0.3);
        dc.DrawEllipse(Ink.Brush(255, 255, 214, 150), null, sun, 36 * unit, 36 * unit);
        for (var i = 0; i < 8; i++)
        {
            var y = h * 0.48 + i * h * 0.035;
            var fade = 0.35 + 0.1 * Math.Sin(Math.Tau * (phase * 2 + i * 0.1));
            dc.DrawRectangle(Ink.Brush(Q(160 * fade), 255, 220, 160), null, new Rect(sun.X - 4 * unit, y, 8 * unit, h * 0.02));
        }
        Wave(dc, w, h, h * 0.58, h * 0.025, 1.3, phase, Ink.Brush(220, 20, 110, 140));
        Wave(dc, w, h, h * 0.7, h * 0.03, 1.1, phase + 0.2, Ink.Brush(235, 12, 78, 112));
        Wave(dc, w, h, h * 0.84, h * 0.028, 1.5, phase + 0.4, Ink.Brush(245, 6, 48, 78));
    }

    private static void Dunes(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 16);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(10, 16, 40), Color.FromRgb(28, 24, 48), Color.FromRgb(18, 14, 28)), null, new Rect(0, 0, w, h));
        Stars(dc, w, h, seconds, 50, 0.01);
        var moon = new Point(w * 0.22, h * 0.2);
        dc.DrawEllipse(Ink.Brush(240, 255, 236, 210), null, moon, 26 * unit, 26 * unit);
        Wave(dc, w, h, h * 0.62, h * 0.04, 0.8, phase, Ink.Brush(255, 92, 62, 48));
        Wave(dc, w, h, h * 0.74, h * 0.035, 1.1, phase + 0.2, Ink.Brush(255, 140, 92, 64));
        Wave(dc, w, h, h * 0.88, h * 0.03, 0.7, phase + 0.35, Ink.Brush(255, 196, 140, 96));
    }

    private static void Fireflies(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 12);
        dc.DrawRectangle(Vertical(Color.FromRgb(8, 18, 14), Color.FromRgb(12, 28, 22)), null, new Rect(0, 0, w, h));
        Wave(dc, w, h, h * 0.78, h * 0.03, 1.2, phase, Ink.Brush(255, 10, 24, 16));
        var unit = Unit(w, h);
        for (var i = 0; i < 36; i++)
        {
            var x = w * (0.08 + 0.84 * Hash(i));
            var y = h * (0.1 + 0.6 * Hash(i + 2));
            x += Math.Sin(Math.Tau * (phase + Hash(i + 4))) * 24 * unit;
            y += Math.Cos(Math.Tau * (phase * 2 + Hash(i + 6))) * 16 * unit;
            var glow = 0.35 + 0.65 * (0.5 + 0.5 * Math.Sin(Math.Tau * (phase * 3 + Hash(i + 8))));
            dc.DrawEllipse(Ink.Brush(Q(40 * glow), 220, 255, 140), null, new Point(x, y), 14 * unit, 14 * unit);
            dc.DrawEllipse(Ink.Brush(Q(230 * glow), 230, 255, 170), null, new Point(x, y), 3 * unit, 3 * unit);
        }
    }

    private static void Streaks(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 7);
        var unit = Unit(w, h);
        dc.DrawRectangle(Vertical(Color.FromRgb(10, 12, 28), Color.FromRgb(24, 18, 48)), null, new Rect(0, 0, w, h));
        var count = 18;
        for (var i = 0; i < count; i++)
        {
            var z = Wrap(Hash(i) + phase * (0.4 + Hash(i + 3)));
            var fade = Math.Sin(Math.PI * z);
            var x = z * (w + 200 * unit) - 100 * unit;
            var y = h * Hash(i + 7);
            var len = (80 + Hash(i + 5) * 160) * unit;
            dc.DrawLine(Ink.Pen(Q(180 * fade), 255, 220, 170, (2 + Hash(i) * 3) * unit), new Point(x, y), new Point(x + len, y - len * 0.35));
        }
    }

    private static void Silk(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 12);
        dc.DrawRectangle(Vertical(Color.FromRgb(28, 16, 40), Color.FromRgb(70, 28, 64), Color.FromRgb(18, 16, 36)), null, new Rect(0, 0, w, h));
        Ribbon(dc, w, h * 0.3, h * 0.05, 1.2, phase, h * 0.1, Ink.Brush(80, 255, 150, 120));
        Ribbon(dc, w, h * 0.45, h * 0.06, 0.9, phase + 0.25, h * 0.12, Ink.Brush(70, 120, 170, 255));
        Ribbon(dc, w, h * 0.62, h * 0.05, 1.5, phase + 0.5, h * 0.09, Ink.Brush(60, 255, 196, 140));
    }

    private static void Bokeh(DrawingContext dc, double w, double h, double seconds)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 16);
        dc.DrawRectangle(Vertical(Color.FromRgb(16, 10, 24), Color.FromRgb(36, 18, 40)), null, new Rect(0, 0, w, h));
        for (var i = 0; i < 16; i++)
        {
            var cx = w * (Hash(i) + 0.04 * Math.Sin(Math.Tau * (phase + Hash(i + 2))));
            var cy = h * (Hash(i + 4) + 0.03 * Math.Cos(Math.Tau * (phase + Hash(i + 6))));
            var radius = Math.Min(w, h) * (0.04 + Hash(i + 8) * 0.08);
            var glow = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(Math.Tau * (phase + Hash(i + 10))));
            var color = i % 3 == 0 ? (255, 186, 120) : i % 3 == 1 ? (140, 180, 255) : (255, 150, 190);
            dc.DrawEllipse(Ink.Brush(Q(50 * glow), (byte)color.Item1, (byte)color.Item2, (byte)color.Item3), null, new Point(Wrap(cx / w) * w, Wrap(cy / h) * h), radius, radius);
        }
    }

    private static void Stars(DrawingContext dc, double w, double h, double seconds, int count, double drift)
    {
        var phase = BuiltInBackgrounds.Phase(seconds, 20);
        var unit = Unit(w, h);
        for (var i = 0; i < count; i++)
        {
            var speed = 0.25 + Hash(i + 3) * 0.75;
            var x = Wrap(Hash(i) + phase * drift) * w;
            var y = Wrap(Hash(i + 5) + phase * speed * (drift == 0 ? 0 : 1)) * h;
            if (drift == 0)
                y = Hash(i + 5) * h;
            var twinkle = 0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(Math.Tau * (phase * 2 + Hash(i + 11))));
            var fade = drift == 0 ? 1 : Edge(x, w) * Edge(y, h);
            var r = (0.7 + Hash(i + 13) * (Hash(i + 19) > 0.92 ? 2.4 : 1.5)) * unit;
            dc.DrawEllipse(Ink.Brush(Q(230 * twinkle * fade), 244, 247, 251), null, new Point(x, y), r, r);
        }
    }

    private static void Wave(DrawingContext dc, double w, double h, double baseY, double amp, double freq, double phase, IBrush brush)
    {
        var steps = 48;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, h), true);
            for (var i = 0; i <= steps; i++)
            {
                var x = w * i / steps;
                var y = baseY + amp * Math.Sin(Math.Tau * (freq * i / steps + phase));
                ctx.LineTo(new Point(x, y));
            }
            ctx.LineTo(new Point(w, h));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static void Ribbon(DrawingContext dc, double w, double y, double amp, double freq, double phase, double girth, IBrush brush)
    {
        var steps = 42;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            double Y(int i, double extra) => y + amp * Math.Sin(Math.Tau * (freq * i / (double)steps + phase)) + extra;
            ctx.BeginFigure(new Point(0, Y(0, 0)), true);
            for (var i = 1; i <= steps; i++)
                ctx.LineTo(new Point(w * i / steps, Y(i, 0)));
            for (var i = steps; i >= 0; i--)
                ctx.LineTo(new Point(w * i / steps, Y(i, girth)));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static void Glyph(DrawingContext dc, int index, double cx, double cy, double s, Pen pen)
    {
        void L(double x1, double y1, double x2, double y2) =>
            dc.DrawLine(pen, new Point(cx + x1 * s, cy + y1 * s), new Point(cx + x2 * s, cy + y2 * s));
        switch (index % 12)
        {
            case 0:
                L(-0.7, 0.2, -0.15, -0.7); L(-0.15, -0.7, 0.15, -0.15); L(0.15, -0.15, 0.7, 0.2); break;
            case 1:
                dc.DrawEllipse(null, pen, new Point(cx, cy + s * 0.15), s * 0.38, s * 0.38);
                L(-0.4, -0.1, -0.7, -0.7); L(0.4, -0.1, 0.7, -0.7); break;
            case 2:
                L(-0.45, -0.7, -0.45, 0.7); L(0.45, -0.7, 0.45, 0.7); L(-0.7, -0.7, -0.2, -0.7); L(0.2, -0.7, 0.7, -0.7); L(-0.7, 0.7, -0.2, 0.7); L(0.2, 0.7, 0.7, 0.7); break;
            case 3:
                dc.DrawEllipse(null, pen, new Point(cx - s * 0.28, cy - s * 0.15), s * 0.32, s * 0.32);
                dc.DrawEllipse(null, pen, new Point(cx + s * 0.28, cy + s * 0.15), s * 0.32, s * 0.32); break;
            case 4:
                dc.DrawEllipse(null, pen, new Point(cx - s * 0.15, cy), s * 0.38, s * 0.38);
                L(0.2, 0, 0.75, 0.15); L(0.75, 0.15, 0.45, 0.7); break;
            case 5:
                L(-0.6, -0.6, -0.2, 0.5); L(-0.2, 0.5, 0.15, -0.6); L(0.15, -0.6, 0.45, 0.2); L(0.45, 0.2, 0.7, -0.15); break;
            case 6:
                L(-0.7, 0.15, 0.7, 0.15); dc.DrawEllipse(null, pen, new Point(cx, cy - s * 0.2), s * 0.45, s * 0.28); break;
            case 7:
                L(-0.55, -0.55, -0.15, 0.45); L(-0.15, 0.45, 0.2, -0.55); L(0.2, -0.55, 0.55, 0.2); L(0.55, 0.2, 0.8, 0.05); L(0.8, 0.05, 0.45, 0.45); break;
            case 8:
                L(-0.2, 0.6, 0.55, -0.6); L(0.55, -0.6, 0.15, -0.35); L(0.55, -0.6, 0.25, -0.15); break;
            case 9:
                L(-0.6, 0.4, 0.1, -0.6); L(0.1, -0.6, 0.55, 0.15); L(0.2, 0.05, 0.7, 0.55); break;
            case 10:
                L(-0.7, -0.35, -0.3, -0.05); L(-0.3, -0.05, 0.1, -0.4); L(0.1, -0.4, 0.45, -0.05); L(0.45, -0.05, 0.75, -0.35);
                L(-0.7, 0.2, -0.3, 0.5); L(-0.3, 0.5, 0.1, 0.15); L(0.1, 0.15, 0.45, 0.5); L(0.45, 0.5, 0.75, 0.2); break;
            default:
                L(-0.7, 0, 0.7, 0); dc.DrawEllipse(null, pen, new Point(cx - s * 0.35, cy), s * 0.28, s * 0.48); dc.DrawEllipse(null, pen, new Point(cx + s * 0.35, cy), s * 0.28, s * 0.48); break;
        }
    }

    private static void DrawLabel(DrawingContext dc, string text, Point at, double size, IBrush brush)
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
        dc.DrawText(formatted, new Point(at.X - formatted.Width / 2, at.Y - formatted.Height / 2));
    }

    private static Point Polar(Point center, double radius, double angle) =>
        new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);

    private static double Unit(double w, double h) => Math.Max(0.5, Math.Min(w, h) / 1080.0);

    private static double Wrap(double value)
    {
        var t = value % 1;
        if (t < 0)
            t += 1;
        return t;
    }

    private static double Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> 16) ^ x) * 0x7feb352du;
            x = ((x >> 15) ^ x) * 0x846ca68bu;
            x = (x >> 16) ^ x;
            return (x & 0xFFFFFF) / 16777216.0;
        }
    }

    private static byte Q(double value) =>
        (byte)Math.Clamp((int)Math.Round(Math.Clamp(value, 0, 255) / 12.0) * 12, 0, 255);

    private static double Edge(double value, double size)
    {
        var band = Math.Max(8, size * 0.06);
        return Math.Clamp(Math.Min(value, size - value) / band, 0, 1);
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static LinearGradientBrush Vertical(params Color[] colors)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
        };
        for (var i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(colors[i], colors.Length == 1 ? 0 : i / (double)(colors.Length - 1)));
        return brush;
    }

    private static class Ink
    {
        private static readonly Dictionary<uint, IBrush> Brushes = new();
        private static readonly Dictionary<long, Pen> Pens = new();

        public static IBrush Brush(byte a, byte r, byte g, byte b)
        {
            uint key = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
            if (!Brushes.TryGetValue(key, out var brush))
            {
                brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                Brushes[key] = brush;
            }
            return brush;
        }

        public static Pen Pen(byte a, byte r, byte g, byte b, double thickness)
        {
            var snapped = Math.Max(1, Math.Round(thickness * 2) / 2);
            uint color = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
            var key = ((long)color << 8) ^ (long)(snapped * 2);
            if (!Pens.TryGetValue(key, out var pen))
            {
                pen = new Pen(Brush(a, r, g, b), snapped);
                Pens[key] = pen;
            }
            return pen;
        }
    }
}
