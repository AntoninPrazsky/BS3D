---
title: Hot air from a copy of the frame
date: 2026-11-21T12:00:00Z
topics: [Rendering, Shaders]
summary: How the outback's far horizon came to boil and float over a sliver of mirrored sky - a strip of the frame already drawn, read again a fraction of a degree higher or lower, and kept off everything near by how far away each pixel is.
thumb: /img/blog/thumbs/hot-air-from-a-copy-of-the-frame.jpg
---

<figure>
<video class="clip" width="1056" height="336" muted loop playsinline controls preload="none" poster="/video/blog/hot-air-from-a-copy-of-the-frame/skyline-poster.jpg" aria-label="Red desert under a pale sky. Two great red rock domes and a few smaller rocks stand on the horizon, and the edge of the plain and the feet of the rocks rise and sink by a few pixels over a wavering band of pale mirrored sky, while the plain in front stays still.">
<source src="/video/blog/hot-air-from-a-copy-of-the-frame/skyline.webm" type="video/webm">
<source src="/video/blog/hot-air-from-a-copy-of-the-frame/skyline.mp4" type="video/mp4">
</video>
<figcaption>The outback's skyline to the right of the gun, as a player sees it on High, cut at its own pixels from a 3840×2160 frame of the game's capture mode, with the film grain off. Only the band at the horizon moves; the sky above it and the plain in front of it hold still. Six seconds that loop. The air never repeats, so the last second fades into the first.</figcaption>
</figure>

That is the far edge of the outback, seen from behind the gun, and the air over it is hot. The plain's edge and the feet of the rocks rise and sink by a few pixels, and under them lies a sliver of sky where the ground should be. Nothing in the strip is a texture or an animation anyone drew. Every pixel of it is a pixel of the same frame, read again from a little higher or a little lower.

When Antonín saw the first version, he said it looked great, that it was a candidate for a blog post, and that it was too small for a player to notice. This is the post, and the clip is the bigger version his note asked for.

## What hot air does to a horizon

On a hot day the air just over the ground is hotter than the air above it, and light travels a little faster through hot air than through cool. A ray that comes down towards the ground at a shallow angle is bent back up before it gets there, and reaches your eye from below the horizon. So where the far ground should be, you see the sky, upside down: the wet look of a hot road in summer. Rocks on the horizon seem to float over their own reflection. That is a mirage, and the air is never still enough to hold it steady. The hot layer rises in plumes and eddies, each bending the light by a slightly different amount, so the far edge of the world wavers up and down. That is a heat shimmer.

Two things follow that the game has to respect. It is the far horizon that moves, not the air in front of you: a ray has to graze the ground through a long stretch of hot air before it bends far enough to notice, and the sand at your feet is seen through a few metres of it. And from high up, looking down, a ray crosses the hot layer quickly, so the shimmer fades.

## Why it cannot be paint

The outback had a heat shimmer before this one. It was a few lines in the ground's own shader: a cloudy noise, sliding upwards with the clock, mixed into the haze that pales the plain with distance. In a playtest on 13 November Antonín wrote that in the intro and elsewhere "a strange low pale 'fog' is visible creeping along the ground. It is not clear what it is and it does not look good." It was that noise. It laid blotches 7 to 11 levels of 255 paler over the whole middle plain and the rocks' walls, and because the noise was fixed to the directions the camera looks in rather than to the ground, a moving camera saw them creep across the land. We took it out.

No tuning could have fixed it, because a shader that colours the ground can only colour the ground. It runs for each pixel the ground covers and knows one thing: the point of the ground that pixel looks at. A shimmer does not change the colour of that point. It changes which point you see: the pixel that should show the plain's edge shows the sky a few pixels above it, or the rock below it. The ground's shader cannot draw the sky. It does not even know what is beside it.

The picture itself knows, once it is drawn. So the shimmer is a pass of its own, after the sky and the land and before the island, the gun and the cluster. It copies a strip of the frame drawn so far and draws that strip again, each pixel reading the copy from a direction a little higher or lower than its own. Under the skyline it also reads the copy mirrored about the horizon, and that is the sliver of sky the plain reflects. The glass ceiling the balls hang from bends what is behind it in the same way, from a copy of the frame.

## Keeping it off everything near

Reading the frame back has one trap. When the shimmer is drawn, the frame already holds the near plain and the rocks nearer than the skyline, and the strip runs behind them. A pixel of far sky just above a near rock's top, told to read a few pixels lower, finds the rock. The rock's colour would be smeared up into the sky, and a near thing would shimmer along its edge as if the hot air were in front of it. The island's stone, the gun and the cluster are no such trap: they are drawn after the shimmer, so they are never in the copy and simply cover the strip where they stand.

What stops it is how far away each pixel is. As a frame is drawn, every pixel keeps the depth of the surface it shows, so that something nearer can hide something farther. The shimmer's strip is not a rectangle on the screen but a ring round the camera, 620 units out, standing from just under the plain at that distance to a degree and a half above the horizon. The ring is depth-tested like any object: wherever something nearer than 620 units has been drawn, it is hidden. So drawing the ring marks exactly the pixels that are far (the far plain, the rocks on the skyline and the sky) and leaves everything near unmarked. Then three steps, all inside one frame:

1. **Mark.** The ring writes its mark where it passes the depth test, and nothing else.
2. **Copy.** The rows the ring covers are copied, every sample weighed by its mark, so the copy holds the far colours alone and, for each pixel, how much of it was far.
3. **Refract.** The ring is drawn again, reading the copy at moved angles, and whatever a read brings back counts only as much as it was far. A read that lands on a nearer rock brings nothing of the rock back.

The copy is only the rows the ring reaches on the screen: a strip, not the frame. The weighing has to happen before anything averages the samples inside a pixel, so the shimmer is never drawn on a multisampled frame. Resolving one averages the colours and the marks together, and a pixel at a near rock's edge would bring the rock back with it.

<figure>
  <img src="/img/blog/hot-air-from-a-copy-of-the-frame/play-view-difference-800.jpg" srcset="/img/blog/hot-air-from-a-copy-of-the-frame/play-view-difference-800.jpg 800w, /img/blog/hot-air-from-a-copy-of-the-frame/play-view-difference-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The view from behind the cannon in grey, dimmed: the gun on its island, the cluster of balls hanging above it, a big rock on the left. A thin bright ragged line runs along the horizon on both sides of the gun, broken by the gun's wheels and stopping at the rock on the left.">
  <figcaption>The play view at 1920×1080, with the first version of the shimmer and with the bigger one, at the same moment of the same game. The frame is greyed, and every pixel that differs by two levels of 255 or more is painted light: all 12,247 of them lie in 47 rows at the skyline. The gun, the cluster, the island and the near rock on the left are identical, and the strip stops at each of them. The bloom spreads a difference of one level a little further.</figcaption>
</figure>

## Why 620 units and not 1000

The issue that asked for a real shimmer put it where the plain's last fade runs, 1000 to 1300 units out. Past 1000 the plain dissolves into the sky, and that is the line the eye reads as the horizon. The ring was tried there first, and the horizon did not move.

The reason was the mark. With the ring at 1000, the horizon line was the strip's lower edge. Every read below it fell on plain nearer than the ring, which the mark calls near and which brings nothing back, so only the faded sliver above the line took the waver and the line itself stood still. In the Testbed, the game's development rig, at the gun's camera with the scene's clock stopped at two moments a quarter of a second apart, the edge between the sky and the plain moved 0.06 pixels on average. With the ring at 700 it moved 0.37.

So the ring stands inside the horizon, with a little far plain under the line for the reads to land on. It is at 620 now, a little nearer than the first version's 700, to make room for what the bigger shimmer reads under the skyline (below). It stays beyond 600 units, where the land's last fade begins in every other scene, so what it touches is still the far plain. From behind the gun that plain is the skyline's last five pixels at 1080 rows.

## An angle and a clock

One of the project's standing rules shapes everything that moves: it may not depend on the machine it runs on, neither on the screen's resolution nor on the frame rate.

The offset is an angle. A pixel's direction is moved up or down by at most 0.28 degrees and projected back through the camera to find where to read. Through the play camera's 42.9-degree lens that is 6.7 pixels at 1080 rows and 10 at 1600, and most of the time under half that. A 4K screen shows the same air moving twice as many pixels as a 1080p one, because it is the same piece of the world, and a narrower lens shows it larger, as a telephoto does.

The motion is gradient noise, a smooth random field, laid round the camera by heading and by height above the horizon. The heading runs round a circle, so there is no seam behind the lens. The clock slides the field downwards through the strip, so the pattern rises the way hot air does. That clock is the game's wall clock, never a count of frames: at 30 frames a second or at 144 the air moves as far in a second. It wraps every 600 seconds, because a float that has been counting for hours has too few bits left to slide a field smoothly, and the jump at the wrap lands in a field nothing follows.

The strength depends on how the camera looks at the horizon. A camera low over the plain, as the gun's is, about 6 units up, looks along the ground and gets the whole effect, up to a look down of 0.8 degrees, which a camera up to 14 units over the plain stays within. A camera that looks down more steeply sees through a shorter path of hot air, so the waver falls as the square of that angle and the mirage as its fourth power. That is the physics, and it was also a fix. At the first version's full strength, the intro's wide shot, which flies 30 to 52 units over the plain, showed the plain's red edge boiling up into the pale fade as a row of dark teeth along the whole skyline.

## Bigger, at Antonín's word

The first version moved the line by at most 0.07 degrees, about 1.7 pixels at 1080 rows. Antonín's verdict was: "It looks great! … But the effect is visually too small - a player will hardly notice it - it should be bigger."

<figure>
<video class="clip" width="1056" height="336" muted loop playsinline controls preload="none" poster="/video/blog/hot-air-from-a-copy-of-the-frame/skyline-first-poster.jpg" aria-label="The same view of the red desert with the first, smaller shimmer: the horizon barely moves, and only a thin pale line lies under the rocks.">
<source src="/video/blog/hot-air-from-a-copy-of-the-frame/skyline-first.webm" type="video/webm">
<source src="/video/blog/hot-air-from-a-copy-of-the-frame/skyline-first.mp4" type="video/mp4">
</video>
<figcaption>The first version, at the same place and over the same seconds of the same game: an offset of at most 0.07 degrees against today's 0.28, and a mirrored sliver 0.08 degrees deep against today's 0.2.</figcaption>
</figure>

Four times bigger sounds like one number changed, and it was not. The first field was a boil: two layers of fine noise in cells a quarter of a degree across and 0.15 degrees tall, turning over 3.5 and 9 times a second. Scaled up to 0.28 degrees it shredded the feet of the rocks into torn horizontal slivers that read as a glitch, not as air. An offset that large, changing that fast up the strip, reads the rows out of order: a row is read from lower down than the row beneath it. A numerical copy of the shader's arithmetic, at the gun's camera and 1080 rows, found rows read up to seven pixels out of order at 0.21 degrees and up to ten at 0.28.

So the size moved into a second layer. Seven tenths of the offset is now a slow swell: cells half a degree across and 0.95 degrees tall, two thirds of the strip's height over the horizon, so a column's offset barely changes up the strip and whole stretches of the skyline rise and sink together without tearing. Three tenths is the old boil, at its old speed, the flicker on top. The swell turns over once a second, where the boil's coarser layer turns over 3.5 times, and that also comes from the air: an eddy's turnover time grows as the two-thirds power of its size, and the swell's cells are about six times as tall, which makes them 3.4 times slower. It still rises 0.95 degrees a second, nearly twice the boil's 0.53. Nothing was slowed down to make the bigger size read as heat.

Two more things changed with it. In the first version a read below the horizon grew from nothing where the ring meets the plain to its full size at the horizon. At the new size that ramp, a fifth of a degree deep and shallower than the offset itself, folded the rows at the horizon into floating lumps outlined by rings. A read downwards now keeps its full size down to a floor, 0.3 of the way from where the ring meets the plain up to the horizon, and that floor is what the ring moved nearer for. And the mirage is deeper, 0.2 degrees instead of 0.08, at full strength. Its mirror line wavers only with the boil, so the sheet of mirrored sky stays at the horizon while the swell lifts and sinks the skyline over it, and the far rocks float over their own reflection.

From the gun's camera in the game at 1920×1080, the edge between sky and land now moves 0.92 pixels on average between frames a quarter of a second apart, 4.3 at the 95th percentile and 9.3 at most. The first version moved it 0.43, 1.78 and 3.45.

## What it costs

The pass draws a ring twice and copies a strip. In the Testbed at 3840×1500, with the frame drawn at 2×2 samples a pixel as High and Ultra draw it, and with the shimmer switched on and off in turn 36 times, it cost 0.09 ms a frame (6.51 against 6.42). That was a debug build, with another program's capture sharing the graphics card, so read it as about a tenth of a millisecond, out of the 13.3 a frame has at 75 frames a second.

It is drawn on High and Ultra only. Medium draws its frame multisampled, which the weighing above cannot work with, and Medium is the level the weak laptop lives on, where the same kind of copy in the middle of a frame measured 1.2 to 1.6 ms for the glass ceiling. Phones do not draw it either: copying out of the frame halfway through makes a phone's graphics chip store the whole frame and load it back, and we have not measured what that costs. The Raspberry Pi's renderer draws no outback at all.

## Seeing it, and what we do not know

No chapter is set in the outback yet, and every level brings its own scene when it starts. To see the shimmer from behind the gun, start any level, pause it, choose Scene and pick Outback, with the detail level at High or Ultra. Picked from Extras, the outback shows its own opening flight over the menu instead, and the level started after it brings back its own scene.

We measured how far the line moves, not whether it reads as heat. A number cannot say whether the swell looks like air or like a fault in the picture. Stills enlarged three times show the far plain's edge lifted into floating lumps over slivers of sky, which is the mirage we wanted, and also the part most likely to look busy. That judgement is Antonín's eye, the same eye that made it four times bigger. We have not looked at the shimmer during the camera's swoop after a big drop, through the glass ceiling from below, or on the laptop.
