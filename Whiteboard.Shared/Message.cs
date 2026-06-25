using System.Text.Json;
using System.Text.Json.Serialization;

namespace Whiteboard.Shared;

public class Message
{
    public string Type { get; set; } = "";

    // join / định danh
    public string? Username { get; set; }
    public string? Room { get; set; }

    // chat
    public string? Text { get; set; }
    public string? From { get; set; }
    public long? Ts { get; set; }

    // vẽ
    public DrawEvent? Draw { get; set; }

    // danh sách thành viên / lịch sử
    public List<MemberInfo>? Users { get; set; }
    public List<DrawEvent>? Events { get; set; }

    // phân quyền
    public string? Target { get; set; }
    public bool? CanDraw { get; set; }
    public bool? IsHost { get; set; }

    // con trỏ
    public double? X { get; set; }
    public double? Y { get; set; }

    // báo lỗi
    public string? Error { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static Message? FromJson(string line) =>
        JsonSerializer.Deserialize<Message>(line, Options);
}
