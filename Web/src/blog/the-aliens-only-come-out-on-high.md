---
title: The aliens only come out on High
date: 2026-11-19T12:00:00Z
topics: [Rendering, Quality levels]
summary: "The same frame at Low, Medium, High and Ultra, and at the fifth level that only a film can use, with a slider under each picture to see what every step draws."
thumb: /img/blog/thumbs/the-aliens-only-come-out-on-high.jpg
---

## One frame, five times

The game has four detail levels you can pick in Settings: Low, Medium, High and Ultra. (There is a Potato entry too, but that is a different, low-poly renderer for the Raspberry Pi and for phones, not a rung of this ladder.) While we were making the trailer we needed a fifth, which is not in the menu and which a player could not use. This post is all five, one frame at a time. Each picture has a slider under it: drag it, or press the names under it, and the same place in the same frame is drawn again at the next level.

The pictures are the game's own capture mode, at 3840×2160, with the film grain off and the cluster's wind off so that nothing differs between them but the level. Without JavaScript the pictures simply stand one above the next.

The rule that shapes the whole ladder is old: **a level lowers what is drawn, never the pixels.** Antonín ruled in August that no level may render below the screen's own size, because a magnified frame looks ugly and no frame rate buys that back. So Low and High are the same number of screen pixels, and what differs is what goes into them.

| | Low | Medium | High | Ultra | Max |
|---|---|---|---|---|---|
| Edges | 2× MSAA | 4× MSAA | 2×2 SSAA | 2×2 SSAA | 3×3 SSAA |
| Pixels drawn, 4K frame | 8.3 M | 8.3 M | 33.2 M | 33.2 M | 74.6 M |
| Scene detail | reduced | full | full | full | full |
| Shadow map | up to 2048 | up to 2048 | as authored | twice that | twice that |
| Fine meshes, cloud heaps, window reflections, wind | no | no | yes | yes | yes |
| Distance rules | usual | usual | usual | farther | finest |

MSAA is multisampling: extra samples only where a triangle's edge crosses a pixel. SSAA is supersampling: the whole frame is drawn larger, 2×2 times the pixels or 3×3, and filtered down to the screen's size. The authored shadow map is whatever size the scene was built with, 4096 texels by default, and Ultra doubles it to 8192.

The first three rungs were made for hardware. The ladder exists for a laptop with an integrated GPU, a Ryzen 7 5700U, whose budget is 16.1 ms a frame, and Medium is what that machine lives on. Ultra was made for the opposite reason. In a playtest note in September, about blocky shadows at High, Antonín asked whether the game should instead have a level "that will never run at 75 Hz on the 6900 XT but has the highest visual quality". So Ultra is the player's alone: Auto quality never steps up to it.

## The aliens

The space chapter opens on a still shot of a planet, and a flying saucer crosses it close to the lens. In that shot the craft is 500 to 700 pixels across at 1080p, so every part of it reads. On the loop it flies round the station the rest of the time, from the play camera it is about fifty pixels across, and nothing on it is bigger than a pixel or two.

Antonín asked for more on High and Ultra: first more detail on the saucer, then that the aliens "should not be only pictures, but simple models visible behind the glass of the saucer's cabin". So Low and Medium keep a lathe for the hull and a lathe for the dome with the crew painted on it, and High and Ultra draw a craft built of parts. What caught his eye most when he compared the levels was the crew.

<figure class="tiers">
  <div class="tiers-frame">
    <div class="tiers-pane" data-label="Low, Medium">
      <img src="/img/blog/the-aliens-only-come-out-on-high/saucer-medium-720.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/saucer-medium-720.jpg 720w, /img/blog/the-aliens-only-come-out-on-high/saucer-medium-1440.jpg 1440w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1440" height="810" loading="lazy" alt="A chrome flying saucer seen from the side with a ring of pale round lights on its rim. Its dome is a flat orange, with one grey alien face painted on it.">
      <span class="tiers-tag">Low, Medium</span>
      <p class="tiers-note"><strong>Low and Medium:</strong> 2,208 triangles. The hull is one smooth lathe, the bulbs are glows painted on it and the crew is painted on the dome. The two levels draw the same craft, to within a few pixels of its edge.</p>
    </div>
    <div class="tiers-pane" data-label="High, Ultra, Max">
      <img src="/img/blog/the-aliens-only-come-out-on-high/saucer-high-720.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/saucer-high-720.jpg 720w, /img/blog/the-aliens-only-come-out-on-high/saucer-high-1440.jpg 1440w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1440" height="810" loading="lazy" alt="The same saucer built of panels with seams and grooves, glass bulbs standing proud of its rim, a landing leg and an antenna. Three grey aliens stand round a console in a lit cabin under a clear glass dome.">
      <span class="tiers-tag">High, Ultra, Max</span>
      <p class="tiers-note"><strong>High and up:</strong> 34,248 triangles. Sixteen seams, grooves, a collar, three legs, glass bulbs, and three modelled aliens in a cabin under real glass (4,200 triangles for them and the cabin, 3,648 for the dome). Ultra and Max draw this same craft.</p>
    </div>
  </div>
  <figcaption>The space chapter's saucer from the intro's still shot, cut from a 3840×2160 frame at its own pixels. Each picture stands for the levels named under it, which draw the saucer alike.</figcaption>
</figure>

The crew turn with the craft, 0.8 radians a second, so two frames half a second apart show them moving against glass whose reflections hold still. A painted crew cannot do that, and it is the thing that makes a dome look like glass with something in it.

## The meadow from behind the gun

The saucer is one object. A level is the whole frame, and the levels differ across the whole of it. This is the meadow's play view, with the HUD on, as a player sees it.

<figure class="tiers">
  <div class="tiers-frame">
    <div class="tiers-pane" data-label="Low">
      <img src="/img/blog/the-aliens-only-come-out-on-high/meadow-low-800.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/meadow-low-800.jpg 800w, /img/blog/the-aliens-only-come-out-on-high/meadow-low-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The meadow seen from behind the cannon: the gun on its stone island, a cluster of coloured balls hanging above it, green hills, a big tree on the right and the score and the magazine at the corners. The sky is streaky flat cloud with dark patches and the tree is a smooth green mass.">
      <span class="tiers-tag">Low</span>
      <p class="tiers-note"><strong>Low:</strong> scene detail is given up (the scene's cheaper shader programs), the sun's shadow map is capped at 2048, the ceiling's glass does not bend what is behind it, there is no motion blur, and edges get two samples. The sky is a flat sheet of cloud.</p>
    </div>
    <div class="tiers-pane" data-label="Medium">
      <img src="/img/blog/the-aliens-only-come-out-on-high/meadow-medium-800.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/meadow-medium-800.jpg 800w, /img/blog/the-aliens-only-come-out-on-high/meadow-medium-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The meadow seen from behind the cannon: the gun on its stone island, a cluster of coloured balls hanging above it, green hills, a big tree on the right and the score and the magazine at the corners. The sky is still streaky flat cloud, but the tree is in leaf and the ceiling&#x27;s glass shows a faceted pattern.">
      <span class="tiers-tag">Medium</span>
      <p class="tiers-note"><strong>Medium:</strong> the scene's full programs, four samples on an edge, refraction in the ceiling's glass and motion blur (a still cannot show the blur). This is the rung the weak laptop lives on. The sky is still a flat sheet.</p>
    </div>
    <div class="tiers-pane" data-label="High">
      <img src="/img/blog/the-aliens-only-come-out-on-high/meadow-high-800.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/meadow-high-800.jpg 800w, /img/blog/the-aliens-only-come-out-on-high/meadow-high-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The meadow seen from behind the cannon: the gun on its stone island, a cluster of coloured balls hanging above it, green hills, a big tree on the right and the score and the magazine at the corners. The sky holds round white cumulus clouds, and the tree has a grown fork and finer leaves.">
      <span class="tiers-tag">High</span>
      <p class="tiers-note"><strong>High:</strong> the frame is drawn at 2×2 samples a pixel. The cumulus are bodies of their own, the old tree has its fork grown in wood, and the shrubs are stems and leaf clumps. The cluster is also blown about by the wind, which is switched off for these pictures.</p>
    </div>
  </div>
  <figcaption>The meadow's play view at each level, 3840×2160 filtered down, with the game's own HUD. Ultra and Max are left out: at this size they differ from High in a quarter of a percent of the pixels, Ultra in the shadows on the stone. The cloud heaps cost 0.53 ms a frame here on our desktop.</figcaption>
</figure>

Most of the difference between Medium and High is not one big feature but a long list of small ones, each cheap and each only on High and up: the cloud heaps (0.53 ms a frame here on our desktop, and 1.4 ms under the mountains' broken sky), the old trees' grown fork (about 0.05 ms), the shrubs and the supersampling. The one expensive entry is the wind. It blows the hanging cluster about, so it is physics, not drawing: for 975 balls it takes the physics from 0.83 ms to 10.1 ms a frame. That is why it is on High and Ultra only.

A still hides two more things Medium has and Low lacks: the ceiling's glass bending the view, and the motion blur when the gun fires.

## Pixels

Supersampling is where most of the ladder's cost goes, and it is hard to see at the size of a screenshot. High draws a 4K frame at 7680×4320 and filters it down, four times the pixels, and it is the biggest cost on the ladder: on the laptop, Medium is worth 12 to 22 ms a frame over High wherever a pass scales with the pixels it shades. Here is one corner of the meadow frame, 480×270 pixels, enlarged three times without smoothing so that each pixel is a square.

<figure class="tiers">
  <div class="tiers-frame">
    <div class="tiers-pane" data-label="Low">
      <img src="/img/blog/the-aliens-only-come-out-on-high/edges-low-1440.jpg" width="1440" height="810" loading="lazy" alt="A close view of the gun&#x27;s wheel and a roller against green grass, enlarged three times so the pixels are visible. The outlines step in clear stairs.">
      <span class="tiers-tag">Low</span>
      <p class="tiers-note"><strong>Low:</strong> two samples along an edge. The wheel's rim and the rollers' outlines step in visible stairs.</p>
    </div>
    <div class="tiers-pane" data-label="Medium">
      <img src="/img/blog/the-aliens-only-come-out-on-high/edges-medium-1440.jpg" width="1440" height="810" loading="lazy" alt="A close view of the gun&#x27;s wheel and a roller against green grass, enlarged three times so the pixels are visible. The stairs are softer.">
      <span class="tiers-tag">Medium</span>
      <p class="tiers-note"><strong>Medium:</strong> four samples along an edge. The steps are shorter and softer.</p>
    </div>
    <div class="tiers-pane" data-label="High">
      <img src="/img/blog/the-aliens-only-come-out-on-high/edges-high-1440.jpg" width="1440" height="810" loading="lazy" alt="A close view of the gun&#x27;s wheel and a roller against green grass, enlarged three times so the pixels are visible. The outlines are smooth.">
      <span class="tiers-tag">High</span>
      <p class="tiers-note"><strong>High:</strong> the frame is drawn four times as large and filtered down, so every pixel is the mean of a 2×2 block. Ultra's pixels are the same as these.</p>
    </div>
  </div>
  <figcaption>The gun's wheel in the meadow frame: 480×270 pixels of the 3840×2160 picture, enlarged three times without smoothing. Ultra is left out, since its pixels at an edge are High's, and so is Max, nine samples a pixel instead of four.</figcaption>
</figure>

## The level only a film can use

When we cut the trailer, Antonín watched the aurora chapter's opening flight and saw trees at the back change shape as the lens came in. Every scene draws things simpler as they get farther away, because a distance a player will never see closely is not worth the triangles. The aurora's stand of trees is a ring of spires, and near the lens each spire is swapped for a real spruce: within 200 units on High, and within 300 on Ultra, the one distance rule Ultra stretches. A lens that flies in carries that circle with it, so a tree at the back turns from a cone into a spruce in a single frame.

A game cannot avoid that without paying for it. A film can. Nothing in a film is held to a frame budget, so we added a switch to the game, called `maxdetail`, that makes every distance rule draw its finest from every lens. The aurora's swap radius goes to 1,000 units. Every planted tree wears its near crown. Every broadleaf of a wood is dressed as a near one, every rooftop in the city keeps its clutter at any distance, and every ball is the finest sphere. What it leaves alone is the level's own choices (a film asks for Ultra) and the fades that are part of a scene's look.

<figure class="tiers">
  <div class="tiers-frame">
    <div class="tiers-pane" data-label="High">
      <img src="/img/blog/the-aliens-only-come-out-on-high/wood-high-800.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/wood-high-800.jpg 800w, /img/blog/the-aliens-only-come-out-on-high/wood-high-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The aurora level Fish from behind the cannon: a cluster of coloured balls under the glass, dark spruces in the snow and green aurora curtains. On the slopes behind, the far trees are pale grey cones with no branches.">
      <span class="tiers-tag">High</span>
      <p class="tiers-note"><strong>High:</strong> within 200 units of the lens the stand of trees is a real wood of spruces. The slopes behind the cluster are farther than that, so their trees are plain spires: grey cones with no boughs.</p>
    </div>
    <div class="tiers-pane" data-label="Ultra">
      <img src="/img/blog/the-aliens-only-come-out-on-high/wood-ultra-800.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/wood-ultra-800.jpg 800w, /img/blog/the-aliens-only-come-out-on-high/wood-ultra-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same view, but the trees on the slopes behind are dark, slender spruces with tiers of short boughs.">
      <span class="tiers-tag">Ultra</span>
      <p class="tiers-note"><strong>Ultra:</strong> spruces out to 300 units, so the slopes behind the cluster are wood too, in the far trees' lighter crown. It costs about 0.7 ms a frame here on our desktop at 3840×1600.</p>
    </div>
    <div class="tiers-pane" data-label="Max">
      <img src="/img/blog/the-aliens-only-come-out-on-high/wood-max-800.jpg" srcset="/img/blog/the-aliens-only-come-out-on-high/wood-max-800.jpg 800w, /img/blog/the-aliens-only-come-out-on-high/wood-max-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same view, with every tree on the slopes a full spruce, its boughs dense and snowy.">
      <span class="tiers-tag">Max</span>
      <p class="tiers-note"><strong>Max:</strong> spruces out to 1,000 units, every planted tree in its near crown, so each one on the slopes is a full spruce. A lens that flies in no longer sees a cone turn into a tree.</p>
    </div>
  </div>
  <figcaption>Fish, the aurora chapter's first level, from the play camera, 3840×2160 filtered down. The three levels that draw the far stand differently.</figcaption>
</figure>

On top of that comes the other thing a film can do, which is take as many samples of the frame as it likes. The trailer's frames are 3840×2160, drawn at 7680×4320 and filtered down, and the capture tool takes a switch for more: 3×3 draws the same frame at 11520×6480, nearly 75 million pixels. The Max pictures in this post use both. The trailer itself used Ultra with `maxdetail`.

## What a film pays instead

A game pays for all of this in frame time. At 75 frames a second a frame has 13.3 ms, and every rung above Medium has to fit in it. A film pays in waiting. The Big Top's first 20 seconds, its tour and the demo that follows, filmed at 3840×2160 on Ultra with the HUD on, took the game 29 seconds to write: 1,200 frames. The encode afterwards took about 200 seconds.

For this post we filmed two seconds of the meadow's play view, 120 frames at the same size, on four levels, alternated, three rounds each, on the desktop with the Radeon RX 6900 XT. The whole run took 10.1 to 10.4 s on Medium, 10.8 to 11.0 s on High, 10.7 to 11.1 s on Ultra and 11.7 to 11.8 s on Max. About 7 s of each is the game starting and building the level. So Max's extra samples and its finest distance rules cost about 0.8 s over Ultra for 120 frames, some 7 ms of waiting a frame. High's four times the pixels of Medium cost a little over half a second, far less than four times as long, because the drawing is not the slow part of filming. Writing 120 uncompressed frames of 6 to 10 MB each to the disk is.

Those figures include the writing, so they say how long a film takes, not what the GPU spends on a frame. We have not tried playing at Max, and the menu has no entry for it. For a trailer the lesson was different: once a frame may take as long as it likes, the finest drawing the game knows is a few seconds' wait, and what we had to choose was what to point it at.

## What the pictures do not show

A still shows none of the wind, the motion blur or the way a scene looks while it moves, and a slider cannot show the pop that made us add the fifth level, only the two sides of it. The Max pictures are Ultra plus `maxdetail` plus 3×3 samples, which is more than the trailer used. And the pictures are one frame of one level for each scene, picked because the difference shows there. Other places on the same levels differ less: on the saucer's loop the craft is fifty pixels across and a crew member's head is two.

Nothing here has been judged on a phone or a different GPU.

*Updated on 21 November 2026: each slider now holds only the levels that draw its picture differently, Ultra draws the aurora's trees farther out than High, and the Max pictures are antialiased as they should have been.*
