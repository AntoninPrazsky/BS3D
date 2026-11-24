"""Does each ball style keep its surface detail at every size it is drawn at? (#1216)

python ball-detail.py <sweep dir> [--cue none] [--styles a,b] [--check LO,HI] [--ladder] [--json out.json]

Reads the captures ball-detail-sweep.ps1 takes of Testbed\\Maps\\Thirteen_Colors.json at 1920x1080, the camera at
(0, 4.5, z) looking at (0, 4.5, 0): the pair <style>-<cue>-z028-1.png (fov 42.86, a ball R 25 display pixels, the
play view) and <style>-<cue>-close-1.png (the same place at fov 15.94, R 69, the menu's close pass), and with
--ladder every <style>-<cue>-zNNN-1.png (fov 42.86). The map's upper row hangs at world z = 0, x = -6..6, whole in
every frame: those thirteen balls are measured, less the middle one, which has the Testbed's crosshair across it.
Their centres are found once per view by an edge-on-a-circle search round the pinhole prediction (on the vinyl's
capture when there is one, its outline being the sharpest) and kept in centres.json beside the captures.

Per ball, in CIE L* (what the eye reads), inside 0.8 of the radius (the limb has a ring of its own):
  dK     the RMS of L* minus its own blur at sigma = R / K, a normalised convolution inside the disc so the sky does not
         leak in: the energy at scales finer than R / K, the same RELATIVE band on every size of ball. d6 is the
         reading the check uses; d3, coarser, sees a figure the size of the ball's face (the metal's streak).
  meanL  the disc's mean linear luminance, encoded: a value that walks with the size is a fault of its own.
Each figure is the median over the twelve balls (thirteen colours less the crosshair's).

THE REPORT, per style:
  play/close   d6 of the play view over d6 of the close-up. Under 1 is not by itself a fault: an octave finer than
               about two pixels at the play size cannot be drawn there (the band limits exist for it, #150), and the
               d6 band holds it close up.
  ideal/close  what a perfect filter keeps: each close-up ball shrunk to the play radius by a box filter in linear
               light, then measured as the play view is. The resolution's own loss, for that style's figure.
  play/ideal   THE READING: the play view against the close-up shrunk perfectly. 1 is one material at both sizes;
               over 1 is detail the shader shows only from afar (a relief stronger in play, a glint that lands on a
               whole pixel), under 1 detail it shows only close up (a layer faded out instead of to its average).
--check LO,HI exits 1 when any style's play/ideal is outside the range (the check docs/rendering/ball-styles.md
states, "What fades where"). --ladder prints the zNNN captures' figures by distance instead.

Needs numpy, scipy and Pillow.
"""
import json
import os
import re
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

W, H = 1920, 1080
BALL_X = np.arange(-6, 7, dtype=float)
CROSSHAIR_BALL = 6
BANDS = (12, 6, 3)
PLAY, CLOSE = 'z028', 'close'


def lens(view):
    """The camera's z and focal length in pixels for a view's file name."""
    if view == CLOSE:
        return 28.0, (H / 2) / np.tan(np.radians(15.94) / 2)
    return float(view[1:]), (H / 2) / np.tan(np.radians(42.86) / 2)


def srgb_to_linear(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lstar(lin_y):
    t = np.cbrt(np.maximum(lin_y, 0))
    return np.where(lin_y > 0.008856, 116 * t - 16, 903.3 * lin_y)


def luminance(path):
    rgb = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64)
    if rgb.shape[:2] != (H, W):
        raise SystemExit('%s is %dx%d, not the sweep\'s %dx%d' % (path, rgb.shape[1], rgb.shape[0], W, H))
    return srgb_to_linear(rgb) @ np.array([0.2126, 0.7152, 0.0722])


def circle_score(grad, cx, cy, r):
    t = np.linspace(0, 2 * np.pi, 96, endpoint=False)
    return ndimage.map_coordinates(grad, [cy + r * np.sin(t), cx + r * np.cos(t)], order=1, mode='nearest').mean()


def find_centres(path, view):
    z, f = lens(view)
    L = lstar(luminance(path))
    grad = np.hypot(ndimage.sobel(L, 1), ndimage.sobel(L, 0))
    r = f * 0.5 / np.sqrt(z * z - 0.25)
    pxs = [W / 2 + f * X / z for X in BALL_X]
    # The row's height on screen is searched on all thirteen at once, then each ball round its prediction
    best = None
    for cy in np.arange(max(H / 2 - 4 * r - 40, r), min(H / 2 + 2 * r + 40, H - r), max(r / 8, 0.5)):
        s = sum(circle_score(grad, px, cy, r) for px in pxs)
        if best is None or s > best[0]:
            best = (s, cy)
    cy0 = best[1]
    step, reach = max(r / 32, 0.25), max(r / 8, 1.5)
    centres = []
    for px in pxs:
        bestb = None
        for dy in np.arange(-reach, reach + 1e-6, step):
            for dx in np.arange(-reach, reach + 1e-6, step):
                s = circle_score(grad, px + dx, cy0 + dy, r)
                if bestb is None or s > bestb[0]:
                    bestb = (s, px + dx, cy0 + dy)
        centres.append((bestb[1], bestb[2]))
    return r, centres


def ball_metrics(y, cx, cy, r):
    pad = int(np.ceil(r * 1.3)) + 4
    x0, y0 = int(cx - pad), int(cy - pad)
    sub = y[y0:int(cy + pad) + 1, x0:int(cx + pad) + 1]
    yy, xx = np.mgrid[y0:y0 + sub.shape[0], x0:x0 + sub.shape[1]]
    rr = np.hypot(xx - cx, yy - cy) / r
    disc = (rr < 0.95).astype(float)
    inner = rr < 0.8
    L = lstar(sub)
    out = {}
    for k in BANDS:
        s = max(r / k, 0.35)
        smooth = ndimage.gaussian_filter(L * disc, s) / np.maximum(ndimage.gaussian_filter(disc, s), 1e-6)
        out['d%d' % k] = float(np.sqrt(np.mean((L - smooth)[inner] ** 2)))
    out['meanL'] = float(lstar(np.array(sub[rr < 0.95].mean())))
    return out


def shrunk(y, cx, cy, r, r_to):
    """The ball at (cx, cy), radius r, shrunk to radius r_to by a box filter in linear light: (image, cx, cy)."""
    pad = int(np.ceil(r * 1.6))
    x0, y0 = int(np.floor(cx)) - pad, int(np.floor(cy)) - pad
    crop = y[y0:y0 + 2 * pad + 1, x0:x0 + 2 * pad + 1]
    s = r_to / r
    n = int(round(crop.shape[0] * s))
    small = np.asarray(Image.fromarray(crop.astype(np.float32), mode='F').resize((n, n), Image.BOX), dtype=np.float64)
    s = n / crop.shape[0]
    # Pixel centres sit at whole indices: an index u is the continuous u + 0.5, which the resize scales by s
    return small, (cx - x0 + 0.5) * s - 0.5, (cy - y0 + 0.5) * s - 0.5


def opt(args, name, default):
    return args[args.index(name) + 1] if name in args else default


def main():
    args = sys.argv[1:]
    if not args or args[0].startswith('--'):
        raise SystemExit(__doc__)
    root = args[0]
    cue = opt(args, '--cue', 'none').replace(':', '')
    styles = opt(args, '--styles', None)
    styles = styles.split(',') if styles else None
    check = opt(args, '--check', None)
    out_json = opt(args, '--json', None)

    pat = re.compile(r'^([a-z]+)-%s-(z\d+|close)-1\.png$' % re.escape(cue))
    caps = {}
    for f in os.listdir(root):
        m = pat.match(f)
        if m and (styles is None or m.group(1) in styles):
            caps.setdefault(m.group(1), {})[m.group(2)] = os.path.join(root, f)
    if not caps:
        raise SystemExit('No <style>-%s-<view>-1.png captures in %s' % (cue, root))
    views = sorted({v for s in caps.values() for v in s})

    centre_file = os.path.join(root, 'centres.json')
    centres = json.load(open(centre_file)) if os.path.exists(centre_file) else {}
    for v in views:
        if v not in centres:
            ref = caps.get('beach', {}).get(v) or next(c[v] for c in caps.values() if v in c)
            r, c = find_centres(ref, v)
            centres[v] = {'r': r, 'c': c}
    json.dump(centres, open(centre_file, 'w'))

    def measure(y, view):
        r = centres[view]['r']
        return [ball_metrics(y, cx, cy, r) for i, (cx, cy) in enumerate(centres[view]['c']) if i != CROSSHAIR_BALL]

    def median(rows):
        return {k: float(np.median([p[k] for p in rows])) for k in rows[0]}

    if '--ladder' in args:
        keys = ['d12', 'd6', 'd3', 'meanL']
        for style in sorted(caps):
            print('%s (cue %s)' % (style, cue))
            print('   view     R  ' + '  '.join('%7s' % k for k in keys))
            for v in sorted(caps[style], key=lambda v: (v == CLOSE, v)):
                m = median(measure(luminance(caps[style][v]), v))
                print('  %5s  %5.1f ' % (v, centres[v]['r']) + '  '.join('%7.2f' % m[k] for k in keys))
        return

    lo, hi = (float(v) for v in check.split(',')) if check else (None, None)
    failed, result = [], {}
    r_play, r_close = (centres[v]['r'] if v in centres else None for v in (PLAY, CLOSE))
    print('cue %s: the play view (R %s) against the close-up (R %s), one place, one pose'
          % (cue, '%.0f' % r_play if r_play else '-', '%.0f' % r_close if r_close else '-'))
    print('  %-10s %10s %11s %10s %8s %7s %7s  %s'
          % ('style', 'play/close', 'ideal/close', 'play/ideal', 'd3 p/i', 'd6 near', 'meanL', ''))
    for style in sorted(caps):
        if PLAY not in caps[style] or CLOSE not in caps[style]:
            print('  %-10s missing %s or %s' % (style, PLAY, CLOSE))
            failed.append(style)
            continue
        y_play, y_close = luminance(caps[style][PLAY]), luminance(caps[style][CLOSE])
        play, close = median(measure(y_play, PLAY)), median(measure(y_close, CLOSE))
        ideal = []
        for i, (cx, cy) in enumerate(centres[CLOSE]['c']):
            if i != CROSSHAIR_BALL:
                small, sx, sy = shrunk(y_close, cx, cy, r_close, r_play)
                ideal.append(ball_metrics(small, sx, sy, r_play))
        ideal = median(ideal)
        row = {'play': play, 'close': close, 'ideal': ideal,
               'play/close': play['d6'] / close['d6'], 'ideal/close': ideal['d6'] / close['d6'],
               'play/ideal': play['d6'] / ideal['d6'], 'd3 play/ideal': play['d3'] / ideal['d3']}
        result[style] = row
        verdict = ''
        if check and not (lo <= row['play/ideal'] <= hi):
            verdict = 'OUT OF %.2f-%.2f' % (lo, hi)
            failed.append(style)
        print('  %-10s %10.2f %11.2f %10.2f %8.2f %7.2f %3.0f/%-3.0f  %s'
              % (style, row['play/close'], row['ideal/close'], row['play/ideal'], row['d3 play/ideal'], close['d6'],
                 close['meanL'], play['meanL'], verdict))
    if out_json:
        json.dump(result, open(out_json, 'w'), indent=1)
    if check:
        print('check %s: %s' % (check, 'FAILED for ' + ', '.join(failed) if failed else 'every style inside'))
        sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
