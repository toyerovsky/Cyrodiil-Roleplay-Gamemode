"""Bakes the game's own UI textures into the pictures the pages' design system draws with (ui/rp/art/game).

The pages take the remaster's own pieces: the menu paper, its paper buttons and filter chips, the list's highlight, the
ornament under a title, the HUD's dark box and the boss bar's diamond tab. Some come as the game ships them, some as
masks the game colours at run time (the separator, the feather), which this colours once. Each is cut and sized for
1080p, where a page's px are, and saved as WebP to keep the pages under check-dist's budget.

    python -I scripts/bake-game-art.py "<export>/Art/UI"

<export> is a retoc export of the game's paks. The pictures it writes are committed with the pages, and the
notification's pieces with the Jobs mod's panel: run it again only after changing a cut or a colour below.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'rp', 'art', 'game')

# The Jobs mod's progress panel is a notification too, a plain page shipped with that mod: it takes the same pieces.
JOBS_OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'mods', 'Jobs', 'Content', 'web', 'art')
JOBS_PIECES = ('plate.webp', 'plate-crown.webp', 'plate-foot.webp', 'notice-tab.webp')

INK = (43, 29, 14)
GOLD = (216, 180, 107)
ENTRY_TINT = (236, 222, 196)

# The HUD's box is black at 60% over the scene, too thin for 4.5:1 under white text in daylight. The boss bar's 80%,
# which the notifications take from, holds it.
PLATE_FILL_ALPHA = 204

WEBP_QUALITY = 84

# Where the clip lies on T_UI_PlayerMenu_Background (1024 square), and a stretch of wooden band beside it to paint over it.
CLIP_SPAN = (140, 900)
CLIP_BOTTOM = 80
BAND_TILE = (30, 140)

# One tab of T_Tab_1 (lowered) and of T_Tab_3 (raised, with the paper under it), both 1024 x 256.
TAB_DOWN = (433, 0, 604, 97)
TAB_UP = (432, 0, 600, 112)


def load(root, relative):
    return Image.open(os.path.join(root, relative)).convert('RGBA')


def save(image, name, lossless=False):
    image.save(os.path.join(OUT, name), 'WEBP', quality=WEBP_QUALITY, lossless=lossless, method=6)


def pixels(image):
    return np.asarray(image).astype(np.float32)


def picture(values):
    return Image.fromarray(np.clip(values + 0.5, 0, 255).astype(np.uint8), 'RGBA')


def tint(image, colour, strength):
    """Multiplies the picture's colour towards `colour`, its shading kept."""
    values = pixels(image)
    multiplied = values[..., :3] * np.array(colour, np.float32) / 255.0
    values[..., :3] = values[..., :3] * (1.0 - strength) + multiplied * strength
    return picture(values)


def dim(image, darken, keep_colour):
    """A disabled look of its own: greyer and darker, not faded (the pages never dim with opacity)."""
    values = pixels(image)
    grey = values[..., :3].mean(axis=2, keepdims=True)
    values[..., :3] = (values[..., :3] * keep_colour + grey * (1.0 - keep_colour)) * darken
    return picture(values)


def colour_mask(image, colour):
    """A white mask drawn in one colour, its alpha kept."""
    values = pixels(image)
    values[..., :3] = np.array(colour, np.float32)
    return picture(values)


def ornament_only(image):
    """Keeps the lit lines of a HUD piece and drops its black fill, so it can lie over a box's edge."""
    values = pixels(image)
    lit = values[..., :3].max(axis=2) > 24
    values[..., 3] = np.where(lit, values[..., 3], 0)
    return picture(values)


def thicken_fill(image):
    """Raises the black fill of a HUD piece to PLATE_FILL_ALPHA, its lines as they are."""
    values = pixels(image)
    fill = (values[..., :3].max(axis=2) <= 24) & (values[..., 3] > 100)
    values[..., 3] = np.where(fill, np.maximum(values[..., 3], PLATE_FILL_ALPHA), values[..., 3])
    return picture(values)


def lift_clip(board):
    """Paints the board's wooden band over where the clip lies, from the band beside it, tile by tile."""
    values = np.asarray(board).copy()
    left, right = CLIP_SPAN
    tile = values[:CLIP_BOTTOM, BAND_TILE[0]:BAND_TILE[1]]
    width = BAND_TILE[1] - BAND_TILE[0]
    for x in range(left, right, width):
        end = min(x + width, right)
        values[:CLIP_BOTTOM, x:end] = tile[:, :end - x]
    return Image.fromarray(values, 'RGBA')


def bake(root):
    os.makedirs(OUT, exist_ok=True)

    # Paper: the menus' page, its ragged edge drawn into it.
    save(load(root, 'Common/Backgrounds/T_UI_MenuRight.png').resize((512, 512), Image.LANCZOS), 'paper.webp')

    # A merchant's board: the player menu's wooden board with its paper, the brass clip lifted off so the board can
    # stretch under it, and the clip on its own to lie over the board's top at its own size.
    board = load(root, 'Common/Backgrounds/T_UI_PlayerMenu_Background.png')
    save(lift_clip(board).resize((640, 640), Image.LANCZOS), 'board.webp')
    save(load(root, 'Common/Backgrounds/T_UI_Clip.png').resize((512, 64), Image.LANCZOS), 'clip.webp')

    # Tabs: one lowered and one raised tab cut from the game's strips of five, so a row holds as many as a page needs.
    # The raised one keeps the paper it opens into.
    save(load(root, 'Common/Tabs/T_Tab_1.png').crop(TAB_DOWN), 'tab.webp')
    save(load(root, 'Common/Tabs/T_Tab_3.png').crop(TAB_UP), 'tab-on.webp')

    # Buttons: the paper button, a gold one for the main action, a grey one for a disabled button.
    button = load(root, 'Common/Buttons/T_UI_ButtonBcg1.png').resize((256, 128), Image.LANCZOS)
    save(button, 'button.webp')
    save(tint(button, GOLD, 0.85), 'button-gold.webp')
    save(dim(button, 0.86, 0.35), 'button-off.webp')

    # Chips: the inventory's filters, light at rest, the plain one under the pointer, the dark one picked.
    for name, source in (('chip', 'T_Filters_Light'), ('chip-hover', 'T_Filters'), ('chip-on', 'T_Filters_Dark')):
        save(load(root, f'Common/Buttons/{source}.png').resize((256, 64), Image.LANCZOS), f'{name}.webp')

    # A list's row picked, a little warmer and darker than the game's near-white, so a tall card is not a white block.
    entry = load(root, 'Common/Backgrounds/T_UI_EntrySelection.png').resize((512, 64), Image.LANCZOS)
    save(tint(entry, ENTRY_TINT, 0.55), 'entry.webp')

    # The ornament under a title, a white mask drawn in ink.
    save(colour_mask(load(root, 'Common/T_InventorySeparator.png'), INK), 'separator.webp', lossless=True)

    # The dark plate over the game: the HUD box's left and right halves, its crown and foot from the middle piece.
    left = load(root, 'Modern/HUD/T_UI_Box_Left.png')
    right = load(root, 'Modern/HUD/T_UI_Box_Right.png')
    plate = Image.new('RGBA', (64, 128))
    plate.paste(left.crop((0, 0, 32, 128)), (0, 0))
    plate.paste(right.crop((32, 0, 64, 128)), (32, 0))
    save(thicken_fill(plate), 'plate.webp', lossless=True)
    middle = ornament_only(load(root, 'Modern/HUD/T_UI_Box_Middle.png'))
    save(middle.crop((16, 0, 48, 10)), 'plate-crown.webp', lossless=True)
    save(middle.crop((16, 116, 48, 128)), 'plate-foot.webp', lossless=True)

    # A notification's tab: the boss bar's end with its diamond.
    tab = thicken_fill(load(root, 'Modern/HUD/T_UI_BossBcg_Side.png').crop((0, 0, 40, 64)))
    save(tab, 'notice-tab.webp', lossless=True)

    # The quill beside a field being written in, drawn in ink.
    feather = load(root, 'Common/T_UI_EditableText_FeatherIndicator.png').resize((32, 32), Image.LANCZOS)
    save(colour_mask(feather, INK), 'feather.webp', lossless=True)

    # The arrows either side of a button under the pointer, as the game's tabs mark the one picked.
    arrows = load(root, 'Common/T_Tab_arrow_on.png')
    half = arrows.width // 2
    for name, side in (('arrow.webp', (0, 0, half, arrows.height)), ('arrow-right.webp', (half, 0, arrows.width, arrows.height))):
        piece = arrows.crop(side)
        save(piece.crop(piece.getchannel('A').point(lambda alpha: 255 if alpha > 8 else 0).getbbox()), name, lossless=True)

    os.makedirs(JOBS_OUT, exist_ok=True)
    for name in JOBS_PIECES:
        shutil.copyfile(os.path.join(OUT, name), os.path.join(JOBS_OUT, name))


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit('usage: python -I scripts/bake-game-art.py "<export>/Art/UI"')
    bake(sys.argv[1])
