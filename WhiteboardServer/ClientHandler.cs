using System.Net.Sockets;
using System.Text;
using Whiteboard.Shared;
using WhiteboardServer.Models;

namespace WhiteboardServer;

// phục vụ một client 
public class ClientHandler
{
    private readonly TcpClient _client;
    private readonly RoomManager _rooms;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Room? _room;
    private User? _user;

    public ClientHandler(TcpClient client, RoomManager rooms)
    {
        _client = client;
        _rooms = rooms;
    }

    public async Task RunAsync()
    {
        var ep = _client.Client.RemoteEndPoint?.ToString();
        Console.WriteLine($"[Server] Client kết nối: {ep}");
        try
        {
            var stream = _client.GetStream();
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

            string? line;
            while ((line = await _reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                Message? msg;
                try { msg = Message.FromJson(line); }
                catch { continue; } // bỏ qua dòng JSON hỏng

                if (msg != null) await HandleAsync(msg);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Lỗi client {ep}: {ex.Message}");
        }
        finally
        {
            await OnDisconnectAsync();
            _client.Close();
            Console.WriteLine($"[Server] Client ngắt: {ep}");
        }
    }

    // gửi

    public async Task SendAsync(Message msg)
    {
        if (_writer == null) return;
        var json = msg.ToJson();
        await _writeLock.WaitAsync();
        try { await _writer.WriteLineAsync(json); }
        catch { /* socket đã đóng */ }
        finally { _writeLock.Release(); }
    }

    private async Task BroadcastAsync(Message msg, bool includeSelf = true)
    {
        if (_room == null) return;
        foreach (var m in _rooms.SnapshotMembers(_room))
        {
            if (!includeSelf && m == _user) continue;
            await m.Handler.SendAsync(msg);
        }
    }

    // xử lý các loại message
    private async Task HandleAsync(Message msg)
    {
        switch (msg.Type)
        {
            case "join":           await HandleJoinAsync(msg); break;
            case "chat":           await HandleChatAsync(msg); break;
            case "draw":           await HandleDrawAsync(msg); break;
            case "clear":          await HandleClearAsync(); break;
            case "set_permission": await HandlePermissionAsync(msg); break;
            case "cursor":         await HandleCursorAsync(msg); break;
        }
    }

    private async Task HandleJoinAsync(Message msg)
    {
        if (_user != null) return; // đã join rồi

        var username = string.IsNullOrWhiteSpace(msg.Username) ? "guest" : msg.Username!.Trim();
        var roomName = string.IsNullOrWhiteSpace(msg.Room) ? "default" : msg.Room!.Trim();

        var (room, user) = _rooms.JoinOrCreate(roomName, username, this);
        _room = room;
        _user = user;
        username = user.Name; // dùng tên server cấp (nếu trùng tên)

        // xác nhận cho vào
        await SendAsync(new Message { Type = "joined", Room = roomName, Username = username, IsHost = user.IsHost });
        // gửi lại toàn bộ lịch sử để khôi phục bảng
        await SendAsync(new Message { Type = "history", Events = _rooms.SnapshotHistory(room) });
        // cập nhật danh sách thành viên + thông báo hệ thống cho cả phòng
        await BroadcastMembersAsync();
        await BroadcastAsync(new Message { Type = "chat", From = "system", Text = $"{username} đã tham gia phòng", Ts = Now() });

        Console.WriteLine($"[Server] {username} vào phòng '{roomName}' ({(user.IsHost ? "Host" : "Member")})");
    }

    private async Task HandleChatAsync(Message msg)
    {
        if (_user == null || _room == null) return;
        await BroadcastAsync(new Message { Type = "chat", From = _user.Name, Text = msg.Text ?? "", Ts = Now() });
    }

    private async Task HandleDrawAsync(Message msg)
    {
        if (_user == null || _room == null || msg.Draw == null) return;

        if (!_user.CanDraw)
        {
            await SendAsync(new Message { Type = "error", Error = "Bạn không có quyền vẽ" });
            return;
        }

        msg.Draw.Owner = _user.Name;
        _rooms.AddHistory(_room, msg.Draw);
        await BroadcastAsync(new Message { Type = "draw", Draw = msg.Draw }, includeSelf: false);
    }

    private async Task HandleClearAsync()
    {
        if (_user == null || _room == null) return;
        // chỉ Host mới được xóa bảng
        if (!_user.IsHost)
        {
            await SendAsync(new Message { Type = "error", Error = "Chỉ Host mới được xóa bảng" });
            return;
        }
        _rooms.ClearHistory(_room);
        await BroadcastAsync(new Message { Type = "clear" }); // cả phòng cùng xoá
    }

    private async Task HandlePermissionAsync(Message msg)
    {
        if (_user == null || _room == null || !_user.IsHost) return; // chỉ Host
        if (string.IsNullOrEmpty(msg.Target)) return;

        var target = _rooms.SnapshotMembers(_room).FirstOrDefault(u => u.Name == msg.Target);
        if (target == null) return;

        target.CanDraw = msg.CanDraw ?? false;
        await BroadcastAsync(new Message { Type = "permission_changed", Target = target.Name, CanDraw = target.CanDraw });
        await BroadcastMembersAsync();
    }

    private async Task HandleCursorAsync(Message msg)
    {
        if (_user == null || _room == null) return;
        await BroadcastAsync(new Message { Type = "cursor", From = _user.Name, X = msg.X, Y = msg.Y }, includeSelf: false);
    }

    // tiện ích

    private async Task BroadcastMembersAsync()
    {
        if (_room == null) return;
        var infos = _rooms.SnapshotMembers(_room)
            .Select(u => new MemberInfo { Name = u.Name, IsHost = u.IsHost, CanDraw = u.CanDraw })
            .ToList();
        await BroadcastAsync(new Message { Type = "members", Users = infos });
    }

    private async Task OnDisconnectAsync()
    {
        if (_room == null || _user == null) return;

        var room = _room;
        var leaving = _user;
        _rooms.Leave(room, leaving);
        _room = null;
        _user = null;

        // thông báo cho các thành viên còn lại
        var infos = _rooms.SnapshotMembers(room)
            .Select(u => new MemberInfo { Name = u.Name, IsHost = u.IsHost, CanDraw = u.CanDraw })
            .ToList();
        foreach (var m in _rooms.SnapshotMembers(room))
        {
            await m.Handler.SendAsync(new Message { Type = "members", Users = infos });
            await m.Handler.SendAsync(new Message { Type = "chat", From = "system", Text = $"{leaving.Name} đã rời phòng", Ts = Now() });
        }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
