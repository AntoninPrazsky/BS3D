# The resolution

Part of [The game: shell](../game-shell.md).

**The player may draw the 3D below the display's resolution; a tier may not** (the owner's request of 2026-10-05, #801; #298's ruling still binds the tiers and the desktop's probe, [The quality tier](quality-tier.md)). The **Resolution** row (DISPLAY, under Fullscreen) steps down the display's ladder and round to native: native, 5/6, 2/3, 1/2 and 1/3 of its height, none under 360 rows, **every rung at the display's exact aspect** (a 21:9 panel offers only 21:9 sizes): on 1920×1080, 1600×900, 1280×720, 960×540, 640×360 (`Prazsky.Core.Render.RenderResolution`, `RenderResolutionTests`). The row shows the size drawn (`1280 x 720`, `1920 x 1080 (native)`; an `x`, since the menu's face has no multiplication sign).

## How

MonoGame 3.8.5 sizes the back buffer to the display in borderless fullscreen on both platforms, so a smaller one is not to be had without #157's mode switch. **The 3D is drawn into a target of its own and scaled up; menus, HUD, crosshair, wordmark and cup are drawn over it at the display's size**, and mouse, alt-tab and F11 are untouched.

- Desktop: `PostProcessPipeline.RenderSize` (the scene target is that size times the tier's factor; the resolve takes one bilinear tap whenever the scene is not the back buffer times the factor). Potato path: an 8-bit target through `PotatoUpscale` ("The Potato path" in `docs/rendering.md`).
- **Stored** as `renderHeight` in `Settings.json`: absent until chosen, `0` native chosen, a height otherwise (cost follows pixels). **`render=<height>`** pins a run and is never written.

## The Raspberry Pi's Auto

Until the player chooses, GamePi starts at native and **steps the 3D down once, to 1280×720** (`AUTO_RENDER_HEIGHT`) when the probe measures under the refresh less `RESOLUTION_REFRESH_MARGIN` (57 FPS at 60 Hz) **for `RESOLUTION_SUSTAIN_SECONDS` of windows in a row** (#834). One window used to be enough, and one window is what dragging the window across the desktop costs (the owner's v0.3.6 run stepped to 720p at 37 FPS on a level the Pi holds fullscreen). A window at or above the floor ends the stretch and settles the probe at native. Log `[resolution] 37 FPS in the level at 1600x900 (floor 57) — under the floor, lowering after 10 s of it`; the main menu then says "Resolution lowered to 1280 x 720 for a smoother frame rate — change it in Settings".

- The probe is `TuneQualityToFrameRate`; the Pi's tier is locked, so this is its one lever, and a level build re-opens it. **Not stored** (a stored verdict is a ratchet). A choice on the row, native included, switches Auto off; it walks Auto → native → 1600×900 → … → 640×360 → Auto. Only the Pi and the `potato` argument have Auto (`ResolutionHasAuto`). Auto quality's box says so (#832, [The quality tier](quality-tier.md)).
- **Measured on the Pi** (1920×1080, no VNC client): the scale-up costs about 0.9–1.3 ms once the scene target's depth is discarded (`TargetDiscard`; ~2.9 without). Since #804 the Auto steps none: a level of each scene held native at 61.8–62.0 FPS (2026-10-05).
