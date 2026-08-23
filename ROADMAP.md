# Tình trạng triển khai so với kế hoạch gốc

Đối chiếu với mục 5 (Lộ trình triển khai) của kế hoạch. Mỗi mục dưới đây map trực tiếp tới một
hàng trong bảng lộ trình gốc, cùng tiêu chí hoàn thành gốc và trạng thái thật của code.

## Giai đoạn 0 — Chuẩn bị

**Tiêu chí gốc:** Project chạy được, cửa sổ overlay rỗng hiện/ẩn bằng phím tắt.

- [x] Khung solution `.NET`/WPF theo đúng cấu trúc thư mục module ở mục 3.2 (`src/Core`,
      `Data`, `Interop`, `App`).
- [x] Cấu hình build (`.csproj` cho từng project, `WinClipboard.sln`).
- [x] **Build được xác nhận** — CI (`windows-latest`) build sạch toàn bộ solution kể cả
      `WinClipboard.App`, 68/68 test pass.
- [ ] **Chưa xác nhận "cửa sổ overlay hiện/ẩn bằng phím tắt"** — phần này cần chạy app thật,
      CI chỉ build chứ không chạy được UI.

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
- [x] Mở bằng giữ phím tắt trong lúc kéo (`ShelfDragTrigger`, `HotkeyTriggerEnabled`).
- [x] Kéo tệp ra khỏi shelf (`ShelfPanelWindow` — `DragDrop.DoDragDrop` khi kéo item, hiện hỗ
      trợ **từng item một**, chưa hỗ trợ kéo cả nhóm cùng lúc).
- [ ] Chưa chạy thử để xác nhận "không rơi/lỗi dữ liệu" trên Windows thật.

## Giai đoạn 3 — Edge-Drag Trigger (+ cử chỉ lắc, bổ sung sau)

**Tiêu chí gốc:** Phát hiện rìa đáng tin cậy trên Explorer và ít nhất 2 ứng dụng khác, có thể
tắt nếu gây phiền.

> **Đính chính:** kế hoạch v2.0 loại bỏ cử chỉ lắc vì cho rằng nó "không có tiền lệ", trong khi
> đó chính là cách kích hoạt đặc trưng của Dropover — xem mục đính chính trong `ARCHITECTURE.md`.
> Cử chỉ lắc đã được bổ sung; cả ba cách kích hoạt cùng tồn tại và bật/tắt độc lập.

- [x] Cơ chế kéo tệp ra rìa màn hình (`ShelfDragTrigger` + `Core/Services/EdgeDetector`).
- [x] **Cử chỉ lắc chuột** (`Core/Services/ShakeDetector` — 7 unit test, gồm chống nhiễu rung tay,
      chống tích luỹ đảo chiều rải rác ngoài cửa sổ thời gian, và một lần lắc chỉ kích hoạt một lần).
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
      (`QuickActionsEngine` — có 10 unit test chạy qua trên Linux, bao gồm test cô lập lỗi
      từng item: `BatchAction_OneItemFails_OthersStillSucceed`, và test khi *bản thân file ZIP*
      không tạo được thì báo lỗi theo từng item chứ không ném ra ngoài batch).
- [x] **Chạy Quick Action bằng kéo-thả** (bổ sung theo Dropover) — mỗi ô action trong panel vừa
      là nút vừa là **drop target**: thả item lên một ô thì chỉ chạy trên đúng những item đó,
      còn bấm vào ô thì chạy trên cả shelf. Kéo từ ngoài ứng dụng thả thẳng lên một ô thì item
      được gom vào shelf rồi chạy luôn trong một thao tác. Engine có
      `ExecuteOnItemsAsync(items, ...)` bên cạnh `ExecuteAsync(shelfId, ...)` cho việc này.
- [x] **Instant Actions** (bổ sung theo Dropover) — kéo nội dung lên bubble khi shelf đang rỗng
      thì bung ra dải action, thả thẳng vào một action để gom-và-chạy trong một thao tác
      (`BubbleWindow.RunInstantActionAsync`). Dùng `Shelf.DefaultTargetPath` nếu shelf đã đặt sẵn,
      chưa đặt thì hỏi thư mục.
- [x] **Nhận URL và text** (bổ sung theo Dropover) — `ShelfDropReader` đọc cả `FileDrop`,
      `UniformResourceLocatorW/…` và text; trước đây chỉ đọc `FileDrop` nên link/text bị bỏ im
      lặng dù model đã có `ShelfItemType.Text`/`.Link`.
- [x] **Sắp xếp lại và đổi tên item** (bổ sung theo Dropover) — nút lên/xuống và đổi tên trong
      panel; đổi tên item dạng File thì đổi tên **thật trên đĩa** (`File.Move`), vì đổi mỗi nhãn
      hiển thị sẽ không sống sót khi kéo tệp ra.
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
- [x] **Đóng gói tự động** — [`.github/workflows/release-build.yml`](.github/workflows/release-build.yml)
      publish `WinClipboard.App` dạng self-contained win-x64 single-file và đính **2 asset** vào
      GitHub Release (tự chạy khi push tag `v*`, hoặc chạy tay qua `workflow_dispatch`):
      bản `.zip` xách tay và bản `.msi` cài đặt.
- [x] **Installer .msi** ([`installer/WinClipboard.wxs`](installer/WinClipboard.wxs), WiX v5) —
      cài **theo từng người dùng** (`Scope="perUser"`, vào `%LocalAppData%\Programs`): không cần
      quyền admin nên không hiện UAC, và khớp với chính cách app lưu dữ liệu (settings + SQLite ở
      `%LocalAppData%`, "khởi động cùng Windows" ghi HKCU). Có lối tắt Start Menu, có mục gỡ cài
      đặt trong Apps & features, cài bản mới tự thay bản cũ (`MajorUpgrade`).
      **Lưu ý:** WiX không chạy được trên Linux nên file này **chỉ được kiểm chứng trên CI** —
      vì vậy job `installer` trong `ci.yml` build MSI ở mọi push/PR, không đợi tới lúc phát hành.
- [ ] **Ký số ứng dụng** — chưa có, cần chứng chỉ code-signing thật. Chưa ký thì SmartScreen sẽ
      cảnh báo khi người dùng chạy lần đầu (cả `.exe` lẫn `.msi`), và rủi ro "bị AV gắn cờ nhầm"
      trong bảng mục 6 vẫn còn nguyên (app cài low-level keyboard hook nên càng dễ bị nghi).
- [ ] **winget** — chưa nộp manifest; cần quyết định kênh phân phối (mục 7 kế hoạch gốc) và
      thường yêu cầu installer đã ký số.
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
đã hiện thực đầy đủ và **được unit test xác nhận đúng (68/68 test pass trên Windows CI)**. Toàn
bộ **giao diện và tích hợp Windows** (WPF windows, hook, hotkey, clipboard, drag-drop, WinRT
Share) đã viết đầy đủ theo đúng kiến trúc kế hoạch và **compile sạch trên Windows**.

Ranh giới hiện tại nằm ở chỗ khác: **chưa ai chạy app thật lần nào.** Mọi tiêu chí hoàn thành
trong kế hoạch gốc đều được phát biểu dưới dạng hành vi ("kéo tệp vào bubble... không rơi/lỗi dữ
liệu", "phát hiện rìa đáng tin cậy trên Explorer và ít nhất 2 ứng dụng khác"), mà hành vi thì
không thể xác nhận bằng compiler hay unit test — chỉ có thể xác nhận bằng cách cài và dùng thử.

Vì vậy bước tiếp theo là **dogfooding**: cài bản build lên một máy Windows thật, dùng vài ngày,
và đối chiếu lại từng tiêu chí hoàn thành ở các giai đoạn 1–5 phía trên. Riêng heuristic "đang
kéo tệp" (rủi ro Cao trong bảng mục 6 kế hoạch gốc) chỉ có thể đánh giá được theo cách này.
