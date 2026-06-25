namespace Whiteboard.Shared;

/// <summary>
/// thao tác vẽ. Mọi hình đều mô tả bằng 2 điểm (x1,y1)-(x2,y2):
///  - line/pen : hai đầu mút
///  - rectangle: hai góc đối diện
///  - circle   : tâm (x1,y1) và một điểm trên đường tròn (x2,y2)
/// </summary>
public class DrawEvent
{
    public string Id { get; set; } = "";
    public string Shape { get; set; } = "line"; // line | rectangle | circle | pen
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Color { get; set; } = "#000000";
    public double Thickness { get; set; } = 2;
    public string? Owner { get; set; }
}
