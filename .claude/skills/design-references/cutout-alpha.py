"""Cuts the plum background out of a generated logo and writes a straight-alpha RGBA PNG.

Why not a colour key: between each letter and its purple outline runs a dark groove whose distance from
the background is only about 16 units, while the outline itself sits barely above it. A threshold that
keeps the groove opaque also keeps half the background, and one that drops the background eats the
groove - and with it the separation that makes the letters read.

So the background is identified by CONNECTIVITY, not by colour alone. A pixel near the background colour
counts as background only if it can be reached from the frame edge without crossing the artwork; the
groove and the letter counters are enclosed, so they stay opaque whatever their colour. The outer glow
is the one place a soft edge is wanted, and there the pixel is un-composited against the modelled
background so it comes out as translucent light rather than as a plum halo.
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

SRC = sys.argv[1]
DST = sys.argv[2]
OUT_WIDTH = int(sys.argv[3]) if len(sys.argv) > 3 else 2048
BORDER = 40          # px of frame taken as pure background when fitting the gradient
# Where to cut. Measured on this logo, the distance-from-background is cleanly bimodal: the frame's own
# noise sits at ~1, the outer glow spans roughly 8..45, and the drawn artwork starts above ~145 (the 70th
# and 75th percentiles of the whole frame are 43.7 and 145.2). So a cut anywhere in 45..145 keeps the
# letters and their outline and drops the glow, and a cut near 8 keeps the glow too - which is only right
# if the logo will sit on the dark background it was drawn against.
T_LO = float(sys.argv[4]) if len(sys.argv) > 4 else 45.0
T_HI = float(sys.argv[5]) if len(sys.argv) > 5 else 95.0
MARGIN_FRAC = 0.015  # breathing room kept around the artwork when cropping

im = Image.open(SRC).convert('RGB')
c = np.asarray(im).astype(np.float32)
h, w, _ = c.shape
print('source %dx%d' % (w, h))

# --- 1. Model the background as a quadratic in x,y (the dome-like radial gradient fits well) ---
yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
xn, yn = xx / w - 0.5, yy / h - 0.5
basis = np.stack([np.ones_like(xn), xn, yn, xn * xn, xn * yn, yn * yn], -1)

edge = np.zeros((h, w), bool)
edge[:BORDER] = edge[-BORDER:] = True
edge[:, :BORDER] = edge[:, -BORDER:] = True
A = basis[edge]
bg = np.empty_like(c)
for ch in range(3):
    coef, *_ = np.linalg.lstsq(A, c[edge][:, ch], rcond=None)
    bg[..., ch] = basis @ coef
del basis, A, xn, yn, xx, yy

d = np.linalg.norm(c - bg, axis=-1)
sigma = float(d[edge].std())
t_lo = max(T_LO, 4.0 * sigma)
print('background residual on the frame: sigma %.2f -> t_lo %.1f, t_hi %.1f' % (sigma, t_lo, T_HI))

# --- 2. Background = near the model AND reachable from the frame edge ---
near = d < T_HI
lab, n = ndimage.label(near)
edge_labels = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
edge_labels = edge_labels[edge_labels > 0]
outside = np.isin(lab, edge_labels)
enclosed = near & ~outside
print('%d near-background regions; %d px enclosed by artwork (groove, counters) -> forced opaque'
      % (n, int(enclosed.sum())))
del lab, near, outside

alpha = np.clip((d - t_lo) / (T_HI - t_lo), 0.0, 1.0)
alpha[d >= T_HI] = 1.0
alpha[enclosed] = 1.0
# Close single-pixel pinholes left by grain inside solid areas.
alpha = np.maximum(alpha, ndimage.grey_closing(alpha, size=3))
del enclosed, d

# --- 3. Un-composite: C = bg*(1-a) + F*a, so F = bg + (C-bg)/a ---
safe = np.maximum(alpha, 1e-3)[..., None]
fg = np.clip(bg + (c - bg) / safe, 0, 255)
fg = np.where(alpha[..., None] > 0.995, c, fg)
del bg, c, safe

# --- 4. Crop to what is actually drawn ---
# Single grains of film noise clear the threshold too, and one of them at the frame edge would stretch the
# box over the whole picture - which is exactly what it did before this. Keep only real regions.
drawn = alpha > 0.02
lab2, n2 = ndimage.label(drawn)
sizes = np.bincount(lab2.ravel())
sizes[0] = 0
drawn = np.isin(lab2, np.nonzero(sizes >= 256)[0])
print('dropped %d speckle regions of the %d found' % ((sizes[1:] < 256).sum(), n2))
ys, xs = np.nonzero(drawn)
del lab2, drawn
mx, my = int(w * MARGIN_FRAC), int(h * MARGIN_FRAC)
x0, x1 = max(0, xs.min() - mx), min(w, xs.max() + 1 + mx)
y0, y1 = max(0, ys.min() - my), min(h, ys.max() + 1 + my)
fg, alpha = fg[y0:y1, x0:x1], alpha[y0:y1, x0:x1]
print('cropped to %dx%d at (%d,%d) - %.0f%% of the frame was empty plum'
      % (x1 - x0, y1 - y0, x0, y0, 100 * (1 - ((x1 - x0) * (y1 - y0)) / (w * h))))

# --- 5. Resize in PREMULTIPLIED space, or the transparent pixels' colour bleeds into the edge ---
ch, cw = alpha.shape
out_h = max(1, round(OUT_WIDTH * ch / cw))
pre = np.concatenate([fg * alpha[..., None], alpha[..., None] * 255.0], -1)
pre = np.asarray(Image.fromarray(np.clip(pre, 0, 255).astype(np.uint8), 'RGBA')
                 .resize((OUT_WIDTH, out_h), Image.LANCZOS)).astype(np.float32)
a_out = pre[..., 3:4] / 255.0
rgb_out = np.where(a_out > 1e-3, pre[..., :3] / np.maximum(a_out, 1e-3), 0.0)

out = np.concatenate([np.clip(rgb_out, 0, 255), np.clip(a_out * 255.0, 0, 255)], -1).astype(np.uint8)
Image.fromarray(out, 'RGBA').save(DST)
op = (out[..., 3] == 255).mean() * 100
tr = (out[..., 3] == 0).mean() * 100
print('%s  %dx%d  %.1f%% fully opaque, %.1f%% fully transparent, %.1f%% soft edge'
      % (DST, OUT_WIDTH, out_h, op, tr, 100 - op - tr))
