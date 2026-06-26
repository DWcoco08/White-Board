namespace WhiteboardServer.Models;

// mỗi người dùng 1p
public class User
{
    public string Name { get; }
    public bool IsHost { get; set; }
    public bool CanDraw { get; set; } = true;
    public ClientHandler Handler { get; }

    public User(string name, ClientHandler handler)
    {
        Name = name;
        Handler = handler;
    }
}
