"""
Measures music loops against a reference family, the way the offending recordings of a family are named (#841, #824,
#706): the owner's ruling of 2026-10-11 is that which recordings offend is decided by measurement - tunelessness, the
low-mid share, energy above 6 kHz - with the desert's `ember` family as the model.

    python measure-music.py Game/Music/ember*.ogg Game/Music/pulse*.ogg [--reference ember] [--json out.jsonl]

Every file is measured on its mono mix; the reference family's files (stem starting with --reference) must be among
the inputs, and set the range the others are held to. The table bolds a figure outside it: a band share under the
reference's lowest, more above 6 kHz than its highest, a less tonal mid band, fewer held-note changes, more tempo
wander or more clipping than any reference recording, and a mid flatness over 1.5x its highest.

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

On the magma family this reproduces #841's band, clipping and seam figures exactly and its tonal mid within 7 points;
its flatness and held-note changes are the same idea with different details, so compare them only within one run.
A tune proxy measures harmonic content and pitch movement, not whether a melody pleases: nothing here replaces the ear.
"""

import argparse
import json
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


def measure(samples, rate, name):
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

    notes, spreads, means = [], [], []
    i = 0
    while i < len(best):
        j = i
        while j < len(best) and best[j] == best[i]:
            j += 1
        if best[i] >= 0 and j - i >= min_run:
            notes.append(best[i])
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

    return dict(name=name, seconds=round(seconds, 1),
                b200_2k=round(100 * share(200, 2000), 1), b300_4k=round(100 * share(300, 4000), 1),
                above6k=round(100 * share(6000, rate), 1), below200=round(100 * share(20, 200), 1),
                tonal_mid=round(100 * float(tonal_mid)), mid_flatness=round(float(np.mean(flatness[loud])), 2),
                held_changes=round(changes, 2), tempo_wander=round(100 * float(wander), 2), clipped=clipped,
                wobble=round(float(np.median(spreads)), 1) if spreads else None,
                detune=round(float(np.median(np.abs(means - tuning))), 1) if len(means) else None,
                wrap_db=round(float(wrap), 1), wrap_pct=round(float((steps < abs(wrap)).mean() * 100)))


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


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--reference", default="ember", help="the family whose range the others are held to")
    parser.add_argument("--json", type=Path, help="also write one JSON line per recording here")
    args = parser.parse_args()

    rows = []
    for path in args.files:
        samples, rate = read_audio(path)
        rows.append(measure(samples, rate, path.stem.removeprefix("theme-")))
        print(f"[measure] {rows[-1]['name']}", file=sys.stderr)
    if args.json:
        args.json.write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
    print(table(rows, args.reference))


if __name__ == "__main__":
    main()
