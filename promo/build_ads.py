import sys
sys.path.insert(0, '.')
from parts import *

W = "font-weight: 800; letter-spacing: -0.03em; line-height: 0.98; color: %s;" % TEXT


def eyebrow(text, size=15):
    return (
        f'<div style="font-family: {MONO}; font-size: {size}px; font-weight: 700; '
        f'letter-spacing: 0.22em; color: {ACCENT}; text-transform: uppercase;">{text}</div>'
    )


def wordmark(mark_size=42, text_size=25, color=TEXT):
    return (
        f'<div style="display: flex; align-items: center; gap: 12px;">{mark(mark_size)}'
        f'<div style="font-size: {text_size}px; font-weight: 700; letter-spacing: -0.02em; '
        f'color: {color};">WinDropOver</div></div>'
    )


def cta(size=20, pad="16px 30px"):
    return (
        f'<div style="display: flex; align-items: center; gap: 20px; flex-wrap: wrap;">'
        f'<div style="background: {ACCENT}; color: #FFFFFF; font-size: {size}px; font-weight: 700; '
        f'padding: {pad}; border-radius: 999px; box-shadow: 0 10px 30px {ACCENT}59;">'
        f'Tải miễn phí</div>'
        f'<div style="font-family: {MONO}; font-size: {size - 5}px; color: {TEXT_3};">'
        f'github.com/wwwxadieu/WinDropOver</div></div>'
    )


def feature(text, size=19):
    return (
        f'<div style="display: flex; align-items: flex-start; gap: 12px;">'
        f'<div style="width: 7px; height: 7px; border-radius: 4px; background: {ACCENT}; '
        f'margin-top: {round(size * 0.42)}px; flex-shrink: 0;"></div>'
        f'<div style="font-size: {size}px; line-height: 1.45; color: {TEXT_2}; '
        f'text-wrap: pretty;">{text}</div></div>'
    )


# ---------------------------------------------------------------- Facebook 1200x630
body = f"""  {bg(1200, 630, "72% 45%")}
  <div style="position: absolute; inset: 0; display: flex; align-items: center; gap: 40px;
              padding: 0 68px;">
    <div style="display: flex; flex-direction: column; gap: 26px; width: 600px;">
      {wordmark()}
      {eyebrow("Cho Windows 10 &amp; 11")}
      <div style="font-size: 78px; {W}">Lắc chuột.<br />Khay hiện ra.</div>
      <div style="font-size: 21px; line-height: 1.5; color: {TEXT_2}; max-width: 540px;
                  text-wrap: pretty;">Đang kéo file mà chưa biết thả đâu? Lắc một cái — khay
        hiện ngay dưới con trỏ. Thả vào đó, tìm chỗ để sau.</div>
      {cta()}
    </div>
    <div style="position: relative; flex: 1; height: 100%; display: flex; align-items: center;
                justify-content: center;">
      <div style="position: absolute; top: 72px; left: -72px; z-index: 3;">
        {shake_cursor(38)}
      </div>
      {scaled_card(1.24, "filled", "margin-top: 34px;")}
    </div>
  </div>"""
open("Main.dc.html", "w").write(page(
    "Facebook", f"position: relative; width: 1200px; height: 630px; background: {INK}; "
                f"overflow: hidden;", body))


# ---------------------------------------------------------------- Instagram feed 1080x1080
body = f"""  {bg(1080, 1080, "50% 34%")}
  <div style="position: absolute; inset: 0; display: flex; flex-direction: column;
              align-items: center; padding: 62px 64px 58px;">
    {wordmark(38, 23)}
    <div style="font-size: 88px; {W} text-align: center; margin-top: 40px;">Lắc chuột.<br />Khay hiện ra.</div>
    <div style="font-size: 24px; line-height: 1.45; color: {TEXT_2}; text-align: center;
                max-width: 660px; margin-top: 22px; text-wrap: pretty;">Đang kéo file mà chưa
      biết thả đâu? Lắc một cái, khay hiện ngay dưới con trỏ.</div>
    <div style="position: relative; margin-top: 28px;">
      <div style="position: absolute; top: -26px; left: -86px; z-index: 3;">
        {shake_cursor(40)}
      </div>
      {scaled_card(1.30)}
    </div>
    <div style="flex: 1;"></div>
    <div style="font-family: {MONO}; font-size: 20px; color: {TEXT_3};">
      Miễn phí &amp; mã nguồn mở · github.com/wwwxadieu/WinDropOver</div>
  </div>"""
open("IGFeed.dc.html", "w").write(page(
    "Instagram", f"position: relative; width: 1080px; height: 1080px; background: {INK}; "
                 f"overflow: hidden;", body))


# ---------------------------------------------------------------- Instagram compare 1080x1080
def ghost_window(x, y, w, h, rot, dim):
    return (
        f'<div style="position: absolute; left: {x}px; top: {y}px; width: {w}px; height: {h}px; '
        f'border-radius: 10px; background: #FFFFFF{dim}; border: 1px solid #FFFFFF1F; '
        f'transform: rotate({rot}deg);">'
        f'<div style="height: 22px; border-bottom: 1px solid #FFFFFF14; display: flex; '
        f'align-items: center; gap: 5px; padding: 0 10px;">'
        f'<div style="width: 7px; height: 7px; border-radius: 4px; background: #FFFFFF2E;"></div>'
        f'<div style="width: 7px; height: 7px; border-radius: 4px; background: #FFFFFF2E;"></div>'
        f'<div style="width: 7px; height: 7px; border-radius: 4px; background: #FFFFFF2E;"></div>'
        f'</div></div>'
    )


body = f"""  {bg(1080, 1080, "50% 78%")}
  <div style="position: absolute; inset: 0; display: flex; flex-direction: column;">
    <div style="position: relative; height: 486px; padding: 56px 64px 0; overflow: hidden;">
      <div style="font-family: {MONO}; font-size: 17px; font-weight: 700; letter-spacing: 0.24em;
                  color: {TEXT_3};">TRƯỚC</div>
      <div style="font-size: 44px; font-weight: 800; letter-spacing: -0.02em; color: {TEXT_2};
                  margin-top: 14px; max-width: 530px; line-height: 1.15;">Mở thư mục đích.
        <span style="white-space: nowrap;">Alt-tab.</span> Kéo lại từ đầu.</div>
      {ghost_window(620, 92, 330, 210, -6, "0A")}
      {ghost_window(680, 156, 330, 210, 4, "0D")}
      {ghost_window(596, 232, 330, 210, -2, "12")}
      <div style="position: absolute; left: 742px; top: 330px;">{cursor(36, 12)}</div>
    </div>
    <div style="height: 1px; background: linear-gradient(90deg, transparent, {CTRL_EDGE} 18%,
                {CTRL_EDGE} 82%, transparent);"></div>
    <div style="position: relative; flex: 1; padding: 46px 64px 0; overflow: hidden;">
      <div style="font-family: {MONO}; font-size: 17px; font-weight: 700; letter-spacing: 0.24em;
                  color: {ACCENT};">SAU</div>
      <div style="font-size: 52px; {W} margin-top: 14px; max-width: 470px; line-height: 1.1;">
        Lắc chuột. Thả vào khay.</div>
      <div style="font-size: 20px; line-height: 1.45; color: {TEXT_2}; margin-top: 20px;
                  max-width: 420px; text-wrap: pretty;">Khay hiện ngay dưới con trỏ. Kéo file ra
        lúc nào cũng được — ra khỏi khay là nó tự biến mất.</div>
      <div style="position: absolute; right: 74px; top: 6px;">{scaled_card(1.06)}</div>
      <div style="position: absolute; left: 64px; bottom: 40px; display: flex; align-items: center;
                  gap: 14px;">{mark(34)}
        <div style="font-family: {MONO}; font-size: 18px; color: {TEXT_3};">WinDropOver · miễn phí</div>
      </div>
    </div>
  </div>"""
open("IGCompare.dc.html", "w").write(page(
    "Instagram so sánh", f"position: relative; width: 1080px; height: 1080px; background: {INK}; "
                         f"overflow: hidden;", body))


# ---------------------------------------------------------------- Instagram story 1080x1920
body = f"""  {bg(1080, 1920, "50% 42%")}
  <div style="position: absolute; inset: 0; display: flex; flex-direction: column;
              align-items: center; padding: 260px 70px 330px;">
    {wordmark(46, 28)}
    <div style="font-size: 112px; {W} text-align: center; margin-top: 52px;">Lắc chuột.<br />Khay hiện ra.</div>
    <div style="position: relative; margin-top: 62px;">
      <div style="position: absolute; top: -40px; left: -76px; z-index: 3;">
        {shake_cursor(46)}
      </div>
      {scaled_card(1.55)}
    </div>
    <div style="display: flex; flex-direction: column; gap: 18px; margin-top: 60px; width: 100%;
                max-width: 700px;">
      {feature("Lắc chuột giữa lúc kéo — không cần nhớ phím tắt", 24)}
      {feature("Kéo file ra khỏi khay là nó tự biến mất", 24)}
      {feature("Chuyển · Sao chép · Nén ZIP · Xoá — thả vào ô là chạy", 24)}
    </div>
    <div style="flex: 1;"></div>
    <div style="font-family: {MONO}; font-size: 24px; color: {TEXT_3}; text-align: center;">
      Miễn phí &amp; mã nguồn mở<br /><span style="color: {TEXT_2};">github.com/wwwxadieu/WinDropOver</span></div>
  </div>"""
open("IGStory.dc.html", "w").write(page(
    "Instagram story", f"position: relative; width: 1080px; height: 1920px; background: {INK}; "
                       f"overflow: hidden;", body))


# ---------------------------------------------------------------- Facebook cover 820x312
body = f"""  {bg(820, 312, "76% 50%")}
  <div style="position: absolute; inset: 0; display: flex; align-items: center;
              padding: 0 46px 0 200px;">
    <div style="display: flex; flex-direction: column; gap: 14px; width: 320px;">
      {wordmark(40, 24)}
      <div style="font-size: 36px; {W}">Lắc chuột. Khay hiện ra.</div>
      <div style="font-family: {MONO}; font-size: 14px; color: {TEXT_3};">
        Khay kéo-thả cho Windows · miễn phí</div>
    </div>
    <div style="position: absolute; right: 44px; top: 30px;">{scaled_card(0.72)}</div>
  </div>"""
open("FBCover.dc.html", "w").write(page(
    "Ảnh bìa Facebook", f"position: relative; width: 820px; height: 312px; background: {INK}; "
                        f"overflow: hidden;", body))

print("ads written")
