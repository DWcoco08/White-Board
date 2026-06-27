using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Whiteboard.Shared;

namespace WhiteboardClient;

public partial class MainWindow : Window
{
    private readonly SocketClient _socket = new();
    private readonly ObservableCollection<string> _chat = new();
    private readonly ObservableCollection<string> _members = new();
    private List<MemberInfo> _memberInfos = new();

    private string _username = "";
    private bool _isHost;
    private bool _canDraw = true;
    private bool _connected;
    private bool _leaving; 
    private int _drawCounter;

    // trạng thái khi đang kéo chuột
    private bool _drawing;
    private Point _start;
    private Point _last;

    public MainWindow()
    {
        InitializeComponent();

        ChatList.ItemsSource = _chat;
        MemberList.ItemsSource = _members;

        _socket.OnMessage += msg => Dispatcher.UIThread.Post(() => HandleMessage(msg));
        _socket.OnDisconnected += reason => Dispatcher.UIThread.Post(() => OnDisconnected(reason));

        BoardHost.PointerPressed += OnBoardPressed;
        BoardHost.PointerMoved += OnBoardMoved;
        BoardHost.PointerReleased += OnBoardReleased;
    }

    // kết nối 
    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (_connected) return;

        var host = string.IsNullOrWhiteSpace(HostBox.Text) ? "127.0.0.1" : HostBox.Text!.Trim();
        if (!int.TryParse(PortBox.Text, out var port)) port = 5000;
        _username = string.IsNullOrWhiteSpace(UserBox.Text) ? "guest" : UserBox.Text!.Trim();
        var room = string.IsNullOrWhiteSpace(RoomBox.Text) ? "default" : RoomBox.Text!.Trim();

        try
        {
            StatusText.Text = "Đang kết nối...";
            await _socket.ConnectAsync(host, port);
            await _socket.SendAsync(new Message { Type = "join", Username = _username, Room = room });
            _connected = true;
            ConnectBtn.IsEnabled = false;
            LeaveBtn.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Lỗi: " + ex.Message;
        }
    }

    private void OnDisconnected(string reason)
    {
        if (_leaving) { _leaving = false; return; }
        _connected = false;
        ConnectBtn.IsEnabled = true;
        LeaveBtn.IsEnabled = false;
        StatusText.Text = "Mất kết nối: " + reason;
    }

    // rời phòng = đóng kết nối
    private void OnLeaveClick(object? sender, RoutedEventArgs e)
    {
        if (!_connected) return;
        _leaving = true;
        _socket.Close();
        ResetRoomUi();
    }

    private void ResetRoomUi()
    {
        _connected = false;
        _isHost = false;
        _canDraw = true;
        Board.Clear();
        _members.Clear();
        _chat.Clear();
        ConnectBtn.IsEnabled = true;
        LeaveBtn.IsEnabled = false;
        PermBtn.IsVisible = false;
        ClearBtn.IsVisible = false;
        StatusText.Text = "Đã rời phòng";
    }

    // nhận message
    private void HandleMessage(Message m)
    {
        switch (m.Type)
        {
            case "joined":
                _username = m.Username ?? _username; // dùng tên server cấp
                _isHost = m.IsHost ?? false;
                PermBtn.IsVisible = _isHost;
                ClearBtn.IsVisible = _isHost; // chỉ Host mới thấy nút Xóa bảng
                StatusText.Text = $"Đã vào phòng {m.Room} ({(_isHost ? "Host" : "Member")})";
                break;

            case "history":
                if (m.Events != null) Board.SetEvents(m.Events);
                break;

            case "members":
                if (m.Users != null) UpdateMembers(m.Users);
                break;

            case "chat":
                AddChat(m.From ?? "?", m.Text ?? "");
                break;

            case "draw":
                if (m.Draw != null) Board.AddEvent(m.Draw);
                break;

            case "clear":
                Board.Clear();
                break;

            case "permission_changed":
                if (m.Target == _username)
                {
                    _canDraw = m.CanDraw ?? true;
                    StatusText.Text = _canDraw ? "Bạn được phép vẽ" : "Bạn đã bị khóa quyền vẽ";
                }
                break;

            case "cursor":
                if (m.X.HasValue && m.Y.HasValue && m.From != null)
                    Board.SetCursor(m.From, new Point(m.X.Value, m.Y.Value));
                break;

            case "error":
                StatusText.Text = "Lỗi: " + (m.Error ?? "");
                break;
        }
    }

    private void UpdateMembers(List<MemberInfo> users)
    {
        _memberInfos = users;
        _members.Clear();
        foreach (var u in users)
            _members.Add($"{(u.IsHost ? "[Host] " : "")}{u.Name}{(u.CanDraw ? "" : " (khóa)")}");

        var me = users.Find(u => u.Name == _username);
        if (me != null)
        {
            _canDraw = me.CanDraw;
            _isHost = me.IsHost;
            PermBtn.IsVisible = _isHost;
            ClearBtn.IsVisible = _isHost; // đồng bộ lại khi quyền Host thay đổi
        }
    }

    private void AddChat(string from, string text)
    {
        _chat.Add($"{from}: {text}");
        // cuộn xuống cuối sau khi bố cục cập nhật
        Dispatcher.UIThread.Post(
            () => ChatScroll.Offset = ChatScroll.Offset.WithY(ChatScroll.Extent.Height),
            DispatcherPriority.Background);
    }

    // chat
    private async void OnSendChatClick(object? sender, RoutedEventArgs e) => await SendChatAsync();

    private async void OnChatKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SendChatAsync();
    }

    private async Task SendChatAsync()
    {
        var text = ChatInput.Text?.Trim();
        if (string.IsNullOrEmpty(text) || !_connected) return;
        await _socket.SendAsync(new Message { Type = "chat", Text = text });
        ChatInput.Text = "";
    }

    // xóa bảng
    private async void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (!_connected) return;
        await _socket.SendAsync(new Message { Type = "clear" });
    }

    // phân quyền 
    private async void OnTogglePermClick(object? sender, RoutedEventArgs e)
    {
        if (!_isHost) return;
        var idx = MemberList.SelectedIndex;
        if (idx < 0 || idx >= _memberInfos.Count)
        {
            StatusText.Text = "Hãy chọn 1 thành viên trong danh sách";
            return;
        }
        var target = _memberInfos[idx];
        if (target.Name == _username) return;
        await _socket.SendAsync(new Message
        {
            Type = "set_permission",
            Target = target.Name,
            CanDraw = !target.CanDraw
        });
    }

    // vẽ 
    private string CurrentShape() => ShapeBox.SelectedIndex switch
    {
        1 => "rectangle",
        2 => "circle",
        3 => "pen",
        _ => "line"
    };

    private string CurrentColor() =>
        (ColorBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "#000000";

    private double CurrentThickness() =>
        double.TryParse((ThickBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var t) ? t : 2;

    private void OnBoardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_connected || !_canDraw) return;
        _drawing = true;
        _start = e.GetPosition(Board);
        _last = _start;
    }

    private async void OnBoardMoved(object? sender, PointerEventArgs e)
    {
        if (!_connected) return;
        var p = e.GetPosition(Board);

        // gửi vị trí con trỏ cho người khác thấy
        await _socket.SendAsync(new Message { Type = "cursor", X = p.X, Y = p.Y });

        if (!_drawing || !_canDraw) return;

        var shape = CurrentShape();
        if (shape == "pen")
        {
            var seg = MakeEvent("pen", _last, p);
            Board.AddEvent(seg);
            await _socket.SendAsync(new Message { Type = "draw", Draw = seg });
            _last = p;
        }
        else
        {
            // xem trước hình đang kéo (chỉ hiển thị cục bộ)
            Board.SetPreview(MakeEvent(shape, _start, p));
        }
    }

    private async void OnBoardReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_drawing) return;
        _drawing = false;

        var shape = CurrentShape();
        Board.SetPreview(null);

        if (shape == "pen") return;     // vẽ từng đoạn khi di chuột, không cần vẽ lại khi thả chuột
        if (!_canDraw) return;

        var ev = MakeEvent(shape, _start, e.GetPosition(Board));
        Board.AddEvent(ev);
        await _socket.SendAsync(new Message { Type = "draw", Draw = ev });
    }

    private DrawEvent MakeEvent(string shape, Point a, Point b) => new()
    {
        Id = $"{_username}-{_drawCounter++}",
        Shape = shape,
        X1 = a.X, Y1 = a.Y,
        X2 = b.X, Y2 = b.Y,
        Color = CurrentColor(),
        Thickness = CurrentThickness(),
        Owner = _username
    };
}
