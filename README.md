# Whiteboard — Bảng vẽ cộng tác thời gian thực qua mạng LAN (TCP Socket)

[English](README.md) | Tiếng Việt

Ứng dụng bảng vẽ cộng tác thời gian thực trong mạng nội bộ. Nhiều người cùng vào một phòng để vẽ
chung, trò chuyện và thấy nhau theo thời gian thực — tất cả được đồng bộ qua giao thức
client–server tự xây dựng trên **TCP Socket**. Viết bằng **C# / .NET 8** và **Avalonia UI**, chạy
được trên **Linux và Windows**.

> Bài tập lớn môn *Lập trình mạng*. Đề tài: *"Hệ thống bảng vẽ cộng tác thời gian thực trong mạng
> LAN sử dụng TCP Socket"*.

---

## Tính năng

- **Đồng bộ vẽ thời gian thực** — đường thẳng, hình chữ nhật, hình tròn và bút tự do. Mỗi nét vẽ
  hiện ngay trên bảng của tất cả thành viên.
- **Xem trước hình khi kéo** — trong lúc kéo chuột, bạn thấy hình trước khi thả tay.
- **Hiển thị con trỏ trực tiếp** — thấy con trỏ của các thành viên khác di chuyển theo thời gian
  thực, kèm tên của từng người.
- **Chọn màu & độ dày nét** — đổi màu và độ dày cho từng hình.
- **Nhiều phòng** — các phòng độc lập, người ở phòng khác không ảnh hưởng lẫn nhau.
- **Chat trong phòng** — gửi/nhận tin nhắn thời gian thực, kèm thông báo vào/ra phòng.
- **Phân quyền Host / Member** — người tạo phòng là **Host**, có thể khóa hoặc mở quyền vẽ của bất
  kỳ thành viên nào. Quyền được **kiểm soát phía server**, không chỉ ẩn nút trên giao diện.
- **Phát lại lịch sử** — người vào sau tự động nhận toàn bộ lịch sử vẽ và dựng lại đúng bảng hiện
  tại.
- **Đồng bộ theo sự kiện** — chỉ truyền *thao tác vẽ* qua mạng (không truyền ảnh màn hình), giúp
  tiết kiệm băng thông.

---

## Công nghệ

| Hạng mục  | Lựa chọn                                |
|-----------|-----------------------------------------|
| Ngôn ngữ  | C#                                      |
| Nền tảng  | .NET 8                                  |
| Giao diện | Avalonia UI 11 (desktop đa nền tảng)    |
| Mạng      | TCP Socket (`TcpListener` / `TcpClient`)|
| Giao thức | NDJSON (mỗi message là một dòng JSON)   |

---

## Kiến trúc

```
                       ┌────────────────────┐
                       │       Server       │
                       │  TcpListener:5000  │
                       │     RoomManager    │
                       └─────────┬──────────┘
              ┌──────────────────┼──────────────────┐
         ┌────┴────┐        ┌────┴────┐        ┌────┴────┐
         │ Client  │        │ Client  │        │ Client  │
         │   huy   │        │  minh   │        │   lan   │
         └─────────┘        └─────────┘        └─────────┘
```

- **Server** nhận kết nối TCP, chạy một luồng xử lý bất đồng bộ cho mỗi client, gom client vào các **phòng**, và phát sự kiện cho mọi người trong cùng phòng.
- Mỗi **client** kết nối, vào phòng và hiển thị bảng vẽ chung. Khi một người vẽ, client gửi một *sự kiện vẽ*; server chuyển tiếp cho các thành viên còn lại.
- Message được đóng khung theo **NDJSON** — mỗi message là một đối tượng JSON trên một dòng kết thúc bằng `\n` — giúp tách chính xác luồng byte liên tục của TCP thành từng message riêng biệt.

---

## Cấu trúc dự án

Solution gồm ba project:

```
Whiteboard.sln
│
├── Whiteboard.Shared/          # Giao thức dùng chung cho client và server (tránh lệch nhau)
│   ├── Message.cs              #   một kiểu "phong bì" cho mọi message
│   ├── DrawEvent.cs            #   một thao tác vẽ (hình, tọa độ, màu, ...)
│   └── MemberInfo.cs           #   trạng thái thành viên (tên, host, được vẽ hay không)
│
├── WhiteboardServer/           # App console — server TCP
│   ├── Program.cs              #   điểm vào (đọc cổng từ tham số, mặc định 5000)
│   ├── TcpServer.cs            #   vòng lặp nhận kết nối, mỗi client một handler
│   ├── ClientHandler.cs        #   đọc/xử lý message, phát cho cả phòng
│   ├── RoomManager.cs          #   quản lý phòng, thành viên, lịch sử (an toàn đa luồng)
│   └── Models/
│       ├── User.cs
│       └── Room.cs
│
└── WhiteboardClient/           # App desktop Avalonia — giao diện
    ├── Program.cs              #   khởi động Avalonia
    ├── App.axaml(.cs)          #   ứng dụng + theme
    ├── MainWindow.axaml(.cs)   #   bố cục giao diện + logic xử lý
    ├── SocketClient.cs         #   kết nối TCP, vòng gửi/nhận
    └── WhiteboardCanvas.cs     #   control tự vẽ: hình, xem trước, con trỏ
```

---

## Giao thức

Mọi message là JSON, mỗi dòng một message (NDJSON), mã hóa UTF-8.

**Client → Server**

```jsonc
{ "type": "join", "username": "huy", "room": "NetworkClass" }
{ "type": "chat", "text": "chào cả nhà" }
{ "type": "draw", "draw": { "id": "huy-0", "shape": "line",
                            "x1": 100, "y1": 100, "x2": 300, "y2": 200,
                            "color": "#000000", "thickness": 2 } }
{ "type": "clear" }
{ "type": "set_permission", "target": "minh", "canDraw": false }
{ "type": "cursor", "x": 120, "y": 80 }
```

**Server → Client**

```jsonc
{ "type": "joined", "room": "NetworkClass", "isHost": true }
{ "type": "history", "events": [ /* toàn bộ DrawEvent đã vẽ */ ] }
{ "type": "members", "users": [ { "name": "huy", "isHost": true, "canDraw": true } ] }
{ "type": "chat", "from": "huy", "text": "chào cả nhà", "ts": 1700000000 }
{ "type": "draw", "draw": { /* DrawEvent */ } }
{ "type": "clear" }
{ "type": "permission_changed", "target": "minh", "canDraw": false }
{ "type": "cursor", "from": "minh", "x": 120, "y": 80 }
{ "type": "error", "error": "Bạn không có quyền vẽ" }
```

`shape` là một trong `line`, `rectangle`, `circle` hoặc `pen`. Hình chữ nhật và hình tròn xác định
bằng hai điểm (góc–đối góc / tâm–mép); bút tự do được gửi thành nhiều đoạn ngắn.

---

## Bắt đầu

### Yêu cầu

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

### Build

```bash
git clone https://github.com/DWcoco08/White-Board.git
cd White-Board
dotnet build
```

### Chạy

Mở hai cửa sổ terminal (server phải chạy liên tục):

```bash
# 1) Chạy server (mặc định cổng 5000)
dotnet run --project WhiteboardServer
#    đổi cổng:  dotnet run --project WhiteboardServer -- 5001

# 2) Chạy client (mỗi người chạy một lần)
dotnet run --project WhiteboardClient
```

Trong cửa sổ client, điền **Server**, **Cổng**, **Tên**, **Phòng** rồi bấm **Kết nối**.
Người vào phòng đầu tiên sẽ là **Host**.

---

## Chạy trên mạng LAN

Để cộng tác giữa các máy khác nhau trong cùng mạng:

1. Trên **máy chạy server**, lấy IP LAN:
   ```bash
   hostname -I        # Linux   → ví dụ 192.168.1.20
   ipconfig           # Windows → tìm dòng IPv4 Address
   ```
2. Mở cổng firewall (ví dụ trên Linux):
   ```bash
   sudo firewall-cmd --add-port=5000/tcp
   ```
3. Trên mỗi **máy client**, điền **Server** là IP LAN đó, giữ **Cổng** `5000`, và dùng **cùng tên
   Phòng**.

> Tất cả các máy phải ở cùng mạng LAN/Wi-Fi. Chỉ dùng `127.0.0.1` khi client chạy chung máy với
> server.

---

## Thử nhanh

Một kịch bản demo:

1. Một người vẽ một hình → mọi người thấy hiện ra ngay.
2. Di chuột trên bảng → người khác thấy con trỏ kèm tên bạn chạy theo.
3. Một người vào sau → bảng của họ được dựng lại từ lịch sử tự động.
4. Gửi một tin nhắn chat → mọi người đều nhận được.
5. Host chọn một thành viên và khóa quyền vẽ → người đó không vẽ được nữa (server chặn).
6. Bấm **Xóa bảng** → bảng của cả phòng cùng trắng.

---

## Điểm thiết kế nổi bật

- **Project giao thức dùng chung** — cả client và server đều tham chiếu `Whiteboard.Shared`, nên
  định dạng message không bao giờ lệch nhau giữa hai bên.
- **Đóng khung NDJSON** — giải quyết bài toán "ranh giới message" kinh điển của TCP mà không cần
  bộ phân tích theo độ dài.
- **Quyền lực phía server** — quyền vẽ và lịch sử được kiểm soát ở server, nên không thể bị qua mặt
  bằng một client đã chỉnh sửa.
- **Đồng bộ theo sự kiện** — truyền thao tác thay vì truyền điểm ảnh giúp giao thức gọn nhẹ và bảng
  vẽ luôn sắc nét ở mọi kích thước.
- **An toàn đa luồng** — trạng thái phòng được bảo vệ bằng khóa, và mỗi socket tuần tự hóa lượt
  ghi, nên nhiều client đồng thời không làm hỏng dữ liệu chung.

---

## Tác giả

**Đặng Nguyễn Đức Huy** — Bài tập lớn môn Lập trình mạng.
