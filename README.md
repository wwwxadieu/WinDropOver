# WinClipboard (WinDropOver)

Ứng dụng quản lý clipboard & shelf kéo-thả cho Windows, theo mô hình Dropover (macOS). Xem
[`ARCHITECTURE.md`](ARCHITECTURE.md) cho chi tiết kiến trúc và [`ROADMAP.md`](ROADMAP.md) cho
tình trạng từng giai đoạn so với kế hoạch gốc.

## Trạng thái hiện tại

Toàn bộ 7 giai đoạn trong kế hoạch đã có code tương ứng (xem ROADMAP.md để biết chi tiết từng
mục). Điểm quan trọng nhất cần biết trước khi dùng: **dự án này được hiện thực trong một sandbox
Linux, không có Windows hay WPF SDK để build/chạy thử.**

- `WinClipboard.Core`, `WinClipboard.Data`, `WinClipboard.Interop` — build sạch (0 lỗi) và có
  **42 unit test pass** trên Linux (`dotnet test`), vì các project này chỉ target `net8.0`/
  `net8.0-windows` (không cần WPF SDK để compile, kể cả các lệnh gọi P/Invoke Win32 trong
  `Interop`).
- `WinClipboard.App` (giao diện WPF) — đã viết đầy đủ (4 cửa sổ, tray icon, toàn bộ service nối
  dây trong `App.xaml.cs`) nhưng **chưa từng được build hay chạy thử**, vì
  `Microsoft.NET.Sdk.WindowsDesktop` (SDK cần để compile XAML/WPF) chỉ tồn tại trên Windows.
  Việc đầu tiên cần làm trên máy Windows thật là `dotnet build` project này và sửa các lỗi biên
  dịch — chắc chắn sẽ có vài lỗi nhỏ (tên thuộc tính XAML, using còn thiếu...) vì chưa qua
  compiler lần nào.

## Build & chạy (trên Windows)

Yêu cầu: .NET 8 SDK, Windows 10 build 19041 trở lên (do dùng WinRT projection cho tính năng
Share — xem ghi chú giới hạn bên dưới).

```powershell
dotnet restore
dotnet build src\WinClipboard.App\WinClipboard.App.csproj
dotnet run --project src\WinClipboard.App
```

Chạy bộ test (không cần Windows, chạy được ngay trên máy hiện thực dự án này):

```bash
dotnet test tests/WinClipboard.Core.Tests
dotnet test tests/WinClipboard.Data.Tests
```

## Cấu trúc solution

| Project | Vai trò | Build được trên Linux? |
|---|---|---|
| `src/WinClipboard.Core` | Model + business logic thuần (Quick Actions engine, edge detection, drag threshold, settings model) — không đụng Windows API | ✅ |
| `src/WinClipboard.Data` | SQLite (`Microsoft.Data.Sqlite`) cho lịch sử clipboard + shelf, JSON settings store | ✅ |
| `src/WinClipboard.Interop` | Toàn bộ P/Invoke Win32: low-level mouse/keyboard hook, hotkey, clipboard listener, layered window, DPI, giả lập Ctrl+V | ✅ (chỉ compile — hook/API thật cần chạy trên Windows) |
| `src/WinClipboard.App` | WPF: tray icon, History overlay, Bubble, Shelf Panel, Settings | ❌ (cần Windows) |
| `tests/WinClipboard.Core.Tests`, `tests/WinClipboard.Data.Tests` | 42 xUnit test | ✅ |

## Giới hạn quan trọng cần biết trước khi tiếp tục phát triển

- **`WinClipboard.App` chưa build-verify.** Đây là rủi ro lớn nhất — hãy dành buổi làm việc đầu
  tiên trên Windows để `dotnet build` và sửa lỗi biên dịch trước khi thêm tính năng mới.
- **Share quick action** (`Services/ShareUI.cs`) dùng `IDataTransferManagerInterop` + WinRT
  `DataTransferManager` theo đúng pattern Microsoft công bố cho desktop app, nhưng đây là phần
  rủi ro nhất trong toàn bộ code vì WinRT interop rất dễ sai chi tiết nhỏ mà chỉ phát hiện được
  lúc chạy — test kỹ trước khi dựa vào nó.
- **Tray icon** hiện vẽ runtime (hình tròn màu accent) thay vì dùng file `.ico` thật — thay bằng
  bộ nhận diện thương hiệu thật khi đã quyết định (xem mục 7 kế hoạch gốc: "Tên sản phẩm chính
  thức và bộ nhận diện thương hiệu").
- **Chưa có:** Preview/Thumbnail Engine riêng biệt có cache (hiện chỉ lưu PNG thumbnail cho ảnh
  copy, chưa có cache cho file/tài liệu khác), kéo nhiều item cùng lúc ra khỏi shelf (hiện chỉ hỗ
  trợ kéo từng item), acrylic/mica thật qua DWM (hiện dùng nền bán trong suốt đơn giản — xem ghi
  chú trong `Styles/Theme.xaml`).
- **Đổi shelf từ session-only sang persisted sau khi đã tạo** chưa hỗ trợ — quyết định lưu-qua-
  khởi-động được chốt ngay lúc tạo shelf (xem `ShelfSessionManager` để biết lý do và cách mở
  rộng).

## Tài liệu gốc

Xem file kế hoạch `.docx` người dùng cung cấp (WinClipboard – Kế hoạch triển khai dự án, phiên
bản 2.0) để biết đầy đủ ý tưởng sản phẩm, lộ trình 7 giai đoạn, rủi ro kỹ thuật và các quyết định
còn để ngỏ.
