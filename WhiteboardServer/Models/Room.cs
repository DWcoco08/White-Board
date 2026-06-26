using Whiteboard.Shared;

namespace WhiteboardServer.Models;

public class Room
{
    public string Name { get; }
    public List<User> Members { get; } = new();
    public List<DrawEvent> History { get; } = new();

    public Room(string name) => Name = name;
}
