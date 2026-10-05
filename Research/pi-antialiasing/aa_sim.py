# A numpy simulation of edge anti-aliasing options for the Pi's Potato path. NOT the game: a cluster of lit
# spheres on the BS3D lattice over a sky gradient, an island rim and a ceiling plate, rasterised the way a GPU
# would at the Pi's 1280x720 render size and scaled up bilinearly to 1920x1080 as PotatoUpscale does.
import numpy as np
from PIL import Image, ImageDraw, ImageFont

RNG = np.random.default_rng(7)

# ---------- the scene, in "render pixels" of a virtual 1280x720 frame; scale s renders it at another size ----------
def build_scene():
    balls = []
    palette = np.array([[0.80, 0.06, 0.05], [0.05, 0.55, 0.10], [0.06, 0.16, 0.80], [0.90, 0.72, 0.05],
                        [0.05, 0.60, 0.70], [0.70, 0.08, 0.60]])
    f = 520.0                       # focal length in 720p pixels: a ball is ~12 px in radius, as in play
    cam = np.array([0.0, -3.2, 21.0])
    levels = 9
    for L in range(levels):
        half = 4 - L // 2
        shift = 0.5 if L % 2 else 0.0
        for ix in range(-half, half + 1):
            for iz in range(-half, half + 1):
                if L % 2 and (ix == half or iz == half):
                    continue
                w = np.array([ix + shift, 2.2 - L / np.sqrt(2.0), iz + shift])
                v = w - cam
                zv = -v[2]
                sx = 640 + f * v[0] / zv
                sy = 360 - f * v[1] / zv
                shell = (ix + 2 * iz + L + 40) % len(palette)
                balls.append((sx, sy, zv, f * 0.5 / zv, palette[shell]))
    return balls, f

SKY_TOP = np.array([0.10, 0.28, 0.75])
SKY_HORIZON = np.array([0.62, 0.78, 0.95])
L_DIR = np.array([-0.45, 0.70, 0.55]); L_DIR = L_DIR / np.linalg.norm(L_DIR)
H_DIR = L_DIR + np.array([0, 0, 1.0]); H_DIR = H_DIR / np.linalg.norm(H_DIR)

def to_display(lin):
    x = np.maximum(lin, 0.0)
    x = x / (1.0 + x * 0.6)
    return np.clip(x, 0, 1) ** (1 / 2.2)

def shade(nx, ny, nz, tint):
    amb = (0.28 + 0.22 * ny)[..., None] * tint
    ndl = np.maximum(nx * L_DIR[0] + ny * L_DIR[1] + nz * L_DIR[2], 0.0)
    ndh = np.maximum(nx * H_DIR[0] + ny * H_DIR[1] + nz * H_DIR[2], 0.0)
    fres = (1 - np.clip(nz, 0, 1)) ** 4
    lin = amb + ndl[..., None] * tint * 1.25 + (ndh ** 40)[..., None] * 0.9 + fres[..., None] * SKY_HORIZON * 0.35
    return to_display(lin)

def sky(yn):
    t = np.clip(yn, 0, 1)[..., None]
    return to_display(SKY_TOP * (1 - t) + SKY_HORIZON * t)

# The island's rim (an ellipse arc, stone below it) and the ceiling plate (a slightly tilted band at the top).
def island_signed(x, y, s):      # > 0 inside the stone, in pixels (approximately, near the rim)
    cx, cy, a, b = 640 * s, 742 * s, 760 * s, 190 * s
    q = np.sqrt(((x - cx) / a) ** 2 + ((y - cy) / b) ** 2)
    return (1 - q) * b * 0.85

def island_color(x, y, s):
    g = 0.42 + 0.10 * np.sin(x / (37.0 * s)) * 0.3
    return to_display(np.stack([g * 1.05, g * 0.92, g * 0.74], -1))

def ceiling_signed(x, y, s):     # > 0 inside the plate
    edge = (214 + (x / s - 640) * 0.035) * s
    return edge - y

def ceiling_color(x, y, s):
    g = 0.30 + 0.0 * x
    return to_display(np.stack([g * 0.75, g * 0.95, g * 1.15], -1))

FAR = 1e9
Z_ISLAND, Z_CEILING = 500.0, 400.0

def render(x0, y0, w, h, s, mode):
    """Rasterise the crop [x0,x0+w)x[y0,y0+h) of the 720p frame at scale s (1 = 720p, 1.5 = 1080p).
    mode: 'hard', 'msaa4', 'rim' (balls only), 'rimfins' (balls + island/ceiling edges), 'ref' (8x8 supersample)."""
    balls, _ = build_scene()
    W, H = int(round(w * s)), int(round(h * s))
    if mode == 'msaa4':
        offs = [(-0.125, -0.375), (0.375, -0.125), (-0.375, 0.125), (0.125, 0.375)]
    elif mode == 'ref':
        n = 8
        offs = [((i + 0.5) / n - 0.5, (j + 0.5) / n - 0.5) for i in range(n) for j in range(n)]
    else:
        offs = [(0.0, 0.0)]

    acc = np.zeros((H, W, 3))
    depth_out = None
    for (ox, oy) in offs:
        py, px = np.mgrid[0:H, 0:W].astype(np.float64)
        X = x0 * s + px + 0.5 + ox
        Y = y0 * s + py + 0.5 + oy
        col = sky(Y / (720.0 * s))
        dep = np.full((H, W), FAR)
        m = island_signed(X, Y, s) > 0
        col[m] = island_color(X, Y, s)[m]; dep[m] = Z_ISLAND
        m = ceiling_signed(X, Y, s) > 0
        col[m] = ceiling_color(X, Y, s)[m]; dep[m] = Z_CEILING
        for (sx, sy, zv, r, tint) in balls:
            cx, cy, R = sx * s, sy * s, r * s
            dx, dy = X - cx, Y - cy
            d2 = dx * dx + dy * dy
            inside = d2 <= R * R
            if not inside.any():
                continue
            nz = np.sqrt(np.clip(1 - d2 / (R * R), 0, 1))
            z = zv - nz * 0.5
            m = inside & (z < dep)
            c = shade(dx / R, -dy / R, nz, tint)
            col[m] = c[m]; dep[m] = z[m]
        acc += col
        depth_out = dep
    img = acc / len(offs)

    if mode in ('rim', 'rimfins'):
        py, px = np.mgrid[0:H, 0:W].astype(np.float64)
        X = x0 * s + px + 0.5
        Y = y0 * s + py + 0.5
        dep = depth_out
        if mode == 'rimfins':
            # Edge fins of the two static meshes: one pixel outward of the edge, alpha = 1 - distance, depth-tested
            for signed, color, z in ((island_signed, island_color, Z_ISLAND), (ceiling_signed, ceiling_color, Z_CEILING)):
                d = -signed(X, Y, s)
                m = (d > 0) & (d <= 1.0) & (z < dep)
                a = np.clip(1 - d, 0, 1)[..., None]
                c = color(X, Y, s)
                img = np.where(m[..., None], a * c + (1 - a) * img, img)
        # The balls' rims, nearest first (the order the instance buffer is in - the worst case for an unsorted blend)
        for (sx, sy, zv, r, tint) in sorted(balls, key=lambda b: b[2]):
            cx, cy, R = sx * s, sy * s, r * s
            dx, dy = X - cx, Y - cy
            dist = np.sqrt(dx * dx + dy * dy)
            m = (dist > R) & (dist <= R + 1.0) & (zv < dep - 1e-4)
            if not m.any():
                continue
            a = np.clip(1 - (dist - R), 0, 1)[..., None]
            inv = 1.0 / np.maximum(dist, 1e-6)
            c = shade(dx * inv, -dy * inv, np.zeros_like(dist), tint)
            img = np.where(m[..., None], a * c + (1 - a) * img, img)
    return img

def bilinear_up(img, factor):
    H, W, _ = img.shape
    OH, OW = int(round(H * factor)), int(round(W * factor))
    ys = (np.arange(OH) + 0.5) / factor - 0.5
    xs = (np.arange(OW) + 0.5) / factor - 0.5
    y0 = np.clip(np.floor(ys).astype(int), 0, H - 1); y1 = np.clip(y0 + 1, 0, H - 1)
    x0 = np.clip(np.floor(xs).astype(int), 0, W - 1); x1 = np.clip(x0 + 1, 0, W - 1)
    fy = np.clip(ys - np.floor(ys), 0, 1)[:, None, None]; fx = np.clip(xs - np.floor(xs), 0, 1)[None, :, None]
    a = img[y0][:, x0]; b = img[y0][:, x1]; c = img[y1][:, x0]; d = img[y1][:, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy

def to_img(a, zoom):
    im = Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8))
    return im.resize((im.width * zoom, im.height * zoom), Image.NEAREST)

if __name__ == '__main__':
    import sys
    out = sys.argv[1]
    # Two crops of the 720p frame: the cluster's flank against the sky and the ceiling, and its foot over the island
    crops = [(628, 196, 190, 120), (470, 492, 190, 120)]
    panels = [
        ("Dnes na Pi: 720p > 1080p, bez AA", lambda c: bilinear_up(render(*c, 1.0, 'hard'), 1.5)),
        ("MSAA 4x v 720p > 1080p  (+1,8 ms zmereno)", lambda c: bilinear_up(render(*c, 1.0, 'msaa4'), 1.5)),
        ("Lem kouli v 720p > 1080p", lambda c: bilinear_up(render(*c, 1.0, 'rim'), 1.5)),
        ("Lem kouli + hrany ostrova a stropu, 720p > 1080p", lambda c: bilinear_up(render(*c, 1.0, 'rimfins'), 1.5)),
        ("1080p nativne, bez AA", lambda c: render(*c, 1.5, 'hard')),
        ("1080p nativne, lem + hrany", lambda c: render(*c, 1.5, 'rimfins')),
    ]
    zoom = 3
    font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 26)
    for ci, crop in enumerate(crops):
        tiles = []
        for title, fn in panels:
            a = fn(crop)
            im = to_img(a, zoom)
            bar = Image.new('RGB', (im.width, 44), (18, 18, 22))
            ImageDraw.Draw(bar).text((10, 6), title, font=font, fill=(240, 240, 240))
            t = Image.new('RGB', (im.width, im.height + 44)); t.paste(bar, (0, 0)); t.paste(im, (0, 44))
            tiles.append(t)
        cols = 2
        rows = (len(tiles) + cols - 1) // cols
        gap = 10
        sheet = Image.new('RGB', (cols * tiles[0].width + (cols - 1) * gap, rows * tiles[0].height + (rows - 1) * gap), (60, 60, 60))
        for i, t in enumerate(tiles):
            sheet.paste(t, ((i % cols) * (t.width + gap), (i // cols) * (t.height + gap)))
        sheet.save(f"{out}/aa-sim-crop{ci + 1}.png")
        print("saved", ci + 1, sheet.size)
