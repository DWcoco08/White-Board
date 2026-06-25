namespace Whiteboard.Shared;

// class chứa thông tin về một thành viên trong phòng vẽ
public class MemberInfo
{
    public string Name { get; set; } = "";
    public bool IsHost { get; set; }
    public bool CanDraw { get; set; } = true;
}
