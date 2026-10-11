# The result page looks up at the fireworks

Part of [The game: feedback](../game-feedback.md).

`Fireworks` bursts its shells 44 to 122 units over the island, a height tuned against the play camera, which looks up at the cluster (anything lower falls behind it). But the shells are launched on the clear while that camera is still up, and then the result page hands the camera to the menu orbit, which looks level across the arena at the trophy, so the rest of the show goes off above the frame. The fix is that the page glances up (`ResultPage`), not that the shells come down: lowering the burst zone would spoil the camera it was tuned for, and a player who has just won should be shown the arena and the sky.

- **The cycle**: the aim is lifted `GLANCE_HEIGHT` (28 units) on a cycle of rise, hold, return the same way, then the rest of the period on the arena and its trophy: `GLANCE_PERIOD` 12.7 s, `GLANCE_RISE` and `GLANCE_HOLD` 3.2 s each. The lens widens by `GLANCE_FOV_WIDEN_DEGREES` on the lift's own smoothstep, so the pitch stays comfortable and most of the burst zone stays in shot; the price is that the highest shells leave the top of the frame.
- ⚠ **Do not lift further to catch the high shells.** Lifted 46 units on a 60 degree lens the pitch was 52 degrees at the top, which puts the frame's top edge at 82 degrees ("as if we had fallen on our backs"); the orbit's own pitch at rest is under 4 degrees, so it is the glance and only the glance. A 1.6 s rise read the same way; the rise was doubled.
- **Only while something is in the air.** A camera that pitches up at an empty sky reads as a fault, so the glance is gated on `Fireworks.Active`, and the clock is reset when the sky is empty (not left running), so the first glance after a burst starts at its start.
- **Folded into the orbit's target before the release blend**, so the first seconds, while the page is still easing off the gun's pose, are not yanked upward too.
- Smoothstep at both ends: a linear rise starts with a lurch, which reads as the frame being dragged rather than somebody looking.

The fireworks themselves are in the victory-fireworks section of this area.
