using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using System;
using System.IO;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// The campaign as a <b>set</b> rather than as designs: the block table, each block's music family and ball
    /// style, the unlock ramp, and <see cref="WriteLevelSet"/>, which writes <c>Levels.json</c> from the designs
    /// in play order and reads it back through the game's own loader. The play order itself is
    /// <see cref="Program.Main"/>'s, because it is a list of the designs; a design names its block's music and
    /// material from the constants here. Split out of <c>Program.cs</c> in #597.
    /// </summary>
    internal static class CampaignSet
    {
        /// <summary>
        /// The campaign's blocks, in order: what each is <b>called</b> — written onto every entry of it as
        /// <c>LevelSetEntry.Block</c> so the game can celebrate finishing one by name (#184) — and how many
        /// levels it holds. The sizes were all <c>BLOCK_SIZE</c> (10) until #295 put a five-level Eruption
        /// between two ten-level chapters, so equality stopped being a construction guarantee and became this
        /// table — and #369 then filled that block to ten, so every entry is 10 again while the table stays,
        /// because a size that is a table is a size that can differ and a size that is a constant cannot; <b>contiguity still is one</b> — names and gates fall out of the entry's position, so a
        /// block cannot reopen later in the set, which is the one thing <c>LevelSet.Load</c> refuses a file
        /// for. Set from the entry's <b>position</b> where <see cref="Design.Music"/> is set on each design,
        /// and the asymmetry is not an oversight: a theme is written into the level <i>file</i>, so it has to
        /// be a property of the design, while a block is written into the <i>set</i>, which is built from
        /// positions.
        /// </summary>
        private static readonly (string Name, int Size)[] BLOCKS =
        {
            ("The Meadow", 10), ("The Gallery", 10), ("The Coil", 10), ("The Tower", 10),
            //THE SILHOUETTES ARE INSERTED FIFTH (#491), between the Tower's violet dusk and the Reveal's cavern:
            //the night the light ramp was missing between the two, under the aurora. See the designs array.
            ("The Silhouettes", 10),
            ("The Reveal", 10),
            ("The Quarry", 10), ("The Nebula", 10), ("The Eruption", 10), ("The Spectrum", 10), ("The Arcade", 10),
            //THE GRID IS INSERTED ELEVENTH RATHER THAN APPENDED (#420), and that is the owner's ruling on the
            //one thing the position decides: the Mirage keeps the campaign's last word. Its own doc argues
            //for it in a sentence nothing here beats - "the eleventh chapter is the one place the arena is
            //not, and the balls stop obeying the rules the other hundred levels taught" - and a chapter of
            //named constructions is not that. Where the Grid does belong is between the made light and the
            //dream: the neon city, then a lattice that is nothing but made light, then the place that is not
            //a place. Every entry after it carries a gate twenty stars higher, which needed no retuning
            //because MinStarsAt is a function of POSITION (see it for the arithmetic that survives this).
            ("The Grid", 10), ("The Mirage", 10),
        };

        //THE BLOCKS' THEMES (#194). A block's music is named on every level of it, so the music changes
        //when the chapter does and not when the level does - see Design.Music for what naming it buys and what
        //leaving it null used to cost. Named after the block rather than after the piece because that is the
        //thing being decided: if a block's music is ever changed it is changed HERE, once, and not ten times.
        //
        //EVERY CHAPTER ITS OWN FAMILY SINCE #486, about ten recordings each, on the owner's word: "I like how
        //the desert chapter plays several versions of the soundtrack - I would like this for the other
        //chapters too, ideally around ten tracks per chapter." A name here is a FAMILY in Game/Music - the
        //files whose names start with it - and the game rotates through a family's recordings, one per level
        //opening, exactly as the Coil's Ember did with its five punk variations (#483 confirmed that chapter
        //as the reference point). Until #486 five pieces stood against twelve blocks and seven assignments
        //were reprises argued one by one (the Quarry's Pulse bookend, the Nebula's night jazz over the void,
        //the Arcade closing on the piece the Meadow opened with, the Spectrum's coda form, the Eruption's and
        //the Mirage's tally arguments); the argument that held the set at five - a composition being its own
        //work and a desktop to render it on - went with local generation, which is free.
        //
        //Four chapters keep the piece they had, each the only chapter on it now, and gained variations in its
        //register: the Gallery's Mural (the set's syncopated piece, log drum and marimba - afrobeat, highlife,
        //kora, desert blues around it), the Coil's Ember (the rock ballad and the punk set, plus stoner, surf,
        //rockabilly and garage), the Tower's Bohemia (the Dorian statement with strings and brass - marches,
        //accordion, hurdy-gurdy and a jig beside it) and the Reveal's Nocturne (night jazz - trip-hop, bossa,
        //vibes, noir). Pulse, the original eurodance piece, goes to THE GRID alone: it is the one chapter built
        //out of arithmetic and this is the one piece that sounds like a machine dancing, which the old comment
        //here already called the only defensible reprise. Seven families are new and named for the place
        //rather than the piece: the Meadow's BLOOM (positive and melodic, its own at last - #449), the
        //Quarry's LUNAR (spacious and slow), the Nebula's NEBULA (synthwave and the cosmos), the Eruption's
        //MAGMA (heat, weight and drums), the Spectrum's SKYLINE (a city waking: house, disco, funk), the
        //Arcade's NEON (outrun, chiptune, electro) and the Mirage's MIRAGE (the dream). The briefs are in each
        //recording's sidecar in Research/AI-Music; the masters live outside the repository (see its README).
        //If the ear disagrees with a family, it is one constant here and a folder of files.
        //
        //Colossus is the one level whose music this tool cannot pin (see WriteLevelSet's own note): its
        //"music" field is authored in Colossus.json itself and says the Quarry's family by hand.

        internal const string MUSIC_RINGS = "bloom";
        internal const string MUSIC_GALLERY = "mural";
        internal const string MUSIC_COIL = "ember";
        internal const string MUSIC_TOWER = "bohemia";
        internal const string MUSIC_REVEAL = "nocturne";
        internal const string MUSIC_QUARRY = "lunar";
        internal const string MUSIC_NEBULA = "nebula";
        internal const string MUSIC_VOLCANO = "magma";
        internal const string MUSIC_ARCADE = "neon";
        internal const string MUSIC_SPECTRUM = "skyline";
        internal const string MUSIC_GRID = "pulse";
        internal const string MUSIC_MIRAGE = "mirage";

        //THE SILHOUETTES' PUPPET (#558). The chapter borrowed the Quarry's LUNAR when #491 inserted it, the one
        //reprise left after #486; it has a family of its own now, briefed for the chapter's character rather
        //than its hour. Black paper cut-outs on a pale check are shadow-puppet theatre and a paper-cut picture
        //book, so the ten are light and curious - storybook pizzicato and clarinet, a shadow-puppet gamelan, a
        //caper, cartoon jazz, a silent-film rag, a toybox, baroque harpsichord, kalimba, a cheeky tango and
        //Nordic folk under the aurora - all in F major, where lunar's drones made a playful gallery of shapes
        //sound like the Moon. Like every family it ships on the owner's ear: a recording he dislikes is one
        //re-render, and this constant is the one line that would hand the chapter back to lunar.
        internal const string MUSIC_SILHOUETTES = "puppet";

        //WHAT EACH CHAPTER'S BALLS ARE MADE OF. A property of the block exactly as the music is — the
        //material changes when the chapter does and not when the level does — and stated once per block here
        //so a block's ten designs cannot drift apart.
        //
        //Since #272's eight styles landed there are ten materials, and since #295 made the Eruption the
        //tenth chapter every chapter hangs a different one and every material has a home — the
        //material is as strong a chapter marker as the scene and the piece of music. Each is placed where
        //it works rather than where it sounds good: two of the eight are scene-bound for reasons measured
        //in their own issues, and both are placed accordingly.
        //
        //The vinyl beach ball is back in the campaign since #295 — the Eruption took the lava its
        //entry always said the volcano had the better claim on, and the Reveal takes the vinyl home. It is
        //still also what everything unauthored draws — the map editor, the Testbed, the front end's own
        //preview, and any level that says nothing.
        //
        //The costs are all measured against the same control and are under "Ball rendering" in
        //docs/rendering.md. Only the bubble is dearer than the vinyl it replaces (about 10 % of a
        //frame at 4K-class fill); every other style here is cheaper, so this table is close to free
        //and in places a saving — which is why the densest chapters can carry what they carry.

        /// <summary>
        /// <b>The Meadow — glass bubbles</b> (#258), and the one entry that predates the rest. The Meadow is
        /// where it goes because it is where a player starts: the game is called Bubble Shooter and its own 3D
        /// wordmark stands in the front end swept in glass, so a first chapter of anything else promises a
        /// different game from the one the title card just showed.
        /// <para>
        /// It is also the honest place for the one expensive style: the transparency costs about 10 % of a
        /// frame at 4K-class fill, and the opening block hangs the smallest clusters in the campaign — 385
        /// balls at the top of it, against the Quarry's 959.
        /// </para>
        /// </summary>
        internal const BallStyle BALLS_MEADOW = BallStyle.Bubble;

        /// <summary>
        /// <b>The Gallery — wound wool</b> (#311). The savanna is warm and golden and the block is ten drawn
        /// pictures; wool is the only soft material in the set, and a picture built out of yarn reads as
        /// something made by hand, which is what a wall of drawn symbols already is. It is also the gentlest
        /// step after the opening chapter's glass, which suits the second chapter's place on the ramp.
        /// </summary>
        internal const BallStyle BALLS_GALLERY = BallStyle.Wool;

        /// <summary>
        /// <b>The Coil — polished marble</b> (#305). The desert's block hangs everything on slender links, and
        /// marble is the material that reads as <i>mass</i>: a coiled column of polished stone standing in
        /// sand is a monument, and the weight is what makes the slenderness alarming rather than merely thin.
        /// </summary>
        internal const BallStyle BALLS_COIL = BallStyle.Marble;

        /// <summary>
        /// <b>The Tower — frosted ice</b> (#307). Mountains, violet dusk, and a block whose layouts are deeper
        /// than the camera frames. Ice is the obvious material for the altitude, and the one whose read is
        /// light carried <i>through</i> the ball — which the mountains' low sun behind a tall cluster gives it
        /// more of than any other chapter.
        /// </summary>
        internal const BallStyle BALLS_TOWER = BallStyle.Ice;

        /// <summary>
        /// <b>The Reveal — polished marble</b> (#419). It was the vinyl beach ball from #295 to here, and the
        /// owner played it and reported that a beach ball and a cave do not go together. He is right, and the
        /// two arguments that put the vinyl here are worth answering rather than deleting, because one of
        /// them was never about the vinyl at all.
        /// <para>
        /// <b>"The plainest style does not compete with the payoff"</b> still holds and still points here.
        /// The block's statement is the <i>payoff</i> — a thing hidden inside another thing — so the material
        /// must stay quiet, which rules out the gem and the plasma. Marble is quiet: one solid colour with a
        /// vein through it. Photographed against the vinyl on Grotto, it is the VINYL that is the busier
        /// figure of the two, because its five white gores cut every ball into bands before the level's own
        /// colours are read at all.
        /// </para>
        /// <para>
        /// <b>"The vinyl's emissive heartbeat was designed against dark backdrops"</b> was the weaker half and
        /// it does not survive being checked: the heartbeat is not the vinyl's. Every ball technique carries
        /// <c>BallEmission</c> by contract — it is point 2 of the list in <c>InstancedModel.fx</c>'s own note
        /// on what a ball technique must do — and <c>MarblePS</c> carries it like the rest. The dark chapter
        /// keeps its breathing cluster whichever of the two it is drawn in.
        /// </para>
        /// <para>
        /// What marble adds is the thing the report is actually about: <b>mass</b>. It is the heavy style, a
        /// piece of cut stone, and this is the campaign's chapter under rock. The desert carries it too and
        /// that is fine — ice and porcelain each already serve two chapters, and a sunlit sandstone canyon
        /// and a dark cave light the same material into two different looks.
        /// </para>
        /// </summary>
        internal const BallStyle BALLS_REVEAL = BallStyle.Marble;

        /// <summary>
        /// <b>The Quarry — anodised metal</b> (#306). A quarry on the moon is a chapter about extracted ore,
        /// and this is the style whose colour <i>is</i> its reflectance — thirteen alloys rather than thirteen
        /// mirrors. The scene matters and not just the theme: metal goes flat on a bright featureless dome
        /// because a mirror ball has nothing to reflect but a gradient, and the moon's lit surface and hard
        /// shadow give it something. It is also the cheapest style in the set on the densest block in the
        /// campaign, which is not a coincidence worth wasting.
        /// </summary>
        internal const BallStyle BALLS_QUARRY = BallStyle.Metal;

        /// <summary>
        /// <b>The Nebula — plasma orbs</b> (#309). Deep space is where a style whose colour lives entirely in
        /// emission belongs: the dome contributes nothing, so a material that makes its own light is the only
        /// one that gains from being out there rather than merely surviving it. It is also the one style that
        /// is <i>alive</i> without anything happening, which is the right note for the chapter the campaign
        /// now ends on.
        /// <para>
        /// Note the inverse trap: the metal must NOT go here. A mirror in deep space reflects nothing and every
        /// ball comes out a flat coloured circle, which is the same fault #258 measured on the film.
        /// </para>
        /// </summary>
        internal const BallStyle BALLS_NEBULA = BallStyle.Plasma;

        /// <summary>
        /// <b>The Eruption — molten crust</b> (#295, from #310's own hand-over sentence). The volcano is the
        /// scene the style was designed for: dark crusted balls whose glowing seams read as cooling lava over
        /// the one backdrop where the ground itself glows, under the darkest dome (9), where an emissive
        /// material is the block's whole light. It also serves the block's statement literally — the glow is
        /// the load, and on these balls the glow is drawn as seams in dark crust.
        /// </summary>
        internal const BallStyle BALLS_VOLCANO = BallStyle.Lava;

        /// <summary>
        /// <b>The Arcade — cut gems</b> (#308). The neon city is the one scene that carries its own point
        /// lights, and a faceted stone is the material with most to do with them: every facet catches a
        /// different sign, so a cluster glitters in the colours of the street rather than of the sky. An
        /// arcade full of jewels is also the block's own register — five hollow pixel-art solids, played for
        /// spectacle.
        /// </summary>
        internal const BallStyle BALLS_ARCADE = BallStyle.Gem;

        /// <summary>
        /// <b>The Spectrum — crackled porcelain</b> (#312). This block sweeps one hue family through a whole
        /// level, so it wants the material that shows a hue best, and a deep glaze over a body is exactly
        /// that: the colour sits under a coat rather than on a surface, which is what makes a single family
        /// read as a range instead of as one flat note. The city at dawn is cool and hard, and so is this.
        /// </summary>
        internal const BallStyle BALLS_SPECTRUM = BallStyle.Porcelain;

        /// <summary>
        /// <b>The Mirage — crackled porcelain again</b>, and it is the table's <b>first reprise</b> (#325).
        /// Ten styles against eleven chapters ends the one-each era by arithmetic, so the only decision left
        /// is which one comes back, and this block decides it differently from every other entry above:
        /// <b>the choice is made against the two ball KINDS, not against the scene</b>.
        /// <para>
        /// Neither special takes the level's material — the granite ignores the tint outright and the clear
        /// glass has no dye in it — so a Mirage level is its style standing next to two things that are not
        /// it, and the style's whole job here is to be told apart from both. The glaze wins that twice: it is
        /// the deepest colour in the set (the tint sits <i>under</i> a coat rather than on a surface), which
        /// is the widest gap there is from a ball with no colour at all; and it is the most <i>worked</i>
        /// surface in the set — fired, glazed, crazed — against the one material in the game that has not
        /// been worked at all. Clear, coloured, and rough: three readings, no two alike.
        /// </para>
        /// <para>
        /// <b>What each of the others would have done instead is why this is not simply the leftover.</b> The
        /// bubble is the trap in one word: a level of transparent film with a transparent KIND in it says
        /// nothing at all. The ice is the same trap softened. The marble is the mirror image — a stone style
        /// beside a stone kind, and the rock's own header says its separation from a grey marble is its
        /// ROUGHNESS, which is exactly the distinction a marble level spends. The plasma and the lava make
        /// their own light, and the dream is already full of luminous orbs breathing through the murk
        /// (<c>docs/scenes.md</c>), so the cluster would sink into the backdrop. The metal is the documented
        /// inverse trap from the Nebula's entry — the dream replaces the sky with a slow marbled flow and a
        /// mirror ball in it reads as a smear. The wool and the gem would both have served; the glaze serves
        /// better on the second half, where matte-against-matte (wool) and facet-against-fleck (gem) are the
        /// two comparisons the granite is hardest to win.
        /// </para>
        /// </summary>
        //ICE for the Grid (#420), the owner's pick of the three reprises offered, and the argument is the
        //scene's light: the backdrop is black, the rig is cool cyan-blue, and a translucent ball refracting
        //that rig is the closest thing this game has to a hologram. The Tower wears the same material under
        //a violet dusk over mountains, which is far enough away that neither chapter reads as the other.
        internal const BallStyle BALLS_GRID = BallStyle.Ice;

        internal const BallStyle BALLS_MIRAGE = BallStyle.Porcelain;

        //WOOL FOR THE SILHOUETTES (#491), the Gallery's material a second time, and chosen by photograph: the
        //Fish hung on the aurora in wool, porcelain, gem and vinyl, and only the wool made the black shape one
        //solid silhouette - the glazed, faceted and gored styles each put a highlight on every black ball, so the
        //cut-out read as a heap of shiny balls. A chapter of drawn pictures on the soft material is also the
        //Gallery's own argument (BALLS_GALLERY) holding twice.
        internal const BallStyle BALLS_SILHOUETTES = BallStyle.Wool;

        /// <summary>
        /// Rewrites the set that orders the levels — since #194 as the <b>blocks of the <see cref="BLOCKS"/>
        /// table</b> rather than one flat ramp of fourteen, six of them since #207 added the desert, seven
        /// since #182 appended the Nebula, and ten since the Arcade, #253's Spectrum and #295's Eruption.
        /// <b>One opens the campaign, Colossus closes the Quarry, and the campaign closes on the last
        /// block's finale</b> — the campaign-complete celebration rides the set's last entry and moves with
        /// it: Colossus's until #182, Garland's then (the owner moved the last word deliberately), Globe's,
        /// Turbine's at #253, and Globe's again since #300 put the two cities in day order. One is a
        /// design here now (the author asked for it regenerated, see <see cref="Program.One"/>) and states its own rules
        /// with the rest, while Colossus is still hand-drawn and keeps its rules verbatim — it is authored
        /// content and this generator has no opinion about it beyond where it sits. The Nebula's designs arrive
        /// as the second array, appended after Colossus, because the Quarry's five entries are four designs plus
        /// that hand-drawn finale — five more designs in the first array would push Colossus out of the Quarry
        /// and misfile both blocks' members (still contiguous, so nothing would refuse the set; the milestones
        /// would simply fire on the wrong levels).
        /// <para>
        /// Colossus (once "Two", back when it was the second level) is the hardest level in the game by a
        /// distance: twelve wide, eighteen deep, six colours in 3×3 blocks rolled per level so nothing is ever
        /// a big easy plate, on 45 shots against a ceiling stepping every 4. Meeting that second was meeting
        /// the wall before the game had taught anything, so it moved to close the set — and the move gave it
        /// its own authored scene and sky (the Moon) the way every other level has one.
        /// </para>
        /// <para>
        /// <b>Colossus is the one level whose music this tool cannot pin</b>, because it does not write the file:
        /// its <c>"music"</c> field is authored in <c>Colossus.json</c> itself, and it has to say the Quarry's
        /// theme or the level falls back to the positional rotation and plays whatever its position happens to
        /// give. It agreed by luck before this was noticed (at 25 entries and four pieces, 24 % 4 was 0, which
        /// is Pulse, which is the Quarry's theme) — exactly the silent coupling to the order that #194 exists
        /// to remove, and <b>that luck is spent twice over</b> (the journal records both): with five pieces
        /// 24 % 5 is 4 and at Colossus's position 29 % 5 is also 4, both Ember, so the
        /// authored field is the only reason the Quarry's finale still plays its own block's piece. Its <c>"sky"</c> was
        /// moved from 2 to the block's 13 in the same edit: the number is <b>inert</b> on the Moon (one of the
        /// four sky-replacing scenes, #142) so it cannot be seen either way, and matching it is what keeps
        /// <see cref="DescribeBlock"/> from permanently reporting a difference nothing can render.
        /// </para>
        /// <para>
        /// The printout is grouped by block, because the block boundaries are the thing that has to be checked
        /// by eye: a block that is not one scene, one dome and one theme is the defect this whole change can
        /// have, and no gate anywhere refuses it.
        /// </para>
        /// </summary>
        internal static LevelSet WriteLevelSet(Design[] designs, params Design[][] blocksAfterColossus)
        {
            LevelSet set = new() { Name = "Bubble Shooter 3D" };

            for (int i = 0; i < designs.Length; i++)
            {
                Design d = designs[i];

                set.Levels.Add(new LevelSetEntry
                {
                    File = d.File,
                    Name = d.Name,
                    Block = BlockNameAt(i),
                    Shots = d.Shots,
                    CeilingStep = d.CeilingStep,
                    WildcardEvery = d.WildcardEvery,
                    MinStars = MinStarsAt(i),
                });
            }

            set.Levels.Add(new LevelSetEntry
            {
                File = "Colossus.json",
                Name = "Colossus",
                Block = BlockNameAt(designs.Length),
                Shots = 45,
                CeilingStep = 4,
                MinStars = MinStarsAt(designs.Length),
            });

            //Everything after the hand-drawn finale above, block by block — see the method doc for why those
            //blocks cannot sit in the first array. Positions continue where Colossus left off, so the block
            //name and the unlock gate fall out of the same two position functions as everything else's.
            foreach (Design[] block in blocksAfterColossus)
                foreach (Design d in block)
                {
                    int index = set.Levels.Count;

                    set.Levels.Add(new LevelSetEntry
                    {
                        File = d.File,
                        Name = d.Name,
                        Block = BlockNameAt(index),
                        Shots = d.Shots,
                        CeilingStep = d.CeilingStep,
                        WildcardEvery = d.WildcardEvery,
                        MinStars = MinStarsAt(index),
                    });
                }

            string path = Path.Combine(LevelEmitter.OutDir, LevelSet.DefaultFileName);
            set.Save(path);

            //Read back through the game's own validating loader, which is the only thing that can say the
            //set is well formed — it is what refuses a zero budget or a level that names no file
            LevelSet loaded = LevelSet.Load(path);

            Console.WriteLine();
            Console.WriteLine($"=== {LevelSet.DefaultFileName}: {loaded.Count} levels in {BLOCKS.Length} blocks ===");
            for (int i = 0; i < loaded.Count; i++)
            {
                if (BlockAt(i).Start == i)
                    Console.WriteLine($"  --- block {loaded.BlockNumber(i)}/{loaded.BlockCount}"
                                      + $" '{loaded.BlockName(i) ?? "unnamed"}' {DescribeBlock(loaded, i)}");

                int gate = loaded.Levels[i].MinStars.GetValueOrDefault();

                Console.WriteLine($"  {i + 1,2}. {loaded.DisplayName(i),-12} {loaded.DescribeRules(i)}"
                    + (gate > 0 ? $", unlocks at {gate} star(s)" : ", open from the start"));
            }

            //The set as the game will read it, handed back for the sag gate — which is priced off each
            //entry's own budget and ceiling step, and those live here rather than on any Design (Colossus has
            //no Design at all).
            return loaded;
        }

        /// <summary>
        /// The block the entry at <paramref name="index"/> belongs to — its name, its first entry's index and
        /// its size. A walk over <see cref="BLOCKS"/>' cumulative sizes rather than a division, since #295's
        /// five-level Eruption ended the era of equal blocks (and #369 restored it without restoring the
        /// assumption); contiguity is still by construction. It throws
        /// rather than wrapping if the catalogue outgrows the table: a set whose last block silently reopened
        /// the first one is a file <c>LevelSet.Load</c> refuses, and finding that out here is cheaper.
        /// </summary>
        private static (string Name, int Start, int Size) BlockAt(int index)
        {
            int start = 0;

            foreach ((string name, int size) in BLOCKS)
            {
                if (index < start + size) return (name, start, size);
                start += size;
            }

            throw new InvalidOperationException(
                $"entry {index + 1} falls past the {BLOCKS.Length} stated blocks ({start} entries); add an "
                + "entry to BLOCKS for every group the catalogue grows by");
        }

        /// <inheritdoc cref="BlockAt"/>
        private static string BlockNameAt(int index) => BlockAt(index).Name;

        /// <summary>
        /// One block's scene, dome, theme and ball style, read off the <b>level files the game will actually load</b> rather
        /// than off the designs that wrote them — which is the only reading that can catch the failure worth
        /// catching here. A block whose five levels disagree is reported as a disagreement rather than silently
        /// summarised from the first of them: it is not a thing any gate refuses, and the whole of #194 is that
        /// the five agree.
        /// </summary>
        private static string DescribeBlock(LevelSet set, int first)
        {
            string scene = null, music = null, balls = null;
            int sky = -1;
            bool sameScene = true, sameSky = true, sameMusic = true, sameBalls = true;

            for (int i = first; i < Math.Min(first + BlockAt(first).Size, set.Count); i++)
            {
                Level level = Level.Load(Path.Combine(LevelEmitter.OutDir, set.Levels[i].File));

                string thisScene = level.Scene?.ToString() ?? "(none)";
                string thisMusic = level.Music ?? "(rotation)";

                //Absent and "beach" are the same thing to a reader, and the writer omits the field rather than
                //stating the default — so the two have to read alike here or forty blocks would report a
                //disagreement with themselves (#258).
                string thisBalls = BallStyles.ToName(level.Balls ?? BallStyle.Beach);

                if (scene == null) { scene = thisScene; sky = level.SkyDome; music = thisMusic; balls = thisBalls; }
                else
                {
                    sameScene &= thisScene == scene;
                    sameSky &= level.SkyDome == sky;
                    sameMusic &= thisMusic == music;
                    sameBalls &= thisBalls == balls;
                }
            }

            return $"{(sameScene ? scene : "MIXED SCENES")}, sky {(sameSky ? sky.ToString() : "MIXED")}"
                   + $", {(sameMusic ? music : "MIXED THEMES")}"
                   + $", {(sameBalls ? balls : "MIXED BALL STYLES")}";
        }

        /// <summary>
        /// The unlock ramp, a function of the entry's <b>position in the set</b> rather than of any design —
        /// which level a gate guards is a property of the order, and a design moved in the order should carry
        /// its new place's gate, not its old one. In the campaign's star currency (see
        /// <c>Prazsky.BS3D.Scoring.StarRating</c>): the opener is free, the second level asks only that
        /// something was cleared, and from the third on the ramp climbs two stars per level. Clearing every
        /// prior level once (one star each) opens the first three gates on its own; past that, par clears
        /// (two stars) keep the road open with no replays, and only a player scraping by on one-star clears
        /// goes back for a better one. The most a player can hold at entry <paramref name="index"/> is
        /// <c>4 × index</c>, so the steepest gate still asks under half of what is on the table.
        /// <para>
        /// <b>The ramp is unchanged by #194, by #207 and again by #182, and that was checked rather than
        /// assumed</b>, because it now runs to thirty-five entries instead of the fourteen it was written for.
        /// The property that has to hold is the par one: a player who two-stars every level ahead of entry
        /// <i>i</i> holds <c>2i</c> against a gate of <c>2(i − 1)</c>, which clears it by two at every
        /// position, however long the set is. At the last entry the gate is 66 against the 136 four-star
        /// clears would have banked — still the "under half" above, so nothing needed retuning and no gate
        /// had to be made block-aware.
        /// </para>
        /// </summary>
        private static int? MinStarsAt(int index) => index switch
        {
            0 => null,
            1 => 1,
            _ => 2 * (index - 1),
        };
    }
}
