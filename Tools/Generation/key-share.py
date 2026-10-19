"""
How much of a loop's pitched energy lies on its key's scale: every note, not just the dominant one (#905).

    python key-share.py <file> "G major" [<file> "E minor" ...]
    python key-share.py --shift <file> "G major" 15 1          the same after varispeed-shifting 15 s of the loop by 1 semitone

`measure-music.py` counts held notes outside the key by the dominant pitch alone (196-1175 Hz), so a second voice or a
pitched drum a semitone off is invisible to it, and "a bit disharmonic" (the owner on Sunset, 2026-10-16) has no column
there. This one sums the STFT power (8192 points, hop 2048, 100 Hz-2.5 kHz) of the harmonic part (median-filter HPSS,
17 frames in time against 17 bins in frequency, so the drums' noise does not blur it) into twelve pitch classes, the
tuning taken from the loop itself (the offset, in cents, that puts the most power on the 12-TET grid's centres), and
reports the share on the asked scale and on the best-fitting of the twelve major scales (a recording that keeps to a
key other than the asked one reads low on the first and high on the second).

How it was shown to fire (2026-10-19): a 15 s stretch of `mural-sunset` varispeed-shifted by +0.5, +1 and +6 semitones
moved the share from 90.7 % to 81.7 %, 70.4 % and 68.6 %; +12 (an octave, still on the scale) left it at 87.7 %. On the
shipped set it reads 75.6-86.4 % for ember (a rock band that plays blues notes), 84.8-99.8 % for the Gallery (`mural-mbira`
99.8) and 80.3-99.0 % for the Tower: so it ranks the old Sunset (90.7 %) in the middle of its family, not at the bottom,
and the old Glacier (80.3 %) at the bottom of its own, level with `summit-idm` (80.8 %). A candidate is compared with the
recording it replaces and its family, never with a fixed line. numpy, scipy and soundfile.
"""
import re
import sys

import numpy as np
import soundfile as sf
from numpy.lib.stride_tricks import sliding_window_view as windows
from scipy.ndimage import median_filter

MAJOR, MINOR = (0, 2, 4, 5, 7, 9, 11), (0, 2, 3, 5, 7, 8, 10)
NAMES = ("C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B")
SIZE, HOP = 8192, 2048
LOW_HZ, HIGH_HZ = 100, 2500
KERNEL = 17


def parse(text):
    m = re.fullmatch(r"\s*([A-Ga-g])([#b]?)\s*(major|minor)?\s*", text)
    if not m:
        raise SystemExit(f'"{text}" is not a key such as "G major" or "E minor"')
    tonic = ({"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}[m[1].upper()] + {"#": 1, "b": -1, "": 0}[m[2]]) % 12
    return tonic, (m[3] or "major") == "minor"


def chroma(mono, rate):
    """(the twelve pitch classes' share of the harmonic power, the tuning offset in semitones)."""
    spectrum = np.abs(np.fft.rfft(windows(mono, SIZE)[::HOP] * np.hanning(SIZE), axis=1)) ** 2
    harmonic = median_filter(spectrum, size=(KERNEL, 1), mode="wrap")
    percussive = median_filter(spectrum, size=(1, KERNEL), mode="nearest")
    power = (spectrum * (harmonic / (harmonic + percussive + 1e-20)) ** 2).sum(axis=0)
    frequencies = np.fft.rfftfreq(SIZE, 1 / rate)
    keep = (frequencies >= LOW_HZ) & (frequencies <= HIGH_HZ)
    midi, power = 69 + 12 * np.log2(frequencies[keep] / 440.0), power[keep]
    best, offset = -1.0, 0.0
    for candidate in np.arange(-0.45, 0.46, 0.05):
        score = float(np.sum(power * np.cos(2 * np.pi * ((midi - candidate) - np.round(midi - candidate)))))
        if score > best:
            best, offset = score, candidate
    classes = np.bincount(np.round(midi - offset).astype(int) % 12, weights=power, minlength=12)
    return classes / classes.sum(), offset


def shares(mono, rate, key):
    classes, offset = chroma(mono, rate)
    tonic, minor = parse(key)
    asked = float(classes[[(tonic + s) % 12 for s in (MINOR if minor else MAJOR)]].sum())
    best, best_tonic = max((float(classes[[(t + s) % 12 for s in MAJOR]].sum()), t) for t in range(12))
    return asked, best, f"{NAMES[best_tonic]} major/{NAMES[(best_tonic + 9) % 12]} minor", offset


def shifted(mono, rate, seconds, semitones, start=8.0):
    """Varispeed a stretch of the loop (pitch moves, length stays): the synthetic fault the column is shown to see."""
    a, n, ratio = int(rate * start), int(rate * seconds), 2 ** (semitones / 12)
    source = mono[a:a + int(n * ratio) + 2]
    moved = np.interp(np.arange(n) * ratio, np.arange(len(source)), source)
    return np.concatenate([mono[:a], moved, mono[a + n:]])


def main(argv):
    args = argv[1:]
    shift = None
    if args and args[0] == "--shift":
        shift = (float(args[3]), float(args[4]))
        args = args[1:3]
    if len(args) < 2 or len(args) % 2:
        print(__doc__)
        return 2
    for path, key in zip(args[0::2], args[1::2]):
        samples, rate = sf.read(path, dtype="float64", always_2d=True)
        mono = samples.mean(axis=1)
        if shift:
            mono = shifted(mono, rate, *shift)
        asked, best, label, offset = shares(mono, rate, key)
        name = re.sub(r".*[\\/]", "", path)
        print(f"{name} | {key} | on the asked scale {asked * 100:.1f} % | best {label} {best * 100:.1f} % | tuning {offset * 100:+.0f} c")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
