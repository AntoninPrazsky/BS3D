"""
Measures music loops against a reference family, the way the offending recordings of a family are named (#841, #824,
#706): the owner's ruling of 2026-10-11 is that which recordings offend is decided by measurement - tunelessness, the
low-mid share, energy above 6 kHz - with the desert's `ember` family as the model.

    python measure-music.py Game/Music/ember*.ogg Game/Music/pulse*.ogg [--reference ember] [--json out.jsonl]
                            [--sidecars Research/AI-Music] [--key "A minor"] [--bpm 126] [--gap-db 20] [--similar 0.90]

Every file is measured on its mono mix; the reference family's files (stem starting with --reference) must be among
the inputs, and set the range the others are held to. The first table bolds a figure outside it: a band share under
the reference's lowest, more above 6 kHz than its highest, a less tonal mid band, fewer held-note changes, more tempo
wander or more clipping than any reference recording, and a mid flatness over 1.5x its highest. A second table holds
the arrangement columns (short gaps, repetition, held notes out of key), bolding what lies over the reference's highest.

Reads .wav (float32 or 16-bit, through loop_crossfade's reader) and, where `soundfile` is installed (the generate-sfx
venv has it), .ogg. Columns:
  - band shares from a Welch spectrum (8192 points), of everything from 20 Hz up;
  - tonal mid: the harmonic part's share of 300 Hz-2 kHz, median-filter HPSS on an 8192-point STFT (hop 2048,
    kernel 17), and that band's mean per-frame spectral flatness (0 = tones, 1 = noise);
  - held-note changes a second: the dominant harmonic-summation pitch (f0 196-1175 Hz by semitone) held for at least
    85 ms, and how often it moves to another note;
  - tempo wander: the spread of the local beat period, autocorrelation of a spectral-flux envelope (100 frames a
    second) in 8 s windows, searched within 10 % of the whole loop's period;
  - clipped runs: 3 or more consecutive samples at or above 0.98 of full scale, any channel;
  - pitch wobble / detune, in cents: the median spread inside a held note, and the median distance of a note's mean
    from the loop's own 12-TET grid - what "drunk" would be if it were in the pitch. A synthetic +-25-cent wow on
    ember-punk-01 moved them from 2.0 / 2.1 to 6.4 / 15.3 (2026-10-13), so they can fire;
  - the level step across the loop's wrap (250 ms either side) and its percentile among the loop's own 250 ms steps.

The second table (#913, #910, #914):
  - short gaps: 20 ms frame RMS every 10 ms, the loudness around a frame its median over 2 s (circular: a loop plays
    on), a gap a run of 30-500 ms whose frames sit more than --gap-db (20) under it. Reported as gaps a minute, the
    deepest (dB under its surroundings; digital silence reads about 100) and the longest (ms; the edges are only known
    to a 10 ms hop). Runs over 500 ms are not counted: a silence of 0.5-2 s is named by no check (the dropout check
    wants 2 s, MusicBake refuses 6 s under -35 dBFS), and a shallow breakdown is not a gap;
  - repetition: bars from --bpm when given (it overrides every file's sidecar, so a run mixing files with and without
    one is re-barred whole), else the loop cut's own log in the sidecar (Research/AI-Music/theme-<stem>.json, "N bars
    at B bpm", when it describes this very file), else the recording's own beat period folded into 90-180 bpm and the
    bar count (four beats a bar) whose length the flux envelope repeats at most; the table says which. Each
    bar is one unit vector: the harmonic part's pitch-class energy (120 Hz-2 kHz) in each eighth of the bar, so which
    notes in which order, drums mostly masked out by the HPSS. Two bars are near-identical at a cosine of --similar
    (0.90); a pattern of 1, 2, 4 or 8 bars repeats for as long as each later bar resembles its counterpart in the
    first statement and it comes round at least once in full. "Opening repeat" is that run from bar 0 (1 = the first
    bar is never repeated at once), "longest repeat" the longest from any bar. Over a 4/4 grid: a waltz or a tempo
    folded the wrong octave (the estimate only) changes what a bar is, and the thresholds shift with it;
  - out of key: the share of held notes (the held-note tracker above) whose pitch class is outside the asked key's
    scale (natural minor or major), the key from the sidecar's request.keyscale or --key, with the note counts, and the
    share outside the best-fitting major/minor scale of the twelve (a recording that keeps to a key other than the
    asked one is out of key but not out of best scale). The JSON row lists where in the loop each out-of-key note
    starts. Only the dominant pitch 196-1175 Hz is a note: a clash of two voices or a bass line is invisible to it.
All three can fire (2026-10-17). On ember-punk-03, which has no gaps of its own, four mutes of 40/80/150/300 ms read as
4 gaps (longest 290 ms, deepest 105 dB), ten of 80 ms as 10, four dips of 30 dB as 4 (33 dB) and four dips of 10 dB, or
one silence of 700 ms, as none. The first two bars of ember-garage played 2/4/8/16 times before the rest of the loop
moved "opening repeat" from 1 bar to 4/8/16/32, one bar played 8 times to 8, with the bar estimated or given by --bpm.
A 15 s section of ember-punk-03 (E minor) transposed by varispeed moved the out-of-key share from 0.0 % to 15.2 %
(+1 semitone), 20.1 % (+6) and 12.5 % (+0.5), +12 and +7 left it at 0.0 %, and the whole loop at +1 read 74.2 % out of
the asked key and 0.0 % out of its best scale.

On the magma family this reproduces #841's band, clipping and seam figures exactly and its tonal mid within 7 points;
its flatness and held-note changes are the same idea with different details, so compare them only within one run.
A tune proxy measures harmonic content and pitch movement, not whether a melody pleases: nothing here replaces the ear.
"""

import argparse
import json
import re
import sys
from pathlib import Path

import numpy as np
from numpy.lib.stride_tricks import sliding_window_view as windows

sys.path.insert(0, str(Path(__file__).resolve().parent))
from loop_crossfade import read_wav  # noqa: E402 - beside this file, whatever the working directory

WELCH_SIZE = 8192
HPSS_HOP = 2048
HPSS_KERNEL = 17
HPSS_TOP_HZ = 4200
PITCH_LOW_HZ, PITCH_HIGH_HZ = 196.0, 1175.0
HARMONICS = 5
HELD_SECONDS = 0.085
FLUX_FPS = 100
TEMPO_WINDOW_S, TEMPO_HOP_S = 8, 2
CLIP_LEVEL, CLIP_RUN = 0.98, 3
SEAM_SECONDS = 0.25
FLATNESS_SLACK = 1.5
GAP_WINDOW_S, GAP_HOP_S, GAP_MEDIAN_S = 0.020, 0.010, 2.0
GAP_DROP_DB = 20.0
GAP_MIN_S, GAP_MAX_S = 0.030, 0.500
BEATS_PER_BAR = 4
SEGMENTS_PER_BAR = 8
CHROMA_LOW_HZ, CHROMA_HIGH_HZ = 120.0, 2000.0
SIMILAR = 0.90
PATTERN_BARS = (1, 2, 4, 8)
SIDECARS = Path(__file__).resolve().parents[2] / "Research" / "AI-Music"
MAJOR, MINOR = (0, 2, 4, 5, 7, 9, 11), (0, 2, 3, 5, 7, 8, 10)
NOTE_NAMES = ("C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B")


def read_audio(path):
    if path.suffix.lower() == ".wav":
        samples, fmt, _ = read_wav(str(path))
        return samples, fmt["rate"]
    try:
        import soundfile
    except ImportError:
        raise SystemExit(f"{path}: reading .ogg needs soundfile (the generate-sfx venv has it); or pass the .wav master")
    samples, rate = soundfile.read(str(path), dtype="float32", always_2d=True)
    return samples, rate


def stft(signal, size, hop):
    return np.fft.rfft(windows(signal, size)[::hop] * np.hanning(size), axis=1)


def load_sidecar(directory, name):
    """The recording's sidecar and whether it is the recording's own. A candidate master (`magma-c3`) falls back to its
    brief's sidecar (`magma`), good for the key; only an exact match may speak for the tempo and the loop cut."""
    for stem, exact in ((name, True), (re.sub(r"-c\d+$", "", name), False)):
        path = directory / f"theme-{stem}.json"
        if path.is_file() and (exact or stem != name):
            return json.loads(path.read_text(encoding="utf-8")), exact
    return None, False


def parse_key(text):
    """'A minor', 'Eb major', 'F#m' -> (scale pitch classes, label, the major scale's tonic with the same notes), or None."""
    m = re.fullmatch(r"\s*([A-Ga-g])([#b]?)\s*(major|maj|minor|min|m)?\s*", text or "")
    if not m:
        return None
    tonic = ({"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}[m[1].upper()] + {"#": 1, "b": -1, "": 0}[m[2]]) % 12
    minor = (m[3] or "major").lower() in ("minor", "min", "m")
    return (tuple((tonic + step) % 12 for step in (MINOR if minor else MAJOR)),
            f"{NOTE_NAMES[tonic]} {'minor' if minor else 'major'}", (tonic + 3 if minor else tonic) % 12)


def out_of_key(note_pitch_classes, key):
    """The share of held notes outside the asked key's scale (None with no key or no notes), and outside the best-fitting
    major/minor scale of the twelve - which says whether the notes keep to any one key even when it is not the asked one.
    A tie between scales goes to the asked key's."""
    pcs = np.asarray(note_pitch_classes)
    if not len(pcs):
        return None, None, None
    outside = lambda scale: float(np.mean(~np.isin(pcs, scale)))
    best_tonic = min(range(12), key=lambda t: (outside([(t + s) % 12 for s in MAJOR]), key is None or t != key[2], t))
    best = outside([(best_tonic + s) % 12 for s in MAJOR])
    best_label = f"{NOTE_NAMES[best_tonic]} major/{NOTE_NAMES[(best_tonic + 9) % 12]} minor"
    return (outside(key[0]) if key else None), best, best_label


def short_gaps(mono, rate, drop_db):
    """Sub-second stretches that fall far under the music around them: 20 ms frame RMS every 10 ms, the loudness around
    each frame its median over 2 s (circular - a loop plays on), a gap a run of GAP_MIN_S..GAP_MAX_S whose frames sit more
    than drop_db under that. Longer runs are not counted: breakdowns and holes belong to the other checks, and a silence
    of 0.5-2 s to none."""
    win, hop = int(GAP_WINDOW_S * rate), int(GAP_HOP_S * rate)
    frames = windows(mono, win)[::hop]
    level = 10 * np.log10(np.einsum("ij,ij->i", frames, frames) / win + 1e-12)
    half = int(GAP_MEDIAN_S / GAP_HOP_S) // 2
    local = np.median(windows(np.pad(level, half, mode="wrap"), 2 * half + 1), axis=-1)
    depth = local - level
    under = depth > drop_db
    count, deepest, longest = 0, 0.0, 0.0
    if under.any() and not under.all():
        first = int(np.argmin(under))  # start on a frame that is not under, so a run over the wrap is one run
        under, depth = np.roll(under, -first), np.roll(depth, -first)
        edges = np.diff(np.concatenate(([0], under.astype(np.int8), [0])))
        for a, b in zip(np.nonzero(edges == 1)[0], np.nonzero(edges == -1)[0]):
            # k frames wholly under cover (k + 1) hops, and the gap's edges fall anywhere within a hop either side of them
            length = (b - a + 2) * GAP_HOP_S
            if GAP_MIN_S <= length <= GAP_MAX_S:
                count += 1
                deepest, longest = max(deepest, float(depth[a:b].max())), max(longest, length)
    return dict(gaps=count, gaps_per_min=round(count * 60 / (len(mono) / rate), 1),
                gap_deepest_db=round(deepest), gap_longest_ms=round(1000 * longest))


def bar_from_sidecar(duration, sidecar):
    """Seconds per bar from the loop cut's own log ("38.00 bars at 150.0 bpm") when it describes this very file."""
    log = sidecar.get("loop_log") if sidecar else None
    for line in reversed(log if isinstance(log, list) else [log or ""]):
        m = re.search(r"([\d.]+) bars at ([\d.]+) bpm", str(line))
        if m:
            bars, bpm = float(m[1]), float(m[2])
            if abs(bars * 60 * BEATS_PER_BAR / bpm - duration) < 0.015 * duration:
                return duration / round(bars) if abs(bars - round(bars)) < 0.15 else 60 * BEATS_PER_BAR / bpm
            return None
    return None


def bar_from_flux(flux, beat, duration):
    """Seconds per bar from the recording itself: four beats of the beat period (folded into 90-180 bpm) say how many
    whole bars the loop holds, and of the bar counts near that the one whose bar length the spectral-flux envelope
    repeats at most (so a loop's length divides into exact bars; the beat period alone is too coarse over 40 bars)."""
    while beat < 60 / 180:
        beat *= 2
    while beat > 60 / 90:
        beat /= 2
    n = len(flux)
    centred = flux - flux.mean()
    ac = np.fft.irfft(np.abs(np.fft.rfft(centred, 2 * n)) ** 2)[:n] / (n - np.arange(n))
    guess = round(duration / (BEATS_PER_BAR * beat))
    scored = [(np.interp(duration / bars * FLUX_FPS, np.arange(n), ac), bars)
              for bars in range(max(2, guess - 2), guess + 3) if duration / bars * FLUX_FPS < n - 1]
    return duration / max(scored)[1] if scored else BEATS_PER_BAR * beat


def bar_features(tonal, bins, rate, bar_seconds, duration):
    """One unit vector a bar: the harmonic part's pitch-class energy (120 Hz-2 kHz, A = 440 Hz) in each eighth of the
    bar, so it says which notes in which order and not only which notes. Drums are mostly masked out by the HPSS."""
    bars = int(duration / bar_seconds + 0.02)
    sel = (bins >= CHROMA_LOW_HZ) & (bins < CHROMA_HIGH_HZ)
    classes = (np.round(12 * np.log2(bins[sel] / 440.0)).astype(int) + 9) % 12
    chroma = tonal[:, sel] @ np.eye(12, dtype=np.float32)[classes]
    centre = (np.arange(len(tonal)) * HPSS_HOP + WELCH_SIZE / 2) / rate
    segment = (centre / (bar_seconds / SEGMENTS_PER_BAR)).astype(int)
    keep = segment < bars * SEGMENTS_PER_BAR
    counts = np.maximum(np.bincount(segment[keep], minlength=bars * SEGMENTS_PER_BAR), 1)
    sums = np.stack([np.bincount(segment[keep], chroma[keep, c], bars * SEGMENTS_PER_BAR) for c in range(12)], axis=1)
    features = (sums / counts[:, None]).reshape(bars, SEGMENTS_PER_BAR * 12)
    return features / np.maximum(np.linalg.norm(features, axis=1, keepdims=True), 1e-12)


def pattern_run(similarity, start, period, threshold):
    """Bars from `start` over which one pattern of `period` bars keeps coming round: every bar after the first statement
    must resemble its counterpart in it. 0 unless the pattern comes round at least once more in full."""
    n = len(similarity)
    length = n - start
    for i in range(start + period, n):
        if similarity[i, start + (i - start) % period] < threshold:
            length = i - start
            break
    return length if length >= 2 * period else 0


def repetition(features, bar_seconds, threshold):
    """How long the music stays on near-identical material. A bar resembles another when the cosine of their features
    is at least `threshold`; a pattern of 1, 2, 4 or 8 bars repeats while each later bar resembles its counterpart in
    the first statement. `open` is the run from bar 0 (a bar's first change), `run` the longest from any bar."""
    similarity = features @ features.T
    bars = len(features)
    runs = [(pattern_run(similarity, s, p, threshold), s) for s in range(bars) for p in PATTERN_BARS]
    opening = max([1] + [pattern_run(similarity, 0, p, threshold) for p in PATTERN_BARS])
    longest, at = max(runs + [(1, 0)], key=lambda run: (run[0], -run[1]))
    return dict(bars=bars, bar_s=round(bar_seconds, 3), open_bars=opening, open_s=round(opening * bar_seconds, 1),
                run_bars=longest, run_s=round(longest * bar_seconds, 1), run_at_bar=at)


def measure(samples, rate, name, sidecar=None, exact=False, key=None, bpm=None, gap_db=GAP_DROP_DB, similar=SIMILAR):
    mono = samples.mean(axis=1).astype(np.float64)
    seconds = len(mono) / rate

    # Band shares
    power = (np.abs(stft(mono, WELCH_SIZE, WELCH_SIZE // 2)) ** 2).mean(axis=0)
    freqs = np.fft.rfftfreq(WELCH_SIZE, 1 / rate)
    total = power[freqs >= 20].sum()
    share = lambda lo, hi: power[(freqs >= lo) & (freqs < hi)].sum() / total

    # HPSS on the band up to HPSS_TOP_HZ
    spec = np.abs(stft(mono, WELCH_SIZE, HPSS_HOP)).astype(np.float32)
    keep = freqs < HPSS_TOP_HZ
    spec, bins = spec[:, keep], freqs[keep]
    half = HPSS_KERNEL // 2
    harmonic = np.median(windows(np.pad(spec, ((half, half), (0, 0)), mode="edge"), HPSS_KERNEL, axis=0), axis=-1)
    percussive = np.median(windows(np.pad(spec, ((0, 0), (half, half)), mode="edge"), HPSS_KERNEL, axis=1), axis=-1)
    mask = harmonic ** 2 / (harmonic ** 2 + percussive ** 2 + 1e-12)
    mid = (bins >= 300) & (bins < 2000)
    energy = spec[:, mid] ** 2
    tonal_mid = (energy * mask[:, mid]).sum() / energy.sum()
    frame_energy = energy.sum(axis=1)
    loud = frame_energy > 1e-3 * np.median(frame_energy)
    flatness = np.exp(np.log(energy + 1e-12).mean(axis=1)) / (energy + 1e-12).mean(axis=1)

    # Held notes on the harmonic part
    tonal = spec * mask
    bin_width = bins[1] - bins[0]
    candidates = PITCH_LOW_HZ * 2 ** (np.arange(0, 40) / 12.0)
    candidates = candidates[candidates <= PITCH_HIGH_HZ * 1.001]
    salience = np.zeros((tonal.shape[0], len(candidates)), np.float32)
    for i, f0 in enumerate(candidates):
        for h in range(1, HARMONICS + 1):
            b = int(round(h * f0 / bin_width))
            if b + 1 < tonal.shape[1]:
                salience[:, i] += 0.8 ** (h - 1) * tonal[:, b - 1:b + 2].max(axis=1)
    peak = salience.max(axis=1)
    best = np.where(peak > 0.1 * np.percentile(peak, 90), salience.argmax(axis=1), -1)
    min_run = int(np.ceil(HELD_SECONDS * rate / HPSS_HOP))

    def fine_f0(frame, f0):
        num = den = 0.0
        for h in range(1, 5):
            b = int(round(h * f0 / bin_width))
            if b < 2 or b + 2 >= len(frame):
                break
            b = b - 1 + int(np.argmax(frame[b - 1:b + 2]))
            a, c0, c = np.log(frame[b - 1:b + 2] + 1e-9)
            d = a - 2 * c0 + c
            num += frame[b] * (b + (0.5 * (a - c) / d if d != 0 else 0.0)) * bin_width / h
            den += frame[b]
        return num / den if den > 0 else np.nan

    notes, note_starts, spreads, means = [], [], [], []
    i = 0
    while i < len(best):
        j = i
        while j < len(best) and best[j] == best[i]:
            j += 1
        if best[i] >= 0 and j - i >= min_run:
            notes.append(best[i])
            note_starts.append((i * HPSS_HOP + WELCH_SIZE / 2) / rate)
            if j - i >= max(min_run, 3):
                f0 = candidates[best[i]]
                with np.errstate(invalid="ignore", divide="ignore"):
                    cents = np.array([1200 * np.log2(fine_f0(tonal[k], f0) / f0) for k in range(i, j)])
                cents = cents[np.isfinite(cents) & (np.abs(cents) < 60)]
                if len(cents) >= 3:
                    spreads.append(np.std(cents))
                    means.append(np.mean(cents))
        i = j
    changes = sum(1 for a, b in zip(notes, notes[1:]) if a != b) / seconds
    means = np.array(means)
    tuning = np.median(means) if len(means) else np.nan

    # Tempo wander
    flux_hop = rate // FLUX_FPS
    flux = np.maximum(np.diff(np.log1p(np.abs(stft(mono, 2048, flux_hop))), axis=0), 0).sum(axis=1)
    flux = np.maximum(flux - np.convolve(flux, np.ones(FLUX_FPS // 2) / (FLUX_FPS // 2), mode="same"), 0)
    centred = flux - flux.mean()
    whole = np.correlate(centred, centred, "full")[len(centred) - 1:]
    period = 25 + int(np.argmax(whole[25:150]))
    lo, hi = int(period * 0.9), int(np.ceil(period * 1.1)) + 1
    periods = []
    span, step = TEMPO_WINDOW_S * FLUX_FPS, TEMPO_HOP_S * FLUX_FPS
    for start in range(0, len(flux) - span, step):
        segment = flux[start:start + span] - flux[start:start + span].mean()
        ac = np.correlate(segment, segment, "full")[span - 1:]
        k = lo + int(np.argmax(ac[lo:hi]))
        if 1 <= k < len(ac) - 1:
            a, b, c = ac[k - 1], ac[k], ac[k + 1]
            k += 0.5 * (a - c) / (a - 2 * b + c) if (a - 2 * b + c) != 0 else 0
        periods.append(k)
    wander = np.std(periods) / np.mean(periods) if len(periods) > 1 else 0.0

    # Clipping
    clipped = 0
    for channel in samples.T:
        edges = np.diff(np.concatenate(([0], (np.abs(channel) >= CLIP_LEVEL).astype(np.int8), [0])))
        clipped += int(np.sum((np.nonzero(edges == -1)[0] - np.nonzero(edges == 1)[0]) >= CLIP_RUN))

    # Seam
    block = int(SEAM_SECONDS * rate)
    level = lambda x: 10 * np.log10((x ** 2).mean() + 1e-12)
    blocks = mono[:(len(mono) // block) * block].reshape(-1, block)
    steps = np.abs(np.diff(10 * np.log10((blocks ** 2).mean(axis=1) + 1e-12)))
    wrap = level(mono[:block]) - level(mono[-block:])

    # Short gaps, repetition, held notes outside the key
    gaps = short_gaps(mono, rate, gap_db)
    bar = (60 * BEATS_PER_BAR / bpm if bpm else bar_from_sidecar(seconds, sidecar if exact else None))
    bar_source = "option" if bpm else "sidecar" if bar else "estimated"
    bar = bar or bar_from_flux(flux, period / FLUX_FPS, seconds)
    arrangement = repetition(bar_features(tonal, bins, rate, bar, seconds), bar, similar)
    scale = parse_key(key or (sidecar or {}).get("request", {}).get("keyscale"))
    pitch_classes = [(int(n) + 7) % 12 for n in notes]  # candidate 0 is 196 Hz, a G: pitch class 7
    outside, outside_best, best_scale = out_of_key(pitch_classes, scale)
    outside_at = [round(t, 1) for t, pc in zip(note_starts, pitch_classes) if scale and pc not in scale[0]]

    return dict(name=name, seconds=round(seconds, 1),
                b200_2k=round(100 * share(200, 2000), 1), b300_4k=round(100 * share(300, 4000), 1),
                above6k=round(100 * share(6000, rate), 1), below200=round(100 * share(20, 200), 1),
                tonal_mid=round(100 * float(tonal_mid)), mid_flatness=round(float(np.mean(flatness[loud])), 2),
                held_changes=round(changes, 2), tempo_wander=round(100 * float(wander), 2), clipped=clipped,
                wobble=round(float(np.median(spreads)), 1) if spreads else None,
                detune=round(float(np.median(np.abs(means - tuning))), 1) if len(means) else None,
                wrap_db=round(float(wrap), 1), wrap_pct=round(float((steps < abs(wrap)).mean() * 100)),
                **gaps, bar_source=bar_source, **arrangement, held_notes=len(notes), key=scale[1] if scale else None,
                out_of_key=None if outside is None else round(100 * outside, 1), out_of_key_at=outside_at,
                out_of_best=None if outside_best is None else round(100 * outside_best, 1), best_scale=best_scale)


def table(rows, reference):
    refs = [r for r in rows if r["name"].split("-")[0] == reference]
    low = lambda c: min(r[c] for r in refs)
    high = lambda c: max(r[c] for r in refs)
    flags = {
        "b200_2k": lambda r: r["b200_2k"] < low("b200_2k"), "b300_4k": lambda r: r["b300_4k"] < low("b300_4k"),
        "above6k": lambda r: r["above6k"] > high("above6k"), "tonal_mid": lambda r: r["tonal_mid"] < low("tonal_mid"),
        "mid_flatness": lambda r: r["mid_flatness"] > FLATNESS_SLACK * high("mid_flatness"),
        "held_changes": lambda r: r["held_changes"] < low("held_changes"),
        "tempo_wander": lambda r: r["tempo_wander"] > high("tempo_wander"), "clipped": lambda r: r["clipped"] > high("clipped"),
    }
    units = {"b200_2k": " %", "b300_4k": " %", "above6k": " %", "tonal_mid": " %", "tempo_wander": " %"}
    columns = list(flags) + ["wobble", "detune"]
    lines = ["| Recording | 200 Hz-2 kHz | 300 Hz-4 kHz | above 6 kHz | tonal mid | mid flatness | held-note changes/s "
             "| tempo wander | clipped runs | pitch wobble | detune | wrap step |", "|" + "---|" * 12]
    for r in rows:
        cells = []
        for c in columns:
            text = f"{r[c]}{units.get(c, '')}"
            cells.append(f"**{text}**" if refs and r not in refs and c in flags and flags[c](r) else text)
        lines.append(f"| `{r['name']}` | " + " | ".join(cells) + f" | {r['wrap_db']:+.1f} dB (p{r['wrap_pct']}) |")
    if refs:
        span = [f"{low(c)}-{high(c)}{units.get(c, '')}" for c in columns]
        lines.append(f"| **{reference}, range of {len(refs)}** | " + " | ".join(span) + " | |")
    return "\n".join(lines)


def arrangement_table(rows, reference):
    refs = [r for r in rows if r["name"].split("-")[0] == reference]
    high = lambda c: max((r[c] for r in refs if r[c] is not None), default=None)
    over = lambda c: lambda r: r[c] is not None and high(c) is not None and r[c] > high(c)
    flags = {c: over(c) for c in ("gaps_per_min", "open_s", "run_s", "out_of_key", "out_of_best")}
    cells = lambda r: {
        "bars": f"{r['bars']} x {r['bar_s']:.2f} s ({r['bar_source']})",
        "gaps_per_min": f"{r['gaps_per_min']}", "gap_deepest_db": f"{r['gap_deepest_db']} dB" if r["gaps"] else "-",
        "gap_longest_ms": f"{r['gap_longest_ms']} ms" if r["gaps"] else "-",
        "open_s": f"{r['open_bars']} bars ({r['open_s']} s)", "run_s": f"{r['run_bars']} bars ({r['run_s']} s)",
        "out_of_key": "-" if r["out_of_key"] is None
        else f"{r['out_of_key']} % ({len(r['out_of_key_at'])} of {r['held_notes']}, {r['key']})",
        "out_of_best": "-" if r["out_of_best"] is None else f"{r['out_of_best']} %"}
    columns = list(cells(rows[0]))
    lines = ["| Recording | bars | short gaps/min | deepest gap | longest gap | opening repeat | longest repeat "
             "| notes out of key | notes out of best scale |", "|" + "---|" * 9]
    for r in rows:
        shown = cells(r)
        lines.append(f"| `{r['name']}` | " + " | ".join(
            f"**{shown[c]}**" if refs and r not in refs and c in flags and flags[c](r) else shown[c] for c in columns) + " |")
    if refs:
        ranged = lambda c, fmt: (f"{min(r[c] for r in refs if r[c] is not None)}-{high(c)}{fmt}"
                                 if any(r[c] is not None for r in refs) else "-")
        lines.append(f"| **{reference}, range of {len(refs)}** | | {ranged('gaps_per_min', '')} | | | {ranged('open_s', ' s')} "
                     f"| {ranged('run_s', ' s')} | {ranged('out_of_key', ' %')} | {ranged('out_of_best', ' %')} |")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--reference", default="ember", help="the family whose range the others are held to")
    parser.add_argument("--json", type=Path, help="also write one JSON line per recording here")
    parser.add_argument("--sidecars", type=Path, default=SIDECARS, help="where theme-<name>.json sidecars are read from")
    parser.add_argument("--key", help='the key to hold the held notes to, e.g. "A minor"; default: the sidecar\'s keyscale')
    parser.add_argument("--bpm", type=float, help="the tempo, for every file given, overriding the sidecar's loop cut "
                                                  "(default: the sidecar's loop cut, or the recording's own beat); "
                                                  "a bar is four beats")
    parser.add_argument("--gap-db", type=float, default=GAP_DROP_DB, help="how far under its surroundings a short gap falls")
    parser.add_argument("--similar", type=float, default=SIMILAR, help="the cosine from which two bars are near-identical")
    args = parser.parse_args()
    if args.key and not parse_key(args.key):
        parser.error(f'--key "{args.key}" is not a key like "A minor" or "Eb major"')

    rows = []
    for path in args.files:
        samples, rate = read_audio(path)
        name = path.stem.removeprefix("theme-")
        sidecar, exact = load_sidecar(args.sidecars, name)
        rows.append(measure(samples, rate, name, sidecar, exact, args.key, args.bpm, args.gap_db, args.similar))
        print(f"[measure] {rows[-1]['name']}", file=sys.stderr)
    if args.json:
        args.json.write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    print(table(rows, args.reference))
    print()
    print(arrangement_table(rows, args.reference))


if __name__ == "__main__":
    main()
