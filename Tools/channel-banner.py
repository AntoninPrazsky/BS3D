"""The YouTube channel banner (#1230): the game's logo framed by an arch of balls, in any scene, as a picture of the game itself.

    python -I Tools/channel-banner.py --scene neon                 # film the neon city at 5120x2880, lay the logo over it
    python -I Tools/channel-banner.py --scene meadow --no-build    # another scene, with the Game web-capture.ps1 already built
    python -I Tools/channel-banner.py --scene neon --frame <png>   # lay the logo over a frame filmed before

Run with a Python that has Pillow - the one web-capture-encode.py uses.

What YouTube shows of a banner decides the whole picture. It asks for 16:9 (2560x1440, at most 6 MB): a TV shows all of it, a
desktop only the middle strip, 2560x423, and a phone the middle 1546x423. So the logo stands in the middle, no taller than the
strip, and whatever else must be seen stands beside it at the same height. No shipped level has a hole there, so the banner hangs
a level of its own: an arch whose legs run down both sides of the logo through the strip, its colours the logo's own rainbow
from left to right. The arch is hung, lit and drawn by the Game (Tools/web-capture.ps1, one still from a stated camera, the
Ultra tier, every distance rule at its finest, no film grain, no HUD); the logo is laid over the frame here.

Writes, in --out (the current folder by default):
  <name>-<frame size>.png   the banner at the size it was filmed
  <name>-2560x1440.jpg      the upload, under YouTube's 6 MB
  <name>-crops.jpg          what each device shows: the TV's picture with both strips outlined, the desktop's strip, the phone's
"""
import argparse
import io
import json
import math
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOGO = os.path.join(ROOT, "Images", "logo", "bs3d-logo-2048.png")
FRAMES_ROOT = os.path.join(os.environ.get("TEMP", "."), "bs3d-web-capture-frames")
LEVELS_ROOT = os.path.join(os.environ.get("TEMP", "."), "bs3d-banner-levels")

# YouTube's banner, in pixels of the 2560x1440 it asks for: the strip a desktop shows and the part of it a phone shows
BANNER = (2560, 1440)
STRIP_HEIGHT = 423
PHONE_WIDTH = 1546
UPLOAD_LIMIT = 5_800_000   # bytes; YouTube refuses a banner over 6 MB

# The logo's height as a share of the picture's: 360 of 1440, which leaves about 30 px of the strip above and below it
LOGO_HEIGHT = 0.25

# The arch, in lattice units (a ball is 1 across, a level 1/sqrt(2) high). Its opening is OPENING_HALF to each side of the axis
# between ball centres (so a ball less in the clear), upright for STRAIGHT levels and closed over CURVE levels by an ellipse,
# under a lintel of LINTEL levels that hangs from the ceiling; each leg is LEG wide and the whole DEPTH cells deep
OPENING_HALF = 5.0
LEG = 4.5
STRAIGHT, CURVE, LINTEL = 8, 3, 3
DEPTH = 3
# Magenta, red, orange, yellow, green, cyan, blue: the order of the logo's own letters, as BallType numbers. Navy is left out:
# at the arch's edge it reads as a hole
RAINBOW = [6, 1, 9, 7, 2, 5, 3]

# The Game hangs the top of every field this shallow at this height (ClusterHang.FIELD_TOP_Y, 8 levels up)
FIELD_TOP_Y = 8 / math.sqrt(2.0)
# The camera: level with the middle of the opening (so the legs stand upright in the picture), on the gun's side of the island,
# as far back as puts PIXELS_PER_UNIT of the 2560 px banner on a lattice unit at the arch through the game's own lens angle.
# 90 makes a ball 90 px, the opening about 780 px in the clear and the arch a little wider than the phone's part of the strip
FOV = 42.9
PIXELS_PER_UNIT = 90.0
# Game time of the frame: the arch has stopped swinging by then (the run has no wind), and four frames let the exposure settle
FROM_SECONDS, FRAMES, FPS = 4.0, 4, 15


def arch_level(scene, sky, balls):
    """The banner's level: the arch as a map of the Game's own format, and the world height of the middle of its opening.

    The Game puts the middle of the FIELD's top level on the axis the camera looks down (ClusterHang.FitWorldOffset), and that
    level is an odd one here (shifted half a cell), so the axis is at sx / 2: an arch centred there is symmetric on both parities."""
    levels = STRAIGHT + CURVE + LINTEL
    outer = OPENING_HALF + LEG
    sx = int(math.ceil(2 * outer)) + 3
    sz = DEPTH + 2
    field_levels = levels + 4   # an even number of empty levels under the layout keeps the lattice's parity
    offset = field_levels - levels
    assert (field_levels - 1) % 2 == 1, "the field's top level must be a shifted one for the axis to be at sx / 2"
    centre = sx / 2.0
    cells = [[[None] * levels for _ in range(sz)] for _ in range(sx)]
    for level in range(levels):
        odd = level % 2
        if level < STRAIGHT:
            opening = OPENING_HALF
        elif level < STRAIGHT + CURVE:
            f = (level - STRAIGHT + 1) / (CURVE + 1.0)
            opening = OPENING_HALF * math.sqrt(max(0.0, 1.0 - f * f))
        else:
            opening = -1.0
        for xi in range(sx):
            x = xi + 0.5 * odd - centre
            if abs(x) > outer or abs(x) < opening:
                continue
            band = int((x + outer) / (2 * outer) * len(RAINBOW))
            colour = RAINBOW[max(0, min(len(RAINBOW) - 1, band))]
            for zi in range(1, 1 + DEPTH):
                cells[xi][zi][level] = {"x": xi + 0.5 * odd, "y": round((level + offset) / math.sqrt(2.0), 6), "z": zi + 0.5 * odd,
                                        "t": colour}
    level_file = {"format": "bs3d-level", "version": 2, "name": "Banner", "author": "BS3D", "sky": sky, "scene": scene,
                  "music": "bloom", "balls": balls, "map": {"sx": sx, "sz": sz, "l": field_levels, "b": cells}}
    os.makedirs(LEVELS_ROOT, exist_ok=True)
    path = os.path.join(LEVELS_ROOT, "Banner-%s-%s.json" % (scene, balls))
    with open(path, "w", encoding="utf-8") as f:
        json.dump(level_file, f)
    middle = FIELD_TOP_Y - (levels - 1 - (STRAIGHT - 1) / 2.0) / math.sqrt(2.0)
    return path, middle


def scene_sky(scene):
    """The sky dome the shipped levels of a scene name: a scene drawn under another chapter's sky is lit by the wrong sun."""
    folder = os.path.join(ROOT, "Game", "Levels")
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".json"):
            continue
        with open(os.path.join(folder, name), encoding="utf-8-sig") as f:
            level = json.load(f)
        if level.get("format") == "bs3d-level" and level.get("scene") == scene:
            return level["sky"]
    sys.exit("no shipped level is set in the scene '%s': name its sky with --sky" % scene)


def film(name, level, middle, size, build):
    """One still of the level through web-capture.ps1, from the banner's camera; returns the last frame's path."""
    distance = BANNER[1] / PIXELS_PER_UNIT / (2 * math.tan(math.radians(FOV) / 2))
    folder = os.path.join(FRAMES_ROOT, "%s-%s" % (name, size))
    command = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", os.path.join(ROOT, "Tools", "web-capture.ps1"),
               "-Name", "%s-%s" % (name, size), "-LevelFile", level, "-From", str(FROM_SECONDS), "-Seconds", "%.3f" % (FRAMES / FPS),
               "-Fps", str(FPS), "-Size", size, "-Quality", "Ultra", "-NoGrain", "-MaxDetail",
               "-CamPos", "0,%.3f,%.3f" % (middle, distance), "-CamTarget", "0,%.3f,0" % middle, "-Fov", str(FOV),
               "-GameArgs", "wind=0", "-NoEncode", "-KeepFrames"]
    if not build:
        command.append("-NoBuild")
    print("filming %s at %s" % (name, size), flush=True)
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode != 0:
        sys.exit("filming failed (exit %d)" % result.returncode)
    frames = sorted(f for f in os.listdir(folder) if f.startswith("frame-") and f.endswith(".png"))
    if not frames:
        sys.exit("no frame in %s" % folder)
    return os.path.join(folder, frames[-1])


def compose(frame_path, stem):
    """The logo over the frame, with a soft local shadow and no band behind it; the upload and the three views."""
    frame = Image.open(frame_path).convert("RGB")
    width, height = frame.size
    if abs(width / height - 16 / 9) > 1e-3:
        sys.exit("the frame is %dx%d, not 16:9" % (width, height))
    logo = Image.open(LOGO).convert("RGBA")
    logo_height = int(round(height * LOGO_HEIGHT))
    logo = logo.resize((int(round(logo.width * logo_height / logo.height)), logo_height), Image.LANCZOS)
    x, y = (width - logo.width) // 2, (height - logo.height) // 2
    alpha = logo.getchannel("A")

    # Two shadows cut from the logo's own shape: a wide faint one that lifts it off whatever is lit behind it, and a tighter one
    # dropped a little. Each is (blur, drop, strength), the first two as shares of the picture's height
    canvas = frame.convert("RGBA")
    for blur, drop, strength in ((0.030, 0.004, 0.40), (0.008, 0.006, 0.55)):
        radius = height * blur
        pad = int(radius * 4)
        mask = Image.new("L", (logo.width + 2 * pad, logo.height + 2 * pad), 0)
        mask.paste(alpha, (pad, pad))
        mask = mask.filter(ImageFilter.GaussianBlur(radius)).point(lambda v: int(v * strength))
        shade = Image.new("RGBA", mask.size, (8, 4, 20, 255))
        shade.putalpha(mask)
        canvas.alpha_composite(shade, (x - pad, y - pad + int(height * drop)))
    canvas.alpha_composite(logo, (x, y))
    banner = canvas.convert("RGB")
    master = "%s-%dx%d.png" % (stem, width, height)
    banner.save(master)

    small = banner.resize(BANNER, Image.LANCZOS) if (width, height) != BANNER else banner
    quality = 95
    while True:
        buffer = io.BytesIO()
        small.save(buffer, "JPEG", quality=quality, subsampling=0, optimize=True)
        if buffer.tell() < UPLOAD_LIMIT or quality <= 80:
            break
        quality -= 1
    upload = "%s-%dx%d.jpg" % (stem, BANNER[0], BANNER[1])
    with open(upload, "wb") as f:
        f.write(buffer.getvalue())

    def middle_crop(crop_width, crop_height):
        w, h = int(round(width * crop_width / BANNER[0])), int(round(height * crop_height / BANNER[1]))
        return banner.crop(((width - w) // 2, (height - h) // 2, (width + w) // 2, (height + h) // 2))

    sheet_width = 1280
    tv = banner.resize((sheet_width, sheet_width * 9 // 16), Image.LANCZOS)
    draw = ImageDraw.Draw(tv)
    for crop_width, colour in ((BANNER[0], (255, 255, 255)), (PHONE_WIDTH, (255, 220, 60))):
        w, h = sheet_width * crop_width / BANNER[0], tv.height * STRIP_HEIGHT / BANNER[1]
        draw.rectangle(((sheet_width - w) / 2, (tv.height - h) / 2, (sheet_width + w) / 2 - 1, (tv.height + h) / 2), outline=colour, width=2)
    desktop = middle_crop(BANNER[0], STRIP_HEIGHT).resize((sheet_width, int(round(sheet_width * STRIP_HEIGHT / BANNER[0]))), Image.LANCZOS)
    phone = middle_crop(PHONE_WIDTH, STRIP_HEIGHT).resize((sheet_width, int(round(sheet_width * STRIP_HEIGHT / PHONE_WIDTH))), Image.LANCZOS)
    gap = 14
    sheet = Image.new("RGB", (sheet_width, tv.height + desktop.height + phone.height + 2 * gap), (20, 20, 24))
    sheet.paste(tv, (0, 0))
    sheet.paste(desktop, (0, tv.height + gap))
    sheet.paste(phone, (0, tv.height + desktop.height + 2 * gap))
    sheet.save(stem + "-crops.jpg", quality=90)

    strip_logo = logo_height * BANNER[1] / height
    print("%s: %dx%d; the upload %s at JPEG quality %d, %.2f MB; the logo %d px of the strip's %d"
          % (os.path.basename(stem), width, height, os.path.basename(upload), quality, buffer.tell() / 1e6, round(strip_logo), STRIP_HEIGHT))


def main():
    parser = argparse.ArgumentParser(description="The YouTube channel banner: the logo framed by an arch of balls, filmed in the Game.")
    parser.add_argument("--scene", default="neon", help="the scene's name as a level file spells it (neon, meadow, space, ...)")
    parser.add_argument("--sky", type=int, help="the sky dome; the one the scene's shipped levels name when left out")
    parser.add_argument("--balls", default="bubble", help="what the balls are made of (bubble, beach, gem, ...)")
    parser.add_argument("--size", default="5120x2880", help="the size filmed, 16:9; the upload is scaled down from it")
    parser.add_argument("--out", default=".", help="the folder the pictures are written to")
    parser.add_argument("--name", help="the pictures' name; bs3d-banner-<scene> when left out")
    parser.add_argument("--frame", help="a frame filmed before: only lay the logo over it")
    parser.add_argument("--no-build", action="store_true", help="use the Game web-capture.ps1 already built")
    options = parser.parse_args()

    name = options.name or "bs3d-banner-%s" % options.scene
    os.makedirs(options.out, exist_ok=True)
    frame = options.frame
    if not frame:
        sky = options.sky if options.sky is not None else scene_sky(options.scene)
        level, middle = arch_level(options.scene, sky, options.balls)
        frame = film(name, level, middle, options.size, not options.no_build)
    compose(frame, os.path.join(options.out, name))


if __name__ == "__main__":
    main()
