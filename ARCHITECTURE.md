# Kiến trúc WinClipboard

Tham chiếu tới mục 3 của kế hoạch gốc. Tài liệu này mô tả các module *đã* tồn tại trong code và
map chúng tới đúng phần kế hoạch mà chúng hiện thực.

## Sơ đồ phụ thuộc project

```
WinClipboard.App  (WPF, net8.0-windows10.0.19041.0)
   │  tray icon, 4 cửa sổ (History/Bubble/Panel/Settings), composition root
   ├──> WinClipboard.Core     (model + business logic thuần)
   ├──> WinClipboard.Data     (SQLite + JSON settings)
   └──> WinClipboard.Interop  (Win32 P/Invoke)
              └──> WinClipboard.Core
WinClipboard.Data ──> WinClipboard.Core
```

`Core` không phụ thuộc project nào khác — mọi thứ trong đó phải chạy được trên bất kỳ hệ điều
hành nào (đây là lý do 20/20 test của nó chạy được trên Linux).

## Mô-đun chính và nơi tìm code (kế hoạch 3.2)

| Mô-đun trong kế hoạch | Code tương ứng | Ghi chú |
|---|---|---|
| Edge & Hotkey Drag Trigger | `Interop/ShelfDragTrigger.cs` + `Core/Services/EdgeDetector.cs` + `Core/Services/DragThresholdDetector.cs` + `Core/Services/ShakeDetector.cs` | Toán học thuần (so sánh toạ độ) tách riêng vào `Core` để test được mà không cần hook thật — xem `EdgeDetectorTests.cs`, `DragThresholdDetectorTests.cs`, `ShakeDetectorTests.cs`. Nay có **3** cách kích hoạt, xem mục đính chính bên dưới. |
| Nhận diện đang kéo tệp | `Interop/ShelfDragTrigger.cs` (left-button-down + ngưỡng di chuyển) | Vẫn là suy đoán như kế hoạch ghi nhận — không có API Windows chính thức để biết chắc "đang kéo file". |
| Shelf Manager | `App/Services/ShelfSessionManager.cs` | Implement `IShelfRepository` — vừa là facade vừa quyết định shelf nào ghi DB (`IsPersisted`) và shelf nào chỉ sống trong RAM (id âm). |
| Quick Actions Engine | `Core/Services/QuickActionsEngine.cs` | Thuần `Core`, test được trên Linux (`QuickActionsEngineTests.cs`) — cô lập lỗi từng item đúng như kế hoạch yêu cầu. |
| Drag & Drop Bridge | WPF `DragDrop`/`AllowDrop` built-in, dùng trong `BubbleWindow.xaml.cs` (nhận drop) và `ShelfPanelWindow.xaml.cs` (kéo item ra ngoài) | WPF's DragDrop API vốn đã bọc OLE drag-drop (COM `IDropTarget`/`IDropSource`) nên **không cần** tự viết COM interop thủ công như bản kế hoạch ban đầu hình dung. |
| Clipboard Monitor | `App/Services/ClipboardMonitorService.cs` + `Interop/Win32MessageWindow.cs` (WM_CLIPBOARDUPDATE) | Đọc clipboard bắt buộc chạy trên UI thread (`System.Windows.Clipboard`); listener Win32 chạy nền — xem phần threading bên dưới. Chống lặp qua cờ `SuppressNextChange`. |
| Global Hotkey Manager | `Interop/Win32MessageWindow.cs` (`RegisterHotKey`) | Cùng một message-loop thread với hook chuột/bàn phím và clipboard listener. |
| Overlay/Bubble Window | `App/Views/BubbleWindow.xaml(.cs)`, `HistoryOverlayWindow.xaml(.cs)`, `ShelfPanelWindow.xaml(.cs)` | `WindowStyleHelper.MakeLayeredToolWindow` áp `WS_EX_LAYERED`/`WS_EX_TOOLWINDOW` lên trên các thuộc tính WPF (`AllowsTransparency`, `Topmost`, `ShowInTaskbar=False`) đã có sẵn. |
| Data Layer | `Data/ClipboardRepository.cs`, `Data/ShelfRepository.cs`, `Data/SqliteSchema.cs` | Xem mô hình dữ liệu bên dưới. |
| Thumbnail / Preview Engine | Một phần: `ClipboardMonitorService.SaveThumbnail` lưu PNG cho ảnh copy | **Chưa có** cache/generation cho thumbnail file/tài liệu khác — xem ROADMAP.md. |

## Mô hình luồng xử lý (kế hoạch 3.3)

Đã hiện thực đúng 3 luồng kế hoạch mô tả:

1. **UI thread (WPF Dispatcher)** — mọi thao tác `System.Windows.Clipboard`, dựng cửa sổ, and
   toàn bộ code trong `App/Views` chạy ở đây.
2. **Win32 message-loop thread riêng** (`Interop/Win32MessageWindow.cs`) — sở hữu
   `WH_MOUSE_LL`, `WH_KEYBOARD_LL`, các hotkey đã `RegisterHotKey`, và clipboard format listener.
   Chạy độc lập UI thread; khi có sự kiện (hotkey, clipboard đổi, trigger kéo-thả) nó chỉ raise
   .NET event — `App.xaml.cs` luôn `Dispatcher.InvokeAsync(...)` để nhảy về UI thread trước khi
   đụng tới bất kỳ API WPF nào.

   **Quy tắc bất di bất dịch: thread này không bao giờ được chờ UI thread.** Không `Dispatcher.Invoke`,
   không `Task.Wait`, không `Join` — chỉ `InvokeAsync` rồi trả về ngay. Lý do: Windows gọi callback
   `WH_MOUSE_LL` **đồng bộ, trước khi sự kiện chuột tới bất kỳ ứng dụng nào**, nên thời gian ở
   trong callback là thời gian con trỏ chuột của *cả máy* đứng hình. Và callback nào vượt quá
   `LowLevelHooksTimeout` (mặc định 300ms) thì **Windows lặng lẽ gỡ hook**, không báo gì, không có
   API nào để hỏi hook còn sống hay không — mọi trigger chết vĩnh viễn cho tới khi khởi động lại app.
   Dựng một cửa sổ WPF lần đầu (parse XAML) thừa sức vượt mốc đó.

   Vì cẩn thận vẫn không đủ (một nhịp GC hay máy đang tải nặng cũng đủ vượt 300ms),
   `Win32MessageWindow` chạy thêm một `WM_TIMER` canh chừng: hook im quá 20 giây thì cài lại.

   Cùng lý do đó, `MouseHookEventArgs` là **struct**: nó được tạo cho mỗi thông điệp chuột hệ thống
   thấy — kể cả mọi `WM_MOUSEMOVE`, tới cả nghìn lần mỗi giây — nên nếu là class thì đó là một lần
   cấp phát heap cho mỗi lần nhích chuột, suốt thời gian app chạy.
3. **Background worker (ngầm định qua `async`/`await`)** — đọc/ghi SQLite, sinh thumbnail PNG,
   chạy Quick Actions đều là `async Task` không chặn hai thread trên; không có thread pool riêng
   được dựng thủ công vì khối lượng công việc trong một ứng dụng cá nhân này chưa cần đến.

## Mô hình dữ liệu (kế hoạch 3.5)

Khớp chính xác 3 bảng kế hoạch mô tả — xem `Data/SqliteSchema.cs` cho DDL đầy đủ:

- `ClipboardItem` — không đổi so với kế hoạch: `Type, CreatedAt, TextContent, FilePath,
  ThumbnailPath, SourceApp, IsPinned, HashDedup`.
- `Shelf` — `Name, ColorHex, DefaultActionType, DefaultTargetPath, SortOrder, IsPersisted`. **Chỉ
  những shelf có `IsPersisted = true` mới có row trong bảng này** — shelf session-only sống hoàn
  toàn trong `ShelfSessionManager`, không bao giờ chạm SQLite (xem phần "Facade" bên dưới).
- `ShelfItem` — `ShelfId, Type, FilePath, TextContent, ThumbnailPath, AddedAt, SortOrder`.

### `ShelfSessionManager`: facade session-vs-persisted

Đây là điểm kiến trúc không có trong kế hoạch gốc (kế hoạch chỉ nói "mặc định giữ trong bộ nhớ,
tuỳ chọn lưu SQLite" mà chưa nói *cách* hiện thực). Cách đã chọn:

- Shelf/item **session-only** được gán id **âm** (giảm dần từ -1), sống trong
  `Dictionary` in-memory, không bao giờ gọi tới `IShelfRepository` thật.
- Shelf/item **persisted** dùng id dương do SQLite autoincrement cấp, đi thẳng qua
  `ShelfRepository`.
- `ShelfSessionManager` tự nó implement `IShelfRepository`, nên `QuickActionsEngine` (và bất kỳ
  code nào khác) dùng chung một interface mà không cần biết shelf đang ở "bên nào".
- **Giới hạn đã biết:** quyết định persist-hay-không chỉ chốt lúc *tạo* shelf. Chuyển một shelf
  đã tồn tại từ session-only sang persisted (hay ngược lại) chưa được hỗ trợ — vì kế hoạch mục 7
  còn để ngỏ câu hỏi "có nên persist mặc định hay không", nên chưa có UI/product quyết định đủ rõ
  để build tính năng toggle này.

## Đính chính: kế hoạch gốc mô tả sai cách Dropover kích hoạt shelf

Mục 1.2 kế hoạch v2.0 ("Vì sao đổi sang mô hình Dropover") nêu hai điều, **cả hai đều sai**:

1. Cử chỉ lắc chuột là *"một heuristic tự đặt ra, không có tiền lệ ở ứng dụng nào khác"*.
2. Dropover kích hoạt shelf bằng *"kéo ra rìa màn hình hoặc giữ phím tắt"*.

Theo [mô tả chính thức trên Mac App Store](https://apps.apple.com/us/app/dropover-easier-drag-drop/id1355679052),
Dropover kích hoạt shelf bằng: **lắc chuột** (được gọi thẳng là *"the app's signature activation
method"*), phím tắt tuỳ chỉnh, menu bar, và thả vào notch. **Không có edge-drag.**

Nói cách khác: cử chỉ lắc không phải là phát minh không tiền lệ — nó chính là chữ ký của Dropover;
còn edge-drag, thứ được xây để thay thế nó, lại là phần Dropover không có. Toàn bộ giai đoạn 3
của kế hoạch được xây trên tiền đề ngược.

Hệ quả trong code hiện tại: giữ cả ba, mỗi cái bật/tắt độc lập trong Settings.

| Cách | Trạng thái | Ghi chú |
|---|---|---|
| Lắc chuột | `ShakeDetector` + `ShakeTriggerEnabled` | Thuật toán O(1) mỗi sample, không cấp phát — chạy trong hook callback nên phải rẻ (rủi ro mục 6: hook chậm bị Windows gỡ). Độ nhạy chỉnh được. |
| Giữ phím tắt | `HotkeyTriggerEnabled` + `HoldKey` | Đúng như Dropover. |
| Kéo ra rìa | `EdgeTriggerEnabled` + `EdgeDetector` | Dropover không có, nhưng đã viết xong và chạy được nên giữ lại như lựa chọn thêm. |

`ShakeDetector` cố tình **không** lưu lịch sử điểm: nó gộp chuyển động thành các "đoạn" theo
hướng và đếm số lần đảo chiều ngay khi xảy ra, nên chi phí mỗi sample là hằng số. Đây chính là
điều kế hoạch lo ngại khi bác bỏ cử chỉ lắc ("phải tích luỹ chuỗi điểm, phân tích đổi hướng vận
tốc") — lo ngại đó chỉ đúng với một cách hiện thực cụ thể, không phải với bản thân cử chỉ.

## Vì sao không cần tự viết COM `IDropTarget`/`IDropSource`

Bản kế hoạch (mục 3.1) liệt kê "COM interop (`IDropTarget`, `IDropSource`, `DoDragDrop`)" như một
lựa chọn công nghệ tách riêng. Trong C#/WPF, `System.Windows.DragDrop` (`DragDrop.DoDragDrop`,
`UIElement.AllowDrop`, sự kiện `Drop`) **đã bọc sẵn đúng cơ chế OLE drag-drop này** — WPF tự đăng
ký cửa sổ làm drop target khi `AllowDrop="True"`. Vì vậy `BubbleWindow`/`ShelfPanelWindow` dùng
thẳng API WPF thay vì tự viết COM interop, giảm đáng kể bề mặt code có thể sai mà không đổi hành
vi so với kế hoạch.

## Phần rủi ro nhất: `Services/ShareUI.cs`

Duy nhất trong toàn bộ codebase dùng WinRT projection (`Windows.ApplicationModel.DataTransfer`)
thay vì P/Invoke Win32 thuần — cần TFM `net8.0-windows10.0.19041.0` (đã cấu hình trong
`WinClipboard.App.csproj`) để CsWinRT tự sinh projection. Pattern dùng đúng theo tài liệu
Microsoft cho desktop app ("Share content from a desktop app" — `IDataTransferManagerInterop`),
nhưng **chưa build-verify** — xem README.md mục giới hạn.
