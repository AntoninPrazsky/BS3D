"""Alpha from a matting network (BiRefNet, ONNX Runtime on the CPU), for comparison with the cut in
cutout-alpha.py. The network decides what the subject is; nothing here tells it about the background.

Note the resolution ceiling built into the method: BiRefNet takes a fixed 1024x1024 input, so whatever the
source size, the mask comes back at 1024x1024 and is scaled up to meet it. On a 4864x3328 logo that is a
4.75x upscale of the matte, which is the thing to look at when judging the edges.
"""
import sys
import time
import numpy as np
import onnxruntime as ort
from PIL import Image

MODEL = sys.argv[1]
SRC = sys.argv[2]
DST = sys.argv[3]
OUT_WIDTH = int(sys.argv[4]) if len(sys.argv) > 4 else 2048
SIZE = 1024
MEAN = np.array([0.485, 0.456, 0.406], np.float32)
STD = np.array([0.229, 0.224, 0.225], np.float32)

so = ort.SessionOptions()
so.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
sess = ort.InferenceSession(MODEL, so, providers=['CPUExecutionProvider'])
inp = sess.get_inputs()[0]
print('input  %s %s %s' % (inp.name, inp.shape, inp.type))
for o in sess.get_outputs():
    print('output %s %s' % (o.name, o.shape))

im = Image.open(SRC).convert('RGB')
w, h = im.size
x = np.asarray(im.resize((SIZE, SIZE), Image.BILINEAR), np.float32) / 255.0
x = ((x - MEAN) / STD).transpose(2, 0, 1)[None]

t = time.time()
outs = sess.run(None, {inp.name: x})
print('inference %.1f s on the CPU' % (time.time() - t))

# BiRefNet emits several supervision maps; the last is the refined one. Logits, so sigmoid them.
m = outs[-1]
m = m[0, 0] if m.ndim == 4 else np.squeeze(m)
alpha = 1.0 / (1.0 + np.exp(-m.astype(np.float32)))
print('matte %s, range %.3f..%.3f, %.1f%% above 0.5' % (alpha.shape, alpha.min(), alpha.max(),
                                                        100 * (alpha > 0.5).mean()))

alpha = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC),
                   np.float32) / 255.0

# Crop to the matte, exactly as the other route does, so the two can be laid side by side.
ys, xs = np.nonzero(alpha > 0.02)
mx, my = int(w * 0.015), int(h * 0.015)
x0, x1 = max(0, xs.min() - mx), min(w, xs.max() + 1 + mx)
y0, y1 = max(0, ys.min() - my), min(h, ys.max() + 1 + my)
rgb = np.asarray(im, np.float32)[y0:y1, x0:x1]
alpha = alpha[y0:y1, x0:x1]
print('cropped to %dx%d at (%d,%d)' % (x1 - x0, y1 - y0, x0, y0))

ch, cw = alpha.shape
out_h = max(1, round(OUT_WIDTH * ch / cw))
pre = np.concatenate([rgb * alpha[..., None], alpha[..., None] * 255.0], -1)
pre = np.asarray(Image.fromarray(np.clip(pre, 0, 255).astype(np.uint8), 'RGBA')
                 .resize((OUT_WIDTH, out_h), Image.LANCZOS), np.float32)
a_out = pre[..., 3:4] / 255.0
rgb_out = np.where(a_out > 1e-3, pre[..., :3] / np.maximum(a_out, 1e-3), 0.0)
out = np.concatenate([np.clip(rgb_out, 0, 255), np.clip(a_out * 255, 0, 255)], -1).astype(np.uint8)
Image.fromarray(out, 'RGBA').save(DST)
op = (out[..., 3] == 255).mean() * 100
tr = (out[..., 3] == 0).mean() * 100
print('%s  %dx%d  %.1f%% opaque, %.1f%% transparent, %.1f%% soft' % (DST, OUT_WIDTH, out_h, op, tr,
                                                                     100 - op - tr))
