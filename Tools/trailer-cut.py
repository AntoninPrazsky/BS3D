"""The trailer's scripted cut (#1180, for #1178): an edit file in, a 4K60 master with sound out.

    python -I Tools/trailer-cut.py Tools/trailer/trailer.json            # film what is missing, then cut
    python -I Tools/trailer-cut.py Tools/trailer/trailer.json --size 1920x1080 --out-name preview   # a quick preview
    python -I Tools/trailer-cut.py Tools/trailer/trailer.json --dry      # the timeline and the beats, nothing rendered

Run with a Python that has PyAV (its bundled FFmpeg), numpy and Pillow - the one web-capture-encode.py uses. No ffmpeg install.

The edit file (JSON; a line starting with // is a comment) says, in order of the timeline:
  - "segments": each a filmed clip ("capture": the arguments of Tools/web-capture.ps1, in its own spelling) or a card on black
    ("card"), with "in" (seconds into the clip), "length", the transition into it ("cut", "dissolve", "dip", "white";
    "transitionSeconds"), the game's sound under it ("sound": dB, or null for none), the music ducked under it ("duck": dB) and
    the trailer's own sound on its cut ("sfx": "hit", "boom" or "whoosh", made by synth_sfx);
  - "cards": text over the footage, at a time on the timeline: "lines", or "parts" of one line each shown at its "reveal";
  - "music": the tracks (Game/Music, or a path in the repository), each from an offset into its recording and at a time on the
    timeline: each next track's time is a switch, where the one before stops ("switchFade") and the next comes in;
  - "sfx": more of the trailer's own sounds, each at a stated time;
  - "snap": whether cuts after the last switch land on the last track's beats;
  - "mix": the music's, the game's and the trailer's own sounds' levels against each other.

Every clip is filmed by web-capture.ps1 (-NoEncode -KeepFrames) into %TEMP%\\bs3d-web-capture-frames\\trailer-<clip>-<size>, and
filmed again only when its arguments change (trailer-args.json beside the frames). A capture is deterministic, so a clip found
at 1920x1080 is the same clip at 3840x2160.

Writes <out-name>-master.mp4 (H.264 High, crf 16, BT.709, a key every half second, AAC 320 kb/s at 48 kHz) and
<out-name>-cutlist.json (every segment's source, frames, start and length, the music's beats, the levels measured).
"""
import argparse
import glob
import hashlib
import json
import math
import os
import subprocess
import sys
import wave

import av
import numpy as np
from av.video.reformatter import Colorspace
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES_ROOT = os.path.join(os.environ.get("TEMP", "."), "bs3d-web-capture-frames")
FONT = os.path.join(ROOT, "Game", "Content", "Fonts", "Anton-Regular.ttf")
LOGO = os.path.join(ROOT, "Images", "logo", "bs3d-logo-2048.png")
MUSIC = os.path.join(ROOT, "Game", "Music")
RATE = 48000
MASTER = {"crf": "16", "preset": "slow", "profile": "high", "bf": "2"}


# ---------------------------------------------------------------- the edit

def load_edit(path):
    with open(path, encoding="utf-8") as f:
        text = "\n".join(line for line in f if not line.lstrip().startswith("//"))
    edit = json.loads(text)
    if edit.get("format") != "bs3d-trailer":
        sys.exit("%s is not a bs3d-trailer edit" % path)
    return edit


def parse_size(text):
    w, h = text.lower().split("x")
    return int(w), int(h)


# ---------------------------------------------------------------- filming

def level_copy(level_file, scene):
    """A copy of a level file with its scene replaced, in %TEMP%: the last shot plays one level in every scene (#1178). The copy is
    the original but for the scene's name, so the game played is the same (with wind=0, measured: every event at the same ms)."""
    source = os.path.join(ROOT, level_file) if not os.path.isabs(level_file) else level_file
    with open(source, encoding="utf-8") as f:
        level = json.load(f)
    level["scene"] = scene
    folder = os.path.join(os.environ.get("TEMP", "."), "bs3d-trailer-levels")
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, "%s-%s.json" % (os.path.splitext(os.path.basename(source))[0], scene))
    with open(path, "w", encoding="utf-8") as f:
        json.dump(level, f)
    return path


def capture_arguments(capture, film, size):
    """web-capture.ps1's arguments for a clip: the edit's defaults ("film") under the clip's own."""
    merged = dict(film)
    merged.update(capture)
    where = (["-LevelFile", level_copy(merged["levelFile"], merged["scene"])] if merged.get("levelFile")
             else ["-Level", str(merged["level"])])
    args = where + ["-From", str(merged.get("from", 0)), "-Seconds", str(capture["seconds"]),
            "-Size", size, "-Quality", merged.get("quality", "Ultra"), "-Fps", str(merged.get("fps", 60))]
    for switch, key in (("-Hud", "hud"), ("-NoGrain", "noGrain"), ("-MaxDetail", "maxDetail"), ("-Audio", "audio"), ("-Splash", "splash"),
                        ("-Demo", "demo"), ("-Shake", "shake"), ("-Aim", "aim")):
        if merged.get(key):
            args.append(switch)
    for name, key in (("-CamPos", "camPos"), ("-CamTarget", "camTarget"), ("-Fov", "fov"), ("-CamPath", "camPath"), ("-GameArgs", "gameArgs")):
        if merged.get(key) not in (None, ""):
            value = merged[key]
            if key == "camPath":
                value = os.path.join(ROOT, value) if not os.path.isabs(value) else value
            args += [name, str(value)]
    return args


def film(segment, edit, size, built, refilm):
    """Films a segment's clip unless its frames are already there for the same arguments; returns the frames' folder and whether the
    Game was built."""
    name = "trailer-%s-%s" % (segment["name"], size)
    folder = os.path.join(FRAMES_ROOT, name)
    args = capture_arguments(segment["capture"], edit.get("film", {}), size)
    signature = hashlib.sha1(json.dumps(args).encode()).hexdigest()
    marker = os.path.join(folder, "trailer-args.json")
    if not refilm and os.path.exists(marker) and os.path.exists(os.path.join(folder, "capture.json")):
        with open(marker, encoding="utf-8") as f:
            if json.load(f).get("signature") == signature:
                return folder, built

    command = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", os.path.join(ROOT, "Tools", "web-capture.ps1"),
               "-Name", name] + args + ["-NoEncode", "-KeepFrames"]
    if built:
        command.append("-NoBuild")
    print("filming %s: %s" % (segment["name"], " ".join(args)), flush=True)
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode != 0:
        sys.exit("filming %s failed (exit %d)" % (segment["name"], result.returncode))
    with open(marker, "w", encoding="utf-8") as f:
        json.dump({"signature": signature, "arguments": args}, f, indent=2)
    return folder, True


# ---------------------------------------------------------------- music

def decode_audio(path):
    """Any file PyAV reads, as float32 (samples, 2) at RATE."""
    with av.open(path) as container:
        resampler = av.AudioResampler(format="flt", layout="stereo", rate=RATE)
        chunks = []
        for frame in container.decode(container.streams.audio[0]):
            for out in resampler.resample(frame):
                chunks.append(out.to_ndarray().reshape(-1, 2))
        for out in resampler.resample(None):
            chunks.append(out.to_ndarray().reshape(-1, 2))
    return np.concatenate(chunks).astype(np.float32)


def beats(signal):
    """The track's tempo (BPM) and its beat times in seconds: spectral-flux onsets at 100 a second, the tempo the autocorrelation's peak
    between 80 and 180 BPM, the phase the offset whose beats sit on the most onset."""
    mono = signal.mean(axis=1)
    hop, size = RATE // 100, 2048
    frames = (len(mono) - size) // hop
    window = np.hanning(size)
    flux = np.zeros(frames)
    previous = None
    for i in range(frames):
        spectrum = np.abs(np.fft.rfft(mono[i * hop:i * hop + size] * window))
        if previous is not None:
            flux[i] = np.sum(np.maximum(spectrum - previous, 0.0))
        previous = spectrum
    onset = np.maximum(flux - np.convolve(flux, np.ones(50) / 50, mode="same"), 0)
    lags = np.arange(int(6000 / 180), int(6000 / 80) + 1)
    ac = np.array([np.dot(onset[:-lag], onset[lag:]) for lag in lags])
    period = lags[np.argmax(ac)]
    phase = max(range(period), key=lambda p: onset[p::period].sum())
    times = np.arange(phase, len(onset), period) / 100.0
    return 6000.0 / period, times


def music_track(entry, length):
    """A track from its offset, looped (the game's recordings are loops) to cover length seconds of the timeline."""
    # A bare name is one of the game's recordings (Game/Music); a path is the repository's (a track made for the trailer)
    name = entry["file"]
    path = os.path.join(ROOT, name) if ("/" in name or "\\" in name) and not os.path.isabs(name) else os.path.join(MUSIC, name)
    signal = decode_audio(path)
    start = int(round(entry.get("from", 0.0) * RATE))
    need = int(math.ceil(length * RATE))
    if not entry.get("loop", True):
        # A piece with an ending (a track made for the trailer) is followed by silence, never by its own beginning
        piece = signal[start:start + need]
        return signal, np.concatenate([piece, np.zeros((max(0, need - len(piece)), 2), dtype=np.float32)])
    reps = int(math.ceil((start + need) / len(signal))) + 1
    looped = np.concatenate([signal] * reps)
    return signal, looped[start:start + need]


# ---------------------------------------------------------------- the trailer's own sounds

def _lowpass(x, cutoff):
    """A one-pole lowpass at `cutoff` Hz."""
    a = 1.0 - math.exp(-2.0 * math.pi * cutoff / RATE)
    y = np.empty_like(x)
    s = 0.0
    for i in range(len(x)):
        s += a * (x[i] - s)
        y[i] = s
    return y


def _peak1(x):
    return x / (np.max(np.abs(x)) + 1e-12)


_SFX = {}


def synth_sfx(kind):
    """The cuts' sounds, made here from sines and filtered noise (#1178), mono, peak 1, and the second of each that marks the
    cut (a hit's start, a whoosh's crest):
      - "hit": a trailer hit for a cut in the fast part, a pitch-dropping sub with a body at 140-300 Hz and its harmonics, a
        drum shell's ring at 400-560 Hz and a dark strike, so a monitor speaker that plays no sub still plays the hit (#726);
      - "boom": the same, bigger and longer, for a part's first frame (the white flash, out of black);
      - "whoosh": the last shot's switch of world, a resonant band of noise swept up from 500 Hz to 3.2 kHz into the cut and a
        short 2.2 kHz tick on it.
    Warm and without a drive or a hiss above 5 kHz (the owner's sound rulings); deterministic, so a re-cut sounds the same."""
    if kind in _SFX:
        return _SFX[kind]
    rng = np.random.default_rng({"hit": 1178, "boom": 1179, "whoosh": 1180}[kind])
    if kind in ("hit", "boom"):
        big = kind == "boom"
        n = int((2.4 if big else 1.1) * RATE)
        t = np.arange(n) / RATE
        attack = 1.0 - np.exp(-t / 0.002)
        lo, hi = (36.0, 92.0) if big else (46.0, 110.0)
        sub = np.sin(2 * np.pi * np.cumsum(lo + (hi - lo) * np.exp(-t / 0.06)) / RATE) * np.exp(-t / (0.85 if big else 0.32))
        lo, hi = (120.0, 260.0) if big else (140.0, 300.0)
        phase = 2 * np.pi * np.cumsum(lo + (hi - lo) * np.exp(-t / 0.025)) / RATE
        body = (np.sin(phase) + 0.6 * np.sin(2 * phase) + 0.35 * np.sin(3 * phase)) * np.exp(-t / (0.2 if big else 0.11))
        #A drum's shell: the hit's identity where a monitor speaker plays it
        lo, hi = (380.0, 520.0) if big else (400.0, 560.0)
        shell = np.sin(2 * np.pi * np.cumsum(lo + (hi - lo) * np.exp(-t / 0.03)) / RATE) * np.exp(-t / (0.08 if big else 0.05))
        strike = _lowpass(rng.standard_normal(n), 2500.0) * np.exp(-t / (0.04 if big else 0.03))
        tail = _lowpass(_lowpass(rng.standard_normal(n), 300.0), 300.0) * np.exp(-t / (0.9 if big else 0.4)) * (1 - np.exp(-t / 0.05))
        #Weighted towards the shell and the strike: the cuts land on the music's beats, whose own kick carries the low end, and
        #measured against the music in 300 Hz-4 kHz a sub-heavy hit sat 14-18 dB under it, lost on a monitor speaker
        weights = (0.6, 0.7, 1.0, 0.8, 0.25) if big else (0.35, 0.6, 1.0, 0.9, 0.2)
        x = attack * sum(w * _peak1(part) for w, part in zip(weights, (sub, body, shell, strike, tail)))
        mark = 0.0
    elif kind == "whoosh":
        n = int(0.42 * RATE)
        crest = 0.24
        t = np.arange(n) / RATE
        envelope = np.where(t < crest, np.exp((t - crest) / 0.075), np.exp(-(t - crest) / 0.045))
        sweep = np.minimum(t / crest, 1.0)
        centre = 500.0 * (3200.0 / 500.0) ** sweep
        noise = rng.standard_normal(n)
        band = np.empty(n)
        low = mid = 0.0
        damping = 1.0 / 2.2
        for i in range(n):
            f = 2.0 * math.sin(math.pi * centre[i] / RATE)
            low += f * mid
            high = noise[i] - low - damping * mid
            mid += f * high
            band[i] = mid
        band = _lowpass(_lowpass(band, 3500.0), 3500.0)
        zip_tone = np.sin(2 * np.pi * np.cumsum(800.0 * (2400.0 / 800.0) ** sweep) / RATE)
        after = np.maximum(t - crest, 0.0)
        tick = np.where(t >= crest, np.sin(2 * np.pi * 2200.0 * after) * np.exp(-after / 0.012) * (1 - np.exp(-after / 0.001)), 0.0)
        x = envelope * (_peak1(band) + 0.18 * zip_tone) + 0.35 * tick
        mark = crest
    else:
        sys.exit("no sound %r: hit, boom or whoosh" % kind)
    _SFX[kind] = (_peak1(x).astype(np.float32), mark)
    return _SFX[kind]


# ---------------------------------------------------------------- the timeline

def build_timeline(segments):
    t = 0.0
    for index, segment in enumerate(segments):
        transition = segment.get("transition", "cut") if index > 0 else "cut"
        overlap = segment.get("transitionSeconds", 0.5) if transition == "dissolve" else 0.0
        segment["_transition"] = transition
        segment["_overlap"] = overlap
        segment["_start"] = t - overlap
        t = segment["_start"] + segment["length"]
    return t


def snap_to_beats(segments, beat_times, after, tolerance=0.3, until=float("inf")):
    """Moves each cut after `after` and before `until` to the nearest beat within tolerance, by lengthening or shortening the segment
    before it. A run of slices that must keep their lengths (one game cut across scenes, #1178) lies past `until`."""
    moved = []
    for index in range(1, len(segments)):
        build_timeline(segments)
        cut = segments[index]["_start"] + segments[index]["_overlap"] * 0.5
        # A segment with "snap": false keeps its own start, and so the length of the one before it: the slices of one game cut
        # across scenes must keep their lengths, or the game's time would jump at a cut (#1178)
        if cut <= after or cut >= until or segments[index].get("snap") is False or segments[index - 1].get("snap") is False:
            continue
        nearest = beat_times[np.argmin(np.abs(beat_times - cut))]
        delta = nearest - cut
        if abs(delta) <= tolerance and abs(delta) > 1e-4:
            segments[index - 1]["length"] += delta
            moved.append((segments[index]["name"], round(cut, 3), round(nearest, 3)))
    build_timeline(segments)
    return moved


# ---------------------------------------------------------------- pictures

class Footage:
    """A filmed clip's frames, read on demand, one cached."""

    def __init__(self, folder, size):
        self.folder = folder
        self.size = size
        with open(os.path.join(folder, "capture.json"), encoding="utf-8") as f:
            self.manifest = json.load(f)
        self.count = self.manifest["frames"]
        self.fps = self.manifest["fps"]
        self._cached = (None, None)

    def frame(self, index):
        index = min(max(index, 0), self.count - 1)
        if self._cached[0] == index:
            return self._cached[1]
        with Image.open(os.path.join(self.folder, "frame-%06d.png" % (index + 1))) as image:
            image = image.convert("RGB")
            if image.size != self.size:
                image = image.resize(self.size, Image.LANCZOS)
            pixels = np.asarray(image, dtype=np.float32)
        self._cached = (index, pixels)
        return pixels

    def sound(self):
        path = os.path.join(self.folder, self.manifest.get("audio") or "audio.wav")
        return decode_audio(path) if self.manifest.get("audio") and os.path.exists(path) else None


def tracked_text(draw, font, text, tracking):
    """The width of text drawn letter by letter with tracking (a fraction of the font size) between letters."""
    width = 0.0
    for i, ch in enumerate(text):
        width += draw.textlength(ch, font=font)
        if i < len(text) - 1:
            width += tracking * font.size
    return width


def load_font(spec, pixels):
    """The edit's type ("font": {"file": <path from the repository>, "weight": <the variable font's wght axis>}), Anton when it
    names none."""
    if not spec:
        return ImageFont.truetype(FONT, pixels)
    path = spec["file"] if os.path.isabs(spec["file"]) else os.path.join(ROOT, spec["file"])
    font = ImageFont.truetype(path, pixels)
    if "weight" in spec:
        axes = font.get_variation_axes()
        font.set_variation_by_axes([spec["weight"] if a.get("name") in (b"Weight", "Weight") else a["default"] for a in axes])
    return font


def render_lines(lines, size, tracking=0.08, colour=(255, 255, 255), shadow=0.65, line_gap=0.18, font_spec=None, only=None):
    """Text lines in the edit's type (Anton by default), centred, tracked out, white with a soft dark shadow under them for
    legibility over any picture: an RGBA image cropped to the text and its shadow. Each line is (text, scale) with scale a
    multiple of `size` pixels. "only" (first, end) draws just those letters of the first line, laid out where the whole line puts
    them, on the whole line's canvas: a line revealed word by word is one such layer per word."""
    pad = int(size * 0.5)
    fonts = [load_font(font_spec, max(8, int(size * scale))) for _, scale in lines]
    probe = ImageDraw.Draw(Image.new("L", (8, 8)))
    widths = [tracked_text(probe, font, text, tracking) for (text, _), font in zip(lines, fonts)]
    heights = [font.getbbox("HXg")[3] - font.getbbox("HXg")[1] for font in fonts]
    width = int(max(widths)) + 2 * pad
    height = int(sum(heights) + line_gap * size * (len(lines) - 1)) + 2 * pad
    alpha = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(alpha)
    y = pad
    for row, ((text, _), font, w, h) in enumerate(zip(lines, fonts, widths, heights)):
        x = (width - w) / 2
        top = font.getbbox("HXg")[1]
        for i, ch in enumerate(text):
            if only is None or (row == 0 and only[0] <= i < only[1]):
                draw.text((x, y - top), ch, font=font, fill=255)
            x += draw.textlength(ch, font=font) + tracking * font.size
        y += h + line_gap * size
    glow = alpha.filter(ImageFilter.GaussianBlur(size * 0.12))
    layer = np.zeros((height, width, 4), dtype=np.float32)
    text_a = np.asarray(alpha, dtype=np.float32) / 255.0
    shadow_a = np.asarray(glow, dtype=np.float32) / 255.0 * shadow
    out_a = text_a + shadow_a * (1 - text_a)
    safe = np.maximum(out_a, 1e-6)
    for c in range(3):
        layer[..., c] = colour[c] * text_a / safe
    layer[..., 3] = out_a
    return layer


def load_logo(width):
    with Image.open(LOGO) as image:
        image = image.convert("RGBA")
        height = round(image.height * width / image.width)
        image = image.resize((width, height), Image.LANCZOS)
        pixels = np.asarray(image, dtype=np.float32)
    layer = pixels.copy()
    layer[..., 3] /= 255.0
    return layer


def composite(frame, layer, cx, cy, scale, opacity):
    """Lays an RGBA layer (float, colour 0-255, alpha 0-1) over the frame, centred at (cx, cy) in pixels, scaled and faded."""
    if opacity <= 0:
        return
    if abs(scale - 1.0) > 1e-3:
        h, w = layer.shape[:2]
        image = Image.fromarray(np.clip(layer[..., :3], 0, 255).astype(np.uint8))
        a = Image.fromarray(np.clip(layer[..., 3] * 255, 0, 255).astype(np.uint8))
        size = (max(1, round(w * scale)), max(1, round(h * scale)))
        layer = np.concatenate([np.asarray(image.resize(size, Image.BICUBIC), dtype=np.float32),
                                np.asarray(a.resize(size, Image.BICUBIC), dtype=np.float32)[..., None] / 255.0], axis=2)
    h, w = layer.shape[:2]
    x0, y0 = int(round(cx - w / 2)), int(round(cy - h / 2))
    fx0, fy0 = max(0, x0), max(0, y0)
    fx1, fy1 = min(frame.shape[1], x0 + w), min(frame.shape[0], y0 + h)
    if fx1 <= fx0 or fy1 <= fy0:
        return
    part = layer[fy0 - y0:fy1 - y0, fx0 - x0:fx1 - x0]
    a = part[..., 3:4] * opacity
    region = frame[fy0:fy1, fx0:fx1]
    region *= (1 - a)
    region += part[..., :3] * a


def smooth(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def card_motion(local, length, fade_in=0.35, fade_out=0.3, push=0.06):
    """A card's opacity and scale at `local` seconds into it: faded in and out, pushed slowly from 1+push to 1."""
    opacity = smooth(local / fade_in) * smooth((length - local) / fade_out)
    scale = 1.0 + push * (1.0 - smooth(local / length))
    return opacity, scale


# ---------------------------------------------------------------- render

def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("edit")
    parser.add_argument("--size", default=None, help="override the edit's size, e.g. 1920x1080 for a quick preview")
    parser.add_argument("--out-dir", default=None, help="where the master goes (default: the edit's outDir, else %%TEMP%%)")
    parser.add_argument("--out-name", default=None)
    parser.add_argument("--dry", action="store_true", help="print the timeline and the beats; film and render nothing")
    parser.add_argument("--refilm", action="store_true", help="film every clip again")
    parser.add_argument("--range", default=None, help="render only from:to seconds of the timeline, for a look")
    parser.add_argument("--stems", action="store_true",
                        help="also write the music, the game and the trailer's own sounds as WAVs, each at its level in the mix")
    arguments = parser.parse_args()

    edit = load_edit(arguments.edit)
    size_text = arguments.size or edit.get("size", "3840x2160")
    width, height = parse_size(size_text)
    fps = int(edit.get("fps", 60))
    segments = edit["segments"]
    out_dir = arguments.out_dir or edit.get("outDir") or os.environ.get("TEMP", ".")
    if not os.path.isabs(out_dir):
        out_dir = os.path.join(ROOT, out_dir)
    os.makedirs(out_dir, exist_ok=True)
    out_name = arguments.out_name or edit.get("name", "trailer")

    # The music first: the switch and the beats decide the cuts. A track's "at" may name a segment ("segment:<name>"): it comes in
    # where that segment begins. "onBeat" moves its offset into the recording to the nearest beat, so it comes in on one
    tracks = edit.get("music", [])
    total_guess = build_timeline(segments)
    by_name = {s["name"]: s for s in segments}
    grids = []
    for entry in tracks:
        at = entry.get("at", 0.0)
        if isinstance(at, str):
            entry["at"] = by_name[at.split(":", 1)[1]]["_start"]
        signal, _ = music_track(entry, 1.0)
        bpm, times = beats(signal)
        # A generated track may miss the tempo it was asked for and the onsets may lock onto a subdivision: "bpm" and "downbeat"
        # (seconds into the recording, its drop say) state the grid instead
        if entry.get("bpm"):
            bpm = float(entry["bpm"])
            period = 60.0 / bpm
            anchor = float(entry.get("downbeat", 0.0))
            first = anchor - math.floor(anchor / period) * period
            times = np.arange(first, len(signal) / RATE, period)
        if entry.get("onBeat"):
            wanted = entry.get("from", 0.0)
            entry["from"] = float(times[np.argmin(np.abs(times - wanted))])
        # The track's beats on the timeline, from its offset, repeated as the loop repeats
        loop = len(signal) / RATE
        timeline = []
        for k in range(int(math.ceil((total_guess + 30) / loop)) + 1):
            timeline.extend(entry["at"] + times + k * loop - entry.get("from", 0.0))
        timeline = np.array([t for t in timeline if t >= entry["at"] - 1e-6])
        grids.append((entry["file"], bpm, timeline))
        print("music %s: %.1f BPM, at %.3f s, from %.3f s into the recording" % (entry["file"], bpm, entry["at"], entry.get("from", 0.0)))

    moved = []
    if edit.get("snap") and grids:
        # Only the cuts after the switch (the switch itself is where the second track was placed), or after "snapFrom" seconds
        # when the edit holds a moment in place before it (a first shot laid on the music's drop, #1178)
        until = edit.get("snapUntil")
        if isinstance(until, str):
            until = by_name[until.split(":", 1)[1]]["_start"] - 1e-3
        moved = snap_to_beats(segments, grids[-1][2], max(tracks[-1]["at"], edit.get("snapFrom", 0.0)) + 1e-3,
                              until=until if until is not None else float("inf"))
    total = build_timeline(segments)
    frames_total = int(round(total * fps))

    # A card over the footage may name a segment ("segment:<name>", plus "offset" seconds): it follows that segment wherever the
    # snapped cuts have put it
    for card in edit.get("cards", []):
        if isinstance(card.get("at"), str):
            card["at"] = by_name[card["at"].split(":", 1)[1]]["_start"] + card.get("offset", 0.0)

    print("timeline %.3f s, %d frames at %d fps, %dx%d" % (total, frames_total, fps, width, height))
    for segment in segments:
        kind = "card" if "card" in segment else "clip"
        print("  %6.3f  %-18s %-4s %5.2f s  %s%s" % (segment["_start"], segment["name"], kind, segment["length"], segment["_transition"],
                                                    " %.2f" % segment["_overlap"] if segment["_overlap"] else ""))
    for name, before, after in moved:
        print("  snapped the cut into %s from %.3f s to the beat at %.3f s" % (name, before, after))
    if arguments.dry:
        return

    # Film what is missing, and only what the cut uses: the frames are written from a quarter of a second before the clip's "in"
    # (the capture's world does not depend on where its window starts: frames before it are drawn and not written), for the
    # clip's length and half a second more. At 4K a frame is 6-9 MB, so filming each play clip from its "from" would write some
    # 90 GB for a minute's trailer
    lead = 0.25
    for segment in segments:
        if "capture" in segment:
            capture = segment["capture"]
            start = segment.get("in", 0.0)
            # "trim": false keeps the window at the clip's "from": a camera path counts from the window's start, so the slices of
            # one orbit filmed in many scenes (the last shot, #1178) must all start their window at the same moment
            skip = max(0.0, start - lead) if segment.get("trim", True) else 0.0
            capture["from"] = round(capture.get("from", 0.0) + skip, 4)
            segment["_in"] = start - skip
            if "seconds" not in capture:
                capture["seconds"] = round(segment["_in"] + segment["length"] + 0.5, 2)
    built = False
    footage = {}
    for segment in segments:
        if "capture" in segment:
            folder, built = film(segment, edit, size_text, built, arguments.refilm)
            footage[segment["name"]] = Footage(folder, (width, height))
            clip = footage[segment["name"]]
            need = (segment["_in"] + segment["length"]) * clip.fps
            if clip.count < need - 1:
                sys.exit("%s has %d frames, the cut needs %d: film more seconds" % (segment["name"], clip.count, need))

    # ---- sound: the game's stem, the music, the mix
    samples = int(round(frames_total / fps * RATE))
    game = np.zeros((samples, 2), dtype=np.float32)
    for segment in segments:
        if "capture" not in segment or segment.get("sound") is None:
            continue
        sound = footage[segment["name"]].sound()
        if sound is None:
            continue
        a = int(round(segment["_in"] * RATE))
        n = int(round(segment["length"] * RATE))
        piece = sound[a:a + n].copy()
        gain = 10 ** (segment["sound"] / 20.0)
        # Crossfades: in over this segment's overlap, out over the next one's
        index = segments.index(segment)
        fade_in = int(segment["_overlap"] * RATE)
        fade_out = int(segments[index + 1]["_overlap"] * RATE) if index + 1 < len(segments) else 0
        if fade_in:
            piece[:fade_in] *= np.linspace(0, 1, fade_in)[:, None]
        if fade_out:
            piece[-fade_out:] *= np.linspace(1, 0, fade_out)[:, None]
        start = int(round(segment["_start"] * RATE))
        end = min(samples, start + len(piece))
        game[start:end] += piece[:end - start] * gain

    music = np.zeros((samples, 2), dtype=np.float32)
    for i, entry in enumerate(tracks):
        at = entry.get("at", 0.0)
        until = tracks[i + 1].get("at", total) if i + 1 < len(tracks) else total
        _, piece = music_track(entry, until - at)
        piece = piece * (10 ** (entry.get("gain", 0.0) / 20.0))
        # Stops dead at the switch (a few ms, no click); the last fades out at the end
        tail = int(entry.get("switchFade", 0.04) * RATE) if i + 1 < len(tracks) else int(edit.get("endFade", 2.5) * RATE)
        if tail:
            piece[-tail:] *= np.linspace(1, 0, tail)[:, None]
        start = int(round(at * RATE))
        end = min(samples, start + len(piece))
        music[start:end] += piece[:end - start]

    # Ducking under a segment that asks for it, ramped over 0.15 s
    duck = np.ones(samples, dtype=np.float32)
    ramp = int(0.15 * RATE)
    for segment in segments:
        if segment.get("duck"):
            a, b = int(segment["_start"] * RATE), int((segment["_start"] + segment["length"]) * RATE)
            level = 10 ** (-abs(segment["duck"]) / 20.0)
            envelope = np.ones(samples, dtype=np.float32)
            envelope[a:b] = level
            for k in range(ramp):
                f = k / ramp
                if 0 <= a - ramp + k < samples:
                    envelope[a - ramp + k] = 1 + (level - 1) * f
                if 0 <= b + k < samples:
                    envelope[b + k] = level + (1 - level) * f
            duck = np.minimum(duck, envelope)
    music *= duck[:, None]

    # The trailer's own sounds on the cuts (#1178): a segment's "sfx" ({"sound", "gain", "offset", "pan"}, or a list of them) at
    # its first frame, the sound's mark (a whoosh's crest) on the cut; the edit's "sfx" list at a stated "at" (seconds, or
    # "segment:<name>"). Their peak sits at the mix's "sfx" level before the gain
    effects = np.zeros((samples, 2), dtype=np.float32)
    sfx_duck = np.ones(samples, dtype=np.float32)
    placed = []
    wanted = [(entry, segment["_start"]) for segment in segments
              for entry in (segment["sfx"] if isinstance(segment.get("sfx"), list) else [segment["sfx"]] if segment.get("sfx") else [])]
    for entry in edit.get("sfx", []):
        at = entry["at"]
        wanted.append((entry, by_name[at.split(":", 1)[1]]["_start"] if isinstance(at, str) else float(at)))
    for entry, at in wanted:
        sound, mark = synth_sfx(entry["sound"])
        when = at + entry.get("offset", 0.0) - mark
        pan = max(-1.0, min(1.0, entry.get("pan", 0.0)))
        gains = (math.cos((pan + 1) * math.pi / 4) * math.sqrt(2), math.sin((pan + 1) * math.pi / 4) * math.sqrt(2))
        level = 10 ** (entry.get("gain", 0.0) / 20.0)
        a = int(round(when * RATE))
        lo, hi = max(0, a), min(samples, a + len(sound))
        if hi > lo:
            for c in range(2):
                effects[lo:hi, c] += sound[lo - a:hi - a] * level * gains[c]
        placed.append({"sound": entry["sound"], "at": round(at + entry.get("offset", 0.0), 3), "gain": entry.get("gain", 0.0)})
        # "duck": the music dips by that much under the sound's mark for 0.12 s, in over 10 ms and back over 0.25 s, so a hit on a
        # beat is heard over the beat's own kick in the band a monitor speaker plays (measured: 7-11 dB under it without)
        if entry.get("duck"):
            level = 10 ** (-abs(entry["duck"]) / 20.0)
            centre = when + mark
            for edge_from, edge_to, value_from, value_to in ((centre - 0.01, centre, 1.0, level), (centre, centre + 0.12, level, level),
                                                             (centre + 0.12, centre + 0.37, level, 1.0)):
                b0, b1 = max(0, int(edge_from * RATE)), min(samples, int(edge_to * RATE))
                if b1 > b0:
                    ramp = np.linspace(value_from, value_to, b1 - b0, dtype=np.float32)
                    sfx_duck[b0:b1] = np.minimum(sfx_duck[b0:b1], ramp)

    def rms_db(x):
        return 20 * math.log10(math.sqrt(float(np.mean(x.astype(np.float64) ** 2))) + 1e-12)

    def peak_db(x):
        return 20 * math.log10(float(np.max(np.abs(x))) + 1e-12)

    mix_settings = edit.get("mix", {})
    # The stems set against each other by their loudness (RMS where they sound), then the edit's own offsets
    music_level = rms_db(music[np.abs(music).max(axis=1) > 1e-4]) if np.any(music) else -120
    game_level = rms_db(game[np.abs(game).max(axis=1) > 1e-4]) if np.any(game) else -120
    music_gain = 10 ** ((mix_settings.get("music", -16.0) - music_level) / 20.0) if np.any(music) else 0.0
    game_gain = 10 ** ((mix_settings.get("game", -22.0) - game_level) / 20.0) if np.any(game) else 0.0
    music *= sfx_duck[:, None]
    effects_gain = 10 ** (mix_settings.get("sfx", -9.0) / 20.0)
    mix = music * music_gain + game * game_gain + effects * effects_gain
    if arguments.stems:
        # Each stem as it enters the mix (before the knee), to measure what a cut's sound adds over the music in a band
        for stem_name, stem in (("music", music * music_gain), ("game", game * game_gain), ("sfx", effects * effects_gain)):
            stem_path = os.path.join(out_dir, "%s-%s.wav" % (out_name, stem_name))
            with wave.open(stem_path, "wb") as w:
                w.setnchannels(2)
                w.setsampwidth(2)
                w.setframerate(RATE)
                w.writeframes((np.clip(stem, -1.0, 1.0) * 32767).astype("<i2").tobytes())
            print("stem", stem_path)
    # A soft knee above -7 dBFS, then a peak at -4 dBFS at most: the AAC encoder overshoots the PCM's peak by 2.6 dB on this
    # material (measured: a mix held at -1 dBFS decoded back at +1.6, one at -3.0 at -0.4), so the headroom is the codec's. The
    # decoded file's own peak is measured and printed at the end
    knee = 10 ** (-7 / 20.0)
    over = np.abs(mix) > knee
    mix[over] = np.sign(mix[over]) * (knee + (1 - knee) * np.tanh((np.abs(mix[over]) - knee) / (1 - knee)))
    peak = float(np.max(np.abs(mix))) if mix.size else 0.0
    if peak > 10 ** (-4 / 20.0):
        mix *= 10 ** (-4 / 20.0) / peak
    levels = {"musicRmsBefore": round(music_level, 1), "gameRmsBefore": round(game_level, 1),
              "mixPeak": round(peak_db(mix), 1), "mixRms": round(rms_db(mix), 1)}
    print("sound: music %.1f dB RMS -> %s, game %.1f dB RMS -> %s, mix peak %.1f dBFS, RMS %.1f dBFS"
          % (music_level, mix_settings.get("music", -16.0), game_level, mix_settings.get("game", -22.0), levels["mixPeak"], levels["mixRms"]))

    # ---- pictures: cards and the logo prepared once
    unit = height / 2160.0
    prepared = {}
    for segment in segments:
        card = segment.get("card")
        if card:
            layers = []
            if card.get("logo"):
                layers.append(("logo", load_logo(int(width * card.get("logoWidth", 0.42))), card.get("logoY", 0.36)))
            if card.get("lines"):
                lines = [(text, scale) for text, scale in card["lines"]]
                layers.append(("text", render_lines(lines, 150 * unit, tracking=card.get("tracking", edit.get("tracking", 0.08)),
                                                    font_spec=edit.get("font")), card.get("textY", 0.5)))
            # A second block at a height of its own: the website's address, set apart from the lines above it (#1178)
            if card.get("footer"):
                lines = [(text, scale) for text, scale in card["footer"]]
                layers.append(("text", render_lines(lines, 150 * unit, tracking=card.get("tracking", edit.get("tracking", 0.08)),
                                                    font_spec=edit.get("font")), card.get("footerY", 0.88)))
            prepared[segment["name"]] = layers
    # A card over the footage: its lines, or "parts" of one line each revealed at its own "reveal" second (the words of "SHOOT.
    # MATCH. BRING IT DOWN." as each happens, #1178), every part laid where the whole line puts it
    overlays = []
    for card in edit.get("cards", []):
        tracking = card.get("tracking", edit.get("tracking", 0.08))
        if card.get("parts"):
            gap = card.get("gap", "  ")
            text = gap.join(card["parts"])
            layers, first = [], 0
            for part, reveal in zip(card["parts"], card["reveal"]):
                layers.append((reveal, render_lines([(text, card.get("scale", 1.0))], 150 * unit, tracking=tracking,
                                                    font_spec=edit.get("font"), only=(first, first + len(part)))))
                first += len(part) + len(gap)
            overlays.append((card, layers))
        else:
            lines = [(text, scale) for text, scale in card["lines"]]
            overlays.append((card, [(0.0, render_lines(lines, 150 * unit, tracking=tracking, font_spec=edit.get("font")))]))

    # ---- render and encode
    first, last = 0, frames_total
    if arguments.range:
        a, b = (float(v) for v in arguments.range.split(":"))
        first, last = int(a * fps), min(frames_total, int(b * fps))
    path = os.path.join(out_dir, out_name + "-master.mp4")
    container = av.open(path, "w", options={"movflags": "+faststart"})
    video = container.add_stream("libx264", rate=fps)
    video.width, video.height, video.pix_fmt = width, height, "yuv420p"
    video.codec_context.options = MASTER
    video.codec_context.gop_size = max(1, fps // 2)
    for attribute in ("color_primaries", "color_trc", "colorspace", "color_range"):
        setattr(video.codec_context, attribute, 1)
    audio = container.add_stream("aac", rate=RATE)
    audio.layout = "stereo"
    audio.bit_rate = 320000
    per_frame = RATE / fps

    for k in range(first, last):
        t = k / fps
        frame = np.zeros((height, width, 3), dtype=np.float32)
        for segment in segments:
            start, length = segment["_start"], segment["length"]
            if not (start <= t < start + length):
                continue
            local = t - start
            if "capture" in segment:
                clip = footage[segment["name"]]
                picture = clip.frame(int(round((segment["_in"] + local) * clip.fps)))
            else:
                picture = np.zeros((height, width, 3), dtype=np.float32)
                card = segment["card"]
                for kind, layer, y in prepared[segment["name"]]:
                    opacity, scale = card_motion(local, length, fade_in=card.get("fadeIn", 0.35), fade_out=card.get("fadeOut", 0.3),
                                                 push=card.get("push", 0.05))
                    composite(picture, layer, width / 2, y * height, scale, opacity)
            if segment["_overlap"] and local < segment["_overlap"]:
                a = smooth(local / segment["_overlap"])
                frame = frame * (1 - a) + picture * a
            else:
                # A copy: the footage keeps its last frame cached, and the cards and dips below draw into this one
                frame = picture.copy()
        # Dips to black around a "dip" cut, and through white around a "white" one (the owner's fade from the logo into the
        # worlds, #1178)
        for segment in segments:
            if segment["_transition"] in ("dip", "white"):
                half = segment.get("transitionSeconds", 0.4) / 2
                distance = abs(t - segment["_start"])
                if distance < half:
                    if segment["_transition"] == "dip":
                        frame *= smooth(distance / half)
                    else:
                        w = 1.0 - smooth(distance / half)
                        frame = frame * (1.0 - w) + 255.0 * w
        for card, layers in overlays:
            local = t - card["at"]
            if 0 <= local < card["length"]:
                opacity, scale = card_motion(local, card["length"], fade_in=card.get("fadeIn", 0.35), fade_out=card.get("fadeOut", 0.3),
                                             push=card.get("push", 0.06))
                cy = card.get("y", 0.5) * height
                # A soft dark band behind text over the footage ("band": its darkness): a thin face over a busy frame needs more
                # than its own shadow to be read
                if card.get("band", 0.0) > 0:
                    half = layers[0][1].shape[0] * 0.9
                    rows = np.arange(height, dtype=np.float32)
                    falloff = np.clip(1.0 - np.abs(rows - cy) / half, 0.0, 1.0)
                    shade = 1.0 - card["band"] * opacity * (falloff * falloff * (3 - 2 * falloff))
                    frame *= shade[:, None, None]
                for reveal, layer in layers:
                    shown = smooth((local - reveal) / card.get("partFade", 0.12)) if card.get("parts") else 1.0
                    composite(frame, layer, card.get("x", 0.5) * width, cy, scale, opacity * shown)
        if edit.get("endFadeVideo"):
            remaining = total - t
            if remaining < edit["endFadeVideo"]:
                frame *= smooth(remaining / edit["endFadeVideo"])

        pixels = np.clip(frame + 0.5, 0, 255).astype(np.uint8)
        source = av.VideoFrame.from_ndarray(pixels, format="rgb24")
        encoded = source.reformat(format="yuv420p", dst_colorspace=Colorspace.ITU709)
        encoded.pts = k - first
        for packet in video.encode(encoded):
            container.mux(packet)

        a, b = round(k * per_frame), round((k + 1) * per_frame)
        chunk = np.ascontiguousarray(mix[a:b].T)
        if chunk.shape[1]:
            sound = av.AudioFrame.from_ndarray(chunk, format="fltp", layout="stereo")
            sound.sample_rate = RATE
            sound.pts = a - round(first * per_frame)
            for packet in audio.encode(sound):
                container.mux(packet)
        if (k - first + 1) % (fps * 5) == 0:
            print("cut %d of %d frames (%.0f s)" % (k - first + 1, last - first, t), flush=True)

    for packet in video.encode():
        container.mux(packet)
    for packet in audio.encode():
        container.mux(packet)
    container.close()

    # The file as a player hears it: its sound decoded back, its length and its peak after the codec
    decoded = decode_audio(path)
    levels["decodedPeak"] = round(peak_db(decoded), 1)
    print("%s: %.1f MB, %d frames, %.3f s; its sound %.3f s, peak %.1f dBFS after AAC"
          % (path, os.path.getsize(path) / 1e6, last - first, (last - first) / fps, len(decoded) / RATE, levels["decodedPeak"]))
    if levels["decodedPeak"] > -0.5:
        print("WARNING: the decoded sound peaks at %.1f dBFS: lower the mix's levels" % levels["decodedPeak"])

    cutlist = {
        "format": "bs3d-trailer-cutlist", "edit": os.path.relpath(arguments.edit, ROOT), "size": [width, height], "fps": fps,
        "seconds": round(total, 3), "frames": frames_total, "levels": levels,
        "music": [{"file": f, "bpm": round(b, 2), "firstBeats": [round(x, 3) for x in g[:8]]} for f, b, g in grids],
        "snapped": [{"segment": n, "from": a, "to": b} for n, a, b in moved],
        "sfx": placed,
        "segments": [{"name": s["name"], "start": round(s["_start"], 3), "length": round(s["length"], 3), "in": s.get("in", 0.0),
                      "transition": s["_transition"], "overlap": s["_overlap"],
                      "source": footage[s["name"]].folder if "capture" in s else "card"} for s in segments],
    }
    with open(os.path.join(out_dir, out_name + "-cutlist.json"), "w", encoding="utf-8") as f:
        json.dump(cutlist, f, indent=2)


if __name__ == "__main__":
    main()
