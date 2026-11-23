"""Bakes the game's own vital bar textures into the pictures the hunger and thirst bars show (Content/web/art).

The game exports them as material masks, not finished pictures: T_Vital_Bar_BW keeps the bar's round shading in green,
its light in red (bright above the middle line, dark below it), a glow around it in blue and its shape in alpha, and T_Vital_EndShine the light on the moving end
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

# The Jobs mod's panel shows the work under way (a tree, a vein) on the same bar: its pictures go to that mod, the bar
# uncoloured, as the page colours it by how far the work is (red to yellow to green), the shine a warm white.
JOBS_OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Jobs', 'Content', 'web', 'art')
WORK = {'body': (255, 255, 255), 'light': (255, 246, 230)}

# The meter colours: amber food, blue drink, each as bright as the bar gets (its middle line) and a lighter colour for the
# shine on its end.
METERS = {
    'food': {'body': (236, 166, 64), 'light': (255, 214, 140)},
    'water': {'body': (92, 168, 240), 'light': (170, 216, 255)},
}

# The game lights a vital by the mask's red channel, measured against its own bars on screen: brightest at the middle
# line (red 117 of 255), the upper half a little below it, the lower half falling to about 0.43 of it towards the
# bottom. The green channel's round shading hardly shows. Below the floor no part goes.
HIGHLIGHT_PEAK = 117 / 255
DARK_FLOOR = 0.17

# The trail shows where the meter just was: a pale, washed-out version of the same bar.
TRAIL_WASH = 0.6


def load(folder, name):
    return np.asarray(Image.open(os.path.join(folder, name)).convert('RGBA')).astype(np.float32) / 255.0


def save(pixels, name, folder=OUT):
    rgba = (np.clip(pixels, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)
    Image.fromarray(rgba, 'RGBA').save(os.path.join(folder, name), optimize=True)


def tint_bar(mask, body, wash=0.0):
    highlight, shape = mask[..., 0], mask[..., 3]
    body = np.array(body, np.float32) / 255.0
    body = body + (1.0 - body) * wash
    lit = DARK_FLOOR + (1.0 - DARK_FLOOR) * np.clip(highlight / HIGHLIGHT_PEAK, 0.0, 1.0)
    rgb = body[None, None, :] * lit[..., None]
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
        save(tint_bar(bar, colours['body']), f'fill-{meter}.png')
        save(tint_bar(bar, colours['body'], wash=TRAIL_WASH), f'trail-{meter}.png')
        save(tint_shine(shine, bar, colours['light']), f'shine-{meter}.png')
    print('baked into', os.path.normpath(OUT))

    os.makedirs(JOBS_OUT, exist_ok=True)
    shutil.copyfile(os.path.join(folder, 'T_Vital_Background_D.png'), os.path.join(JOBS_OUT, 'vital-plate.png'))
    shutil.copyfile(os.path.join(folder, 'T_Vital_Border_D.png'), os.path.join(JOBS_OUT, 'vital-frame.png'))
    save(tint_bar(bar, WORK['body']), 'vital-fill.png', JOBS_OUT)
    save(tint_shine(shine, bar, WORK['light']), 'vital-shine.png', JOBS_OUT)
    print('baked into', os.path.normpath(JOBS_OUT))


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
