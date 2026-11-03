# A Recraft cut-out for the pages: cropped to what is drawn, scaled so its longer side is SIZE px, saved as webp.
# usage: python -I to_ui.py SOURCE OUT SIZE
import sys

from PIL import Image

QUALITY = 88


def main():
    if len(sys.argv) != 4:
        raise SystemExit("usage: python -I to_ui.py SOURCE OUT SIZE")
    source, out, size = sys.argv[1], sys.argv[2], int(sys.argv[3])
    image = Image.open(source).convert("RGBA")
    image = image.crop(image.getchannel("A").getbbox())
    scale = size / max(image.size)
    image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    image.save(out, "WEBP", quality=QUALITY, method=6)
    print(f"{out}: {image.width}x{image.height}")


if __name__ == "__main__":
    main()
