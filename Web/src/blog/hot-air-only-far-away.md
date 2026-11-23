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

Here is that read in the shader, trimmed. It moves by an angle, never by pixels, so a laptop and a 4K screen see the same air.

```hlsl
// wobble: rising noise, -1 to 1
// Amplitude: max move, in radians
float read = elevation
    + Amplitude * reach * wobble;

// that direction's place in the
// copy, back through the camera
float4 shifted = tex2Dlod(
  GrabSampler,
  float4(centre
    + GrabUv(heading, read)
    - here, 0, 0));

// only far colour is in the copy:
// a near rock brings back nothing
color = color * (1.0 - shifted.a)
      + shifted.rgb;
```

The island, the gun and the balls are drawn afterwards, so they never wobble.

<figure>
  <img src="/img/blog/hot-air-only-far-away/play-view-difference-800.jpg" srcset="/img/blog/hot-air-only-far-away/play-view-difference-800.jpg 800w, /img/blog/hot-air-only-far-away/play-view-difference-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The view from behind the cannon in grey, dimmed: the gun on its island, the cluster of balls hanging above it, a big rock on the left. A thin bright ragged line runs along the horizon on both sides of the gun, broken by the gun's wheels and stopping at the rock on the left.">
  <figcaption>Every pixel that differs by two levels of 255 or more between the shimmer's two versions (below) at the same moment, painted light over the greyed play view: all at the skyline, stopping at the gun, the island and the near rock.</figcaption>
</figure>

## Bigger, because Antonín asked

Our first version moved the horizon by at most 0.07 degrees, a fast fine flicker. Antonín's verdict on it: "It looks great! … But the effect is visually too small - a player will hardly notice it - it should be bigger."

<figure>
<video class="clip" width="1056" height="336" muted loop playsinline controls preload="none" poster="/video/blog/hot-air-only-far-away/skyline-first-poster.jpg" aria-label="The same view of the red desert with the first, smaller shimmer: the horizon barely moves, and only a thin pale line lies under the rocks.">
<source src="/video/blog/hot-air-only-far-away/skyline-first.webm" type="video/webm">
<source src="/video/blog/hot-air-only-far-away/skyline-first.mp4" type="video/mp4">
</video>
<figcaption>The first version, at the same place over the same seconds as the clip at the top.</figcaption>
</figure>

It now moves four times as far, up to 0.28 degrees. Scaling up the fine flicker alone tore the feet of the rocks into slivers. So most of the new size is a slow swell that lifts whole stretches of the skyline at once, with the first version's flicker on top.

To see it, set Quality to High or Ultra, pause any level, choose Scene and pick Outback.
