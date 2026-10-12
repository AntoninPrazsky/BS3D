"""A warm body under a render's first note (#726), then a 5 ms fade-in and a 20 ms fade-out: the step between a chosen
Stable Audio render and MusicBake --sfx for ceiling-feed.ogg. Writes a 44.1 kHz stereo 32-bit float wav (MusicBake mixes
it to mono and peak-normalises it). Needs numpy and soundfile (the generate-sfx.py venv has both).

usage: python sfx-body.py <render.wav> <out.wav> <body_level> [<body_decay_per_s>]
    ceiling-feed.ogg: python sfx-body.py feed3-f-25.wav ceiling-feed.wav 0.15 5

The body is a sine an octave under the render's first note (measured: the strongest partial 150-2000 Hz over its first
0.02-0.18 s), with a 10 ms attack and an exponential decay - the same shape as CeilingFeedSynth's E3 body. Its level is
relative to the render's peak. The owner asked for "a warm low body kept underneath for the machines that can play it"
(#726, 2026-10-02); the render that measured best carries none under 200 Hz.
"""
import sys

import numpy as np
import soundfile as sf

RATE = 44100


def strongest(x, a, b, lo=150, hi=2000):
    seg = x[int(a * RATE):int(b * RATE)] * np.hanning(int(b * RATE) - int(a * RATE))
    n = 1 << int(np.ceil(np.log2(len(seg) * 8)))
    p = np.abs(np.fft.rfft(seg, n)) ** 2
    f = np.fft.rfftfreq(n, 1 / RATE)
    m = (f >= lo) & (f < hi)
    return float(f[m][np.argmax(p[m])])


def main():
    src, out, level = sys.argv[1], sys.argv[2], float(sys.argv[3])
    decay = float(sys.argv[4]) if len(sys.argv) > 4 else 5.0
    d, rate = sf.read(src, always_2d=True)
    assert rate == RATE
    x = d.mean(axis=1)
    peak = np.abs(x).max()
    first = strongest(x, 0.02, 0.18)
    body_hz = first / 2
    t = np.arange(len(x)) / RATE
    body = level * peak * np.sin(2 * np.pi * body_hz * t) * np.minimum(1, t / 0.010) * np.exp(-t * decay)
    y = x + body
    fade_in, fade_out = int(0.005 * RATE), int(0.020 * RATE)
    y[:fade_in] *= np.linspace(0, 1, fade_in)
    y[-fade_out:] *= np.linspace(1, 0, fade_out)
    sf.write(out, np.stack([y, y], axis=1).astype(np.float32), RATE, subtype="FLOAT")
    print(f"first note {first:.1f} Hz, body {body_hz:.1f} Hz at {level} of the peak, decay {decay}/s -> {out}")


if __name__ == "__main__":
    main()
