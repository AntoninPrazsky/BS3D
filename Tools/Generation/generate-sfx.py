"""Stable Audio 3 sound-effect renders for BS3D (#482), CPU only.

    venv/Scripts/python.exe generate-sfx.py prompts.json [--model small-sfx-base] [--out out] [--steps 50] [--cfg 7]

Every prompt in the JSON list ({"name", "prompt", "negative", "duration", "seed"}) is rendered once, written as
<out>/<name>-<seed>.wav (float32, the model's own sample rate) with a .txt beside it carrying everything that shaped
the sound: model, prompt, negative prompt, duration, steps, cfg, seed, seconds taken, peak and RMS. That sidecar is
what a render is re-made from, the way the design-references renders are.
"""
import argparse, json, math, sys, time
from pathlib import Path

import torch
import torchaudio


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prompts")
    ap.add_argument("--model", default="small-sfx-base")
    ap.add_argument("--out", default="out")
    ap.add_argument("--steps", type=int, default=50)
    ap.add_argument("--cfg", type=float, default=7.0)
    ap.add_argument("--device", default="cpu")
    ap.add_argument("--count", type=int, default=1, help="renders per prompt, consecutive seeds from the prompt's seed")
    ap.add_argument("--normalize", type=float, default=None, help="peak-normalize every render to this dBFS (e.g. -1); the sidecar keeps the raw peak")
    args = ap.parse_args()

    from stable_audio_3 import StableAudioModel
    from stable_audio_3.loading_utils import load_diffusion_cond
    from stable_audio_3.model_configs import all_models

    t0 = time.time()
    torch.set_num_threads(max(1, torch.get_num_threads()))
    # StableAudioModel.from_pretrained, with one correction: the base model's config points its T5Gemma text encoder
    # at the GATED stable-audio-3-small-sfx repository (repo_id + subfolder), although the base repository ships the
    # same encoder folder itself. Point every conditioner at the model's own repository instead, so a model that is
    # not gated loads without a Hugging Face login.
    cfg = all_models[args.model]
    local_config, local_ckpt = cfg.resolve()
    model_config = json.loads(Path(local_config).read_text(encoding="utf-8"))
    for c in model_config.get("model", {}).get("conditioning", {}).get("configs", []):
        conf = c.get("config", {})
        if conf.get("repo_id") and conf["repo_id"] != cfg.repo_id:
            print(f"[sfx] conditioner {c.get('type')}: repo_id {conf['repo_id']} -> {cfg.repo_id}", flush=True)
            conf["repo_id"] = cfg.repo_id
    half = args.device != "cpu"
    inner = load_diffusion_cond(model_config, local_ckpt, device=args.device, model_half=half)
    inner.use_lora = False
    inner.lora_names = []
    model = StableAudioModel(inner, model_config, args.device, half)
    sample_rate = model.model.sample_rate
    print(f"[sfx] {args.model} loaded on {args.device} in {time.time() - t0:.1f} s, {sample_rate} Hz, {torch.get_num_threads()} threads", flush=True)

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    items = json.loads(Path(args.prompts).read_text(encoding="utf-8"))
    rows = []
    for it in items:
        for k in range(args.count):
            seed = int(it.get("seed", 1)) + k
            duration = float(it.get("duration", 3))
            negative = it.get("negative", "")
            t1 = time.time()
            audio = model.generate(
                prompt=it["prompt"], negative_prompt=negative or None, duration=duration,
                steps=args.steps, cfg_scale=args.cfg, seed=seed,
            )
            secs = time.time() - t1
            wav = audio[0].detach().to(torch.float32).cpu()  # [channels, samples]
            peak = float(wav.abs().max())
            rms = float(wav.pow(2).mean().sqrt())
            clip = float((wav.abs() >= 0.99).float().mean())
            if args.normalize is not None and peak > 0:
                wav = wav * (10 ** (args.normalize / 20) / peak)
            base = out / f"{it['name']}-{seed}"
            torchaudio.save(str(base) + ".wav", wav, sample_rate)
            rms_db = 20 * math.log10(max(rms, 1e-9))
            meta = (
                f"name: {it['name']}\nmodel: {args.model}\nseed: {seed}\nduration: {duration}\nsteps: {args.steps}\n"
                f"cfg: {args.cfg}\nsample_rate: {sample_rate}\nchannels: {wav.shape[0]}\nsamples: {wav.shape[1]}\n"
                f"seconds: {secs:.1f}\npeak_raw: {peak:.3f}\nrms_raw_dbfs: {rms_db:.1f}\nclipped: {clip * 100:.2f}%\n"
                f"normalized_to_dbfs: {args.normalize}\n\nprompt: {it['prompt']}\nnegative: {negative}\n"
            )
            (Path(str(base) + ".txt")).write_text(meta, encoding="utf-8")
            rows.append((it["name"], seed, it["prompt"], duration, secs, peak, rms_db, clip))
            print(f"[sfx] {base.name}.wav  {duration:.1f} s  seed {seed}  {secs:.1f} s  peak {peak:.2f}  rms {rms_db:.1f} dBFS  clip {clip * 100:.1f}%", flush=True)

    # One page of players beside the files, so a batch is listened to in a browser rather than clicked through in
    # Explorer: opened from disk, <audio> plays the wav next to it. Grouped by sound, the prompt under each row.
    html = ["<!doctype html><meta charset='utf-8'><title>SFX renders</title>",
            "<style>body{font:14px system-ui;margin:24px;max-width:1100px}h2{margin-top:32px}"
            ".r{display:grid;grid-template-columns:320px 1fr;gap:8px 16px;align-items:center;padding:6px 0;border-top:1px solid #ddd}"
            ".p{color:#555;font-size:12px;grid-column:1/3}.bad{color:#b00}</style>",
            f"<h1>{out.name}</h1><p>{args.model}, steps {args.steps}, cfg {args.cfg}, normalized to {args.normalize} dBFS</p>"]
    last = None
    for name, seed, prompt, duration, secs, peak, rms_db, clip in rows:
        if name != last:
            html.append(f"<h2>{name}</h2>"); last = name
        flag = " <span class='bad'>clipped</span>" if clip > 0.005 else ""
        html.append(f"<div class='r'><audio controls preload='none' src='{name}-{seed}.wav'></audio>"
                    f"<div><b>seed {seed}</b> · {duration:.1f} s · raw peak {peak:.2f}, rms {rms_db:.0f} dBFS{flag}</div>"
                    f"<div class='p'>{prompt}</div></div>")
    (out / "index.html").write_text("\n".join(html), encoding="utf-8")
    print(f"[sfx] {out / 'index.html'}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
