"""Bakes the game's own vital bar textures into the pictures the hunger and thirst bars show (Content/web/art).

The game exports them as material masks, not finished pictures: T_Vital_Bar_BW keeps the bar's body shading in green,
its highlight in red, a glow around it in blue and its shape in alpha, and T_Vital_EndShine the light on the moving end
of a bar (a halo in red, a streak in green, on black). The game colours them per vital at run time. This does that once
per meter, so the page only stacks plain pictures. The plate (T_Vital_Background_D) and the frame (T_Vital_Border_D)
are copied as they are.

    python tools/bake_vitals.py "<export>/Art/UI/Modern/HUD/Vitals"

Run it again after changing a colour below; the pictures it writes are committed with the mod.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'Content', 'web', 'art')

# The meter colours: amber food, blue drink, each a darker body and a lighter highlight.
METERS = {
    'food': {'body': (196, 128, 38), 'light': (255, 214, 140)},
    'water': {'body': (44, 116, 190), 'light': (170, 216, 255)},
}

# The trail shows where the meter just was: a pale, washed-out version of the same bar.
TRAIL_WASH = 0.6


def load(folder, name):
    return np.asarray(Image.open(os.path.join(folder, name)).convert('RGBA')).astype(np.float32) / 255.0


def save(pixels, name):
    rgba = (np.clip(pixels, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)
    Image.fromarray(rgba, 'RGBA').save(os.path.join(OUT, name), optimize=True)


def tint_bar(mask, body, light, wash=0.0):
    shade, highlight, shape = mask[..., 1], mask[..., 0], mask[..., 3]
    body = np.array(body, np.float32) / 255.0
    light = np.array(light, np.float32) / 255.0
    body = body + (1.0 - body) * wash
    light = light + (1.0 - light) * wash
    # The shading carries the body colour from dark to full; the highlight lifts it towards the light colour.
    rgb = body[None, None, :] * (0.45 + 0.75 * shade[..., None])
    rgb = rgb + (light[None, None, :] - rgb) * np.clip(highlight[..., None] * 1.3, 0.0, 1.0) * 0.6
    # The blue glow around the shape is left out: a fill cut at its value would cut the glow square too.
    return np.dstack([rgb, shape])


def tint_shine(mask, bar, light):
    # Squeezed to the bar's height (64x64 to 64x32), then kept inside the bar's shape: the game masks it by the bar,
    # and a page stacking pictures can't, so the shape's top-to-bottom profile is baked in.
    height = bar.shape[0]
    squeezed = np.asarray(Image.fromarray((mask * 255).astype(np.uint8), 'RGBA').resize(
        (mask.shape[1], height), Image.LANCZOS)).astype(np.float32) / 255.0
    profile = bar[:, bar.shape[1] // 2, 3][:, None]
    halo, streak = squeezed[..., 0] * profile, squeezed[..., 1] * profile
    light = np.array(light, np.float32) / 255.0
    rgb = light[None, None, :] * halo[..., None] + streak[..., None]
    alpha = np.clip(np.maximum(halo * 0.85, streak), 0.0, 1.0)
    rgb = rgb / np.maximum(alpha[..., None], 1e-4)
    return np.dstack([np.clip(rgb, 0.0, 1.0), alpha])


def main(folder):
    os.makedirs(OUT, exist_ok=True)
    shutil.copyfile(os.path.join(folder, 'T_Vital_Background_D.png'), os.path.join(OUT, 'plate.png'))
    shutil.copyfile(os.path.join(folder, 'T_Vital_Border_D.png'), os.path.join(OUT, 'frame.png'))
    bar = load(folder, 'T_Vital_Bar_BW.png')
    shine = load(folder, 'T_Vital_EndShine.png')
    for meter, colours in METERS.items():
        save(tint_bar(bar, colours['body'], colours['light']), f'fill-{meter}.png')
        save(tint_bar(bar, colours['body'], colours['light'], wash=TRAIL_WASH), f'trail-{meter}.png')
        save(tint_shine(shine, bar, colours['light']), f'shine-{meter}.png')
    print('baked into', os.path.normpath(OUT))


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
