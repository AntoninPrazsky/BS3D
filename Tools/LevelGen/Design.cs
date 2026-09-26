using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;
using System;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// One level as the generator draws it: a silhouette, a colouring, a scene and a set of rules — every design in
    /// <c>Designs/</c> returns one, <see cref="LevelEmitter.Emit"/> writes it and <see cref="CampaignSet.WriteLevelSet"/>
    /// takes its budget into the set. Moved out of <see cref="Program"/> in #597.
    /// </summary>
    internal sealed class Design
    {
        public string File;
        public string Name;
        public byte Grid;
        public byte Depth;

        /// <summary>
        /// How deep the play field is. <see cref="Program.FIELD_LEVELS"/> for every ordinary level — the deepest
        /// field the game hangs at its standard height and frames whole — and larger only for a tall one,
        /// which is framed from its floor up and reaches out of shot.
        /// </summary>
        public byte FieldLevels = Program.FIELD_LEVELS;

        /// <summary>
        /// Which scene the level plays in — the whole of what a level says about its backdrop, the
        /// scenes' parameters being fixed in code (level format 2). A block's five designs all name the
        /// same one; <see cref="CampaignSet.DescribeBlock"/> is what reports it if they do not. Note the default is
        /// <see cref="SceneKind.City"/> rather than "unset", so a design that forgets to name a scene
        /// gets the city — the printout is where that shows.
        /// </summary>
        public SceneKind Scene;
        public byte Sky;

        /// <summary>
        /// <b>How many cells of skin a body keeps; zero, the default, leaves it solid.</b> A design says
        /// what its silhouette is and this says how much of the inside of that silhouette is actually
        /// there — every cell further than this from a free cell is taken out, so the shape, the
        /// anchors and every visible face are untouched and what goes is the part no player ever sees.
        /// <para>
        /// It exists for #398, where the owner's verdict on a whole chapter was the same sentence nine
        /// times over: <i>"this level takes too long to finish, but otherwise isn't much of a
        /// challenge - I start shooting mindlessly just to get it over with. It should be less
        /// dense."</i> The Quarry's five #255 structures had already been rebuilt once for SHAPE - a
        /// lintel on pillars, loads on slings, a hanging wall on a seam - and the measurement is what
        /// said the rebuild had not touched the other axis: they shipped at 229 to 432 balls, three of
        /// the five HEAVIER than the solid masses they stood beside, because a member drawn as a member
        /// is still filled in behind its face.
        /// </para>
        /// <para>
        /// <b>The field's own boundary counts as free</b>, which is the half of the rule that keeps a
        /// level hanging: the top course is against the glass and has nothing above it, so every anchor
        /// is skin by construction and no hollowing can cost a level its grip. The floor and the field
        /// walls answer the same way.
        /// </para>
        /// <para>
        /// <b>⚠ It is not a difficulty lever on its own.</b> How long a level takes to play is its
        /// standing-group count far more than its ball count, and a skin cuts a 2x2x2 tile in half
        /// rather than removing it — so a body hollowed and left otherwise alone plays just as many
        /// shots for smaller payouts, which is the complaint made worse. It is paired with the tile
        /// size (<see cref="Program.Prism"/> recorded that remedy first, doubling a tile to stop its last
        /// storey dragging) and with a re-priced budget every time it is used.
        /// </para>
        /// </summary>
        public int Hollow;

        /// <summary>
        /// Which composition the level plays, written into <c>Level.Music</c> — and it is a property of
        /// the <b>block</b> rather than of the design: every level of a block names the same piece, so
        /// the music changes when the chapter does and not when the level does (#194).
        /// <para>
        /// Left null a level hands the choice to the set's own positional rotation
        /// (<c>GameMusic.SetTheme</c>'s fallback, the level's index over the families on disk), which is what every level did
        /// before this — and which is exactly why the order could not be rearranged without silently
        /// rescoring the campaign. Naming it pins it. An unknown spelling falls back to that same
        /// rotation rather than throwing, so a typo here is a level that quietly plays the wrong piece:
        /// the five names are <c>pulse</c>, <c>bohemia</c>, <c>nocturne</c>, <c>mural</c> (#264's
        /// bass-led groove, which replaced the polka) and <c>ember</c> (#163's rock ballad, which the
        /// Coil took in #207).
        /// </para>
        /// </summary>
        public string Music;

        /// <summary>
        /// What the level's balls are made of, written into <c>Level.Balls</c> (#258) — and a property of
        /// the <b>block</b> for <see cref="Music"/>'s reason: the material changes when the chapter does,
        /// not when the level does. <see cref="CampaignSet.DescribeBlock"/> reports it and says so when a block's ten
        /// disagree.
        /// <para>
        /// <b>Every block states one now</b>, and each states a different one — see the table of
        /// <c>BALLS_*</c> constants in <see cref="CampaignSet"/> for which chapter hangs what and why. It was
        /// the opening block alone until #272's eight styles landed, and the campaign is where they are
        /// spent: nine chapters, nine materials.
        /// </para>
        /// <para>
        /// Left null the level says nothing about its balls and every consumer draws the moulded vinyl
        /// beach ball — which no shipped level does any more, though it is still what the map editor, the
        /// Testbed and any unauthored map get. Absent and "beach" mean the same thing to a reader, so the
        /// writer omits the field rather than stating the default (<c>Level.Balls</c> is nullable and
        /// <c>WhenWritingNull</c> drops it).
        /// </para>
        /// </summary>
        public BallStyle? Balls;

        public int Shots;
        public int CeilingStep;

        /// <summary>
        /// One ball in this many dealt into the magazine is a <b>wildcard</b> (#330), the joker that
        /// matches whatever it lands beside — null, the default, for every level that wants none, which
        /// was every shipped level until #420.
        /// <para>
        /// It is a property of the <b>set entry</b> and not of the layout, which is why it lives here
        /// rather than in the design's own drawing: a wildcard arrives through the magazine, so how
        /// often one comes is a rule about the level being played rather than about the picture hanging
        /// in it. <c>LevelSet</c> refuses a cadence of zero; <see cref="CampaignSet.WriteLevelSet"/> passes this straight
        /// through.
        /// </para>
        /// </summary>
        public int? WildcardEvery;

        /// <summary>Round radius, angle, layout level, layout depth -> is there a ball here.</summary>
        public Func<float, float, int, int, bool> Occupied;

        /// <summary>Taxicab radius instead, for the designs whose cross-section is a diamond.</summary>
        public Func<float, int, int, bool> OccupiedManhattan;

        /// <summary>
        /// Raw lattice indices instead (x, z, layout level, layout depth), for a design that is
        /// <b>drawn</b> rather than solved from a radius (#130). Every other shape here is a solid of
        /// revolution or a taxicab shape and reads a centred distance; a picture is a bitmap and needs
        /// the indices themselves, exactly as <see cref="BlockColour"/> already does for colour.
        /// </summary>
        public Func<int, int, int, int, bool> OccupiedBlock;

        //Exactly one of the three is set. They differ in what the pattern is a function of — the
        //centred polar frame, the centred taxicab one, or the raw lattice indices — and a design that
        //had to take all three would have to ignore two of them at every call site.
        public Func<float, float, int, int, BallType> Colour;
        public Func<float, int, int, BallType> ColourManhattan;
        public Func<int, int, int, BallType> BlockColour;

        /// <summary>
        /// What each ball <b>is</b>, beside what colour it is (#323/#325) — the second axis
        /// <see cref="BallKind"/> opened, and the whole of what the Mirage block needed from this tool.
        /// Left null every cell is <see cref="BallKind.Normal"/>, which is every design written before
        /// the eleventh block and what an absent <c>"k"</c> in a map file already meant.
        /// <para>
        /// It takes the <b>polar</b> frame, mirroring <see cref="Colour"/>, and
        /// <see cref="BlockKind"/> takes the raw lattice indices, mirroring <see cref="BlockColour"/>.
        /// There is deliberately no taxicab overload: the two designs here that are drawn on a taxicab
        /// radius reach it through <c>r</c> and <c>ang</c> anyway (a kind rule asks where a cell sits on
        /// the SKIN, which is a question about the shape's boundary rather than about its cross-section),
        /// and a third selector nobody sets is a third thing to keep in step.
        /// </para>
        /// <para>
        /// ⚠ <b>The colour is still resolved for a rock or a glass ball and still written to the file.</b>
        /// Neither kind reads it — the granite technique ignores the tint entirely and the clear glass has
        /// no dye in it — but the field is not nullable and a cell has to carry something. See
        /// <see cref="LevelEmitter.ROCK_TINT"/> for what the rocks are given and why it is not one of the
        /// colours the level plays.
        /// </para>
        /// </summary>
        public Func<float, float, int, int, BallKind> Kind;

        /// <inheritdoc cref="Kind"/>
        public Func<int, int, int, int, BallKind> BlockKind;
    }
}
