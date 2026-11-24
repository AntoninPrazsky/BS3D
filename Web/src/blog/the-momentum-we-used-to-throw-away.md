---
title: The momentum we used to throw away
date: 2026-11-24T12:00:00Z
topics: [Physics, Gameplay]
summary: A shot now hands its momentum to the cluster it hits, so the balls lean and swing, and the landing preview had to learn to aim at a cluster that moves.
thumb: /img/blog/thumbs/the-momentum-we-used-to-throw-away.jpg
---

<figure class="grid grid-2" data-together>
<div class="cell"><video class="clip" width="544" height="638" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/pennant-without-poster.jpg" aria-label="A pennant of red, green and blue balls hanging point down from a glass ceiling over a meadow. Three balls arrive one after another from the left and stick under its tip, a pale light runs up through the balls, and the pennant hangs where it hung."><source src="/video/blog/the-momentum-we-used-to-throw-away/pennant-without.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/pennant-without.mp4" type="video/mp4"></video><span class="cell-label">Before: the shots only light the balls</span></div>
<div class="cell"><video class="clip" width="544" height="638" muted loop playsinline controls preload="none" poster="/video/blog/the-momentum-we-used-to-throw-away/pennant-with-poster.jpg" aria-label="The same three shots with the push: the lower half of the pennant swings away to the right, its tip by about a third of the picture, and back past where it hung."><source src="/video/blog/the-momentum-we-used-to-throw-away/pennant-with.webm" type="video/webm"><source src="/video/blog/the-momentum-we-used-to-throw-away/pennant-with.mp4" type="video/mp4"></video><span class="cell-label">Now: the pennant swings</span></div>
<figcaption>Three quick shots into the tip of Pennant, the first level of The Meadow, without the push and with it. Filmed by the game's capture mode close beside the cluster, with the wind off so that only the shots move the balls. 3.6 seconds that loop.</figcaption>
</figure>

Until this week, every hit looked like the one marked Before.

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

After a second, the swing is gently damped, so the cluster comes to rest a few seconds after the hit.

## The ghost had to learn to lead

The game shows where a shot will stick: a ghost ball in the cell it will land in. A swinging cluster broke that promise. Fire again while the last hit still swings, and while the ball flies, its target moves.

We fired sixteen quick shots, 0.3 seconds apart, into each of six levels, and one in eight landed beside its ghost. Two changes fixed it.

The preview now asks the ball it aims at how fast it moves, and leads it like a moving target. Each shot also remembers its ghost's cell, and keeps it if it strikes the ball that ghost was worked out against. With both, all 96 of those shots landed where the ghost showed, and 174 of 179 over a longer run.

It all comes with the next release. Quick shots add up, so fire three into the tip of the first level's pennant and watch it swing.
