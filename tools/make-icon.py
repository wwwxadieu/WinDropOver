#!/usr/bin/env python3
"""
Renders the WinClipboard mark to assets/WinClipboard.ico.

Written by hand because this container has no image library at all — no Pillow, no ImageMagick,
no rsvg — and an application shipping without an icon looks unfinished in the taskbar, the tray,
the Start menu and Apps & features all at once.

The mark is defined here as geometry, not as pixels, and the same shapes are mirrored in
Styles/AppIcon.xaml so the in-app vector and the file icon cannot drift apart.

Shape: a rounded card (the shelf) with two sheets resting on it, the upper one offset — the
smallest arrangement that still reads as "things collected together" at 16px.
"""
import struct, zlib

ACCENT      = (0x3B, 0x82, 0xF6, 255)   # the app's accent blue
SHEET_FRONT = (0xFF, 0xFF, 0xFF, 255)
SHEET_MID   = (0xFF, 0xFF, 0xFF, 205)
SHEET_BACK  = (0xFF, 0xFF, 0xFF, 150)   # fading back, so depth reads without any outline

SIZES = [16, 24, 32, 48, 64, 128, 256]
SS = 4                                   # supersampling factor, for antialiasing


def triangle(ax, ay, bx, by, cx, cy):
    """Coverage test for a triangle, by sign of the cross product against each edge."""
    def side(px, py, x1, y1, x2, y2):
        return (x2 - x1) * (py - y1) - (y2 - y1) * (px - x1)

    def inside(px, py):
        d1 = side(px, py, ax, ay, bx, by)
        d2 = side(px, py, bx, by, cx, cy)
        d3 = side(px, py, cx, cy, ax, ay)
        return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))
    return inside


def rounded_rect(x, y, w, h, r):
    """Coverage test for a rounded rectangle, in a 0..1 coordinate space."""
    def inside(px, py):
        if not (x <= px <= x + w and y <= py <= y + h):
            return False
        # Only the four corner boxes need the radius test.
        cx = x + r if px < x + r else (x + w - r if px > x + w - r else px)
        cy = y + r if py < y + r else (y + h - r if py > y + h - r else py)
        return (px - cx) ** 2 + (py - cy) ** 2 <= r * r
    return inside


# Geometry in a unit square. Kept deliberately chunky: at 16px a hairline disappears entirely.
#
# An arrow dropping into a tray, because that is literally what the application does, and it is
# the one arrangement that survives being 16 pixels wide. Earlier attempts drew a stack of
# sheets, which at icon size read as a pyramid of bowls rather than as anything being collected.
SHAPES = [
    (rounded_rect(0.06, 0.06, 0.88, 0.88, 0.22), ACCENT),
    (rounded_rect(0.435, 0.185, 0.13, 0.28, 0.055), SHEET_FRONT),          # shaft
    (triangle(0.315, 0.42, 0.685, 0.42, 0.50, 0.655), SHEET_FRONT),        # head
    (rounded_rect(0.205, 0.715, 0.59, 0.135, 0.06), SHEET_MID),            # tray
]


def render(size):
    """Returns RGBA bytes, compositing the shapes back to front with supersampled coverage."""
    pixels = bytearray(size * size * 4)
    step = 1.0 / (size * SS)
    for py in range(size):
        for px in range(size):
            r = g = b = a = 0.0
            for shape, colour in SHAPES:
                hits = 0
                for sy in range(SS):
                    for sx in range(SS):
                        u = (px * SS + sx + 0.5) * step
                        v = (py * SS + sy + 0.5) * step
                        if shape(u, v):
                            hits += 1
                if hits == 0:
                    continue
                # Source-over: coverage times the shape's own alpha.
                sa = (hits / (SS * SS)) * (colour[3] / 255.0)
                r = colour[0] * sa + r * (1 - sa)
                g = colour[1] * sa + g * (1 - sa)
                b = colour[2] * sa + b * (1 - sa)
                a = sa + a * (1 - sa)
            i = (py * size + px) * 4
            pixels[i:i + 4] = bytes((round(r), round(g), round(b), round(a * 255)))
    return bytes(pixels)


def png(size, rgba):
    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    raw = b"".join(b"\x00" + rgba[y * size * 4:(y + 1) * size * 4] for y in range(size))
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def main():
    # PNG-compressed entries throughout: Windows has read those since Vista, and this app
    # requires Windows 10 anyway, so the older uncompressed DIB format buys nothing.
    images = [png(s, render(s)) for s in SIZES]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, blobs = b"", b""
    for size, data in zip(SIZES, images):
        entries += struct.pack("<BBBBHHII",
                               size if size < 256 else 0, size if size < 256 else 0,
                               0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)

    with open("assets/WinClipboard.ico", "wb") as f:
        f.write(header + entries + blobs)
    print(f"assets/WinClipboard.ico: {len(SIZES)} kích thước, {len(header + entries + blobs)} bytes")


if __name__ == "__main__":
    main()
