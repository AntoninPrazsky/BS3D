"""Sharp edges from the connectivity cut, subject decision from the matting network.

The two methods disagreed about one thing on this logo: whether the thin purple keyline looping around each
letter belongs to the mark. The cut keeps it, because it is solid artwork far from the background colour;
BiRefNet drops it, because it does not read as part of the subject. Over the dark plum it was drawn on the
keyline is a lem; over the game's bright sky it reads as a sticker outline, so the network's answer is the
one wanted - but its matte comes from a 1024x1024 input and is four times too soft to use directly.

So: gate the cut's alpha with the network's mask, dilated enough not to clip the cut's own sharp edge.
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

SRC = sys.argv[1]        # the RGB source, full resolution
CUT = sys.argv[2]        # the connectivity cut, already cropped and scaled
MASK = sys.argv[3]       # the network's mask at the SOURCE's resolution
DST = sys.argv[4]
GATE = float(sys.argv[5]) if len(sys.argv) > 5 else 0.5
DILATE = int(sys.argv[6]) if len(sys.argv) > 6 else 12

cut = np.asarray(Image.open(CUT).convert('RGBA'), np.float32)
mask = np.asarray(Image.open(MASK).convert('L'), np.float32) / 255.0

# The cut is cropped and scaled; bring the mask into its frame by matching aspect and size.
gate = mask > GATE
gate = ndimage.binary_dilation(gate, np.ones((DILATE * 2 + 1,) * 2, bool))
gate_img = Image.fromarray((gate * 255).astype(np.uint8))
# Crop the mask the same way cutout-alpha.py cropped the picture, then scale to the cut's size.
ys, xs = np.nonzero(np.asarray(gate_img) > 0)
h, w = mask.shape
mx, my = int(w * 0.015), int(h * 0.015)
box = (max(0, xs.min() - mx), max(0, ys.min() - my), min(w, xs.max() + 1 + mx), min(h, ys.max() + 1 + my))
gate_img = gate_img.crop(box).resize((cut.shape[1], cut.shape[0]), Image.LANCZOS)
g = np.asarray(gate_img, np.float32) / 255.0
print('gate: %.1f%% of the cut frame kept, dilated by %d px at source scale' % (100 * (g > 0.5).mean(), DILATE))

alpha = cut[..., 3] * np.clip(g, 0, 1)
out = np.concatenate([cut[..., :3], alpha[..., None]], -1).astype(np.uint8)
Image.fromarray(out, 'RGBA').save(DST)
op = (out[..., 3] == 255).mean() * 100
tr = (out[..., 3] == 0).mean() * 100
print('%s  %dx%d  %.1f%% opaque, %.1f%% transparent, %.1f%% soft'
      % (DST, out.shape[1], out.shape[0], op, tr, 100 - op - tr))
