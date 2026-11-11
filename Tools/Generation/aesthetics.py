"""Audiobox Aesthetics scores for music renders, to order candidates before the owner listens (#1089).

    python aesthetics.py <file>... [--json out.jsonl]

Prints one row per file, highest Content Enjoyment (CE) first, with Content Usefulness (CU), Production Complexity
(PC) and Production Quality (PQ): Meta's Audiobox Aesthetics predictor (CC-BY-4.0, pip `audiobox_aesthetics`), on the
CPU, about 3 s a 60 s loop after a one-off model load.

What it was measured to say (#1089, 2026-11-11), on 38 recordings the owner rejected (every one a regeneration merge
of #824, #841, #909-#915 replaced because he said it sounded bad) against the 43 he kept (their replacements and the
ember family):
  - CE is the one axis with a signal: it ranks a kept recording above a rejected one 72 % of the time (AUC 0.72), the
    replacement beat the recording it replaced on 27 of 37 paths, and on #1087's blind test the owner's best pick
    scored above his worst in 4 of 5 briefs.
  - Every recording under CE 7.08 was one he rejected (the 8 lowest of 81); the lowest he kept is magma-dnb at 7.08.
    So CE under 7.0 marks a render to drop before it reaches him. It is a floor, not a judge: rejected recordings
    reach 7.87 and kept ones start at 7.08, so above the floor his ear still decides.
  - CU, PQ and PC separate little (AUC 0.52-0.60). A CLAP prompt contrast ("polished, in tune" against "amateurish,
    out of tune") separated nothing (AUC 0.45-0.54).
Scores are comparable across files and runs; the absolute numbers are the model's 1-10 scale.

Needs a venv with audiobox_aesthetics and soundfile (C:\\Users\\panrd\\AI\\audioscore\\venv on the desktop).
"""
import argparse, json, os, sys

FLOOR = 7.0  # CE under this: below every recording the owner kept (#1089)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="+")
    ap.add_argument("--json", help="also write one JSON line per file here")
    args = ap.parse_args()

    import soundfile as sf
    import torch
    from audiobox_aesthetics.infer import initialize_predictor

    torch.set_num_threads(os.cpu_count() or 4)
    predictor = initialize_predictor()
    rows = []
    for f in args.files:
        x, sr = sf.read(f, dtype="float32", always_2d=True)
        r = predictor.forward([{"path": torch.from_numpy(x.T.copy()), "sample_rate": sr}])[0]
        r = json.loads(r) if isinstance(r, str) else r
        rows.append({"file": f, **{k: round(float(v), 3) for k, v in r.items()}})
    rows.sort(key=lambda r: -r["CE"])
    width = max(len(os.path.basename(r["file"])) for r in rows)
    print(f"{'file':{width}}    CE    CU    PC    PQ")
    for r in rows:
        flag = "  under the floor: drop" if r["CE"] < FLOOR else ""
        print(f"{os.path.basename(r['file']):{width}}  {r['CE']:4.2f}  {r['CU']:4.2f}  {r['PC']:4.2f}  {r['PQ']:4.2f}{flag}")
    if args.json:
        with open(args.json, "w", encoding="utf-8") as f:
            for r in rows:
                f.write(json.dumps(r) + "\n")


if __name__ == "__main__":
    sys.exit(main())
