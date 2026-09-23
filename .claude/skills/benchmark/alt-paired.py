"""Paired reading of a Testbed alt= run (#540): python alt-paired.py <log> [baseline-spec] [drop-cycles]

Groups the [fps] lines into whole cycles of the variant list, takes each variant's difference from the
baseline WITHIN its own cycle, and prints the median difference and how often its sign held — the benchmark
skill's rule for the APU, where two runs are not comparable but two windows a second apart are.
"""
import re, sys, statistics

LINE = re.compile(r'^\[fps\] [\d.,]+ \(([\d.,]+) ms\).*, variant (.+?)\s*$')
path = sys.argv[1]
readings = [(m.group(2), float(m.group(1).replace(',', '.')))
            for m in (LINE.match(l) for l in open(path, encoding='utf-8', errors='replace')) if m]
if not readings:
    sys.exit('no [fps] lines with a variant in ' + path)

order = []
for spec, _ in readings:
    if spec in order: break
    order.append(spec)
base = sys.argv[2] if len(sys.argv) > 2 else order[0]
drop = int(sys.argv[3]) if len(sys.argv) > 3 else 1

# Cut into cycles at every return to the first variant; keep only complete ones.
cycles, cur = [], {}
for spec, ms in readings:
    if spec == order[0] and cur:
        cycles.append(cur); cur = {}
    cur[spec] = ms
if len(cur) == len(order): cycles.append(cur)
cycles = [c for c in cycles if len(c) == len(order)][drop:]

print(f'{path}: {len(cycles)} complete cycles of {len(order)} variants, baseline {base}')
print(f"{'variant':<34}{'median ms':>10}{'diff':>8}{'sign held':>11}")
for spec in order:
    vals = [c[spec] for c in cycles]
    diffs = [c[spec] - c[base] for c in cycles]
    held = sum(1 for d in diffs if (d > 0) == (statistics.median(diffs) > 0)) / len(diffs) if spec != base else 1.0
    print(f'{spec:<34}{statistics.median(vals):10.2f}{statistics.median(diffs):+8.2f}{held * 100:10.0f}%')
