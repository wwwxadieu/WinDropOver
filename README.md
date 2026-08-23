# WinClipboard (WinDropOver)

Ứng dụng quản lý clipboard & shelf kéo-thả cho Windows, theo mô hình Dropover (macOS). Xem
[`ARCHITECTURE.md`](ARCHITECTURE.md) cho chi tiết kiến trúc và [`ROADMAP.md`](ROADMAP.md) cho
tình trạng từng giai đoạn so với kế hoạch gốc.

## Trạng thái hiện tại

Toàn bộ 7 giai đoạn trong kế hoạch đã có code tương ứng (xem ROADMAP.md để biết chi tiết từng
mục).

**Toàn bộ solution — kể cả `WinClipboard.App` (WPF) — build sạch và 68/68 unit test pass trên
Windows**, được xác nhận tự động bởi GitHub Actions ([`.github/workflows/ci.yml`](.github/workflows/ci.yml),
chạy trên `windows-latest`) ở mọi push/PR vào `main`.

Lưu ý về cách dự án được hiện thực: code ban đầu viết trong một sandbox Linux không có WPF SDK,
nên phần giao diện chỉ được compile lần đầu khi CI chạy. Hai lỗi thật đã phát hiện và sửa qua CI:
xung đột namespace WinForms/WPF, và SQLite connection pool giữ file khiến test teardown fail trên
Windows. Từ đó tới nay CI xanh liên tục.

**Đã có người chạy thật.** Việc đó phát hiện một loạt lỗi mà compiler và unit test không thể
thấy: app làm treo con trỏ toàn máy rồi tự thoát khi cầm file, không cử chỉ nào mở được shelf,
và cú drop nhận được nhưng đọc rỗng. Tất cả đã sửa, và cơ chế shelf được xác nhận hoạt động từ
`v0.1.6-alpha`.

Phần giao diện mới nhất — thẻ shelf, lưới thumbnail, ẩn-khi-click-ra-ngoài — thì **chưa**: nó
mới chỉ qua compile và ảnh chụp CI. Xem mục giới hạn bên dưới.

## Cài đặt

Tải ở [trang Releases](https://github.com/wwwxadieu/WinDropOver/releases). Mỗi bản phát hành có
hai lựa chọn:

| Tệp | Dùng khi |
|---|---|
| `WinClipboard-win-x64.msi` | **Cách thường dùng.** Cài theo từng người dùng vào `%LocalAppData%\Programs` — không cần quyền admin, có lối tắt Start Menu, gỡ được qua Apps & features. Cài bản mới tự thay bản cũ. |
| `WinClipboard-win-x64.zip` | Bản xách tay: giải nén rồi chạy `WinClipboard.exe`, không ghi gì vào Start Menu hay danh sách chương trình. |

Cả hai đều self-contained (đã nhúng .NET runtime), máy đích không cần cài thêm gì.

**Chưa ký số**, nên lần chạy đầu Windows SmartScreen sẽ cảnh báo — bấm *More info → Run anyway*.
App có cài low-level keyboard hook nên cũng có thể bị phần mềm diệt virus gắn cờ nhầm.

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
| `src/WinClipboard.App` | WPF: tray icon, shelf card, lịch sử clipboard, Cài đặt | ❌ (cần Windows — CI build project này) |
| `tests/WinClipboard.Core.Tests`, `tests/WinClipboard.Data.Tests` | 42 xUnit test | ✅ |

Cột cuối chỉ nói về việc *phát triển cục bộ trên Linux*; trên Windows (và trên CI) toàn bộ
solution build được.

## Giới hạn quan trọng cần biết trước khi tiếp tục phát triển

- **Chưa ai chạy thử app thật.** Build xanh chỉ chứng minh code hợp lệ về mặt biên dịch, chưa
  chứng minh hành vi runtime đúng. Toàn bộ phần phụ thuộc hành vi Windows thật — low-level hook có
  bắt đúng thao tác kéo tệp không, bubble có hiện đúng vị trí trên đa màn hình/đa DPI không,
  drag-drop giữa các ứng dụng có hoạt động không, dán lại có đúng cửa sổ đích không — **đều chưa
  được kiểm chứng**. Đây giờ là rủi ro lớn nhất còn lại.
- **Share quick action** (`Services/ShareUI.cs`) dùng `IDataTransferManagerInterop` + WinRT
  `DataTransferManager` theo đúng pattern Microsoft công bố cho desktop app. Nay đã compile được,
  nhưng WinRT interop rất dễ sai chi tiết nhỏ mà chỉ lộ ra lúc chạy — test kỹ trước khi dựa vào nó.
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
