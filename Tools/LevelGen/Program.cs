using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// Writes the game's pattern levels (Three to Seven) and the set that orders them, and <b>validates
    /// every one through the game's own loader</b> before it is written anywhere the game will see it.
    /// A design is one <see cref="Design"/>: a silhouette, a colouring, a scene and a set of rules.
    /// <para>
    /// It exists because these levels are generated, and a generated level that is only checked by
    /// playing it is checked by nobody. The three properties it enforces are all invisible in a
    /// screenshot and all of them were got wrong at least once here — see <see cref="Validate"/>,
    /// <see cref="DropTest"/> and <see cref="FindLonelyBalls"/>. What it cannot check is whether the
    /// thing looks good, which is what the screenshot skill is for.
    /// </para>
    /// <para>
    /// <c>dotnet run --project Tools\LevelGen\LevelGen.csproj [output directory]</c>. Rewrites
    /// <c>Levels.json</c> too, One and Colossus included, so run it whole rather than for one level.
    /// </para>
    /// </summary>
    internal static class Program
    {
        // The field is 16 levels deep for every design: that is the deepest field the game hangs at its
        // standard height (FIELD_TOP_Y, 8/sqrt2) without raising it off the death line, so every level is
        // framed by the camera and the gun exactly the way One.json is. The layout hangs at the top and
        // the empty levels under it are the room shot balls attach into.
        private const byte FIELD_LEVELS = 16;

        private const float HALF = 0.5f;

        /// <summary>
        /// The smallest standing group a ball may belong to. It is <c>MINIMUM_CLUSTER_SIZE</c> less the one
        /// ball the player is about to land: a pair plus a shot is three and falls, a lone ball plus a shot
        /// is two and does nothing. Stated here rather than read off the physics constant because it is a
        /// statement about <i>authoring</i>, and the arithmetic linking them is the point of it.
        /// </summary>
        private const int MIN_GROUP = 3 - 1;

        /// <summary>
        /// How much of the cluster one shot may take before the level stops being a level. Not 100: a design
        /// whose best shot leaves a handful of balls standing is still over on the first lucky ball, and the
        /// two banded designs that failed this took 100% exactly, so the margin costs nothing and catches
        /// the near misses.
        /// <para>
        /// <b>The pack runs 6–52 % and the top of that band is where a level is DESIGNED to cascade</b>, not
        /// where one slipped through. Two things put a design up there and only one of them is about pictures.
        /// A drawn symbol in ONE ink is one connected group by construction (Smiley 52 %, Heart 42 %,
        /// Star 40 %) — and on a level whose point is being recognised, the symbol coming away in one piece is
        /// the reward. A colouring in wide concentric shells is the same thing on a solid of revolution, which
        /// is why the gentle block that teaches what a colour group <i>is</i> holds three of the six highest
        /// figures in the pack (One 49 %, Bullseye 45 %, Toadstool 42 %). This said "the top of that band is
        /// the three pictures" and quoted "4–16 % for the geometric levels", which was never true of One or
        /// Bullseye and is the reason it is now measured here rather than characterised.
        /// </para>
        /// <para>
        /// The bottom of the band is where the dial is turned the other way: <see cref="Zebra"/> at 9 % and
        /// <see cref="Elephant"/> at 21 % are pictures drawn in two and three inks, so their symbols are not one
        /// group at all, and <see cref="Lantern"/> at 6 % is a wall of panes with no plate anywhere. That is the
        /// whole difficulty ramp of #194's blocks, stated in one column of numbers.
        /// </para>
        /// </summary>
        private const int ONE_SHOT_PERCENT = 90;

        /// <summary>How many levels are in one block. See <see cref="Main"/> for what a block is (#194).</summary>
        private const int BLOCK_SIZE = 5;

        //THE FIVE BLOCKS' THEMES (#194). A block's piece is named on every level of it, so the music changes
        //when the chapter does and not when the level does - see Design.Music for what naming it buys and what
        //leaving it null used to cost. Named after the block rather than after the piece because that is the
        //thing being decided: if a block's music is ever changed it is changed HERE, once, and not five times.
        //
        //Four pieces exist (#163 would be a fifth) against five blocks, so exactly one is reprised, and it is
        //Pulse: the campaign opens on the piece Level One has always played and the finale brings it back.
        /// <summary>
        /// What each block is <b>called</b>, written onto every entry of it as <c>LevelSetEntry.Block</c> so the
        /// game can celebrate finishing one by name (#184). Indexed by block, so the order here IS the order of
        /// the catalogue's own five groups.
        /// <para>
        /// Set from the entry's <b>position</b> where <see cref="Design.Music"/> is set on each design, and the
        /// asymmetry is not an oversight: a theme is written into the level <i>file</i>, so it has to be a
        /// property of the design, while a block is written into the <i>set</i>, which is built from positions.
        /// Deriving it from the position also makes the blocks contiguous and equal by construction, which is
        /// the one thing <c>LevelSet.Load</c> refuses a file for getting wrong.
        /// </para>
        /// </summary>
        private static readonly string[] BLOCK_NAMES =
        {
            "The Meadow", "The Gallery", "The Tower", "The Reveal", "The Quarry"
        };

        private const string MUSIC_RINGS = "pulse";
        private const string MUSIC_GALLERY = "dechovka";
        private const string MUSIC_TOWER = "bohemia";
        private const string MUSIC_REVEAL = "nocturne";
        private const string MUSIC_QUARRY = "pulse";

        /// <summary>Where the levels are written. Set once in <see cref="Main"/>, read by everything below.</summary>
        private static string _outDir;

        private static int Main(string[] args)
        {
            try
            {
                _outDir = args.Length > 0 ? args[0] : FindLevelsDirectory();
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }

            Console.WriteLine($"Writing to {_outDir}");

            //IN PLAY ORDER, AND IN BLOCKS OF FIVE (#194). A block is one scene, one dome, one music theme and
            //one statable style, and it is contiguous — so a fixed chunk of BLOCK_SIZE by position IS a block,
            //which is what #184's block-completion celebration needs and what a flat list could not give it.
            //
            //The campaign's light drains out of it as it goes: green noon, gold afternoon, violet dusk,
            //underground dark, airless black. Difficulty ramps with it, and so does how much of each block is
            //new — one new level in the first block, then two, three, four, and one again at the finale, which
            //closes on the hardest content the pack already had.
            //
            //ONE RECORDED DECISION IS REVERSED HERE and it is worth naming rather than leaving to be noticed.
            //The three pictures used to be deliberately INTERLEAVED with the geometric levels, "because they
            //are all gentle by design, and three easy levels back to back is a lull rather than a ramp". They
            //are a block now. What changed is the thing that reasoning rested on: the campaign was one flat
            //ramp of fourteen, where a run of easy levels is simply a flat stretch of it. A block is a chapter
            //with its own scene, sky and music, and a chapter of pictures is a change of register rather than a
            //stall — provided the block itself ramps, which is what Elephant and Zebra are for: their symbols are
            //drawn in three inks and two, so neither has the single big payoff that makes the shipped three
            //gentle, and the block's best single shot falls 40 %, 21 %, 9 % across its last three levels. The lull
            //was real; five gentle levels would still be one.
            Design[] designs =
            {
                //1. THE MEADOW - "Rings". Solids of revolution in concentric shells or angular sectors: every
                //colour is a plate of dozens, so one matching ball takes a whole shell. The block that teaches
                //what a colour group is, in the cheapest scene in the game under the one clear blue dome.
                One(), Bullseye(), Toadstool(), Pinwheel(), Gem(),

                //2. THE SAVANNA - "The Gallery". Flat drawn walls, read off a bitmap written in the source.
                Heart(), Smiley(), Star(), Elephant(), Zebra(),

                //3. THE MOUNTAINS - "The Tower". The layout is deeper than the camera frames, so a level's
                //length is its height and it is worked from the underside up as the glass hands it down.
                Crown(), Horn(), Column(), Helix(), Lean(),

                //4. THE CAVERN - "The Reveal". An outer body with a differently-shaped thing standing inside
                //it; clearing the outside is the payoff (#161).
                Onion(), Chest(), Fossil(), Mango(), Lantern(),

                //5. THE MOON - "The Quarry". Chunky lattice-aligned blocks of colour, five or six of them, and
                //no plate to trigger anywhere: every shot is a shot at a handful of balls. Colossus closes it,
                //and closes the campaign, from WriteLevelSet.
                Mosaic(), Prism(), Hopper(), Static()
            };

            bool ok = true;
            foreach (Design design in designs) ok &= Emit(design);

            WriteLevelSet(designs);

            //A non-zero exit so this can be put in front of a commit: a level that fails the checks is a
            //level that plays wrong, and the whole point of generating them is that nobody has to notice
            if (!ok) Console.WriteLine("At least one level FAILED its checks - see above.");

            return ok ? 0 : 1;
        }

        /// <summary>
        /// The game's <c>Game\Levels</c>, found by walking up from wherever this was built to the repository
        /// root. The tool lives at a known depth inside the repo but is <i>run</i> from its bin directory,
        /// whose depth depends on the configuration and target framework, so the walk is by landmark rather
        /// than by counting <c>..</c> — and an explicit directory can always be passed instead.
        /// </summary>
        private static string FindLevelsDirectory()
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Game", "Levels");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException(
                $"No 'Game\\Levels' directory above '{AppContext.BaseDirectory}'. Pass the output directory as an argument.");
        }

        /// <summary>
        /// Rewrites the set that orders the levels — since #194 as <b>five blocks of <see cref="BLOCK_SIZE"/></b>
        /// rather than one flat ramp of fourteen. <b>One opens the campaign and Colossus closes it</b>; One is a
        /// design here now (the author asked for it regenerated, see <see cref="One"/>) and states its own rules
        /// with the rest, while Colossus is still hand-drawn and keeps its rules verbatim — it is authored
        /// content and this generator has no opinion about it beyond where it sits.
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
        /// theme or the level falls back to the positional rotation and plays whatever position 25 happens to
        /// give. It agreed by luck before this was noticed (24 % 4 is 0, which is Pulse, which is the Quarry's
        /// theme) — exactly the silent coupling to the order that #194 exists to remove. Its <c>"sky"</c> was
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
        private static void WriteLevelSet(Design[] designs)
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

            string path = Path.Combine(_outDir, LevelSet.DefaultFileName);
            set.Save(path);

            //Read back through the game's own validating loader, which is the only thing that can say the
            //set is well formed — it is what refuses a zero budget or a level that names no file
            LevelSet loaded = LevelSet.Load(path);

            Console.WriteLine();
            Console.WriteLine($"=== {LevelSet.DefaultFileName}: {loaded.Count} levels in blocks of {BLOCK_SIZE} ===");
            for (int i = 0; i < loaded.Count; i++)
            {
                if (i % BLOCK_SIZE == 0)
                    Console.WriteLine($"  --- block {loaded.BlockNumber(i)}/{loaded.BlockCount}"
                                      + $" '{loaded.BlockName(i) ?? "unnamed"}' {DescribeBlock(loaded, i)}");

                int gate = loaded.Levels[i].MinStars.GetValueOrDefault();

                Console.WriteLine($"  {i + 1,2}. {loaded.DisplayName(i),-12} {loaded.DescribeRules(i)}"
                    + (gate > 0 ? $", unlocks at {gate} star(s)" : ", open from the start"));
            }
        }

        /// <summary>
        /// Which block the entry at <paramref name="index"/> belongs to. A plain division, because the campaign's
        /// blocks are equal and contiguous by construction here — see <see cref="BLOCK_NAMES"/>. It throws rather
        /// than wrapping if the catalogue outgrows the names: a set whose last block silently reopened the first
        /// one is a file <c>LevelSet.Load</c> refuses, and finding that out here is cheaper.
        /// </summary>
        private static string BlockNameAt(int index)
        {
            int block = index / BLOCK_SIZE;

            if (block >= BLOCK_NAMES.Length)
                throw new InvalidOperationException(
                    $"entry {index + 1} falls in block {block + 1} but only {BLOCK_NAMES.Length} block names are "
                    + "stated; add one to BLOCK_NAMES for every group of BLOCK_SIZE the catalogue grows by");

            return BLOCK_NAMES[block];
        }

        /// <summary>
        /// One block's scene, dome and theme, read off the <b>level files the game will actually load</b> rather
        /// than off the designs that wrote them — which is the only reading that can catch the failure worth
        /// catching here. A block whose five levels disagree is reported as a disagreement rather than silently
        /// summarised from the first of them: it is not a thing any gate refuses, and the whole of #194 is that
        /// the five agree.
        /// </summary>
        private static string DescribeBlock(LevelSet set, int first)
        {
            string scene = null, music = null;
            int sky = -1;
            bool sameScene = true, sameSky = true, sameMusic = true;

            for (int i = first; i < Math.Min(first + BLOCK_SIZE, set.Count); i++)
            {
                Level level = Level.Load(Path.Combine(_outDir, set.Levels[i].File));

                string thisScene = level.Scene?.Kind.ToString() ?? "(none)";
                string thisMusic = level.Music ?? "(rotation)";

                if (scene == null) { scene = thisScene; sky = level.SkyDome; music = thisMusic; }
                else
                {
                    sameScene &= thisScene == scene;
                    sameSky &= level.SkyDome == sky;
                    sameMusic &= thisMusic == music;
                }
            }

            return $"{(sameScene ? scene : "MIXED SCENES")}, sky {(sameSky ? sky.ToString() : "MIXED")}"
                   + $", {(sameMusic ? music : "MIXED THEMES")}";
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
        /// <b>The ramp is unchanged by #194 and that was checked rather than assumed</b>, because it now runs to
        /// twenty-five entries instead of fourteen. The property that has to hold is the par one: a player who
        /// two-stars every level ahead of entry <i>i</i> holds <c>2i</c> against a gate of <c>2(i − 1)</c>, which
        /// clears it by two at every position, however long the set is. At the last entry the gate is 46 against
        /// the 96 four-star clears would have banked — still the "under half" above, so nothing needed retuning
        /// and no gate had to be made block-aware.
        /// </para>
        /// </summary>
        private static int? MinStarsAt(int index) => index switch
        {
            0 => null,
            1 => 1,
            _ => 2 * (index - 1),
        };

        #region The designs

        /// <summary>
        /// The campaign's opener: a full square slab against the glass with a round pyramid tapering to a
        /// point under it. It replaces the hand-drawn <c>One.json</c>, at the author's request and for two
        /// reasons they gave — the pyramid had a <b>tail</b>, and it had no sky of its own.
        /// <para>
        /// The tail was real and is gone. The old layout narrowed from a 100-ball slab down to a single ball
        /// nine levels below it and then <b>widened again</b>, to 4, 8, 12, 25 and 16 — sixty-five balls
        /// hanging under the point, which reads as something stuck to the pyramid rather than as part of it.
        /// The point is now the bottom of the level.
        /// </para>
        /// <para>
        /// Two things came free with the regeneration. The old field was 10 wide against a slab occupying
        /// x 0…9 — <b>no lateral margin at all</b>, the wall trap that made shots bounce off the flanks of
        /// the pattern pack; twelve gives it the clear column every other level now has. And a plain map file
        /// carries no scene, so One played in whichever backdrop the player last picked; as a level file it
        /// names one.
        /// </para>
        /// <para>
        /// <b>That scene is the meadow now, and not the savanna it opened in until #194.</b> The savanna was
        /// picked when this design was written and the reason given was only that it is the warmest dome of the
        /// set (14) — nothing about a square slab over a round pyramid needs a savanna. Three things pay for the
        /// move. The block is one place at one hour, and this is the meadow block. The savanna is the one
        /// campaign scene that carries <b>point lights of its own</b> — a ring of flickering campfires that
        /// <c>SceneLights</c> puts onto the balls — and the level that teaches colour matching is the last one
        /// whose colours should be tinted by the backdrop. And the savanna misses 75 FPS at High on a 6900XT
        /// (#165) where the meadow's terrain is the cheapest in the game, which is the right trade on the level
        /// a first-time player meets first. Dome 1 is the block's, for <see cref="Bullseye"/>'s own reason.
        /// </para>
        /// <para>
        /// Four colours in concentric rings, which is a deliberate simplification: the old One used
        /// <b>eight</b>, and the magazine draws evenly among the colours still alive, so the wanted ball
        /// arrived one time in eight — a harder draw than anything after it, on the level that teaches the
        /// game. Rings also keep several colours on the slab, which is the anchor layer, so no single ball
        /// can cut the whole level loose.
        /// </para>
        /// </summary>
        private static Design One() => new()
        {
            File = "One.json",
            Name = "One",
            Grid = 12,
            Depth = 10,
            Scene = new MeadowSceneConfig(),
            Sky = 1,
            Music = MUSIC_RINGS,
            Shots = 30,
            CeilingStep = 5,
            //The slab is SQUARE and everything under it is round, which is the shape the old One had and the
            //reason this reads as a pyramid hanging off a plate rather than as a cone. The square extent is
            //recovered from the polar pair the emitter passes: max(|cos|,|sin|) scaled by the radius is the
            //Chebyshev distance, i.e. the half-extent of the square the point sits on.
            Occupied = (r, ang, i, depth) => i == depth - 1
                ? Chebyshev(r, ang) <= 4.5f
                : r <= 0.4f + i * 0.375f,
            //Each shape gets rings of its OWN kind — square ones on the square slab, round ones on the round
            //pyramid. Round rings over the whole thing was the first try and it left the slab's four corners
            //poking out past the last round ring into a ring of their own: ten balls of a fourth colour, in
            //threes, in the corners, on the level that teaches the game. Squared, the slab's outermost ring
            //is a proper border and the palette is three honest colours.
            Colour = (r, ang, i, depth) => Ring(i == depth - 1 ? Chebyshev(r, ang) : r,
                new[] { BallType.Type1, BallType.Type7, BallType.Type4 }),
        };

        /// <summary>
        /// A stepped cone hanging point-down, coloured in concentric rings: a target seen from underneath,
        /// and a flight of coloured steps seen from the side. Three colours, each ring one solid shell, so
        /// every ring is already a group of dozens waiting for one matching ball to touch it.
        /// </summary>
        /// <summary>
        /// A heart drawn across a flat hanging wall (#130) — the first level in the pack that is a
        /// <b>picture</b> rather than a solid of revolution, and the first whose shape is read off a bitmap
        /// instead of solved from a radius. Gentle on purpose: a generous shot budget, a slow ceiling and
        /// three colours, in One's spirit, because the point of it is to be recognised rather than aimed
        /// through.
        /// <para>
        /// The background is a 2×2 check of two colours and not one flat colour, which is the rule the
        /// picture region states and the drop test enforces: a single-colour background makes the wall's top
        /// row one group holding the whole thing up, and one matching ball takes the level.
        /// </para>
        /// </summary>
        /// <summary>
        /// One picture level: a flat wall the size of its own <paramref name="bitmap"/>, hanging in a
        /// <paramref name="grid"/>-wide field. The bitmap's own dimensions are the wall's, so a symbol is
        /// added by drawing it and nothing else — there is no second place to keep its size in step.
        /// </summary>
        /// <param name="symbol">
        /// What the symbol is drawn in — <b>one colour per ink</b>, in <see cref="SYMBOL_INK"/>'s own order, so
        /// a silhouette passes one, a symbol with a detail inside it two, and a drawn scene four. It was a
        /// <c>symbol</c> and an <c>accent</c> while there were three pictures, which is the exact shape of the
        /// three gentle ones and exactly what kept a picture from being hard: see the region's remarks on inks
        /// as the difficulty dial. The three shipped pictures pass one and two colours here and come out
        /// <b>byte-identical</b> to what the pair produced.
        /// </param>
        /// <param name="background">
        /// The wall behind it, laid down as a 2×2 check of these. <b>Never one colour</b>: see the region's
        /// remarks and the drop test — a flat background makes the wall's top row a single group holding the
        /// whole picture up. Two is the gentle case; three or four quarters every background group and thins the
        /// magazine's draw with it, at the cost of nothing but the entries in this array.
        /// </param>
        private static Design Picture(string file, string name, SceneConfig scene, byte sky, string music,
            int shots, int ceilingStep, string[] bitmap, byte grid,
            BallType[] symbol, BallType[] background)
        {
            int width = bitmap[0].Length;

            //Even by construction here: the field is 16 and an odd difference would have the loader extend it
            //a level and move the drawing off where it was put. A bitmap with an odd number of rows is caught
            //by the emitter's own offset check rather than silently drawn in the wrong place.
            byte depth = (byte)bitmap.Length;

            return new Design
            {
                File = file,
                Name = name,
                Grid = grid,
                Depth = depth,
                Scene = scene,
                Sky = sky,
                Music = music,
                Shots = shots,
                CeilingStep = ceilingStep,
                OccupiedBlock = (x, z, i, d) => OnWall(x, z, i, d, width, grid, out _, out _),
                BlockColour = (x, z, i) =>
                {
                    OnWall(x, z, i, depth, width, grid, out int column, out int row);

                    int ink = SYMBOL_INK.IndexOf(PixelAt(bitmap, column, row));

                    //An ink this picture has no colour for is a typo in the bitmap, and it is refused here for
                    //the same reason the emitter refuses an odd layout offset: it would otherwise be drawn as
                    //BACKGROUND — a hole in the symbol, which is the one mistake in a hand-drawn bitmap that
                    //looks deliberate. The palette's own length says how many inks a picture uses, so a
                    //two-colour symbol is held to two however many characters SYMBOL_INK knows.
                    if (ink >= symbol.Length)
                        throw new InvalidOperationException(
                            $"{file}: '{SYMBOL_INK[ink]}' at column {column}, row {row} is ink {ink + 1} of a "
                            + $"{symbol.Length}-colour symbol");

                    return ink >= 0 ? symbol[ink] : Band((column / 2) + (row / 2), background);
                },
            };
        }

        /// <summary>
        /// A heart. Easy on purpose — a budget that forgives, and a ceiling slow enough that the picture can
        /// be read while it is played — because the point of it is to be recognised rather than aimed through.
        /// </summary>
        private static Design Heart() => Picture("Heart.json", "Heart", new SavannaSceneConfig(), sky: 14,
            MUSIC_GALLERY, shots: 60, ceilingStep: 10, HEART, grid: 15,
            symbol: new[] { BallType.Type1 },
            background: new[] { BallType.Type4, BallType.Type7 });

        /// <summary>
        /// A smiley: a yellow face with black eyes and a smile, over a blue-and-white sky check. The first
        /// picture here drawn in <b>two inks</b> — a symbol with detail inside it rather than a silhouette — and
        /// the reason <see cref="Picture"/> ever took more than one colour for a symbol. It takes a palette of
        /// however many the bitmap uses now (<see cref="SYMBOL_INK"/>), which is what lets
        /// <see cref="Elephant"/>'s three inks and <see cref="Zebra"/>'s stripes be the Gallery's hard end.
        /// </summary>
        private static Design Smiley() => Picture("Smiley.json", "Smiley", new SavannaSceneConfig(), sky: 14,
            MUSIC_GALLERY, shots: 60, ceilingStep: 10, SMILEY, grid: 15,
            symbol: new[] { BallType.Type7, BallType.Type8 },
            background: new[] { BallType.Type3, BallType.Type4 });

        /// <summary>
        /// A five-pointed star, yellow over a night check. It was drawn against the space backdrop — the only
        /// scene in the game whose own sky is already a star field, which was the wittiest scene pairing in the
        /// pack — and #194 <b>costs it that joke</b>: the Gallery is one place, and it is the savanna, because
        /// two of the three shipped pictures were already there and space is where the Quarry's <see cref="Static"/>
        /// came from. The loss is real and is recorded here rather than quietly dropped. What is left is a
        /// night-blue check hanging in gold daylight, which the block's own sky makes read as a lantern rather
        /// than as a constellation — a different picture, not a worse one.
        /// </summary>
        private static Design Star() => Picture("Star.json", "Star", new SavannaSceneConfig(), sky: 14,
            MUSIC_GALLERY, shots: 55, ceilingStep: 9, STAR, grid: 15,
            symbol: new[] { BallType.Type7 },
            background: new[] { BallType.Type3, BallType.Type5 });

        /// <summary>
        /// A zebra, and the <b>stripes are the difficulty</b>: the first picture in the pack whose symbol is not
        /// one group. Two inks in bands two columns wide cut the animal into standing stripes, the largest 40
        /// balls, so there is no moment where the drawing comes away in one piece — measured best single shot
        /// <b>9 %</b> of the cluster, against Star's 40 %, Heart's 42 % and Smiley's 52 % — the lowest figure of
        /// any picture here and level with the hard end of the campaign. It is a picture that plays like
        /// <see cref="Static"/>, and the reason is drawn on its face, which is the point: a player can see why it
        /// is hard. It closes the Gallery for that reason.
        /// <para>
        /// <b>Upright bands, not the animal's own slanted ones.</b> A stripe one cell wide running diagonally is
        /// the lonely-ball rule's own worked example — cells touch only their four orthogonal neighbours on a
        /// level — while two columns standing upright are a solid slab whatever the level's parity. The bands are
        /// struck across the whole drawing rather than fitted to it, so the legs come out half black and half
        /// white, which is what a zebra's legs actually look like.
        /// </para>
        /// <para>
        /// <b>Fifteen columns in a seventeen-wide field</b> where the gentle three are 13 in 15: on a picture the
        /// wall <i>is</i> the level, so widening it is the only way one gets bigger, and 420 balls against their
        /// 364 is as far as that goes before the field is wider than anything else in the pack (<see cref="Gem"/>
        /// is 17).
        /// </para>
        /// <para>
        /// <b>A cool, quiet two-colour sky, and a screenshot is what settled that.</b> It was drawn against three
        /// warm background colours first — yellow, red and green, on the ink dial's own logic that more ground
        /// colours is more difficulty — and the animal <i>disappeared</i>: black and white are the two least
        /// saturated things in the palette and three saturated hues behind them mean the eye reads the check and
        /// not the shape. Against a blue-and-cyan check, which is close in both hue and luminance, the striped mass
        /// separates cleanly and the neck and legs are legible. The difficulty that ground colour was carrying is
        /// carried by the stripes, which is the dial that does not cost legibility. <see cref="Elephant"/> before
        /// it lost a four-colour ground the same way and for the same reason.
        /// </para>
        /// <para>
        /// Measured: 420 balls, margin 1, nothing alone, 4 in pairs, nothing recoloured, four colours, and best
        /// single shots of 5 %, 9 %, 7 % and the black stripes' own. The anchor row is all background and carries
        /// both sky colours, so there is no single-colour anchor. 48 shots stepping every 8 is six descents, the
        /// shipped pictures' six, against twice the work. One near miss under the lonely-ball rule is worth
        /// naming: in the leg rows each ink is a <b>single column</b> four rows tall, and what saves it is the wall
        /// being two cells thick in Z (so a group of eight) rather than anything about the drawing.
        /// </para>
        /// </summary>
        private static Design Zebra() => Picture("Zebra.json", "Zebra", new SavannaSceneConfig(), sky: 14,
            MUSIC_GALLERY, shots: 48, ceilingStep: 8, ZEBRA, grid: 17,
            symbol: new[] { BallType.Type4, BallType.Type8 },
            background: new[] { BallType.Type3, BallType.Type5 });

        /// <summary>
        /// An elephant's head face-on, and the <b>first picture here drawn in three inks</b>: a pale face, blue
        /// ears either side, black eyes and a black trunk hanging down the middle. It is the Gallery's step up
        /// from the gentle three — the symbol is three groups rather than one, so the biggest payoff on it is the
        /// face at 21 % where <see cref="Smiley"/>'s is 52 % — and it is the block's most legible level, which is
        /// the point of putting it before <see cref="Zebra"/> rather than after.
        /// <para>
        /// <b>The background is two warm colours against a cool animal, and that is what makes it read.</b> This
        /// began with the four-colour ground the ink dial suggests, and a screenshot refused it: a 17-wide wall of
        /// yellow, red, magenta and green check is four saturated hues competing with a symbol drawn in the two
        /// least saturated, and the eye reads the ground rather than the shape. Two warm colours behind a
        /// cyan-and-blue head is a hue contrast instead of a contest, and the elephant comes forward. The
        /// difficulty the fourth ground colour was carrying is carried by the <b>symbol's</b> three inks, which is
        /// the dial that does not cost legibility — see the region's remarks.
        /// </para>
        /// <para>
        /// Measured: 420 balls, margin 1, <b>nothing alone, nothing in a pair and nothing recoloured</b> — the
        /// cleanest wall in the pack — five colours running 40 to 114 balls, and best single shots 8 %, 8 %, 21 %,
        /// 13 % and 5 %. Two of those figures are larger than the colour's own biggest group (36 against 20, and
        /// 58 against 22), which is the drop test counting what a ground group was the last anchor for: the
        /// ears and the trunk hang off the check around them.
        /// </para>
        /// </summary>
        private static Design Elephant() => Picture("Elephant.json", "Elephant", new SavannaSceneConfig(), sky: 14,
            MUSIC_GALLERY, shots: 48, ceilingStep: 8, ELEPHANT, grid: 17,
            symbol: new[] { BallType.Type5, BallType.Type3, BallType.Type8 },
            background: new[] { BallType.Type7, BallType.Type1 });

        /// <summary>
        /// An elephant's head face-on, 15 by 14: forehead, an ear either side, two eyes and the trunk. Three
        /// inks — <c>#</c> the face, <c>o</c> the ears, <c>+</c> the eyes and the trunk.
        /// <para>
        /// The ears stop square rather than tapering to a point: a one-column ear tip is a column of four balls
        /// that reads as a fray rather than as an ear, and the taper is carried by the face's own two narrowing
        /// rows instead. The eyes are <b>inside</b> the face, never a hole in it.
        /// </para>
        /// </summary>
        private static readonly string[] ELEPHANT =
        {
            "...............",
            "...............",
            "....#######....",
            ".ooo#######ooo.",
            ".ooo#######ooo.",
            ".ooo++###++ooo.",
            ".ooo++###++ooo.",
            ".ooo#######ooo.",
            "..oo#######oo..",
            ".....#####.....",
            "......+++......",
            "......+++......",
            "......+++......",
            "......+++......",
        };


        private static Design Bullseye() => new()
        {
            File = "Three.json",
            Name = "Bullseye",
            Grid = 15,
            Depth = 4,
            Scene = new MeadowSceneConfig(),
            //Dome 1 is the only clear blue one in the set; most of the rest are warm or magenta, and over
            //green hills those read as a clash rather than as weather. A red-and-gold target wants that blue.
            //Since #194 that is the whole block's dome and not just this level's: the block's scene and sky
            //were chosen to be the pair this design already needed, so its pairing anchors the block rather
            //than surviving it.
            Sky = 1,
            Music = MUSIC_RINGS,
            Shots = 40,
            CeilingStep = 8,
            //Widest at the top (that layer anchors the whole cluster to the glass) and narrowing downwards
            Occupied = (r, ang, i, depth) => r <= 5.7f - (depth - 1 - i) * 1.15f,
            Colour = (r, ang, i, depth) => Ring(r, new[] { BallType.Type1, BallType.Type4, BallType.Type7 }),
        };

        /// <summary>
        /// A cylinder tiled in 2x2x2 blocks of colour — a chunky mosaic column. Horizontal colour bands
        /// were the first try and they were a <b>one-shot level</b>: a band is a single group, and the top
        /// band is what holds the cluster to the glass, so one matching ball dropped all 387 balls at once.
        /// Blocks keep the graphic look and put all three colours on the anchor layer, which is the rule
        /// every design here has to satisfy — see the drop test in <see cref="Validate"/>.
        /// </summary>
        private static Design Mosaic() => new()
        {
            File = "Four.json",
            Name = "Mosaic",
            Grid = 13,
            Depth = 6,
            //A dark room for the brightest cluster in the game. It played over the sea, which was the wrong
            //pairing twice over: the sea mirrors its dome, so it is a large area of whatever the sky is doing
            //and never a backdrop, and against a lit dusk the CMY blocks had nothing to be vivid against.
            //The requirement that pairing states is DARK, and the Moon meets it: #194 gave the cavern to the
            //Reveal block and moved this to the Quarry, where the sky is replaced with black and MoonSceneConfig
            //drives the whole light rig, so the CMY blocks are still the only saturated thing in the frame. What
            //is given up is "enclosed" — a cavern wraps the cluster and the Moon puts it against a horizon — and
            //that was never the part doing the work here.
            Scene = new MoonSceneConfig(),
            Sky = 13,
            //The one design that has to be worked rather than triggered: its best shot takes 24 of 387
            //balls, so it wants a budget nearer two shots per block than the four-shot cascades elsewhere
            Shots = 56,
            CeilingStep = 8,
            Music = MUSIC_QUARRY,
            Occupied = (r, ang, i, depth) => r <= 4.5f,
            //Blocked on the raw indices rather than on the centred position: the blocks are meant to be
            //lattice-aligned, and the half-cell stagger between levels is the packing showing through.
            BlockColour = (x, z, i) => Band((x / 2) + (z / 2) + (i / 2),
                new[] { BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// A toadstool: a cap of three concentric rings with a stalk hanging under it — the red rim, white gills
        /// and gold core of the thing, seen from underneath, which is where the player is standing.
        /// <para>
        /// <b>The stalk is not a second body bolted on; it is the core ring continued.</b> The occupancy is the
        /// cap's dome <i>or</i> <see cref="TOADSTOOL_STALK"/> of the axis at any height, and the colouring is the
        /// same <see cref="Ring"/> over the whole layout, so the stalk comes out in the core's own colour and in
        /// the core's own group by construction. That is what answers the objection <see cref="One"/> records
        /// against its old <b>tail</b> — "something stuck to the pyramid rather than part of it": a tail is a
        /// lobe of its own hanging off a point, where this is 105 balls of one group running from the glass to
        /// the floor, of which the stalk is the visible bottom half.
        /// </para>
        /// <para>
        /// <see cref="TOADSTOOL_SQUASH"/> is what buys the stalk its room. A true hemisphere of radius 5.3 is
        /// 7.5 levels deep and would fill the whole layout; squashed to 0.68 the cap fills five levels and the
        /// stalk gets the other five, in a field that is still the standard sixteen. The cap's radius is 5.3 and
        /// not less because three rings of <see cref="Ring"/>'s own 1.9 thickness need a rim past 3.8 — at 4.5
        /// the outer ring is 0.7 of a cell wide, which is a dotted circle and the lonely-ball trap
        /// <see cref="Gem"/> was rebuilt for. Here it is 1.5 cells wide and five levels tall.
        /// </para>
        /// <para>
        /// <see cref="TOADSTOOL_STALK"/> is 1.7 rather than a round number because of what the two level
        /// parities do with it: it takes 12 cells on an unshifted level and 9 on a shifted one — three to four
        /// cells across, thin enough to read as a stalk and thick enough that it is never a string of balls
        /// touching nothing. At 1.4 it drops to 4 and 5 cells, which is a stem you can see through.
        /// </para>
        /// <para>
        /// Measured: 389 balls, margin 2, nothing alone, nothing in a pair, nothing recoloured, and per level
        /// 12, 9, 12, 9, 12 for the stalk then 37, 52, 69, 88, 89 for the cap. The three colours come out
        /// 164/120/105 — best single shots 42 %, 30 % and 26 %, so the widest is under <see cref="Bullseye"/>'s
        /// own 45 % two levels earlier. All three hang off the 89-cell anchor layer alone, with nothing falling
        /// when the other two are taken away, which is the check that says the stalk is the core continued.
        /// </para>
        /// </summary>
        private static Design Toadstool() => new()
        {
            File = "Toadstool.json",
            Name = "Toadstool",
            //Fifteen for a cap reaching 5.3, which is Bullseye's and Pinwheel's field: the block frames the same
            Grid = 15,
            //Ten deep, of which the cap is the top five and the stalk the bottom five. Even by necessity — the
            //field is 16 and Emit refuses an odd offset — and it leaves the six empty levels of growth room.
            Depth = 10,
            Scene = new MeadowSceneConfig(),
            Sky = 1,
            Music = MUSIC_RINGS,
            Shots = 44,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) =>
                DomeDistance(r, i, depth, TOADSTOOL_SQUASH) <= TOADSTOOL_CAP || r <= TOADSTOOL_STALK,
            //Gold core and stalk, white gills, red rim: a fly agaric from underneath. Rings on the plain round
            //radius and NOT on the dome distance the cap is cut from — shells parallel to a curved surface hide
            //two of the three colours behind the third, which is the whole reason Bullseye's rings read.
            Colour = (r, ang, i, depth) => Ring(r,
                new[] { BallType.Type7, BallType.Type4, BallType.Type1 }),
        };

        /// <summary>
        /// A disc cut into four spiral sectors — a pinwheel from below, four vertical wedges from the side.
        /// The twist term is what bends the sector boundaries into a spiral instead of a cross.
        /// <para>
        /// Moved out of the desert into the meadow block by #194, which nothing recorded here argued against —
        /// dome 7 was the blazing red one and this design never gave a reason for it. What to watch is the one
        /// thing the move can cost: a red sector is a quarter of the disc, and dome 1's blue is behind the upper
        /// half of it. If the red loses its edge there the fix is the palette, not the dome, which is the whole
        /// block's; a screenshot decides it, as it did for <see cref="Gem"/>'s ring.
        /// </para>
        /// </summary>
        private static Design Pinwheel() => new()
        {
            File = "Five.json",
            Name = "Pinwheel",
            Grid = 15,
            Depth = 4,
            Scene = new MeadowSceneConfig(),
            Sky = 1,
            Music = MUSIC_RINGS,
            Shots = 44,
            CeilingStep = 9,
            Occupied = (r, ang, i, depth) => r <= 5.5f,
            Colour = (r, ang, i, depth) => Sector(ang, r * 0.16f, 4,
                new[] { BallType.Type1, BallType.Type7, BallType.Type2, BallType.Type3 }),
        };

        /// <summary>
        /// A crown: a hollow ring in vertical bars of colour, tapering gently wider toward the top, with six
        /// pointed teeth rising a pair of levels above the band — one per bar — each tipped with a magenta
        /// accent. The drain is still visible straight up the middle (the inner radius never closes), so a shot
        /// fired up the axis still goes clean through and the player still has to work the ring rather than
        /// spray at the centre. The teeth are the silhouette read: a plain constant-radius ring read as a
        /// napkin ring, plainer than its neighbours despite carrying more balls (#174), so the band now tapers
        /// the way <see cref="Bullseye"/> and <see cref="Prism"/> do and the teeth extend it upward, narrowing
        /// to the accent — the same shape a crown is, in the same occupancy arithmetic the other solids of
        /// revolution already use. See <see cref="CrownOccupied"/> and <see cref="CrownColour"/> for the band,
        /// the teeth and the tip.
        /// </summary>
        private static Design Crown() => new()
        {
            File = "Six.json",
            Name = "Crown",
            Grid = 15,
            Depth = 8,
            Scene = new MountainSceneConfig(),
            //Dome 8, a deep violet dusk, and not the 10 this shipped with. Under 10 the peaks came out pale
            //sand against a candy-pink sky and the whole frame read as kitsch; under 8 they read as snow and
            //the sky as weather, which is the same scene doing what it was built to do. The crown's gold and
            //red carry against a dark sky, where against pink they were competing with it. Since #194 that dome
            //is the whole Tower block's, for this level's own reason — and this level OPENS the block because it
            //is the one member the camera frames whole: a hollow ring teaches the axis, and the drain visible
            //straight up the middle of it teaches why the axis matters, before four levels that reach out of
            //shot ask the player to work one.
            Sky = 8,
            Music = MUSIC_TOWER,
            Shots = 44,
            CeilingStep = 9,
            Occupied = CrownOccupied,
            Colour = CrownColour,
        };

        //Crown's shape, in layout levels (i = 0 at the bottom, depth-1 at the anchor bonded to the glass).
        //The band occupies i = 0..CROWN_BAND_TOP; the teeth rise the two levels above it, their tips on the
        //anchor level. The inner radius never closes, so the drain up the axis survives every level.
        private const int CROWN_SECTORS = 6;
        private const int CROWN_BAND_TOP = 5;
        private const float CROWN_INNER = 2.9f;
        private const float CROWN_BAND_OUTER = 5.3f;       //widest at the band's own top, tapering down from it
        private const float CROWN_BAND_TAPER = 0.15f;      //per layout level below the band top
        private const float CROWN_TOOTH_INNER = 3.0f;
        private const float CROWN_TOOTH_OUTER = 5.0f;
        private const float CROWN_TOOTH_HALFFRAC = 0.28f;  //~34° — a chunky body the tip tapers out of
        private const float CROWN_TIP_INNER = 3.3f;
        private const float CROWN_TIP_OUTER = 4.8f;
        private const float CROWN_TIP_HALFFRAC = 0.25f;    //30° — narrower than the body, reads as the point
        private static readonly BallType CROWN_ACCENT = BallType.Type6;   //magenta — not in the bar palette
        private static readonly BallType[] CROWN_BARS =
        {
            BallType.Type7, BallType.Type3, BallType.Type1,
            BallType.Type7, BallType.Type3, BallType.Type1,
        };

        /// <summary>
        /// Crown's occupancy: a gently tapering band (the ring itself, with the drain up the middle), six
        /// teeth rising above it — one per bar — and each tooth narrowing to a tip on the anchor level. The
        /// band's outer radius uses the same <c>(top − i) · taper</c> idiom <see cref="Bullseye"/> and
        /// <see cref="Prism"/> do; the teeth reuse <see cref="Sector"/>'s own <c>+0.5</c> framing through
        /// <see cref="InCrownToothWedge"/>. The tip lives on the anchor level, so it bonds straight to the
        /// glass — no tooth floats.
        /// </summary>
        private static bool CrownOccupied(float r, float ang, int i, int depth)
        {
            if (i <= CROWN_BAND_TOP)
                return r >= CROWN_INNER && r <= CROWN_BAND_OUTER - (CROWN_BAND_TOP - i) * CROWN_BAND_TAPER;

            //The tooth tip on the anchor level: the narrowest, outermost point of each tooth, drawn in the
            //accent colour by CrownColour. Kept to >=2 cells by CROWN_TIP_HALFFRAC/OUTER so it is its own
            //connected group and not a lonely ball the repair pass would recolour back into the bar.
            if (i == depth - 1)
                return r >= CROWN_TIP_INNER && r <= CROWN_TIP_OUTER && InCrownToothWedge(ang, CROWN_TIP_HALFFRAC);

            //The tooth body one level below the tip: wider, in the bar's own colour, so it extends that bar
            //upward into the tooth and the tooth reads as the bar growing into a point rather than as a
            //separate stud sat on top of the ring.
            return r >= CROWN_TOOTH_INNER && r <= CROWN_TOOTH_OUTER && InCrownToothWedge(ang, CROWN_TOOTH_HALFFRAC);
        }

        /// <summary>
        /// Crown's colour: the bar palette everywhere except the tooth tips, which take the accent — a set
        /// jewel at each point, distinct from the bar it grows out of, so the points read as deliberate
        /// marks rather than as the bar colour trailing off into the tooth.
        /// </summary>
        private static BallType CrownColour(float r, float ang, int i, int depth)
        {
            if (i == depth - 1 && InCrownToothWedge(ang, CROWN_TIP_HALFFRAC))
                return CROWN_ACCENT;

            return Sector(ang, 0f, CROWN_SECTORS, CROWN_BARS);
        }

        /// <summary>
        /// Whether <paramref name="ang"/> falls within the middle of a Crown sector — i.e. within a tooth,
        /// since one tooth sits at each sector's centre (where the bars are). <paramref name="halfFrac"/> is
        /// half the tooth's width as a fraction of the sector (0.5 would fill the whole sector, 0 the
        /// boundary line), built on <see cref="SectorIndex"/>'s own <c>+0.5</c> framing so the tooth and the
        /// bar share the same centre.
        /// </summary>
        private static bool InCrownToothWedge(float ang, float halfFrac)
        {
            float turns = (ang / MathF.Tau) + 0.5f;
            float frac = turns * CROWN_SECTORS - MathF.Floor(turns * CROWN_SECTORS);
            return frac >= 0.5f - halfFrac && frac <= 0.5f + halfFrac;
        }

        /// <summary>
        /// An octahedron hanging point-down, cut into concentric diamond rings — the angular answer to
        /// <see cref="Bullseye"/>'s round ones, and the one design whose silhouette reads from any angle.
        /// Banding it by height was the first try and it lost the level in one shot for the reason given
        /// on <see cref="Mosaic"/>; rings put three colours on the anchor layer.
        /// </summary>
        private static Design Gem() => new()
        {
            File = "Seven.json",
            Name = "Gem",
            //Seventeen where the round designs need fifteen, and the diamond is why: a taxicab rim of m = 7
            //reaches seven whole cells along each axis, where a round radius of 5.5 reaches five. Fifteen
            //put the four points of the diamond exactly ON the field wall — no lateral margin at all, the
            //trap LateralMargin now refuses. Widening the field keeps the shape, which is the thing worth
            //keeping here; capping the rim at m = 5 would have cost the gem two rings of its widest face.
            Grid = 17,
            Depth = 6,
            //The meadow block's, since #194. The recorded decision here was about the ring's COLOUR — magenta
            //sank into the dream's violet soup, which a screenshot said and a palette on paper would not have —
            //and not about the dream itself; yellow reads against everything, so the reason for it survives the
            //move untouched. Under a clear blue dome the octahedron reads as cut glass in daylight, which plays
            //to the one thing this shape is documented for: a silhouette that reads from any angle wants a plain
            //sky behind it rather than a violet fog.
            Scene = new MeadowSceneConfig(),
            Sky = 1,
            Music = MUSIC_RINGS,
            Shots = 44,
            CeilingStep = 9,
            //Diamond cross-section (|dx| + |dz|), widening towards the top, and rings measured on that same
            //taxicab radius so the rings follow the facets instead of cutting across them.
            //
            //Both numbers below are forced by the lattice rather than chosen for looks. The taxicab radius
            //only ever lands on whole numbers, and cells on one level touch only ORTHOGONALLY — so a ring one
            //unit wide is a diagonal staircase of balls that do not touch each other at all. Rings are
            //therefore two units wide (floor(m/2), not the m/2.2 this started with), and every layer's rim
            //stops on an ODD m so its outermost ring is a complete two, never a bare diagonal. The first Gem
            //broke both: its top layer's rim was the single shell m=7, twenty-four balls standing alone
            //against the glass with no level above to connect through, each needing two landed balls of its
            //own colour before anything could fall. It is the hardest defect here to see and the easiest to
            //author by accident, which is why FindLonelyBalls now refuses it.
            OccupiedManhattan = (m, i, depth) => m <= 1 + 2 * ((i + 1) / 2),
            //Yellow rather than the magenta this started with: the dream scene is a violet soup and the
            //magenta ring sank into it, which a screenshot showed and a palette on paper would not have
            ColourManhattan = (m, i, depth) => Band((int)MathF.Floor(m * HALF),
                new[] { BallType.Type7, BallType.Type3, BallType.Type5 }),
        };

        /// <summary>
        /// A stepped cone tiled in 2×2×1 blocks of five colours — the first level of the pack that has to be
        /// aimed rather than triggered. Everything before it is built out of plates and wedges of dozens;
        /// here a block is four balls and there are five colours to draw from, so the useful ball arrives a
        /// fifth of the time and clears four when it does.
        /// </summary>
        private static Design Prism() => new()
        {
            File = "Eight.json",
            Name = "Prism",
            Grid = 15,
            Depth = 6,
            //The Quarry's, since #194. The sea gave this nothing it was chosen for — no note here ever argued
            //it — and it gave up something: the sea mirrors its dome, so it is a large area of whatever the sky
            //is doing rather than a backdrop, which is the same objection Mosaic's own comment makes about
            //having played there. Five colours in blocks of eight want a ground that stays still behind them.
            Scene = new MoonSceneConfig(),
            Sky = 13,
            Music = MUSIC_QUARRY,
            Shots = 60,
            CeilingStep = 6,
            //Stepped, so the silhouette is not another cylinder and the lower steps are reachable early
            Occupied = (r, ang, i, depth) => r <= 5.5f - (depth - 1 - i) * 0.55f,
            //Blocks TWO levels tall (x / 2, z / 2, i / 2), where this shipped with one. One level made the
            //group four balls, and against five colours that is a shot for every four — the widest step is
            //the last thing left standing and it took two dozen shots on its own, which is the "the last
            //storey drags" this was reported as. Two levels doubles every group to eight without touching
            //the shape or the palette, which are the parts worth keeping.
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i / 2,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type7 }),
        };

        /// <summary>
        /// The hardest of the generated set and the one that stands in front of Colossus: a full cylinder, blocks
        /// of four, and <b>six</b> colours scattered so no two neighbouring blocks agree by design. There is
        /// no plate to trigger anywhere on it — every shot is a shot at four balls, and the ceiling steps
        /// every five while you take them.
        /// </summary>
        private static Design Static() => new()
        {
            File = "Nine.json",
            Name = "Static",
            Grid = 15,
            //Four deep and not six. The difficulty here is the six colours and the four-ball group, not the
            //tonnage: six deep came out at 555 balls, half again as many as Colossus, and the pack has a weak
            //laptop to run on. Four keeps it in Colossus's family at ~370 while every shot still costs the same.
            Depth = 4,
            //Space until #194, and the Moon is the same airless family with a ground under it — which is what
            //this level wanted from space and did not get: six colours of small blocks against nothing at all
            //read as noise floating in a void, where the Moon's lit horizon gives the cluster somewhere to be.
            Scene = new MoonSceneConfig(),
            Sky = 13,
            Music = MUSIC_QUARRY,
            Shots = 60,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => r <= 5.5f,
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// A square block with a terraced pit bored up into it from underneath — the quarry itself rather than
        /// another thing cut out of it. The outer wall is a plain square prism and all the geometry is on the
        /// <b>inside</b>: the void is widest at the bottom level and steps in by <see cref="HOPPER_BENCH"/> a level
        /// until it closes, so the player is looking up into four benches of diced colour.
        /// <para>
        /// <b>The hole is the point, and it is the opposite of <see cref="Crown"/>'s.</b> Crown's ring lets a shot
        /// up the axis pass clean through, which is what stops the player spraying at the middle; this one is a
        /// funnel that <i>catches</i> — a ball fired up the axis goes into the pit and lands on whichever bench it
        /// reaches first, so the middle of the level is a target with a shape rather than a hole or a wall.
        /// </para>
        /// <para>
        /// <b>The void widens downwards, which is the only direction a void may widen.</b> The mass is what hangs
        /// off the glass, so the top two levels are left solid — the mouth is 3.6 less 0.9 a level and reaches zero
        /// at i = 4 — and everything below hangs off that lid. A pit widening the other way would hollow out the
        /// anchor layer and stand the whole block on its own rim.
        /// </para>
        /// <para>
        /// <b>The wall is square and the pit is round, and swapping either is a worse level.</b> A round wall is
        /// Mosaic's and Static's silhouette, which the block already has twice; a square pit would leave the bottom
        /// bench 0.9 of a cell wide the whole way round, where a round pit inside a square wall runs 0.9 at the
        /// axis directions and 2.8 at the corners — so the bench that is thinnest where the player is aiming
        /// straight up is thickest at the corners, and every level of it is carried by the wider ring above anyway.
        /// </para>
        /// <para>
        /// Measured: 465 balls — the heaviest thing in the Quarry, against Mosaic's 387, Static's 370 and
        /// Colossus's 364 — margin 1, nothing alone, nothing in a pair, 1 recoloured, six colours running 53 to
        /// 122 balls, thirty standing groups from 4 to 63 with a median of 15, and a best single shot of 63
        /// (13 %). Per level 56, 60, 88, 80, 100 and 81, so the terraces are as drawn. <b>No empty cell is sealed
        /// in</b>, which is the check that says the pit is open from below as designed and no colour is parked
        /// where nothing can reach it.
        /// </para>
        /// </summary>
        private static Design Hopper() => new()
        {
            File = "Hopper.json",
            Name = "Hopper",
            //Thirteen, as Mosaic has, for a wall reaching 4.75: one free column all round
            Grid = 13,
            Depth = 6,
            Scene = new MoonSceneConfig(),
            Sky = 13,
            Music = MUSIC_QUARRY,
            //Prism's pair of numbers: ten steps of ceiling against the ten empty field levels under a six-deep
            //layout, so the budget and the descent run out together. 465 balls against Prism's 351 and six
            //colours against five is where the difficulty is — 7.8 balls a shot, between Static's 6.2 and
            //Colossus's 8.1.
            Shots = 60,
            CeilingStep = 6,
            //Chebyshev at x.75 rather than on the half: the polar round-trip makes an exactly attainable
            //threshold a coin toss per cell, which is what costs One.json's slab four of its hundred balls.
            //The pit's own threshold is compared against r itself, which the emitter builds without
            //trigonometry, and 3.6 - 0.9i never lands on an attainable radius.
            Occupied = (r, ang, i, depth) => Chebyshev(r, ang) <= HOPPER_WALL
                                             && r >= HOPPER_MOUTH - i * HOPPER_BENCH,
            BlockColour = (x, z, i) => Scatter(x / 2, z / 2, i / 2,
                new[] { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type5, BallType.Type6, BallType.Type7 }),
        };

        /// <summary>
        /// The first <b>tall</b> level: a column reaching up out of shot, played from the bottom as the glass
        /// brings it down. The camera frames only the lowest <c>FRAMED_LEVELS</c> of a field this deep (see
        /// <c>GameplayScreen.FRAMED_LEVELS</c>), so the level's length is its height rather than its
        /// footprint, and the player never sees the top of what they are working through.
        /// <para>
        /// <b>Narrow on purpose.</b> Nine wide against the pack's thirteen: the column is four times the
        /// depth, so at thirteen it would be well over a thousand constrained bodies, and a tall level is
        /// meant to last through its height and not through its mass. Nine also keeps the whole visible face
        /// within an easy traverse, which matters when the interesting cells are all at the bottom.
        /// </para>
        /// <para>
        /// The descent is the ordinary <c>ceilingStep</c> and no new mechanic — but it is doing a second job
        /// here. On every other level it is the pressure; on this one it is also how the level is <i>fed</i>,
        /// so it is set fast (every 5, against the 8 to 10 the gentle levels take) alongside the largest budget
        /// in the game. That coupling is the thing to watch when this is tuned: a step too slow leaves the
        /// player with nothing in reach, and too fast is a level that arrives at the death line with most of
        /// its column still overhead.
        /// </para>
        /// <para>
        /// Since #194 it is the middle level of the Tower block rather than a lone tall level in a flat ramp,
        /// and it is the block's <b>endurance</b> beat: the largest budget, the plainest silhouette, and a
        /// colour rule that is nothing but reading what is coming. <see cref="Horn"/> before it and
        /// <see cref="Helix"/> and <see cref="Lean"/> after it are each tall in a way this one is not.
        /// </para>
        /// </summary>
        private static Design Column() => new()
        {
            File = "Ten.json",
            Name = "Column",
            Grid = 11,
            //The whole point. FIELD_LEVELS is 16 everywhere else; this field is 34 deep, of which 24 carry
            //balls — a layout half again as tall as an ordinary level's entire field, and the camera frames
            //18 of it (GameplayScreen.FRAMED_LEVELS). The ten empty levels under it are the usual growth room.
            Depth = 24,
            FieldLevels = 34,
            Scene = new MountainSceneConfig(),
            //Dome 8, the block's, where this shipped under 1. Nothing here argued for 1; dome 8 is Crown's
            //measured pairing (see Crown) and a block is one place at one hour.
            Sky = 8,
            Music = MUSIC_TOWER,
            Shots = 90,
            CeilingStep = 5,
            //Five cells across, against the pack's eleven. Twenty-four levels of a thirteen-wide disc would
            //be well over a thousand constrained bodies; this is ~500, in Colossus's family, and a tall level is
            //meant to last through its height rather than its mass.
            Occupied = (r, ang, i, depth) => r <= 2.6f,
            //Bands two levels thick around four colours: reading the column is reading what is coming, and a
            //band is a group of ~50, so the descent keeps handing the player something worth hitting
            Colour = (r, ang, i, depth) => Band(i / 2,
                new[] { BallType.Type1, BallType.Type5, BallType.Type7, BallType.Type3 }),
        };

        /// <summary>
        /// A sphere hanging whole from the glass - the roundest shape the lattice can make - coloured like a
        /// halved onion: a yellow skin around a white bulk around a small green heart, peeled from the
        /// outside in as the player clears it.
        /// <para>
        /// <see cref="SphereDistance"/> is true 3D distance from the sphere's own centre, with the level
        /// index scaled by <c>1/sqrt(2)</c> to match <c>BallsMap.GetRealPosition</c>'s vertical spacing - a
        /// radius built from <c>i</c> and <c>r</c> untouched comes out an egg, stretched along Y, because a
        /// level is not one lattice unit tall. <see cref="Depth"/> and the field's own centre are chosen so
        /// the sphere's north and south poles land exactly on the layout's top and bottom levels.
        /// </para>
        /// <para>
        /// The skin cannot wrap the whole sphere: the top layer is the one bonded to the glass, and a top
        /// layer of one colour anchors everything under it to that colour's single group (the rule every
        /// design here answers - see <see cref="Validate"/>). A true 3D shell also narrows to a single point
        /// at each pole, so whichever colour that point falls in becomes the entire cap. <see cref="OnionShell"/>
        /// answers both at once: it rings each level by its <b>own</b> radius rather than by distance from
        /// the sphere's centre, so every level - however small its own cap is - shows the same green-centre,
        /// white-ring, yellow-rim proportions the equator does. That is also just what a real onion's rings
        /// look like at any height: narrower near the root and stem, never absent.
        /// </para>
        /// </summary>
        private static Design Onion() => new()
        {
            File = "Eleven.json",
            Name = "Onion",
            Grid = 15,
            //One short of the true round number (see SphereDistance's own remarks): a mathematically exact
            //sphere at this radius wants Depth 16, whose top and bottom layers are then single points too
            //narrow to reliably carry all three colours (the annulus a lattice this coarse needs to land a
            //ball in it). Fifteen trims a hair off each pole instead - a slightly flattened onion, which is
            //closer to a real one's shape than a mathematical sphere is anyway.
            Depth = 15,
            //At the generous end of the pack's usual growth room (12, matching Bullseye/Pinwheel/Static):
            //the sphere's own bottom pole already reaches the layout's own floor, unlike every stepped-cone
            //design here, which tapers to its point several levels above the layout ends - so this design
            //has no such margin built in above the field's own growth room and wants the full amount of it.
            FieldLevels = 27,
            //The cavern since #194, where this opens the Reveal block: it is the family's original, so it
            //teaches the peel before the four levels that hide something other than a smaller sphere. The forest
            //it came from was never argued for, and the cavern is the one scene the pack documents as chosen for
            //being dark and enclosed — which is what a reveal wants, since it makes the cluster the only lit
            //thing in the frame. It inherits that pairing from Mosaic, which vacated it for the Moon.
            Scene = new CavernSceneConfig(),
            //Inert here: the cavern is one of the four sky-replacing scenes, so this number pins nothing but
            //what the settings row cycles from (SceneRenderer.ReplacesSky, #142).
            Sky = 13,
            Music = MUSIC_REVEAL,
            Shots = 48,
            CeilingStep = 8,
            Occupied = (r, ang, i, depth) => SphereDistance(r, i, depth) <= ONION_RADIUS,
            Colour = (r, ang, i, depth) => OnionShell(r, ang, i, depth),
        };

        #region The reveals (#161)

        //Onion was the family's original and these are the answer to what #161 actually asked for: an inside
        //that is a DIFFERENT SHAPE, "a mango, or a kinder surprise", and not a smaller sphere of another colour.
        //Four facts decide how one is built, and three of them are things Onion does not do.
        //
        //THE UNDERSIDE IS THE FACE THE PLAYER SEES. The cluster hangs from the glass and the gun stands on the
        //island below it, so anything open at the bottom shows its inside from the first frame. A bell with a
        //clapper in it is not a reveal at all; every body here is closed underneath.
        //
        //A BODY HUNG FROM ITS WIDEST SECTION HAS NO ANCHOR PROBLEM. The anchor layer is the layout's top level
        //and nothing else touches the glass, so whatever that layer is made of decides the level: one colour
        //there and the first matching ball takes everything. A sphere narrows to a point up there, which is the
        //trap OnionShell answers by RE-COLOURING each level by its own radius - measured, Onion's anchor layer is
        //16 cells in two colours. Answer it with the SILHOUETTE instead and the colouring is free: Chest's lid is
        //49 cells in four colours, Mango's shoulder 66 in five.
        //
        //THE PAYOFF MUST HANG ON ITS OWN, and this is the one no gate can see, because Validate only reads the
        //layout as authored. Measured off the emitted files: keep only Onion's green heart and all 161 balls of
        //it fall - it is held up by the white around it, so the thing the level is named for cannot outlive the
        //peel. Every payoff here reaches the glass through its own cord, stalk or stem and measures 0 balls
        //falling when everything else is taken away.
        //
        //AND IT MUST BE SEALED. A payoff a shot can already touch is not a reveal; 20 of Onion's 161 heart balls
        //are on the outside at the start. Sealed costs something worth naming: the magazine draws evenly among
        //the colours still ALIVE, so part of the draw cannot clear anything until the shell is breached. It is
        //not a dead shot - a payoff-coloured ball lands on the shell and becomes a seed of its own colour out
        //there - but it is a slow one, and it is why these budgets are generous against their clears.

        /// <summary>
        /// A flat-faced box with a <b>ball in it</b> — the family's plainest statement, and the one that teaches
        /// the block. A crate of dark iron, red paint and brass in chunky two-cell slats; six closed faces, so
        /// what the player sees at the start is a solid box and nothing else. It is <b>hollow</b>, and the first
        /// slat that comes away opens onto a dark cavity with a pearl hanging in it on a short cord.
        /// <para>
        /// Square where every other reveal here is turned: the box is cut on the <b>Chebyshev</b> extent (the
        /// square half-extent <see cref="Chebyshev"/> recovers from the polar pair, taken straight off the offsets
        /// here because <see cref="ChestPart"/> already works in the lattice frame), so its faces are straight in
        /// WORLD space and alternate 8 cells and 7 with the packing's half-cell stagger — a brick bond, which is
        /// what a crate should look like anyway.
        /// </para>
        /// <para>
        /// <b>The pearl hangs from the lid, not from the box.</b> A ball resting in a cavity is connected for the
        /// validator and gone the moment the shell is cleared, which is the defect the family's original carries;
        /// the cord is a column of the pearl's own colour up the axis and through the lid to the glass, so
        /// measured, keeping only the pearl leaves 61 balls with <b>0 falling</b>. At the start 0 of those 61 are
        /// on the outside — the cavity is sealed, so it floods to nothing from outside the field.
        /// </para>
        /// <para>
        /// Slats colour by <c>(x / 2) + (z / 2)</c>, which is <see cref="Mosaic"/>'s rule with the level term
        /// dropped: constant sums run along the one diagonal a cell never touches across levels (its cross-level
        /// neighbours are the <c>+1,+1</c> pair, never <c>+1,−1</c>), so no two same-coloured columns merge.
        /// </para>
        /// <para>
        /// Measured: 630 balls — the heaviest reveal here — margin 3, nothing alone, 12 in pairs (box cells
        /// clipped by the pearl; <see cref="Static"/> ships 14), nothing recoloured, and the flattest report in
        /// the block: best single shots 48, 48, 48 and 61, i.e. 7 %, 7 %, 7 % and 9 %. <b>The grain is not as
        /// even as "one 2x2 column each" suggests, and the budget is set from the measurement rather than from
        /// that model.</b> There are 30 groups, sized 2, 2, 2, 2, 2, 2, 3, 3, 3, 4, 8, 8, 8, 8, 8, 8, 10, 30, 36,
        /// 36, 36, 36, 36, 36, 48, 48, 48, 48, 48 and 61 — the cavity splits the four centre columns into a lid
        /// fragment and a floor fragment that never touch, which is where the eights, threes and twos come from.
        /// So the shell is 29 groups and the pearl one, i.e. <b>30 perfect landings minimum</b>, not the thirteen
        /// a uniform-column model predicts. 56 shots is <see cref="Mosaic"/>'s budget for a comparable count;
        /// at the 40 this was first authored with it would have been tighter than Colossus, which the docs call
        /// the wall, on the level that teaches the block.
        /// </para>
        /// </summary>
        private static Design Chest() => new()
        {
            File = "Chest.json",
            Name = "Chest",
            Grid = CHEST_GRID,
            Depth = CHEST_DEPTH,
            Scene = new CavernSceneConfig(),
            //Inert in the cavern (SceneRenderer.ReplacesSky, #142); the block's number, as Onion states it
            Sky = 13,
            Music = MUSIC_REVEAL,
            Shots = 56,
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) => ChestPart(x, z, i) != 0,
            BlockColour = (x, z, i) => ChestPart(x, z, i) == 2
                ? BallType.Type4
                : Band((x / 2) + (z / 2), new[] { BallType.Type8, BallType.Type1, BallType.Type7 }),
        };

        /// <summary>
        /// A plain boulder of masonry-blocked stone with a <b>fern frond pressed flat inside it</b> — the one
        /// reveal here whose payoff is a PICTURE, drawn off a bitmap in the source the way the Gallery block's
        /// walls are (#130) and buried in the middle of a solid of revolution. Outside it is a rock and gives
        /// nothing away; halfway there is green showing through a hole; the payoff is a flat frond hanging in the
        /// dark, facing the gun.
        /// <para>
        /// The picture plane is <c>(x, level)</c> and the slab is <see cref="FOSSIL_SLAB"/> thick in Z, for the
        /// reasons the pictures region states in full: the gun starts at +Z looking at the origin, so X runs
        /// across the screen and the level axis up it, and a slab one cell thick in Z has half its cross-level
        /// neighbours reaching to a Z that is not there. Two to three cells is what makes it a solid plate at
        /// either parity.
        /// </para>
        /// <para>
        /// <b>The frond is clipped to <see cref="FOSSIL_BURY"/> inside the rim, and that one rule does three
        /// jobs.</b> It seals the picture — measured 0 of its 92 balls on the outside, where the first draft
        /// showed 12 at the leaflet tips, any one of which is a green ball away from dropping the payoff before it
        /// has been seen. It tapers the frond for free, since the rim narrows towards the crown and the floor, so
        /// the leaflets shorten exactly where a real frond's do. And it makes the bitmap safe to redraw: whatever
        /// is drawn, the stone wins at the surface.
        /// </para>
        /// <para>
        /// The stem is in columns 4 and 5 of <b>every</b> row, so the frond is one connected group reaching the
        /// anchor layer — measured, keeping only the frond leaves 92 balls with 0 falling, and the anchor layer is
        /// 29 cells in four colours (9 dark, 9 sandstone, 5 blue, 6 fern).
        /// </para>
        /// <para>
        /// Stone in 3×3×3 blocks — <see cref="Lean"/>'s rule, the one colouring in the pack that reads as a
        /// <b>built</b> thing rather than a turned one, which is what rubble wants. A hashed <see cref="Scatter"/>
        /// was the first try and it is the wrong tool here: three colours over 3×3 blocks percolate, and one of
        /// them came out as a single connected group of 190 — a <b>33 %</b> single shot, measured.
        /// </para>
        /// <para>
        /// Measured: 562 balls, margin 3, nothing alone, 2 in pairs, nothing recoloured, best single shots 51
        /// (9 %), 72 (12 %), 80 (14 %) and the frond's own 92 (16 %). A picture is one group by construction, and
        /// on a level whose point is being recognised its coming away in one piece is the reward — the shipped
        /// pictures sit at 40 to 52 % for the same reason. <b>The block rule does not make 27-ball blocks</b>, and
        /// that is worth knowing before the budget is retuned: constant values of <c>(x/3)+(z/3)+(i/3)</c>
        /// percolate along the <c>(+x, −level)</c> diagonal, because a cell's cross-level neighbours are the
        /// <c>{0,+1}²</c> pair on odd levels and <c>{−1,0}²</c> on even ones, so the stone comes out as 15 groups
        /// of 51 to 80 rather than dozens of 27. Sixteen standing groups in all, on 36 shots.
        /// </para>
        /// </summary>
        private static Design Fossil() => new()
        {
            File = "Fossil.json",
            Name = "Fossil",
            Grid = FOSSIL_GRID,
            Depth = FOSSIL_DEPTH,
            Scene = new CavernSceneConfig(),
            Sky = 13,
            Music = MUSIC_REVEAL,
            //The slowest ceiling in the block, because this is the one level whose payoff has to be LOOKED at to
            //be got: the picture is the reward and it arrives late. Fifteen stone groups and the frond, so 36
            //shots is a little over two a group.
            Shots = 36,
            CeilingStep = 9,
            OccupiedBlock = (x, z, i, depth) => FossilRock(x, z, i),
            BlockColour = (x, z, i) => FossilFern(x, z, i)
                ? BallType.Type2
                : Band((x / 3) + (z / 3) + (i / 3), new[] { BallType.Type8, BallType.Type3, BallType.Type4 }),
        };

        /// <summary>
        /// A lopsided fruit with an <b>off-centre stone</b> in it: red and magenta peel in eight vertical strips,
        /// gold and green flesh in eight more half a strip out of phase, and a flat pale stone hanging off the axis
        /// on its own stalk. It is #161's own word made into a level. Three acts, and the middle one is the point
        /// — <b>the flesh cannot be touched until the peel is broken</b>, so the level plays peel, eat, then the
        /// stone.
        /// <para>
        /// <b>It hangs from its widest section and that is the whole answer to the anchor rule.</b> The profile is
        /// widest at the glass and tapers to a blunt nose (<see cref="MANGO_DROP"/> is where it would close to
        /// nothing, well below where the layout ends, which is what leaves the nose six cells across instead of a
        /// point with nothing to touch). So the anchor layer is a full cross-section of the fruit: measured 66
        /// cells carrying all five colours — 18 red, 18 magenta, 14 gold, 10 green, 6 stone — where Onion hangs
        /// 959 balls off 16 cells in two colours. No colouring trick is needed on top of it.
        /// </para>
        /// <para>
        /// <b>The stone hangs on a stalk of its own colour</b>, up the same off-centre column to the glass.
        /// Measured, keeping only the stone leaves 52 balls with 0 falling: the peel and the flesh can both go and
        /// it is still hanging, which is the property the family's original does not have (keep only Onion's green
        /// heart and all 161 balls of it fall).
        /// </para>
        /// <para>
        /// <b>The peel is tested before the stone and that is load-bearing.</b> A cell within
        /// <see cref="MANGO_SKIN"/> of the surface is peel whatever the stone's numbers say, so the stone cannot
        /// break the surface however it is retuned — measured 0 of its 52 balls on the outside. Where it would poke
        /// out it is simply clipped, which is what a real stone lying against the belly of a mango looks like. The
        /// 8 flesh balls of 165 that ARE reachable at the start are the lattice rounding the peel thin on the
        /// flanks, and they are the level's only early foothold.
        /// </para>
        /// <para>
        /// Strips rather than shells for the 90 % rule: a shell wrapping a body is one group, which is what cost
        /// Onion 604 balls in one before its boundary was made to swing. Eight strips of two colours means
        /// neighbours never agree. Measured: 530 balls, margin 2, nothing alone, nothing in a pair, 2 recoloured,
        /// thirteen standing groups from 3 to 81, and best single shots 41 (7 %), 45 (8 %), 52 (9 %), 78 (14 %)
        /// and 81 (15 %). The two flesh figures are the higher ones because the wedges all meet on the axis above
        /// and below the stone, so each flesh colour is one piece — the stone standing off-centre is what keeps it
        /// to two rather than one.
        /// </para>
        /// </summary>
        private static Design Mango() => new()
        {
            File = "Mango.json",
            Name = "Mango",
            Grid = 15,
            //Ten, so the fruit is wider than it is tall and the field keeps six levels of growth room under it -
            //which is also the descent room: five ceiling steps at 0.6 each against the 4.79 world units the
            //lowest ball starts above the death line.
            Depth = 10,
            Scene = new CavernSceneConfig(),
            Sky = 13,
            Music = MUSIC_REVEAL,
            //Five colours is the hardest draw in the block and this is its last level. A clean clear is 18 to 22
            //shots, so 36 puts four stars at about 0.6 of the budget.
            Shots = 36,
            CeilingStep = 7,
            Occupied = (r, ang, i, depth) => r <= MangoRim(ang, i, depth),
            Colour = MangoInside,
        };

        /// <summary>
        /// A drum of coloured panes with a <b>shaft up the middle that nothing outside it shows</b>: solid at both
        /// ends, hollow between, so it reads as a whole vessel until the bottom plug goes and the drain opens up
        /// under it. <see cref="Crown"/> does its hole in the open and the player aims around it from the first
        /// shot; hiding it makes the hole a reward — and, the part worth having, <b>a new line of fire</b>: with
        /// the bore open, a shot up the axis reaches the INSIDE of the top plug, which is the layer bonded to the
        /// glass. It is the block's one reveal whose payoff is not a thing but an absence.
        /// <para>
        /// <b>The plugs carry a colour of their own, and that is the mechanic.</b> Magenta is in the two plugs and
        /// nowhere else, so it leaves the draw when the plugs do, and half of it is in the TOP plug, which cannot
        /// be touched until the shaft is open. The level therefore keeps handing the player a colour whose only
        /// remaining home is up the bore, which is what makes the reveal change the magazine and not just the
        /// view. Blue is in both the plugs and the panes on purpose, so the bottom plug's other half comes away
        /// with the wall rather than having to be picked out of it.
        /// </para>
        /// <para>
        /// <b>The plugs are two levels each, and the first draft's one level was a tax rather than a mechanic.</b>
        /// At one level the plugs were 20 balls of 417 in groups of 4, 4, 4, 4, 4, 2 and 2 — a sixth of the draw
        /// for 4 % of the cluster, on the longest budget in the block, which is exactly the pattern
        /// <see cref="Helix"/>'s own remarks reject in writing for its rungs. Two levels doubles the plugs without
        /// touching the wall or the palette, at the cost of two levels of bore. Measured after: magenta is 40 balls
        /// in groups of 10 rather than 20 in groups of 4, which is a colour worth drawing.
        /// </para>
        /// <para>
        /// Measured: 454 balls, margin 2, nothing alone, nothing in a pair, 2 recoloured, and the <b>flattest
        /// colour spread in the pack</b> — the largest standing group of any colour is 29 and every other one is
        /// 14 or less, so best single shots are 3 %, 6 %, 3 %, 2 % and 3 %. There is no plate anywhere on it, which
        /// is why it takes the block's longest budget and closes it.
        /// </para>
        /// <para>
        /// The wall is <b>panes, not staves</b>: six wedges rolled by the course, which is what keeps a
        /// two-cell-thick tube from being six groups of seventy. <b>The roll is 2 and the palette is four, and
        /// neither is free.</b> A cell's cross-level neighbours sit half a cell away in x and z, so the wedge one
        /// along and the course one up TOUCH — with a roll of ±1 that neighbour is the same colour and the panes
        /// fuse into a helix. Measured on the three-colour first cut: 460 wall balls in about eight groups, two of
        /// them 78. A roll of 2 against four colours differs by 1 or 3 in every direction that touches and repeats
        /// only at the opposite wedge and two courses away, neither of which is adjacent.
        /// </para>
        /// </summary>
        private static Design Lantern() => new()
        {
            File = "Lantern.json",
            Name = "Lantern",
            Grid = 13,
            Depth = 10,
            FieldLevels = REVEAL_FIELD_LEVELS,
            Scene = new CavernSceneConfig(),
            Sky = 13,
            Music = MUSIC_REVEAL,
            //The finest grain in the block - panes of a dozen rather than plates - so the longest budget of the
            //five, and it closes the block on it
            Shots = 58,
            CeilingStep = 7,
            //A tube, plugged at both ends. The bore is the reveal and the caps are what hide it.
            Occupied = (r, ang, i, depth) => r <= LANTERN_OUTER
                && (r >= LANTERN_BORE || i < LANTERN_CAP || i >= depth - LANTERN_CAP),
            //Panes on the wall, diced plugs at the ends. The plug palette is deliberately NOT the wall's:
            //magenta lives only there, so clearing the plugs takes a colour out of the draw.
            Colour = (r, ang, i, depth) => r >= LANTERN_BORE
                ? Band(SectorIndex(ang, 0f, LANTERN_PANES) + 2 * (i / LANTERN_COURSE),
                       new[] { BallType.Type7, BallType.Type1, BallType.Type5, BallType.Type3 })
                : BandPolar(r, ang, i / 2, new[] { BallType.Type3, BallType.Type6 }),
        };

        #endregion

        #region The tall levels (#160)

        //Column was the only tall level in the pack, and #160's complaint about adding more was specific: "not
        //just Column stretched differently". These three are each a different KIND of tall, and what separates
        //them is not the silhouette but what the height does to the PLAY:
        //
        //  Horn  - the level gets BIGGER as it descends. The mass is back-loaded, so the budget is spent on a
        //          stalk and then the bell arrives.
        //  Helix - the level is OPEN and turns, so its width is a function of height and the player can read a
        //          whole turn of what is coming.
        //  Lean  - the level WALKS SIDEWAYS out of the middle of the frame, so the gun has to follow it.
        //
        //All three are 24 or 20 levels deep in a field of 34 or 30 against the 18 the camera frames
        //(GameplayScreen.FRAMED_LEVELS), so the top is out of shot at the start on all of them.

        /// <summary>
        /// A needle hanging point-down that opens into a bell out of shot. The <b>flare is quadratic</b> —
        /// <see cref="HORN_FLARE"/> times the level index squared — which is the whole design in one number:
        /// the lowest levels barely widen at all and the top ones carry most of the cluster, so the level does
        /// not get <i>longer</i> as the glass hands it down, it gets <i>bigger</i>. A linear taper reaches the
        /// same width having spent half of it on the middle, where it reads as a plain cone.
        /// <para>
        /// <b>It has to widen upwards, not downwards.</b> The layout hangs off the field's top level, so a cone
        /// tapering to its point up there would stand every ball in the level behind the few cells of the tip —
        /// and whatever colour that tip is takes the lot on one shot.
        /// </para>
        /// <para>
        /// Coloured by <see cref="HornShell"/>, which rings each level by <b>its own</b> rim rather than by an
        /// absolute radius — <see cref="OnionShell"/>'s trick, needed here for a second reason. An absolute ring
        /// would put the outermost colour only on the top few levels, and the magazine draws evenly among the
        /// colours still <i>alive</i>: a third of the level's shots would be unspendable until the mouth came
        /// into reach. Ringed by its own rim, the narrow stalk carries all three colours in the same proportions
        /// the mouth does, and each shell is one piece from the point to the glass, so peeling any one off never
        /// stands the other two on nothing.
        /// </para>
        /// <para>
        /// Measured: 506 balls, margin 2, nothing standing alone or in a pair, 12 recoloured by the repair pass
        /// (all in the stalk, where a shell boundary falls between two rows of a nine-cell level — the rim
        /// artefact that pass exists for), best single shots 23 %, 30 % and 37 %. If that repair count ever grows
        /// past about twenty the tip has gone too narrow for three shells and <see cref="HORN_TIP"/> is the
        /// number to raise, not the boundaries.
        /// </para>
        /// </summary>
        private static Design Horn() => new()
        {
            File = "Horn.json",
            Name = "Horn",
            //Thirteen for a rim reaching 4.67 at the mouth, which leaves the free column LateralMargin asks for
            Grid = 13,
            Depth = 20,
            //Ten levels of growth room under the layout, as Column has. Eight layout levels sit inside the
            //camera's window at the start, and those eight are the stalk - the bell is the part nobody has seen.
            FieldLevels = 30,
            Scene = new MountainSceneConfig(),
            Sky = 8,
            Music = MUSIC_TOWER,
            Shots = 80,
            CeilingStep = 6,
            Occupied = (r, ang, i, depth) => r <= HornRim(i),
            Colour = (r, ang, i, depth) => HornShell(r, i),
        };

        /// <summary>
        /// Two strands winding around a common axis, tied by a rung every <see cref="HELIX_RUNG_EVERY"/>th
        /// level — a double helix, and the one shape in the pack whose <b>width</b> is a function of height:
        /// nine cells across where the strands stand side by side, three where one is directly behind the
        /// other, and a little over one whole turn from the glass to the floor. What that buys is the thing a
        /// tall level is for — the player can see a full turn of what is descending and knows which strand will
        /// be facing them when it arrives.
        /// <para>
        /// <b>The rungs are why this is a level and not a pair of falling ribbons.</b> A strand is bonded to the
        /// glass at its own top cell and nowhere else, so a colour cut anywhere along it would drop everything
        /// below the cut — the whole strand, on one shot near the top. With a rung every fourth level each
        /// strand also hangs off the other, and the worst group anywhere is a good cascade rather than a level
        /// ending.
        /// </para>
        /// <para>
        /// <b>A rung has no colour of its own, deliberately.</b> Given one it would be a handful of groups of
        /// four or five balls apiece, and the magazine draws evenly among the colours still standing — so a
        /// quarter of every shot would be spent on a colour worth five balls a hit, which is not a difficulty,
        /// it is a tax. Each half of a rung takes the colour of the strand it reaches from instead, so a rung
        /// reads as the two strands touching, which is exactly what it is.
        /// </para>
        /// <para>
        /// Segments of <see cref="HELIX_SEGMENT"/> levels rather than a whole strand in one colour: a strand's
        /// own colour would be a group of about 200, near half the level on one ball. The two strands are offset
        /// by two palette entries so the halves of every rung differ.
        /// </para>
        /// <para>
        /// Measured: 438 balls, margin 2, and the cleanest report in the pack — <b>nothing alone, nothing in a
        /// pair and nothing recoloured at all</b>, because the colouring only ever changes at a segment boundary
        /// or across the axis and both are crossings between blocks of dozens. Colour counts 105/107/112/114,
        /// best single shots 6 %, 6 %, 7 % and 12 %. Thinning <see cref="HELIX_STRAND"/> is what would end that:
        /// at 1.5 a strand drops to four cells on the unshifted levels against nine on the shifted ones, which
        /// pinches every other level and starts stranding rim cells. It is also the lightest tall level here, so
        /// the figure to watch in play is the opposite one — whether 66 shots is a loose budget for 438 balls.
        /// </para>
        /// </summary>
        private static Design Helix() => new()
        {
            File = "Helix.json",
            Name = "Helix",
            Grid = 13,
            Depth = 24,
            FieldLevels = 34,
            Scene = new MountainSceneConfig(),
            Sky = 8,
            Music = MUSIC_TOWER,
            Shots = 66,
            CeilingStep = 5,
            Occupied = (r, ang, i, depth) => HelixStrand(r, ang, i) != 0 || HelixRung(r, ang, i, depth),
            Colour = (r, ang, i, depth) =>
            {
                int strand = HelixStrand(r, ang, i);

                //A rung cell outside both strands: it takes the colour of the strand it reaches from, so the
                //two halves meet at the axis in different colours and neither is a group of its own
                if (strand == 0)
                {
                    Untwist(r, ang, i * HELIX_TURNS_PER_LEVEL, out float along, out _);
                    strand = along >= 0f ? 1 : -1;
                }

                return Band(i / HELIX_SEGMENT + (strand > 0 ? 0 : 2),
                    new[] { BallType.Type1, BallType.Type7, BallType.Type3, BallType.Type5 });
            },
        };

        /// <summary>
        /// A round tower that <b>leans</b>: its axis walks <see cref="LEAN_PER_LEVEL"/> of a cell along X for
        /// every level it climbs, so over twenty-four levels it steps five and a bit cells sideways — a shade
        /// over one full width, where the tower at Pisa manages four degrees. It is the one tall level the gun
        /// has to <i>follow</i>: X runs across the screen (the gun starts at +Z looking at the origin), so the
        /// lean is on the axis the player can actually see, and the column walks out of the middle of the frame
        /// as the glass hands it down.
        /// <para>
        /// Drawn on the <b>raw lattice indices</b> and not on the emitter's centred radius, which is what a
        /// leaning shape needs: the centre it is measured from moves per level, so the polar pair the emitter
        /// offers is the wrong frame. <see cref="LeanRadius"/> rebuilds the emitter's own <c>dx</c>/<c>dz</c>
        /// around the drifted centre — and it may take the shifted-level offset straight off the <i>layout</i>
        /// index because <see cref="Emit"/> refuses an odd layout offset, so a layout level and its field level
        /// always agree in parity.
        /// </para>
        /// <para>
        /// Coloured in 3×3×3 blocks of masonry — <see cref="Mosaic"/>'s rule, and the only colouring already in
        /// the pack that reads as a <b>built</b> thing rather than a turned one, which is what a leaning tower
        /// wants. Blocks put all three colours on the anchor layer, the rule every design here answers, and 27
        /// cells is the size that keeps a group worth shooting at over twenty-four levels: the same blocks two
        /// levels tall rather than three make a tower this long a grind.
        /// </para>
        /// <para>
        /// Measured: 515 balls, best single shots 9 %, 20 % and 27 %, 4 balls standing in pairs and 7 recoloured
        /// — blocks clipped by the drifting rim, which is the repair pass's own remit (Prism ships with 4 in
        /// pairs and Static with 14). A couple of dozen recoloured would mean the block size and the drift are
        /// fighting each other, and the fix then is a block size that divides the drift rather than a bigger pass.
        /// </para>
        /// <para>
        /// <b>Margin 1 — the tightest in the pack, and the lean is the reason.</b> The tower's envelope is ten
        /// cells across X against five in Z, so the field is square around a shape that is not. One free column
        /// is what <see cref="LateralMargin"/> asks for and it is enough (it gives every flank ball a lateral
        /// neighbour to offer, which is the whole of what the trap needs), and Gem, One and Star all ship at 1 —
        /// but this is the one design reaching for the wall over twenty-four levels rather than six, so it is the
        /// first place to look if a shot is ever reported bouncing off a flank. <see cref="LEAN_GRID"/> at 15 is
        /// the lever, and it costs a wider glass plate and a longer camera stand-off.
        /// </para>
        /// </summary>
        private static Design Lean() => new()
        {
            File = "Lean.json",
            Name = "Lean",
            Grid = LEAN_GRID,
            Depth = 24,
            FieldLevels = 34,
            Scene = new MountainSceneConfig(),
            Sky = 8,
            Music = MUSIC_TOWER,
            Shots = 78,
            CeilingStep = 6,
            OccupiedBlock = (x, z, i, depth) => LeanRadius(x, z, i, depth) <= LEAN_RADIUS,
            BlockColour = (x, z, i) => Band((x / 3) + (z / 3) + (i / 3),
                new[] { BallType.Type1, BallType.Type4, BallType.Type3 }),
        };

        #endregion

        #endregion

        #region Colour helpers

        //Concentric shells one and a bit cells thick: thick enough that a ring is a solid band of colour
        //rather than a dotted circle once the lattice has rounded it off.
        private static BallType Ring(float r, BallType[] palette) =>
            palette[(int)MathF.Floor(r / 1.9f) % palette.Length];

        private static BallType Band(int band, BallType[] palette) => palette[band % palette.Length];

        #region Pictures (#130)

        //A level that reads as a PICTURE rather than as a solid of revolution. It is a flat wall hanging in
        //the field with a symbol drawn across it, and four facts about the lattice decide how one is drawn.
        //
        //THE PICTURE PLANE IS (x, LEVEL), and the wall is thin in Z. The gun starts at +Z looking at the
        //origin (Cannon.CalculateInitialPositionAndAimTarget), so X runs across the screen and the level axis
        //runs up it: a wall spanning those two is the one the player sees face-on. Spanning X and Z instead
        //would draw the picture on the floor, seen edge-on from the gun.
        //
        //IT IS TWO CELLS THICK, not one. A cell touches its four orthogonal neighbours on its own level and
        //up to four on each adjacent one, and WHICH diagonal offsets those are depends on the level's parity
        //- so a wall one cell thick in Z has half its vertical neighbours reaching to a Z that is not there.
        //Two cells thick is the thinnest wall that is a solid slab whatever the parity, and it doubles every
        //group, which is what keeps the strokes above the lonely-ball floor.
        //
        //ROWS ARE SHORTER THAN COLUMNS ARE WIDE. Levels sit 1/sqrt(2) apart vertically against a cell pitch
        //of 1 horizontally, so a bitmap drawn square comes out squashed to 71% of its height. A picture is
        //therefore drawn about 1.4x TALLER in rows than it is meant to look - the hearts below are 14 rows
        //for 11 columns and come out very nearly square.
        //
        //THE BACKGROUND CANNOT BE ONE COLOUR. The wall's top row is the anchor layer, and a background that
        //is a single colour makes that row a single group holding the whole picture up: one matching ball
        //takes the level (Validate's drop test, the trap Mosaic and Gem both hit first). The background is a
        //2x2 check of two colours for exactly the reason Mosaic's blocks are.
        //
        //HOW MANY INKS THE SYMBOL IS DRAWN IN IS THE DIFFICULTY DIAL - AND THE BACKGROUND'S PALETTE IS NOT.
        //That asymmetry is the whole finding of the Gallery block (#194) and it cost two rounds of screenshots.
        //
        //The SYMBOL side works as expected. A symbol is one connected group by construction, so a drawing in ONE
        //ink is the biggest group on its wall and its own colour takes half the level in a single shot: the three
        //gentle pictures hold three of the pack's highest one-shot percentages (Star 40 %, Heart 42 %,
        //Smiley 52 %), which is deliberate on a level whose point is being recognised. The same drawing split
        //across more inks has no single payoff at all - the elephant's face takes 21 % and the zebra's stripes
        //9 %, where either outline in one ink would have taken half the wall. That is a real difficulty dial and
        //it costs nothing.
        //
        //The BACKGROUND side looked like the same dial from the other side and is not. More ground colours does
        //quarter every background group and does thin the magazine's draw - and it also DESTROYS THE PICTURE.
        //Measured by looking: the zebra over a three-colour warm check and the elephant over a four-colour one
        //were both unreadable, because a symbol is drawn in the palette's least saturated colours (a zebra is
        //black and white) and three or four saturated hues behind it mean the eye reads the check and not the
        //shape. Both were fixed the same way - a QUIET two-colour ground, cool behind the zebra so it is close in
        //hue and luminance, warm behind the cool elephant so the contrast is hue rather than noise - and the
        //difficulty moved onto the symbol's inks, where it belongs.
        //
        //So: two background colours, always, and never one (see above). A picture level's difficulty is drawn in
        //the symbol.
        //
        //A four-ink SCENE was tried too and dropped: a flat-crowned acacia with a low sun behind it and a band of
        //ground under both, spending all four inks on a crown, a trunk, a sun and the ground. It passed every gate
        //comfortably (420 balls, the flattest colour spread of any picture at 14 %) and it did not read - a wide
        //flat crown, a sun and a ground band are three horizontal bands, and horizontal bands at this scale read
        //as bands rather than as a landscape. The lesson is the pictures' own: a wall of 15 by 14 cells carries a
        //SYMBOL, not a composition.

        /// <summary>
        /// Where a picture's own <c>(column, row)</c> sits in the lattice, and whether a cell is on the wall
        /// at all. Row 0 is the TOP of the picture, which is layout level <c>depth - 1</c> — the anchor layer,
        /// so a bitmap is written the way it is seen, top line first.
        /// </summary>
        private static bool OnWall(int x, int z, int i, int depth, int width, int grid, out int column, out int row)
        {
            //Centred across the grid, and two cells deep about the middle of it
            int x0 = (grid - width) / 2;
            int z0 = (grid - PICTURE_THICKNESS) / 2;

            column = x - x0;
            row = depth - 1 - i;

            return column >= 0 && column < width && z >= z0 && z < z0 + PICTURE_THICKNESS;
        }

        /// <summary>How deep a picture wall is, in cells. See the region's remarks for why it is not one.</summary>
        private const int PICTURE_THICKNESS = 2;

        /// <summary>
        /// The characters a bitmap draws its symbol with, <b>in palette order</b>: <c>#</c> is the first ink,
        /// <c>o</c> the second, <c>+</c> the third, <c>=</c> the fourth, and anything else is background. It is
        /// the contract between a bitmap and the palette <see cref="Picture"/> is handed, so it is stated once
        /// here rather than as a chain of conditionals that grows an <c>else</c> per ink.
        /// <para>
        /// Four characters in the alphabet, of which the pack uses three (<see cref="ELEPHANT"/>'s face, ears and
        /// trunk). What bounds a picture is not this string but <b>its own palette's length</b> — a glyph past the
        /// end of that palette is refused rather than quietly drawn as background — so the fourth costs nothing
        /// and is kept. The one design that spent all four was a drawn <i>scene</i> and it was dropped for not
        /// reading at this scale; the region's remarks carry that measurement, so the next author does not repeat
        /// it.
        /// </para>
        /// </summary>
        private const string SYMBOL_INK = "#o+=";

        /// <summary>
        /// Reads a bitmap written as text — the <see cref="SYMBOL_INK"/> characters are the symbol's inks and
        /// anything else is background. Rows are top-first, so the array reads in source exactly as the level
        /// reads in the game, which is the whole reason for spelling a picture out rather than solving it from a
        /// formula: a mistake in it is visible in the diff.
        /// </summary>
        private static char PixelAt(string[] bitmap, int column, int row) =>
            row >= 0 && row < bitmap.Length && column >= 0 && column < bitmap[row].Length
                ? bitmap[row][column]
                : '.';

        /// <summary>
        /// A heart, 13 columns by 14 rows. Two rules shape it, and the drop test taught the second one.
        /// <list type="bullet">
        /// <item><b>Every stroke is at least two cells wide</b> in both directions — the lonely-ball rule (a
        /// one-cell diagonal run is a string of balls that touch nothing at all), which a drawn symbol walks
        /// straight into wherever it curves or comes to a point. The first draft ended in a one-cell tip.</item>
        /// <item><b>The symbol never reaches an edge, and the top two rows are background.</b> The first
        /// draft filled the wall's full width and touched its top row, which made the heart one connected
        /// group of 208 holding everything under it up: dropping it took <b>93 % of the level in one shot</b>.
        /// Background down both sides and across the top is what keeps the wall hanging when the symbol goes,
        /// and it is the same shape of trap the background's own check answers from the other side.</item>
        /// </list>
        /// </summary>
        private static readonly string[] HEART =
        {
            ".............",
            ".............",
            "...##...##...",
            "..####.####..",
            "..#########..",
            "..#########..",
            "..#########..",
            "..#########..",
            "...#######...",
            "...#######...",
            "....#####....",
            "....#####....",
            ".....###.....",
            ".....###.....",
        };

        /// <summary>
        /// A smiley, 13 by 14. The eyes and the smile are the <c>o</c> accent, drawn <b>inside</b> the face
        /// rather than cut out of it — a hole in the face would be background, and background enclosed by the
        /// symbol is a pocket the wall's own background cannot reach.
        /// <para>
        /// Both features are two cells wide everywhere for the lonely-ball rule, the smile's turned-up ends
        /// included: one-cell corners would be a pair apiece, which stands but asks the player for two landed
        /// balls to clear rather than one.
        /// </para>
        /// </summary>
        private static readonly string[] SMILEY =
        {
            ".............",
            ".............",
            "....#####....",
            "...#######...",
            "..#########..",
            "..##oo#oo##..",
            "..##oo#oo##..",
            "..#########..",
            "..#########..",
            "..#oo###oo#..",
            "..##ooooo##..",
            "..#########..",
            "...#######...",
            "....#####....",
        };

        /// <summary>
        /// A five-pointed star, 13 by 14: the point up, the arms across, and two legs under it. The hardest
        /// of the three to keep above the lonely-ball floor — a star is nothing but places where the shape
        /// comes to a point — so every arm and leg is two cells wide and the tip is three.
        /// </summary>
        private static readonly string[] STAR =
        {
            ".............",
            ".............",
            ".....###.....",
            ".....###.....",
            "....#####....",
            "..#########..",
            "..#########..",
            "...#######...",
            "....#####....",
            "....#####....",
            "....##.##....",
            "...##...##...",
            "...##...##...",
            "..##.....##..",
        };

        /// <summary>
        /// A zebra, 15 columns by 14 rows: the body across the middle, the neck and head rising to the right,
        /// two stout legs under it. <b>The two inks alternate every two columns across the whole drawing</b>
        /// rather than following its outline, which is what makes the stripes read as stripes and the legs come
        /// out half of each. Nothing here is narrower than two columns except where a band crosses a leg, and a
        /// one-column run inside a solid body is still four rows tall and two cells deep — the lonely-ball rule
        /// is about a diagonal run of single cells, which this bitmap has none of.
        /// <para>
        /// The animal keeps a clear column of background down each side and two clear rows across the top, for
        /// the reason <see cref="HEART"/> states: a symbol touching the anchor row holds the whole wall up.
        /// </para>
        /// </summary>
        private static readonly string[] ZEBRA =
        {
            "...............",
            "...............",
            ".........o##o..",
            "........oo##oo.",
            "........oo##oo.",
            ".......#oo##o..",
            "..##oo##oo##...",
            "..##oo##oo##o..",
            "..##oo##oo##o..",
            "...#oo##oo##...",
            "...#o....o#....",
            "...#o....o#....",
            "...#o....o#....",
            "...#o....o#....",
        };


        #endregion

        /// <summary>
        /// The square (Chebyshev) half-extent of a point the emitter hands over in polar form — the distance
        /// a <b>square</b> shape measures in, as opposed to <see cref="Ring"/>'s round one. Recovered from
        /// the pair rather than passed as a third argument: every design but the slab wants the round radius,
        /// and a shape function taking both would have to ignore one of them at every call site.
        /// </summary>
        private static float Chebyshev(float r, float ang) =>
            r * MathF.Max(MathF.Abs(MathF.Cos(ang)), MathF.Abs(MathF.Sin(ang)));

        /// <summary>
        /// A colour per block that looks unpatterned but is a pure function of the block's coordinates, so a
        /// level is the same every time it is played. An integer hash rather than a <see cref="Random"/>
        /// walked in loop order: the walk's order is an implementation detail of the emitter, and a layout
        /// that changes when that loop is reordered is a layout nobody can reason about.
        /// <para>
        /// Deliberately NOT anti-clustered. Two neighbouring blocks that happen to agree merge into a group
        /// of eight, and those accidents are the level's only breathing room — a scatter forced to alternate
        /// would be uniformly four everywhere, which is a grind rather than a difficulty.
        /// </para>
        /// </summary>
        private static BallType Scatter(int blockX, int blockZ, int level, BallType[] palette)
        {
            //Odd multipliers well apart in magnitude, then a couple of xorshift rounds: enough mixing that
            //neighbouring blocks land on unrelated colours, and cheap enough not to matter at generation time
            uint h = (uint)(blockX * 73856093 ^ blockZ * 19349663 ^ level * 83492791);
            h ^= h >> 13;
            h *= 2654435761;
            h ^= h >> 16;

            return palette[h % (uint)palette.Length];
        }

        //Angular wedges. twist shears the boundary with radius, which is what turns a cross into a spiral.
        private static BallType Sector(float ang, float twist, int sectors, BallType[] palette) =>
            palette[SectorIndex(ang, twist, sectors) % palette.Length];

        /// <summary>
        /// Which wedge a point is in, 0 to <paramref name="sectors"/> − 1. Split out of <see cref="Sector"/>,
        /// whose behaviour is unchanged, because a wedge index is also a <i>term</i> a design can add something
        /// to: <see cref="Lantern"/> rolls the palette by the course as well as by the wedge, which turns six
        /// vertical staves of dozens into panes of a dozen without a horizontal band anywhere.
        /// </summary>
        private static int SectorIndex(float ang, float twist, int sectors)
        {
            float turns = (ang / MathF.Tau) + 0.5f + twist;      //0..1 around the disc, plus the shear
            int index = (int)MathF.Floor(turns * sectors);
            return ((index % sectors) + sectors) % sectors;      //MathF.Floor of a negative turns
        }

        /// <summary>
        /// The 2×2 block a cell belongs to, recovered from the polar pair a <see cref="Design.Colour"/> delegate
        /// is handed — the sibling of <see cref="Chebyshev"/>, and what lets ONE colour rule change its own KIND
        /// at a radius: staves or shells outside, lattice-aligned blocks inside.
        /// <para>
        /// It is needed because the emitter hands exactly one colour delegate the frame it asked for and the three
        /// frames are exclusive, so a design that wants blocks <i>and</i> a radius has to derive one from the
        /// other. The polar pair is the lossless direction — <c>r·cos(ang)</c> and <c>r·sin(ang)</c> are the very
        /// <c>dx</c> and <c>dz</c> the emitter built them from — where a radius cannot be got out of
        /// <see cref="Design.BlockColour"/>'s raw indices without a second copy of the centring arithmetic.
        /// </para>
        /// <para>
        /// <b>The quarter-cell bias is load-bearing.</b> A block boundary lands where the halved coordinate is a
        /// whole number, and the lattice puts <c>dx</c> on a whole cell on the shifted (odd) levels and on a half
        /// cell on the unshifted ones — so an unbiased <c>floor(dx/2)</c> has a boundary exactly ON the cells of
        /// every odd level, where the float noise in <c>r·cos(ang)</c> decides which block a ball is in. Biased by
        /// a quarter, no boundary can coincide with either parity and the nearest one is an eighth of a block away.
        /// </para>
        /// </summary>
        private static void PolarBlock(float r, float ang, out int blockX, out int blockZ)
        {
            blockX = (int)MathF.Floor((r * MathF.Cos(ang) - BLOCK_BIAS) * HALF);
            blockZ = (int)MathF.Floor((r * MathF.Sin(ang) - BLOCK_BIAS) * HALF);
        }

        //A quarter of a cell: the one offset no cell of either level parity can land on. See PolarBlock.
        private const float BLOCK_BIAS = 0.25f;

        /// <summary>
        /// <see cref="Mosaic"/>'s diagonal dice — <see cref="Band"/> over the sum of <see cref="PolarBlock"/>'s
        /// block indices and the block level — for a design whose colour rule is a function of the radius.
        /// <para>
        /// It exists because <see cref="Scatter"/> <b>percolates below five colours</b>, which is the one thing
        /// about the pack's chunky colourings that is invisible until it is measured. A hashed dice puts a block
        /// in a palette entry with probability 1/n and this lattice gives a cell up to twelve neighbours, so at
        /// n = 3 or 4 the same-colour blocks join into a giant component: measured, a three-colour scatter over a
        /// hollow core came out as ONE fused group of 105 — 18 % of the level, on the region that was supposed to
        /// be the fine-grained half of it. <see cref="Prism"/> (5) and <see cref="Static"/> (6) are above the
        /// threshold and are the two designs <see cref="Scatter"/> is right for. Banded on the block coordinates
        /// instead, no two touching blocks can agree at all, so a group is one block and the grain is the grain
        /// that was authored.
        /// </para>
        /// <para>
        /// Normalised before <see cref="Band"/> sees it: these block indices are CENTRED and go negative, and
        /// <c>Band</c> indexes with a bare <c>%</c> — Mosaic hands it raw lattice indices, which cannot.
        /// </para>
        /// </summary>
        private static BallType BandPolar(float r, float ang, int blockLevel, BallType[] palette)
        {
            PolarBlock(r, ang, out int blockX, out int blockZ);
            return Band(((blockX + blockZ + blockLevel) % palette.Length + palette.Length) % palette.Length, palette);
        }

        //The onion's own geometry. See Onion() for why the two distances differ.
        private const float ONION_RADIUS = 5.5f;
        private const float INV_SQRT_TWO = 0.70710678f;

        //Where the two colour boundaries sit, as a share of the level's own radius, and how far the outer one
        //swings with the angle around the axis. See OnionShell for why the swing exists and what it has to
        //cross at both ends to work.
        private const float ONION_HEART = 0.28f;
        private const float ONION_BULK = 0.60f;
        private const float ONION_STAVES = 6f;
        private const float ONION_SWING = 0.50f;

        //BOTH boundaries swing, and the inner one has to go negative at the trough or the white simply wraps
        //around the heart and is one piece again: an angular gap a couple of cells wide out at the rim is
        //no gap at all by the time it has narrowed to the axis, so a rib that stops at the heart's edge
        //never actually separates anything. Measured: swinging the outer boundary alone cut the skin from
        //604 to 158 and left the white one group of 408 (42 % of the cluster).
        private const float ONION_SWING_HEART = 0.42f;

        //A level index's vertical world offset from the layout's own centre, in the same units r is
        //already in (BallsMap.GetRealPosition puts a level at Y = level / sqrt(2)).
        private static float OnionVertical(int i, int depth) => (i - (depth - 1) * HALF) * INV_SQRT_TWO;

        private static float SphereDistance(float r, int i, int depth)
        {
            float dy = OnionVertical(i, depth);
            return MathF.Sqrt(r * r + dy * dy);
        }

        /// <summary>
        /// Green heart, white bulk, yellow skin - ringed by <b>each level's own radius</b> rather than by
        /// true 3D distance from the sphere's centre. The two agree at the equator, where a level's own
        /// radius already is the sphere's, and it is everywhere else that the difference matters: a true
        /// 3D shell narrows to nothing at the poles, so whichever ring the pole's own tiny point happens to
        /// fall in becomes the ENTIRE top layer - the one bonded to the glass - and a single-colour anchor
        /// is the trap every design in this pack has to answer (see <see cref="Validate"/>). Ringed by its
        /// own radius instead, every level, however small, shows the same green-centre/white-ring/yellow-rim
        /// proportions the equator does, which is also just what a real onion's rings look like from any
        /// height - narrower near the root and stem, never absent. It is what keeps the heart's own colour
        /// standing in one piece straight up the middle from pole to pole, directly bonded to the glass at
        /// the top without ever having to pass through the white around it - and the same for the skin at
        /// its own rim - so peeling any one layer off never stands the other two on nothing.
        /// <para>
        /// <b>The boundary between the outer two SWINGS with the angle around the axis, and that is what makes
        /// this a level rather than one shot.</b> Ringed by radius alone each layer is a single connected
        /// piece from pole to pole - the skin measured 604 balls in one group, 62 % of the cluster, so the
        /// first lucky yellow ball ended it. <b>Played, three runs each, 24 shots a run with the aim spread
        /// between shots: ringed by radius alone the level was cleared every run, in 2, 4 and 8 shots of 48;
        /// with the swing, two of the three runs were still going at 24.</b> (A first attempt at that
        /// measurement spread the aim so wide that every shot missed the field outright - 24 shots, zero
        /// contacts of any kind - which any level survives. A scripted play-through is only a datum once the
        /// shots are confirmed to be landing.)
        /// <see cref="ONION_SWING"/> carries the boundary past the surface at <see cref="ONION_STAVES"/>
        /// angles and back inside the heart's own radius between them, so the two outer layers interlock as
        /// <i>staves</i>: white reaches daylight where the boundary swings out, cutting the skin there, and
        /// yellow reaches the core where it swings in, cutting the white. Each stave still runs the full
        /// height, so each is bonded to the glass on its own and the anchor rule above is untouched - and a
        /// bulb of interlocking segments is what an onion actually looks like cut across.
        /// </para>
        /// </summary>
        private static BallType OnionShell(float r, float ang, int i, int depth)
        {
            float dy = OnionVertical(i, depth);
            float capRadius = MathF.Sqrt(MathF.Max(0f, ONION_RADIUS * ONION_RADIUS - dy * dy));
            float shell = capRadius > 0f ? r / capRadius * ONION_RADIUS : 0f;

            //One swing drives both boundaries. Past 1 the white breaks the surface and cuts the skin; below 0
            //BOTH boundaries vanish and the yellow runs from rim to axis, which is the only thing that cuts
            //the white - and the heart with it, into wedges that each still stand the full height.
            float swing = MathF.Cos(ONION_STAVES * ang);
            float heart = ONION_HEART + ONION_SWING_HEART * swing;
            float bulk = ONION_BULK + ONION_SWING * swing;

            if (shell <= ONION_RADIUS * heart) return BallType.Type2; //green heart
            if (shell <= ONION_RADIUS * bulk) return BallType.Type4;  //white bulk
            return BallType.Type7;                                    //yellow skin
        }

        //THE HANGING DOMES' OWN GEOMETRY. OnionVertical measures a level's offset from the layout's own middle,
        //which is what a sphere wants; a dome hangs from the GLASS, so it measures how far a level has dropped
        //below the layout's top instead. Same 1/sqrt(2): a distance built from i and r untouched comes out
        //stretched along Y, because a level is not one lattice unit tall.
        private static float DomeDrop(int i, int depth) => (depth - 1 - i) * INV_SQRT_TWO;

        //The toadstool's own geometry. The cap's radius is set by the palette (three rings of Ring's own 1.9
        //want a rim past 3.8), the squash by the stalk (a true hemisphere of this radius would fill the layout),
        //and the stalk by the lattice (1.7 is 12 cells on an unshifted level and 9 on a shifted one).
        private const float TOADSTOOL_CAP = 5.3f;
        private const float TOADSTOOL_SQUASH = 0.68f;
        private const float TOADSTOOL_STALK = 1.7f;

        /// <summary>
        /// The dome's distance with the vertical scaled: <paramref name="stretch"/> is how many times taller than
        /// wide the body is, so 1 is a hemisphere, below 1 squashes it into a cap and above 1 draws the pole down
        /// into a teardrop. Stated as a factor rather than as a second radius because it is the one number that
        /// has to agree with <c>Depth</c>: the body reaches <c>radius * stretch</c> below the glass, and
        /// <c>(Depth - 1) / sqrt(2)</c> is how far there is to reach.
        /// </summary>
        private static float DomeDistance(float r, int i, int depth, float stretch)
        {
            float dy = DomeDrop(i, depth) / stretch;
            return MathF.Sqrt(r * r + dy * dy);
        }

        #region The reveals' own geometry (#161)

        /// <summary>
        /// The Reveal block's field depth, and the one number in it that is not about how a level LOOKS: <b>the
        /// ceiling's descent has to have somewhere to go, and the room it descends into is the EMPTY levels under
        /// the layout — not the field's size.</b>
        /// <para>
        /// A hollow or nested vessel wants ten levels of height to be one. Ten deep in the pack's ordinary
        /// sixteen-level field leaves an offset of six, and six empty levels is 4.74 world units of air under the
        /// lowest ball against a descent of 0.6 a step — eight steps, which is less pressure than
        /// <see cref="Crown"/> gets and would have left the budget as the only thing on these levels. Eighteen puts
        /// the offset back to eight and the air to 6.16, i.e. 10.3 steps.
        /// </para>
        /// <para>
        /// Eighteen and not twenty, and both facts that pin it are the game's own. <c>FRAMED_LEVELS</c> is 18, so
        /// an 18-level field is the deepest one still framed WHOLE — a twenty would start with the top two levels
        /// of the layout out of shot, which is no way to run a block whose point is a shape being uncovered. And a
        /// field this deep is RAISED off the death line rather than pinned at <c>FIELD_TOP_Y</c> (17 is the first
        /// depth raised), so the cluster hangs 1.36 higher — exactly as <c>Colossus.json</c>'s eighteen levels
        /// already do. A proven configuration, not a new one.
        /// </para>
        /// </summary>
        private const byte REVEAL_FIELD_LEVELS = 18;

        //The hopper's pit: how far the wall stands out, how wide the mouth is at the bottom level, and how much
        //the void steps in a level. 3.6 and 0.9 close the pit at i = 4, which leaves the top two levels solid -
        //that lid is what the rest of the block hangs from.
        private const float HOPPER_WALL = 4.75f;
        private const float HOPPER_MOUTH = 3.6f;
        private const float HOPPER_BENCH = 0.9f;

        //The lantern's drum. The bore is hidden by plugs of LANTERN_CAP levels at each end - two, not one: see
        //the design's own remarks for what one level cost the draw.
        private const float LANTERN_OUTER = 4.3f;
        private const float LANTERN_BORE = 2.3f;
        private const int LANTERN_CAP = 2;
        private const int LANTERN_PANES = 6;
        private const int LANTERN_COURSE = 2;

        /// <summary>
        /// How far below the <b>anchor layer</b> a level hangs, in the same world units <c>r</c> is already in.
        /// The natural vertical coordinate for a body hung from the glass by its widest section, where
        /// <see cref="OnionVertical"/>'s measure from the layout's own middle is the one for a body centred on its
        /// equator. Both are a level index times <c>1/sqrt(2)</c>, which is the spacing
        /// <c>BallsMap.GetRealPosition</c> puts between levels.
        /// </summary>
        private static float BelowGlass(int i, int depth) => (depth - 1 - i) * INV_SQRT_TWO;

        /// <summary>
        /// The emitter's own centred offsets, rebuilt from the raw lattice indices — the <c>x + shift - axis</c>
        /// line for line, for a design that has to read BOTH a shape and the lattice (see <see cref="ChestPart"/>
        /// and <see cref="LeanRadius"/> for the same rebuild). The shifted-level offset may be taken off the
        /// <i>layout</i> index because <see cref="Emit"/> refuses an odd layout offset, so a layout level and its
        /// field level always agree in parity.
        /// </summary>
        private static void Centred(int x, int z, int i, byte grid, out float dx, out float dz)
        {
            float axis = (grid - 1) * HALF + HALF;
            float shift = (i % 2) * HALF;

            dx = x + shift - axis;
            dz = z + shift - axis;
        }

        /// <summary>
        /// A boulder's radius at one height: an ellipsoid of semi-axes <paramref name="radius"/> and
        /// <paramref name="tall"/>, measured from its own middle. <paramref name="tall"/> larger than the layout
        /// is deep is the point rather than a mistake — it cuts the poles off and leaves a flat crown, which is
        /// what gives the anchor layer a disc of cells to carry several colours in.
        /// </summary>
        private static float BoulderRim(float dyc, float radius, float tall)
        {
            float taper = 1f - (dyc / tall) * (dyc / tall);
            return taper <= 0f ? 0f : radius * MathF.Sqrt(taper);
        }

        //The crate. CHEST_HALF at 3.6 is the one half-extent that comes out 8 cells on the unshifted levels and 7
        //on the shifted ones - the evenest brick bond the stagger allows, and the narrowest box whose walls can be
        //two cells thick and still leave a cavity worth hiding something in. The grid and the depth are constants
        //beside it because ChestPart needs all three, and two copies of a number are two places for it to be
        //wrong (LEAN_GRID is stated for the same reason).
        private const byte CHEST_GRID = 15;
        private const byte CHEST_DEPTH = 12;
        private const float CHEST_HALF = 3.6f;
        private const float CHEST_WALL = 2f;
        private const int CHEST_LID = 2;
        private const int CHEST_FLOOR = 2;

        //The pearl and the cord it hangs from. PEARL_DROP is measured from the glass, so the pearl sits low in
        //the box with air above it and the cord visible through the hole the player opens.
        private const float PEARL_RADIUS = 1.8f;
        private const float PEARL_DROP = 4.2f;
        private const float CORD_HALF = 1.1f;

        /// <summary>
        /// What a cell of the chest is: <c>0</c> nothing, <c>1</c> the box, <c>2</c> the pearl or its cord. One
        /// function for the shape and the colouring both, as <see cref="HelixStrand"/> is for its strands: a
        /// hollow body and its contents cannot be cut in one frame and coloured in another.
        /// </summary>
        private static int ChestPart(int x, int z, int i)
        {
            Centred(x, z, i, CHEST_GRID, out float dx, out float dz);

            float r = MathF.Sqrt(dx * dx + dz * dz);
            float d = BelowGlass(i, CHEST_DEPTH);
            float dy = d - PEARL_DROP;

            //The pearl and its cord are tested FIRST, so the lid is pierced by the cord rather than the cord
            //being clipped by the lid - which is the difference between a pearl bonded to the glass in its own
            //colour and a pearl that falls with the box
            if (r * r + dy * dy <= PEARL_RADIUS * PEARL_RADIUS) return 2;
            if (r <= CORD_HALF && d <= PEARL_DROP) return 2;

            //The square extent Chebyshev recovers from the polar pair, taken straight off the offsets
            float box = MathF.Max(MathF.Abs(dx), MathF.Abs(dz));
            if (box > CHEST_HALF) return 0;

            bool insideWalls = box <= CHEST_HALF - CHEST_WALL;
            bool underLid = i >= CHEST_FLOOR && i < CHEST_DEPTH - CHEST_LID;

            return insideWalls && underLid ? 0 : 1;
        }

        //The fossil's stone. FOSSIL_TALL is larger than the layout is deep so the poles are cut off and the crown
        //is a disc rather than a point (see BoulderRim). FOSSIL_SLAB is the picture's thickness in Z (two cells on
        //the unshifted levels, three on the shifted ones - the thinnest plate that holds at either parity, see the
        //pictures region). FOSSIL_BURY is how far inside the rim the frond is held and it is the number that seals
        //the payoff: raise it if a leaflet is ever reported showing through the flank, lower it and the frond
        //fills out and starts to leak.
        private const byte FOSSIL_GRID = 15;
        private const byte FOSSIL_DEPTH = 12;
        private const float FOSSIL_RADIUS = 4.3f;
        private const float FOSSIL_TALL = 5.6f;
        private const float FOSSIL_SLAB = 1.1f;
        private const float FOSSIL_BURY = 1.5f;

        /// <summary>
        /// A fern frond, 9 columns by 12 rows: a stem in columns 4 and 5 of <b>every</b> row with leaflets
        /// alternating either side of it. Two rules shape it and both are about where it may not go.
        /// <list type="bullet">
        /// <item><b>The stem is in every row</b>, so the frond is one connected group and its top row reaches the
        /// anchor layer — which is what lets it hang when the stone around it has gone. A leaflet may be one row
        /// thick because it hangs off that stem: the lonely-ball rule is about a ball's own colour GROUP, and the
        /// frond is one group.</item>
        /// <item><b>The bottom row is blank.</b> The layout's lowest level faces open air underneath, so a frond
        /// reaching it is exposed there however deeply <see cref="FOSSIL_BURY"/> buries it — measured, 4 balls of
        /// it were on the outside before this row was cleared, and a green ball landing on one of them takes the
        /// whole payoff before it has been seen.</item>
        /// </list>
        /// </summary>
        private static readonly string[] FERN =
        {
            "....##...",
            "....##...",
            "..####...",
            "..####...",
            "....####.",
            "....####.",
            ".#####...",
            ".#####...",
            "....####.",
            "....####.",
            "..####...",
            ".........",
        };

        private static bool FossilRock(int x, int z, int i)
        {
            Centred(x, z, i, FOSSIL_GRID, out float dx, out float dz);

            float rim = BoulderRim(OnionVertical(i, FOSSIL_DEPTH), FOSSIL_RADIUS, FOSSIL_TALL);
            return dx * dx + dz * dz <= rim * rim;
        }

        /// <summary>
        /// Whether a cell is on the frond: inside the slab, at least <see cref="FOSSIL_BURY"/> inside the rock's
        /// own rim, and drawn as <c>#</c> in <see cref="FERN"/>. Row 0 is the TOP of the picture, which is layout
        /// level <c>depth - 1</c>, so the bitmap reads in source the way the fossil reads in the game — the
        /// convention <see cref="OnWall"/> already keeps for the Gallery's walls.
        /// <para>
        /// The burial test is what makes the bitmap safe to redraw: the stone wins at the surface, so a leaflet
        /// drawn too long is trimmed rather than exposed. It also means a redraw changes the frond's ball count by
        /// less than the bitmap suggests, so re-measure rather than counting the hashes.
        /// </para>
        /// </summary>
        private static bool FossilFern(int x, int z, int i)
        {
            Centred(x, z, i, FOSSIL_GRID, out float dx, out float dz);
            if (MathF.Abs(dz) > FOSSIL_SLAB) return false;

            float rim = BoulderRim(OnionVertical(i, FOSSIL_DEPTH), FOSSIL_RADIUS, FOSSIL_TALL) - FOSSIL_BURY;
            if (rim <= 0f || dx * dx + dz * dz > rim * rim) return false;

            int column = x - (FOSSIL_GRID - FERN[0].Length) / 2;

            return PixelAt(FERN, column, FOSSIL_DEPTH - 1 - i) == '#';
        }

        //The fruit's own profile. MANGO_DROP is where the taper would close to nothing and it is well below where
        //the layout ends, which is what leaves the bottom a blunt nose instead of a point. MANGO_BULGE fattens the
        //+X flank, so the fruit is lopsided on the axis that runs ACROSS THE SCREEN (the gun starts at +Z looking
        //at the origin) and the stone's own offset reads as the fruit's fat side rather than as an error.
        //MANGO_SKIN is the peel, thick enough that the flesh is sealed but for eight cells of 165.
        private const float MANGO_RADIUS = 4.6f;
        private const float MANGO_DROP = 8.5f;
        private const float MANGO_BULGE = 0.45f;
        private const float MANGO_SKIN = 1.5f;
        private const int MANGO_STRIPS = 8;

        //The stone: off the axis in X, flattened in Z so its broad face is the one the gun sees, and generous on
        //purpose - MangoInside tests the peel first, so wherever it would break the surface it is simply clipped.
        private const float MANGO_STONE_X = 1.6f;
        private const float MANGO_STONE_Y = 3.4f;
        private const float MANGO_STONE_LONG = 2.1f;
        private const float MANGO_STONE_FLAT = 1.15f;
        private const float MANGO_STONE_TALL = 2.3f;

        private static float MangoRim(float ang, int i, int depth)
        {
            float d = BelowGlass(i, depth);
            float taper = 1f - (d / MANGO_DROP) * (d / MANGO_DROP);

            return taper <= 0f ? 0f : (MANGO_RADIUS + MANGO_BULGE * MathF.Cos(ang)) * MathF.Sqrt(taper);
        }

        /// <summary>
        /// The stone and the stalk it hangs from, as one shape. <see cref="Untwist"/> at zero turns is the
        /// emitter's polar pair read back as plain offsets — the tall levels' helper doing the un-rotated case,
        /// rather than a second copy of <c>r cos θ</c> here.
        /// </summary>
        private static bool MangoStone(float r, float ang, int i, int depth)
        {
            Untwist(r, ang, 0f, out float dx, out float dz);

            float d = BelowGlass(i, depth);
            float ex = (dx - MANGO_STONE_X) / MANGO_STONE_LONG;
            float ez = dz / MANGO_STONE_FLAT;
            float ey = (d - MANGO_STONE_Y) / MANGO_STONE_TALL;

            if (ex * ex + ez * ez + ey * ey <= 1f) return true;

            //The stalk: the same off-centre column, from the glass down into the stone's own top. It is what bonds
            //the stone to the ceiling in its OWN colour, so the flesh and the peel can both go and leave it
            //hanging - the property Onion's heart does not have.
            return d <= MANGO_STONE_Y
                   && MathF.Abs(dx - MANGO_STONE_X) <= MANGO_STONE_FLAT
                   && MathF.Abs(dz) <= MANGO_STONE_FLAT;
        }

        /// <summary>
        /// Peel, stone, flesh — <b>in that order</b>, which is the design's own safety catch: a cell within
        /// <see cref="MANGO_SKIN"/> of the surface is peel whatever the stone's numbers say, so the stone cannot
        /// break the surface and be seen before the fruit is opened. The flesh's strips are half a strip out of
        /// phase with the peel's, so the two sets of seams do not line up and the fruit reads as woven rather than
        /// as one set of cuts through both.
        /// </summary>
        private static BallType MangoInside(float r, float ang, int i, int depth)
        {
            float rim = MangoRim(ang, i, depth);

            if (rim - r <= MANGO_SKIN)
                return Sector(ang, 0f, MANGO_STRIPS, new[] { BallType.Type1, BallType.Type6 });

            if (MangoStone(r, ang, i, depth)) return BallType.Type4;

            return Sector(ang, HALF / MANGO_STRIPS, MANGO_STRIPS, new[] { BallType.Type7, BallType.Type2 });
        }

        #endregion

        #region The tall levels' own geometry (#160)

        //The horn's own geometry: the radius at the point, and how hard it opens. The rim grows with the SQUARE
        //of the level index, so the mouth is the last thing to arrive - see Horn() for why it opens upwards.
        private const float HORN_TIP = 1.6f;
        private const float HORN_FLARE = 0.0085f;

        //Where the two shell boundaries sit, as a share of the level's OWN rim. Not thirds: equal shares put
        //five ninths of the balls in the outer shell, and the drop test then reads that colour as more than half
        //the level on one ball. At 0.45 and 0.78 the three shells come out very nearly level with each other.
        private const float HORN_CORE = 0.45f;
        private const float HORN_SKIN = 0.78f;

        /// <summary>The horn's radius at one layout level — <see cref="HORN_TIP"/> at the point, opening quadratically.</summary>
        private static float HornRim(int i) => HORN_TIP + HORN_FLARE * i * i;

        /// <summary>
        /// White core, red flesh, gold skin — ringed by <b>each level's own rim</b> rather than by an absolute
        /// radius, so a narrow level down at the point shows the same three-colour proportions the mouth does.
        /// See <see cref="Horn"/> for the two reasons that matters (the anchor rule and the magazine's draw) and
        /// <see cref="OnionShell"/> for the same trick answering the same trap on a sphere.
        /// </summary>
        private static BallType HornShell(float r, int i)
        {
            float shell = r / HornRim(i);

            if (shell <= HORN_CORE) return BallType.Type4;  //white core
            if (shell <= HORN_SKIN) return BallType.Type1;  //red flesh
            return BallType.Type7;                          //gold skin
        }

        //Each strand's centre runs at HELIX_RADIUS from the axis and the strand itself is a disc of
        //HELIX_STRAND. 2.6 and 1.7 leaves a 1.8-cell gap between the two strands - enough that they read as two,
        //and not so much that either is thin enough to go lonely. The pair reaches 4.3 from the axis, inside the
        //4.5 a 13-wide field allows.
        private const float HELIX_RADIUS = 2.6f;
        private const float HELIX_STRAND = 1.7f;

        //18 degrees a level: 1.2 turns over the layout, and a little under a full turn across the eighteen
        //levels the camera frames, so what the player can see IS one turn of the helix
        private const float HELIX_TURNS_PER_LEVEL = 0.05f;

        //1.1 and not 1. At 0.05 turns a level the strand frame comes back onto the lattice axes every fifth
        //level, where a rung's half-width is compared against whole-number cell offsets: a threshold of exactly
        //1 is then a coin toss on float dust out of the polar round-trip.
        private const float HELIX_RUNG_HALF = 1.1f;
        private const int HELIX_RUNG_EVERY = 4;
        private const int HELIX_SEGMENT = 3;

        /// <summary>
        /// A point in a shape's <b>own</b> frame at a given height: the emitter's polar pair read at an angle
        /// rotated back by <paramref name="turns"/> whole turns. The shape stays a plain pair of discs and only
        /// the frame it is measured in turns with the level, so the occupancy and the colouring cannot disagree
        /// about where the shape is pointing.
        /// </summary>
        /// <param name="along">Signed distance along the shape's long axis.</param>
        /// <param name="across">Signed distance across it.</param>
        private static void Untwist(float r, float ang, float turns, out float along, out float across)
        {
            float local = ang - turns * MathF.Tau;
            along = r * MathF.Cos(local);
            across = r * MathF.Sin(local);
        }

        /// <summary>
        /// Which strand a cell belongs to at this height: <c>+1</c> the one at <c>+along</c>, <c>-1</c> the one
        /// opposite, <c>0</c> neither. The two centres are a half-turn apart by construction — they are
        /// <c>(±HELIX_RADIUS, 0)</c> in the frame <see cref="Untwist"/> hands back — so there is one rotation to
        /// get wrong rather than two.
        /// </summary>
        private static int HelixStrand(float r, float ang, int i)
        {
            Untwist(r, ang, i * HELIX_TURNS_PER_LEVEL, out float along, out float across);

            float near = (along - HELIX_RADIUS) * (along - HELIX_RADIUS) + across * across;
            if (near <= HELIX_STRAND * HELIX_STRAND) return 1;

            float far = (along + HELIX_RADIUS) * (along + HELIX_RADIUS) + across * across;
            return far <= HELIX_STRAND * HELIX_STRAND ? -1 : 0;
        }

        /// <summary>
        /// A rung: the bar across the axis joining the two strands, on every <see cref="HELIX_RUNG_EVERY"/>th
        /// level <b>counted from the glass down</b> — so the top level carries one, and the level is tied at the
        /// one place where a failure would cost everything.
        /// </summary>
        private static bool HelixRung(float r, float ang, int i, int depth)
        {
            if ((depth - 1 - i) % HELIX_RUNG_EVERY != 0) return false;

            Untwist(r, ang, i * HELIX_TURNS_PER_LEVEL, out float along, out float across);
            return MathF.Abs(across) <= HELIX_RUNG_HALF && MathF.Abs(along) <= HELIX_RADIUS;
        }

        //The leaning tower's own geometry. LEAN_GRID is stated here and not only on the design because the shape
        //function needs the field's width to find its axis, and two copies of that number are two places for it
        //to be wrong. 0.22 of a cell per level against a level's own height of 1/sqrt(2) is a lean of 17 degrees.
        private const byte LEAN_GRID = 13;
        private const float LEAN_RADIUS = 2.6f;
        private const float LEAN_PER_LEVEL = 0.22f;

        /// <summary>
        /// How far one layout level's centre has walked from the layout's own middle, in cells. Measured from
        /// the middle rather than from the bottom so the tower leans <i>through</i> the field's axis instead of
        /// off one side of it — which is what keeps the lateral margin the same on both flanks.
        /// </summary>
        private static float LeanShift(int i, int depth) => (i - (depth - 1) * HALF) * LEAN_PER_LEVEL;

        /// <summary>
        /// The emitter's own <c>r</c>, rebuilt around the drifted centre: the <c>x + shift - axis</c> line for
        /// line, with the shift taken off the layout index (see <see cref="Lean"/> for why that is the same
        /// parity). The drift is in X alone; Z stays on the field's axis, so the tower leans across the screen
        /// rather than away from the gun, where it would only look narrow.
        /// </summary>
        private static float LeanRadius(int x, int z, int i, int depth)
        {
            float axis = (LEAN_GRID - 1) * HALF + HALF;
            float shift = (i % 2) * HALF;

            float dx = x + shift - axis - LeanShift(i, depth);
            float dz = z + shift - axis;

            return MathF.Sqrt(dx * dx + dz * dz);
        }

        #endregion

        #endregion

        #region Emitting one design

        /// <returns>Whether the level that came out passed every check.</returns>
        private static bool Emit(Design design)
        {
            byte n = design.Grid;
            byte depth = design.Depth;
            byte fieldLevels = design.FieldLevels;
            byte offset = (byte)(fieldLevels - depth);

            if (offset % 2 != 0)
                throw new InvalidOperationException(
                    $"{design.File}: field {fieldLevels} less layout {depth} is an odd offset; the loader would " +
                    "extend the field by one level to keep the level parity and the design would not sit where it was drawn");

            //One world axis for every layer. The shifted (odd) levels put their cells on it exactly; the
            //unshifted ones sit half a cell off it, which is the lattice's own close packing and not an error.
            float axis = (n - 1) * 0.5f + 0.5f;

            BallPositionType[,,] balls = new BallPositionType[n, n, depth];

            for (byte i = 0; i < depth; i++)
            {
                byte fieldLevel = (byte)(i + offset);
                float shift = (fieldLevel % 2) > 0 ? 0.5f : 0f;

                for (byte x = 0; x < n; x++)
                    for (byte z = 0; z < n; z++)
                    {
                        float dx = x + shift - axis;
                        float dz = z + shift - axis;
                        float r = MathF.Sqrt(dx * dx + dz * dz);
                        float manhattan = MathF.Abs(dx) + MathF.Abs(dz);
                        float ang = MathF.Atan2(dz, dx);

                        bool occupied =
                            design.OccupiedBlock != null ? design.OccupiedBlock(x, z, i, depth)
                            : design.OccupiedManhattan != null ? design.OccupiedManhattan(manhattan, i, depth)
                            : design.Occupied(r, ang, i, depth);

                        if (!occupied) continue;

                        BallType type =
                            design.Colour != null ? design.Colour(r, ang, i, depth)
                            : design.ColourManhattan != null ? design.ColourManhattan(manhattan, i, depth)
                            : design.BlockColour(x, z, i);

                        //The position the ball will actually occupy in the raw grid frame, so the stored
                        //one agrees with what PutBallAt recomputes at load rather than merely being ignored
                        Vector3 position = BallsMap.GetRealPosition(x, z, fieldLevel);

                        balls[x, z, i] = new BallPositionType
                        {
                            PositionX = position.X,
                            PositionY = position.Y,
                            PositionZ = position.Z,
                            Type = type,
                        };
                    }
            }

            int repaired = RepairLonelyBalls(balls, n, depth, offset, fieldLevels);

            Level level = new()
            {
                Name = design.Name,
                Author = "BS3D",
                SkyDome = design.Sky,
                Scene = design.Scene,
                Music = design.Music,
                Map = new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = fieldLevels, Balls = balls },
            };

            string path = Path.Combine(_outDir, design.File);
            level.Save(path);

            return Validate(design, path, repaired);
        }

        /// <summary>
        /// Recolours every ball whose own colour group is smaller than <see cref="MIN_GROUP"/> to whichever
        /// neighbouring colour puts it in the largest one — the safety net under
        /// <see cref="FindLonelyBalls"/>, so a design cannot ship a ball that needs two shots.
        /// <para>
        /// A shape drawn as a formula meets a lattice that rounds it off, and the rounding leaves slivers: a
        /// block clipped by the rim of a disc, a ring one cell wide where the curve happens to fall between
        /// two rows. Those are a handful of balls out of hundreds and are invisible in the pattern, which is
        /// exactly why they are worth fixing here rather than by bending the formula until they go away.
        /// A whole rim of them is a <b>design</b> fault and belongs in the design — see <see cref="Gem"/>.
        /// </para>
        /// </summary>
        /// <returns>How many balls were recoloured, which is the number that says whether a design is being
        /// rounded off at its edges or quietly rewritten.</returns>
        private static int RepairLonelyBalls(BallPositionType[,,] balls, byte n, byte depth, byte offset, byte fieldLevels)
        {
            //Repaired on a map rather than on the array: the neighbour rule and the parity that drives it are
            //BallsMap's, and a second copy of them here is a second place for them to be wrong
            BallsMap map = new(new BallPositionTypes { StageSizeX = n, StageSizeZ = n, Levels = fieldLevels, Balls = balls });
            StaticBall[,,] array = map.GetStaticBallsArray();
            XZLevel size = new(map.StageSizeX, map.StageSizeZ, map.Levels);

            int repaired = 0;

            //Recolouring one ball can rescue its neighbour, so this runs until it stops changing anything.
            //Bounded because a pathological design could otherwise cycle two cells against each other.
            for (int pass = 0; pass < 8; pass++)
            {
                int changed = 0;

                for (byte l = 0; l < map.Levels; l++)
                    for (byte x = 0; x < map.StageSizeX; x++)
                        for (byte z = 0; z < map.StageSizeZ; z++)
                        {
                            if (array[x, z, l] == null) continue;

                            XZLevel cell = new(x, z, l);
                            if (map.GetConnectedSameTypeCells(cell).Count >= MIN_GROUP) continue;

                            BallType best = array[x, z, l].Type;
                            int bestGroup = 0;

                            //Every colour standing next to it is a candidate; the one that leaves it in the
                            //biggest group wins. Measured by actually recolouring and asking, because the
                            //answer depends on what those neighbours are themselves connected to.
                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            {
                                StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                                if (other == null || other.Type == best) continue;

                                map.PutBallAt(x, z, l, other.Type);
                                int group = map.GetConnectedSameTypeCells(cell).Count;

                                if (group > bestGroup) { bestGroup = group; best = other.Type; }
                            }

                            map.PutBallAt(x, z, l, best);
                            if (bestGroup > 0) { changed++; repaired++; }
                        }

                if (changed == 0) break;
            }

            //Back into the layout array the level file is written from
            for (byte i = 0; i < depth; i++)
                for (byte x = 0; x < n; x++)
                    for (byte z = 0; z < n; z++)
                        if (balls[x, z, i] != null)
                            balls[x, z, i].Type = array[x, z, i + offset].Type;

            return repaired;
        }

        /// <summary>
        /// Reads the file back the way the game does and reports what it actually got. A design is only
        /// worth shipping if the loader agrees with it, every ball hangs off the glass, and every colour
        /// has somewhere to be matched.
        /// </summary>
        /// <returns>
        /// Whether the level passes all three: nothing floating free of the glass, no ball standing alone,
        /// and no colour whose best single shot is the whole cluster.
        /// </returns>
        private static bool Validate(Design design, string path, int repaired)
        {
            Level loaded = Level.Load(path);
            BallsMap map = new(loaded.Map);
            map.Center();

            int disconnected = map.GetCellsDisconnectedFromCeiling().Count;

            StaticBall[,,] array = map.GetStaticBallsArray();
            Dictionary<BallType, int> counts = new();
            Dictionary<BallType, int> largestGroup = new();

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        StaticBall ball = array[x, z, l];
                        if (ball == null) continue;

                        counts.TryGetValue(ball.Type, out int c);
                        counts[ball.Type] = c + 1;

                        int group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l)).Count;
                        largestGroup.TryGetValue(ball.Type, out int g);
                        if (group > g) largestGroup[ball.Type] = group;
                    }

            long fileSize = new FileInfo(path).Length;

            //The music is echoed back off the LOADED level rather than off the design, like the scene and the
            //dome beside it: a theme that failed to reach the file is a level that plays the wrong piece and
            //nothing else says so — the fallback is silent by design (see Design.Music)
            Console.WriteLine($"--- {design.File} '{loaded.Name}' ({loaded.Scene.Kind}, sky {loaded.SkyDome}"
                              + $", {loaded.Music ?? "no theme named"}) {fileSize / 1024} kB");
            Console.WriteLine($"    field {map.StageSizeX}x{map.StageSizeZ}x{map.Levels}, layout {design.Depth} deep, "
                              + $"{map.GetBallsCount()} balls, lowest occupied level {map.GetLowestOccupiedLevel()}");
            Console.WriteLine($"    hanging off the glass: {(disconnected == 0 ? "all" : $"NO - {disconnected} balls float free")}");

            int total = map.GetBallsCount();

            int margin = LateralMargin(map);
            Console.WriteLine($"    lateral margin: {(margin >= 1 ? $"{margin} free cell(s) all round" : "NONE - the layout is ON the field wall")}");

            LonelyReport lonely = FindLonelyBalls(map);
            Console.WriteLine($"    reachable in one ball: {(lonely.Alone == 0 ? "all" : $"NO - {lonely.Alone} STAND ALONE")}"
                              + $" (in pairs {lonely.Paired}, primed {total - lonely.Alone - lonely.Paired})"
                              + $", {repaired} recoloured by the repair pass");
            foreach (string where in lonely.Examples) Console.WriteLine($"      {where}");

            bool oneShot = false;

            foreach (var pair in counts.OrderBy(p => p.Key))
            {
                int dropped = DropTest(loaded.Map, pair.Key);
                int percent = total == 0 ? 0 : dropped * 100 / total;

                if (percent >= ONE_SHOT_PERCENT) oneShot = true;

                Console.WriteLine($"    {pair.Key,-6} {pair.Value,4} balls, largest standing group {largestGroup[pair.Key],4}"
                                  + (largestGroup[pair.Key] >= 3 ? "  primed" : "  <-- NOT PRIMED")
                                  + $", best single shot drops {dropped,4} ({percent,3}%)"
                                  + (percent >= ONE_SHOT_PERCENT ? "  <-- ONE-SHOT LEVEL" : string.Empty));
            }

            return disconnected == 0 && lonely.Alone == 0 && !oneShot && margin >= 1;
        }

        /// <summary>
        /// How many empty columns of field the layout leaves on its tightest side — <b>the room a shot has to
        /// land in when it arrives at the cluster's flank</b>, and the check that was missing when this pack
        /// was written.
        /// <para>
        /// The field is a box. A ball on the cluster's side face that is also on the field's <i>wall</i> has
        /// no lateral neighbours at all: if the cells under it are taken, a shot into that pocket finds
        /// nothing in either ring, does not stick, bounces off and costs a ball and the streak. It is a
        /// documented trap ("The landing preview, and why the field's edge needed one" in
        /// <c>docs/game-session.md</c>) and every disc here walked straight into it — a radius of 5.5 in a
        /// 13-wide field reaches the wall on the unshifted levels, and the Gem's taxicab rim reached it on
        /// all four sides. It was reported from play as "the ball bounced instead of sticking", on Pinwheel
        /// and on Static, and reproduced at one refusal in 34 varied-angle shots.
        /// </para>
        /// <para>
        /// One free column is enough: it gives every flank ball a lateral neighbour to offer. It is bought by
        /// widening the FIELD rather than by shrinking the shape — the shapes are what the level looks like
        /// and they were kept deliberately — which costs a slightly wider glass plate and a slightly longer
        /// camera stand-off, and nothing else.
        /// </para>
        /// </summary>
        private static int LateralMargin(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l] == null) continue;

                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (z < minZ) minZ = z;
                        if (z > maxZ) maxZ = z;
                    }

            if (minX == int.MaxValue) return int.MaxValue; //an empty layout is all margin

            return Math.Min(
                Math.Min(minX, map.StageSizeX - 1 - maxX),
                Math.Min(minZ, map.StageSizeZ - 1 - maxZ));
        }

        /// <summary>
        /// Balls whose own colour group is too small to be finished with one shot. <b>A ball standing alone
        /// needs two landed balls</b> to make the minimum three, and that is not a puzzle, it is a chore — the
        /// player has to hit the same isolated cell twice with the right colour before anything happens.
        /// <para>
        /// It comes from the lattice, not from carelessness: cells on one level touch only their four
        /// <b>orthogonal</b> neighbours, never their diagonal ones. So any colour band one cell thick that
        /// runs diagonally is a string of balls that do not touch each other at all. A taxicab ring of odd
        /// width is exactly that, and the first Gem had 28 of them around the rim of its top layer, which is
        /// the layer against the glass and therefore has no level above to connect through either.
        /// </para>
        /// </summary>
        private static LonelyReport FindLonelyBalls(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            LonelyReport report = new();

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l] == null) continue;

                        int group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l)).Count;
                        if (group >= 3) continue;

                        if (group == 1) report.Alone++; else report.Paired++;

                        if (report.Examples.Count < 3)
                            report.Examples.Add($"group of {group}: {array[x, z, l].Type} at cell ({x},{z}) on level {l}");
                    }

            return report;
        }

        private sealed class LonelyReport
        {
            public int Alone;
            public int Paired;
            public readonly List<string> Examples = new();
        }

        /// <summary>
        /// How many balls the best single shot of one colour would bring down: its largest standing group,
        /// plus everything that group was the last anchor for. This is the number that decides whether a
        /// design is a level or a firework — a colour that drops the whole cluster means the anchor layer
        /// is one colour, and the level is over on the first lucky ball.
        /// </summary>
        /// <remarks>
        /// Run on a map rebuilt from the same data rather than on the caller's, because it destroys the one
        /// it measures. <see cref="BallsMap"/> has no clone and does not need one for this.
        /// </remarks>
        private static int DropTest(BallPositionTypes data, BallType type)
        {
            BallsMap map = new(data);
            StaticBall[,,] array = map.GetStaticBallsArray();

            List<XZLevel> largest = new();

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l]?.Type != type) continue;

                        List<XZLevel> group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l));
                        if (group.Count > largest.Count) largest = group;
                    }

            foreach (XZLevel cell in largest) map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

            return largest.Count + map.GetCellsDisconnectedFromCeiling().Count;
        }

        #endregion

        private sealed class Design
        {
            public string File;
            public string Name;
            public byte Grid;
            public byte Depth;

            /// <summary>
            /// How deep the play field is. <see cref="FIELD_LEVELS"/> for every ordinary level — the deepest
            /// field the game hangs at its standard height and frames whole — and larger only for a tall one,
            /// which is framed from its floor up and reaches out of shot.
            /// </summary>
            public byte FieldLevels = FIELD_LEVELS;
            public SceneConfig Scene;
            public byte Sky;

            /// <summary>
            /// Which composition the level plays, written into <c>Level.Music</c> — and it is a property of
            /// the <b>block</b> rather than of the design: every level of a block names the same piece, so
            /// the music changes when the chapter does and not when the level does (#194).
            /// <para>
            /// Left null a level hands the choice to the set's own positional rotation
            /// (<c>ProceduralMusic.ThemeFor</c>'s <c>index % THEME_COUNT</c>), which is what every level did
            /// before this — and which is exactly why the order could not be rearranged without silently
            /// rescoring the campaign. Naming it pins it. An unknown spelling falls back to that same
            /// rotation rather than throwing, so a typo here is a level that quietly plays the wrong piece:
            /// the five names are <c>pulse</c>, <c>bohemia</c>, <c>nocturne</c>, <c>dechovka</c> and
            /// <c>ember</c> (#163, the rock ballad — no block uses it yet).
            /// </para>
            /// </summary>
            public string Music;

            public int Shots;
            public int CeilingStep;

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
        }
    }
}
