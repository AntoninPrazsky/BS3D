"""Cut a seamless loop out of a rendered track.

Usage: loop_crossfade.py <in.wav> <out.wav> <bpm>     (bpm 0 = estimate it)

A generated piece has an intro and an ending -- a fade-out, or a final hit into
silence -- so folding the file's own tail into its head leaves a hole at the
wrap: seamless to the sample, but audibly an ending followed by a beginning.

Instead: estimate the beat period near <bpm>, find the loud body of the track,
and pick a start S early in the body and an end E late in it, a whole number of
bars apart, where the music around E best matches the music around S. Align S
to E at the millisecond, then write audio[S:E] with the audio just past E
crossfaded into its start -- so the wrap E -> S sounds like E -> E+ did.

WAV chunks are parsed by hand: ace-synth's wav32 is IEEE-float PCM (format tag
3), which the stdlib `wave` module refuses to read.
"""
import struct
import sys

import numpy as np

FEATURE_HOP_S = 0.05
CONTEXT_S = 4.0
MATCH_SLACK = 0.05
BODY_LEVEL = 0.6
RHYTHM_SHARE = 0.9
MAX_CANDIDATES = 400


def read_wav(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise SystemExit(f"{path} is not a RIFF/WAVE file")
    pos, fmt, raw = 12, None, None
    while pos + 8 <= len(data):
        chunk_id = data[pos:pos + 4]
        size = struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = data[pos + 8:pos + 8 + size]
        if chunk_id == b"fmt ":
            tag, channels, rate, _, _, bits = struct.unpack("<HHIIHH", body[:16])
            fmt = dict(tag=tag, channels=channels, rate=rate, bits=bits)
        elif chunk_id == b"data":
            raw = body
        pos += 8 + size + (size & 1)
    if fmt is None or raw is None:
        raise SystemExit(f"{path}: missing fmt or data chunk")
    ch = fmt["channels"]
    if fmt["tag"] == 3 and fmt["bits"] == 32:
        return np.frombuffer(raw, dtype=np.float32).reshape(-1, ch).copy(), fmt, "float32"
    if fmt["tag"] == 1 and fmt["bits"] == 16:
        return np.frombuffer(raw, dtype=np.int16).reshape(-1, ch).astype(np.float32) / 32768.0, fmt, "int16"
    raise SystemExit(f"unsupported WAV format: tag={fmt['tag']} bits={fmt['bits']} (use wav16 or wav32)")


def write_wav(path, samples, rate, channels, kind):
    if kind == "float32":
        tag, bits, payload = 3, 32, samples.astype(np.float32).tobytes()
    else:
        tag, bits = 1, 16
        payload = np.clip(samples * 32768.0, -32768, 32767).astype(np.int16).tobytes()
    block = channels * bits // 8
    fmt_chunk = struct.pack("<HHIIHH", tag, channels, rate, rate * block, block, bits)
    with open(path, "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 4 + 8 + len(fmt_chunk) + 8 + len(payload)) + b"WAVE")
        f.write(b"fmt " + struct.pack("<I", len(fmt_chunk)) + fmt_chunk)
        f.write(b"data" + struct.pack("<I", len(payload)) + payload)


def stft_mag(mono, n_fft, hop):
    window = np.hanning(n_fft).astype(np.float32)
    frames = 1 + (len(mono) - n_fft) // hop
    out = np.empty((frames, n_fft // 2 + 1), dtype=np.float32)
    for i in range(0, frames, 256):
        j = min(frames, i + 256)
        idx = np.arange(n_fft)[None, :] + hop * np.arange(i, j)[:, None]
        out[i:j] = np.abs(np.fft.rfft(mono[idx] * window, axis=1))
    return out


def beat_period(mono, rate, bpm):
    hop = rate // 100
    flux = np.maximum(0.0, np.diff(np.log1p(10.0 * stft_mag(mono, 1024, hop)), axis=0)).sum(axis=1)
    flux = np.maximum(0.0, flux - np.convolve(flux, np.ones(50) / 50, mode="same"))
    spectrum = np.fft.rfft(flux - flux.mean(), n=2 * len(flux))
    ac = np.fft.irfft(np.abs(spectrum) ** 2)[:len(flux)]
    fps = rate / hop
    lo, hi = (60 / (bpm * 1.08), 60 / (bpm * 0.92)) if bpm > 0 else (60 / 180, 60 / 70)
    lags = np.arange(int(lo * fps), int(np.ceil(hi * fps)) + 1)
    k = int(lags[np.argmax(ac[lags])])
    a, b, c = ac[k - 1], ac[k], ac[k + 1]
    denom = a - 2 * b + c
    return (k + (0.5 * (a - c) / denom if denom != 0 else 0.0)) / fps


def band_features(mono, rate, hop):
    n_fft = 4096
    mag = stft_mag(mono, n_fft, hop)
    freqs = np.fft.rfftfreq(n_fft, 1.0 / rate)
    edges = np.geomspace(40, 16000, 33)
    bands = np.stack([mag[:, (freqs >= edges[i]) & (freqs < edges[i + 1])].sum(axis=1)
                      for i in range(32)], axis=1)
    return np.log1p(bands), np.sqrt((mag ** 2).sum(axis=1))


def transient_envelope(mono):
    """1 ms envelope of the first difference -- where the hits are, not what pitch they carry."""
    return np.convolve(np.abs(np.diff(mono, prepend=mono[0])), np.ones(96) / 96, mode="same")[::48]


def crossfade_agreement(mono, rate, s0, e0, seconds, mean, std):
    """How alike the two streams the crossfade mixes are: rhythm (envelope correlation) and spectrum (band cosine).

    The wrap sample itself is exact by construction -- the loop's first sample is the source's sample after E.
    What can go wrong is the second or two after it, where audio[E:] fades into audio[S:]."""
    n = int(seconds * rate)
    a, b = transient_envelope(mono[e0:e0 + n]), transient_envelope(mono[s0:s0 + n])
    a, b = a - a.mean(), b - b.mean()
    rhythm = float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-12))
    hop = int(FEATURE_HOP_S * rate)
    fa, _ = band_features(mono[e0:e0 + n + 4096], rate, hop)
    fb, _ = band_features(mono[s0:s0 + n + 4096], rate, hop)
    va, vb = ((fa - mean) / std).ravel(), ((fb - mean) / std).ravel()
    spectrum = float(np.dot(va, vb) / (np.linalg.norm(va) * np.linalg.norm(vb) + 1e-12))
    return rhythm, spectrum


def main():
    in_path, out_path, bpm = sys.argv[1], sys.argv[2], float(sys.argv[3])
    audio, fmt, kind = read_wav(in_path)
    rate, channels = fmt["rate"], fmt["channels"]
    mono = audio.mean(axis=1)

    beat = beat_period(mono, rate, bpm)
    hop = int(FEATURE_HOP_S * rate)
    fps = rate / hop
    bar = 4 * beat * fps

    raw_feat, energy = band_features(mono, rate, hop)
    mean, std = raw_feat.mean(axis=0), raw_feat.std(axis=0) + 1e-6
    feat = (raw_feat - mean) / std

    smooth = np.convolve(energy, np.ones(int(2 * fps)) / int(2 * fps), mode="same")
    # 0.6 and not lower: a render can open with half a minute of stop-start intro at a quarter to two-fifths of
    # its median level (measured), and a 0.4 threshold lets the loop start there, where nothing late matches.
    body = np.where(smooth >= BODY_LEVEL * np.median(smooth))[0]
    b0, b1 = int(body[0]), int(body[-1])

    xfade_s = float(np.clip(2 * beat, 0.5, 1.5))
    ctx = int(CONTEXT_S * fps)
    reach = max(int(4 * bar), int(0.3 * (b1 - b0)))
    s_lo, s_hi = max(b0 + int(bar), ctx), b0 + reach
    e_hi = min(b1 - int(2 * bar), len(feat) - ctx - int(np.ceil(xfade_s * fps)) - 1)
    e_lo = max(e_hi - reach, s_hi + int(8 * bar))
    if e_lo >= e_hi or s_lo >= s_hi:
        raise SystemExit("track too short to find a loop inside its body")

    def vectors(lo, hi):
        m = np.stack([feat[p - ctx:p + ctx].ravel() for p in range(lo, hi)])
        return m / (np.linalg.norm(m, axis=1, keepdims=True) + 1e-9)

    sim = vectors(e_lo, e_hi) @ vectors(s_lo, s_hi).T
    lag = (np.arange(e_lo, e_hi)[:, None] - np.arange(s_lo, s_hi)[None, :]).astype(np.float64)
    on_grid = np.abs(lag - np.round(lag / bar) * bar) <= 1.5
    best = sim[on_grid].max()
    good = np.argwhere(on_grid & (sim >= best - MATCH_SLACK))

    env = transient_envelope(mono)

    def align(s0, e0):
        """Millisecond alignment on the transient envelope, then sample alignment on the waveform."""
        ref = env[e0 // 48 - 1000:e0 // 48 + 3000]
        s0 += 48 * max(range(-80, 81), key=lambda d: float(np.dot(ref, env[s0 // 48 + d - 1000:s0 // 48 + d + 3000])))
        ref = mono[e0 - rate // 4:e0 + rate // 4]
        return s0 + max(range(-48, 49), key=lambda d: float(np.dot(ref, mono[s0 + d - rate // 4:s0 + d + rate // 4])))

    # What "in time" means for this track: its own passages one bar apart, measured the way a candidate is.
    beat_n = int(beat * rate)
    rng = np.random.default_rng(0)
    probes = rng.integers(b0 * hop, b1 * hop - 4 * beat_n - 4 * rate, 40)
    own_r = float(np.median([crossfade_agreement(mono, rate, p, p + 4 * beat_n, xfade_s + 1.0, mean, std)[0]
                             for p in probes]))

    # The best match alone picks short loops: two points close together are always alike. So walk the pairs
    # that match nearly as well as the best one, longest first, and take the first whose two streams under the
    # crossfade keep time as well as the track keeps time with itself a bar later.
    chosen = None
    for ei, si in sorted(good, key=lambda p: -lag[p[0], p[1]])[:MAX_CANDIDATES]:
        e0 = int((e_lo + ei) * hop)
        s0 = align(int((s_lo + si) * hop), e0)
        rhythm, spectrum = crossfade_agreement(mono, rate, s0, e0, xfade_s + 1.0, mean, std)
        if chosen is None or rhythm > chosen[2]:
            chosen = (s0, e0, rhythm, spectrum, float(sim[ei, si]))
        if rhythm >= RHYTHM_SHARE * own_r:
            chosen = (s0, e0, rhythm, spectrum, float(sim[ei, si]))
            break
    s0, e0, rhythm, spectrum, match = chosen

    x = int(xfade_s * rate)
    loop = audio[s0:e0].copy()
    t = np.linspace(0, np.pi / 2, x, dtype=np.float32)[:, None]
    loop[:x] = audio[s0:s0 + x] * np.sin(t) + audio[e0:e0 + x] * np.cos(t)

    write_wav(out_path, loop, rate, channels, kind)
    verdict = "in time" if rhythm >= RHYTHM_SHARE * own_r else "BELOW the track's own timekeeping -- listen"
    print(f"Wrote {out_path}: {len(loop) / rate:.1f}s loop = source {s0 / rate:.2f}s..{e0 / rate:.2f}s "
          f"({(e0 - s0) / (4 * beat * rate):.2f} bars at {60 / beat:.1f} bpm), crossfade {xfade_s:.2f}s | "
          f"context match {match:.3f} (best {best:.3f}) | crossfade rhythm r={rhythm:.2f} vs the track "
          f"a bar later r={own_r:.2f}: {verdict} | spectrum cos={spectrum:.2f}")


if __name__ == "__main__":
    main()
