# A Recraft picture for a page that shows it in a circle fading out (a guide's card): scaled to SIZE px square, and what
# lies outside the circle made one flat colour, which the page never shows and the webp then stores for almost nothing.
# usage: python -I to_round.py SOURCE OUT SIZE [QUALITY]
import sys

from PIL import Image, ImageDraw, ImageFilter

QUALITY = 66
# The circle's edge, as a share of the side: a hair inside it, softened so no ring shows where the flat colour begins.
EDGE = 0.01
USAGE = "usage: python -I to_round.py SOURCE OUT SIZE [QUALITY]"


def main():
    if len(sys.argv) not in (4, 5):
        raise SystemExit(USAGE)
    source, out, size = sys.argv[1], sys.argv[2], int(sys.argv[3])
    quality = int(sys.argv[4]) if len(sys.argv) == 5 else QUALITY
    image = Image.open(source).convert("RGB").resize((size, size), Image.LANCZOS)
    margin = round(size * EDGE)
    circle = Image.new("L", (size, size), 0)
    ImageDraw.Draw(circle).ellipse((margin, margin, size - margin, size - margin), fill=255)
    circle = circle.filter(ImageFilter.GaussianBlur(size * EDGE))
    ground = Image.new("RGB", (size, size), image.resize((1, 1)).getpixel((0, 0)))
    Image.composite(image, ground, circle).save(out, "WEBP", quality=quality, method=6)
    print(f"{out}: {size}x{size}")


if __name__ == "__main__":
    main()
