---
title: The camera that tripped on the island's edge
date: 2026-10-19T08:00:00Z
topics: [Camera, Testing]
summary: A camera path with a switch in it hops. How logging the lens every frame found three terms behind one kink, and what a smooth curve cost.
thumb: /img/blog/thumbs/the-camera-that-tripped-on-the-island.jpg
---

When a shot brings down a big part of the cluster, the camera lets go of the cannon and follows the wreckage down the funnel. In the scenes where the island floats, in space, in a dream, on the sea, it goes on out under the island and looks up. Antonín's note about it was short: as the camera comes down beside the rim it hops away from the edge, which is not smooth, and it may swing out wider if that is what it takes not to stumble on the island.

A smoothing filter would have hidden the hop, and made the camera lag the wreckage it follows. We wanted the term that causes it. So we logged the lens every frame in scripted drops (a copy of one level's map in each scene, a bomb bringing down 64 balls), and replayed the real camera code in a test against a falling subject, so that every roll of its random tilt could be swept. The number to watch is the second difference of the lens's position, its acceleration, in units a second squared, where a unit is a ball's width. Over the sweep the open scenes had a median of about 4,000 and a worst case of 18,000.

## Three terms, one kink

1. **A switch on the subject's height.** While the wreckage was above the island's top face the lens was lifted above it, and the rim's push applied only after the subject crossed. A lens whose own reach was under the rim, the smaller tilts, crossed that switch and was shoved 9 units sideways in two frames. That was the hop.
2. **The dive.** Going under, the camera's elevation was pulled 40 degrees in about a quarter of a second, the lens falling 90 units a second, and then stopped dead.
3. **Corners in the progress.** The camera's three legs, summed, ran at a different rate on each side of the island's top and the throat's foot. On the sea the dive and the submerge pull also added together, so the lens plunged at twice the speed.

## One curve

The fix takes the switch out. The lens's radius is raised until it stands 34.5 units from the drain's axis at any elevation: the stone's 26, a clearance and a margin of 7, through a soft maximum so the corner is a bend. That keeps it clear of the rim by construction. The old push stays as a net, and nothing switches it on the subject any more.

The dive is unhurried: it spans 12 units of the wreckage's descent, about a second, with a quintic ease, so the acceleration is continuous at both ends. It also has a clock of at most 1.1 seconds, because a group lingering in the throat sinks at a third of a falling ball's speed, and otherwise the lens would stand at the wall for as long as the group lingered. On the sea one pull is used, the stronger of the two. In the open scenes the progress is one monotone cubic through the same anchors.

| Scene, seed | Before | After |
|---|---|---|
| Space, 1 | 8,783 | 173 |
| Space, 2 | 3,790 | 116 |
| Dream, 1 | 4,955 | 114 |
| Dream, 2 | 596 | 196 |
| Sea, 1 | 2,203 | 188 |
| Sea, 2 | 4,480 | 471 |
| Volcano, 1 (grounded) | 3,569 | 3,408 |
| Volcano, 2 (grounded) | 2,440 | 1,764 |

The lens's peak acceleration in the game, in units a second squared. The volcano's island stands on the ground, so its path was left alone: its two columns differ only by the physics' run-to-run variation.

The price is that the shot stands farther out, up to 43 units, for the smaller drops, and passes the drum's side for about a third of a second.

<figure>
  <a href="/img/blog/drop-space-before-after-large.jpg"><img src="/img/blog/drop-space-before-after.jpg" width="1196" height="450" loading="lazy" alt="Two rows of three frames from the same drop in the space scene. In the top row the camera starts close up against the island's blank wall, then looks down into the funnel from above the rim. In the bottom row it passes outside the rim, goes under the island and looks up at the funnel."></a>
  <figcaption>The space scene, before the fix (top) and after (bottom), about two to three seconds into the same scripted drop. A screenshot stalls the frame, so the rows are close in time, not equal. In the top left frame the lens is pressed against the island's wall. Click for a larger one.</figcaption>
</figure>

## How we know it stays fixed

`DropCinematicPathTests` flies the real cinematic against a falling subject: seven open scenes, two sizes of drop, forty tilts, at 60 and 144 frames a second. It holds the second difference under 800 and the lens outside the rim. The worst case after the fix is about 450. We ran it against the old camera code to see it fail, and the first three of its checks did.

A still strip cannot show motion, which was the one thing Antonín had to judge. He watched it, and had no objection.
