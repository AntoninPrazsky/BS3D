"""Makes a seamless loop of the Game's capture frames (#946) whose picture has no period to cut on (#1229), for
Tools/web-capture-encode.py, cropping them to a box as it goes.

    python -I Tools/web-capture-loop.py --frames <capture folder> --fade <frames> --out <folder> [--crop x0,y0,x1,y1]
    python -I Tools/web-capture-encode.py --frames <that folder> --fps <the capture's> --out <path without extension>

A noise field (the outback's heat shimmer) never comes back to where it began, so no window of it loops by itself. Film the
loop and FADE frames more (web-capture.ps1 -NoEncode leaves them in its frames folder): the clip is the capture without its
last FADE frames, and its first FADE frames are cross-faded from those last ones, which follow the clip's last frame, into the
clip's own. So the clip's last frame is followed by what the capture had next, and that fades into the clip's own start: no
step at the seam larger than any other frame's (docs/website.md, "Filming the game"). The rest are copied as they are, cropped.

--crop cuts every frame to the box at its own pixels (left, top, right, bottom; right and bottom exclusive), before the encoder,
which only scales: a clip of a strip of the picture is a crop of the frames, not a scaled-down whole.
The frames are written as frame-NNNNNN.png from 1, the names the encoder reads.
"""
import argparse
import glob
import os
import sys

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--frames", required=True, help="the capture's folder of frame-NNNNNN.png")
    parser.add_argument("--fade", required=True, type=int, help="frames cross-faded at the seam; the capture runs this many past the loop")
    parser.add_argument("--out", required=True, help="the folder the loop's frames are written to (must hold no frame-*.png)")
    parser.add_argument("--crop", default=None, help="x0,y0,x1,y1: the box every frame is cut to, at its own pixels")
    arguments = parser.parse_args()

    files = sorted(glob.glob(os.path.join(arguments.frames, "frame-*.png")))
    if not files:
        sys.exit("no frame-*.png in " + arguments.frames)
    fade = arguments.fade
    length = len(files) - fade
    # The fade's two ends must be different frames, with the clip's own frames between them
    if fade < 1 or length < 2 * fade:
        sys.exit("%d frames cannot loop with a fade of %d: the loop must be at least twice the fade" % (len(files), fade))

    box = None
    if arguments.crop:
        box = tuple(int(value) for value in arguments.crop.split(","))
        if len(box) != 4 or box[0] >= box[2] or box[1] >= box[3]:
            sys.exit("--crop is x0,y0,x1,y1 with x0 < x1 and y0 < y1: " + arguments.crop)

    if os.path.isdir(arguments.out) and glob.glob(os.path.join(arguments.out, "frame-*.png")):
        sys.exit("the output folder already holds frames: " + arguments.out)
    os.makedirs(arguments.out, exist_ok=True)

    def load(index):
        with Image.open(files[index]) as image:
            image = image.convert("RGB")
            return np.asarray(image.crop(box) if box else image).astype(np.float32)

    for index in range(length):
        if index < fade:
            # From the frame that follows the clip's last (weight near 1 at the clip's first frame) into the clip's own
            weight = (index + 0.5) / fade
            pixels = (1 - weight) * load(length + index) + weight * load(index)
        else:
            pixels = load(index)
        Image.fromarray(np.clip(pixels + 0.5, 0, 255).astype(np.uint8)).save(
            os.path.join(arguments.out, "frame-%06d.png" % (index + 1)))
    size = "%dx%d" % (box[2] - box[0], box[3] - box[1]) if box else "their own size"
    print("%d frames of %s written to %s, the first %d cross-faded from the %d after the loop" % (length, size, arguments.out, fade, fade))


if __name__ == "__main__":
    main()
