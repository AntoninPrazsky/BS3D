"""A rendered silhouette becomes a LevelGen picture bitmap (#491).

    python silhouette-to-picture.py <silhouette.png> --name Anchor [--width 13] [--max-rows 18] [--top 2] [--side 1]
                                    [--fill 0.5] [--close] [--out-dir <dir>]

Thresholds the image against its own border (dark ink on light or light on dark, either way), crops to the shape,
and resamples it onto the wall's grid by area average, then thresholds the coverage at --fill. The wall's rows sit
1/sqrt(2) apart against a column pitch of 1, so the drawn shape gets sqrt(2) times more rows than a square grid
would give it — the Heart is 14 rows for 13 columns — or it would hang squashed. Never more than --max-rows rows
(GameplayScreen.FRAMED_LEVELS is 18; 20 would quietly make a tall level), always an even number of them (an odd
count moves the drawing a level), and with --top empty rows over the shape and --side empty columns beside it,
because the background is what the wall hangs by.

Writes, into --out-dir:
  <name>.txt          the C# string[] literal in the form Block02_Gallery.cs's Picture() takes ('#' ink, '.' background)
  <name>-preview.png  the wall as it will look: one disc a cell, rows at their real pitch, odd rows shifted half a cell
  <name>-map.json     a map the Testbed opens directly (Testbed.exe <name>-map.json): the wall in a 15x15x18 field,
                      ink as type 1 over the Heart's 4/7 background check, so the bitmap can be photographed in place
It prints the bitmap, the ink count and how many 4-connected pieces the ink fell into — a key whose teeth broke into
islands reads as noise, and the count says so before anyone plays it.
"""
import argparse, json, math, os
import numpy as np
from PIL import Image, ImageDraw

ROW_PITCH = math.sqrt(0.5)     # levels sit 1/sqrt(2) apart against a cell pitch of 1
FIELD_LEVELS = 18              # PICTURE_FIELD_LEVELS in LevelGen, and GameplayScreen.FRAMED_LEVELS
GRID = 15                      # the Gallery's field width
BACKGROUND_CHECK = (4, 7)      # the Heart's background, a 2x2 check
INK_TYPE = 1


def otsu(values):
    """The threshold that best splits a grey image in two — the classic between-class variance maximum."""
    hist, edges = np.histogram(values, bins=256, range=(0.0, 1.0))
    hist = hist.astype(np.float64)
    total = hist.sum()
    weight_b = np.cumsum(hist)
    weight_f = total - weight_b
    centres = (edges[:-1] + edges[1:]) / 2
    sum_b = np.cumsum(hist * centres)
    mean_b = np.divide(sum_b, weight_b, out=np.zeros_like(sum_b), where=weight_b > 0)
    mean_f = np.divide(sum_b[-1] - sum_b, weight_f, out=np.zeros_like(sum_b), where=weight_f > 0)
    between = weight_b * weight_f * (mean_b - mean_f) ** 2
    return float(centres[int(np.argmax(between))])


def ink_mask(image):
    """Ink is whatever is not the background. A cut-out with an alpha channel says so directly; a flat picture is
    split by Otsu's threshold, and the border — the background by construction of the prompt — says which side of
    it is ink, so dark-on-light and light-on-dark both work."""
    if 'A' in image.getbands():
        alpha = np.asarray(image.getchannel('A'), dtype=np.float32) / 255.0
        if alpha.min() < 0.5:
            return alpha >= 0.5
    g = np.asarray(image.convert('L'), dtype=np.float32) / 255.0
    border = np.concatenate([g[0], g[-1], g[:, 0], g[:, -1]])
    background = float(np.median(border))
    split = otsu(g)
    return (g < split) if background > split else (g > split)


def crop(mask, pad=1):
    ys, xs = np.where(mask)
    if len(ys) == 0:
        raise SystemExit('no ink found')
    y0, y1 = max(0, ys.min() - pad), min(mask.shape[0], ys.max() + 1 + pad)
    x0, x1 = max(0, xs.min() - pad), min(mask.shape[1], xs.max() + 1 + pad)
    return mask[y0:y1, x0:x1]


def resample(mask, cols, rows, fill):
    coverage = Image.fromarray((mask * 255).astype(np.uint8)).resize((cols, rows), Image.BOX)
    return np.asarray(coverage, dtype=np.float32) / 255.0 >= fill


def close3(bits):
    pad = np.pad(bits, 1)
    dil = np.zeros_like(bits)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            dil |= pad[1 + dy:1 + dy + bits.shape[0], 1 + dx:1 + dx + bits.shape[1]]
    pad = np.pad(dil, 1, constant_values=True)
    ero = np.ones_like(bits)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ero &= pad[1 + dy:1 + dy + bits.shape[0], 1 + dx:1 + dx + bits.shape[1]]
    return ero


def components(bits):
    seen = np.zeros_like(bits)
    count = 0
    for y in range(bits.shape[0]):
        for x in range(bits.shape[1]):
            if not bits[y, x] or seen[y, x]:
                continue
            count += 1
            stack = [(y, x)]
            seen[y, x] = True
            while stack:
                cy, cx = stack.pop()
                for ny, nx in ((cy - 1, cx), (cy + 1, cx), (cy, cx - 1), (cy, cx + 1)):
                    if 0 <= ny < bits.shape[0] and 0 <= nx < bits.shape[1] and bits[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
    return count


def preview(bitmap, path, cell=28):
    rows, cols = len(bitmap), len(bitmap[0])
    w = int((cols + 0.5) * cell) + cell
    h = int(rows * ROW_PITCH * cell) + cell
    img = Image.new('RGB', (w, h), (40, 44, 52))
    d = ImageDraw.Draw(img)
    r = cell * 0.47
    for row in range(rows):
        for col in range(cols):
            ch = bitmap[row][col]
            check = ((col // 2) + (row // 2)) % 2
            colour = (214, 48, 60) if ch == '#' else ((200, 200, 190) if check == 0 else (150, 150, 140))
            cx = cell * 0.5 + col * cell + (0.5 * cell if row % 2 == 1 else 0) + cell * 0.25
            cy = cell * 0.5 + row * ROW_PITCH * cell + cell * 0.25
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=colour)
    img.save(path)


def testbed_map(bitmap, path):
    rows, cols = len(bitmap), len(bitmap[0])
    depth = rows
    offset = FIELD_LEVELS - depth
    x0 = (GRID - cols) // 2
    zi = GRID // 2
    b = [[[None] * depth for _ in range(GRID)] for _ in range(GRID)]
    for row in range(rows):
        level = depth - 1 - row
        absolute = level + offset
        shift = 0.5 if absolute % 2 == 1 else 0.0
        for col in range(cols):
            ch = bitmap[row][col]
            t = INK_TYPE if ch == '#' else BACKGROUND_CHECK[((col // 2) + (row // 2)) % 2]
            xi = x0 + col
            b[xi][zi][level] = {'x': xi + shift, 'y': absolute * ROW_PITCH, 'z': zi + shift, 't': t}
    json.dump({'sx': GRID, 'sz': GRID, 'l': FIELD_LEVELS, 'b': b}, open(path, 'w', encoding='utf-8'))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('image')
    ap.add_argument('--name', required=True)
    ap.add_argument('--width', type=int, default=13, help='bitmap columns, side margins included (the Heart: 13)')
    ap.add_argument('--max-rows', type=int, default=FIELD_LEVELS)
    ap.add_argument('--top', type=int, default=2, help='empty rows over the shape')
    ap.add_argument('--side', type=int, default=1, help='empty columns beside the shape')
    ap.add_argument('--fill', type=float, default=0.5, help='coverage a cell needs to be ink')
    ap.add_argument('--close', action='store_true', help='3x3 closing after the threshold, to bridge thin parts')
    ap.add_argument('--out-dir', default='.')
    args = ap.parse_args()
    if args.width > GRID:
        raise SystemExit(f'--width {args.width} is wider than the Gallery field ({GRID}); the wall would not fit the map')
    if args.max_rows > FIELD_LEVELS:
        raise SystemExit(f'--max-rows {args.max_rows} is over FRAMED_LEVELS ({FIELD_LEVELS}); the level would turn tall')

    mask = crop(ink_mask(Image.open(args.image)))
    aspect = mask.shape[0] / mask.shape[1]

    drawn_w = args.width - 2 * args.side
    drawn_h = max(1, round(drawn_w * aspect / ROW_PITCH))
    room = args.max_rows - args.top
    if drawn_h > room:
        drawn_h = room
        drawn_w = max(1, round(drawn_h * ROW_PITCH / aspect))
    total_w = drawn_w + 2 * args.side
    total_h = args.top + drawn_h
    if total_h % 2 == 1:
        total_h += 1 if total_h < args.max_rows else -1
        if total_h - args.top < drawn_h:
            drawn_h = total_h - args.top

    bits = resample(mask, drawn_w, drawn_h, args.fill)
    if args.close:
        bits = close3(bits)

    bitmap = []
    for row in range(total_h):
        line = ['.'] * total_w
        r = row - args.top
        if 0 <= r < drawn_h:
            for c in range(drawn_w):
                if bits[r, c]:
                    line[args.side + c] = '#'
        bitmap.append(''.join(line))

    os.makedirs(args.out_dir, exist_ok=True)
    literal = f'        private static readonly string[] {args.name.upper()} =\n        {{\n' + \
        ''.join(f'            "{line}",\n' for line in bitmap) + '        };\n'
    open(os.path.join(args.out_dir, f'{args.name}.txt'), 'w', encoding='utf-8', newline='\n').write(literal)
    preview(bitmap, os.path.join(args.out_dir, f'{args.name}-preview.png'))
    testbed_map(bitmap, os.path.join(args.out_dir, f'{args.name}-map.json'))

    ink = int(bits.sum())
    print('\n'.join(bitmap))
    print(f'{args.name}: {total_w} x {total_h} ({drawn_w} x {drawn_h} drawn, source aspect {aspect:.2f}), '
          f'{ink} ink cells ({ink / bits.size * 100:.0f} % of the drawn box), {components(bits)} piece(s)')


if __name__ == '__main__':
    main()
