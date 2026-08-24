# Shared pieces for the WinDropOver promo artboards.
#
# Every colour here is lifted from src/WinClipboard.App/Styles/Theme.xaml and converted
# from WPF's #AARRGGBB to CSS's #RRGGBBAA. Every dimension is lifted from
# Views/BubbleWindow.xaml. The card in the ads is the card in the app.

ACCENT      = "#3B82F6"
ACCENT_HOV  = "#5B9BFB"
DANGER      = "#EF4444"
GLASS       = "#1C1C20D9"   # GlassBrush      #D91C1C20
GLASS_EDGE  = "#FFFFFF40"   # GlassBorderBrush #40FFFFFF
CONTROL     = "#FFFFFF14"   # ControlBrush    #14FFFFFF
CTRL_EDGE   = "#FFFFFF26"   # SurfaceBorderBrush #26FFFFFF
SCRIM       = "#1C1C2059"   # ScrimBrush      #591C1C20
TEXT        = "#F2F2F2"     # TextPrimaryBrush
TEXT_2      = "#FFFFFF99"   # TextSecondaryBrush
TEXT_3      = "#FFFFFF66"   # TextMutedBrush
THUMB_BAR   = "#FFFFFF40"   # ScrollThumbBrush

INK         = "#0B0B0E"     # ad background, darker than the app so the glass card lifts off it
INK_2       = "#141419"

FONTS = (
    '<link rel="preconnect" href="https://fonts.googleapis.com" />\n'
    '  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="crossorigin" />\n'
    '  <link rel="stylesheet" '
    'href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;700;800&amp;'
    'family=JetBrains+Mono:wght@500;700&amp;display=swap" />'
)

SANS = "'Be Vietnam Pro', 'Segoe UI', system-ui, sans-serif"
MONO = "'JetBrains Mono', ui-monospace, 'Cascadia Mono', monospace"

# Icon geometry copied verbatim out of Theme.xaml, 24x24 grid, stroked with round caps.
ICONS = {
    "close": "M 18,6 L 6,18 M 6,6 L 18,18",
    "trash": "M 5,7 L 19,7 M 10,7 L 10,5 L 14,5 L 14,7 M 6.5,7 L 7.5,19 L 16.5,19 L 17.5,7 "
             "M 10.5,10.5 L 10.5,15.5 M 13.5,10.5 L 13.5,15.5",
    "chevron": "M 9,6 L 15,12 L 9,18",
    "move": "M 3,12 L 13,12 M 9.5,8.5 L 13,12 L 9.5,15.5 M 17,4 L 21,4 L 21,20 L 17,20",
    "copy": "M 9,9 L 20,9 L 20,20 L 9,20 Z M 5,15 L 4,15 L 4,4 L 15,4 L 15,5",
    "zip":  "M 3,9 L 21,9 L 21,20 L 3,20 Z M 3,9 L 5.5,4 L 18.5,4 L 21,9 M 12,9 L 12,20",
    "bolt": "M 13,3 L 6,13 L 11.5,13 L 11,21 L 18,11 L 12.5,11 Z",
    "trash_x": "M 5,7 L 19,7 M 10,7 L 10,5 L 14,5 L 14,7 M 6.5,7 L 7.5,19 L 16.5,19 L 17.5,7 "
               "M 10,10.5 L 14,15.5 M 14,10.5 L 10,15.5",
}


def icon(name, size, stroke, width=1.8):
    return (
        f'<svg width="{size}" height="{size}" viewBox="0 0 24 24" fill="none" '
        f'aria-hidden="true"><path d="{ICONS[name]}" stroke="{stroke}" stroke-width="{width}" '
        f'stroke-linecap="round" stroke-linejoin="round" /></svg>'
    )


def mark(size):
    """The application mark, the same geometry as Styles/AppIcon.xaml and tools/make-icon.py."""
    return (
        f'<svg width="{size}" height="{size}" viewBox="0 0 100 100" aria-hidden="true">'
        f'<rect x="6" y="6" width="88" height="88" rx="22" ry="22" fill="{ACCENT}" />'
        f'<rect x="43.5" y="18.5" width="13" height="28" rx="5.5" ry="5.5" fill="#FFFFFF" />'
        f'<path d="M 31.5,42 L 68.5,42 L 50,65.5 Z" fill="#FFFFFF" />'
        f'<rect x="20.5" y="71.5" width="59" height="13.5" rx="6" ry="6" fill="#FFFFFFCD" />'
        f'</svg>'
    )


# Placeholder file glyphs. Deliberately drawn rather than faked as real Office icons:
# a page with a folded corner, tinted per type, with the extension spelled out.
FILES = [
    ("bao-cao-Q3", "PDF",  "#EF4444"),
    ("anh-bia",    "PNG",  "#22C55E"),
    ("hop-dong",   "DOCX", "#3B82F6"),
    ("du-lieu",    "XLSX", "#16A34A"),
    ("logo",       "SVG",  "#F59E0B"),
    ("ban-nhap",   "ZIP",  "#A78BFA"),
]


def file_glyph(ext, tint, size=40):
    return (
        f'<svg width="{size}" height="{size}" viewBox="0 0 40 40" aria-hidden="true">'
        f'<path d="M 8,3 L 25,3 L 34,12 L 34,37 L 8,37 Z" fill="{tint}" fill-opacity="0.16" '
        f'stroke="{tint}" stroke-width="1.6" stroke-linejoin="round" />'
        f'<path d="M 25,3 L 25,12 L 34,12" fill="none" stroke="{tint}" stroke-width="1.6" '
        f'stroke-linejoin="round" />'
        f'<text x="21" y="30" text-anchor="middle" font-family="{MONO}" font-size="8.5" '
        f'font-weight="700" fill="{tint}">{ext}</text></svg>'
    )


def thumb_item(name, ext, tint):
    """One tile in the shelf's thumbnail grid: 80 wide, 72x72 tile at radius 8, 40px glyph."""
    return (
        f'<div style="width: 80px; margin: 3px 2px;">'
        f'<div style="width: 72px; height: 72px; border-radius: 8px; background: {CONTROL}; '
        f'display: flex; align-items: center; justify-content: center; margin: 0 auto;">'
        f'{file_glyph(ext, tint)}</div>'
        f'<div style="margin-top: 4px; font-size: 10px; line-height: 1.25; color: {TEXT_2}; '
        f'text-align: center; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">'
        f'{name}</div></div>'
    )


def pill(inner, extra=""):
    return (
        f'<div style="border-radius: 13px; background: {CONTROL}; padding: 5px 12px; '
        f'display: inline-flex; align-items: center; color: {TEXT}; font-size: 11px; {extra}">'
        f'{inner}</div>'
    )


def action_tile(name, label, danger=False):
    tint = DANGER if danger else TEXT
    edge = "#EF444440" if danger else CTRL_EDGE
    return (
        f'<div style="border-radius: 12px; background: {CONTROL}; border: 1px solid {edge}; '
        f'display: flex; flex-direction: column; align-items: center; justify-content: center; '
        f'gap: 7px; padding: 12px 4px;">'
        f'{icon(name, 26, tint)}'
        f'<div style="font-size: 11px; color: {tint};">{label}</div></div>'
    )


def card(mode="filled"):
    """The shelf card at 1:1 with the app: 316x376, radius 14, padding 14x12, 1px #40FFFFFF edge."""
    if mode == "actions":
        body = (
            f'<div style="position: absolute; left: 14px; right: 14px; top: 46px; bottom: 12px; '
            f'border-radius: 12px; background: {SCRIM}; border: 1px solid {CTRL_EDGE}; '
            f'display: flex; flex-direction: column; padding: 12px 8px;">'
            f'<div style="text-align: center; color: {TEXT}; font-size: 12px; font-weight: 500;">'
            f'Hành động</div>'
            f'<div style="text-align: center; color: {TEXT_3}; font-size: 10px; margin-top: 3px;">'
            f'Thả vào một ô để chạy</div>'
            f'<div style="flex: 1; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); '
            f'grid-template-rows: repeat(2, minmax(0, 1fr)); gap: 8px; margin-top: 12px;">'
            f'{action_tile("move", "Chuyển")}{action_tile("copy", "Sao chép")}'
            f'{action_tile("zip", "Nén ZIP")}{action_tile("trash_x", "Xoá", danger=True)}'
            f'</div></div>'
        )
    elif mode == "empty":
        body = (
            f'<div style="flex: 1; display: flex; flex-direction: column; align-items: center; '
            f'justify-content: center; gap: 4px;">'
            f'<div style="width: 56px; height: 56px; border-radius: 14px; border: 1.5px dashed '
            f'{CTRL_EDGE}; display: flex; align-items: center; justify-content: center; '
            f'margin-bottom: 6px;">{icon("bolt", 24, TEXT_3)}</div>'
            f'<div style="color: {TEXT}; font-size: 13px;">Thả tệp vào đây</div>'
            f'<div style="color: {TEXT_3}; font-size: 11px;">từ bất kỳ thư mục hay ứng dụng nào</div>'
            f'</div>'
        )
    else:
        tiles = "".join(thumb_item(n, e, t) for n, e, t in FILES)
        body = (
            f'<div style="flex: 1; display: flex; flex-wrap: wrap; align-content: flex-start; '
            f'justify-content: center; padding-top: 2px;">{tiles}</div>'
        )

    footer = ""
    if mode != "actions":
        count = f'{len(FILES)} mục' if mode == "filled" else "0 mục"
        footer = (
            f'<div style="display: flex; justify-content: center; margin-top: 8px;">'
            + pill(
                f'<span>{count}</span>'
                f'<span style="display: inline-flex; margin-left: 6px;">'
                f'{icon("chevron", 14, TEXT_3, 2)}</span>'
            )
            + '</div>'
            f'<div style="display: flex; justify-content: center; margin-top: 10px;">'
            + pill(
                f'<span style="display: inline-flex; margin-right: 7px;">'
                f'{icon("bolt", 14, TEXT_2)}</span><span>Hành động</span>'
            )
            + '</div>'
        )

    return (
        f'<div style="position: relative; width: 316px; height: 376px;">'
        # the blurred desktop the glass samples — a stand-in for the real captured backdrop
        f'<div style="position: absolute; inset: -18px; border-radius: 30px; '
        f'background: radial-gradient(120px 120px at 30% 25%, {ACCENT}5C, transparent 70%), '
        f'radial-gradient(160px 160px at 75% 80%, #7C3AED4D, transparent 70%); '
        f'filter: blur(30px);"></div>'
        f'<div style="position: absolute; inset: 0; border-radius: 14px; background: {GLASS}; '
        f'box-shadow: 0 28px 70px rgba(0,0,0,0.6), 0 4px 14px rgba(0,0,0,0.4);"></div>'
        f'<div style="position: absolute; inset: 0; border-radius: 14px; '
        f'background: linear-gradient(160deg, #FFFFFF1A, #FFFFFF00 42%);"></div>'
        f'<div style="position: absolute; inset: 0; border-radius: 14px; '
        f'border: 1px solid {GLASS_EDGE}; padding: 12px 14px; box-sizing: border-box; '
        f'display: flex; flex-direction: column;">'
        # header: close circle, grab bar, trash circle — all 28px circles, bar 34x4
        f'<div style="position: relative; height: 28px; display: flex; align-items: center; '
        f'justify-content: space-between; margin-bottom: 4px;">'
        f'<div style="width: 28px; height: 28px; border-radius: 14px; background: {CONTROL}; '
        f'display: flex; align-items: center; justify-content: center;">'
        f'{icon("close", 15, TEXT_2)}</div>'
        f'<div style="width: 34px; height: 4px; border-radius: 2px; background: {THUMB_BAR};"></div>'
        f'<div style="width: 28px; height: 28px; border-radius: 14px; background: {CONTROL}; '
        f'display: flex; align-items: center; justify-content: center;">'
        f'{icon("trash", 15, DANGER)}</div></div>'
        f'{body}{footer}</div></div>'
    )


def scaled_card(scale, mode="filled", extra=""):
    w, h = round(316 * scale), round(376 * scale)
    return (
        f'<div style="width: {w}px; height: {h}px; {extra}">'
        f'<div style="width: 316px; height: 376px; transform: scale({scale}); '
        f'transform-origin: 0 0;">{card(mode)}</div></div>'
    )


def cursor(size=34, rot=0):
    """A plain arrow pointer, so the shake motif reads without a screenshot."""
    return (
        f'<svg width="{size}" height="{size}" viewBox="0 0 24 24" aria-hidden="true" '
        f'style="transform: rotate({rot}deg); filter: drop-shadow(0 3px 6px rgba(0,0,0,0.7));">'
        f'<path d="M 5,2 L 5,19 L 9.5,15 L 12.5,21.5 L 15.5,20 L 12.5,13.8 L 18.5,13.5 Z" '
        f'fill="#FFFFFF" stroke="#0B0B0E" stroke-width="1.2" stroke-linejoin="round" /></svg>'
    )


def shake_cursor(size=36, ghosts=3):
    """The shake, drawn as the pointer's own afterimages.

    An abstract zigzag beside the card reads as a stray line — it has to be explained.
    Three fading copies of the pointer itself read as "this just shook" with nothing to
    explain, and they sit against the card instead of floating away from it.
    """
    lead = 34 * ghosts
    parts = []
    drops = [6, -5, 4, -3]
    for i in range(ghosts, 0, -1):
        op = {1: 0.42, 2: 0.22, 3: 0.11, 4: 0.06}.get(i, 0.08)
        parts.append(
            f'<div style="position: absolute; left: {lead - 34 * i}px; '
            f'top: {24 + drops[(i - 1) % len(drops)]}px; opacity: {op};">'
            f'{cursor(size)}</div>'
        )
    # two short arcs, tight to the pointer, reading as motion rather than decoration
    parts.append(
        f'<svg style="position: absolute; left: {lead - 46}px; top: 0;" width="{size + 52}" '
        f'height="22" viewBox="0 0 92 22" fill="none" aria-hidden="true">'
        f'<path d="M 8,15 C 26,3 46,3 64,14" stroke="{ACCENT}" stroke-width="3" '
        f'stroke-linecap="round" opacity="0.7" />'
        f'<path d="M 26,20 C 38,12 52,12 62,19" stroke="{ACCENT}" stroke-width="2.5" '
        f'stroke-linecap="round" opacity="0.32" /></svg>'
    )
    parts.append(
        f'<div style="position: absolute; left: {lead}px; top: 24px;">{cursor(size)}</div>'
    )
    return (
        f'<div style="position: relative; width: {lead + size}px; height: {size + 38}px;">'
        + "".join(parts) + '</div>'
    )


def page(title, root_style, body, helmet_extra=""):
    """Wrap authored markup as a Design Component artboard."""
    return f"""<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  {FONTS}
  <script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
  <style>
    body {{ margin: 0; font-family: {SANS}; -webkit-font-smoothing: antialiased; }}
    a {{ color: {ACCENT}; text-decoration: none; }}
    a:hover {{ color: {ACCENT_HOV}; }}
    * {{ box-sizing: border-box; }}
{helmet_extra}  </style>
</helmet>
<div style="{root_style}">
{body}
</div>
</x-dc>
</body>
</html>
"""


def grain(opacity=0.05):
    """A little film grain so the flat dark ground has some tooth."""
    return (
        f'<svg style="position: absolute; inset: 0; width: 100%; height: 100%; '
        f'opacity: {opacity}; pointer-events: none; mix-blend-mode: overlay;" aria-hidden="true">'
        f'<filter id="gr"><feTurbulence type="fractalNoise" baseFrequency="0.8" '
        f'numOctaves="3" /></filter><rect width="100%" height="100%" filter="url(#gr)" /></svg>'
    )


def bg(w, h, glow="55% 40%"):
    """Ad ground: near-black with one soft accent bloom. No rainbow gradients."""
    return (
        f'<div style="position: absolute; inset: 0; background: '
        f'radial-gradient(80% 70% at {glow}, {INK_2} 0%, {INK} 62%);"></div>'
        + grain()
    )
