# Bộ quảng cáo

Ảnh đăng Facebook và Instagram, nội dung bài đăng, và một sân quay để dựng video giới thiệu.

## Đăng được ngay

`export/` chứa PNG đã xuất sẵn, đúng kích thước từng nơi — kéo thẳng vào ô đăng bài, không cần
mở gì. Sửa lại thì dựng lại rồi xuất đè lên (xem *Dựng lại* bên dưới).

## Mở ngay

- **`recording-stage.html`** — mở bằng trình duyệt. Bảy cảnh giới thiệu chạy đúng thời lượng
  chuyển động thật của app. Bấm *Chế độ quay* để giấu phần điều khiển, rồi thu màn hình: đó là
  video. Ba nút khung hình cắt thật 1:1 và 9:16 để xem trước cảnh nào bị mất hai mép.
  Không cần cài gì, không gọi mạng ngoài trừ Google Fonts.

Các tệp `.dc.html` là từng khung ảnh, mở riêng lẻ bằng trình duyệt cũng xem được.

## Kích thước

| Tệp | Khung | Dùng cho |
| --- | --- | --- |
| `Main.dc.html` | 1200 × 630 | Bài Facebook có link |
| `FBCover.dc.html` | 820 × 312 | Ảnh bìa trang Facebook |
| `IGFeed.dc.html` | 1080 × 1080 | Bài ảnh Facebook và Instagram — hướng khoe sản phẩm |
| `IGCompare.dc.html` | 1080 × 1080 | Cùng chỗ đăng — hướng "Trước / Sau" |
| `IGStory.dc.html` | 1080 × 1920 | Story và Reels |
| `Caption.dc.html` | tài liệu | Nội dung bài đăng, hashtag, chữ chèn Story |
| `VideoScript.dc.html` | tài liệu | Kịch bản video 30 giây, bảy cảnh |

Hai khung vuông cố ý đi hai hướng khác nhau. Đăng cả hai rồi giữ cái nào có tương tác tốt hơn.

## Dựng lại sau khi sửa

`parts.py` giữ toàn bộ màu, kích thước và hình icon; hai tệp `build_*.py` ráp chúng thành các
khung. Sửa ở `parts.py` thì mọi khung đổi theo:

```
cd promo && python3 build_ads.py && python3 build_docs.py
```

Xuất lại PNG cần một trình duyệt chạy nền. Với Chrome hoặc Edge trên Windows:

```
"C:\Program Files\Google\Chrome\Application\chrome.exe" --headless --disable-gpu ^
  --window-size=1200,630 --screenshot=export\facebook-1200x630.png Main.dc.html
```

Kích thước cửa sổ phải khớp đúng bảng trên. Dùng `--headless` của Chrome bản đầy đủ thì ảnh có
thể bị hụt phần dưới do trừ hao thanh công cụ — nếu gặp, thêm `--headless=new`.

`canvas.json` chỉ mô tả vị trí các khung trên canvas của bản đã đăng, không ảnh hưởng khi mở
tệp `.dc.html` trực tiếp.

## Hai điều cần biết

**Giao diện trong ảnh là dựng lại bằng vector, không phải ảnh chụp màn hình.** Mọi màu, bo góc
và kích thước đều lấy từ `src/WinClipboard.App/Styles/Theme.xaml` và `Views/BubbleWindow.xaml`,
nên không lệch so với app. Nhưng lớp kính mờ của khay chỉ ra đúng chất khi có desktop thật ở
phía sau — ảnh chụp từ máy đang chạy app sẽ thuyết phục hơn hẳn. Chỗ để thay ảnh vào là khối
`scaled_card(...)` trong từng khung.

**Thời lượng chuyển động trong `recording-stage.html` là số thật**, đọc ra từ
`Views/BubbleWindow.xaml.cs`: thẻ mở 190ms, chồng file 260ms, bảng hành động 170ms với bốn ô
lệch nhau 40ms. Nếu đổi các con số đó trong app thì đổi luôn ở đầu tệp, không thì bản dựng mẫu
sẽ nói sai về sản phẩm.
