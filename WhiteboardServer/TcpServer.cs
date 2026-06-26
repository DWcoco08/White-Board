using System.Net;
using System.Net.Sockets;

namespace WhiteboardServer;

public class TcpServer
{
    private readonly int _port;
    private readonly RoomManager _rooms = new();

    public TcpServer(int port) => _port = port;

    public async Task StartAsync()
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[Server] Đang lắng nghe trên 0.0.0.0:{_port}");
        Console.WriteLine("[Server] Nhấn Ctrl+C để dừng.");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            var handler = new ClientHandler(client, _rooms);
            _ = handler.RunAsync(); // mỗi client một luồng phục vụ
        }
    }
}
