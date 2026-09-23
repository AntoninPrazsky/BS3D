"""#540: read every run log, check its [fps] line says what was asked for, print medians."""
import re, sys, glob, os, statistics

DROP = int(os.environ.get('DROP', '8'))
FPS = re.compile(r'^\[fps\] ([\d,.]+) \(([\d,.]+) ms\) . (\w+), dome (\d+), ssaa (\d)x, (\w+), msaa (\d)x(?: \(asked (\d)\))?, detail (\w+), (\d+x\d+)')
rows = []
for path in sorted(glob.glob(os.path.join(sys.argv[1], '*.log'))):
    name = os.path.basename(path)
    kind, target, tier = name.split('-')[0].split('_')
    lines = open(path, encoding='utf-8', errors='replace').read().splitlines()
    build = [l for l in lines if l.startswith('[build]')]
    q = [l for l in lines if l.startswith('[quality]')]
    fps = [FPS.match(l) for l in lines if l.startswith('[fps]')]
    bad = [l for l in lines if l.startswith('[fps]') and not FPS.match(l)]
    fps = [m for m in fps if m]
    kept = fps[DROP:]
    if not kept:
        print('NO DATA', name, len(fps), bad[:1]); continue
    ms = sorted(float(m.group(2).replace(',', '.')) for m in kept)
    last = kept[-1]
    scene, dome, ssaa, t, msaa, detail, size = last.group(3), last.group(4), last.group(5), last.group(6), last.group(7), last.group(9), last.group(10)
    mixed = len({(m.group(3), m.group(6), m.group(10)) for m in kept}) > 1
    rows.append(dict(kind=kind, target=target, tier=tier, scene=scene, dome=dome, ssaa=ssaa, t=t, msaa=msaa, detail=detail,
                     size=size, n=len(ms), med=statistics.median(ms), lo=ms[0], hi=ms[-1],
                     p10=ms[int(len(ms) * 0.1)], p90=ms[min(len(ms) - 1, int(len(ms) * 0.9))],
                     build=[b.split('set ')[1].split(',')[0] if 'set ' in b else b.split()[-1] for b in build],
                     flags=('MIXED ' if mixed else '') + ('QUALITY ' if q else '') + ('' if size == '1600x900' else 'SIZE ') + ('' if t == tier else 'TIER ')))

order = {'high': 0, 'medium': 1, 'low': 2}
print(f"{'target':<12}{'scene':<10}{'dome':>5} {'tier':<7}{'ssaa':>4}{'msaa':>5} {'detail':<8}{'n':>3} {'median':>7} {'p10':>6} {'p90':>6} {'min':>6} {'max':>6}  build  flags")
for r in sorted(rows, key=lambda r: (r['kind'], r['target'], order[r['tier']])):
    print(f"{r['target']:<12}{r['scene']:<10}{r['dome']:>5} {r['tier']:<7}{r['ssaa']:>4}{r['msaa']:>5} {r['detail']:<8}{r['n']:>3} {r['med']:7.2f} {r['p10']:6.2f} {r['p90']:6.2f} {r['lo']:6.2f} {r['hi']:6.2f}  {'/'.join(r['build'][1:])}  {r['flags']}")
