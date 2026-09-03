namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which of <c>InstancedModel.fx</c>'s <b>ball techniques</b> shades the patterned mesh parts of the next
    /// draw — what the surface does with the light that arrives at it, and nothing else.
    /// <para>
    /// This is a <b>rendering</b> concept and it lives here rather than beside the game's own
    /// <c>Prazsky.BS3D.GameStructure.BallStyle</c> because of the layering: the styles are what a <i>map</i>
    /// names and are read off a level file, while these are the programs the shader was compiled with. They
    /// correspond one for one today and there is no reason they must — a style could be given a technique the
    /// game already had, and this library cannot see the enum that would say so.
    /// </para>
    /// <para>
    /// <b>It replaced a <c>bool</c> (#304), and that is the whole reason it exists.</b> Two shadings behind
    /// <c>GlassBubble</c> was one flag; the eight styles split out of #272 behind eight flags would be eight
    /// ways to ask for two shadings at once. A renderer is in exactly one of these at a time, which is the fact
    /// an enum states and a set of bools cannot.
    /// </para>
    /// </summary>
    public enum BallShading
    {
        /// <summary>
        /// The moulded vinyl skin: gores, polar discs, welded seams and cast relief, drawn by
        /// <c>InstancedModelPattern</c>. The shading a renderer that has said nothing gets, and the one every
        /// ball in this game had before #258.
        /// </summary>
        Vinyl = 0,

        /// <summary>
        /// The hollow glass bubble (#258): a dyed soap film, transparent through the middle and iridescent
        /// along the rim, drawn by <c>InstancedModelBubble</c>.
        /// <para>
        /// <b>Selecting it is not enough to draw one.</b> A film is transparent, so the shell has to be put out
        /// as two passes with opposite cull modes and the right depth states between them — an order this
        /// property cannot express, because it says how a pixel is shaded and not in what order the pixels
        /// arrive. <c>Prazsky.BS3D.BallRenderSet.Draw</c> is the one caller that does both, and it asks
        /// <c>BallStyles.IsTransparent</c> rather than naming this member, so a second transparent shading gets
        /// the same treatment without that method being edited again.
        /// </para>
        /// </summary>
        Bubble = 1,

        /// <summary>
        /// Polished marble (#305): the type colour as the body of a piece of cut stone, veined through with
        /// that same colour carried towards white, under a hard tight polish and a raised reflection of the
        /// sky. Drawn by <c>InstancedModelMarble</c>.
        /// <para>
        /// Opaque, and it carries no relief at all — polished stone is smooth, and the moulding, the welds and
        /// their self-shadow are the vinyl skin's signature. It is the one shading here that is <i>cheaper</i>
        /// than the vinyl it stands beside.
        /// </para>
        /// </summary>
        Marble = 2,

        /// <summary>
        /// Wound wool (#311): a ball of yarn in the type colour, wrapped in bands that lie at changing angles,
        /// fibrous and soft with almost no highlight and a fuzzy halo at the silhouette. Drawn by
        /// <c>InstancedModelWool</c>.
        /// <para>
        /// The only <b>soft</b> one. Its figure is a normal perturbation rather than a colour change, so unlike
        /// the marble's veining it reads at every tint by construction — a strand <i>is</i> shading, and cannot
        /// be swamped by it.
        /// </para>
        /// </summary>
        Wool = 3,

        /// <summary>
        /// Anodised metal (#306): the type colour as a metal's <i>reflectance</i> rather than as a body colour,
        /// so thirteen alloys mirror the same dome in thirteen colours. Drawn by <c>InstancedModelMetal</c>.
        /// <para>
        /// It has <b>no diffuse term at all</b> — every photon leaving it bounced off it — and a brushed grain
        /// in object space, which is not a detail but the whole rotation cue: a perfect mirror sphere spinning
        /// looks identical frame to frame.
        /// </para>
        /// </summary>
        Metal = 4,

        /// <summary>
        /// Frosted ice (#307): a cloudy frozen solid in the type colour, cracked through with bright internal
        /// threads and cool along the silhouette. Drawn by <c>InstancedModelIce</c>.
        /// <para>
        /// <b>Opaque</b>, and deliberately so — frosting <i>is</i> short-range subsurface scattering, which is
        /// an opaque phenomenon, so this needs none of <see cref="Bubble"/>'s two-pass shell. It is a solid you
        /// cannot see the level through, where the film is a hollow thing you can.
        /// </para>
        /// </summary>
        Ice = 5,

        /// <summary>
        /// Cut gem (#308): a brilliant-cut stone in the type colour, with flat faces that catch the light
        /// separately and a bright girdle at the rim. Drawn by <c>InstancedModelGem</c>.
        /// <para>
        /// <b>The facets are shaded, never built</b> — #271's ruling, since no shading fixes a faceted
        /// silhouette. The mesh is untouched and the faceting is a height field, which is also the only way it
        /// <i>can</i> be written: a pixel shader here cannot rotate a vector from object space into world
        /// space, but a scalar handed to <c>PerturbNormalFromHeight</c> gets that mapping for free.
        /// </para>
        /// </summary>
        Gem = 6,

        /// <summary>
        /// Plasma orb (#309): a dark, nearly empty globe with thin bright filaments of ionised gas crawling
        /// across the inside of it in the type colour. Drawn by <c>InstancedModelPlasma</c>.
        /// <para>
        /// The only shading here whose read is <b>motion</b> — every other is a still material seen under a
        /// moving camera. It is also the dearest: a domain warp is two noise evaluations where the others have
        /// one, animated, so nothing about it is cacheable.
        /// </para>
        /// </summary>
        Plasma = 7,

        /// <summary>
        /// Molten crust (#310): a near-black basalt shell cracked into plates, with the molten interior glowing
        /// through the seams in the type colour and breathing on the heartbeat. Drawn by
        /// <c>InstancedModelLava</c>.
        /// <para>
        /// The inverse construction of <see cref="Plasma"/> — that is thin bright lines over an empty dark
        /// shell that writhe, this is a solid heavy crust whose seams breathe. One is electricity, the other
        /// heat. It is also the one new shading that <i>keeps</i> the vinyl's relief machinery, because plates
        /// have to read as broken stone.
        /// </para>
        /// </summary>
        Lava = 8,

        /// <summary>
        /// Crackled porcelain (#312): a deep coloured glaze over a ceramic body, crazed all over with the fine
        /// hairline network an old glaze develops. Drawn by <c>InstancedModelPorcelain</c>.
        /// <para>
        /// Its crack tone <b>inverts</b> on the glaze's own luminance — a bright glaze is crazed with darker
        /// lines and a dark one with lighter ones — which is how the darkest types keep a visible net without a
        /// table of per-type constants.
        /// </para>
        /// </summary>
        Porcelain = 9,

        /// <summary>
        /// Rough granite (#324): a speckled, unpolished stone — a grey body under a coarse lumpy surface, shot
        /// through with the light and dark mineral grains a cut granite shows. Drawn by
        /// <c>InstancedModelStone</c>.
        /// <para>
        /// <b>It is the one shading here that has no type colour at all</b>, and that is what it is for. Every
        /// other member takes the ball's tint and does something with it, because every other member draws one
        /// of the thirteen colours the player matches. This one draws the ball that <i>cannot</i> be matched
        /// (<c>Prazsky.BS3D.GameStructure.BallKind.Rock</c>), and a rock wearing a colour would be a lie the
        /// player acts on. So the stone's own grey is a constant of the shading and the per-draw tint is
        /// ignored.
        /// </para>
        /// <para>
        /// It is also the one shading a <b>map cannot name</b>: it is not in <c>BallStyle</c>, because the
        /// style is what a level file says its balls are made of and this is what a single ball IS. A rock is
        /// drawn as stone on a bubble level and on a lava level alike — the kind opts out of the style, which
        /// is the only way "that one is different" survives ten materials.
        /// </para>
        /// </summary>
        Stone = 10,

        /// <summary>
        /// Clear glass (#325): a hollow shell with <b>no dye in it at all</b> — a rim, a highlight, a cast seam
        /// and as close to nothing as possible in between. Drawn by <c>InstancedModelHollow</c>.
        /// <para>
        /// It is <see cref="Stone"/>'s argument at the opposite end of the palette. Both are shadings a map
        /// cannot name and both belong to a <c>BallKind</c> rather than to a <c>BallStyle</c>; the stone draws
        /// the ball that can never be matched, and this draws the ball that has <i>no colour yet</i> — the
        /// transparent kind, before a shot gives it one. Every other member of this enum takes the per-draw
        /// tint and does something with it; these two ignore it, because a colour on either would be a lie the
        /// player acts on.
        /// </para>
        /// <para>
        /// <b>Transparent, so selecting it is not enough to draw one</b> — the same caveat <see cref="Bubble"/>
        /// carries, and the same two-pass shell answers it (<c>BallRenderSet.DrawHollow</c>). Where it differs
        /// from the film is what it is FOR: a bubble level dyes every ball, so on one of those this is the one
        /// undyed shell in the frame, which is exactly the collision #325 had to solve and the reason the kind
        /// opts out of the style rather than borrowing it.
        /// </para>
        /// </summary>
        Hollow = 11,

        /// <summary>
        /// A live bomb (#326): a dark ridged casing with a hot charge burning inside it, breathing hard.
        /// Drawn by <c>InstancedModelBomb</c>.
        /// <para>
        /// The third shading that belongs to a <c>BallKind</c> rather than to a <c>BallStyle</c>, and the
        /// third with <b>no type colour</b> — a bomb is not a colour the player matches, so a bomb wearing one
        /// of the thirteen would be a lie they act on, which is the rule <see cref="Stone"/> states first.
        /// </para>
        /// <para>
        /// <b>It has to read as ARMED before it is hit, on all ten materials</b>, and it does — the
        /// ten-material sweep puts it clearly apart from every style, the lava's glowing crust included (that
        /// one glows in a web of cracks, this in straight latitude bands). It did <i>not</i> at play distance
        /// until the owner looked at it and named why: the glowing lines were too narrow to see, so the ball
        /// read as a black sphere and its blinking read as nothing at all. The original argument here — the
        /// stone's finding turned round, a rock being named across a field by being the one ball that does
        /// <b>not</b> breathe — was right about the channel and silent about the one thing that decides
        /// whether the channel carries: how big the lit figure is. The fixes and their measurements are in
        /// the technique's own header in <c>InstancedModel.fx</c>.
        /// </para>
        /// </summary>
        Bomb = 12,

        /// <summary>
        /// A live zap (#327): a dark shell with a cage of electric arcs crawling over it and jumping between
        /// its poles. Drawn by <c>InstancedModelZap</c>.
        /// <para>
        /// The fourth shading that belongs to a <c>BallKind</c> rather than to a <c>BallStyle</c>, and the
        /// fourth with <b>no type colour</b>, on <see cref="Bomb"/>'s argument exactly.
        /// </para>
        /// <para>
        /// <b>⚠ Its collision to solve is <see cref="Plasma"/>, not the lava</b>, and it is a sharper one than
        /// the bomb had: that style is already the game's crawling-filament look, so "glowing squiggles" was
        /// taken before this kind existed. What separates them is stated in the technique's header and is
        /// structural rather than tonal — the plasma ball glows <i>all over</i>, in its own type colour, with
        /// filaments drifting through a lit body; a zap is a <b>dark</b> shell in one fixed cold-white blue,
        /// and what moves on it is a small number of hard, thin arcs that snap between fixed poles rather
        /// than a field that drifts. The bomb's own lesson applies on top: what a special is read by at play
        /// distance is the SIZE of the lit figure, not the amount of light in it.
        /// </para>
        /// </summary>
        Zap = 13
    }
}
