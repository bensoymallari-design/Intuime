using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Intuime.Core;

namespace Intuime.Rendering;

public sealed class IconFace : Control
{
    private string _id = BuiltInIcons.DefaultId;
    private string _color = "#F4F7FB";

    public void Set(string id, string color)
    {
        var nextId = string.IsNullOrWhiteSpace(id) ? BuiltInIcons.DefaultId : id;
        var nextColor = string.IsNullOrWhiteSpace(color) ? "#F4F7FB" : color;
        if (_id == nextId && _color == nextColor)
            return;
        _id = nextId;
        _color = nextColor;
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 2 || Bounds.Height < 2)
            return;
        IconArt.Draw(context, _id, Bounds.Width, Bounds.Height, _color);
    }
}

public static class IconArt
{
    public static void Draw(DrawingContext dc, string id, double w, double h, string color)
    {
        using (dc.PushClip(new Rect(0, 0, w, h)))
        {
            var icon = BuiltInIcons.Find(id);
            var key = icon?.Id ?? BuiltInIcons.DefaultId;
            if (icon?.Category == "Flags")
            {
                DrawFlag(dc, w, h, string.IsNullOrWhiteSpace(icon.Spec) ? "h:#888888,#CCCCCC" : icon.Spec);
                return;
            }

            var ink = Painting.Brush(color, "#F4F7FB");
            var box = Square(w, h, key.StartsWith("smiles/", StringComparison.Ordinal) ? 0.04 : 0.16);
            if (key.StartsWith("smiles/", StringComparison.Ordinal))
            {
                Smile(dc, box, key);
                return;
            }

            switch (key)
            {
                case "media/pause": Bars(dc, ink, box, 2); break;
                case "media/stop": dc.DrawRectangle(ink, null, box.R(0.16, 0.16, 0.68, 0.68), box.W * 0.06, box.W * 0.06); break;
                case "media/previous": Previous(dc, ink, box); break;
                case "media/next": Next(dc, ink, box); break;
                case "media/rewind": Doubles(dc, ink, box, left: true); break;
                case "media/forward": Doubles(dc, ink, box, left: false); break;
                case "media/record": dc.DrawEllipse(ink, null, box.P(0.5, 0.5), box.W * 0.32, box.W * 0.32); break;
                case "media/volume": Volume(dc, ink, box, mute: false); break;
                case "media/mute": Volume(dc, ink, box, mute: true); break;
                case "media/music": Music(dc, ink, box); break;
                case "media/microphone": Mic(dc, ink, box); break;
                case "media/camera": Camera(dc, ink, box); break;
                case "media/headphones": Headphones(dc, ink, box); break;
                case "media/repeat": Repeat(dc, ink, box); break;
                case "media/shuffle": Shuffle(dc, ink, box); break;
                case "arrows/left": Arrow(dc, ink, box, 180); break;
                case "arrows/right": Arrow(dc, ink, box, 0); break;
                case "arrows/up": Arrow(dc, ink, box, -90); break;
                case "arrows/down": Arrow(dc, ink, box, 90); break;
                case "arrows/refresh": Repeat(dc, ink, box); break;
                case "arrows/undo": ArcArrow(dc, ink, box, left: true); break;
                case "arrows/redo": ArcArrow(dc, ink, box, left: false); break;
                case "arrows/expand": Expand(dc, ink, box); break;
                case "arrows/back": Back(dc, ink, box); break;
                case "arrows/out": Leave(dc, ink, box); break;
                case "symbols/check": Mark(dc, ink, box, 0.18, 0.52, 0.40, 0.76, 0.82, 0.28); break;
                case "symbols/close": Cross(dc, ink, box); break;
                case "symbols/plus": Plus(dc, ink, box); break;
                case "symbols/minus": Line(dc, ink, box, 0.18, 0.5, 0.82, 0.5, 0.14); break;
                case "symbols/search": Search(dc, ink, box); break;
                case "symbols/info": Info(dc, ink, box); break;
                case "symbols/warning": Warning(dc, ink, box); break;
                case "symbols/question": Question(dc, ink, box); break;
                case "symbols/star": Star(dc, ink, box.P(0.5, 0.52), box.W * 0.42); break;
                case "symbols/heart": Heart(dc, Painting.Brush("#E15B64"), box.P(0.5, 0.54), box.W * 0.42); break;
                case "symbols/gear": Gear(dc, ink, box); break;
                case "symbols/lock": Lock(dc, ink, box); break;
                case "symbols/home": Home(dc, ink, box); break;
                case "symbols/pin": Pin(dc, ink, box); break;
                case "objects/mail": Mail(dc, ink, box); break;
                case "objects/phone": Phone(dc, ink, box); break;
                case "objects/cart": Cart(dc, ink, box); break;
                case "objects/clock": Clock(dc, ink, box); break;
                case "objects/calendar": Calendar(dc, ink, box); break;
                case "objects/trash": Trash(dc, ink, box); break;
                case "objects/share": Share(dc, ink, box); break;
                case "objects/download": DownUp(dc, ink, box, down: true); break;
                case "objects/upload": DownUp(dc, ink, box, down: false); break;
                case "objects/wifi": Wifi(dc, ink, box); break;
                case "objects/battery": Battery(dc, ink, box); break;
                case "objects/user": User(dc, ink, box); break;
                default: Triangle(dc, ink, box, 0.18, 0.12, 0.18, 0.88, 0.86, 0.5); break;
            }
        }
    }

    private static void Smile(DrawingContext dc, Box box, string id)
    {
        var face = Painting.Brush("#F6C445");
        var ink = Painting.Brush("#3A2A16");
        var blush = Painting.Brush("#E39A6A");
        dc.DrawEllipse(face, null, box.P(0.5, 0.5), box.W * 0.48, box.W * 0.48);
        void Eye(double x, bool open)
        {
            if (open)
                dc.DrawEllipse(ink, null, box.P(x, 0.40), box.W * 0.045, box.W * 0.055);
            else
                Line(dc, ink, box, x - 0.07, 0.40, x + 0.07, 0.40, 0.045);
        }

        var grin = id is "smiles/grin" or "smiles/party" or "smiles/starry";
        var sad = id is "smiles/sad" or "smiles/cry" or "smiles/worried";
        if (id == "smiles/love")
        {
            Heart(dc, Painting.Brush("#E15B64"), box.P(0.34, 0.40), box.W * 0.09);
            Heart(dc, Painting.Brush("#E15B64"), box.P(0.66, 0.40), box.W * 0.09);
        }
        else if (id == "smiles/starry")
        {
            Star(dc, Painting.Brush("#E7A23A"), box.P(0.34, 0.40), box.W * 0.09);
            Star(dc, Painting.Brush("#E7A23A"), box.P(0.66, 0.40), box.W * 0.09);
        }
        else if (id == "smiles/cool")
        {
            dc.DrawRectangle(ink, null, box.R(0.16, 0.34, 0.28, 0.12), box.W * 0.04, box.W * 0.04);
            dc.DrawRectangle(ink, null, box.R(0.56, 0.34, 0.28, 0.12), box.W * 0.04, box.W * 0.04);
            Line(dc, ink, box, 0.44, 0.40, 0.56, 0.40, 0.04);
        }
        else if (id == "smiles/wink")
        {
            Eye(0.34, true);
            Line(dc, ink, box, 0.58, 0.40, 0.74, 0.40, 0.045);
        }
        else if (id == "smiles/sleepy")
        {
            Line(dc, ink, box, 0.26, 0.42, 0.40, 0.38, 0.045);
            Line(dc, ink, box, 0.60, 0.38, 0.74, 0.42, 0.045);
        }
        else
        {
            Eye(0.34, id != "smiles/kiss");
            Eye(0.66, id != "smiles/kiss");
        }

        if (id == "smiles/angry")
        {
            Line(dc, ink, box, 0.24, 0.28, 0.42, 0.34, 0.045);
            Line(dc, ink, box, 0.76, 0.28, 0.58, 0.34, 0.045);
        }
        else if (id == "smiles/worried")
        {
            Line(dc, ink, box, 0.26, 0.30, 0.42, 0.26, 0.04);
            Line(dc, ink, box, 0.74, 0.30, 0.58, 0.26, 0.04);
        }

        if (id == "smiles/kiss")
            dc.DrawEllipse(Painting.Brush("#E15B64"), null, box.P(0.5, 0.66), box.W * 0.08, box.W * 0.06);
        else if (grin)
            dc.DrawEllipse(ink, null, box.P(0.5, 0.66), box.W * 0.16, box.W * 0.10);
        else if (sad)
            ArcMouth(dc, ink, box, 0.32, 0.78, 0.68, 0.78, down: false);
        else if (id == "smiles/neutral" || id == "smiles/think")
            Line(dc, ink, box, 0.36, 0.68, 0.64, 0.68, 0.045);
        else
            ArcMouth(dc, ink, box, 0.32, 0.58, 0.68, 0.58, down: true);

        if (id == "smiles/cry")
            dc.DrawEllipse(Painting.Brush("#7EC8E3"), null, box.P(0.72, 0.58), box.W * 0.035, box.W * 0.06);
        if (id is "smiles/smile" or "smiles/wink" or "smiles/love")
        {
            dc.DrawEllipse(blush, null, box.P(0.22, 0.56), box.W * 0.06, box.W * 0.035);
            dc.DrawEllipse(blush, null, box.P(0.78, 0.56), box.W * 0.06, box.W * 0.035);
        }
        if (id == "smiles/party")
            Triangle(dc, Painting.Brush("#E15B64"), box, 0.28, 0.22, 0.50, 0.02, 0.62, 0.24);
        if (id == "smiles/think")
            dc.DrawEllipse(ink, null, box.P(0.72, 0.78), box.W * 0.08, box.W * 0.05);
    }

    private static void DrawFlag(DrawingContext dc, double w, double h, string spec)
    {
        if (spec.StartsWith("h:", StringComparison.Ordinal))
            Bands(dc, w, h, true, spec[2..]);
        else if (spec.StartsWith("v:", StringComparison.Ordinal))
            Bands(dc, w, h, false, spec[2..]);
        else if (spec.StartsWith("disc:", StringComparison.Ordinal))
            Disc(dc, w, h, spec[5..], 0.5);
        else if (spec.StartsWith("discl:", StringComparison.Ordinal))
            Disc(dc, w, h, spec[6..], 0.42);
        else if (spec.StartsWith("nordic2:", StringComparison.Ordinal))
            Nordic(dc, w, h, spec[8..], inner: true);
        else if (spec.StartsWith("nordic:", StringComparison.Ordinal))
            Nordic(dc, w, h, spec[7..], inner: false);
        else if (spec.StartsWith("plus:", StringComparison.Ordinal))
            PlusFlag(dc, w, h, spec[5..], toEdge: false);
        else if (spec.StartsWith("cross:", StringComparison.Ordinal))
            PlusFlag(dc, w, h, spec[6..], toEdge: true);
        else if (spec.StartsWith("saltire:", StringComparison.Ordinal))
            Saltire(dc, w, h, spec[8..]);
        else if (spec.StartsWith("star:", StringComparison.Ordinal))
            StarFlag(dc, w, h, spec[5..]);
        else if (spec.StartsWith("crescent:", StringComparison.Ordinal))
            Crescent(dc, w, h, spec[9..]);
        else if (spec.StartsWith("tri:", StringComparison.Ordinal))
            TriangleFlag(dc, w, h, spec[4..]);
        else
        {
            switch (spec)
            {
                case "us": UnitedStates(dc, w, h); break;
                case "uk": UnitedKingdom(dc, w, h); break;
                case "cn": China(dc, w, h); break;
                case "ca": Canada(dc, w, h); break;
                case "br": Brazil(dc, w, h); break;
                case "in": India(dc, w, h); break;
                case "gr": Greece(dc, w, h); break;
                case "il": Israel(dc, w, h); break;
                case "eu": Europe(dc, w, h); break;
                case "rain": Rainbow(dc, w, h); break;
                case "uae": Emirates(dc, w, h); break;
                case "pk": Pakistan(dc, w, h); break;
                case "cl": Chile(dc, w, h); break;
                case "cu": Cuba(dc, w, h); break;
                case "gl": Greenland(dc, w, h); break;
                case "pt": Portugal(dc, w, h); break;
                case "kr": Korea(dc, w, h); break;
                case "gh": Ghana(dc, w, h); break;
                default: Bands(dc, w, h, true, "#888888,#DDDDDD"); break;
            }
        }
    }

    private static void Bands(DrawingContext dc, double w, double h, bool horizontal, string body)
    {
        var bands = ParseBands(body);
        var total = bands.Sum(item => item.Weight);
        var cursor = 0.0;
        foreach (var band in bands)
        {
            var size = (horizontal ? h : w) * band.Weight / total;
            var rect = horizontal
                ? new Rect(0, cursor, w, size + 0.5)
                : new Rect(cursor, 0, size + 0.5, h);
            dc.DrawRectangle(new SolidColorBrush(band.Color), null, rect);
            cursor += size;
        }
    }

    private static void Disc(DrawingContext dc, double w, double h, string body, double cx)
    {
        var colors = ParseBands(body);
        dc.DrawRectangle(new SolidColorBrush(colors[0].Color), null, new Rect(0, 0, w, h));
        var r = Math.Min(w, h) * 0.28;
        dc.DrawEllipse(new SolidColorBrush(colors[1].Color), null, new Point(w * cx, h * 0.5), r, r);
    }

    private static void Nordic(DrawingContext dc, double w, double h, string body, bool inner)
    {
        var colors = ParseBands(body);
        dc.DrawRectangle(new SolidColorBrush(colors[0].Color), null, new Rect(0, 0, w, h));
        var outer = Math.Min(w, h) * (inner ? 0.28 : 0.18);
        var x = w * 0.30;
        dc.DrawRectangle(new SolidColorBrush(colors[1].Color), null, new Rect(x - outer / 2, 0, outer, h));
        dc.DrawRectangle(new SolidColorBrush(colors[1].Color), null, new Rect(0, (h - outer) / 2, w, outer));
        if (!inner || colors.Length < 3)
            return;
        var thin = outer * 0.45;
        dc.DrawRectangle(new SolidColorBrush(colors[2].Color), null, new Rect(x - thin / 2, 0, thin, h));
        dc.DrawRectangle(new SolidColorBrush(colors[2].Color), null, new Rect(0, (h - thin) / 2, w, thin));
    }

    private static void PlusFlag(DrawingContext dc, double w, double h, string body, bool toEdge)
    {
        var colors = ParseBands(body);
        dc.DrawRectangle(new SolidColorBrush(colors[0].Color), null, new Rect(0, 0, w, h));
        var t = Math.Min(w, h) * (toEdge ? 0.22 : 0.22);
        var arm = toEdge ? 1.0 : 0.62;
        var brush = new SolidColorBrush(colors[1].Color);
        dc.DrawRectangle(brush, null, new Rect((w - t) / 2, h * (1 - arm) / 2, t, h * arm));
        dc.DrawRectangle(brush, null, new Rect(w * (1 - arm) / 2, (h - t) / 2, w * arm, t));
    }

    private static void Saltire(DrawingContext dc, double w, double h, string body)
    {
        var colors = ParseBands(body);
        dc.DrawRectangle(new SolidColorBrush(colors[0].Color), null, new Rect(0, 0, w, h));
        var pen = new Pen(new SolidColorBrush(colors[1].Color), Math.Min(w, h) * 0.18) { LineCap = PenLineCap.Flat };
        dc.DrawLine(pen, new Point(0, 0), new Point(w, h));
        dc.DrawLine(pen, new Point(w, 0), new Point(0, h));
    }

    private static void StarFlag(DrawingContext dc, double w, double h, string body)
    {
        var colors = ParseBands(body);
        dc.DrawRectangle(new SolidColorBrush(colors[0].Color), null, new Rect(0, 0, w, h));
        Star(dc, new SolidColorBrush(colors[1].Color), new Point(w * 0.5, h * 0.5), Math.Min(w, h) * 0.28);
    }

    private static void Crescent(DrawingContext dc, double w, double h, string body)
    {
        var colors = ParseBands(body);
        var field = new SolidColorBrush(colors[0].Color);
        var mark = new SolidColorBrush(colors[1].Color);
        dc.DrawRectangle(field, null, new Rect(0, 0, w, h));
        var r = Math.Min(w, h) * 0.28;
        dc.DrawEllipse(mark, null, new Point(w * 0.46, h * 0.5), r, r);
        dc.DrawEllipse(field, null, new Point(w * 0.54, h * 0.46), r * 0.82, r * 0.82);
        Star(dc, mark, new Point(w * 0.68, h * 0.5), r * 0.38);
    }

    private static void TriangleFlag(DrawingContext dc, double w, double h, string body)
    {
        var colors = ParseBands(body);
        if (colors.Length >= 3)
            Bands(dc, w, h, true, string.Join(",", colors.Take(3).Select(item => $"#{item.Color.R:X2}{item.Color.G:X2}{item.Color.B:X2}")));
        if (colors.Length < 4)
            return;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, 0), true);
            ctx.LineTo(new Point(w * 0.42, h * 0.5));
            ctx.LineTo(new Point(0, h));
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(colors[3].Color), null, geo);
    }

    private static void UnitedStates(DrawingContext dc, double w, double h)
    {
        for (var i = 0; i < 13; i++)
        {
            var brush = new SolidColorBrush(i % 2 == 0 ? Color.Parse("#B22234") : Color.Parse("#FFFFFF"));
            dc.DrawRectangle(brush, null, new Rect(0, h * i / 13.0, w, h / 13.0 + 0.5));
        }
        var cw = w * 0.4;
        var ch = h * 7 / 13.0;
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#3C3B6E")), null, new Rect(0, 0, cw, ch));
        var star = new SolidColorBrush(Colors.White);
        for (var row = 0; row < 5; row++)
        for (var col = 0; col < 6; col++)
            dc.DrawEllipse(star, null, new Point(cw * (col + 0.5) / 6.0, ch * (row + 0.5) / 5.0), Math.Max(0.8, cw * 0.03), Math.Max(0.8, cw * 0.03));
    }

    private static void UnitedKingdom(DrawingContext dc, double w, double h)
    {
        var blue = new SolidColorBrush(Color.Parse("#012169"));
        var white = new SolidColorBrush(Colors.White);
        var red = new SolidColorBrush(Color.Parse("#C8102E"));
        dc.DrawRectangle(blue, null, new Rect(0, 0, w, h));
        var saltire = new Pen(white, Math.Min(w, h) * 0.16) { LineCap = PenLineCap.Flat };
        dc.DrawLine(saltire, new Point(0, 0), new Point(w, h));
        dc.DrawLine(saltire, new Point(w, 0), new Point(0, h));
        var redX = new Pen(red, Math.Min(w, h) * 0.07) { LineCap = PenLineCap.Flat };
        dc.DrawLine(redX, new Point(0, 0), new Point(w, h));
        dc.DrawLine(redX, new Point(w, 0), new Point(0, h));
        var t = Math.Min(w, h) * 0.20;
        dc.DrawRectangle(white, null, new Rect((w - t) / 2, 0, t, h));
        dc.DrawRectangle(white, null, new Rect(0, (h - t) / 2, w, t));
        var thin = t * 0.55;
        dc.DrawRectangle(red, null, new Rect((w - thin) / 2, 0, thin, h));
        dc.DrawRectangle(red, null, new Rect(0, (h - thin) / 2, w, thin));
    }

    private static void China(DrawingContext dc, double w, double h)
    {
        var red = new SolidColorBrush(Color.Parse("#DE2910"));
        var yellow = new SolidColorBrush(Color.Parse("#FFDE00"));
        dc.DrawRectangle(red, null, new Rect(0, 0, w, h));
        Star(dc, yellow, new Point(w * 0.22, h * 0.28), Math.Min(w, h) * 0.16);
        foreach (var spot in new[] { (0.40, 0.12), (0.48, 0.24), (0.48, 0.40), (0.40, 0.52) })
            Star(dc, yellow, new Point(w * spot.Item1, h * spot.Item2), Math.Min(w, h) * 0.05);
    }

    private static void Canada(DrawingContext dc, double w, double h)
    {
        var red = new SolidColorBrush(Color.Parse("#FF0000"));
        dc.DrawRectangle(red, null, new Rect(0, 0, w * 0.25, h));
        dc.DrawRectangle(new SolidColorBrush(Colors.White), null, new Rect(w * 0.25, 0, w * 0.5, h));
        dc.DrawRectangle(red, null, new Rect(w * 0.75, 0, w * 0.25, h));
        var leaf = new (double X, double Y)[]
        {
            (0.50, 0.16), (0.56, 0.32), (0.68, 0.30), (0.60, 0.44), (0.72, 0.50),
            (0.58, 0.54), (0.64, 0.68), (0.54, 0.60), (0.53, 0.84), (0.47, 0.84),
            (0.46, 0.60), (0.36, 0.68), (0.42, 0.54), (0.28, 0.50), (0.40, 0.44),
            (0.32, 0.30), (0.44, 0.32)
        };
        Poly(dc, red, leaf.Select(p => new Point(w * p.X, h * p.Y)).ToArray());
    }

    private static void Brazil(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#009C3B")), null, new Rect(0, 0, w, h));
        Poly(dc, new SolidColorBrush(Color.Parse("#FFDF00")), new Point(w * 0.5, h * 0.12), new Point(w * 0.90, h * 0.5), new Point(w * 0.5, h * 0.88), new Point(w * 0.10, h * 0.5));
        dc.DrawEllipse(new SolidColorBrush(Color.Parse("#002776")), null, new Point(w * 0.5, h * 0.5), Math.Min(w, h) * 0.22, Math.Min(w, h) * 0.22);
    }

    private static void India(DrawingContext dc, double w, double h)
    {
        Bands(dc, w, h, true, "#FF9933,#FFFFFF,#138808");
        var c = new Point(w * 0.5, h * 0.5);
        var r = Math.Min(w, h) * 0.12;
        var navy = new Pen(new SolidColorBrush(Color.Parse("#000080")), Math.Max(1, r * 0.16));
        dc.DrawEllipse(null, navy, c, r, r);
        for (var i = 0; i < 8; i++)
        {
            var a = Math.Tau * i / 8.0;
            dc.DrawLine(navy, c, new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r));
        }
    }

    private static void Greece(DrawingContext dc, double w, double h)
    {
        var blue = new SolidColorBrush(Color.Parse("#0D5EAF"));
        var white = new SolidColorBrush(Colors.White);
        for (var i = 0; i < 9; i++)
            dc.DrawRectangle(i % 2 == 0 ? blue : white, null, new Rect(0, h * i / 9.0, w, h / 9.0 + 0.5));
        var s = h * 5 / 9.0;
        dc.DrawRectangle(blue, null, new Rect(0, 0, s, s));
        var t = s * 0.22;
        dc.DrawRectangle(white, null, new Rect((s - t) / 2, 0, t, s));
        dc.DrawRectangle(white, null, new Rect(0, (s - t) / 2, s, t));
    }

    private static void Israel(DrawingContext dc, double w, double h)
    {
        var blue = new SolidColorBrush(Color.Parse("#0038B8"));
        dc.DrawRectangle(new SolidColorBrush(Colors.White), null, new Rect(0, 0, w, h));
        dc.DrawRectangle(blue, null, new Rect(0, h * 0.18, w, h * 0.12));
        dc.DrawRectangle(blue, null, new Rect(0, h * 0.70, w, h * 0.12));
        var pen = new Pen(blue, Math.Max(1.2, Math.Min(w, h) * 0.035));
        Point A(double x, double y) => new(w * x, h * y);
        dc.DrawLine(pen, A(0.38, 0.58), A(0.50, 0.36));
        dc.DrawLine(pen, A(0.50, 0.36), A(0.62, 0.58));
        dc.DrawLine(pen, A(0.62, 0.58), A(0.38, 0.58));
        dc.DrawLine(pen, A(0.38, 0.42), A(0.62, 0.42));
        dc.DrawLine(pen, A(0.62, 0.42), A(0.50, 0.64));
        dc.DrawLine(pen, A(0.50, 0.64), A(0.38, 0.42));
    }

    private static void Europe(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#003399")), null, new Rect(0, 0, w, h));
        var yellow = new SolidColorBrush(Color.Parse("#FFCC00"));
        var radius = Math.Min(w, h) * 0.32;
        for (var i = 0; i < 12; i++)
        {
            var a = Math.Tau * i / 12.0 - Math.PI / 2;
            Star(dc, yellow, new Point(w * 0.5 + Math.Cos(a) * radius, h * 0.5 + Math.Sin(a) * radius), Math.Min(w, h) * 0.045);
        }
    }

    private static void Rainbow(DrawingContext dc, double w, double h)
    {
        Bands(dc, w, h, true, "#E40303,#FF8C00,#FFED00,#008026,#24408E,#732982");
    }

    private static void Emirates(DrawingContext dc, double w, double h)
    {
        Bands(dc, w, h, true, "#00732F,#FFFFFF,#000000");
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#FF0000")), null, new Rect(0, 0, w * 0.25, h));
    }

    private static void Pakistan(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Colors.White), null, new Rect(0, 0, w * 0.25, h));
        var green = new SolidColorBrush(Color.Parse("#01411C"));
        dc.DrawRectangle(green, null, new Rect(w * 0.25, 0, w * 0.75, h));
        var mark = new SolidColorBrush(Colors.White);
        var r = Math.Min(w, h) * 0.22;
        dc.DrawEllipse(mark, null, new Point(w * 0.58, h * 0.5), r, r);
        dc.DrawEllipse(green, null, new Point(w * 0.64, h * 0.46), r * 0.82, r * 0.82);
        Star(dc, mark, new Point(w * 0.72, h * 0.46), r * 0.36);
    }

    private static void Chile(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Colors.White), null, new Rect(0, 0, w, h * 0.5));
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#D52B1E")), null, new Rect(0, h * 0.5, w, h * 0.5));
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#0039A6")), null, new Rect(0, 0, w * 0.33, h * 0.5));
        Star(dc, new SolidColorBrush(Colors.White), new Point(w * 0.165, h * 0.25), Math.Min(w, h) * 0.1);
    }

    private static void Cuba(DrawingContext dc, double w, double h)
    {
        var blue = new SolidColorBrush(Color.Parse("#002A8F"));
        var white = new SolidColorBrush(Colors.White);
        for (var i = 0; i < 5; i++)
            dc.DrawRectangle(i % 2 == 0 ? blue : white, null, new Rect(0, h * i / 5.0, w, h / 5.0 + 0.5));
        Poly(dc, new SolidColorBrush(Color.Parse("#CB1515")), new Point(0, 0), new Point(w * 0.4, h * 0.5), new Point(0, h));
        Star(dc, white, new Point(w * 0.15, h * 0.5), Math.Min(w, h) * 0.1);
    }

    private static void Greenland(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Colors.White), null, new Rect(0, 0, w, h * 0.5));
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#D00C33")), null, new Rect(0, h * 0.5, w, h * 0.5));
        var r = Math.Min(w, h) * 0.28;
        var c = new Point(w * 0.38, h * 0.5);
        dc.DrawEllipse(new SolidColorBrush(Color.Parse("#D00C33")), null, c, r, r);
        using (dc.PushClip(new Rect(0, h * 0.5, w, h * 0.5)))
            dc.DrawEllipse(new SolidColorBrush(Colors.White), null, c, r, r);
    }

    private static void Portugal(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#006600")), null, new Rect(0, 0, w * 0.4, h));
        dc.DrawRectangle(new SolidColorBrush(Color.Parse("#FF0000")), null, new Rect(w * 0.4, 0, w * 0.6, h));
        var c = new Point(w * 0.4, h * 0.5);
        var r = Math.Min(w, h) * 0.18;
        dc.DrawEllipse(new SolidColorBrush(Color.Parse("#FFCC00")), null, c, r, r);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#FF0000")), Math.Max(1, r * 0.18)), c, r * 0.62, r * 0.62);
    }

    private static void Korea(DrawingContext dc, double w, double h)
    {
        dc.DrawRectangle(new SolidColorBrush(Colors.White), null, new Rect(0, 0, w, h));
        var c = new Point(w * 0.5, h * 0.5);
        var r = Math.Min(w, h) * 0.22;
        dc.DrawEllipse(new SolidColorBrush(Color.Parse("#CD2E3A")), null, c, r, r);
        using (dc.PushClip(new Rect(c.X - r, c.Y, r * 2, r)))
            dc.DrawEllipse(new SolidColorBrush(Color.Parse("#0047A0")), null, c, r, r);
        var pen = new Pen(new SolidColorBrush(Color.Parse("#000000")), Math.Max(1, h * 0.035));
        void Bars(double x, double y, double angle, int gaps)
        {
            for (var i = 0; i < 3; i++)
            {
                if (gaps == i)
                    continue;
                var ox = (i - 1) * h * 0.07;
                var a = angle;
                var p = new Point(x + Math.Cos(a + Math.PI / 2) * ox, y + Math.Sin(a + Math.PI / 2) * ox);
                var d = Math.Min(w, h) * 0.12;
                dc.DrawLine(pen, new Point(p.X - Math.Cos(a) * d, p.Y - Math.Sin(a) * d), new Point(p.X + Math.Cos(a) * d, p.Y + Math.Sin(a) * d));
            }
        }
        Bars(w * 0.22, h * 0.24, 0.6, 1);
        Bars(w * 0.78, h * 0.24, -0.6, -1);
        Bars(w * 0.22, h * 0.76, -0.6, -1);
        Bars(w * 0.78, h * 0.76, 0.6, 0);
    }

    private static void Ghana(DrawingContext dc, double w, double h)
    {
        Bands(dc, w, h, true, "#CE1126,#FCD116,#006B3F");
        Star(dc, new SolidColorBrush(Color.Parse("#000000")), new Point(w * 0.5, h * 0.5), Math.Min(w, h) * 0.16);
    }

    private static void Triangle(DrawingContext dc, IBrush brush, Box box, double x1, double y1, double x2, double y2, double x3, double y3) =>
        Poly(dc, brush, box.P(x1, y1), box.P(x2, y2), box.P(x3, y3));

    private static void Bars(DrawingContext dc, IBrush brush, Box box, int count)
    {
        var gap = 0.14;
        var width = (0.68 - gap * (count - 1)) / count;
        for (var i = 0; i < count; i++)
            dc.DrawRectangle(brush, null, box.R(0.16 + i * (width + gap), 0.14, width, 0.72), box.W * 0.04, box.W * 0.04);
    }

    private static void Previous(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.12, 0.16, 0.14, 0.68));
        Triangle(dc, brush, box, 0.86, 0.12, 0.86, 0.88, 0.36, 0.5);
    }

    private static void Next(DrawingContext dc, IBrush brush, Box box)
    {
        Triangle(dc, brush, box, 0.14, 0.12, 0.14, 0.88, 0.64, 0.5);
        dc.DrawRectangle(brush, null, box.R(0.74, 0.16, 0.14, 0.68));
    }

    private static void Doubles(DrawingContext dc, IBrush brush, Box box, bool left)
    {
        if (left)
        {
            Triangle(dc, brush, box, 0.52, 0.14, 0.52, 0.86, 0.12, 0.5);
            Triangle(dc, brush, box, 0.92, 0.14, 0.92, 0.86, 0.52, 0.5);
        }
        else
        {
            Triangle(dc, brush, box, 0.08, 0.14, 0.08, 0.86, 0.48, 0.5);
            Triangle(dc, brush, box, 0.48, 0.14, 0.48, 0.86, 0.88, 0.5);
        }
    }

    private static void Volume(DrawingContext dc, IBrush brush, Box box, bool mute)
    {
        Poly(dc, brush, box.P(0.12, 0.38), box.P(0.30, 0.38), box.P(0.48, 0.18), box.P(0.48, 0.82), box.P(0.30, 0.62), box.P(0.12, 0.62));
        var pen = new Pen(brush, Math.Max(1.4, box.W * 0.07)) { LineCap = PenLineCap.Round };
        dc.DrawLine(pen, box.P(0.60, 0.36), box.P(0.78, 0.28));
        dc.DrawLine(pen, box.P(0.60, 0.50), box.P(0.84, 0.50));
        dc.DrawLine(pen, box.P(0.60, 0.64), box.P(0.78, 0.72));
        if (mute)
            Cross(dc, brush, box);
    }

    private static void Music(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(brush, null, box.P(0.32, 0.72), box.W * 0.14, box.W * 0.11);
        dc.DrawEllipse(brush, null, box.P(0.68, 0.62), box.W * 0.14, box.W * 0.11);
        var pen = new Pen(brush, Math.Max(1.6, box.W * 0.07)) { LineCap = PenLineCap.Square };
        dc.DrawLine(pen, box.P(0.44, 0.72), box.P(0.44, 0.22));
        dc.DrawLine(pen, box.P(0.80, 0.62), box.P(0.80, 0.16));
        dc.DrawLine(pen, box.P(0.44, 0.22), box.P(0.80, 0.16));
    }

    private static void Mic(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.36, 0.10, 0.28, 0.42), box.W * 0.14, box.W * 0.14);
        dc.DrawLine(new Pen(brush, Math.Max(1.5, box.W * 0.07)) { LineCap = PenLineCap.Round }, box.P(0.24, 0.42), box.P(0.24, 0.52));
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(box.P(0.24, 0.48), false);
            ctx.ArcTo(box.P(0.76, 0.48), new Size(box.W * 0.26, box.W * 0.26), 0, false, SweepDirection.Clockwise);
        }
        dc.DrawGeometry(null, new Pen(brush, Math.Max(1.5, box.W * 0.07)) { LineCap = PenLineCap.Round }, arc);
        Line(dc, brush, box, 0.5, 0.74, 0.5, 0.88, 0.07);
        Line(dc, brush, box, 0.34, 0.88, 0.66, 0.88, 0.07);
    }

    private static void Camera(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.10, 0.28, 0.80, 0.52), box.W * 0.08, box.W * 0.08);
        dc.DrawRectangle(brush, null, box.R(0.34, 0.16, 0.24, 0.14));
        dc.DrawEllipse(brush, null, box.P(0.5, 0.54), box.W * 0.16, box.W * 0.16);
        dc.DrawEllipse(Painting.Brush("#163246"), null, box.P(0.5, 0.54), box.W * 0.07, box.W * 0.07);
    }

    private static void Headphones(DrawingContext dc, IBrush brush, Box box)
    {
        var pen = new Pen(brush, Math.Max(2, box.W * 0.08)) { LineCap = PenLineCap.Round };
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(box.P(0.22, 0.58), false);
            ctx.ArcTo(box.P(0.78, 0.58), new Size(box.W * 0.28, box.W * 0.34), 0, false, SweepDirection.Clockwise);
        }
        dc.DrawGeometry(null, pen, arc);
        dc.DrawRectangle(brush, null, box.R(0.12, 0.52, 0.16, 0.28), box.W * 0.04, box.W * 0.04);
        dc.DrawRectangle(brush, null, box.R(0.72, 0.52, 0.16, 0.28), box.W * 0.04, box.W * 0.04);
    }

    private static void Repeat(DrawingContext dc, IBrush brush, Box box)
    {
        var pen = new Pen(brush, Math.Max(1.6, box.W * 0.08)) { LineCap = PenLineCap.Round };
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(box.P(0.72, 0.30), false);
            ctx.ArcTo(box.P(0.28, 0.70), new Size(box.W * 0.28, box.W * 0.28), 0, true, SweepDirection.CounterClockwise);
        }
        dc.DrawGeometry(null, pen, arc);
        Triangle(dc, brush, box, 0.62, 0.14, 0.86, 0.18, 0.70, 0.38);
    }

    private static void Shuffle(DrawingContext dc, IBrush brush, Box box)
    {
        Line(dc, brush, box, 0.16, 0.28, 0.46, 0.28, 0.07);
        Line(dc, brush, box, 0.46, 0.28, 0.84, 0.72, 0.07);
        Line(dc, brush, box, 0.16, 0.72, 0.46, 0.72, 0.07);
        Line(dc, brush, box, 0.46, 0.72, 0.84, 0.28, 0.07);
        Triangle(dc, brush, box, 0.70, 0.16, 0.90, 0.28, 0.70, 0.40);
        Triangle(dc, brush, box, 0.70, 0.60, 0.90, 0.72, 0.70, 0.84);
    }

    private static void Arrow(DrawingContext dc, IBrush brush, Box box, double degrees)
    {
        var a = degrees * Math.PI / 180.0;
        Point R(double x, double y)
        {
            var dx = x - 0.5;
            var dy = y - 0.5;
            var c = Math.Cos(a);
            var s = Math.Sin(a);
            return box.P(0.5 + dx * c - dy * s, 0.5 + dx * s + dy * c);
        }
        Poly(dc, brush, R(0.86, 0.5), R(0.28, 0.16), R(0.28, 0.36), R(0.08, 0.36), R(0.08, 0.64), R(0.28, 0.64), R(0.28, 0.84));
    }

    private static void ArcArrow(DrawingContext dc, IBrush brush, Box box, bool left)
    {
        var pen = new Pen(brush, Math.Max(1.6, box.W * 0.08)) { LineCap = PenLineCap.Round };
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(box.P(left ? 0.68 : 0.32, 0.30), false);
            ctx.ArcTo(box.P(left ? 0.32 : 0.68, 0.70), new Size(box.W * 0.26, box.W * 0.26), 0, false, left ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);
        }
        dc.DrawGeometry(null, pen, arc);
        if (left)
            Triangle(dc, brush, box, 0.18, 0.22, 0.42, 0.16, 0.34, 0.40);
        else
            Triangle(dc, brush, box, 0.82, 0.22, 0.58, 0.16, 0.66, 0.40);
    }

    private static void Expand(DrawingContext dc, IBrush brush, Box box)
    {
        Arrow(dc, brush, new Box(box.X, box.Y, box.W * 0.55, box.H * 0.55), -45);
        Arrow(dc, brush, new Box(box.X + box.W * 0.45, box.Y + box.H * 0.45, box.W * 0.55, box.H * 0.55), 135);
    }

    private static void Back(DrawingContext dc, IBrush brush, Box box)
    {
        Line(dc, brush, box, 0.62, 0.22, 0.28, 0.50, 0.10);
        Line(dc, brush, box, 0.28, 0.50, 0.62, 0.78, 0.10);
    }

    private static void Leave(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(null, new Pen(brush, Math.Max(1.5, box.W * 0.07)), box.R(0.12, 0.18, 0.46, 0.64));
        Arrow(dc, brush, new Box(box.X + box.W * 0.34, box.Y + box.H * 0.22, box.W * 0.62, box.H * 0.56), 0);
    }

    private static void Mark(DrawingContext dc, IBrush brush, Box box, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        Line(dc, brush, box, x1, y1, x2, y2, 0.12);
        Line(dc, brush, box, x2, y2, x3, y3, 0.12);
    }

    private static void Cross(DrawingContext dc, IBrush brush, Box box)
    {
        Line(dc, brush, box, 0.24, 0.24, 0.76, 0.76, 0.10);
        Line(dc, brush, box, 0.76, 0.24, 0.24, 0.76, 0.10);
    }

    private static void Plus(DrawingContext dc, IBrush brush, Box box)
    {
        Line(dc, brush, box, 0.5, 0.16, 0.5, 0.84, 0.14);
        Line(dc, brush, box, 0.16, 0.5, 0.84, 0.5, 0.14);
    }

    private static void Search(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(null, new Pen(brush, Math.Max(1.6, box.W * 0.08)), box.P(0.42, 0.42), box.W * 0.22, box.W * 0.22);
        Line(dc, brush, box, 0.58, 0.58, 0.82, 0.82, 0.09);
    }

    private static void Info(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(null, new Pen(brush, Math.Max(1.6, box.W * 0.07)), box.P(0.5, 0.5), box.W * 0.36, box.W * 0.36);
        dc.DrawEllipse(brush, null, box.P(0.5, 0.32), box.W * 0.045, box.W * 0.045);
        Line(dc, brush, box, 0.5, 0.44, 0.5, 0.72, 0.08);
    }

    private static void Warning(DrawingContext dc, IBrush brush, Box box)
    {
        Triangle(dc, brush, box, 0.5, 0.10, 0.90, 0.86, 0.10, 0.86);
        Line(dc, Painting.Brush("#163246"), box, 0.5, 0.38, 0.5, 0.62, 0.08);
        dc.DrawEllipse(Painting.Brush("#163246"), null, box.P(0.5, 0.74), box.W * 0.04, box.W * 0.04);
    }

    private static void Question(DrawingContext dc, IBrush brush, Box box)
    {
        var pen = new Pen(brush, Math.Max(1.6, box.W * 0.08)) { LineCap = PenLineCap.Round };
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(box.P(0.32, 0.36), false);
            ctx.ArcTo(box.P(0.62, 0.48), new Size(box.W * 0.20, box.W * 0.20), 0, false, SweepDirection.Clockwise);
        }
        dc.DrawGeometry(null, pen, arc);
        Line(dc, brush, box, 0.62, 0.48, 0.50, 0.64, 0.08);
        dc.DrawEllipse(brush, null, box.P(0.50, 0.78), box.W * 0.045, box.W * 0.045);
    }

    private static void Gear(DrawingContext dc, IBrush brush, Box box)
    {
        var c = box.P(0.5, 0.5);
        for (var i = 0; i < 8; i++)
        {
            var a = Math.Tau * i / 8.0;
            dc.DrawRectangle(brush, null, new Rect(c.X + Math.Cos(a) * box.W * 0.30 - box.W * 0.07, c.Y + Math.Sin(a) * box.W * 0.30 - box.W * 0.07, box.W * 0.14, box.W * 0.14));
        }
        dc.DrawEllipse(brush, null, c, box.W * 0.28, box.W * 0.28);
        dc.DrawEllipse(Painting.Brush("#163246"), null, c, box.W * 0.12, box.W * 0.12);
    }

    private static void Lock(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.22, 0.44, 0.56, 0.42), box.W * 0.06, box.W * 0.06);
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(box.P(0.32, 0.48), false);
            ctx.ArcTo(box.P(0.68, 0.48), new Size(box.W * 0.18, box.W * 0.22), 0, false, SweepDirection.Clockwise);
        }
        dc.DrawGeometry(null, new Pen(brush, Math.Max(1.8, box.W * 0.08)) { LineCap = PenLineCap.Round }, arc);
    }

    private static void Home(DrawingContext dc, IBrush brush, Box box)
    {
        Triangle(dc, brush, box, 0.50, 0.12, 0.90, 0.48, 0.10, 0.48);
        dc.DrawRectangle(brush, null, box.R(0.24, 0.46, 0.52, 0.40));
        dc.DrawRectangle(Painting.Brush("#163246"), null, box.R(0.42, 0.60, 0.16, 0.26));
    }

    private static void Pin(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(brush, null, box.P(0.5, 0.36), box.W * 0.24, box.W * 0.24);
        Triangle(dc, brush, box, 0.28, 0.46, 0.72, 0.46, 0.50, 0.90);
        dc.DrawEllipse(Painting.Brush("#163246"), null, box.P(0.5, 0.36), box.W * 0.09, box.W * 0.09);
    }

    private static void Mail(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.10, 0.22, 0.80, 0.56), box.W * 0.04, box.W * 0.04);
        var pen = new Pen(Painting.Brush("#163246"), Math.Max(1.4, box.W * 0.05));
        dc.DrawLine(pen, box.P(0.14, 0.28), box.P(0.50, 0.52));
        dc.DrawLine(pen, box.P(0.86, 0.28), box.P(0.50, 0.52));
    }

    private static void Phone(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.30, 0.08, 0.40, 0.84), box.W * 0.08, box.W * 0.08);
        dc.DrawEllipse(Painting.Brush("#163246"), null, box.P(0.50, 0.78), box.W * 0.04, box.W * 0.04);
    }

    private static void Cart(DrawingContext dc, IBrush brush, Box box)
    {
        Line(dc, brush, box, 0.16, 0.24, 0.30, 0.24, 0.07);
        Line(dc, brush, box, 0.30, 0.24, 0.40, 0.58, 0.07);
        Line(dc, brush, box, 0.40, 0.58, 0.78, 0.58, 0.07);
        Line(dc, brush, box, 0.36, 0.36, 0.82, 0.36, 0.07);
        dc.DrawEllipse(brush, null, box.P(0.46, 0.76), box.W * 0.06, box.W * 0.06);
        dc.DrawEllipse(brush, null, box.P(0.70, 0.76), box.W * 0.06, box.W * 0.06);
    }

    private static void Clock(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(null, new Pen(brush, Math.Max(1.6, box.W * 0.07)), box.P(0.5, 0.5), box.W * 0.36, box.W * 0.36);
        Line(dc, brush, box, 0.5, 0.5, 0.5, 0.28, 0.07);
        Line(dc, brush, box, 0.5, 0.5, 0.68, 0.58, 0.07);
    }

    private static void Calendar(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(brush, null, box.R(0.14, 0.22, 0.72, 0.64), box.W * 0.06, box.W * 0.06);
        dc.DrawRectangle(Painting.Brush("#E7A23A"), null, box.R(0.14, 0.22, 0.72, 0.18));
        Line(dc, brush, box, 0.32, 0.14, 0.32, 0.30, 0.06);
        Line(dc, brush, box, 0.68, 0.14, 0.68, 0.30, 0.06);
    }

    private static void Trash(DrawingContext dc, IBrush brush, Box box)
    {
        Line(dc, brush, box, 0.22, 0.24, 0.78, 0.24, 0.07);
        dc.DrawRectangle(brush, null, box.R(0.28, 0.32, 0.44, 0.54), box.W * 0.04, box.W * 0.04);
        Line(dc, Painting.Brush("#163246"), box, 0.42, 0.40, 0.42, 0.74, 0.05);
        Line(dc, Painting.Brush("#163246"), box, 0.58, 0.40, 0.58, 0.74, 0.05);
    }

    private static void Share(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(brush, null, box.P(0.24, 0.5), box.W * 0.09, box.W * 0.09);
        dc.DrawEllipse(brush, null, box.P(0.74, 0.26), box.W * 0.09, box.W * 0.09);
        dc.DrawEllipse(brush, null, box.P(0.74, 0.74), box.W * 0.09, box.W * 0.09);
        Line(dc, brush, box, 0.32, 0.46, 0.66, 0.30, 0.06);
        Line(dc, brush, box, 0.32, 0.54, 0.66, 0.70, 0.06);
    }

    private static void DownUp(DrawingContext dc, IBrush brush, Box box, bool down)
    {
        if (down)
        {
            Line(dc, brush, box, 0.5, 0.12, 0.5, 0.62, 0.08);
            Triangle(dc, brush, box, 0.28, 0.50, 0.72, 0.50, 0.50, 0.76);
            Line(dc, brush, box, 0.22, 0.86, 0.78, 0.86, 0.08);
        }
        else
        {
            Line(dc, brush, box, 0.5, 0.88, 0.5, 0.38, 0.08);
            Triangle(dc, brush, box, 0.28, 0.46, 0.72, 0.46, 0.50, 0.20);
            Line(dc, brush, box, 0.22, 0.12, 0.78, 0.12, 0.08);
        }
    }

    private static void Wifi(DrawingContext dc, IBrush brush, Box box)
    {
        var pen = new Pen(brush, Math.Max(1.6, box.W * 0.07)) { LineCap = PenLineCap.Round };
        void Arc(double y, double rx)
        {
            var geo = new StreamGeometry();
            using var ctx = geo.Open();
            ctx.BeginFigure(box.P(0.5 - rx, y), false);
            ctx.ArcTo(box.P(0.5 + rx, y), new Size(box.W * rx, box.W * rx * 0.8), 0, false, SweepDirection.Clockwise);
            dc.DrawGeometry(null, pen, geo);
        }
        Arc(0.46, 0.34);
        Arc(0.58, 0.22);
        dc.DrawEllipse(brush, null, box.P(0.5, 0.74), box.W * 0.06, box.W * 0.06);
    }

    private static void Battery(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawRectangle(null, new Pen(brush, Math.Max(1.5, box.W * 0.07)), box.R(0.12, 0.30, 0.66, 0.40));
        dc.DrawRectangle(brush, null, box.R(0.78, 0.42, 0.08, 0.16));
        dc.DrawRectangle(brush, null, box.R(0.18, 0.36, 0.36, 0.28));
    }

    private static void User(DrawingContext dc, IBrush brush, Box box)
    {
        dc.DrawEllipse(brush, null, box.P(0.5, 0.32), box.W * 0.16, box.W * 0.16);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(box.P(0.18, 0.88), true);
            ctx.ArcTo(box.P(0.82, 0.88), new Size(box.W * 0.34, box.W * 0.28), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static void ArcMouth(DrawingContext dc, IBrush brush, Box box, double x1, double y1, double x2, double y2, bool down)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(box.P(x1, y1), false);
            ctx.ArcTo(box.P(x2, y2), new Size(box.W * 0.22, box.W * 0.16), 0, false, down ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        }
        dc.DrawGeometry(null, new Pen(brush, Math.Max(1.4, box.W * 0.05)) { LineCap = PenLineCap.Round }, geo);
    }

    private static void Heart(DrawingContext dc, IBrush brush, Point center, double radius)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y + radius * 0.85), true);
            ctx.ArcTo(new Point(center.X, center.Y - radius * 0.15), new Size(radius * 0.55, radius * 0.6), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(new Point(center.X, center.Y + radius * 0.85), new Size(radius * 0.55, radius * 0.6), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static void Star(DrawingContext dc, IBrush brush, Point center, double radius)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < 10; i++)
            {
                var angle = -Math.PI / 2 + i * Math.PI / 5;
                var reach = i % 2 == 0 ? radius : radius * 0.42;
                var point = new Point(center.X + Math.Cos(angle) * reach, center.Y + Math.Sin(angle) * reach);
                if (i == 0)
                    ctx.BeginFigure(point, true);
                else
                    ctx.LineTo(point);
            }
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static void Line(DrawingContext dc, IBrush brush, Box box, double x1, double y1, double x2, double y2, double thickness)
    {
        dc.DrawLine(new Pen(brush, Math.Max(1.2, box.W * thickness)) { LineCap = PenLineCap.Round }, box.P(x1, y1), box.P(x2, y2));
    }

    private static void Poly(DrawingContext dc, IBrush brush, params Point[] points)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
                ctx.LineTo(points[i]);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(brush, null, geo);
    }

    private static Box Square(double w, double h, double pad)
    {
        var side = Math.Min(w, h) * (1 - pad * 2);
        return new Box((w - side) / 2, (h - side) / 2, side, side);
    }

    private readonly record struct Box(double X, double Y, double W, double H)
    {
        public Point P(double x, double y) => new(X + W * x, Y + H * y);
        public Rect R(double x, double y, double w, double h) => new(X + W * x, Y + H * y, W * w, H * h);
    }

    private static (double Weight, Color Color)[] ParseBands(string body)
    {
        var parts = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<(double, Color)>();
        foreach (var part in parts)
        {
            var bits = part.Split(':', StringSplitOptions.TrimEntries);
            if (bits.Length == 2 && double.TryParse(bits[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var weight))
                list.Add((weight, Color.Parse(bits[1])));
            else
                list.Add((1, Color.Parse(bits[0])));
        }
        return list.ToArray();
    }
}
