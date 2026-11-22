---
title: Hot air, only far away
date: 2026-11-22T12:00:00Z
topics: [Rendering, Shaders]
summary: The outback's far horizon wavers over a sliver of mirrored sky, drawn by bending a copy of the frame only where the world is far away.
thumb: /img/blog/thumbs/hot-air-only-far-away.jpg
---

<figure>
<video class="clip" width="1056" height="336" muted loop playsinline controls preload="none" poster="/video/blog/hot-air-only-far-away/skyline-poster.jpg" aria-label="Red desert under a pale sky. Two great red rock domes and a few smaller rocks stand on the horizon, and the edge of the plain and the feet of the rocks rise and sink by a few pixels over a wavering band of pale mirrored sky, while the plain in front stays still.">
<source src="/video/blog/hot-air-only-far-away/skyline.webm" type="video/webm">
<source src="/video/blog/hot-air-only-far-away/skyline.mp4" type="video/mp4">
</video>
<figcaption>The outback's skyline beside the gun on High, cut from a 3840×2160 frame of the game's capture mode with the film grain off. Only the band at the horizon moves. Six seconds that loop.</figcaption>
</figure>

That is the far edge of the outback, and the air over it is hot. The feet of the rocks rise and sink over a thin strip of mirrored sky. Nothing in that strip was painted: it is the frame itself, bent.

## What a heat shimmer is

On a hot day the air just over the ground is hotter than the air above, and that bends light. A ray heading for the far ground is bent back up before it gets there, so you see the sky where the ground should be. That is a mirage, the wet look of a hot road.

The hot air never holds still: it rises in eddies, and the far horizon wavers with it. The sand at your feet stays put, seen through too little hot air to move.

## The trick: bend a copy, only far away

A shimmer does not change a colour; it changes which point you see. So once the sky and the land are drawn, the game copies a strip of the frame at the horizon and draws it again, each pixel reading the copy from a little higher or lower. Under the skyline it reads the copy mirrored, and that is the sliver of sky.

The trap is anything near. A pixel of sky above a near rock, told to read lower, would find the rock and smear it upwards. So the strip is a ring far out around the camera, hidden wherever something nearer stands, and a read that lands on anything near brings nothing back.

The island, the gun and the balls are drawn afterwards, so they never wobble.

## Bigger, because Antonín asked

Our first version moved the horizon by at most 0.07 degrees, a fast fine flicker. Antonín's verdict on it: "It looks great! … But the effect is visually too small - a player will hardly notice it - it should be bigger."

It now moves four times as far, up to 0.28 degrees. Scaling up the fine flicker alone tore the feet of the rocks into slivers. So most of the new size is a slow swell that lifts whole stretches of the skyline at once, with the first version's flicker on top.

To see it, set Quality to High or Ultra, pause any level, choose Scene and pick Outback.
