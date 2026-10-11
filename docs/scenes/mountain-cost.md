# The mountain's frame cost

Part of [The scenes](../scenes.md). Continues [The mountains](mountains.md).

## The "38 FPS mountain" was the present path (#270)

A 6900 XT playtest at 3840x1600 / 75 Hz reported about 38 FPS, which is 75 / 2: not a cost. The frame is under 5 ms (`level=Column`, `quality=high ssaa=2`, 1600x900: 200 FPS under `fpscap=200`), yet vsync presented 37.5 while `fpscap=75` held 75.0 for the same work, and under vsync the result was not even monotonic in `ssaa` (`ssaa` 2 gave 37.5, 1 and 4 gave 75). It took a heavy cluster, not the backdrop.

**The fix was to stop vsyncing**: the Game presents immediately and `Game/Platform/FrameLimiter.cs` holds the rate on the CPU. Safe because the game is only windowed or borderless (`HardwareModeSwitch = false`), so DWM owns the flip. It aims a few percent over the refresh (78 on a 75 Hz panel: a limiter never pays back an overrun). `fpscap=N` exists in the Game because a vsync-capped level can only say "dearer than one refresh".

## The real cost

- **12.35 ms** fullscreen, 3840x1600, `quality=high` (`ssaa` 2), `level=Column`, `nocap`, against the **13.3 ms a 75 Hz panel allows**: about a millisecond of headroom. The empty scene (`campos=0,2,45 camtarget=0,-4,0`, dome 8) is 10.5 ms.
- **It is pixel work with no look-identical reduction left** (#296; `Full.json`, windowed 3840x1600): `ssaa` 1 3.19 ms, `ssaa` 2 10.99, a fixed 0.59 plus 2.60 per unit of shaded pixels. An early `break` out of `Fbm2BandLimited` measured 11.10 (a real `[loop]` costs more than it saves); hoisting `ddx`/`ddy` measured noise.
- **Other scenes, same rig** (fullscreen, `quality=high`, `fpscap=400`): cavern Onion 5.96 ms, savanna Heart 8.30, desert Basket 9.35, dream (front end, `preview=Onion`) 7.20 ("The quality tier" in `docs/game-shell.md`).

## Traps

- ⚠ **A `Level` file carries its own scene, dome and weather, and `GameplayScreen.Session.cs` applies all three over the command line.** `level=Column` with `scene=meadow` drew the mountain: the `[game] scene` startup line prints what was *asked for*, the `[fps]` line what draws. Compare scenes on the front end or on levels that name them (trap 11 in `.claude/skills/benchmark/SKILL.md`).
- ⚠ **The windowed `ssaa` 4 stand-in for 4K** (23.0 Mpix: 12.0 predicted, 12.35 measured) holds for terrain scenes only. The cavern, the dream, space and the Moon shade a target the size of the **back buffer**, so a small window's `ssaa` grows only the resolve (cavern Onion: 4.00 ms windowed at `ssaa` 4, 5.96 fullscreen at 2). Measure those at the size they are played at.
