"""How different the two sides of a blog post's comparison are (#1237): a before/after pair goes in only if the difference is plain at
a glance at the size a reader sees it (docs/website.md, "The blog"), and this is the measure taken before it goes in.

Run it with the Python web-capture-encode.py uses (PyAV, numpy, Pillow, SciPy):

    python -I Tools/web-compare.py clips <before.webm> <after.webm> [--quiet 12]
    python -I Tools/web-compare.py stills <before.jpg> <after.jpg> [--shown 1056]

clips: two clips of one moment, frame for frame (a capture with and without the change, same arguments otherwise):
  - stray: every textured point of the region that moves is tracked through EACH clip on its own, frame by frame (nothing moves
    more than a few pixels between two frames), and the two tracks are compared at the same frame. The points start in the same
    place in both, since the frames before the change acts are identical, so the distance is how far that piece of the picture
    stands from its twin. Reported as the 90th percentile over the points at the frame where it peaks, in pixels of the clip and as
    a share of its width (the scale-free figure: a clip is shown at its own size on a desktop and shrunk on a phone).
  - share: the share of that region whose luma differs by VISIBLE or more at the same frame, and the same before anything happens
    (--quiet frames: the noise floor of two encodes of identical frames, 0 for the repository's encoder).
  Trap: matching the two clips' frames against each other directly is fooled by the lattice: a patch of one ball matches any ball of its
  colour a row away, so the answer grew with the search (a pennant's swing read 126, 161 and 233 px for searches of 112, 160 and
  240). Tracking each clip on its own never searches further than a ball.

stills: two pictures of one place, as the page shows them (--shown scales both to that width first):
  - the share of the picture whose colour differs by VISIBLE or more in any channel, and the region that changed at all (by 8 or
    more, closed into blobs) with the share of it that differs visibly. A figure says what the pixels do; look at the two side by
    side at the size shown as well, since a small change on the one thing the caption names can still read at a glance.
"""
import argparse
import json

import av
import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.signal import fftconvolve

VISIBLE = 20   # levels of 255: a step anyone sees between two pictures
SEARCH = 14    # px a tracked point is searched each way between two frames, under half a ball in a clip cut round a cluster
PATCH = 24     # px of a tracked patch


def decode(path):
    with av.open(path) as container:
        return np.stack([frame.to_ndarray(format="rgb24") for frame in container.decode(video=0)])


def luma(rgb):
    rgb = rgb.astype(np.float32)
    return 0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]


def step(prev, cur, y, x):
    """Where the PATCH at (y, x) of prev is in cur, searched SEARCH px each way (least squared difference)."""
    h, w = prev.shape
    y, x = int(round(y)), int(round(x))
    patch = prev[y:y + PATCH, x:x + PATCH]
    y0, x0 = max(0, y - SEARCH), max(0, x - SEARCH)
    window = cur[y0:min(h, y + PATCH + SEARCH), x0:min(w, x + PATCH + SEARCH)]
    ones = np.ones_like(patch)
    ssd = (fftconvolve(window * window, ones[::-1, ::-1], mode="valid")
           - 2 * fftconvolve(window, patch[::-1, ::-1], mode="valid") + (patch * patch).sum())
    iy, ix = np.unravel_index(np.argmin(ssd), ssd.shape)
    return (y0 + iy) - y, (x0 + ix) - x


def track(Y, points):
    h, w = Y[0].shape
    out = np.zeros((len(Y), len(points), 2), np.float32)
    cur = np.array(points, np.float32)
    out[0] = cur
    for t in range(1, len(Y)):
        for i, (y, x) in enumerate(cur):
            dy, dx = step(Y[t - 1], Y[t], y, x)
            cur[i] = (min(max(y + dy, 0), h - PATCH), min(max(x + dx, 0), w - PATCH))
        out[t] = cur
    return out


def clips(before, after, quiet):
    A, B = decode(before), decode(after)
    n = min(len(A), len(B))
    YA = np.stack([luma(f) for f in A[:n]])
    YB = np.stack([luma(f) for f in B[:n]])
    # the region that moves: anything either clip changes over its length, closed into one area
    change = (np.abs(YA - YA[0]).max(axis=0) >= VISIBLE) | (np.abs(YB - YB[0]).max(axis=0) >= VISIBLE)
    region = ndimage.binary_closing(ndimage.binary_dilation(change, iterations=8), iterations=8)
    h, w = YA[0].shape
    points = [(y, x) for y in range(0, h - PATCH, 16) for x in range(0, w - PATCH, 16)
              if region[y:y + PATCH, x:x + PATCH].mean() > 0.6 and YA[0][y:y + PATCH, x:x + PATCH].std() > 8]
    if not points:
        raise SystemExit("nothing moves in either clip")
    stray = np.linalg.norm(track(YB, points) - track(YA, points), axis=2)
    p90 = np.percentile(stray, 90, axis=1)
    differs = ((np.abs(YA - YB) >= VISIBLE) & region[None]).reshape(n, -1).sum(axis=1) / region.sum()
    return {
        "size": f"{w}x{h}", "frames": n, "points": len(points),
        "stray_px": round(float(p90.max()), 1), "stray_frame": int(p90.argmax()) + 1,
        "stray_share_of_width": round(float(p90.max() / w), 3),
        "region_share_of_picture": round(float(region.mean()), 3),
        "differing_share_max": round(float(differs.max()), 3),
        "differing_share_mean": round(float(differs[quiet:].mean()), 3),
        "noise_floor": round(float(differs[:quiet].max()), 3) if quiet else None,
    }


def stills(before, after, shown):
    pictures = []
    for path in (before, after):
        image = Image.open(path).convert("RGB")
        if shown and image.width != shown:
            image = image.resize((shown, round(image.height * shown / image.width)), Image.LANCZOS)
        pictures.append(np.asarray(image).astype(np.float32))
    A, B = pictures
    colour = np.abs(A - B).max(axis=2)
    region = ndimage.binary_closing(ndimage.binary_dilation(colour >= 8, iterations=4), iterations=6)
    return {
        "size": f"{A.shape[1]}x{A.shape[0]}",
        "differing_share": round(float((colour >= VISIBLE).mean()), 3),
        "region_share_of_picture": round(float(region.mean()), 3),
        "differing_share_of_region": round(float((colour >= VISIBLE)[region].mean()), 3) if region.any() else 0.0,
        "mean_luma_step_in_region": round(float(np.abs(luma(A) - luma(B))[region].mean()), 1) if region.any() else 0.0,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("kind", choices=["clips", "stills"])
    parser.add_argument("before")
    parser.add_argument("after")
    parser.add_argument("--quiet", type=int, default=0, help="clips: frames before the change acts (the noise floor)")
    parser.add_argument("--shown", type=int, default=0, help="stills: the width the page shows them at")
    arguments = parser.parse_args()
    if arguments.kind == "clips":
        result = clips(arguments.before, arguments.after, arguments.quiet)
    else:
        result = stills(arguments.before, arguments.after, arguments.shown)
    print(json.dumps(result, indent=1))


if __name__ == "__main__":
    main()
