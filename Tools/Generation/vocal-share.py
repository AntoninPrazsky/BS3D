"""#916's voice proxy: how loud the VOICE stands in a loop, as Demucs (htdemucs, CPU) separates it.

    vocal-share.py <loop.ogg|wav> [...]

Per file: the separated vocals stem against the mix in 300 Hz-4 kHz (the band the owner's monitor speakers play): its
level (dB under the mix), its loud 250 ms (the 95th percentile of the stem's 250 ms windows against the mix's own), and
the share of 250 ms windows where the vocals stand within 10 dB of the mix. A proxy, seen to fire (#916, 2026-10-27): chopped
wordless voices read 1.5-8.6 dB under the mix in the renders briefed for them, where an instrumental read 37.5 (ember-garage)
and 53.7 dB (a voice brief whose render came back without one). It is blind to a vocoder: the old neon-electro, whose
whispers and syllables the owner hears, read 50.4 dB, and a vocoder melody 32.4 dB, so a vocoder's prominence is the ear's.
Run it with the Demucs venv (C:/Users/panrd/AI/stems/venv), about a minute a loop on the CPU.
"""
import sys

import numpy as np
import soundfile as sf
import torch
import julius
from demucs.apply import apply_model
from demucs.pretrained import get_model


def band(x, sr, lo=300.0, hi=4000.0):
    n = len(x)
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1.0 / sr)
    spec[(f < lo) | (f > hi)] = 0
    return np.fft.irfft(spec, n)


def win_db(x, sr, w=0.25):
    k = int(sr * w)
    m = len(x) // k
    r = np.sqrt(np.mean(x[: m * k].reshape(m, k) ** 2, axis=1)) + 1e-9
    return 20 * np.log10(r)


model = get_model("htdemucs")
model.eval()
vi = model.sources.index("vocals")
print("| loop | vocals 300-4k vs mix | loud 250 ms | windows within 10 dB |")
print("|---|---|---|---|")
for path in sys.argv[1:]:
    x, sr = sf.read(path, dtype="float32", always_2d=True)
    t = torch.from_numpy(x.T.copy())
    t44 = julius.resample_frac(t, sr, model.samplerate)
    with torch.no_grad():
        out = apply_model(model, t44[None], device="cpu", progress=False)[0]
    voc = julius.resample_frac(out[vi], model.samplerate, sr).numpy().T[: len(x)]
    mix_m = band(x.mean(axis=1), sr)
    voc_m = band(voc.mean(axis=1)[: len(mix_m)], sr)
    lvl = 20 * np.log10((np.sqrt(np.mean(voc_m ** 2)) + 1e-12) / (np.sqrt(np.mean(mix_m ** 2)) + 1e-12))
    dv, dm = win_db(voc_m, sr), win_db(mix_m, sr)
    rel = dv - dm
    loud = float(np.percentile(rel, 95))
    near = float(np.mean(rel > -10) * 100)
    name = path.replace("\\", "/").split("/")[-1]
    print(f"| `{name}` | {lvl:+.1f} dB | {loud:+.1f} dB | {near:.0f} % |", flush=True)
