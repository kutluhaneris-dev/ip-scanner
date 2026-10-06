"""IPZ logosundan program simgesini ve başlık görselini üretir.

Kaynak: tools/ipz-logo-source.png (şeffaf zeminli beyaz IPZ logosu, ipzproje.com.tr).
Çıktılar: src/IPScanner/Resources/ipz.ico, src/IPScanner/Resources/ipz-logo-white.png, docs/ipz-logo.png
Kullanım: python3 tools/make_logo.py   (Pillow gerekir)
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(__file__)
RES = os.path.join(HERE, "..", "src", "IPScanner", "Resources")
DOCS = os.path.join(HERE, "..", "docs")
NAVY = (20, 33, 61, 255)  # Theme.Navy ile aynı


def load():
    src = Image.open(os.path.join(HERE, "ipz-logo-source.png")).convert("RGBA")
    full = src.crop(src.getchannel("A").getbbox())
    # Yalnızca "IPZ" harfleri: alt satırdaki yazıdan önceki boşluğu bul.
    alpha = full.getchannel("A")
    w, h = full.size
    rows = [any(alpha.getpixel((x, y)) > 40 for x in range(0, w, 2)) for y in range(h)]
    y = int(h * 0.5)
    while y < h and rows[y]:
        y += 1
    letters = full.crop((0, 0, w, y))
    letters = letters.crop(letters.getchannel("A").getbbox())
    return full, letters


def icon(letters, size):
    S = 512
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.2), fill=NAVY)
    margin = 0.14 if size >= 32 else 0.08
    box = int(S * (1 - 2 * margin))
    lw, lh = letters.size
    scale = box / lw
    mark = letters.resize((box, int(lh * scale)), Image.LANCZOS)
    img.alpha_composite(mark, ((S - mark.width) // 2, (S - mark.height) // 2))
    return img.resize((size, size), Image.LANCZOS)


def main():
    full, letters = load()
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    frames = [icon(letters, s) for s in sizes]
    frames[-1].save(os.path.join(RES, "ipz.ico"), format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])

    # Başlık bandı için: tam logo (alt yazıyla), 120 px yükseklik.
    h = 120
    full.resize((round(full.width * h / full.height), h), Image.LANCZOS).save(os.path.join(RES, "ipz-logo-white.png"))

    # README için lacivert zeminli kare.
    icon(letters, 256).save(os.path.join(DOCS, "ipz-logo.png"))


if __name__ == "__main__":
    main()
