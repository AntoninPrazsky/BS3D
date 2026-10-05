# The Potato ball's pixel before and after #804's "constants to the CPU": the old shader's arithmetic against the new
# split (DrawPotato's uniforms + the new shader), on random inputs. They must agree to rounding.
import math, random

def v(*a): return list(a)
def add(a, b): return [x + y for x, y in zip(a, b)]
def sub(a, b): return [x - y for x, y in zip(a, b)]
def mul(a, b): return [x * y for x, y in zip(a, b)] if isinstance(b, list) else [x * b for x in a]
def dot(a, b): return sum(x * y for x, y in zip(a, b))
def norm(a):
    l = math.sqrt(dot(a, a)); return [x / l for x in a]
def lerp(a, b, t): return [x + (y - x) * t for x, y in zip(a, b)] if isinstance(a, list) else a + (b - a) * t
def sat(x): return max(0.0, min(1.0, x))
def s2l(c): return [x * (x * (x * 0.305306011 + 0.682171111) + 0.012522878) for x in c]
def heartbeat(t):
    ph = t - math.floor(t)
    a = (ph - 0.10) * 13.0; b = (ph - 0.29) * 15.0
    return sat(math.exp(-a * a) + 0.55 * math.exp(-b * b))

def old(U, P, N, occ4, ripple, sceneD, sceneS, facing_clamp):
    eye = norm(sub(U['Eye'], P)); n = norm(N)
    diffuse = [0, 0, 0]; specular = [0, 0, 0]
    def addlight(L, ld, ls):
        nonlocal diffuse, specular
        dl = dot(n, L); lit = 1.0 if dl >= 0 else 0.0
        diffuse = add(diffuse, mul(ld, dl * lit))
        dh = max(dot(n, norm(add(L, eye))), 0)
        specular = add(specular, mul(ls, (dh * lit) ** U['SpecPower']))
    addlight(norm(sub(U['Key'], P)), U['L0d'], U['L0s'])
    addlight(mul(U['L1dir'], -1), U['L1d'], U['L1s'])
    addlight(mul(U['L2dir'], -1), U['L2d'], U['L2s'])
    diffuse = add(mul(diffuse, U['Strength']), sceneD); specular = add(mul(specular, U['Strength']), sceneS)
    o = sat(occ4[3] - 1.1 * max(0, dot(n, occ4[:3])))
    gp = sat(1 - (P[1] - U['GroundH']) / 2.0)
    occlusion = sat(o - 0.55 * gp * sat(-n[1]))
    burial = sat((0.45 - occ4[3]) / 0.35)
    dOcc = lerp(0.6, 1.0, occlusion) * lerp(1.0, 0.4, burial)
    def sky(d): return lerp(U['Ground'], U['Sky'], d[1] * 0.5 + 0.5)
    primary = s2l(U['Primary'])
    crust = add(mul(primary, U['CrustTint'] * U['CrustDark']), [U['CrustDark'] * (1 - U['CrustTint'])] * 3)
    alpha = U['Diffuse'][3]
    covered = mul(add(add(mul(mul(diffuse, s2l(U['Diffuse'][:3])), dOcc), mul(mul(sky(n), s2l(U['Ambient'])), occlusion)), s2l(U['Emissive'])), crust)
    ls = s2l(U['Specular'])
    rough = math.sqrt(2.0 / (U['SpecPower'] + 2.0))
    refl = sub(mul(eye, -1), mul(n, 2 * dot(mul(eye, -1), n)))
    env = lerp(sky(refl), mul(add(U['Sky'], U['Ground']), 0.5), sat(rough))
    r0 = lerp(mul(ls, 0.04), ls, U['Metal'])
    facing = max(sat(dot(n, eye)), facing_clamp)
    fres = add(r0, mul(sub([max(1.0, x) for x in r0], r0), (1 - facing) ** 5))
    glints = mul(add(mul(specular, ls), mul(mul(env, fres), U['SAS'])), occlusion)
    covered = add(covered, mul(glints, U['SAW'] * alpha))
    added = add(mul(glints, 1 - U['SAW']), U['EmissiveTint'])
    shaded = add(covered, added)
    beat = heartbeat(U['PulseTime'] * U['PulseSpeed'] - dot(P, U['PulseDir']) / max(U['PulseWave'], 1e-4))
    shaded = add(shaded, mul(primary, U['ES'] * U['SE'] * ((1 - U['PD']) * occlusion * occlusion + U['PD'] * beat)))
    peak = max(primary)
    if U['Glow'] > 0:
        hue = [sat(x / max(peak, 1e-3)) ** 1.7 for x in primary]
        em = lerp(0.18, 1.0, sat(dot(primary, [0.2126, 0.7152, 0.0722])))
        shaded = add(shaded, mul(hue, U['Glow'] * em * lerp(1 - U['PD'], 1, beat) * U['SE'] * occlusion))
    amount = abs(ripple) * (1.0 if U['RippleStrength'] >= 1e-4 else 0.0)
    lit = add(shaded, mul(lerp([x / max(peak, 1e-3) for x in primary], [1, 1, 1], 0.5), U['RippleStrength'] * amount))
    alarmed = lerp(shaded, mul(U['Alarm'], 1.7), amount * 0.95)
    return alarmed if ripple < 0 else lit

def cpu(U):
    C = {}
    alpha = U['Diffuse'][3]
    C['DiffuseLin'] = s2l(U['Diffuse'][:3]); C['alpha'] = alpha
    ambient = s2l(U['Ambient']); spec = s2l(U['Specular'])
    C['EmissiveLin'] = s2l(U['Emissive']); C['SpecLin'] = spec
    mid = mul(add(U['Sky'], U['Ground']), 0.5); tilt = mul(sub(U['Sky'], U['Ground']), 0.5)
    C['AmbMid'] = mul(mid, ambient); C['AmbTilt'] = mul(tilt, ambient)
    rough = sat(math.sqrt(2.0 / (U['SpecPower'] + 2.0)))
    C['EnvMid'] = mid; C['EnvTilt'] = mul(tilt, 1 - rough)
    r0 = lerp(mul(spec, 0.04), spec, U['Metal'])
    C['Refl'] = mul(r0, U['SAS']); C['Rise'] = mul(sub([max(1.0, x) for x in r0], r0), U['SAS'])
    primary = s2l(U['Primary'])
    lava = U['Glow'] > 0
    C['Crust'] = add(mul(primary, U['CrustTint'] * U['CrustDark']), [U['CrustDark'] * (1 - U['CrustTint'])] * 3)
    em = mul(primary, U['ES'] * U['SE'])
    C['EmStill'] = mul(em, 1 - U['PD']); C['EmBeat'] = mul(em, U['PD'])
    peak = max(primary); hue = [x / max(peak, 1e-3) for x in primary]
    glow = [0, 0, 0]
    if lava:
        cut = [sat(x) ** 1.7 for x in hue]
        lum = sat(dot(primary, [0.2126, 0.7152, 0.0722]))
        glow = mul(cut, U['Glow'] * lerp(0.18, 1.0, lum) * U['SE'])
    C['GlowStill'] = mul(glow, 1 - U['PD']); C['GlowBeat'] = mul(glow, U['PD'])
    rip = 1.0 if U['RippleStrength'] >= 1e-4 else 0.0
    C['Flash'] = mul(lerp(hue, [1, 1, 1], 0.5), U['RippleStrength'] * rip)
    C['Alarm'] = mul(U['Alarm'], 1.7) + [0.95 * rip]
    C['Phase'] = mul(U['PulseDir'], 1 / max(U['PulseWave'], 1e-4)) + [U['PulseTime'] * U['PulseSpeed']]
    return C

def new(U, C, P, N, occ4, ripple, sceneD, sceneS, facing_clamp):
    eye = norm(sub(U['Eye'], P)); n = norm(N)
    diffuse = [0, 0, 0]; specular = [0, 0, 0]
    def addlight(L, ld, ls):
        nonlocal diffuse, specular
        dl = dot(n, L); lit = 1.0 if dl >= 0 else 0.0
        diffuse = add(diffuse, mul(ld, dl * lit))
        dh = max(dot(n, norm(add(L, eye))), 0)
        specular = add(specular, mul(ls, (dh * lit) ** U['SpecPower']))
    addlight(norm(sub(U['Key'], P)), U['L0d'], U['L0s'])
    addlight(mul(U['L1dir'], -1), U['L1d'], U['L1s'])
    addlight(mul(U['L2dir'], -1), U['L2d'], U['L2s'])
    diffuse = add(mul(diffuse, U['Strength']), sceneD); specular = add(mul(specular, U['Strength']), sceneS)
    o = sat(occ4[3] - 1.1 * max(0, dot(n, occ4[:3])))
    gp = sat(1 - (P[1] - U['GroundH']) / 2.0)
    occlusion = sat(o - 0.55 * gp * sat(-n[1]))
    burial = sat((0.45 - occ4[3]) / 0.35)
    dOcc = lerp(0.6, 1.0, occlusion) * lerp(1.0, 0.4, burial)
    covered = mul(add(add(mul(mul(diffuse, C['DiffuseLin']), dOcc), mul(add(C['AmbMid'], mul(C['AmbTilt'], n[1])), occlusion)), C['EmissiveLin']), C['Crust'])
    te = dot(n, eye)
    env = add(C['EnvMid'], mul(C['EnvTilt'], 2 * te * n[1] - eye[1]))
    facing = max(sat(te), facing_clamp)
    g = 1 - facing; g2 = g * g
    fres = add(C['Refl'], mul(C['Rise'], g2 * g2 * g))
    glints = mul(add(mul(specular, C['SpecLin']), mul(env, fres)), occlusion)
    covered = add(covered, mul(glints, U['SAW'] * C['alpha']))
    added = add(mul(glints, 1 - U['SAW']), U['EmissiveTint'])
    shaded = add(covered, added)
    beat = heartbeat(C['Phase'][3] - dot(P, C['Phase'][:3]))
    shaded = add(shaded, add(mul(add(mul(C['EmStill'], occlusion), C['GlowStill']), occlusion), mul(add(C['EmBeat'], mul(C['GlowBeat'], occlusion)), beat)))
    amount = abs(ripple)
    lit = add(shaded, mul(C['Flash'], amount))
    alarmed = lerp(shaded, C['Alarm'][:3], amount * C['Alarm'][3])
    return alarmed if ripple < 0 else lit

random.seed(804)
r = random.random
def rv(lo=0.0, hi=1.0): return [lo + (hi - lo) * r() for _ in range(3)]
worst = 0.0
for trial in range(20000):
    lava = r() < 0.3
    U = dict(Eye=rv(-30, 30), Key=rv(-60, 60), L0d=rv(0, 2), L0s=rv(0, 2), L1dir=norm(rv(-1, 1)), L1d=rv(0, 1), L1s=rv(0, 1),
             L2dir=norm(rv(-1, 1)), L2d=rv(0, 1), L2s=rv(0, 1), Strength=0.3 + r() * 1.5, GroundH=-2 + r() * 4,
             Sky=rv(0, 2), Ground=rv(0, 1), Primary=rv(), Diffuse=rv() + [1.0], Ambient=rv(), Emissive=rv(0, 0.2), Specular=rv(),
             SpecPower=4 + r() * 120, Metal=r(), SAS=r() * 2, SAW=r(), EmissiveTint=rv(0, 0.3),
             CrustTint=0.62 if lava else 1.0, CrustDark=0.16 if lava else 1.0, Glow=(2.6 * 0.3 if lava else 0.0),
             ES=r() * 1.5, SE=(0.0 if r() < 0.2 else 1.0), PD=r() * 0.6, PulseTime=r() * 500, PulseSpeed=1.1, PulseDir=norm(rv(-1, 1)),
             PulseWave=4 + r() * 20, RippleStrength=(0.0 if r() < 0.2 else r() * 2), Alarm=rv())
    P = rv(-8, 8); N = norm(rv(-1, 1)); occ4 = norm(rv(-1, 1)) + [r()]
    ripple = (r() * 2 - 1) if r() < 0.6 else 0.0
    sceneD = rv(0, 0.5); sceneS = rv(0, 0.5); fc = r() * 0.2
    a = old(U, P, N, occ4, ripple, sceneD, sceneS, fc)
    b = new(U, cpu(U), P, N, occ4, ripple, sceneD, sceneS, fc)
    e = max(abs(x - y) / max(1.0, abs(x)) for x, y in zip(a, b))
    worst = max(worst, e)
print('worst relative difference over 20000 random pixels: %.3e' % worst)
assert worst < 1e-9, 'the two disagree'
print('AGREE')
