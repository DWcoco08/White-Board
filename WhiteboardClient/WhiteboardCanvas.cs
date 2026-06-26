using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Whiteboard.Shared;

namespace WhiteboardClient;

// vùng vẽ, hiển thị các sự kiện vẽ và con trỏ của người khác
public class WhiteboardCanvas : Control
{
    private readonly List<DrawEvent> _events = new();
    private DrawEvent? _preview;
    private readonly Dictionary<string, Point> _cursors = new();

    public void AddEvent(DrawEvent e)
    {
        _events.Add(e);
        InvalidateVisual();
    }

    public void SetEvents(IEnumerable<DrawEvent> events)
    {
        _events.Clear();
        _events.AddRange(events);
        InvalidateVisual();
    }

    public void Clear()
    {
        _events.Clear();
        InvalidateVisual();
    }

    public void SetPreview(DrawEvent? e)
    {
        _preview = e;
        InvalidateVisual();
    }

    public void SetCursor(string name, Point p)
    {
        _cursors[name] = p;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        ctx.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        foreach (var e in _events) DrawShape(ctx, e);
        if (_preview != null) DrawShape(ctx, _preview);

        foreach (var kv in _cursors)
        {
            var p = kv.Value;
            ctx.DrawEllipse(Brushes.OrangeRed, null, p, 4, 4);
            var label = new FormattedText(
                kv.Key, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Typeface.Default, 11, Brushes.OrangeRed);
            ctx.DrawText(label, new Point(p.X + 6, p.Y + 6));
        }
    }

    private static void DrawShape(DrawingContext ctx, DrawEvent e)
    {
        Color color;
        try { color = Color.Parse(string.IsNullOrEmpty(e.Color) ? "#000000" : e.Color); }
        catch { color = Colors.Black; }

        var pen = new Pen(new SolidColorBrush(color), e.Thickness <= 0 ? 2 : e.Thickness);

        switch (e.Shape)
        {
            case "rectangle":
                ctx.DrawRectangle(null, pen, NormRect(e));
                break;
            case "circle":
                var radius = Distance(e.X1, e.Y1, e.X2, e.Y2);
                ctx.DrawEllipse(null, pen, new Point(e.X1, e.Y1), radius, radius);
                break;
            default: // line, pen
                ctx.DrawLine(pen, new Point(e.X1, e.Y1), new Point(e.X2, e.Y2));
                break;
        }
    }

    private static Rect NormRect(DrawEvent e)
    {
        var x = Math.Min(e.X1, e.X2);
        var y = Math.Min(e.Y1, e.Y2);
        var w = Math.Abs(e.X2 - e.X1);
        var h = Math.Abs(e.Y2 - e.Y1);
        return new Rect(x, y, w, h);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
}
