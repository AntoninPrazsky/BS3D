---
title: Five scenes that started as AI pictures
date: 2026-10-25T12:00:00Z
topics: [Scenery, AI pictures, Rendering]
summary: "Before we change how a scene looks, we ask a local image model what it should look like. Five scenes, each with the picture it was built from and the game before and after."
thumb: /img/blog/thumbs/five-scenes-that-started-as-ai-pictures.jpg
---

Before we change how a scene looks, we ask for a picture of what it should look like. Antonín runs two small image models on the graphics card of his own computer, Z-Image-Turbo and FLUX.2 klein, through a program called stable-diffusion.cpp. We type a description, such as "a small grove of umbrella thorn acacia trees in dry golden grass", and about half a minute later a picture comes back. The ones worth keeping go next to a capture of the game, and we write down what the picture has that the game does not.

None of these pictures is in the game. The game draws everything itself, from triangles and shaders. The pictures are only what it was built to look like. They are not honest witnesses either: a model draws a prettier world than any game can, and gets some details wrong. What they are good at is showing us ordinary things we had stopped seeing, such as how dark an acacia's leaves really are.

Here are five changes, all made after version 0.4.0. Under each pair of game pictures there is a Before and an After button. Press one, or click the picture, to switch between the two in the same place.

## Acacia leaves

The savanna's acacias had leaves like fern fronds: eleven broad leaflets down each side of a card about a metre long, in a bright yellow-green. FLUX.2 klein's grove looks nothing like that. Each crown is a dark, fine, spiky mat. Every leaf is made of smaller leaves, and those of leaflets a few millimetres long, so from a distance the whole crown reads as one soft layer.

<figure>
  <img src="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-reference.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: three flat-topped acacia trees with dark, fine-leaved crowns stand in dry golden grass in evening light.">
  <figcaption>Drawn by FLUX.2 klein from "a small grove of three flat-topped umbrella thorn acacia trees ten to twenty metres away in dry golden grass". It is not in the game.</figcaption>
</figure>

We measured the difference. Taking the green pixels from the upper half of each picture, the trees in the pictures were about three times darker than ours, and green where ours were yellow. Now every leaf card is a twig with ten pairs of thin side branches, each carrying a row of tiny leaflets, and the canopy colours are darker and greener.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-tree-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-tree-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/acacia-tree-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="Looking up at a savanna acacia from below. Its crown is made of broad olive-yellow leaflets in big flat fronds, with blue sky between them.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-tree-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-tree-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/acacia-tree-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same acacia from the same place. The crown is a darker blue-green and made of narrow, finely cut leaves.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>A savanna acacia seen from below, before and after.</figcaption>
</figure>

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-leaves-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-leaves-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/acacia-leaves-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="Close up under the branches of an acacia: large olive-yellow leaves, each a row of broad leaflets, against blue sky.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-leaves-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/acacia-leaves-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/acacia-leaves-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same branches close up. The leaves are narrow and dark green, with rows of fine leaflets that overlap into a feathery mat.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>The same leaves close up. At the play camera the change costs between 0.08 and 0.15 of a millisecond a frame.</figcaption>
</figure>

What is still wrong: the boughs fan out from their tips like a starburst, where the trees in the picture keep branching into finer twigs under the leaves. The baobab's leaves are still fronds too.

## A fire with depth

A savanna fire used to be three flat pictures, each with one tongue of flame drawn on it, always turned to face the camera. Close up, it looked like a drawing of a fire.

We asked both models for the inside of a flame, as a close-up photograph. Z-Image-Turbo drew sheets and threads of see-through orange gas folding over each other, a white-yellow core, and edges that fade into the dark. FLUX.2 klein's answer to the same words was a glowing ring, so Z-Image's is the picture we used.

<figure>
  <img src="/img/blog/five-scenes-that-started-as-ai-pictures/fire-reference.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: a close-up of a flame, layered translucent sheets of orange gas folding over each other around a bright core, on a black background.">
  <figcaption>Drawn by Z-Image-Turbo from "a macro photograph of the inside of a flame: translucent undulating plasma, layered sheets and filaments of glowing orange and red gas". It is not in the game.</figcaption>
</figure>

On High and Ultra a fire is now a small volume of glowing gas. For each pixel that looks through the fire's box, the game takes a dozen steps along the line of sight into a three-dimensional pattern of tongues and turbulence. The hot middle shines through the cooler gas around it, and as the camera moves, different tongues come forward and glow through one another, which a flat picture cannot do. Low and Medium keep the flat flame.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/fire-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/fire-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/fire-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="A savanna fire seen from the ground. The flame is a flat orange shape with sharp-edged tongues and curly lines drawn on it.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/fire-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/fire-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/fire-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same fire. It is a soft, glowing orange column, brightest at the base and fading into the sky at its tip.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>The savanna fire on High, before and after. It costs 0.30 of a millisecond a frame, and 0.38 with all eight fires in view.</figcaption>
</figure>

We have only judged stills. How it flickers, and how it looks in the chapter's opening camera move, is not checked. In a still the volume's edges are soft by nature, and a fine grain shows at the tips.

## Mars like a rover's photograph

Antonín's note on the Mars scene was short: it still does not look like Mars, there are blotches, torn patches and lines. We asked Z-Image-Turbo for a raw photograph from a rover's mast camera. It drew a plain of muted ochre-grey, a few dark angular rocks with sand drifted round them, a pale outcrop far off and a flat butterscotch sky. FLUX.2 klein's Mars was too orange to be a rover's photograph, but it covered the ground in small stones of every size, so we took from it how many rocks there are and how big, and from Z-Image the colour.

<figure>
  <img src="/img/blog/five-scenes-that-started-as-ai-pictures/mars-reference.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: a flat ochre-grey plain with scattered dark angular rocks, a pale outcrop in the distance and a flat butterscotch sky, as if seen by a rover.">
  <figcaption>Drawn by Z-Image-Turbo from "a raw photograph from a robot rover's mast camera on Mars". It is not in the game.</figcaption>
</figure>

Against it our Mars had three faults. The dark ovals were the worst. They were the ground's stones: low dark mounds that the height map gave no outline, so from any distance they read as holes. The stones are now real rocks, standing on the plain in three sizes, 1,800 small, 360 middling and 45 large, and they cast shadows. Close to the camera there is a scatter of gravel that fades out before it gets too small to see. The patches of rust, sand and cracks were all different tones, so now they are one muted tone, and the cracks and ripples are half as strong. And the sand drifts, which stopped dead at every crater wall, now thin out up the wall.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/mars-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/mars-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/mars-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="A flat red-ochre plain with a layered mesa on the horizon. Dark oval blotches lie on the ground like holes.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/mars-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/mars-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/mars-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same plain, paler and calmer. The ovals are gone. Small dark rocks and gravel lie on the sand, with a few larger rocks near the camera.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>Mars from the ground, before and after. From the play camera the change costs 0.13 of a millisecond a frame.</figcaption>
</figure>

The rocks stand only in the clearing round the island, because further out the game has no copy of the craters to stand them on. The Low setting keeps no gravel and no sand.

## Clouds with a body

The sky has always had a flat sheet of cloud, a pattern on a plane high above the island. It has one big advantage: its shadow is the shadow on the ground. But from the island's low view it reads as a pattern on a ceiling. Z-Image-Turbo's field of cumulus is a different thing. Each cloud is a solid heap with a white sunlit top, grey sides and a flat darker base, and the heaps get smaller and closer together towards the horizon.

<figure>
  <img src="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-reference.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: large white cumulus clouds with sunlit tops and flat grey bases over a grassland, deep blue sky between them, the clouds smaller towards the horizon.">
  <figcaption>Drawn by Z-Image-Turbo from "a sky full of large dense fair-weather cumulus clouds over a wide open grassland". It is not in the game.</figcaption>
</figure>

On High and Ultra, every heap of cumulus near the camera is now a body of its own. It has a flat base on the old sheet's plane and a billowing top shaped by three-dimensional noise. It is lit by the sun through the heap itself, by the sky from above and by the sheet's underside colour from below. The old sheet stays behind them, because it still makes the shadows, and the heaps drift with it, so the shadow on the ground is still the cloud overhead. The heaps stand out as far as the eye goes, so the band just over the skyline fills with small ones, as in the picture. Under the meadow's scattered sky they cost 0.54 of a millisecond a frame. Under the broken sky of the mountains, the dearest we measured, they cost 1.08.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-scattered-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-scattered-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/clouds-scattered-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The meadow scene: the cannon on its island, a cluster of balls hanging above it, green hills and a big tree. The teal sky holds a flat, streaky pattern of cloud and one large dark violet patch.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-scattered-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-scattered-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/clouds-scattered-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same meadow view. The sky holds solid white heaps of cumulus, flat underneath and lit from the sun's side, smaller towards the horizon.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>The meadow, before and after.</figcaption>
</figure>

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-broken-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-broken-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/clouds-broken-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same view under a cloudier sky: a thick streaky layer of blue-grey and cream cloud covers most of the sky.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-broken-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/clouds-broken-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/clouds-broken-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="Big white and grey cumulus heaps with bright tops and shaded sides pile up across the sky, with patches of teal between them.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>The meadow again, under a cloudier sky.</figcaption>
</figure>

Only scattered and broken skies get heaps so far. An overcast or stormy sky keeps its flat lid, and so do the Low and Medium settings and the Raspberry Pi version. The tops are also softer than the picture's cauliflower edges.

## Pyramids in the desert

The desert chapter used to be dunes and nothing else, on purpose. We changed our minds, and asked both models for the three Giza pyramids from a couple of kilometres away.

<figure>
  <img src="/img/blog/five-scenes-that-started-as-ai-pictures/pyramids-reference.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: the three Giza pyramids across golden dunes, the nearest lit warm on one face and shaded blue on the other, the far ones paler in haze.">
  <figcaption>Drawn by FLUX.2 klein from "the three Giza pyramids seen from about two kilometres away across rolling golden sand dunes in the late afternoon". It is not in the game.</figcaption>
</figure>

Both pictures agree on three things. The pyramids stand in a staggered line, one behind another. One face of each is lit and the other is in shade. The far ones fade into haze. The game now has Khufu, Khafre and Menkaure, and the small pyramids of the queens, at their real sizes and on their real diagonal, though squeezed closer together so the group fits the dunes. Khufu stands far behind the view the game opens on.

The stone is golden limestone in courses, each course a slightly different tone with a dark line beneath it, fading with distance. Our first blocks were square and looked like floor tiles, so they are now offset like masonry, with some fallen out, and the tip of Khafre keeps its pale casing. The haze is lighter than the dunes' own, because otherwise the pyramids melt into the sky.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/pyramids-before-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/pyramids-before-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/pyramids-before-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The desert scene: the cannon on its island, a cluster of balls above it, and golden dunes to the horizon under a teal sky. Nothing stands on the horizon.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/five-scenes-that-started-as-ai-pictures/pyramids-after-800.jpg" srcset="/img/blog/five-scenes-that-started-as-ai-pictures/pyramids-after-800.jpg 800w, /img/blog/five-scenes-that-started-as-ai-pictures/pyramids-after-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="The same view with two pale gold pyramids standing on the horizon behind the cluster, each with a lighter face and a darker one.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>The desert, before and after. The Raspberry Pi version does not draw the pyramids.</figcaption>
</figure>

## What we cannot show you

Every pair above is the same scene from the same camera, captured in our test program before and after the change. They are stills. How the fire flickers and how the clouds drift is not something a picture can say, and we have not yet judged either in motion. If something looks wrong when you play, the pause menu's "Write to the Author" sends us a line.
