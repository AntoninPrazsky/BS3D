"""
How loud a recording's separated drums stand against its own mix, in the band the owner's monitor speakers play
(#905, #906): the column that answers "the drums override the melody" and "blows and pounding" with a number.

    python drum-share.py Game/Music mural summit ember           every recording with a drums layer, by name prefix
    python drum-share.py --pairs name=loop.wav,drums.wav ...     a candidate loop and the stem separate_drums.py made of it

The first form reads `Music/<stem>.ogg` and its layer `Music/Drums/<stem>.ogg` (stored at half level,
`DrumLayer.STORED_SCALE`, scaled back here). The second form reads a candidate's loop and the float stem
`C:/Users/panrd/AI/stems/separate_drums.py` wrote for it (stored at 1.0), before anything is baked. Both are
band-limited to 300 Hz-4 kHz by an FFT mask over the whole loop, then compared:
  - level: the stem's RMS against the mix's, dB (the whole band beside it: a stem can read over the mix under 200 Hz,
    where Demucs puts a bass with a hard attack and the mix cancels part of it);
  - loud: the stem's 95th percentile of 250 ms RMS against the mix's, dB - how loud the drums get when they play;
  - crest: the stem's 95th percentile of 250 ms RMS over its median, dB - a stem that is silent most of the time and
    then hits reads 30-40, a steady groove 5-15;
  - near: the share of 250 ms windows in which the stem stands within 6 dB of the mix.

How it was shown to fire (2026-10-19, on the shipped set before #905 and #906): the old `summit-glacier`, which the owner
called blows and pounding, read -2.3 dB level, -1.1 dB loud and crest 38 dB, where the other recordings of its family
read -6.1 to -21.2, -5.0 to -16.5 and 7-25 (only the house track `summit-deephouse`, whose drums are the music, stands
as high: -1.3 / -0.7), and ember, the model, reads -4.5 to -13.8 / -2.0 to -10.3. It singles out the old `mural-sunset`,
which the owner called drums overriding the melody, only weakly: -8.9 / -5.1 / crest 17 / near 20 % against its
family's -7.9 to -18.3 / -5.4 to -14.1 / near 1-35 % (three recordings stand higher in level, it is the highest in
loud by 0.3 dB, and `mural-mbira`, which he did not name, reads -13.3 / -11.1 / 2 %), so for Sunset the column is a
target to get under, not a finding. The stem is Demucs' guess: a marimba's
attack or a plucked bass can land in it, so read it as an upper bound on the drums. numpy and soundfile.
"""
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

STORED_SCALE = 0.5  # Game/Audio/DrumLayer.STORED_SCALE
LOW_HZ, HIGH_HZ = 300, 4000
WINDOW_S = 0.25
NEAR_DB = 6.0


def bandpass(x, rate, low=LOW_HZ, high=HIGH_HZ):
    """Zero-phase band-limit by an FFT mask over the whole loop."""
    spectrum = np.fft.rfft(x)
    frequencies = np.fft.rfftfreq(len(x), 1 / rate)
    spectrum[(frequencies < low) | (frequencies >= high)] = 0
    return np.fft.irfft(spectrum, len(x))


def window_db(x, rate):
    size = int(rate * WINDOW_S)
    count = len(x) // size
    rms = np.sqrt(np.mean(x[: count * size].reshape(count, size) ** 2, axis=1)) + 1e-9
    return 20 * np.log10(rms)


def figures(recording, layer, stored):
    """(whole-band level, 300 Hz-4 kHz level, loud, crest, near), the stem against the mix."""
    mix, rate = sf.read(str(recording), dtype="float64", always_2d=True)
    stem, _ = sf.read(str(layer), dtype="float64", always_2d=True)
    n = min(len(mix), len(stem))
    mix, stem = mix[:n].mean(axis=1), stem[:n].mean(axis=1) / stored
    whole = 20 * np.log10(np.sqrt(np.mean(stem ** 2)) / np.sqrt(np.mean(mix ** 2)) + 1e-12)
    mix, stem = bandpass(mix, rate), bandpass(stem, rate)
    level = 20 * np.log10(np.sqrt(np.mean(stem ** 2)) / np.sqrt(np.mean(mix ** 2)) + 1e-12)
    mix_db, stem_db = window_db(mix, rate), window_db(stem, rate)
    loud = np.percentile(stem_db, 95) - np.percentile(mix_db, 95)
    crest = np.percentile(stem_db, 95) - np.median(stem_db)
    near = float(np.mean(stem_db - mix_db > -NEAR_DB) * 100)
    return whole, level, loud, crest, near


def table(rows):
    print("| recording | drums vs mix (all) dB | drums vs mix 300 Hz-4 kHz dB | loud 250 ms drums vs mix dB | drums crest dB | windows within 6 dB of the mix % |")
    print("|---|---|---|---|---|---|")
    for name, (whole, level, loud, crest, near) in rows:
        print(f"| {name} | {whole:+.1f} | {level:+.1f} | {loud:+.1f} | {crest:.1f} | {near:.0f} |")


def main(argv):
    if len(argv) > 1 and argv[1] == "--pairs":
        rows = []
        for pair in argv[2:]:
            name, files = pair.split("=", 1)
            recording, layer = files.split(",")
            rows.append((name, figures(Path(recording), Path(layer), 1.0)))
        table(rows)
        return 0
    if len(argv) < 2:
        print(__doc__)
        return 2
    music, prefixes = Path(argv[1]), argv[2:]
    rows = []
    for layer in sorted((music / "Drums").glob("*.ogg")):
        recording = music / f"{layer.stem}.ogg"
        if recording.exists() and (not prefixes or any(layer.stem.startswith(p) for p in prefixes)):
            rows.append((layer.stem, figures(recording, layer, STORED_SCALE)))
    table(rows)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
