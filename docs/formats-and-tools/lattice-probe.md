# The lattice probe (Tools/LatticeProbe, #674)

Part of [Formats and tools](../formats-and-tools.md).

Procedural "randomness" (water, clouds, grass, stars, craters, flowers) that the eye reads as a repeating grid was fixed inside one scene nine times in four months, each time after the owner found it: the shader's author cannot find it first, because the eye completes a pattern. **The probe says whether a screenshot carries a lattice.** An instrument, not a gate.

```
LatticeProbe <image.png | folder>... [--tile 256] [--stride 128] [--crosshair 48] [--null-tiles 200] [--flagged]
LatticeProbe --selftest        # fields of known answer; exit 3 on a failure; CI runs it on every push
```

## The method

`TileDetector`: 256 px tiles at a stride of 128, each **detrended** by a least-squares quadratic (a sky gradient leaks into the lowest bins), **Hann-windowed** and transformed; every local maximum of the power spectrum within **periods `MIN_PERIOD`–`MAX_PERIOD` (3–23 px)** is divided by the median of its own 9×9 neighbourhood, so a lattice is a peak standing out of the spectrum round it and a gradient scores near 1. A tile's score is its largest ratio; it also reports that peak's period and orientation and **`peaks`**, how many maxima stand over the null (a description, not a verdict).

- **A score means** (`NullModel`): the 99.9th percentile of the same score over pure 1/f² noise, simulated at start from a fixed seed through the same pipeline, so a lattice-free field is flagged on about one tile in a thousand.
- **Refused tiles**, each a false positive found on real captures: under `TEXTURE_FLOOR` (2 levels RMS; 8-bit quantisation steps are perfectly periodic), a tile with **one isolated straight edge** (a horizon, a rim: seven rows or columns over `EDGE_ISOLATION` (6×) the median of the rest) and any tile under the **crosshair**.
- **`--selftest`** holds it to synthetic fields of known answer. ⚠ **A hundred and fifty exact plane waves ARE a lattice** (discrete frequencies every tile shares): a self-test built that way read 100 % and a wrong conclusion about the probe was posted to #674 and retracted. **Full-cell jitter is printed, not asserted**: it has a zero in its own spectrum at the lattice frequency, so a clean row there clears nothing.

## The sweep: `Tools/lattice-sweep.ps1 -Out <folder>`

The Testbed once per scene and vantage with `nopost` (film grain hides what is sought), `nooverc`, the overlay off (F12), a held camera and `shotframe=240` (`sceneseed=3`), then the probe over the folder (table in `lattice.txt`). Four vantages look **away from the island**: `out` (`campos 0,-4,30` → `0,-6,200`), `sky` (→ `0,40,200`), `graze` (`campos 0,-9,30` → `0,-8.8,200`), `down` (`campos 0,10,60` → `0,-12,58`: two units off the vertical, since a straight-down look-at degenerates to one flat field). `play` is off by default (the cluster's own tiles flag). **Read the picture before believing a share.**

First sweep (2026-09-29, 1600×900), the baseline a fix is compared against: **space** 22–67 % (strongest at **8.83 px, on the axes**: the star fields as one cell grid, which #370 had fixed once; moon and aurora show the same layer at 10–12 % and 5–8 %); **meadow** 83 % `down` (a knot at every lattice point of the seam's gradient noise, `GradientNoise2` being exactly 0 there; the seam is the zero set of two noises since: **2 % after**); **polar** 23–41 % (the sastrugi's parallel ridges: a knot fix did not move it); **designed lattices** (city windows, the grid scene) 30–100 %; clean (≤ 7 %): forest, savanna, outback, dream, storm, mountain.

⚠ **A share is not a defect.** A STRIPED texture (ridges, ripples, crests) is one dominant wave (one orientation, two peaks) and is read as a lattice; a 2-D grid needs two. Thin rows and the vantages' blindness to a scene's own play view mean a column of zeros clears nothing. **The probe cannot see a recurrence longer than a tile** (the meadow's `Hash21` repeats every 50 cells in x and 100 in y) **nor a full-cell jitter**. A flagged scene is a lead; the picture is the judge. Not built (#674): a hash audit through the real HLSL, a point-field battery for fields that are too *even*, and the fixes by class (each wants a before/after and the owner's verdict).
