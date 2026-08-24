import sys
sys.path.insert(0, '.')
from parts import *

PANEL = f"background: #FFFFFF08; border: 1px solid {CTRL_EDGE}; border-radius: 14px;"


def label(text):
    return (
        f'<div style="font-family: {MONO}; font-size: 13px; font-weight: 700; '
        f'letter-spacing: 0.2em; color: {ACCENT}; text-transform: uppercase;">{text}</div>'
    )


def h2(text):
    return (
        f'<div style="font-size: 30px; font-weight: 800; letter-spacing: -0.02em; '
        f'color: {TEXT}; margin-top: 10px;">{text}</div>'
    )


def note(text):
    return (
        f'<div style="font-size: 15px; line-height: 1.55; color: {TEXT_3}; margin-top: 8px; '
        f'text-wrap: pretty;">{text}</div>'
    )


def copyblock(text, size=17):
    """Caption text, laid out to be selected and pasted straight into the post box."""
    return (
        f'<div style="{PANEL} padding: 24px 26px; margin-top: 16px; font-size: {size}px; '
        f'line-height: 1.62; color: {TEXT}; white-space: pre-line; text-wrap: pretty;">{text}</div>'
    )


def tags(text):
    return (
        f'<div style="{PANEL} padding: 18px 22px; margin-top: 12px; font-family: {MONO}; '
        f'font-size: 15px; line-height: 1.7; color: {ACCENT}; word-break: break-word;">{text}</div>'
    )


def section(lbl, title, *blocks):
    return (
        f'<div style="display: flex; flex-direction: column;">'
        + label(lbl) + h2(title) + "".join(blocks) + '</div>'
    )


# ================================================================= caption sheet
FB_MAIN = """Bạn đang kéo một file, rồi nhận ra thư mục đích đang nằm sau bốn cửa sổ khác.

WinDropOver sinh ra cho đúng khoảnh khắc đó. Đang kéo, bạn lắc chuột một cái — một cái khay hiện ra ngay dưới con trỏ. Thả file vào đấy. Chuyện tìm chỗ để, tính sau.

• Lắc chuột giữa lúc kéo là khay hiện ra — không cần nhớ phím tắt
• Kéo file ra khỏi khay là nó tự biến mất, không phải dọn
• Thả file vào ô hành động: Chuyển, Sao chép, Nén ZIP, Xoá
• Cài per-user, không cần quyền quản trị

Miễn phí, mã nguồn mở.
Tải tại: github.com/wwwxadieu/WinDropOver/releases

Nói trước cho rõ: app chưa ký số, nên lần đầu chạy Windows SmartScreen sẽ cảnh báo — bấm "More info" rồi "Run anyway". Mã nguồn công khai, ai muốn chắc chắn thì tự build được."""

FB_SHORT = """Đang kéo file mà chưa biết thả đâu? Lắc chuột.

Khay hiện ngay dưới con trỏ. Thả vào đó, tìm chỗ để sau. Kéo ra là file tự rời khay.

Miễn phí, mã nguồn mở, không cần quyền quản trị.
github.com/wwwxadieu/WinDropOver"""

IG_MAIN = """Lắc chuột. Khay hiện ra.

Đang kéo file mà chưa biết thả đâu? Lắc một cái, khay hiện ngay dưới con trỏ. Thả vào đó — tìm chỗ để sau.

Kéo file ra khỏi khay là nó tự biến mất. Cần gấp thì thả thẳng vào ô hành động: Chuyển, Sao chép, Nén ZIP, Xoá.

Miễn phí, mã nguồn mở. Link ở bio."""

STORY_TEXT = """Khung 1 — "Đang kéo file…"
Khung 2 — "…chưa biết thả đâu?"
Khung 3 — "Lắc chuột."
Khung 4 — "Xong." + sticker link"""

body = f"""  <div style="position: absolute; inset: 0; background: {INK};"></div>
  {grain(0.04)}
  <div style="position: relative; padding: 68px 66px 72px; display: flex; flex-direction: column;
              gap: 46px;">
    <div style="display: flex; align-items: center; gap: 14px;">{mark(40)}
      <div style="font-size: 24px; font-weight: 700; color: {TEXT};">WinDropOver</div>
      <div style="flex: 1;"></div>
      <div style="font-family: {MONO}; font-size: 14px; color: {TEXT_3};">Nội dung đăng bài</div>
    </div>

    {section("Facebook", "Bài chính",
             note("Dùng với ảnh 1200 × 630 (bài có link) hoặc 1080 × 1080 (bài ảnh)."),
             copyblock(FB_MAIN))}

    {section("Facebook", "Biến thể ngắn",
             note("Cho bài đăng lại, hoặc khi post vào group — group ghét bài dài."),
             copyblock(FB_SHORT))}

    {section("Instagram", "Caption",
             note("Instagram không cho bấm link trong caption, nên phải để link ở bio."),
             copyblock(IG_MAIN),
             tags("#windows #windows11 #productivity #tienich #thuthuatmaytinh #congnghe "
                  "#opensource #devtools #dropover #keotha #phanmemmienphi #maytinh"))}

    {section("Story", "Chữ chèn lên 4 khung",
             note("Mỗi khung 2 giây, chữ to, đặt ở nửa trên để không bị thanh trả lời che."),
             copyblock(STORY_TEXT, 16))}

    <div style="{PANEL} padding: 26px 28px; border-color: #EF444440;">
      <div style="font-family: {MONO}; font-size: 13px; font-weight: 700; letter-spacing: 0.2em;
                  color: {DANGER};">NHỚ KIỂM TRA</div>
      <div style="font-size: 16px; line-height: 1.6; color: {TEXT_2}; margin-top: 12px;
                  text-wrap: pretty;">Ảnh trong bộ này là giao diện <b style="color: {TEXT};">dựng
        lại bằng vector</b> theo đúng màu và kích thước trong mã nguồn, không phải ảnh chụp màn
        hình. Bạn đang có app chạy thật — chụp vài tấm rồi thay vào sẽ thuyết phục hơn hẳn, nhất
        là ảnh có desktop thật mờ sau lớp kính.</div>
    </div>
  </div>"""
open("Caption.dc.html", "w").write(page(
    "Nội dung đăng bài", f"position: relative; width: 900px; min-height: 2323px; "
                         f"background: {INK}; overflow: hidden;", body))


# ================================================================= video script
SHOTS = [
    ("0:00", "0:03", "Desktop gọn. Con trỏ nhấc một file lên và kéo ngang qua, rồi khựng lại "
                     "giữa màn hình.", "Kéo được rồi.",
     "Quay đúng cái ngập ngừng đó — nó là toàn bộ vấn đề mà app giải quyết."),
    ("0:03", "0:05", "Con trỏ lắc trái–phải ba nhịp, vẫn giữ file.", "Giờ thả đâu?",
     "Lắc dứt khoát, biên độ rộng. Lắc yếu thì app không nhận, mà quay ra cũng không đọc được."),
    ("0:05", "0:08", "Khay bung lên ngay dưới con trỏ.", "Lắc chuột.",
     "Cảnh quan trọng nhất. Để nó thở trọn 3 giây, đừng cắt sớm."),
    ("0:08", "0:13", "Thả bốn file vào khay. Chồng file xoè ra như cầm một xấp giấy.",
     "Thả vào. Tìm chỗ để sau.",
     "Thả từng file cách nhau một nhịp để thấy chồng file lớn dần."),
    ("0:13", "0:18", "Mở một thư mục, kéo một file từ khay ra. File biến mất khỏi khay.",
     "Kéo ra là xong.",
     "Chi tiết ít ai ngờ nhất — cho thấy khay tự vơi đi, đừng lướt qua."),
    ("0:18", "0:25", "Kéo file lên nút Hành động. Bảng bốn ô nở lên. Thả vào ô Nén ZIP.",
     "Chuyển · Sao chép · Nén ZIP · Xoá",
     "Bảng nở trong 170ms, bốn ô vào lệch nhau 40ms. Phải quay 60fps mới thấy được."),
    ("0:25", "0:30", "Cắt sang nền tối trơn. Logo hiện lên, rồi dòng link.",
     "Miễn phí. Mã nguồn mở.",
     "github.com/wwwxadieu/WinDropOver — để chữ đứng yên đủ lâu để người ta gõ lại."),
]


def shot(a, b, scene, screen, tip):
    return (
        f'<div style="{PANEL} padding: 20px 22px; display: grid; '
        f'grid-template-columns: 92px minmax(0, 1fr); gap: 20px; align-items: start;">'
        f'<div style="display: flex; flex-direction: column; gap: 2px;">'
        f'<div style="font-family: {MONO}; font-size: 17px; font-weight: 700; color: {TEXT};">'
        f'{a}</div>'
        f'<div style="font-family: {MONO}; font-size: 13px; color: {TEXT_3};">→ {b}</div></div>'
        f'<div style="display: flex; flex-direction: column; gap: 10px;">'
        f'<div style="font-size: 16px; line-height: 1.5; color: {TEXT_2}; text-wrap: pretty;">'
        f'{scene}</div>'
        f'<div style="display: inline-flex; align-self: flex-start; background: {ACCENT}1F; '
        f'border: 1px solid {ACCENT}59; border-radius: 8px; padding: 7px 13px; font-size: 15px; '
        f'font-weight: 700; color: {TEXT};">{screen}</div>'
        f'<div style="font-size: 14px; line-height: 1.5; color: {TEXT_3}; text-wrap: pretty;">'
        f'{tip}</div></div></div>'
    )


REC = [
    ("Quay", "OBS Studio, Display Capture, 1920 × 1080 @ 60fps. 30fps sẽ nuốt mất các chuyển "
             "động 40–170ms — chính là thứ đang muốn khoe."),
    ("Nền", "Hình nền tối, trơn, ít chi tiết. Khay là kính mờ: nền rối thì nhìn qua chỉ thấy "
            "một vũng xám."),
    ("Tốc độ", "Rê chuột chậm hơn lúc dùng thật khoảng một phần ba. Tốc độ dùng quen tay là tốc "
               "độ người xem không kịp đọc."),
    ("Khung hình", "Quay 16:9 rồi cắt: 1:1 cho bài Instagram, 9:16 cho Reels và Story. Đừng quay "
                   "dọc ngay từ đầu — cắt xuống thì được, phóng lên thì vỡ."),
    ("Âm thanh", "Nhạc không lời, nhịp vừa, cắt cảnh rơi vào phách. Không cần lời thuyết minh; "
                 "chữ trên màn hình đã đủ."),
    ("Bản 15 giây", "Cho Reels: giữ cảnh 2, 3, 4 và 7. Bỏ cảnh 1, 5, 6. Vẫn kể trọn được câu "
                    "chuyện lắc → hiện → thả → tải."),
]


def rec_row(k, v):
    return (
        f'<div style="display: grid; grid-template-columns: 150px minmax(0, 1fr); gap: 20px; '
        f'padding: 15px 0; border-top: 1px solid {CTRL_EDGE};">'
        f'<div style="font-size: 16px; font-weight: 700; color: {TEXT};">{k}</div>'
        f'<div style="font-size: 15px; line-height: 1.55; color: {TEXT_2}; text-wrap: pretty;">'
        f'{v}</div></div>'
    )


body = f"""  <div style="position: absolute; inset: 0; background: {INK};"></div>
  {grain(0.04)}
  <div style="position: relative; padding: 68px 66px 72px; display: flex; flex-direction: column;
              gap: 44px;">
    <div style="display: flex; align-items: center; gap: 14px;">{mark(40)}
      <div style="font-size: 24px; font-weight: 700; color: {TEXT};">WinDropOver</div>
      <div style="flex: 1;"></div>
      <div style="font-family: {MONO}; font-size: 14px; color: {TEXT_3};">Kịch bản video · 30 giây</div>
    </div>

    <div>
      <div style="font-size: 44px; font-weight: 800; letter-spacing: -0.02em; color: #F2F2F2; margin-bottom: 10px;">Bảy cảnh, không lời thuyết minh</div>
      <div style="font-size: 17px; line-height: 1.55; color: {TEXT_2}; max-width: 720px;
                  text-wrap: pretty;">Cả video chỉ chứng minh một câu: đang kéo file, lắc chuột,
        khay hiện ra. Mọi thứ khác là hệ quả. Đừng thêm cảnh nào không phục vụ câu đó.</div>
    </div>

    <div style="display: flex; flex-direction: column; gap: 14px;">
      {"".join(shot(*s) for s in SHOTS)}
    </div>

    <div>
      {label("Khi quay")}
      {h2("Sáu điều quyết định video xem được hay không")}
      <div style="margin-top: 18px;">{"".join(rec_row(k, v) for k, v in REC)}</div>
    </div>
  </div>"""
body = body.replace("{W_H}", "")
open("VideoScript.dc.html", "w").write(page(
    "Kịch bản video", f"position: relative; width: 1100px; min-height: 2010px; "
                      f"background: {INK}; overflow: hidden;", body))

print("docs written")
