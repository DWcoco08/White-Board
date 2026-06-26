using WhiteboardServer;

// port mặc định 5000, có thể đổi
int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5000;

var server = new TcpServer(port);
await server.StartAsync();
