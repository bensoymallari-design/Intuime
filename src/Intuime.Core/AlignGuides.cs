namespace Intuime.Core;

public readonly record struct GuideBox(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

public readonly record struct AlignGuide(double X1, double Y1, double X2, double Y2);

public static class AlignGuides
{
    public static (double X, double Y, IReadOnlyList<AlignGuide> Lines) Snap(
        GuideBox moving,
        double sceneWidth,
        double sceneHeight,
        IReadOnlyList<GuideBox> others,
        double threshold,
        bool grid,
        int gridSize)
    {
        var alignX = AlignmentDelta(moving, sceneWidth, sceneHeight, others, threshold, horizontal: true);
        var alignY = AlignmentDelta(moving, sceneWidth, sceneHeight, others, threshold, horizontal: false);
        var spaceX = SpacingDelta(moving, others, threshold, horizontal: true);
        var spaceY = SpacingDelta(moving, others, threshold, horizontal: false);
        var dx = Pick(alignX, spaceX) ?? GridDelta(moving.X, grid, gridSize);
        var dy = Pick(alignY, spaceY) ?? GridDelta(moving.Y, grid, gridSize);
        var placed = new GuideBox(moving.X + dx, moving.Y + dy, moving.Width, moving.Height);
        var lines = new List<AlignGuide>();
        if (Won(alignX, spaceX))
            AlignmentLines(lines, placed, sceneWidth, sceneHeight, others, horizontal: true);
        else if (spaceX != null)
            SpacingLines(lines, placed, others, horizontal: true);
        if (Won(alignY, spaceY))
            AlignmentLines(lines, placed, sceneWidth, sceneHeight, others, horizontal: false);
        else if (spaceY != null)
            SpacingLines(lines, placed, others, horizontal: false);
        return (placed.X, placed.Y, lines);
    }

    private static bool Won(double? align, double? space) =>
        align != null && (space == null || Math.Abs(align.Value) <= Math.Abs(space.Value) + 0.01);

    private static double? Pick(double? align, double? space)
    {
        if (align == null)
            return space;
        if (space == null)
            return align;
        return Math.Abs(space.Value) < Math.Abs(align.Value) - 0.01 ? space : align;
    }

    private static double GridDelta(double value, bool grid, int size)
    {
        if (!grid || size <= 1)
            return 0;
        return LayoutMath.Snap(value, true, size) - value;
    }

    private static double? AlignmentDelta(GuideBox moving, double sceneWidth, double sceneHeight, IReadOnlyList<GuideBox> others, double threshold, bool horizontal)
    {
        double? best = null;
        foreach (var anchor in Anchors(moving, horizontal))
        {
            foreach (var target in Targets(sceneWidth, sceneHeight, others, horizontal))
            {
                var delta = target - anchor;
                if (Math.Abs(delta) > threshold)
                    continue;
                if (best == null || Math.Abs(delta) < Math.Abs(best.Value))
                    best = delta;
            }
        }
        return best;
    }

    private static void AlignmentLines(List<AlignGuide> lines, GuideBox placed, double sceneWidth, double sceneHeight, IReadOnlyList<GuideBox> others, bool horizontal)
    {
        var anchors = Anchors(placed, horizontal);
        if (horizontal)
        {
            Consider(0, 0, sceneHeight, scene: true);
            Consider(sceneWidth / 2, 0, sceneHeight, scene: true);
            Consider(sceneWidth, 0, sceneHeight, scene: true);
            foreach (var other in others)
            {
                Consider(other.Left, other.Top, other.Bottom, scene: false);
                Consider(other.CenterX, other.Top, other.Bottom, scene: false);
                Consider(other.Right, other.Top, other.Bottom, scene: false);
            }
        }
        else
        {
            Consider(0, 0, sceneWidth, scene: true);
            Consider(sceneHeight / 2, 0, sceneWidth, scene: true);
            Consider(sceneHeight, 0, sceneWidth, scene: true);
            foreach (var other in others)
            {
                Consider(other.Top, other.Left, other.Right, scene: false);
                Consider(other.CenterY, other.Left, other.Right, scene: false);
                Consider(other.Bottom, other.Left, other.Right, scene: false);
            }
        }

        void Consider(double position, double from, double to, bool scene)
        {
            if (!anchors.Any(anchor => Math.Abs(anchor - position) < 0.6))
                return;
            if (scene)
            {
                Add(lines, horizontal, position, from, to);
                return;
            }
            var start = horizontal ? Math.Min(placed.Top, from) : Math.Min(placed.Left, from);
            var end = horizontal ? Math.Max(placed.Bottom, to) : Math.Max(placed.Right, to);
            Add(lines, horizontal, position, start - 12, end + 12);
        }
    }

    private static IEnumerable<double> Anchors(GuideBox box, bool horizontal)
    {
        if (horizontal)
        {
            yield return box.Left;
            yield return box.CenterX;
            yield return box.Right;
        }
        else
        {
            yield return box.Top;
            yield return box.CenterY;
            yield return box.Bottom;
        }
    }

    private static IEnumerable<double> Targets(double sceneWidth, double sceneHeight, IReadOnlyList<GuideBox> others, bool horizontal)
    {
        if (horizontal)
        {
            yield return 0;
            yield return sceneWidth / 2;
            yield return sceneWidth;
            foreach (var other in others)
            {
                yield return other.Left;
                yield return other.CenterX;
                yield return other.Right;
            }
        }
        else
        {
            yield return 0;
            yield return sceneHeight / 2;
            yield return sceneHeight;
            foreach (var other in others)
            {
                yield return other.Top;
                yield return other.CenterY;
                yield return other.Bottom;
            }
        }
    }

    private static double? SpacingDelta(GuideBox moving, IReadOnlyList<GuideBox> others, double threshold, bool horizontal)
    {
        double? best = null;
        void Consider(double delta)
        {
            if (Math.Abs(delta) > threshold)
                return;
            if (best == null || Math.Abs(delta) < Math.Abs(best.Value))
                best = delta;
        }

        var before = Neighbor(moving, others, horizontal, before: true);
        var after = Neighbor(moving, others, horizontal, before: false);
        if (before is GuideBox left && after is GuideBox right)
        {
            var opening = horizontal ? right.Left - left.Right : right.Top - left.Bottom;
            var size = horizontal ? moving.Width : moving.Height;
            if (opening > size + 4)
            {
                var desired = (horizontal ? left.Right : left.Bottom) + (opening - size) / 2;
                Consider(desired - (horizontal ? moving.Left : moving.Top));
            }
        }
        if (before is GuideBox near)
        {
            var further = Neighbor(near, others, horizontal, before: true);
            if (further is GuideBox far)
            {
                var gap = horizontal ? near.Left - far.Right : near.Top - far.Bottom;
                if (gap >= 4)
                {
                    var desired = (horizontal ? near.Right : near.Bottom) + gap;
                    Consider(desired - (horizontal ? moving.Left : moving.Top));
                }
            }
        }
        if (after is GuideBox next)
        {
            var further = Neighbor(next, others, horizontal, before: false);
            if (further is GuideBox far)
            {
                var gap = horizontal ? far.Left - next.Right : far.Top - next.Bottom;
                if (gap >= 4)
                {
                    var size = horizontal ? moving.Width : moving.Height;
                    var desired = (horizontal ? next.Left : next.Top) - gap - size;
                    Consider(desired - (horizontal ? moving.Left : moving.Top));
                }
            }
        }
        return best;
    }

    private static void SpacingLines(List<AlignGuide> lines, GuideBox placed, IReadOnlyList<GuideBox> others, bool horizontal)
    {
        var before = Neighbor(placed, others, horizontal, before: true);
        var after = Neighbor(placed, others, horizontal, before: false);
        var cross = horizontal ? placed.CenterY : placed.CenterX;
        if (before is GuideBox left && after is GuideBox right)
        {
            var gapBefore = horizontal ? placed.Left - left.Right : placed.Top - left.Bottom;
            var gapAfter = horizontal ? right.Left - placed.Right : right.Top - placed.Bottom;
            if (gapBefore >= 2 && Math.Abs(gapBefore - gapAfter) < 1.5)
            {
                Gap(lines, horizontal, horizontal ? left.Right : left.Bottom, horizontal ? placed.Left : placed.Top, cross);
                Gap(lines, horizontal, horizontal ? placed.Right : placed.Bottom, horizontal ? right.Left : right.Top, cross);
                return;
            }
        }
        if (before is GuideBox near)
        {
            var further = Neighbor(near, others, horizontal, before: true);
            if (further is GuideBox far)
            {
                var reference = horizontal ? near.Left - far.Right : near.Top - far.Bottom;
                var gap = horizontal ? placed.Left - near.Right : placed.Top - near.Bottom;
                if (reference >= 4 && Math.Abs(reference - gap) < 1.5)
                {
                    Gap(lines, horizontal, horizontal ? far.Right : far.Bottom, horizontal ? near.Left : near.Top, cross);
                    Gap(lines, horizontal, horizontal ? near.Right : near.Bottom, horizontal ? placed.Left : placed.Top, cross);
                }
            }
        }
        if (after is GuideBox next)
        {
            var further = Neighbor(next, others, horizontal, before: false);
            if (further is GuideBox far)
            {
                var reference = horizontal ? far.Left - next.Right : far.Top - next.Bottom;
                var gap = horizontal ? next.Left - placed.Right : next.Top - placed.Bottom;
                if (reference >= 4 && Math.Abs(reference - gap) < 1.5)
                {
                    Gap(lines, horizontal, horizontal ? next.Right : next.Bottom, horizontal ? far.Left : far.Top, cross);
                    Gap(lines, horizontal, horizontal ? placed.Right : placed.Bottom, horizontal ? next.Left : next.Top, cross);
                }
            }
        }
    }

    private static GuideBox? Neighbor(GuideBox moving, IReadOnlyList<GuideBox> others, bool horizontal, bool before)
    {
        GuideBox? best = null;
        foreach (var other in others)
        {
            if (SameBox(other, moving) || !SharesBand(moving, other, horizontal))
                continue;
            if (before)
            {
                if ((horizontal ? other.CenterX : other.CenterY) >= (horizontal ? moving.Left : moving.Top))
                    continue;
                if (best == null || (horizontal ? other.Right : other.Bottom) > (horizontal ? best.Value.Right : best.Value.Bottom))
                    best = other;
            }
            else
            {
                if ((horizontal ? other.CenterX : other.CenterY) <= (horizontal ? moving.Right : moving.Bottom))
                    continue;
                if (best == null || (horizontal ? other.Left : other.Top) < (horizontal ? best.Value.Left : best.Value.Top))
                    best = other;
            }
        }
        return best;
    }

    private static bool SameBox(GuideBox a, GuideBox b) =>
        Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    private static bool SharesBand(GuideBox a, GuideBox b, bool horizontal)
    {
        var overlap = horizontal
            ? Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top)
            : Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        return overlap > 8;
    }

    private static void Gap(List<AlignGuide> lines, bool horizontal, double from, double to, double cross)
    {
        if (to < from)
            return;
        const double tick = 7;
        if (horizontal)
        {
            lines.Add(new AlignGuide(from, cross, to, cross));
            lines.Add(new AlignGuide(from, cross - tick, from, cross + tick));
            lines.Add(new AlignGuide(to, cross - tick, to, cross + tick));
        }
        else
        {
            lines.Add(new AlignGuide(cross, from, cross, to));
            lines.Add(new AlignGuide(cross - tick, from, cross + tick, from));
            lines.Add(new AlignGuide(cross - tick, to, cross + tick, to));
        }
    }

    private static void Add(List<AlignGuide> lines, bool vertical, double at, double from, double to)
    {
        if (to < from)
            (from, to) = (to, from);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var same = vertical
                ? Math.Abs(line.X1 - at) < 0.5 && Math.Abs(line.X2 - at) < 0.5
                : Math.Abs(line.Y1 - at) < 0.5 && Math.Abs(line.Y2 - at) < 0.5;
            if (!same)
                continue;
            if (vertical)
                lines[i] = new AlignGuide(at, Math.Min(line.Y1, from), at, Math.Max(line.Y2, to));
            else
                lines[i] = new AlignGuide(Math.Min(line.X1, from), at, Math.Max(line.X2, to), at);
            return;
        }
        lines.Add(vertical
            ? new AlignGuide(at, from, at, to)
            : new AlignGuide(from, at, to, at));
    }
}
