using Whiteboard.Shared;
using WhiteboardServer.Models;

namespace WhiteboardServer;

// quản lý tất cả các phòng vẽ
public class RoomManager
{
    private readonly Dictionary<string, Room> _rooms = new();
    private readonly object _lock = new();

    /// tham gia phòng, người đầu tiên tham gia/tạo phòng sẽ là Host
    public (Room room, User user) JoinOrCreate(string roomName, string username, ClientHandler handler)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomName, out var room))
            {
                room = new Room(roomName);
                _rooms[roomName] = room;
            }

            username = MakeUniqueName(room, username); // tránh trùng tên trong cùng phòng
            bool isHost = room.Members.Count == 0;
            var user = new User(username, handler) { IsHost = isHost, CanDraw = true };
            room.Members.Add(user);
            return (room, user);
        }
    }

    // nếu tên đã có người dùng trong phòng thì thêm hậu tố: huy -> huy(2) -> huy(3) ...
    private static string MakeUniqueName(Room room, string name)
    {
        if (!room.Members.Exists(m => m.Name == name)) return name;
        for (int i = 2; ; i++)
        {
            var candidate = $"{name}({i})";
            if (!room.Members.Exists(m => m.Name == candidate)) return candidate;
        }
    }

    // rời phòng, nếu là Host thì chuyển quyền cho người tiếp theo, nếu không còn ai thì xoá phòng
    public void Leave(Room room, User user)
    {
        lock (_lock)
        {
            room.Members.Remove(user);
            if (user.IsHost && room.Members.Count > 0)
                room.Members[0].IsHost = true;
            if (room.Members.Count == 0)
                _rooms.Remove(room.Name);
        }
    }

    public void AddHistory(Room room, DrawEvent e)
    {
        lock (_lock) { room.History.Add(e); }
    }

    public void ClearHistory(Room room)
    {
        lock (_lock) { room.History.Clear(); }
    }

    // bản sao của danh sách thành viên và lịch sử để gửi cho người mới tham gia phòng
    public List<User> SnapshotMembers(Room room)
    {
        lock (_lock) { return new List<User>(room.Members); }
    }

    public List<DrawEvent> SnapshotHistory(Room room)
    {
        lock (_lock) { return new List<DrawEvent>(room.History); }
    }
}
