using System.Net.Sockets;
using System.Text;
using Whiteboard.Shared;

namespace WhiteboardClient;

// kết nối tcp với server, gửi và nhận message
public class SocketClient
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event Action<Message>? OnMessage;
    public event Action<string>? OnDisconnected;

    public async Task ConnectAsync(string host, int port)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port);

        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        _ = Task.Run(ReadLoopAsync);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            string? line;
            while (_reader != null && (line = await _reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                Message? msg = null;
                try { msg = Message.FromJson(line); } catch {}
                if (msg != null) OnMessage?.Invoke(msg);
            }
        }
        catch (Exception ex)
        {
            OnDisconnected?.Invoke(ex.Message);
            return;
        }
        OnDisconnected?.Invoke("Server đã đóng kết nối");
    }

    public async Task SendAsync(Message msg)
    {
        if (_writer == null) return;
        var json = msg.ToJson();
        await _writeLock.WaitAsync();
        try { await _writer.WriteLineAsync(json); }
        catch { /* mất kết nối */ }
        finally { _writeLock.Release(); }
    }

    public void Close()
    {
        try { _client?.Close(); } catch {}
    }
}
