---
title: The momentum we used to throw away
date: 2026-11-24T12:00:00Z
topics: [Physics, Gameplay]
summary: A shot now hands its momentum to the cluster it hits, so the balls lean and swing, and the landing preview had to learn to aim at a cluster that moves.
thumb: /img/blog/thumbs/the-momentum-we-used-to-throw-away.jpg
---

<figure>
<video class="clip" width="1056" height="594" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/zigzag-poster.jpg" aria-label="A cannon on a stone island under a blue sky fires a yellow ball at a sheet of red, yellow and white balls hanging from a glass ceiling over a meadow. The ball sticks to the lower edge of the sheet, and the whole sheet leans away from the shot and swings back.">
<source src="/video/blog/the-momentum-we-used-to-throw-away/zigzag.webm" type="video/webm">
<source src="/video/blog/the-momentum-we-used-to-throw-away/zigzag.mp4" type="video/mp4">
</video>
<figcaption>Zigzag, the third level of The Meadow, filmed by the game's capture mode from beside the island, with the wind off so that only the shot moves the balls. 3.6 seconds that loop.</figcaption>
</figure>

Until this week, that sheet would not have moved at all.

## Physics we had promised

In BS3D the cluster hangs on real physics: every ball is a body, held to its neighbours by springy sockets. Yet a shot flew in at 200 units a second, snapped into its cell and stopped dead.

Antonín played the whole game through and noted: "There is no sign of the impact's energy on the other balls … players then say I promised physics and there is none."

The stop was deliberate. A landed shot is put straight into its cell and tied to its neighbours there, and its speed was set to zero so that nothing it still carried would tug at the new sockets. So its momentum simply vanished.

## Where the momentum goes now

Now the cluster gets it. A ball that sticks to what it hits carries all of its momentum into it, so that is the push the game hands over: the shot's mass times its velocity.

Handed to the one ball it hit, that much stretched its sockets 0.2 to 0.8 of a ball's width past their length, and at 0.4 a cluster looks torn open. So it is spread through the sockets, five steps out, each step taking 0.7 of the one before. Here is the hand-out, trimmed:

```csharp
// The walk: every ball within
// five steps of the one hit, and
// its step (0 for that ball).
foreach (var (ball, step) in walk)
{
  // each step weighs 0.7 of the
  // one before; all the weights
  // together add up to 1
  float share =
    MathF.Pow(0.7f, step) / total;
  Vector3 kick =
    push * share / ball.Mass;

  // never more than 8 units a
  // second for any one ball
  if (kick.Length() > 8f)
    kick *= 8f / kick.Length();
  ball.Velocity += kick;
}
```

Spread like this, the push stretches a socket by two hundredths of a ball's width at most, and the shot's own cell never moves. A shot that bounces off pushes too, and so does the Cut when it strikes.

<figure class="grid grid-2" data-together>
<div class="cell"><video class="clip" width="544" height="592" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/zigzag-without-poster.jpg" aria-label="The Zigzag sheet close up: a yellow ball arrives at its lower edge and sticks, a pale ripple of light runs through the balls, and the sheet stays where it hung."><source src="/video/blog/the-momentum-we-used-to-throw-away/zigzag-without.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/zigzag-without.mp4" type="video/mp4"></video><span class="cell-label">Before: the shot only lights the balls</span></div>
<div class="cell"><video class="clip" width="544" height="592" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/zigzag-with-poster.jpg" aria-label="The same moment with the push: the ball sticks, the light runs through the sheet, and its lower part swings away to the right and back."><source src="/video/blog/the-momentum-we-used-to-throw-away/zigzag-with.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/zigzag-with.mp4" type="video/mp4"></video><span class="cell-label">Now: the sheet answers the blow</span></div>
<figcaption>The same shot into Zigzag at the same moment, without the push and with it.</figcaption>
</figure>

After a second, the swing is gently damped, so the cluster comes to rest a few seconds after the hit. The shape decides how much it moves.

<figure class="grid grid-2" data-together>
<div class="cell"><video class="clip" width="544" height="592" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/pendant-without-poster.jpg" aria-label="A strand of coloured balls hanging from a glass ceiling over a desert, ending in a wide purple bulb. A ball sticks to the bulb, a pale light runs through the balls, and the bulb sways only a little."><source src="/video/blog/the-momentum-we-used-to-throw-away/pendant-without.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/pendant-without.mp4" type="video/mp4"></video><span class="cell-label">Before: the strand barely sways</span></div>
<div class="cell"><video class="clip" width="544" height="592" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/pendant-with-poster.jpg" aria-label="The same moment with the push: the ball sticks, and the bulb swings off to the right and back."><source src="/video/blog/the-momentum-we-used-to-throw-away/pendant-with.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/pendant-with.mp4" type="video/mp4"></video><span class="cell-label">Now: the bulb swings out</span></div>
<figcaption>Pendant, the fifth level of The Coil, without the push and with it.</figcaption>
</figure>

<figure class="grid grid-2" data-together>
<div class="cell"><video class="clip" width="544" height="592" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/cannonball-without-poster.jpg" aria-label="An upside-down pyramid of red and blue balls hanging from a glass ceiling over a meadow. A ball sticks under its tip, a pale light runs through the balls, and the pyramid stays still."><source src="/video/blog/the-momentum-we-used-to-throw-away/cannonball-without.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/cannonball-without.mp4" type="video/mp4"></video><span class="cell-label">Before: the block hangs still</span></div>
<div class="cell"><video class="clip" width="544" height="592" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/cannonball-with-poster.jpg" aria-label="The same moment with the push: the ball sticks under the tip, the light runs through the pyramid, and it hangs as still as before."><source src="/video/blog/the-momentum-we-used-to-throw-away/cannonball-with.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/cannonball-with.mp4" type="video/mp4"></video><span class="cell-label">Now: it hardly moves</span></div>
<figcaption>Cannonball, the fourth level of The Meadow, without the push and with it: the two are hard to tell apart, because a block shares the push among many more balls. Our tests say the same in numbers. They hang a level twice and fire the same shot into both, one with the push: Pendant's farthest ball strays 1.3 to 1.6 ball widths from its twin, Cannonball's 0.04 to 0.29.</figcaption>
</figure>

## The ghost had to learn to lead

The game shows where a shot will stick: a ghost ball in the cell it will land in. A swinging cluster broke that promise. Fire again while the last hit still swings, and while the ball flies, its target moves.

We fired sixteen quick shots, 0.3 seconds apart, into each of six levels, and one in eight landed beside its ghost. Two changes fixed it.

The preview now asks the ball it aims at how fast it moves, and leads it like a moving target. Each shot also remembers its ghost's cell, and keeps it if it strikes the ball that ghost was worked out against. With both, all 96 of those shots landed where the ghost showed, and 174 of 179 over a longer run.

It all comes with the next release. Fire into the bottom of a sheet and watch it lean.
