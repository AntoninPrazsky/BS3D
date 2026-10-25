---
title: The ball that holds up thirty-eight others
date: 2026-10-19T12:00:00Z
topics: [Physics, Data structures]
summary: How the cluster is laid out, what it means for a ball to hang from the glass, and the bookkeeping that keeps hundreds of physics bodies honest.
thumb: /img/blog/thumbs/the-ball-that-holds-up-thirty-eight.jpg
---

The shot to look for in Bubble Shooter 3D is rarely the biggest clump of one colour. It is the one ball that holds a lot of others up. We wanted to know how rare such a ball is, so we asked a level file.

<figure>
  <img src="/img/shots/umbrella-800.jpg" srcset="/img/shots/umbrella-800.jpg 800w, /img/shots/umbrella-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="A canopy of black and gold balls hangs under glass, with a handle of red balls hanging from its middle, in front of the northern lights.">
  <figcaption>Umbrella, in The Silhouettes: a canopy hung from the glass, and a handle hung from the canopy.</figcaption>
</figure>

Umbrella has 368 balls hanging from the glass. Pretend we pull out any one of them and count what no longer reaches the top. For 351 of them the answer is nothing. For 17 it is something, and the handle is the clean case: its top ball holds 38 others, the next one 33, then 28, 23 and 18, five fewer each time. A match that includes the right ball takes the whole handle down with it.

## Twelve neighbours

That count comes from a throwaway script that runs the game's own neighbour rule over the file. The cluster is a 3D array indexed `[x, z, level]`, where *level* is the vertical axis. Balls are one unit across. Every odd level is shifted half a ball in both X and Z, and the levels are 1/√2 apart.

Do the arithmetic for a ball and the one above it: the offset is (0.5, 0.5, 0.707), and 0.25 + 0.25 + 0.5 is exactly 1. They touch. So a ball touches four in its own layer and four in each of the layers above and below, twelve at most, which is the face-centred cubic packing, the one a fruit seller builds, seen with its square layers on top.

Which cells those are depends on the level's parity, and that is the only subtle part:

```csharp
int diagonalShift = (cell.Level % 2) > 0 ? 0 : -1;

for (int levelOffset = -1; levelOffset <= 1; levelOffset += 2)
    for (int dX = 0; dX <= 1; dX++)
        for (int dZ = 0; dZ <= 1; dZ++)
            // (cell.X + dX + diagonalShift, cell.Z + dZ + diagonalShift)
            // on level cell.Level + levelOffset, when it is inside the field
```

## What hanging means

The balls of the top level hang from the glass. When a match removes a group, we walk the touching neighbours from every ball on the top level, and everything the walk does not reach is let go. That is `GetCellsDisconnectedFromCeiling`, a plain breadth-first search. The physics only mirrors it: every touching pair is joined by a Bepu ball-socket constraint, and a released ball has all of its constraints removed so that it falls alone, not as a chain.

Mirroring a graph into a solver is where it can go quietly wrong. Each pair must get exactly one constraint, so the build pass joins same-level pairs only towards +X and +Z, and cross-level pairs only from the even level of the pair; neighbouring levels always differ in parity. A ball shot in later has no constraints yet, so it joins all four same-level neighbours and both adjacent levels. Each ball keeps its constraint handles in three groups (below, beside, above) of four slots, because it touches at most four in a group.

A constraint no ball remembers is a ball that never falls. Worse, Bepu reuses handles: a stale handle can answer "exists" for a constraint that belongs to someone else, and removing it removes the wrong thing without a word. A review found no live bug, only places where one would hide. So a full slot now throws instead of dropping the handle, and `ClusterInvariants` reads the solver's own constraint lists and checks that every stored handle is a live socket between two lattice neighbours (or the ceiling), and that every touching pair has exactly one. The tests run it after every operation of 300 random landings and releases on each of four maps, with the freed handles reused by the next attach, because that is where a stale slot would start to lie.

We broke each check by hand to see it fail. One break failed nothing: joining cross-level pairs twice, starting from level 1. No shipped level has a ball on level 1: layouts hang at the top of the field. A break only proves a check if it reaches data the check reads.

## Two ways to hang

One more trap sits where the cluster meets the glass. The ceiling socket anchors a ball's own north pole to a point under the plate, and that pair of points has two solutions: the ball hanging under the anchor, or the ball standing inverted on top of it, a diameter higher. A shot dragged in from above and to one side by its new constraints could settle into the wrong one when it had few neighbours. It was a valid rest, so the body went to sleep there, 1.16 units (a bit more than a ball's width) above its cell: a ball visibly outside the glass, which Antonín reported from play. The fix is to put the body in its cell before the constraints exist, so their error is zero and nothing pushes it into the second solution.
