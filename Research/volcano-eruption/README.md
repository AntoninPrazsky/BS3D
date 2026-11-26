# The volcano's eruption, before and after #1260

Material for a blog post (the owner, 2026-11-26: "we could write a post about it, with exactly the animation on the verdict page").

- `eruption.gif`: one burst side by side, before (left) and after (right), in real time: 70 frames at 10 a second, the crater cropped out of 960x540 frames.
- `intro-before.jpg`, `intro-after.jpg`: the Eruption chapter's intro, its eruption shot at 4.6, 5.2, 5.8, 6.4 and 6.85 s of the intro's clock.

## What changed

Every lava blob's launch speed took the eruption **as it was now**, so a blob already in the air took the new speed every frame. The whole fountain swelled to full height within the burst's 0.6 s attack, then shrank back into the crater as one object through the decay, its blobs slowing in mid-air. The owner called it "an amateur PowerPoint animation". Now each blob takes the eruption of the moment it left the vent and flies its own parabola: the fountain grows as faster blobs are thrown and falls back as they come down. The shader is handed the eruption's last 3.75 s as sixteen samples, and the particle count and pixels stay the same.

The jet's top above the crater rim, in pixels of a 960x540 frame, against seconds after the burst:

| | +0.5 | +0.7 | +1.2 | +1.7 | +2.2 | +3.0 | +3.5 | +4.0 |
|---|---|---|---|---|---|---|---|---|
| before | 204 | 274 | 209 | 155 | 125 | 98 | 73 | 49 |
| after | 65 | 82 | 95 | 158 | 229 | 233 | 226 | 124 |

## Filming it again

The Game's capture mode, on a level of the Eruption chapter that is not its first (so no intro), from a stated camera on the crater. Bursts fall at `(k + 0.10 + 0.55 * Hash01(k)) * 19` s of game time (k = 1: 30.74 s):

```
BS3D.exe capture=<folder> userdata=<scratch> level=102 capturefps=20 capturefrom=30.2 captureseconds=7 capturesize=960x540 campos=70,0,30 camtarget=-45,175,-250 fov=50
```

The intro's shot: `level=101 seed=7 quality=high introshot=4.0,4.6,5.2,5.8,6.4,6.85` through `Tools/quiet-capture.ps1`. The "before" build is any build older than merge 0706b175, such as the v0.6.1 release.
