"""Does each ball style keep its surface detail at every size it is drawn at? (#1216)

python ball-detail.py <sweep dir> [--cue none] [--styles a,b] [--close 10] [--play 28] [--check LO,HI]
                      [--ladder] [--centres centres.json] [--json out.json]

Reads the <style>-<cue>-zNNN-1.png captures ball-detail-sweep.ps1 takes (Testbed\\Maps\\Thirteen_Colors.json, the
camera at (0, 4.5, z) looking at (0, 4.5, 0), fov 42.86 degrees, 1920x1080). The map's upper row hangs at world z = 0,
x = -6..6, whole in the frame at every distance: those thirteen balls are measured, less the middle one, which has the
Testbed's crosshair across it. Their centres are found once per distance by an edge-on-a-circle search round the
pinhole prediction (on the vinyl's capture when there is one, its outline being the sharpest) and kept in
centres.json beside the captures; --centres reuses another sweep's (the camera fixes them, not the style).

Per ball, in CIE L* (what the eye reads), inside 0.8 of the radius (the limb has a ring of its own):
  dK     the RMS of L* minus its own blur at sigma = R / K, a normalised convolution inside the disc so the sky does not
         leak in: the energy at scales finer than R / K, which is the same RELATIVE band on every size of ball. d6 is
         the reading the check uses; d3 is coarser and sees a figure the size of the ball's face (the metal's streak).
  meanL  the disc's mean linear luminance, encoded: a value that walks with the size is a fault of its own.
Each figure is the median over the twelve balls (thirteen colours less the crosshair's).

The REPORT is each style's play-over-close ratio of d6 and d3, the play view's figure over the close pass's: 1 is the
same detail at both sizes, over 1 detail that shows only from afar, under 1 detail that shows only close up. Some
loss is unavoidable: an octave finer than about two pixels in play has to go (the band limits exist for that, #150),
and the d6 band reaches it close up. --check LO,HI exits 1 when any style's d6 ratio is outside the range (the check
docs/rendering/ball-styles.md states, "What fades where"). --ladder prints every distance in the folder instead.

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
FOV = np.radians(42.86)
F = (H / 2) / np.tan(FOV / 2)
BALL_X = np.arange(-6, 7, dtype=float)
CROSSHAIR_BALL = 6
BANDS = (12, 6, 3)


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


def find_centres(path, z):
    L = lstar(luminance(path))
    grad = np.hypot(ndimage.sobel(L, 1), ndimage.sobel(L, 0))
    r = F * 0.5 / np.sqrt(z * z - 0.25)
    pxs = [W / 2 + F * X / z for X in BALL_X]
    # The row's height on screen is searched on all thirteen at once, then each ball round its prediction
    best = None
    for cy in np.arange(H / 2 - 4 * r - 40, H / 2 + 2 * r + 40, max(r / 8, 0.5)):
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
    x0, x1 = int(cx - pad), int(cx + pad) + 1
    y0, y1 = int(cy - pad), int(cy + pad) + 1
    sub = y[y0:y1, x0:x1]
    yy, xx = np.mgrid[y0:y1, x0:x1]
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
    close_z, play_z = int(opt(args, '--close', '10')), int(opt(args, '--play', '28'))
    check = opt(args, '--check', None)
    out_json = opt(args, '--json', None)

    pat = re.compile(r'^([a-z]+)-%s-z(\d+)-1\.png$' % re.escape(cue))
    caps = {}
    for f in os.listdir(root):
        m = pat.match(f)
        if m and (styles is None or m.group(1) in styles):
            caps.setdefault(m.group(1), {})[int(m.group(2))] = os.path.join(root, f)
    if not caps:
        raise SystemExit('No <style>-%s-zNNN-1.png captures in %s' % (cue, root))
    zs = sorted({z for s in caps.values() for z in s})

    centre_file = opt(args, '--centres', os.path.join(root, 'centres.json'))
    centres = json.load(open(centre_file)) if os.path.exists(centre_file) else {}
    for z in zs:
        if str(z) not in centres:
            ref = caps.get('beach', {}).get(z) or next(c[z] for c in caps.values() if z in c)
            r, c = find_centres(ref, z)
            centres[str(z)] = {'r': r, 'c': c}
    json.dump(centres, open(os.path.join(root, 'centres.json'), 'w'))

    result = {}
    for style in sorted(caps):
        for z in sorted(caps[style]):
            y = luminance(caps[style][z])
            r = centres[str(z)]['r']
            per = [ball_metrics(y, cx, cy, r) for i, (cx, cy) in enumerate(centres[str(z)]['c']) if i != CROSSHAIR_BALL]
            med = {k: float(np.median([p[k] for p in per])) for k in per[0]}
            med['R'] = r
            result.setdefault(style, {})[z] = med
    if out_json:
        json.dump(result, open(out_json, 'w'), indent=1)

    if '--ladder' in args:
        keys = ['d12', 'd6', 'd3', 'meanL']
        for style, rows in result.items():
            print('%s (cue %s)' % (style, cue))
            print('   z     R  ' + '  '.join('%7s' % k for k in keys))
            for z in sorted(rows):
                print('  %3d  %5.1f ' % (z, rows[z]['R']) + '  '.join('%7.2f' % rows[z][k] for k in keys))
        return

    lo, hi = (float(v) for v in check.split(',')) if check else (None, None)
    failed = []
    print('cue %s: play (z %d) over close (z %d)' % (cue, play_z, close_z))
    print('  %-10s %6s %6s %7s %7s %7s  %s' % ('style', 'd6', 'd3', 'd6 far', 'd6 near', 'meanL', ''))
    for style, rows in result.items():
        if close_z not in rows or play_z not in rows:
            print('  %-10s missing z %d or z %d' % (style, close_z, play_z))
            failed.append(style)
            continue
        near, far = rows[close_z], rows[play_z]
        d6, d3 = far['d6'] / near['d6'], far['d3'] / near['d3']
        verdict = ''
        if check and not (lo <= d6 <= hi):
            verdict = 'OUT OF %.2f-%.2f' % (lo, hi)
            failed.append(style)
        print('  %-10s %6.2f %6.2f %7.2f %7.2f %3.0f/%-3.0f  %s'
              % (style, d6, d3, far['d6'], near['d6'], near['meanL'], far['meanL'], verdict))
    if check:
        print('check %s: %s' % (check, 'FAILED for ' + ', '.join(failed) if failed else 'every style inside'))
        sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
