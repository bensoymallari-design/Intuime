namespace Intuime.Core;

public static class LayoutMath
{
    public static double Snap(double value, bool enabled, int grid)
    {
        if (!enabled || grid <= 1)
            return value;
        return Math.Round(value / grid) * grid;
    }

    public static bool Contains(SceneElement element, double x, double y)
    {
        var local = ToLocal(element, x, y);
        return Math.Abs(local.X) <= element.Width / 2 && Math.Abs(local.Y) <= element.Height / 2;
    }

    public static (double X, double Y) ToLocal(SceneElement element, double x, double y)
    {
        var cx = element.X + element.Width / 2;
        var cy = element.Y + element.Height / 2;
        var radians = element.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = x - cx;
        var dy = y - cy;
        return (dx * cos + dy * sin, -dx * sin + dy * cos);
    }

    public static (double Left, double Top, double Right, double Bottom) AxisBounds(SceneElement element)
    {
        var corners = new (double X, double Y)[]
        {
            World(element, 0, 0),
            World(element, element.Width, 0),
            World(element, element.Width, element.Height),
            World(element, 0, element.Height)
        };
        return (
            corners.Min(c => c.X),
            corners.Min(c => c.Y),
            corners.Max(c => c.X),
            corners.Max(c => c.Y));
    }

    public static bool Intersects(SceneElement element, double left, double top, double right, double bottom)
    {
        var bounds = AxisBounds(element);
        return bounds.Right >= left && bounds.Left <= right && bounds.Bottom >= top && bounds.Top <= bottom;
    }

    public static bool Overlaps(SceneElement a, SceneElement b)
    {
        var bounds = AxisBounds(a);
        return Intersects(b, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    public static string? VideoOverlayOwner(IReadOnlyList<SceneElement> elements, SceneElement item)
    {
        if (item.Kind == ElementKind.Video || !item.IsVisible)
            return null;
        SceneElement? owner = null;
        foreach (var video in elements)
        {
            if (video.Kind != ElementKind.Video || !video.IsVisible || video.Z >= item.Z)
                continue;
            if (!Overlaps(video, item))
                continue;
            if (owner == null || video.Z > owner.Z)
                owner = video;
        }
        return owner?.Id;
    }

    public static void Align(IReadOnlyList<SceneElement> items, string edge)
    {
        if (items.Count < 2)
            return;
        switch (edge)
        {
            case "left":
                var left = items.Min(i => i.X);
                foreach (var item in items) item.X = left;
                break;
            case "right":
                var right = items.Max(i => i.X + i.Width);
                foreach (var item in items) item.X = right - item.Width;
                break;
            case "top":
                var top = items.Min(i => i.Y);
                foreach (var item in items) item.Y = top;
                break;
            case "bottom":
                var bottom = items.Max(i => i.Y + i.Height);
                foreach (var item in items) item.Y = bottom - item.Height;
                break;
            case "center":
                var center = items.Average(i => i.X + i.Width / 2);
                foreach (var item in items) item.X = center - item.Width / 2;
                break;
            case "middle":
                var middle = items.Average(i => i.Y + i.Height / 2);
                foreach (var item in items) item.Y = middle - item.Height / 2;
                break;
        }
    }

    public static void Distribute(IReadOnlyList<SceneElement> items, bool horizontal)
    {
        if (items.Count < 3)
            return;
        var ordered = horizontal
            ? items.OrderBy(i => i.X).ToList()
            : items.OrderBy(i => i.Y).ToList();
        var first = horizontal ? ordered[0].X : ordered[0].Y;
        var last = horizontal ? ordered[^1].X : ordered[^1].Y;
        var step = (last - first) / (ordered.Count - 1);
        for (var i = 1; i < ordered.Count - 1; i++)
        {
            if (horizontal) ordered[i].X = first + step * i;
            else ordered[i].Y = first + step * i;
        }
    }

    public static (double X, double Y) World(SceneElement element, double localX, double localY)
    {
        var cx = element.X + element.Width / 2;
        var cy = element.Y + element.Height / 2;
        var radians = element.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = localX - element.Width / 2;
        var dy = localY - element.Height / 2;
        return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
    }
}
