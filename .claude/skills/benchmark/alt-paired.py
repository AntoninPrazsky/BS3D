"""Paired reading of a Testbed alt= run (#540): python alt-paired.py <log> [baseline-spec] [drop-cycles] [--unsettled]

Groups the [fps] lines into whole cycles of the variant list, takes each variant's difference from the
baseline WITHIN its own cycle, and prints the median difference and how often its sign held — the benchmark
skill's rule for the APU, where two runs are not comparable but two windows a second apart are.

Only windows that SETTLED are read (#1129). The CPU runs about forty frames ahead of the GPU in an uncapped
Testbed, so a window opened on its switch began with the variant before's frames: half the window at 13 ms a
frame, which pulled every reading towards the variant listed before it and shrank a two-variant toggle's
difference towards nothing. Since #1129 the Testbed holds each window shut until the GPU has drawn the variant
before and ends the line with "settled after <n> frames" before "variant". A cycling log without it (an older
build) is refused, loudly; --unsettled reads it anyway, to compare against one, and says so on the output.
"""
import re, sys, statistics

args = [a for a in sys.argv[1:] if a != '--unsettled']
unsettled_ok = len(args) != len(sys.argv) - 1

LINE = re.compile(r'^\[fps\] [\d.,]+ \(([\d.,]+) ms\)(.*), variant (.+?)\s*$')
SETTLED = re.compile(r', settled after (\d+) frames$')
path = args[0]
readings, settles, unsettled = [], [], 0
for l in open(path, encoding='utf-8', errors='replace'):
    m = LINE.match(l)
    if not m:
        continue
    s = SETTLED.search(m.group(2))
    if s:
        settles.append(int(s.group(1)))
    else:
        unsettled += 1
    readings.append((m.group(3), float(m.group(1).replace(',', '.'))))
if not readings:
    sys.exit('no [fps] lines with a variant in ' + path)

order = []
for spec, _ in readings:
    if spec in order: break
    order.append(spec)

if unsettled and len(order) > 1:
    if not unsettled_ok:
        sys.exit(f'REFUSED: {unsettled} of the {len(readings)} cycling [fps] lines in {path} do not say "settled after <n> frames".\n'
                 'Their windows opened on the switch, with about forty frames of the variant before at the front (#1129):\n'
                 'each reading leans towards the variant listed before it and a toggle\'s difference shrinks towards nothing.\n'
                 'Re-run on a Testbed built since #1129, or pass --unsettled to read it as it is.')
    print(f'WARNING --unsettled: {unsettled} windows opened on their switch; every figure below carries the variant before (#1129)')

base = args[1] if len(args) > 1 else order[0]
drop = int(args[2]) if len(args) > 2 else 1

# Cut into cycles at every return to the first variant; keep only complete ones.
cycles, cur = [], {}
for spec, ms in readings:
    if spec == order[0] and cur:
        cycles.append(cur); cur = {}
    cur[spec] = ms
if len(cur) == len(order): cycles.append(cur)
cycles = [c for c in cycles if len(c) == len(order)][drop:]

settled = f', each window settled after {min(settles)}-{max(settles)} frames' if settles else ''
print(f'{path}: {len(cycles)} complete cycles of {len(order)} variants, baseline {base}{settled}')
print(f"{'variant':<34}{'median ms':>10}{'diff':>8}{'sign held':>11}")
for spec in order:
    vals = [c[spec] for c in cycles]
    diffs = [c[spec] - c[base] for c in cycles]
    held = sum(1 for d in diffs if (d > 0) == (statistics.median(diffs) > 0)) / len(diffs) if spec != base else 1.0
    print(f'{spec:<34}{statistics.median(vals):10.2f}{statistics.median(diffs):+8.2f}{held * 100:10.0f}%')
