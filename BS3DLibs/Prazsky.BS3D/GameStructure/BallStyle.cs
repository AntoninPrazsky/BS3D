using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// What the balls of a map are MADE of (#258). Not what they are — that is <see cref="BallType"/>, the
    /// thirteen colours the lattice, the match rule and the score are all about — but what light does when it
    /// arrives at one, which is the whole of the difference between the entries below and the whole of what
    /// this decides.
    /// <para>
    /// It is a property of the <b>map</b> and not of the player, the machine or the scene: a level is authored
    /// to look a particular way and a hand-built map that wants glass gets glass wherever it is opened, exactly
    /// as it gets the backdrop and the dome it names (<c>Prazsky.BS3D.Levels.Level.Balls</c>). Nothing about the
    /// simulation, the lattice or the rules reads it — a bubble level plays move for move like a vinyl one.
    /// </para>
    /// </summary>
    public enum BallStyle : byte
    {
        /// <summary>
        /// The moulded vinyl beach ball the game has always drawn: five gores of the type colour against white,
        /// a white disc at each pole, welded seams and a fine cast relief that breaks up the highlight. Opaque,
        /// and the value a map that says nothing gets — every level authored before #258 is this one.
        /// </summary>
        Beach = 0,

        /// <summary>
        /// A hollow glass bubble: a soap film with the type colour dyed into it, transparent through the middle,
        /// bright and iridescent along the rim. The look the game's own 3D wordmark is swept in, which is why it
        /// exists at all.
        /// </summary>
        Bubble = 1,

        /// <summary>
        /// Polished marble (#305): a piece of cut stone in the type colour, veined through with that same
        /// colour carried towards white, hard and smooth under a tight polish that picks up the sky.
        /// <para>
        /// The heavy one. Where the vinyl is an air-filled skin and the bubble a film around nothing, this has
        /// mass — which is what it is for, and why it suits the blocks whose backdrops are stone.
        /// </para>
        /// </summary>
        Marble = 2,

        /// <summary>
        /// Wound wool (#311): a ball of yarn dyed the type colour, wrapped by hand in bands that lie at
        /// changing angles, fibrous and matte with a fuzzy halo where the light comes through the loose fibres
        /// at its edge.
        /// <para>
        /// The soft one, and the only one — every other style is a hard surface of some kind. It also takes dye
        /// better than any of them: nothing reflects, transmits or radiates enough to dilute the tint, so it is
        /// the safest style in the set on the thirteen colours.
        /// </para>
        /// </summary>
        Wool = 3,

        /// <summary>
        /// Anodised metal (#306): a turned, brushed alloy in the type colour — gold, copper, brass, oxidised
        /// titanium, gunmetal — mirroring the dome in its own hue.
        /// <para>
        /// Not chrome, deliberately. A white mirror has no colour of its own and thirteen of them are thirteen
        /// identical balls; a metal carries its hue in <i>what it does to the light it reflects</i>, which is
        /// where this style puts the tint.
        /// </para>
        /// </summary>
        Metal = 4,

        /// <summary>
        /// Frosted ice (#307): a ball of cloudy frozen water dyed the type colour, broken through into irregular
        /// plates with bright threads along the fractures between them, cool and pale along its silhouette, and
        /// lit from inside where the sun is behind it.
        /// <para>
        /// Solid, not hollow — frosted ice is not clear ice, and you cannot see a level through a frosted
        /// marble. That keeps it a different <i>material</i> from <see cref="Bubble"/> rather than a recolour of
        /// one, and costs it none of the film's transparency machinery.
        /// </para>
        /// <para>
        /// <b>The fracture has to be irregular, and #337 is why.</b> The net was three straight, evenly spaced
        /// line fields crossing, and that is the construction quilted leather is stitched on — which is what the
        /// owner's playtest called it. It is a Voronoi cell net now, whose plates carry their own value: the
        /// crack is what says ice up close, and the <i>plates</i> are what still say it at play distance, where
        /// a hairline is long sub-pixel. It is also what keeps this style and <see cref="Porcelain"/> apart —
        /// both used to be "a pale ball with a fine crack net", and a craquelure is regular where a fracture
        /// is not.
        /// </para>
        /// </summary>
        Ice = 5,

        /// <summary>
        /// Cut gem (#308): a brilliant-cut stone in the type colour — flat faces each catching the light on
        /// their own, deep absorbed colour in the body, and a bright girdle where a real stone's total internal
        /// reflection piles light along the rim.
        /// <para>
        /// The faces are <b>shaded and never built</b>: the sphere keeps its exact silhouette, because a coarse
        /// enough mesh to show real facets shows a polygonal outline too, and no shading fixes that (#271).
        /// </para>
        /// </summary>
        Gem = 6,

        /// <summary>
        /// Plasma orb (#309): the desktop plasma-ball toy — a dark globe with thin bright arcs of ionised gas
        /// crawling across the inside of it, tinted the type colour, reaching out of a bright core.
        /// <para>
        /// The one style that is <b>alive whether anything is happening or not</b>, and the one a screenshot
        /// says almost nothing about. It belongs on the dark backdrops: its colour lives in thin bright lines,
        /// so a cluster reads dark and a bright dome washes it out.
        /// </para>
        /// </summary>
        Plasma = 7,

        /// <summary>
        /// Molten crust (#310): a cooling lump of lava — a near-black basalt crust broken into plates, with the
        /// molten interior glowing through the seams in the type colour, hottest and whitest at their cores,
        /// breathing on the cluster's own heartbeat.
        /// <para>
        /// Its colour lives entirely in the <b>emission</b>, which is the cleanest colour separation the game
        /// can have — nothing dilutes an emissive seam. The crust is the same darkness on all thirteen, so it
        /// leans on the seams harder than any style but the plasma; since #315 how brightly a seam burns
        /// follows the type's own luminance, which is what keeps orange and brown two balls rather than one.
        /// </para>
        /// <para>
        /// Since #338 the crust for about a plate-width either side of a seam <b>glows too</b> — thin rock over
        /// a hot gap — which is how the style answers "too much of the ball is black" without lightening the
        /// crust: a halo is still emission, so the colour is still undiluted, and it grades out instead of
        /// flooding, so the plates stay plates. Measured on a Thirteen_Colors capture, the share of a ball's disc
        /// carrying real chroma went from 49 % to 58 %, at unchanged mean luminance.
        /// </para>
        /// </summary>
        Lava = 8,

        /// <summary>
        /// Crackled porcelain (#312): a deep, wet-looking coloured glaze over a <b>white clay body</b>, crazed
        /// all over with the fine hairline network an old glaze develops, and banded round its equator with a
        /// meander in white enamel. Hard, cool and expensive-looking.
        /// <para>
        /// <b>The decoration is what names it, and #339 is why.</b> Until then the body was the tint itself —
        /// a coloured sphere under a tight highlight, which is painted plastic, and the owner could not say
        /// what the material was because the shader was not drawing one. China is known by its ornament, so it
        /// has one: a <b>Hilbert curve</b>, which is the pattern of this kind that can be evaluated rather than
        /// stored, and which tiles — the standard curve enters and leaves its square at the two corners of one
        /// edge, so tiles round the equator weld into a single continuous border. Under it the clay shows
        /// through where the glaze thins, a craze line is the glaze parted down to that clay and stained, and
        /// the body passes light the way a held-up teacup does.
        /// </para>
        /// <para>
        /// The pattern is the tell — a crackle net reads as ceramic and as nothing else. What makes it
        /// porcelain rather than a shiny ball is <b>glaze depth</b>: the colour sits slightly under the
        /// surface, because a tight bright lobe for the glaze's own face is laid over a body that is already
        /// shaded.
        /// </para>
        /// </summary>
        Porcelain = 9
    }

    /// <summary>
    /// Reading a <see cref="BallStyle"/> off a level file or a command line. Lenient in the same way and for the
    /// same reason <c>SceneRenderer.TryParseScene</c> and <c>ProceduralMusic.ThemeFor</c> are: a look is not
    /// worth throwing over, so an unknown spelling comes back false and the caller keeps its default rather
    /// than a level failing to open because somebody typed "bubbles".
    /// </summary>
    public static class BallStyles
    {
        /// <summary>
        /// The spellings a style answers to. <c>glass</c> and <c>bubbles</c> are there because they are what a
        /// hand-editing author reaches for first; <c>vinyl</c> names the beach ball by its material, which is
        /// how the rendering notes have always talked about it.
        /// </summary>
        public static bool TryParse(string name, out BallStyle style)
        {
            style = BallStyle.Beach;

            if (string.IsNullOrWhiteSpace(name)) return false;

            switch (name.Trim().ToLowerInvariant())
            {
                case "beach":
                case "vinyl":
                    style = BallStyle.Beach;
                    return true;

                case "bubble":
                case "bubbles":
                case "glass":
                    style = BallStyle.Bubble;
                    return true;

                case "marble":
                case "stone":
                    style = BallStyle.Marble;
                    return true;

                case "wool":
                case "yarn":
                    style = BallStyle.Wool;
                    return true;

                case "metal":
                case "anodised":
                case "chrome":
                    style = BallStyle.Metal;
                    return true;

                case "ice":
                case "frost":
                case "frosted":
                    style = BallStyle.Ice;
                    return true;

                case "gem":
                case "crystal":
                case "diamond":
                    style = BallStyle.Gem;
                    return true;

                case "plasma":
                case "orb":
                    style = BallStyle.Plasma;
                    return true;

                case "lava":
                case "molten":
                case "magma":
                    style = BallStyle.Lava;
                    return true;

                case "porcelain":
                case "ceramic":
                case "china":
                    style = BallStyle.Porcelain;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// The spelling a style is written back out as — the enum's own name, lowercased, which is one of the
        /// keys <see cref="TryParse"/> takes. It is what keeps a level saved by the map editor loadable by the
        /// map editor without either side carrying a table of names.
        /// </summary>
        public static string ToName(BallStyle style) => style.ToString().ToLowerInvariant();

        /// <summary>
        /// Whether light gets THROUGH a ball of this style, and so whether it has to be drawn as a shell —
        /// two passes with opposite cull modes and the right depth states between them, which is
        /// <c>BallRenderSet.Draw</c>'s doing and not the shader's.
        /// <para>
        /// It exists so that method asks what it means (#304). It asked <c>_style == BallStyle.Bubble</c>,
        /// which was the same answer by accident while the bubble was the only transparent style: the test is
        /// about a PROPERTY of the material, and a second transparent style added under the old test would
        /// have been drawn as a single opaque wall — a bubble missing everything that says it is hollow, and
        /// no error anywhere.
        /// </para>
        /// <para>
        /// Stated here beside the enum rather than on the render set because it is a fact about the material,
        /// which is what this file is: the level format, the map editor and the render set all have to agree
        /// about it, and only one of them draws anything.
        /// </para>
        /// </summary>
        public static bool IsTransparent(BallStyle style) => style switch
        {
            BallStyle.Bubble => true,
            _ => false
        };

        /// <summary>The next style in the enum, wrapping — what a cycling key in an authoring tool wants.</summary>
        public static BallStyle Next(BallStyle style)
        {
            BallStyle[] all = Enum.GetValues<BallStyle>();

            return all[(Array.IndexOf(all, style) + 1) % all.Length];
        }
    }
}
