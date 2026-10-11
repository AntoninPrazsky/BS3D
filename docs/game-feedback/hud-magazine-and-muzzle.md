# The magazine strip, the chips and the muzzle's marks

Part of [The game: feedback](../game-feedback.md). Sibling of [the in-play HUD](hud.md).

How the player reads "which round fires next" and "is it special": a 2D strip in the corner and 3D marks on the gun.

## The magazine strip (bottom right)

- **Why:** from the play pose the barrel hides the muzzle end, so the next round shows as a small ellipse and the rest through glass.
- Flat discs **in FIRING order, head leftmost and largest, ringed**. **The HEAD's origin is solved, for a full magazine ending flush with the margin**, so it does not slide as the row shortens.
- **`HUD_MAG_GAP` is the clear space between drawn edges** (halo to halo): measured between fills, each halo ate `HUD_MAG_RIM` and the discs read as touching blobs.
- Colours via `TypeColor`: **no second palette**. A dark halo behind each disc and a light outline inside, or the 8-ball is a hole.
- **A wildcard's disc cycles with the ball it stands for** (#330): every tint off the magazine (strip, muzzle mark, aim beam) goes through `GameplayScreen.LoadedColour`.
- **Never more discs than shots left** (`ScoreKeeper.ShotsRemaining`), and the barrel agrees (#262): `CollectMagazineBalls` takes the same `Math.Min(Magazine.SIZE, remaining)` (see `docs/game-session.md`).
- **Discs are antialiased**: `DrawDisc` draws each disc and ring as ONE quad from a cached coverage texture. ⚠ The old scanline version used `MathF.Round` (.5 to even), drawing half the rows twice and the rest never (a combed strip that reads as a shader fault): floor(x + 0.5).

## The power-up chips

- **Swap** (`Ｅ`, pad `⇐` X) above the strip: the count if more than one, dimmed and "Swap used" once spent. **Brake** (`Ｑ`, pad `⇑` Y) one row above, also dimmed when no pressure step is left to give back (#692); a **Cut** chip stands in the column. Each only on a level that offers it (Swap from chapter three, Brake from four; "The tools' cards" in `docs/game-session.md`).
- **Own fonts** (#703): `HUD_FONT_CHIP_PROMPT`/`HUD_FONT_CHIP_DETAIL` are the card pair times 0.8 (a hint up all level at card size was far too huge); `HUD_SWAP_ABOVE_STRIP` = `HUD_MARGIN`.
- **Keycaps stand in one column** (#694): `ChipColumnLayout` sizes it by the widest glyph either device draws, captions start at one left edge, measured once per font pair so it never moves between "Swap" and "Swap used" or keyboard and pad. `ChipColumnLayoutTests`.

## The muzzle round's mark: a collar on the gun

- **A ripple failed** (#175): it adds white light, washing out the colour to be read (a same-hue flare was invisible, and the negative branch is one `float3` per draw, so it cannot carry a slot's hue). **A camera-facing billboard failed** (#425): depth-read, it showed whatever sliver the barrel did not occlude, never the same shape twice (`BallGlow` is deleted).
- **Now `CannonRig.DrawMuzzleCollar`**: a lathed steel band ringing the tube just behind the muzzle face, glowing in the round's colour, opaque, turning with the gun. **Around the tube, not on the face**: the play camera sees the gun from behind.
- Rate `MUZZLE_MARK_HZ`, gated by `_previewBeamVisible` ("would a shot leave the barrel this instant"). Its breath was set by argument: captures scheduled against a 1.6 Hz sine measure the sampler, so quote no screen figure without a frame-accurate capture path.
- **`CannonRig.CollarHue`** (#478): ball tints keep a floor in their other channels, so the ring was pastel; the normalised hue is pushed twice as far from its own grey (`COLLAR_SATURATION`) and renormalised (a per-channel power turns yellow orange). A bright saturated colour clips every channel and goes **white**. The push comes from the sky (`SetCollarSky`, fed by `ApplySkyLighting`, `COLLAR_BRIGHTNESS_DIM` to `COLLAR_BRIGHTNESS_BRIGHT`): **the sky decides how loud, never what colour**.
- **Precise aim (#321):** leaned in, nothing rejects the mark, so its strength falls with `_preciseAim.Blend` to `MUZZLE_GLOW_ADS_STRENGTH` (a floor: the only 3D statement of the next colour) and its breath is damped out. The overview is untouched: `PreciseAim.Step` snaps `Blend` to exactly 0 on release.

## A special round turns rays round the muzzle (#820)

- While the bore holds a wildcard or the Cutter and `_previewBeamVisible`, `Effects.MuzzleRays` draws one quad **bolted to the barrel**, square to the bore at `CannonRig.MuzzleFaceZ`: nothing inside `CannonRig.CollarRadius`, a corona, two sets of rays turning opposite ways (seven broad, thirteen fine, hashed lengths, #640), added light, depth-read.
- **Wildcard: a rainbow** (any colour). **Cutter: the zap's cyan** (`CUTTER_RAYS_HUE`), and its collar too (a red collar under cyan rays read as a red ball). Over-driven or whitened roots went white; keep `MUZZLE_RAYS_RADIANCE` unwhitened.
- Leaned in: strength 25 %, reach 1.35 with `_preciseAim.Blend`. **The strip says it too**: `PlayHud.DrawRoundRays` bakes a burst (`RayBurstTexture`) behind that round's disc.
- Cost about 0.015 ms (3840×1600, `High`, Pennant, `wildcard=1`). **Not on the Potato path** (no `MuzzleRays` in GamePi). The Cutter's strip disc still shows its dealt colour (#766).

## The loaded balls do not breathe (#252)

Balls in the barrel take no ripple and no heartbeat: they glow steadily, and the pulsing is the gun's, in one place. The pulse uniforms are per renderer, so it is a different DRAW: `BallRenderSet` keeps **two planes** of buckets, `BallDrawFrame.Add`'s `still` flag routes into the second, and **each pass states its own `PulseDepth`** (never inherits). **Released balls take the still plane too** (#473); the shot in flight still breathes. The Testbed still breathes its magazine.
