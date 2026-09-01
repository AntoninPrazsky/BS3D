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
        Stone = 10
    }
}
