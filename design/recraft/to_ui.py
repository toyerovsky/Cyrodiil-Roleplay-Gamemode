# A Recraft cut-out for the pages: cropped to what is drawn, scaled so its longer side is SIZE px, saved as webp.
# QUALITY is the colour's and the edges' (alpha) both: a set of small pictures shown together goes lower to fit the
# pages' budget.
# usage: python -I to_ui.py SOURCE OUT SIZE [QUALITY]
import sys

from PIL import Image

QUALITY = 88
# remove_background leaves a haze of nearly clear pixels (alpha 1 to 8) out to the edges, which would keep the crop at
# the whole square.
HAZE_ALPHA = 8
USAGE = "usage: python -I to_ui.py SOURCE OUT SIZE [QUALITY]"


def main():
    if len(sys.argv) not in (4, 5):
        raise SystemExit(USAGE)
    source, out, size = sys.argv[1], sys.argv[2], int(sys.argv[3])
    quality = int(sys.argv[4]) if len(sys.argv) == 5 else QUALITY
    image = Image.open(source).convert("RGBA")
    image.putalpha(image.getchannel("A").point(lambda alpha: 0 if alpha <= HAZE_ALPHA else alpha))
    image = image.crop(image.getchannel("A").getbbox())
    scale = size / max(image.size)
    image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    image.save(out, "WEBP", quality=quality, alpha_quality=quality, method=6)
    print(f"{out}: {image.width}x{image.height}")


if __name__ == "__main__":
    main()
