---
title: Logos of studios that never existed
date: 2026-10-28T12:00:00Z
topics: [AI pictures, Measurement]
summary: "Ask a small image model for a game picture and now and then it signs it, with the logo of a studio that was never there. What we found in 1,743 of our own renders, why we think it happens, and the one use where the other model is brilliant: painting over the game's own frame."
thumb: /img/blog/thumbs/logos-of-studios-that-never-existed.jpg
---

Before we change how a scene looks, we ask a small image model for a picture of what it should look like ([Five scenes that started as AI pictures](/blog/five-scenes-that-started-as-ai-pictures/) shows five of them). Antonín runs two models on the graphics card of his own computer, Z-Image-Turbo and FLUX.2 klein, through a program called stable-diffusion.cpp. Over six weeks they drew us 1,743 pictures that have a note beside them saying which model drew each one and from what words. Some of them came back with something nobody asked for.

## A studio in the corner

This is FLUX.2 klein's answer to "game environment concept art: a small campfire seen from far away ... a purple and orange dusk sky, wide shot". The fire is what we asked for. The black band along the bottom is not.

<figure>
  <img src="/img/blog/logos-of-studios-that-never-existed/fire-far-klein.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: a small campfire burning in dry grass on a wide plain at dusk, flat-topped trees on the horizon. Along the bottom runs a black band with white logos at both ends.">
  <figcaption>Drawn by FLUX.2 klein from "Game environment concept art: a small campfire seen from far away, about thirty metres, in the middle of a wide dry grass plain at dusk", the opening of a longer description. It is not in the game.</figcaption>
</figure>

Up close, the band holds a logo, a studio name, a line of small print and a second logo on the right. None of it can be read, and none of it looks like any studio we know.

<figure>
  <img src="/img/blog/logos-of-studios-that-never-existed/fire-far-strip.jpg" width="960" height="66" loading="lazy" alt="The two ends of the black band, enlarged: on the left a white emblem, a word in heavy capitals and a run of illegible small text; on the right two more illegible marks.">
  <figcaption>The two ends of the band, at about one and a half times the size it was drawn.</figcaption>
</figure>

It was not the only one. Here are the corners of six more.

<figure>
  <img src="/img/blog/logos-of-studios-that-never-existed/corners-klein.jpg" width="960" height="554" loading="lazy" alt="Six corners of pictures drawn by an image model, two to a row. A handwritten signature over dry grass; a red emblem beside a word in capitals over a dark forest floor; the number 2025 over ferns; a round white badge with a red mark in the top corner of a glass roof over coloured balls; and two black bands along the bottom of a volcanic and a red Martian ground, each with a leaping white figure beside a made-up name.">
  <figcaption>Corners of six pictures drawn by FLUX.2 klein, at about 1.1 times the size they were drawn. The descriptions opened with "Game environment concept art at eye level: a dry African savanna" (top left), "Video game screenshot at late dusk, realistic 2026 graphics: inside a dense old mixed forest" (top right and middle left, both painted over a frame of our own forest), "Game screenshot concept art, stylised 3D bubble shooter" (middle right) and "Game environment concept art: a round stone arena platform", in a volcanic landscape and on the surface of Mars (bottom row). None of the pictures is in the game.</figcaption>
</figure>

An artist's signature in the grass. A red emblem with a word beside it. A year. A round badge in the top corner, like a phone app's. And twice more the black band along the bottom, each time with a leaping white figure beside a name, and each time a different name.

## Counting them

To find out how often this happens, we asked a third model to look. Qwen3-VL-8B is a small model that reads pictures, and it ran on the same computer. We showed it every one of the 1,743 pictures, and 148 other files from the same folders (captures of the game, the frames we gave the models to paint over, and pictures that have no note), one at a time, and asked it to look for "graphics laid over the picture that are not part of the depicted scene": an interface, text or numbers, a logo, a watermark, a signature, or a black strip with writing in it. It said yes 165 times, 86 of them for the models' pictures. We looked at every one of those 86 by eye, and 34 were marks like the ones above. Most of the rest were things we had asked for: the game's logo, flat white pictograms, the labels on the front of a hi-fi amplifier.

All 34 were drawn by FLUX.2 klein, 34 of its 704 pictures. Z-Image-Turbo drew 974 and none of them carries a mark. Neither do the 65 older pictures whose notes do not name the model.

klein did not mark its pictures at random. Every one of the 34 came from a description with the word "game" in it, and 34 of the 140 such pictures carry a mark, about one in four. Of its 564 pictures without that word, 544 asked for a photograph, and not one of them is marked. Eight of klein's descriptions said "realistic 2026 graphics", and seven of those eight pictures came back marked. Painting over a picture we gave it made the marks more likely, not less: 14 of the 36 pictures painted over one are marked, and 20 of the 104 drawn from words alone.

## A game that is not ours

The same model can do the same thing to a frame of our own game. For the aurora scene we gave both models a clean capture, with no score, no counters and no buttons on it, and asked each to paint over it. The setting for that is called strength: at 0 the model hands our frame back untouched, at 1 it ignores it. We used 0.55, and the words "Video game screenshot at night, realistic 2026 graphics: a snowy boreal forest clearing under green aurora curtains", and more about the snow and the spruces.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Our frame">
      <img src="/img/blog/logos-of-studios-that-never-existed/aurora-our-frame.jpg" width="960" height="657" loading="lazy" alt="A capture of the game: the gun from behind on the stone island at night, a dark cluster of balls hanging above it, snowy spruces around and green aurora curtains in the sky. There is nothing drawn over the picture.">
      <span class="compare-tag">Our frame</span>
    </div>
    <div class="compare-pane" data-label="FLUX.2 klein">
      <img src="/img/blog/logos-of-studios-that-never-existed/aurora-klein.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: a snowy forest clearing under green aurora curtains, with no gun and no island. Small white bars, one red, sit in the bottom left corner, a column of round icons on the left edge and a small counter in the bottom right corner.">
      <span class="compare-tag">FLUX.2 klein</span>
    </div>
  </div>
  <figcaption>The game's own frame, and FLUX.2 klein's picture painted over it at strength 0.55. Neither the gun nor the island survived; the bars, the icons and the counter are new. The picture is not in the game.</figcaption>
</figure>

FLUX.2 klein kept nothing of our frame. It drew a clearing of its own, and in its corners it drew the interface of a game we have never seen: two thin bars at the bottom left, one of them red like a health bar, a column of round icons down the left edge, and a small counter at the bottom right.

Z-Image-Turbo, given the same frame, the same words and the same strength, kept our gun where it stood and the round pit in the middle of the island, and painted snow, spruces and aurora around them. It drew nothing over the picture.

<figure>
  <img src="/img/blog/logos-of-studios-that-never-existed/aurora-zimage.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model over the game's frame: the gun on its two wheels seen from behind, a round pit in the snow in front of it, a dark tangled mass hanging above, snowy spruces on both sides and green and violet aurora curtains in the sky. Nothing is drawn over the picture.">
  <figcaption>Z-Image-Turbo's picture painted over the same frame, at the same strength and with the same words. It is not in the game.</figcaption>
</figure>

## Why, as far as we can tell

We do not know what either model learnt from, so this is a guess. The pictures people put online under words like "game", "concept art" and "screenshot" often carry exactly these things: an artist's signature in the corner of a concept painting, a studio's logo and small print along the bottom of a press screenshot, the score and the health bar of a game being played. If a model has learnt that such words go with such corners, it draws the corners too. It cannot write, so what it draws only looks like a logo and some small print.

That fits the one clear pattern in our counts: klein's marks came when the words said "game", and never when they said "photograph". It also fits the years. Asked for "realistic 2026 graphics", klein put numbers in the corners that read like years, 2025, 2015 and 2001, as if it took 2026 for a date and stamped one on the picture.

Why Z-Image-Turbo never did it, we cannot tell. It may have learnt from pictures with such marks removed, or have learnt to leave them out, and we do not know which, or whether either is true.

## Where the same trick shines

Painting over our own frame is also the most useful thing these models have done for us, when the other model does it. When the Mountains scene needed more detail, we took four frames of the range from the game and asked Z-Image-Turbo to paint over each one at strength 0.5, with "Photograph of sharp alpine summits seen from a nearby ridge: dark fractured rock towers with layered strata, snow lying on every ledge and step", and so on, ending in "no people, no text".

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Our frame">
      <img src="/img/blog/logos-of-studios-that-never-existed/crest-our-frame.jpg" width="960" height="657" loading="lazy" alt="A capture of the game's old mountains: a few steep peaks under a pale sky, their faces smooth and evenly shaded brown, with flat patches of snow and falling snowflakes.">
      <span class="compare-tag">Our frame</span>
    </div>
    <div class="compare-pane" data-label="Z-Image-Turbo">
      <img src="/img/blog/logos-of-studios-that-never-existed/crest-zimage.jpg" width="960" height="657" loading="lazy" alt="The same peaks at the same size and place, painted as rock: layered beds of grey and brown stone run across the faces, with snow lying in the gullies and on the ledges.">
      <span class="compare-tag">Z-Image-Turbo</span>
    </div>
  </div>
  <figcaption>A frame of the old Mountains scene, and Z-Image-Turbo's picture painted over it at strength 0.5. The repainted picture is not in the game.</figcaption>
</figure>

It kept every peak where it was, at the same size and in the same light, and turned our smooth shaded faces into rock, with beds of strata across them and snow on the ledges. That is what made the job concrete. It was the same mountain twice, as we drew it and as rock would look.

FLUX.2 klein, given the same frame at the same strength, drew other mountains.

<figure>
  <img src="/img/blog/logos-of-studios-that-never-existed/crest-klein.jpg" width="960" height="657" loading="lazy" alt="A picture drawn by an image model: two dark rock towers with horizontal bands of strata rising out of deep snow, under a pale sky. They are not the peaks of the game's frame.">
  <figcaption>FLUX.2 klein's picture painted over the same frame, at the same strength and with the same words. It is not in the game.</figcaption>
</figure>

klein's pictures turned out to be useful too, for a different reason. We measured how much detail each picture has at each size: the picture is split into bands of 2, 4, 8, 16 and 32 pixels, the way a stack of ever stronger blurs splits it, and we read the contrast in each band over the rock. In the old range, the finest band, 2 pixels, held a tenth to a fifth of the contrast of the 32-pixel band. klein's rock kept a third to a half. Z-Image's repaint kept much of our own smoothness: its finest bands measured only 1.5 to 2 times the game's. So Z-Image's pictures showed where the rock should be, and klein's showed how fine it should get. The range on High and Ultra is now drawn by a new shader on a mesh twice as fine, and its 2-pixel band keeps 0.5 to 0.95 of the coarse band's contrast, four to seven times what it had.

## What we cannot say

We do not know what FLUX.2 klein or Z-Image-Turbo were trained on, so the explanation above is a guess that fits what we saw, not a finding. Our renders are not a fair sample either. They are the pictures we happened to need, so the two models were not given the same words equally often, and the counts say what happened to our descriptions, not what each model does in general. The words also changed together: klein's game descriptions that said "no text" carry fewer marks, 1 of 30 against 33 of 110, but twelve of those 30 asked for flat icons or an object on a plain background rather than a scene, so we cannot tell whether the words or the subject made the difference.

The marks were found by a third model, and each one it found was checked by eye. Anything it missed is not in the counts. We looked at 32 of klein's pictures it had called clean, picked at random, and found no mark in any of them, which says it did not miss many, not that it missed none. And the logos look like no studio we know, but we cannot prove that none of them belongs to anyone.
