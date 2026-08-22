# Tình trạng triển khai so với kế hoạch gốc

Đối chiếu với mục 5 (Lộ trình triển khai) của kế hoạch. Mỗi mục dưới đây map trực tiếp tới một
hàng trong bảng lộ trình gốc, cùng tiêu chí hoàn thành gốc và trạng thái thật của code.

## Giai đoạn 0 — Chuẩn bị

**Tiêu chí gốc:** Project chạy được, cửa sổ overlay rỗng hiện/ẩn bằng phím tắt.

- [x] Khung solution `.NET`/WPF theo đúng cấu trúc thư mục module ở mục 3.2 (`src/Core`,
      `Data`, `Interop`, `App`).
- [x] Cấu hình build (`.csproj` cho từng project, `WinClipboard.sln`).
- [ ] **Chưa xác nhận được "project chạy được"** — `WinClipboard.App` chưa từng `dotnet build`
      thành công vì môi trường hiện thực không có Windows/WPF SDK. Đây là việc đầu tiên cần làm
      trên máy Windows thật.

## Giai đoạn 1 — MVP lịch sử clipboard

**Tiêu chí gốc:** Dùng thay thế được clipboard history mặc định của Windows trong sinh hoạt
hằng ngày.

- [x] Clipboard Monitor (`ClipboardMonitorService` + `Win32MessageWindow.ClipboardChanged`).
- [x] Hotkey Manager (`Win32MessageWindow.RegisterHotkey`, mặc định Ctrl+Shift+V).
- [x] Overlay chế độ lịch sử (`HistoryOverlayWindow`) — văn bản/ảnh/tệp cơ bản.
- [x] Lưu SQLite (`ClipboardRepository`, có index theo `HashDedup` và `CreatedAt`).
- [x] Dán lại bằng click (`PasteService` — set clipboard + `SetForegroundWindow` + giả lập
      Ctrl+V qua `SendInput`).
- [ ] **Chưa chạy thử thật** trên Windows để xác nhận tiêu chí "dùng thay thế được trong sinh
      hoạt hằng ngày" — mọi thứ ở trên mới dừng ở mức "viết đúng theo API, chưa test runtime".

## Giai đoạn 2 — Shelf cơ bản (phím tắt)

**Tiêu chí gốc:** Kéo tệp vào bubble bằng phím tắt, xem lại trong panel, kéo ra nơi cần dán,
không rơi/lỗi dữ liệu.

- [x] Drag & Drop Bridge — dùng WPF `DragDrop`/`AllowDrop` thay vì tự viết COM (xem
      `ARCHITECTURE.md` để biết lý do đây là lựa chọn tương đương, ít rủi ro hơn).
- [x] Bubble (`BubbleWindow`) + panel (`ShelfPanelWindow`) với 1 shelf mặc định
      (`ShelfSessionManager.EnsureDefaultShelfAsync`).
- [x] Mở bằng giữ phím tắt trong lúc kéo (`EdgeAndHotkeyDragTrigger`, `HotkeyTriggerEnabled`).
- [x] Kéo tệp ra khỏi shelf (`ShelfPanelWindow` — `DragDrop.DoDragDrop` khi kéo item, hiện hỗ
      trợ **từng item một**, chưa hỗ trợ kéo cả nhóm cùng lúc).
- [ ] Chưa chạy thử để xác nhận "không rơi/lỗi dữ liệu" trên Windows thật.

## Giai đoạn 3 — Edge-Drag Trigger

**Tiêu chí gốc:** Phát hiện rìa đáng tin cậy trên Explorer và ít nhất 2 ứng dụng khác, có thể
tắt nếu gây phiền.

- [x] Cơ chế kéo tệp ra rìa màn hình (`EdgeAndHotkeyDragTrigger` + `Core/Services/EdgeDetector`).
- [x] Toán học phát hiện rìa được test đầy đủ trên Linux (`EdgeDetectorTests.cs` — 6 test, bao
      gồm trường hợp góc màn hình, rìa bị tắt, margin = 0).
- [x] Có thể tắt qua Settings (`EdgeTriggerEnabled` trong `AppSettings` + UI trong
      `SettingsWindow`).
- [ ] **Chưa thể xác nhận "đáng tin cậy trên Explorer và ứng dụng khác"** — đây vốn dĩ là việc
      chỉ kiểm chứng được bằng cách chạy thật trên Windows với nhiều ứng dụng khác nhau, không
      thể verify bằng unit test.

## Giai đoạn 4 — Multi-shelf + Quick Actions

**Tiêu chí gốc:** Trải nghiệm Panel Shelf đầy đủ như bản mockup đã duyệt, Quick Actions xử lý
lỗi theo từng item.

- [x] Tạo/đổi tên/xoá nhiều shelf, gán màu (`ShelfPanelWindow.OnAddShelfClicked`,
      `ShelfSessionManager`).
- [x] Gán thư mục đích (`Shelf.DefaultTargetPath` có trong model; UI chọn thư mục đích *mỗi lần
      chạy* Quick Action đã có qua `OpenFolderDialog`, nhưng **UI để set `DefaultTargetPath` cố
      định cho một shelf — tức thao tác một-chạm — chưa được xây**, chỉ có chỗ trong data model
      và trong `QuickActionsEngine` đã sẵn sàng dùng nó).
- [x] Quick Actions: chuyển vào thư mục, nén ZIP, sao chép, sao chép vào Clipboard
      (`QuickActionsEngine` — có 6 unit test chạy qua trên Linux, bao gồm test cô lập lỗi
      từng item: `BatchAction_OneItemFails_OthersStillSucceed`).
- [ ] Quick Action "Mở bằng..." và "Chia sẻ" có trong model (`QuickActionType.OpenWith`,
      `.Share`) và có implementation (`ShellLauncherImpl`), nhưng **không xuất hiện trên thanh
      Quick Actions của `ShelfPanelWindow`** — panel hiện chỉ có 4 nút (Chuyển/Sao chép/Zip/
      Copy) đúng như bảng tính năng liệt kê, 2 action còn lại cần thêm nút nếu muốn lộ ra UI.
- [ ] Chưa chạy thử thật để xác nhận khớp mockup đã duyệt (mockup không có trong phạm vi những
      gì được cung cấp để đối chiếu).

## Giai đoạn 5 — Nâng cao lịch sử clipboard

**Tiêu chí gốc:** Trải nghiệm lịch sử đầy đủ như bản mockup đã duyệt.

- [x] Tìm kiếm (`ClipboardRepository.QueryAsync(searchText:)`, ô tìm kiếm trong
      `HistoryOverlayWindow`).
- [x] Ghim (`SetPinnedAsync`, nút ghim trong template item, ghim luôn hiện đầu danh sách).
- [x] Lọc theo loại (`RadioButton` filter chips: Tất cả/Văn bản/Ảnh/Tệp/Liên kết).
- [x] Xử lý trùng lặp (`HashDedup` + `FindByHashAsync` — không insert nếu trùng nội dung gần
      nhất).
- [x] Trạng thái trống (`EmptyState` panel khi danh sách rỗng).
- [x] Panel tự co giãn theo số mục rồi cuộn (`SizeToContent="Height"` + `MaxHeight` +
      `ScrollViewer.VerticalScrollBarVisibility="Auto"` trên `ListBox`).
- [ ] Chưa chạy thử thật để xác nhận khớp mockup đã duyệt.

## Giai đoạn 6 — Đóng gói & phát hành

**Tiêu chí gốc:** Cài đặt/gỡ cài đặt sạch sẽ, không bị phần mềm diệt virus gắn cờ nhầm.

- [x] Khởi động cùng Windows (`StartupRegistration` — ghi registry Run key).
- [x] Trang cài đặt đầy đủ (`SettingsWindow` — rìa kích hoạt, phím giữ, tự ẩn bubble, giới hạn
      lịch sử, tự xoá dữ liệu nhạy cảm, khởi động cùng Windows).
- [ ] **Ký số ứng dụng** — không làm được (cần chứng chỉ code-signing thật + máy Windows).
- [ ] **Đóng gói installer** (.exe/.msi/winget) — chưa có, cần quyết định kênh phân phối trước
      (xem mục 7 kế hoạch gốc, câu hỏi còn để ngỏ).
- [ ] Danh sách loại trừ theo ứng dụng nguồn (`AppSettings.ExcludedSourceApps` có trong model
      nhưng **`ClipboardMonitorService` chưa đọc field này để thực sự bỏ qua** — cần nối dây
      thêm một điều kiện kiểm tra `SourceApp` trước khi lưu).

## Việc cần quyết định tiếp theo (mục 7 kế hoạch gốc)

Không có mục nào trong danh sách này cần code để trả lời — đây là các quyết định sản phẩm mà
kế hoạch gốc cố tình để ngỏ cho người dùng/chủ dự án quyết định trước khi build tiếp:

- Rìa màn hình mặc định, phím tắt giữ-khi-kéo mặc định — **đã có giá trị mặc định hợp lý trong
  code** (`ScreenEdge.Right`, `ModifierHoldKey.RightShift`) nhưng đều chỉnh được qua Settings,
  không khoá cứng.
- Shelf lưu qua khởi động theo mặc định hay không — **mặc định đang là KHÔNG**
  (`AppSettings.NewShelvesPersistByDefault = false`), khớp với hướng "tắt, người dùng tự bật"
  trong kế hoạch.
- Mô hình phát hành, đồng bộ nhiều máy, kênh phân phối, tên sản phẩm/thương hiệu, dogfooding
  trước giai đoạn 4 — đều là quyết định sản phẩm/kinh doanh, không có trong phạm vi code.

## Tổng kết mức độ hoàn thành

Toàn bộ **logic nghiệp vụ** (data model, dedup, Quick Actions, edge/drag detection, settings)
đã hiện thực đầy đủ và **được unit test xác nhận đúng (42/42 test pass)**. Toàn bộ **giao diện
và tích hợp Windows** (WPF windows, hook, hotkey, clipboard, drag-drop, WinRT Share) đã viết
đầy đủ theo đúng kiến trúc kế hoạch mô tả nhưng **chưa qua một lần build hay chạy thử nào** — vì
vậy bước tiếp theo bắt buộc, trước khi làm bất cứ việc gì khác, là mở solution trên Windows,
`dotnet build`, và sửa các lỗi biên dịch/runtime sẽ xuất hiện.
