"""Stable Audio 3 sound-effect renders for BS3D on the GPU, through sa3.cpp's Vulkan build (#1088).

    python generate-sfx-sa3.py prompts.json [--model small-sfx|medium] [--out out] [--count 3] [--steps N] [--cfg X]

The same prompt list and the same outputs as generate-sfx.py: every prompt ({"name", "prompt", "negative",
"duration", "seed"}) is rendered --count times on consecutive seeds, as <out>/<name>-<seed>.wav with a .txt sidecar
holding everything that shaped it, and one index.html of players goes beside them. The difference is the engine:
sa3-generate.exe on the RX 6900 XT takes ~1.9 s a small-sfx render including the model load, where the CPU script
takes ~5.7 s a render after a 20 s load (#1088, measured 2026-11-11). Without --steps and --cfg the model's own
defaults apply (8 steps, CFG 1 for both small-sfx and medium); a negative prompt only reaches the model with --cfg > 1.

sa3-generate writes 16-bit PCM normalised to a peak of 0.96 (every render of 2026-11-11), so the sidecar's peak is
that and not the model's raw level; Tools/MusicBake sets the played level anyway.

The engine lives outside the repository: BS3D_AI_ROOT names the folder holding sa3\\bin (the release's core zip plus
ggml-vulkan.dll) and sa3\\models (models.cmd --variant small-sfx|medium). Needs numpy and soundfile (the ComfyUI venv).
"""
import argparse, html, json, os, subprocess, sys, time
from pathlib import Path

import numpy as np
import soundfile as sf


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prompts")
    ap.add_argument("--model", default="small-sfx", choices=["small-sfx", "medium", "small-music"])
    ap.add_argument("--encoding", default="f16", help="the DiT GGUF downloaded by models.cmd (f16 by default)")
    ap.add_argument("--out", default="out")
    ap.add_argument("--count", type=int, default=1, help="renders per prompt, consecutive seeds from the prompt's seed")
    ap.add_argument("--steps", type=int, default=None)
    ap.add_argument("--cfg", type=float, default=None)
    ap.add_argument("--ai-root", default=os.environ.get("BS3D_AI_ROOT", r"C:\Users\panrd\AI"))
    args = ap.parse_args()

    exe = Path(args.ai_root) / "sa3" / "bin" / "sa3-generate.exe"
    models = Path(args.ai_root) / "sa3" / "models"
    if not exe.exists():
        sys.exit(f"sa3-generate.exe not found at {exe}")
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    items = json.loads(Path(args.prompts).read_text(encoding="utf-8"))
    rows = []
    for it in items:
        duration = float(it.get("duration", 3))
        negative = it.get("negative", "")
        for k in range(args.count):
            seed = int(it.get("seed", 1)) + k
            base = out / f"{it['name']}-{seed}"
            cmd = [str(exe), "--model", args.model, "--encoding", args.encoding, "--models-dir", str(models),
                   "--prompt", it["prompt"], "--duration", str(duration), "--seed", str(seed), "--out", str(base) + ".wav"]
            if args.steps:
                cmd += ["--steps", str(args.steps)]
            if args.cfg is not None:
                cmd += ["--cfg-scale", str(args.cfg)]
                if args.cfg > 1 and negative:
                    cmd += ["--negative-prompt", negative]
            t1 = time.time()
            p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
            secs = time.time() - t1
            if p.returncode != 0 or not Path(str(base) + ".wav").exists():
                print(p.stdout[-1500:], p.stderr[-1500:], file=sys.stderr)
                sys.exit(f"sa3-generate failed on {base.name} (exit {p.returncode})")
            wav, sr = sf.read(str(base) + ".wav", dtype="float32", always_2d=True)
            peak = float(np.abs(wav).max())
            rms_db = 20 * np.log10(max(float(np.sqrt(np.mean(wav.astype(np.float64) ** 2))), 1e-9))
            meta = (
                f"name: {it['name']}\nengine: sa3.cpp (Vulkan)\nmodel: {args.model} ({args.encoding})\nseed: {seed}\n"
                f"duration: {duration}\nsteps: {args.steps or 'model default'}\ncfg: {args.cfg if args.cfg is not None else 'model default'}\n"
                f"sample_rate: {sr}\nchannels: {wav.shape[1]}\nsamples: {wav.shape[0]}\nseconds: {secs:.1f}\n"
                f"peak: {peak:.3f}\nrms_dbfs: {rms_db:.1f}\n\nprompt: {it['prompt']}\nnegative: {negative}\n"
            )
            Path(str(base) + ".txt").write_text(meta, encoding="utf-8")
            rows.append((it["name"], seed, it["prompt"], duration, secs, rms_db))
            print(f"[sfx] {base.name}.wav  {duration:.1f} s  seed {seed}  {secs:.1f} s  rms {rms_db:.1f} dBFS", flush=True)

    # One page of players beside the files, as generate-sfx.py writes: opened from disk, <audio> plays the wav next to it.
    page = ["<!doctype html><meta charset='utf-8'><title>SFX renders</title>",
            "<style>body{font:14px system-ui;margin:24px;max-width:1100px}h2{margin-top:32px}"
            ".r{display:grid;grid-template-columns:320px 1fr;gap:8px 16px;align-items:center;padding:6px 0;border-top:1px solid #ddd}"
            ".p{color:#555;font-size:12px;grid-column:1/3}</style>",
            f"<h1>{html.escape(out.name)}</h1><p>sa3.cpp {args.model}, steps {args.steps or 'default'}, cfg {args.cfg if args.cfg is not None else 'default'}</p>"]
    last = None
    for name, seed, prompt, duration, secs, rms_db in rows:
        if name != last:
            page.append(f"<h2>{html.escape(name)}</h2>")
            last = name
        page.append(f"<div class='r'><audio controls preload='none' src='{html.escape(name)}-{seed}.wav'></audio>"
                    f"<div><b>seed {seed}</b> · {duration:.1f} s · {secs:.1f} s · rms {rms_db:.0f} dBFS</div>"
                    f"<div class='p'>{html.escape(prompt)}</div></div>")
    (out / "index.html").write_text("\n".join(page), encoding="utf-8")
    print(f"[sfx] {out / 'index.html'}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
