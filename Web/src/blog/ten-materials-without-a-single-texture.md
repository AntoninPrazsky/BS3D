---
title: Ten materials without a single texture
date: 2026-11-24T14:00:00Z
topics: [Rendering, Shaders]
summary: "The balls in BS3D come in ten materials, from soap film to lava, and not one of them is a picture: each is worked out pixel by pixel from where the pixel lies on the ball."
thumb: /img/blog/thumbs/ten-materials-without-a-single-texture.jpg
---

<figure class="tiers" data-slider-label="Material">
  <div class="tiers-frame">
    <div class="tiers-pane" data-label="Bubble">
      <img src="/img/blog/ten-materials-without-a-single-texture/bubble-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/bubble-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/bubble-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="A close crowd of hanging balls made of thin, glossy soap film in yellow, brown, olive, beige, blue, cyan and green. Each colour is printed with its own dots or rings, and the blue sky shows through the pale ones.">
      <span class="tiers-tag">Bubble</span>
      <p class="tiers-note"><strong>Bubble</strong>, the Meadow's: a dyed soap film whose rainbow is worked out from the film's thickness, draining downwards, a mirror at its rim. The one you can see through.</p>
    </div>
    <div class="tiers-pane" data-label="Wool">
      <img src="/img/blog/ten-materials-without-a-single-texture/wool-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/wool-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/wool-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as matt balls of yarn, the strands winding round each one. There is almost no shine, and each colour carries a knitted-in figure: triangles on the olive, crosses on the cyan, checks on the beige.">
      <span class="tiers-tag">Wool</span>
      <p class="tiers-note"><strong>Wool</strong>, the Gallery's and the Silhouettes': yarn wound round three axes, a fuzz in its own colour at the edge and almost no shine. The only soft one.</p>
    </div>
    <div class="tiers-pane" data-label="Marble">
      <img src="/img/blog/ten-materials-without-a-single-texture/marble-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/marble-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/marble-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as polished stone with one small bright highlight each, faint pale veins in the stone and bands of a darker shade round every ball.">
      <span class="tiers-tag">Marble</span>
      <p class="tiers-note"><strong>Marble</strong>, the Coil's and the Reveal's: polished stone with veins of a second, paler mineral, each with a dark margin, warped so that they never repeat.</p>
    </div>
    <div class="tiers-pane" data-label="Ice">
      <img src="/img/blog/ten-materials-without-a-single-texture/ice-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/ice-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/ice-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as frosted ice, each one covered by a net of pale cracks between plates of slightly different shades, with darker bands frozen inside.">
      <span class="tiers-tag">Ice</span>
      <p class="tiers-note"><strong>Ice</strong>, the Tower's and the Grid's: frosted, not clear. A net of cracks between plates, a grain of frost, and a glow in its own colour when the sun is behind it.</p>
    </div>
    <div class="tiers-pane" data-label="Metal">
      <img src="/img/blog/ten-materials-without-a-single-texture/metal-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/metal-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/metal-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as coloured metal with fine brushed lines running round each one, the sky and the sun streaked along the lines, and a dark emblem stamped on each colour: triangles, hexagons, clubs, crosses.">
      <span class="tiers-tag">Metal</span>
      <p class="tiers-note"><strong>Metal</strong>, the Quarry's: its colour is how much it reflects, not a paint, so it mirrors the sky and the sun, and fine brushing runs round it.</p>
    </div>
    <div class="tiers-pane" data-label="Plasma">
      <img src="/img/blog/ten-materials-without-a-single-texture/plasma-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/plasma-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/plasma-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as dark glass globes, each with thin glowing filaments curling across it in its colour: orange, yellow-green, white and cyan.">
      <span class="tiers-tag">Plasma</span>
      <p class="tiers-note"><strong>Plasma</strong>, the Nebula's: the plasma-ball toy, a dark globe with glowing filaments that crawl. Its light is all its own.</p>
    </div>
    <div class="tiers-pane" data-label="Vinyl">
      <img src="/img/blog/ten-materials-without-a-single-texture/vinyl-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/vinyl-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/vinyl-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as glossy inflated vinyl, each colour printed with a big dark emblem like a playing card's: hexagons on the blue, crosses on the cyan, flowers on the beige, clubs on the green, triangles on the olive.">
      <span class="tiers-tag">Vinyl</span>
      <p class="tiers-note"><strong>Vinyl</strong>, the Big Top's, and what a level that names nothing draws: moulded, with its welds and its gloss. With the colour figures off it is a striped beach ball (below).</p>
    </div>
    <div class="tiers-pane" data-label="Lava">
      <img src="/img/blog/ten-materials-without-a-single-texture/lava-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/lava-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/lava-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as black rock split by glowing seams in each ball's colour, orange, yellow-green, blue, white and cyan, the rock near each seam glowing too.">
      <span class="tiers-tag">Lava</span>
      <p class="tiers-note"><strong>Lava</strong>, the Eruption's: black basalt plates with molten seams that breathe with the cluster's heartbeat, and heat glowing in the crust beside them.</p>
    </div>
    <div class="tiers-pane" data-label="Porcelain">
      <img src="/img/blog/ten-materials-without-a-single-texture/porcelain-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/porcelain-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/porcelain-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as glazed china with a soft sheen and one bright highlight each, painted with figures in a darker shade: triangles, crosses, dots, checks and a star.">
      <span class="tiers-tag">Porcelain</span>
      <p class="tiers-note"><strong>Porcelain</strong>, the Spectrum's and the Mirage's: a coloured glaze over white clay, which shows through where the coat is thin, and fine crazing close up.</p>
    </div>
    <div class="tiers-pane" data-label="Gem">
      <img src="/img/blog/ten-materials-without-a-single-texture/gem-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/gem-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/gem-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same balls as dark, deeply coloured cut stones, faint facet edges across each and many small sparkles of light, with darker bands of agate inside.">
      <span class="tiers-tag">Gem</span>
      <p class="tiers-note"><strong>Gem</strong>, the Arcade's: a cut stone whose facets are shaded, never built, so its outline stays round.</p>
    </div>
  </div>
  <figcaption>One cluster in all ten materials, in the order a player meets them: the map of Orrery, a Nebula level, hung in the meadow and filmed from the same place at the same moment at Ultra. Drag the slider, press a name or click the picture.</figcaption>
</figure>

Every chapter of BS3D hangs its cluster in a material of its own, from soap bubbles to cut gems. The balls play the same in all ten; only the look changes.

## Arithmetic, not pictures

None of the ten is a picture wrapped round a sphere. Each is a small program that works out every pixel's colour from where it lies on the ball, in the ball's own space, so the pattern turns as the ball rolls.

Most of it is sine waves at different rates and angles, bent until they stop looking regular. The ice draws its cracks from cells, like dried mud; the gem's facets are a height stepping from face to face. One wave, trimmed:

```hlsl
// One wave of a ball's relief.
// p: the pixel's place on the
// ball, in the ball's own space;
// footprint: the pixel's size
// against the ball's radius.
float ReliefOctave(float3 p,
  float3 dir, float f,
  float footprint)
{
  float w = sin(dot(p, dir) * f);
  // fade it as the pixel grows;
  // gone at half a wave
  return w * saturate(
    1 - footprint * f / PI);
}
```

Summed, the waves make a height whose slope tilts the surface to catch the light. The fade keeps a far ball from glittering: a wave shorter than two pixels is not drawn.

<figure>
<video class="clip" width="800" height="450" muted loop playsinline controls preload="none" poster="/video/blog/ten-materials-without-a-single-texture/plasma-poster.jpg" aria-label="Plasma balls close up: dark globes in orange, olive, beige, blue and green, with thin glowing filaments that slowly curl and slide across each one while the balls hang still.">
<source src="/video/blog/ten-materials-without-a-single-texture/plasma.webm" type="video/webm">
<source src="/video/blog/ten-materials-without-a-single-texture/plasma.mp4" type="video/mp4">
</video>
<figcaption>The plasma is the one material that moves by itself: its filaments crawl whether anything happens or not. It makes all its own light, and the scene's lights never touch it. A crop of the same cluster, 3.6 seconds that loop.</figcaption>
</figure>

## Thirteen colours that stay apart

Thirteen colours must stay apart in every material, and in some they come close: in wool, black, brown and silver. So in nine of the ten, each colour also carries a figure, from a set chosen for the material: emblems on the vinyl, spots on the bubble.

The figure is a darker or paler shade of the ball's colour and the rest shifts the other way, so a ball too far off to show its figure still shows its colour.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Figures off">
      <img src="/img/blog/ten-materials-without-a-single-texture/vinyl-plain-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/vinyl-plain-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/vinyl-plain-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The vinyl balls with no figures: beach balls, each striped in its own colour and white.">
      <span class="compare-tag">Figures off</span>
    </div>
    <div class="compare-pane" data-label="Figures on">
      <img src="/img/blog/ten-materials-without-a-single-texture/vinyl-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/vinyl-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/vinyl-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1600" height="900" loading="lazy" alt="The same vinyl balls with their figures: the stripes are gone, and each colour carries its own dark emblem, hexagons, crosses, flowers, clubs, triangles, stars.">
      <span class="compare-tag">Figures on</span>
    </div>
  </div>
  <figcaption>The vinyl with the colour figures off and on. Off, it is a beach ball. On, each colour's emblem takes the place of the white stripes, like a deck of cards.</figcaption>
</figure>

## What each one costs

Antonín asked what each material costs, so we timed all ten on our RX 6900 XT, in Girandole's 1204 balls at Ultra: 8.50 ms a frame with the vinyl, and six others within our noise of 0.3 ms.

The bubble added 1.82 ms: it shows its far side through its near one, so each ball is drawn twice. The ice added 1.28 ms for its cracks, and the plasma saved 0.90 ms, since it never runs the lighting.

We cut nothing. The bubble is the first chapter's, and its biggest level, Amphora, plays at 9.02 ms a frame, well inside the 13.33 ms of 75 frames a second. Weaker graphics chips we have not timed yet.

## Up close and far away

Antonín also saw some materials change with the distance. The tilt is divided by the pixel's area on the ball, with a floor under that area so that a pixel seen edge-on never divides by nothing. That floor was the landscapes', sized for hills: on any ball over about 25 pixels in radius it flattened the relief, to a seventh on the menu's close pass. The gem's facets and the wool's strands showed mostly from afar.

The balls' floor is now half the pixel's own area, and the relief is whole at any size. Brushing too fine for a pixel now streaks the metal's reflection instead, as on a brushed ball seen across a room.

<figure class="compare">
  <div class="compare-frame">
    <div class="compare-pane" data-label="Before">
      <img src="/img/blog/ten-materials-without-a-single-texture/metal-round-before-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/metal-round-before-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/metal-round-before-1056.jpg 1056w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1056" height="594" loading="lazy" alt="Looking along the cannon's barrel at a cluster under a black, starry sky. In the glass bore sits a green metal ball as smooth as a mirror, with one soft highlight.">
      <span class="compare-tag">Before</span>
    </div>
    <div class="compare-pane" data-label="After">
      <img src="/img/blog/ten-materials-without-a-single-texture/metal-round-after-800.jpg" srcset="/img/blog/ten-materials-without-a-single-texture/metal-round-after-800.jpg 800w, /img/blog/ten-materials-without-a-single-texture/metal-round-after-1056.jpg 1056w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="1056" height="594" loading="lazy" alt="The same view after the fix: the green ball in the bore and the red one behind it are covered in fine brushed lines, and their highlights are streaked along them.">
      <span class="compare-tag">After</span>
    </div>
  </div>
  <figcaption>A metal ball in the gun under precise aim, the biggest a ball is ever drawn, on Crib in the Quarry, before the fix and after. Before, its brushing kept a few per cent of its depth there.</figcaption>
</figure>

The fix comes with the next release.
