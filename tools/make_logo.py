"""IPZ logosunu çizer: Resources/ipz.ico (program simgesi) ve Resources/ipz-logo.png (başlık).

Kullanım: python3 tools/make_logo.py   (Pillow gerekir)
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "src", "IPScanner", "Resources")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
TOP = (37, 99, 235)      # mavi
BOTTOM = (6, 182, 212)   # camgöbeği


def gradient(size):
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2 * (size - 1))
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM))
    return img


def draw(size, with_text):
    S = 1024  # büyük çiz, sonra küçült: kenarlar yumuşak olsun
    base = gradient(256).resize((S, S), Image.BICUBIC).convert("RGBA")
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=255)
    logo = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    logo.paste(base, (0, 0), mask)

    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    white = (255, 255, 255, 255)

    if with_text:
        # Sağ üstte tarama dalgaları, solda altta "IPZ".
        cx, cy = int(S * 0.70), int(S * 0.40)
        r_dot = int(S * 0.045)
        widths = int(S * 0.045)
        for i, r in enumerate((0.11, 0.19, 0.27)):
            rr = int(S * r)
            alpha = (255, 200, 140)[i]
            d.arc([cx - rr, cy - rr, cx + rr, cy + rr], start=200, end=340, fill=(255, 255, 255, alpha), width=widths)
        d.ellipse([cx - r_dot, cy - r_dot, cx + r_dot, cy + r_dot], fill=white)

        font = ImageFont.truetype(FONT, int(S * 0.38))
        text = "IPZ"
        box = d.textbbox((0, 0), text, font=font)
        tw, th = box[2] - box[0], box[3] - box[1]
        tx = (S - tw) // 2 - box[0]
        ty = int(S * 0.88) - th - box[1]
        shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).text((tx, ty + int(S * 0.012)), text, font=font, fill=(10, 30, 80, 110))
        layer = Image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(S * 0.012)), layer)
        d = ImageDraw.Draw(layer)
        d.text((tx, ty), text, font=font, fill=white)
    else:
        # Küçük boylarda yazı okunmaz: yalnızca ortada tarama dalgaları.
        cx, cy = S // 2, int(S * 0.72)
        r_dot = int(S * 0.09)
        for i, r in enumerate((0.25, 0.45)):
            rr = int(S * r)
            d.arc([cx - rr, cy - rr, cx + rr, cy + rr], start=215, end=325, fill=white, width=int(S * 0.11))
        d.ellipse([cx - r_dot, cy - r_dot, cx + r_dot, cy + r_dot], fill=white)

    logo = Image.alpha_composite(logo, layer)
    return logo.resize((size, size), Image.LANCZOS)


def main():
    os.makedirs(ROOT, exist_ok=True)
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    frames = [draw(s, with_text=s >= 40) for s in sizes]
    frames[-1].save(os.path.join(ROOT, "ipz.ico"), format="ICO", sizes=[(s, s) for s in sizes],
                    append_images=frames[:-1])
    draw(128, True).save(os.path.join(ROOT, "ipz-logo.png"))
    draw(512, True).save(os.path.join(os.path.dirname(__file__), "..", "docs", "ipz-logo.png"))


if __name__ == "__main__":
    main()
