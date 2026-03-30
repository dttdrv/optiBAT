"""Generate optiBAT app icon: a battery with a lightning bolt."""
from PIL import Image, ImageDraw, ImageFont
import os

SIZES = [16, 24, 32, 48, 64, 128, 256]
OUTPUT = os.path.join(os.path.dirname(__file__), "..", "src", "OptiBat", "Resources", "app.ico")


def draw_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Battery body
    pad = size // 8
    bw = size - 2 * pad  # battery width
    bh = int(bw * 0.55)  # battery height
    top = (size - bh) // 2
    r = max(1, size // 16)  # corner radius

    # Battery terminal (top nub)
    nub_w = bw // 3
    nub_h = max(2, size // 12)
    nub_x = pad + (bw - nub_w) // 2
    draw.rectangle([nub_x, top - nub_h, nub_x + nub_w, top], fill="#2E8B57")

    # Battery outline
    draw.rounded_rectangle(
        [pad, top, pad + bw, top + bh],
        radius=r,
        fill="#1a1a2e",
        outline="#2E8B57",
        width=max(1, size // 16),
    )

    # Fill level (green, ~75%)
    fill_pad = max(2, size // 12)
    fill_w = int((bw - 2 * fill_pad) * 0.75)
    draw.rectangle(
        [
            pad + fill_pad,
            top + fill_pad,
            pad + fill_pad + fill_w,
            top + bh - fill_pad,
        ],
        fill="#2E8B57",
    )

    # Lightning bolt (simple)
    cx = size // 2
    cy = top + bh // 2
    s = max(2, size // 6)
    bolt = [
        (cx - s // 2, cy - s),
        (cx + s // 4, cy - s // 4),
        (cx - s // 4, cy),
        (cx + s // 2, cy + s),
        (cx - s // 4, cy + s // 4),
        (cx + s // 4, cy),
    ]
    draw.polygon(bolt, fill="#FFD700")

    return img


def main():
    images = [draw_icon(s) for s in SIZES]
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    images[0].save(
        OUTPUT, format="ICO", sizes=[(s, s) for s in SIZES], append_images=images[1:]
    )
    print(f"Icon saved to {OUTPUT}")


if __name__ == "__main__":
    main()
