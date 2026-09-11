﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// Writes every level of the campaign but the hand-drawn Colossus, and the set that orders them, and
    /// <b>validates every one through the game's own loader</b> before it is written anywhere the game will see
    /// it. A design is one <see cref="Design"/>: a silhouette, a colouring, a scene and a set of rules.
    /// <para>
    /// It exists because these levels are generated, and a generated level that is only checked by
    /// playing it is checked by nobody. The three properties it enforces are all invisible in a
    /// screenshot and all of them were got wrong at least once here — see <see cref="Validate"/>,
    /// <see cref="DropTest"/> and <see cref="FindLonelyBalls"/>. What it cannot check is whether the
    /// thing looks good, which is what the screenshot skill is for.
    /// </para>
    /// <para>
    /// <b>Nor could any of them, until #301, check whether a level survives being taken apart</b> — every
    /// check above reads the layout as authored, and all of them are true of a level that cannot be
    /// finished, because none knows what the remainder <i>weighs</i>. <see cref="SagProbe"/> is the answer
    /// and it hangs the level in the real simulation instead of reading it; <see cref="WorstAnchorLoad"/>
    /// is the cheap figure that came out of building it. Read <see cref="RunSagGate"/> for why the first is
    /// opt-in and <c>docs/formats-and-tools.md</c> for what it is not yet entitled to decide.
    /// </para>
    /// <para>
    /// <b>The class is split by block (#386).</b> This file is the generator itself: <see cref="Main"/>, the
    /// block tables, the set and its unlock ramp, <see cref="Emit"/> with every gate, and <see cref="Design"/>.
    /// Each block's designs, with every helper only that block's designs use, are in
    /// <c>Designs/BlockNN_Name.cs</c>, and a helper the designs of more than one block use is in
    /// <c>Designs/Shared.cs</c> — so a new design goes into its block's file, and a new block is a new file
    /// there and a row in the tables here. <b>A static field's initialiser must not read a static field declared
    /// in another of these files</b>: the order in which a partial class's parts are initialised is unspecified.
    /// </para>
    /// <para>
    /// <c>dotnet run --project Tools\LevelGen\LevelGen.csproj [output directory] [--sag[=Name,Name]]</c>.
    /// Rewrites <c>Levels.json</c> too, One and Colossus included, so run it whole rather than for one level.
    /// </para>
    /// </summary>
    internal static partial class Program
    {
        // The field is 16 levels deep for every design: that is the deepest field the game hangs at its
        // standard height (FIELD_TOP_Y, 8/sqrt2) without raising it off the death line, so every level is
        // framed by the camera and the gun exactly the way One.json is. The layout hangs at the top and
        // the empty levels under it are the room shot balls attach into.
        private const byte FIELD_LEVELS = 16;

        // The pictures' own, and it is a LEVER ON HANGING HEIGHT rather than on growth room (#203). A field
        // is hung at FIELD_TOP_Y unless it is deep enough that its bottom level would start past the death
        // line, in which case GameplayScreen.FitFieldToMap raises the WHOLE field until the floor clears the
        // line by FIELD_FLOOR_MARGIN — so past 16 levels, adding depth pushes the layout UP rather than
        // leaving room under it. That is the only lever a design has on how much air its lowest row starts
        // with, the layout always hanging at the top of its field.
        //
        // The pictures needed one. A wall is 14 rows in a field of 16, so its lowest ball started 1.96 above
        // the line by centre and 1.46 by surface, where every other level in the pack has at least 2.88 and
        // most have 7 to 8.5 (measured off the game's own [field] line, all five). Three things followed from
        // that, and the third is the one that says this was a fault rather than a tight margin.
        //
        // The floor alarm arms at CEILING_DEATH_Y + 3 steps of 0.6, i.e. -3.70, and these walls START at
        // -3.54 — 0.16 from lighting the net, on a cluster whose own comment budgets "a few tenths of a unit"
        // of bob for a shove. A stalk left dangling two lattice levels under the wall sat at -4.95, one shove
        // or one descent from the line. And an UNTOUCHED wall crossed the line on its FOURTH descent
        // (1.96 / 0.6 = 3.3) against a budget that buys SIX on every one of the five — 60 shots stepping
        // every 10, 55 every 9, 48 every 8 — so the ceiling could end these levels before their own budget
        // ran out. That is the shape of the bug: not a level that is hard, a level whose two clocks disagree.
        //
        // Measured after, on all five (the [field] line again): top Y 5.66 -> 7.02, and it now reports
        // "raised off the line" because the raise is what does the work; floor -4.95 -> -5.00; lowest ball
        // -3.54 -> -2.17, i.e. 1.96 -> 3.33 above the line by centre and 1.46 -> 2.83 by surface. An untouched
        // wall now survives five descents and crosses on the sixth, which is where the budget ends anyway.
        // The raise costs about 2.3 degrees of the gun's elevation budget — [aimcheck]'s steepest cell moves
        // from (0,0,14) at 67.3/69.9 deg to (0,0,16) at 69.6/72.0 of the same 80.2 limit — so all five still
        // PASS with roughly eight degrees spare. GameCameraFit re-solves a hair closer (30.8 -> 30.7 out on
        // the 15-wide pictures, 31.5 -> 31.4 on the 17-wide), which is not a visible change in ball size.
        //
        // EVERY FIGURE ABOVE IS AGAINST THE DEATH LINE AT -5.5, where it stood until it was lowered onto the
        // island (GameplayScreen.CEILING_DEATH_Y is ArenaIsland.TOP_Y + 1 now, after a report that a cluster
        // which merely SWUNG dipped under it a few shots into a level). Re-measured after that move: an
        // 18-level field is no longer raised at all - it is pinned at FIELD_TOP_Y again and hangs 1.36 lower
        // - and a picture's lowest ball starts 3.96 above the line instead of 3.33, which is 6.6 descents
        // against a budget that buys six. The depth still does what it was chosen for; two of the three units
        // of air now come from the line rather than from the raise, and the aimcheck cost the raise used to
        // charge is gone with it (the steepest cell needs 60.5 deg of the 80.2 limit, not 69.6).
        //
        // 18 is the figure and it is also the CEILING: GameplayScreen.FRAMED_LEVELS is 18 and its test is
        // "Levels > FRAMED_LEVELS", so 20 would quietly turn a picture into a tall level, fed by
        // FeedTallColumn and aim-clamped by TALL_AIM_HEADROOM. 18 - 14 = 4 is even, which is what keeps every
        // row's level parity — and therefore the drawing itself — exactly where it was; the emitter refuses
        // an odd offset rather than drawing it shifted. If the air this buys is ever not enough, the next
        // lever is fewer bitmap rows, which means redrawing the pictures.
        private const byte PICTURE_FIELD_LEVELS = 18;

        // THE CEILING BUDGET IS PART OF A DESIGN, NOT A KNOB TURNED AFTERWARDS (#288), and it is arithmetic no
        // gate can do for you — Validate reads the layout as authored and knows nothing about how long the
        // level lasts. Four shipped levels lost to "The cluster reached the line" rather than to their own
        // budget before anyone did the sum. It is two numbers:
        //
        //     clearance      = the [field] line's "above the line", printed by the game on every level load
        //     consumed       = floor(Shots / CeilingStep) * GameplayScreen.CEILING_DESCENT_PER_STEP (0.60)
        //     final headroom = clearance - consumed
        //
        // FINAL HEADROOM MUST CLEAR A SWING, and how deep a swing goes is measured rather than guessed: the
        // probe recorded beside GameplayScreen.CLUSTER_SWING_ALLOWANCE took 35 swings over 67 s on Chest, the
        // second-heaviest cluster in the pack, and the deepest was 0.82 units below the trend. So a level
        // ending with less than that has an ordinary swing UNDER the line however well it is played, and the
        // hold rule then decides it; the allowance itself, 1.00, is the figure to design to. Measured before
        // the fix: Pylon -1.04, Ghost +0.36, Orrery +0.37, Cube +1.18 — the first past the line with no swing
        // at all, on an untouched cluster, at shot 66 of 74.
        //
        // A LOW FIGURE IS NOT BY ITSELF THE FAULT, and this is the part that stops the sum being used as a
        // gate. Horn and Turbine both sit at -0.23 and neither has ever been complained about, because their
        // lowest ball is a TIP — Horn opens upwards from HORN_TIP, so the few balls at the bottom go in the
        // first shots and the true lowest point then jumps several units up. The sum is honest about the
        // cluster AS AUTHORED; what a competent run leaves hanging is the designer's own judgement, and a
        // shape whose lowest balls are structural (Pylon's legs, Ghost's foot) has to be read that way.
        //
        // THE LEVERS, in the order they are worth trying: CeilingStep (costs nothing but pace), then Shots,
        // then FieldLevels — but the third is spent the moment FitFieldToMap has raised a field as far as the
        // floor margin allows (Pylon, floor Y -7.00), and it is closed outright wherever a block has promised
        // its levels are framed whole (the Arcade's 18, GameplayScreen.FRAMED_LEVELS). Changing the layout is
        // the last resort, not the first.

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
        /// <b>The pack runs 5–52 % and the top of that band is where a level is DESIGNED to cascade</b>, not
        /// where one slipped through. Two things put a design up there and only one of them is about pictures.
        /// A drawn symbol in ONE ink is one connected group by construction (Smiley 52 %, Heart 42 %,
        /// Star 40 %) — and on a level whose point is being recognised, the symbol coming away in one piece is
        /// the reward. A colouring in wide concentric shells is the same thing on a solid of revolution, which
        /// is why the gentle block that teaches what a colour group <i>is</i> holds the next two figures under
        /// the pictures (Bullseye 45 %, Toadstool 42 %). This said "the top of that band is the three
        /// pictures" and quoted "4–16 % for the geometric levels", which was never true of Bullseye and is the
        /// reason it is now measured here rather than characterised.
        /// </para>
        /// <para>
        /// <b><see cref="One"/> is the design that has visited both ends of this band, and which end it sits
        /// at is decided by where the colour boundaries run.</b> Banded a course a colour it topped the pack
        /// outright at <b>74 %</b>, because a course of a pyramid is load-bearing — everything under it hangs
        /// off it, so the band that goes takes the tip down with it. Turned onto the pyramid's <i>walls</i>
        /// instead (#234's third pass) the same shape, the same three colours and the same 385 balls measure
        /// <b>30 %</b>: a shell hangs off the plate it reaches and not off the shell inside it, so nothing
        /// rides a wall down. The figure moved by 44 points with no change to the geometry at all.
        /// </para>
        /// <para>
        /// The bottom of the band is where the dial is turned the other way: <see cref="Zebra"/> at 9 % and
        /// <see cref="Elephant"/> at 21 % are pictures drawn in two and three inks, so their symbols are not one
        /// group at all, and <see cref="Lantern"/> at 6 % is a wall of panes with no plate anywhere. That is the
        /// whole difficulty ramp of #194's blocks, stated in one column of numbers.
        /// </para>
        /// </summary>
        private const int ONE_SHOT_PERCENT = 90;

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
            ("The Meadow", 10), ("The Gallery", 10), ("The Coil", 10), ("The Tower", 10), ("The Reveal", 10),
            ("The Quarry", 10), ("The Nebula", 10), ("The Eruption", 10), ("The Spectrum", 10), ("The Arcade", 10),
            ("The Mirage", 10),
        };

        //THE BLOCKS' THEMES (#194). A block's piece is named on every level of it, so the music changes
        //when the chapter does and not when the level does - see Design.Music for what naming it buys and what
        //leaving it null used to cost. Named after the block rather than after the piece because that is the
        //thing being decided: if a block's music is ever changed it is changed HERE, once, and not five times.
        //
        //FIVE pieces against TEN blocks, so half the assignments are reprises, and every reprise is a desk
        //decision with one constant behind it. The first is the bookend #207 chose: the campaign opens on the
        //piece Level One has always played and the Quarry brings it back — that reprise was FORCED while four
        //pieces existed; it is a choice now, and it is kept because a reprise at the end of the original ramp
        //is a real musical idea where "every block gets its own" is only tidy. The second is the Nebula
        //taking Nocturne: a seventh block against five pieces made a second reprise unavoidable short of
        //composing (#229's job, not this one's), and night jazz over the void is the register that fits. The
        //third is the Arcade taking Pulse, which is the only one of the five that sounds like the place it
        //plays in — an electronic piece over a neon city — and since #300 made the Arcade the campaign's
        //last block, it puts the LAST block on the piece the first one opened with: the frame round the
        //whole thing that comment always hoped it was. The fourth is the Spectrum taking Bohemia, chosen
        //rather than left over: the one piece whose form is a statement, a second subject and a coda that
        //brings the statement back — chosen when the Spectrum closed the campaign (#253), and kept at #300:
        //the coda-form now reads as the day's own closing figure, the dawn chapter being the campaign's
        //last daylight before the finale's night.
        //
        //The Coil takes Ember, and that is #163 and #207 answering each other. #163 landed the rock ballad with
        //no block using it; #207 wrote, when it still had to reprise Nocturne here, that this was the block with
        //the weaker claim on a reprise and the one to give the ballad to when it landed. Both are now true at
        //once, so the desert gets the amplifier and Nocturne is left to the Reveal alone. THE ERUPTION REPRISES
        //EMBER (#295, the owner's pick over a new piece or a fourth Pulse): the rock ballad is the fire
        //register and had the only single-block piece beside Mural, so the reprise evens the tally — and if
        //the ear disagrees, a new piece is #292's line of work and one constant here.

        private const string MUSIC_RINGS = "pulse";
        private const string MUSIC_GALLERY = "mural";
        private const string MUSIC_COIL = "ember";
        private const string MUSIC_TOWER = "bohemia";
        private const string MUSIC_REVEAL = "nocturne";
        private const string MUSIC_QUARRY = "pulse";
        private const string MUSIC_NEBULA = "nocturne";
        private const string MUSIC_VOLCANO = "ember";
        private const string MUSIC_ARCADE = "pulse";
        private const string MUSIC_SPECTRUM = "bohemia";

        //THE MIRAGE REPRISES MURAL, and the tally argument #295 used for the Eruption's Ember is the same one
        //here with the last single-block piece: Mural was the Gallery's alone, so an eleventh block on
        //anything else would have left one piece carrying a chapter while three carried two or three. The ear
        //agrees with the arithmetic for once. Mural is the set's only SYNCOPATED piece - the 3+3+2 tresillo,
        //the kick never marking all four beats, the hook down in a melodic sub - so it is the one composition
        //whose weight lands where a listener does not expect it, which is what a chapter of balls that are
        //not what they look like wants behind it. A hallucination is a place where the beat is off.
        private const string MUSIC_MIRAGE = "mural";

        /// <summary>
        /// WHAT EACH CHAPTER'S BALLS ARE MADE OF. A property of the <b>block</b> exactly as the music is — the
        /// material changes when the chapter does and not when the level does — and stated once per block here
        /// so a block's ten designs cannot drift apart.
        /// <para>
        /// Since #272's eight styles landed there are ten materials, and since #295 made the Eruption the
        /// tenth chapter <b>every chapter hangs a different one and every material has a home</b> — the
        /// material is as strong a chapter marker as the scene and the piece of music. Each is placed where
        /// it works rather than where it sounds good: two of the eight are scene-bound for reasons measured
        /// in their own issues, and both are placed accordingly.
        /// </para>
        /// <para>
        /// <b>The vinyl beach ball is back in the campaign since #295</b> — the Eruption took the lava its
        /// entry always said the volcano had the better claim on, and the Reveal takes the vinyl home. It is
        /// still also what everything unauthored draws — the map editor, the Testbed, the front end's own
        /// preview, and any level that says nothing.
        /// </para>
        /// <para>
        /// The costs are all measured against the same control and are under "Ball rendering" in
        /// <c>docs/rendering.md</c>. Only the bubble is dearer than the vinyl it replaces (about 10 % of a
        /// frame at 4K-class fill); every other style here is <i>cheaper</i>, so this table is close to free
        /// and in places a saving — which is why the densest chapters can carry what they carry.
        /// </para>
        /// </summary>

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
        private const BallStyle BALLS_MEADOW = BallStyle.Bubble;

        /// <summary>
        /// <b>The Gallery — wound wool</b> (#311). The savanna is warm and golden and the block is ten drawn
        /// pictures; wool is the only soft material in the set, and a picture built out of yarn reads as
        /// something made by hand, which is what a wall of drawn symbols already is. It is also the gentlest
        /// step after the opening chapter's glass, which suits the second chapter's place on the ramp.
        /// </summary>
        private const BallStyle BALLS_GALLERY = BallStyle.Wool;

        /// <summary>
        /// <b>The Coil — polished marble</b> (#305). The desert's block hangs everything on slender links, and
        /// marble is the material that reads as <i>mass</i>: a coiled column of polished stone standing in
        /// sand is a monument, and the weight is what makes the slenderness alarming rather than merely thin.
        /// </summary>
        private const BallStyle BALLS_COIL = BallStyle.Marble;

        /// <summary>
        /// <b>The Tower — frosted ice</b> (#307). Mountains, violet dusk, and a block whose layouts are deeper
        /// than the camera frames. Ice is the obvious material for the altitude, and the one whose read is
        /// light carried <i>through</i> the ball — which the mountains' low sun behind a tall cluster gives it
        /// more of than any other chapter.
        /// </summary>
        private const BallStyle BALLS_TOWER = BallStyle.Ice;

        /// <summary>
        /// <b>The Reveal — the vinyl beach ball</b>, since #295 moved the molten crust to the volcano its own
        /// entry always said had the better claim (the sentence stood here from #310 and came true). The
        /// vinyl is the right second choice for the cavern twice over: the block's statement is the
        /// <i>payoff</i> — a thing hidden inside another thing — not the material, so the plainest style is
        /// the one that does not compete with it; and the vinyl's emissive heartbeat was designed against
        /// dark backdrops, so the campaign's dark chapter is where the classic ball still reads as alive.
        /// (#313 recorded the lava/cavern pairing as the set's weakest — it read close to the plasma two
        /// chapters on; this move retires that note.)
        /// </summary>
        private const BallStyle BALLS_REVEAL = BallStyle.Beach;

        /// <summary>
        /// <b>The Quarry — anodised metal</b> (#306). A quarry on the moon is a chapter about extracted ore,
        /// and this is the style whose colour <i>is</i> its reflectance — thirteen alloys rather than thirteen
        /// mirrors. The scene matters and not just the theme: metal goes flat on a bright featureless dome
        /// because a mirror ball has nothing to reflect but a gradient, and the moon's lit surface and hard
        /// shadow give it something. It is also the cheapest style in the set on the densest block in the
        /// campaign, which is not a coincidence worth wasting.
        /// </summary>
        private const BallStyle BALLS_QUARRY = BallStyle.Metal;

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
        private const BallStyle BALLS_NEBULA = BallStyle.Plasma;

        /// <summary>
        /// <b>The Eruption — molten crust</b> (#295, from #310's own hand-over sentence). The volcano is the
        /// scene the style was designed for: dark crusted balls whose glowing seams read as cooling lava over
        /// the one backdrop where the ground itself glows, under the darkest dome (9), where an emissive
        /// material is the block's whole light. It also serves the block's statement literally — the glow is
        /// the load, and on these balls the glow is drawn as seams in dark crust.
        /// </summary>
        private const BallStyle BALLS_VOLCANO = BallStyle.Lava;

        /// <summary>
        /// <b>The Arcade — cut gems</b> (#308). The neon city is the one scene that carries its own point
        /// lights, and a faceted stone is the material with most to do with them: every facet catches a
        /// different sign, so a cluster glitters in the colours of the street rather than of the sky. An
        /// arcade full of jewels is also the block's own register — five hollow pixel-art solids, played for
        /// spectacle.
        /// </summary>
        private const BallStyle BALLS_ARCADE = BallStyle.Gem;

        /// <summary>
        /// <b>The Spectrum — crackled porcelain</b> (#312). This block sweeps one hue family through a whole
        /// level, so it wants the material that shows a hue best, and a deep glaze over a body is exactly
        /// that: the colour sits under a coat rather than on a surface, which is what makes a single family
        /// read as a range instead of as one flat note. The city at dawn is cool and hard, and so is this.
        /// </summary>
        private const BallStyle BALLS_SPECTRUM = BallStyle.Porcelain;

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
        private const BallStyle BALLS_MIRAGE = BallStyle.Porcelain;

        /// <summary>
        /// WHAT COLOUR A ROCK IS WRITTEN AS, forced by <see cref="Emit"/> over whatever the design's own
        /// colour rule answered. Nothing in the game reads it — <c>GranitePS</c> ignores
        /// <c>PatternPrimaryColor</c> entirely and is the one shading whose colour is a constant, and
        /// <c>RecountBallTypes</c> counts only the matchable balls, so a rock never puts a colour in the
        /// magazine either. But <see cref="BallPositionType.Type"/> is not nullable and every cell carries
        /// one, so the value is a decision about what a READER of the file sees.
        /// <para>
        /// Slate, because it is the nearest of the thirteen to what the ball is actually drawn as, so a map
        /// opened in the editor or read as JSON says roughly the truth rather than something arbitrary — and
        /// <b>no rock level plays slate</b> (nor black, its neighbour; see the five Mirage palettes). A rock
        /// wearing a colour the level is matching invites the one misreading the granite technique's header
        /// exists to prevent, which is a player aiming that colour at it.
        /// </para>
        /// <para>
        /// <b>⚠ It is forced in the emitter rather than asked of each design, and that is the fix for how it
        /// first shipped.</b> The constant was written, documented and then simply never called: every rock
        /// in the block came out wearing whatever its design's own <c>BlockColour</c> had answered for that
        /// cell, which on Obsidian was one of the five colours the level plays. Nothing failed — the census
        /// skips rocks, so no gate could see it — and the file said the opposite of what this comment
        /// claimed. A rule about what a rock IS belongs in the one place every rock passes through.
        /// </para>
        /// </summary>
        private const BallType ROCK_TINT = BallType.Type11;

        /// <summary>Where the levels are written. Set once in <see cref="Main"/>, read by everything below.</summary>
        private static string _outDir;

        private static int Main(string[] args)
        {
            //The output directory is still the first PLAIN argument, exactly as it was; the flags are named so
            //a path can never be mistaken for one. See RunSagGate for what --sag costs and why it is opt-in.
            string dirArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
            bool sag = args.Any(a => a == "--sag" || a.StartsWith("--sag=", StringComparison.Ordinal));
            string[] sagOnly = args
                .Where(a => a.StartsWith("--sag=", StringComparison.Ordinal))
                .SelectMany(a => a["--sag=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            try
            {
                _outDir = dirArg ?? FindLevelsDirectory();
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }

            Console.WriteLine($"Writing to {_outDir}");

            //IN PLAY ORDER, AND IN BLOCKS (#194). A block is one scene, one dome, one music theme and one
            //statable style, and it is contiguous — a chunk of the BLOCKS table by position IS a block, which
            //is what #184's block-completion celebration needs and what a flat list could not give it. The
            //chunks were all ten levels, then #295's Eruption shipped five and #369 filled it back to ten;
            //the table carries each block's size either way.
            //
            //The campaign's light drains out of it as it goes: green noon, gold afternoon, the desert's cool
            //late light, violet dusk, underground dark, airless black — and since #182, past the black, deep
            //space. Difficulty ramps with it, and so does how much of each block is new — one new level in
            //the first block, then two, five, three, four, one at the Quarry, and five again in the Nebula,
            //which closes on the one level that plays every colour the game has.
            //
            //THE COIL IS INSERTED AT 3 RATHER THAN APPENDED (#207), and both halves of that are decisions.
            //Appending it would have put a bright hot chapter after the airless black one and taken the last
            //word off Colossus, which is the level the whole ramp is built to arrive at; slotted third, the
            //desert is the step the light was missing between the savanna's gold and the mountains' violet.
            //Nothing had to be retuned to move eleven levels down the order, because the unlock ramp
            //is a function of POSITION and not of any design — see MinStarsAt, which was written that way for
            //exactly this.
            //
            //THE NEBULA IS APPENDED, and that reverses the second half of #207's reasoning deliberately (the
            //owner's ask, #182): the campaign's last word moves off Colossus onto the Nebula's finale. The
            //first half survives intact, because the block is not a bright chapter after the airless black
            //one — space is the step PAST airless black, the ramp continuing outward rather than turning
            //back. Colossus keeps every rule it had and closes the Quarry; what it hands over is only the
            //campaign-complete moment. The Nebula's designs live in their own array below rather than in this
            //one, because WriteLevelSet appends Colossus after everything in THIS array — five designs added
            //here would land Colossus at the tail of the NEBULA's five and file Comet under the Quarry. The
            //blocks would still be contiguous (names fall out of positions, nothing refuses the set); they
            //would celebrate the wrong levels.
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
                One(), Bullseye(), Toadstool(), Pinwheel(), Diabolo(), Shuttle(), Amphora(), Saturn(), Fountain(), Gem(),

                //2. THE SAVANNA - "The Gallery". Flat drawn walls, read off a bitmap written in the source.
                Heart(), Smiley(), Star(), Elephant(), Moon(), Paw(), Meerkat(), Giraffe(), Balloon(), Zebra(),

                //3. THE DESERT - "The Coil" (#207). Every layout here hangs on SLENDER LINKS, so the cluster
                //springs and swings instead of sitting there: strands twisted round each other, a ledge winding
                //round a thin core, a woven shell, a weight on four ropes, a closed loop. The one block whose
                //style is a statement about the PHYSICS rather than about the silhouette — and the block that
                //finally plays in the desert, which no level did.
                Rope(), Minaret(), Basket(), Pendulum(), Pendant(), Web(), Crane(), Mobile(), Bridge(), Knot(),

                //4. THE MOUNTAINS - "The Tower". The layout is deeper than the camera frames, so a level's
                //length is its height and it is worked from the underside up as the glass hands it down.
                //
                //COLUMN OPENS IT since #206, where Crown did before, and that reverses the reasoning written
                //into Crown itself (see its Sky comment, rewritten with this). The block's stated style is "the
                //layout is deeper than the camera frames", and Crown is the one member that is NOT: it is the
                //only 16-level field here, framed whole. Opening on it therefore spent the chapter's first
                //level on the one that does not demonstrate what the chapter is. Column is the plainest tall
                //level in the game — a column reaching out of shot, no second idea in it — so it states the
                //block's premise in its first minute. The cost is that it is also the LARGEST budget in the
                //game (90 shots, ceiling every 5), so the chapter opens on its longest level; Crown moving to
                //second keeps its teaching intact, the axis and the drain up the middle of it reading just as
                //well behind the premise as ahead of it.
                Column(), Crown(), Horn(), Helix(), Pagoda(), Spyglass(), Belfry(), Organ(), Pylon(), Lean(),

                //5. THE CAVERN - "The Reveal". An outer body with a differently-shaped thing standing inside
                //it; clearing the outside is the payoff (#161).
                Onion(), Chest(), Fossil(), Mango(), Spark(), Grotto(), Scales(), Ship(), Spring(), Lantern(),

                //6. THE MOON - "The Quarry". Chunky lattice-aligned blocks of colour, five or six of them, and
                //no plate to trigger anywhere: every shot is a shot at a handful of balls. Colossus closes it,
                //from WriteLevelSet.
                Mosaic(), Prism(), Hopper(), Trilithon(), Gantry(), Fault(), Crib(), Highwall(), Static()
            };

            //7. THE NEBULA (#182) - the arena in deep space, and the block the five #152 colours arrive in,
            //one or two per level until the finale plays all thirteen. Every level is TALL and OPEN in the
            //Helix's sense - the silhouette turns and changes as it descends, so the player reads what is
            //coming - and each is a different KIND of tall, the Tower's own rule (#160). See the block's
            //region for why a second tall block exists at all when the Coil recorded that only the Tower
            //should be one. The block lives in its own array because WriteLevelSet appends Colossus after
            //everything in the array above: five designs added THERE would still make contiguous blocks
            //(the names fall out of positions, so nothing refuses the set) but would misfile them - Comet
            //labelled the Quarry's, Colossus labelled the Nebula's, and THE QUARRY COMPLETE celebrating on
            //the wrong level. Only DescribeBlock's non-gating MIXED print would show it.
            Design[] nebula = { Comet(), Vortex(), Carousel(), Wishbone(), Sail(), Analemma(), Binary(), Kepler(), Orrery(), Garland() };

            //8. THE VOLCANO - "The Eruption" (#295). THE GLOW IS THE LOAD: the molten seams are what
            //everything hangs by, so reading where a level shines is reading where it will break - and every
            //level here HAPPENED IN A DIRECTION, a bearing the shape carries (the torn flank, the downhill
            //run, the downwind rake, the leaning column). The arc job is the light returning after the void,
            //GEOLOGICALLY - the earth glowing by itself - one step before the dawn hands the light back
            //received (and two before the finale's neon, since #300 put the cities in day order). Five
            //levels until #369, which filled the block to ten with the five that bring the BOMB and the ZAP
            //into the campaign (#368) - the volcano being the one place a bomb does not have to explain
            //itself. See the block's own region for the statement in full and for the engineering law every
            //design here obeys (a designed breakaway is always the lowest thing on its own load path).
            Design[] volcano =
                { Breach(), Causeway(), Meander(), Volley(), Plume(), Vent(), Sill(), Fume(), Caldera(), Paroxysm() };

            //9. THE CITY AT DAWN - "The Spectrum" (#253). One HUE FAMILY a level, swept through the whole
            //body as a gradient: white to cyan to blue to navy and back, a heat ramp, a green one, a twilight
            //one, and the wheel entire on the finale. No new colour is involved anywhere - a family is a
            //subset and an ordering of the fixed thirteen - and no sweep is a stack of floors, because a
            //floor of one colour is the anchor of everything under it and would end the level on one ball.
            //(#302 added BANDED TIERS to four of these without breaking that sentence - see the block's
            //geometry region header for the rule a tier obeys.)
            //The light ramp's return COMPLETES here: after the volcano's geological glow, this is light
            //RECEIVED again - dawn breaking over the city where the arena has always stood, its neon still
            //off. It closed the campaign until #300 moved it ahead of the Arcade, on the owner's ruling that
            //the neon city reads as coming AFTER the normal one - which it does, the way a day does: the
            //campaign's last stretch is now ONE DAY IN THIS CITY, dawn to night.
            //
            //THE ORDER WITHIN THE BLOCK IS THE SHIPPED PLAY ORDER, AND IT STOPPED BEING THE MEASURED
            //DIFFICULTY RAMP WITH #302. It was slotted by standing-group count (6, 8, 9, 10, 15, 14, 19, 22,
            //26, 31 when #255's five were interleaved), and #302 is the record of why that figure ranks the
            //wrong quantity twice over: it says nothing about whether the remainder HANGS (the sag probe's
            //question - four of these ten lost to gravity before the ceiling moved), and on a design with
            //tiers it prices shots that cascade (one cleared tier orphans everything below it, so Pleat's
            //probe runs clear on a third of the budget). Re-measured after the tiers the counts run 6, 17,
            //9, 35, 15, 19, 26, 22, 26 and 31 - Pleat's 35 at fourth position outranks the Turbine - so the
            //order no longer tracks the count, deliberately. The two ends still stand: the Icicle opens,
            //being the plainest body in it, and the Turbine still closes the BLOCK - the campaign it closed
            //until #300 now ends a chapter later. The families are not what ramps - a green level is no
            //harder than a blue one.
            //Its designs live in their own array for the same reason the Nebula's and the Arcade's do - see
            //WriteLevelSet.
            Design[] spectrum = { Icicle(), Pinecone(), Hourglass(), Pleat(), Trellis(), Bolt(), Totem(), Kiln(), Girandole(), Turbine() };

            //10. THE NEON CITY - "The Arcade" - THE CAMPAIGN'S LAST BLOCK since #300. Five HOLLOW pixel-art
            //solids: the Gallery's drawn symbols given a third dimension, wrapped onto a die, a stepped
            //temple, a slot reel, a donut and a globe, so a level's picture is read by walking the gun round
            //it. Every one is framed whole, which is the deliberate opposite of the tall blocks before it -
            //an object meant to be RECOGNISED has to be in shot.
            //The light ramp ends where the day does. It used to end on the Spectrum's dawn, with the neon
            //city justified as the only lit place reachable straight after the void ("past the void there is
            //no darker place to go"); #295's volcano then gave the return a geological first step and #300
            //swapped the two cities on the owner's ruling. Read in play order the ending is now: dawn breaks
            //over the city (the Spectrum), the day passes through ten hue families, and the campaign ends
            //AFTER DARK with the city turning its own lights on - the made light as the finale's celebration,
            //over the same skyline, where the arena has always stood. The last word rides the set's last
            //entry, so it moves off Turbine onto Globe - and the block that measures tightest (1.33-1.65 a
            //group against the Spectrum's 1.37-6.67) now sits last, which is #300's other half: the campaign
            //climaxes where it ends.
            //Its designs live in their own array for the same reason the Nebula's do - see WriteLevelSet.
            Design[] arcade = { Cube(), Ziggurat(), Reel(), Donut(), Ghost(), Cabinet(), Tetra(), Giza(), Trophy(), Globe() };

            //11. THE DREAM - "The Mirage" (#323/#325), THE CAMPAIGN'S LAST BLOCK, and the first chapter in
            //the game whose subject is a RULE rather than a shape. Ten levels, and they are two fives: the
            //first five hang the TRANSPARENT ball - the shell with no colour in it until a shot lands beside
            //it and gives it one - and the last five hang the ROCK, which takes no colour ever and which no
            //shot removes. ONE NEW KIND A LEVEL, never both, which is the owner's own instruction and the
            //reason the block is ten and not five: a player meeting two new rules in one level learns
            //neither.
            //
            //THE GLASS COMES FIRST, also the owner's call, and it is the right way round on the ramp as well.
            //The transparent ball is the GENEROUS rule - one shot into a pocket of glass pays several times
            //over, and the five levels here are built to pay - where the rock is the rule that says no. A
            //chapter that opened on the ball you cannot shoot would be a chapter that opened by taking
            //something away.
            //
            //THE SCENE IS THE DREAM, the one fully-built backdrop no shipped level had ever named (the note
            //in docs/scenes.md that said so is retired by this block). It is not a leftover: the dream's own
            //signature is hard glassy solids tumbling and melting into one another under a sky of slow
            //marbled colour, so the chapter about a ball that is glass until it becomes a colour plays in
            //the one place where that is what the BACKDROP is already doing. The dome number is inert here -
            //the dream replaces the sky and states its own light rig - and every level names the same one
            //anyway so DescribeBlock has something to agree with.
            //
            //THE LIGHT RAMP does not continue, and that is deliberate rather than an oversight. The campaign
            //walked the day down from green noon to the neon city after dark (see the Arcade), and the day
            //ENDED there. What follows a day is not a later hour of it; the eleventh chapter is the one
            //place the arena is not, and the balls stop obeying the rules the other hundred levels taught.
            //Its designs live in their own array for the reason the Nebula's, the Eruption's, the Spectrum's
            //and the Arcade's do - see WriteLevelSet.
            Design[] mirage =
            {
                Facet(), Trefoil(), Harlequin(), Diadem(), Solitaire(),
                Anvil(), Seam(), Keystone(), Cairn(), Obsidian(),
            };

            bool ok = true;
            foreach (Design design in designs) ok &= Emit(design);
            foreach (Design design in nebula) ok &= Emit(design);
            foreach (Design design in volcano) ok &= Emit(design);
            foreach (Design design in spectrum) ok &= Emit(design);
            foreach (Design design in arcade) ok &= Emit(design);
            foreach (Design design in mirage) ok &= Emit(design);

            LevelSet set = WriteLevelSet(designs, nebula, volcano, spectrum, arcade, mirage);

            //The gate that hangs the levels instead of reading them (#301/#302). Off the WRITTEN SET rather
            //than off the designs above, for two reasons: the set is where a level's budget and ceiling step
            //actually live, and it is the only list that includes the hand-drawn Colossus - a shipped level
            //this gate has as much business asking about as any generated one.
            if (sag) ok &= RunSagGate(set, sagOnly);

            //A non-zero exit so this can be put in front of a commit: a level that fails the checks is a
            //level that plays wrong, and the whole point of generating them is that nobody has to notice
            if (!ok) Console.WriteLine("At least one level FAILED its checks - see above.");

            return ok ? 0 : 1;
        }

        /// <summary>
        /// Hangs every level of the set in the real simulation and plays it, printing what the death line had
        /// to say — see <see cref="SagProbe"/> for what it does and why nothing else in this tool could have
        /// caught what it catches.
        /// <para>
        /// <b>It is opt-in (<c>--sag</c>), and unlike everything else here it REFUSES NOTHING.</b> Two
        /// separate reasons, and only the first is about cost. The other gates read a layout and cost
        /// milliseconds, so they run on every invocation and stand in front of a commit; this one steps a
        /// nine-hundred-body simulation through whole levels several ways, which is minutes for the pack.
        /// <c>--sag=Pylon,Orrery</c> narrows it to the levels named, which is the form to use while
        /// iterating on one, and turns on a shot-by-shot trace when it is given exactly one.
        /// </para>
        /// <para>
        /// <b>⚠ The second reason is that its threshold is fitted to twelve levels.</b> The owner's playtest
        /// gives three levels known finishable (<see cref="Amphora"/>, <see cref="Giza"/>,
        /// <see cref="Saturn"/>) against the nine known not to be, and
        /// <see cref="SagProbe.SAG_RUNS_TO_REPORT"/> separates them cleanly on that set — seven of the nine
        /// named, none of the three. That is a real calibration and it is still twelve levels, so a run of
        /// this prints a <i>ranking</i> and refuses nothing: the levels it names are where to look first, not
        /// a verdict to redraw a design on. Doing that off an uncalibrated number is #288's mistake with a
        /// better tool, and the whole of this file exists because that mistake was made once.
        /// </para>
        /// </summary>
        /// <returns>
        /// Always true. The signature is kept so the day this is calibrated it can start refusing without the
        /// call site moving — and it is written out rather than made void so that nobody has to guess whether
        /// a silent exit code meant the levels passed.
        /// </returns>
        private static bool RunSagGate(LevelSet set, string[] only)
        {
            Console.WriteLine();
            Console.WriteLine("=== sag probe: every level hung in the real simulation and played ===");
            Console.WriteLine("    (clearance = how far the lowest ball stayed above the death line;"
                              + " negative is under it and still inside the swing allowance)");
            Console.WriteLine("    ⚠ A RANKING, NOT A VERDICT - it refuses nothing and fails nothing."
                              + " See RunSagGate's doc for what it is not yet entitled to decide.");

            bool ok = true;

            for (int i = 0; i < set.Count; i++)
            {
                LevelSetEntry entry = set.Levels[i];

                if (only.Length > 0 && !only.Any(name =>
                        entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;

                string path = Path.Combine(_outDir, entry.File);

                //A missing file IS reported as a failure, and it is the one thing here that can be: it is a
                //fact about the disk rather than a verdict about a design.
                if (!File.Exists(path))
                {
                    Console.WriteLine($"  {i + 1,2}. {entry.Name,-12} MISSING - {entry.File} is not on disk");
                    ok = false;
                    continue;
                }

                //Both are optional in the format and both absences mean "no pressure": no budget, and a glass
                //that holds still. A run with no budget still ends - the probe stops when nothing matchable is
                //left standing - so the cap can honestly be the largest there is.
                int shots = entry.Shots ?? int.MaxValue;
                int ceilingStep = entry.CeilingStep ?? 0;

                //Narrowed to one level, the shot-by-shot comes with it: a verdict says a level sags, and only
                //the trace says at which cut, which is the form an author can act on.
                SagProbe.Run[] runs = SagProbe.Play(path, shots, ceilingStep, trace: only.Length == 1);
                SagProbe.Run worst = SagProbe.Worst(runs);
                int sags = runs.Count(r => r.Outcome == SagProbe.Outcome.Sagged);

                //HOW OFTEN, not whether - the fraction is the reading, because "some order loses this level"
                //turned out to be true of levels that play perfectly well. The threshold is calibrated against
                //the owner's playtest (SagProbe.SAG_RUNS_TO_REPORT); it does NOT set `ok`, because a threshold
                //fitted to twelve levels is not yet entitled to refuse a design - see this method's doc.
                bool sagged = sags >= SagProbe.SAG_RUNS_TO_REPORT;

                Console.WriteLine($"  {i + 1,2}. {entry.Name,-12} sagged {sags} of {runs.Length}; worst: "
                    + $"{worst.Outcome,-11} after {worst.Shots,3} shot(s) of "
                    + $"{(entry.Shots.HasValue ? entry.Shots.Value.ToString() : "∞"),3}"
                    + $", closest the line came {worst.WorstClearance,6:F2}"
                    + (sagged
                        //Which pressure ended it, because that is the distinction #288 got the wrong way
                        //round: a level that sags with the glass still at rest is a LAYOUT fault, and no
                        //value of ceilingStep can reach it.
                        ? worst.CeilingHadMoved
                            ? "  <-- SAGGED (the glass had stepped)"
                            : "  <-- SAGGED WITH THE GLASS AT REST - a layout fault"
                        : string.Empty));
            }

            return ok;
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
        /// Rewrites the set that orders the levels — since #194 as the <b>blocks of the <see cref="BLOCKS"/>
        /// table</b> rather than one flat ramp of fourteen, six of them since #207 added the desert, seven
        /// since #182 appended the Nebula, and ten since the Arcade, #253's Spectrum and #295's Eruption.
        /// <b>One opens the campaign, Colossus closes the Quarry, and the campaign closes on the last
        /// block's finale</b> — the campaign-complete celebration rides the set's last entry and moves with
        /// it: Colossus's until #182, Garland's then (the owner moved the last word deliberately), Globe's,
        /// Turbine's at #253, and Globe's again since #300 put the two cities in day order. One is a
        /// design here now (the author asked for it regenerated, see <see cref="One"/>) and states its own rules
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
        private static LevelSet WriteLevelSet(Design[] designs, params Design[][] blocksAfterColossus)
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
                        MinStars = MinStarsAt(index),
                    });
                }

            string path = Path.Combine(_outDir, LevelSet.DefaultFileName);
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
                Level level = Level.Load(Path.Combine(_outDir, set.Levels[i].File));

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

                        //What the ball IS, beside what colour it is (#323/#325). Resolved AFTER the colour
                        //and never instead of it: the cell carries both, and a glass ball simply ignores the
                        //colour it was given - see Design.Kind.
                        BallKind kind =
                            design.Kind != null ? design.Kind(r, ang, i, depth)
                            : design.BlockKind != null ? design.BlockKind(x, z, i, depth)
                            : BallKind.Normal;

                        //A ROCK'S COLOUR IS NOT THE DESIGN'S TO CHOOSE, and forcing it here is the only place
                        //that can be true of every rock at once - see ROCK_TINT, and the way it first shipped.
                        if (kind == BallKind.Rock) type = ROCK_TINT;

                        //The position the ball will actually occupy in the raw grid frame, so the stored
                        //one agrees with what PutBallAt recomputes at load rather than merely being ignored
                        Vector3 position = BallsMap.GetRealPosition(x, z, fieldLevel);

                        balls[x, z, i] = new BallPositionType
                        {
                            PositionX = position.X,
                            PositionY = position.Y,
                            PositionZ = position.Z,
                            Type = type,
                            Kind = kind,
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
                Balls = design.Balls,
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
        /// <para>
        /// <b>⚠ It skips every ball that is not <see cref="BallKind.Normal"/>, and both halves of that matter
        /// (#323/#325).</b> A rock and a glass ball have no colour group by definition — <see cref="BallsMap.
        /// GetConnectedSameTypeCells"/> returns an EMPTY list for either, which is a group of 0 and reads here
        /// as the worst lonely ball in the level — so without the skip this pass would have tried to repair
        /// every special in the Mirage block. And the repair is <c>PutBallAt</c>, whose <c>kind</c> parameter
        /// <b>defaults to Normal</b>: the "repair" would have turned each of them into an ordinary coloured
        /// ball, silently, with the level file written from the result and nothing anywhere saying so. The
        /// neighbour scan skips them too, for the plainer reason that a colour a rock is carrying is not a
        /// colour anything can match.
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
                            if (!BallKinds.Matchable(array[x, z, l].Kind)) continue;

                            XZLevel cell = new(x, z, l);
                            if (map.GetConnectedSameTypeCells(cell).Count >= MIN_GROUP) continue;

                            BallType best = array[x, z, l].Type;
                            int bestGroup = 0;

                            //⚠ THE KIND HAS TO BE CARRIED THROUGH EVERY PutBallAt BELOW (#331), and this is
                            //exactly #325's recorded data loss arriving through the opposite door. That one
                            //was "a special is not matchable, so the repair must skip it"; this is "a special
                            //IS matchable" — an infectious ball is an ordinary ball of its colour that is
                            //sick, so it passes the guard above and belongs in the repair — and PutBallAt's
                            //`kind` parameter defaults to Normal, so every recolour here would silently CURE
                            //it, with the level file written from the result. The trial recolours below do it
                            //too: the ball must still be sick while the group is measured, or a sick ball's
                            //group is measured on a field the repair has already changed.
                            BallKind kind = array[x, z, l].Kind;

                            //Every colour standing next to it is a candidate; the one that leaves it in the
                            //biggest group wins. Measured by actually recolouring and asking, because the
                            //answer depends on what those neighbours are themselves connected to.
                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            {
                                StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                                if (other == null || !BallKinds.Matchable(other.Kind) || other.Type == best) continue;

                                map.PutBallAt(x, z, l, other.Type, kind);
                                int group = map.GetConnectedSameTypeCells(cell).Count;

                                if (group > bestGroup) { bestGroup = group; best = other.Type; }
                            }

                            map.PutBallAt(x, z, l, best, kind);
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
            //⚠ ACIDS AND FROZEN BALLS COUNT TOWARDS ASKING THE WALK BELOW, and the acid's line is a fix
            //rather than an addition (#329): #328 added the kind without adding it here, so a level built of
            //acid and colour alone would have skipped the stranded-specials walk entirely — which is #343's
            //own bug, arriving through the next kind. Anything not matchable belongs in this list.
            int rocks = 0, glass = 0, bombs = 0, zaps = 0, acids = 0, frozen = 0, infectious = 0, wells = 0;

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        StaticBall ball = array[x, z, l];
                        if (ball == null) continue;

                        if (ball.Kind == BallKind.Rock) rocks++;
                        if (ball.Kind == BallKind.Transparent) glass++;
                        if (ball.Kind == BallKind.Bomb) bombs++;
                        if (ball.Kind == BallKind.Zap) zaps++;
                        if (ball.Kind == BallKind.Acid) acids++;
                        if (ball.Kind == BallKind.Frozen) frozen++;
                        //⚠ BallKind.Infectious is deliberately NOT in this list (#331), and it is the one
                        //exception to the rule the line above states ("anything not matchable belongs here").
                        //It is the first special that IS matchable: a sick ball is an ordinary ball of its
                        //colour that can be shot out like any other, so it needs none of what the stranded
                        //walk asks — no empty cell to land beside, no landing to reach it. Its own gate is
                        //the anchor-course one inside that walk, and its real gate is the sag probe, which
                        //plays the level and watches it rot.
                        if (ball.Kind == BallKind.Infectious) infectious++;
                        //A gravity well is matchable too (#332), so it is here for the same reason and with
                        //the same exception: it needs nothing the stranded walk asks, but the walk holds its
                        //own refusal for it.
                        if (ball.Kind == BallKind.Gravity) wells++;

                        //⚠ THE COLOUR CENSUS IS OVER THE MATCHABLE BALLS ONLY (#323/#325), and it is not
                        //merely tidier: a rock or a glass ball counted here would enter `counts` under the
                        //colour its cell happens to carry and NEVER enter `largestGroup`, since its own
                        //group is empty and the `group > g` test therefore never fires — and the report loop
                        //below indexes largestGroup by every key of counts. A specials-only colour was a
                        //KeyNotFoundException, not a wrong number.
                        if (!BallKinds.Matchable(ball.Kind)) continue;

                        counts.TryGetValue(ball.Type, out int c);
                        counts[ball.Type] = c + 1;

                        int group = map.GetConnectedSameTypeCells(new XZLevel(x, z, l)).Count;
                        largestGroup.TryGetValue(ball.Type, out int g);
                        if (group > g) largestGroup[ball.Type] = group;
                    }

            long fileSize = new FileInfo(path).Length;

            //The music is echoed back off the LOADED level rather than off the design, like the scene and the
            //dome beside it: a theme that failed to reach the file is a level that plays the wrong piece and
            //nothing else says so — the fallback is silent by design (see Design.Music). The ball style is
            //echoed for the same reason and it is the same silence: a style that failed to reach the file is a
            //level drawn in the wrong material, and absent reads as the vinyl ball rather than as an error.
            Console.WriteLine($"--- {design.File} '{loaded.Name}' ({loaded.Scene?.ToString() ?? "(none)"}, sky {loaded.SkyDome}"
                              + $", {loaded.Music ?? "no theme named"}"
                              + $", {BallStyles.ToName(loaded.Balls ?? BallStyle.Beach)} balls) {fileSize / 1024} kB");
            Console.WriteLine($"    field {map.StageSizeX}x{map.StageSizeZ}x{map.Levels}, layout {design.Depth} deep, "
                              + $"{map.GetBallsCount()} balls, lowest occupied level {map.GetLowestOccupiedLevel()}");
            Console.WriteLine($"    hanging off the glass: {(disconnected == 0 ? "all" : $"NO - {disconnected} balls float free")}");

            //THE HANGING TOTAL and THE PLAYABLE ONE, and since #323 they are two numbers rather than one.
            //Everything the glass carries weighs on it, rocks included, so the anchor load is priced off the
            //whole cluster; but a rock never comes down to a shot, so every figure about what a SHOT is
            //worth — balls a shot at par, and the one-shot percentage — is priced off what can be removed.
            //On a level with no specials in it the two are equal and every figure reads exactly as it did.
            int total = map.GetBallsCount();
            int removable = map.GetRemovableBallsCount();
            int matchable = total - rocks - glass - bombs - zaps;

            if (rocks > 0 || glass > 0 || bombs > 0 || zaps > 0)
                Console.WriteLine($"    specials: {rocks} rock(s) that never match and never fall to a shot,"
                                  + $" {glass} glass ball(s) waiting for a colour, {bombs} live bomb(s),"
                                  + $" {zaps} zap(s) — {matchable} of {total} balls are matchable as they hang");

            int margin = LateralMargin(map);
            Console.WriteLine($"    lateral margin: {(margin >= 1 ? $"{margin} free cell(s) all round" : "NONE - the layout is ON the field wall")}");

            int groups = CountGroups(map);
            Console.WriteLine($"    {groups} standing colour groups, i.e. {removable / (float)groups:F1} balls a shot at par"
                              + $" — the budget is {design.Shots / (float)groups:F2} shots per group");

            //What the glass carries, and the worst one shot can leave it carrying (#301/#302). Two figures on
            //one line because neither means much alone: the anchor count is the level's own hanging width and
            //the load is what a shot does to it.
            var anchorLoad = WorstAnchorLoad(loaded.Map);
            Console.WriteLine($"    {CountCeilingAnchors(map)} ceiling anchors carrying {total} balls"
                              + $" ({total / (float)CountCeilingAnchors(map):F1} each); worst single shot leaves"
                              + $" {anchorLoad.Standing} on {anchorLoad.Anchors} — anchor load {anchorLoad.Load:F1}");

            LonelyReport lonely = FindLonelyBalls(map);
            Console.WriteLine($"    reachable in one ball: {(lonely.Alone == 0 ? "all" : $"NO - {lonely.Alone} STAND ALONE")}"
                              + $" (in pairs {lonely.Paired}, primed {matchable - lonely.Alone - lonely.Paired})"
                              + $", {repaired} recoloured by the repair pass");
            foreach (string where in lonely.Examples) Console.WriteLine($"      {where}");

            //The specials' own reachability question, asked only of a level that has one in it — see
            //FindStrandedSpecials for what it refuses and why nothing else here could have refused it.
            //
            //⚠ THE ROCKS COUNT TOWARDS ASKING IT (#343). While this read `glass + bombs`, a level built
            //entirely of stone and colour skipped the walk altogether, so the one gate that would have
            //caught a rock hanging off the ceiling was never run on the three levels that had one.
            int specials = rocks + glass + bombs + zaps + acids + frozen + infectious + wells;

            StrandedReport stranded = specials == 0 ? new StrandedReport() : FindStrandedSpecials(map);

            if (specials > 0)
            {
                Console.WriteLine($"    specials a shot can reach: {(stranded.Walled == 0 ? "all" : $"NO - {stranded.Walled} WALLED IN")}"
                                  + $" (glass against the ceiling {stranded.Anchoring}, best glass landing pays"
                                  + $" {stranded.MostAtOnce})");
                Console.WriteLine($"    glass in bodies of two or more: {(stranded.AloneGlass == 0 ? "all" : $"NO - {stranded.AloneGlass} ALONE")}");
                Console.WriteLine($"    rocks the player can bring down: {(stranded.CeilingRocks == 0 ? "all" : $"NO - {stranded.CeilingRocks} ON THE ANCHOR COURSE")}");
                Console.WriteLine($"    ice a group can ever reach: {(stranded.SealedIce == 0 ? "all" : $"NO - {stranded.SealedIce} SEALED IN")}");
                Console.WriteLine($"    infection off the anchor course: {(stranded.CeilingInfection == 0 ? "all" : $"NO - {stranded.CeilingInfection} ON THE ANCHOR COURSE")}");
                Console.WriteLine($"    wells a shot can fly near: {(stranded.BuriedWells == 0 ? "all" : $"NO - {stranded.BuriedWells} BURIED")}");
                foreach (string where in stranded.Examples) Console.WriteLine($"      {where}");
            }

            bool oneShot = false;

            foreach (var pair in counts.OrderBy(p => p.Key))
            {
                int dropped = DropTest(loaded.Map, pair.Key);
                int percent = removable == 0 ? 0 : dropped * 100 / removable;

                if (percent >= ONE_SHOT_PERCENT) oneShot = true;

                Console.WriteLine($"    {pair.Key,-6} {pair.Value,4} balls, largest standing group {largestGroup[pair.Key],4}"
                                  + (largestGroup[pair.Key] >= 3 ? "  primed" : "  <-- NOT PRIMED")
                                  + $", best single shot drops {dropped,4} ({percent,3}%)"
                                  + (percent >= ONE_SHOT_PERCENT ? "  <-- ONE-SHOT LEVEL" : string.Empty));
            }

            return disconnected == 0 && lonely.Alone == 0 && !oneShot && margin >= 1
                   && stranded.Walled == 0 && stranded.Anchoring == 0 && stranded.CeilingRocks == 0
                   && stranded.AloneGlass == 0 && stranded.SealedIce == 0 && stranded.CeilingInfection == 0 && stranded.BuriedWells == 0;
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
        /// How many standing colour groups the level is made of — <b>the number a shot budget is priced
        /// against</b>, because one landed ball takes exactly one group with it (plus whatever that group was
        /// the last anchor for, which is what makes a design with cascades cheaper than this count says).
        /// <para>
        /// It is printed as a ratio for that reason. The pack runs from <b>Colossus at 0.98 shots a group</b>
        /// (364 balls in 46 groups against 45 shots — measured off its file, since this tool does not write
        /// it), which is the hardest level in the game and is meant to be, to <see cref="Horn"/>'s 20, where
        /// four shells mean the budget is not the thing being fought at all. A design landing near 1 with no
        /// cascade in it is a level that has to be played perfectly; the Arcade's five are priced off this
        /// line, from 1.65 down to 1.37, and every one of them was over-tight before it was measured.
        /// </para>
        /// </summary>
        private static int CountGroups(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            bool[,,] counted = new bool[map.StageSizeX, map.StageSizeZ, map.Levels];
            int groups = 0;

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        if (array[x, z, l] == null || counted[x, z, l]) continue;

                        //A rock and an uncoloured glass ball are not a colour group and must not be priced as
                        //one (#323/#325). They would each have counted as their OWN group, too, and never been
                        //struck off: GetConnectedSameTypeCells returns an empty list for either, so the loop
                        //below marks nothing. On Cairn's 96 rocks that is 96 phantom groups, and the budget
                        //ratio this figure exists to price would have read a third of what it is.
                        if (!BallKinds.Matchable(array[x, z, l].Kind)) continue;

                        groups++;
                        foreach (XZLevel cell in map.GetConnectedSameTypeCells(new XZLevel(x, z, l)))
                            counted[cell.X, cell.Z, cell.Level] = true;
                    }

            return groups;
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

                        //Only a ball the player can MATCH can stand alone in this sense. A rock is never
                        //matched at all and a glass ball has no colour to be lonely in until a shot gives it
                        //one — see FindStrandedSpecials for the question that IS worth asking about the glass,
                        //which is a question about the empty cells around it and not about its colour.
                        if (!BallKinds.Matchable(array[x, z, l].Kind)) continue;

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
        /// The question <see cref="FindLonelyBalls"/> cannot ask about a special that has no colour, and the
        /// gate the eleventh block needed (#325, widened by #326). Glass and a bomb both have no colour group
        /// and nothing above can say anything about either; what they have instead is <b>an empty cell beside
        /// them or nothing</b>, because the only thing that removes either is a shot landing in one.
        /// <para>
        /// <b>WALLED IN</b> — such a ball with no empty neighbour at all. No shot can land beside it, so it is
        /// never coloured and never detonated; and both kinds are <see cref="BallKinds.Removable"/>, so they
        /// still count against the level being cleared. Every other gate here passes such a level: it does not
        /// float, it does not stand alone (it has no group to stand alone in), the lateral margin is whatever
        /// the body's is, and no colour one-shots it. It simply cannot be finished.
        /// </para>
        /// <para>
        /// <b>The test is a PROPERTY and not a list of kinds</b> — removable but not matchable, which is
        /// exactly "a ball a landing beside it has to reach". Transparent and Bomb answer it today; the next
        /// special that does is gated the day it exists rather than the day somebody remembers this file.
        /// </para>
        /// <para>
        /// <b>⚠ This is a design law rather than a solvability proof, and the difference is worth stating.</b>
        /// It reads the level <i>as it hangs</i>, and a buried glass ball could in principle become reachable
        /// later, once the body around it has been cleared away. That argument is true and it is refused
        /// anyway: a ball the player cannot see, cannot reach and cannot plan around is not a puzzle element
        /// on the shot it is buried, and the block's own rule — <b>glass goes on the skin</b>
        /// (<see cref="MirageSkin"/>) — means no design here ever wants to make that argument. A design that
        /// genuinely does can widen this check; it should not quietly pass it.
        /// </para>
        /// <para>
        /// <b>ANCHORING</b> — a glass ball in the field's topmost level, which is the only level
        /// <c>BallsConstraintsBuilder</c> bonds to the ceiling plate. Glass there is a ceiling anchor that
        /// <i>dissolves</i>: one shot beside it colours it, the colour takes it, and the cluster's hanging
        /// width drops with no warning to the player and no colour on the ball to have warned them. That is
        /// #301/#302's failure mode with the one ingredient those issues did not have — invisibility — so the
        /// glass is kept off the anchor course by refusal rather than by care.
        /// <para>
        /// <b>⚠ ANCHORING stays GLASS-only, and a bomb on the anchor course is deliberately allowed</b> (#326). A
        /// bomb up there costs the ceiling far more than a glass ball does — its own socket and every other
        /// one within the blast — but the whole of what made the glass case a refusal was that it gave
        /// <i>no warning</i>: a colourless ball says nothing about being load-bearing and nothing about being
        /// about to leave. A bomb is a dark ribbed casing with a charge burning in it and it only ever goes
        /// off because the player aimed at it. That is a trap the player can see and choose, which is a
        /// design, where the glass one is a design the player cannot read. It reads at the stand-off a level
        /// is played from as well as close up — that took a round of widening the glowing bands and slowing
        /// their blink, and the measurements are on <c>BallRenderSet.BOMB_EMISSION</c>.
        /// </para>
        /// </para>
        /// <para>
        /// <b>A ROCK ON THE ANCHOR COURSE</b> (#343) — the same cell, the opposite kind, and the one case
        /// here that is refused for the player's sake rather than the ceiling's. A rock matches with nothing
        /// and no colour removes it; it leaves the cluster only when the last thing holding it up is cut. On
        /// the topmost level the thing holding it up is <b>the ceiling plate itself</b>, and that socket is
        /// never cut by anything the player can do — so a rock up there is a ball that stays on the field
        /// for the whole level no matter how well it is played.
        /// <para>
        /// <b>⚠ This retracts a line of #324, and the retraction is the owner's.</b> #324 listed "a ceiling
        /// anchor that cannot be cut" among the rock's legitimate uses, and this file argued the same thing
        /// in this very paragraph: a stone anchor is one no shot can take, so it only ever <i>improves</i>
        /// what <see cref="WorstAnchorLoad"/> measures. Both are true and both were answering the wrong
        /// question. An anchor that cannot be cut is a good deal for the <i>cluster</i> and a bad one for the
        /// <i>player</i>, who is left looking at a ball they can never do anything about — and three shipped
        /// levels were built on it (<see cref="Seam"/> 26 anchors, <see cref="Cairn"/> 44,
        /// <see cref="Obsidian"/> 58).
        /// </para>
        /// <para>
        /// <b>The remedy the block already had is <see cref="Keystone"/>'s</b>: stone stops one course short
        /// of the glass. It costs no cell and no silhouette — those cells stay occupied and simply carry a
        /// colour instead of granite — so what changes is the flood fill at the top course and nothing about
        /// the shape.
        /// </para>
        /// <para>
        /// <b>⚠ A bomb within blast radius of such a rock WOULD take it down</b>
        /// (<c>BallsConstraintsBuilder.DetonateBombs</c> makes a victim of any cell in reach whatever its
        /// kind), so a rock beside a bomb on the anchor course is not literally permanent. It is refused
        /// anyway, on this check's own standing rule: it reads the level <b>as it hangs</b>, and a removal
        /// path that depends on the player choosing to detonate one particular bomb is not the guarantee
        /// #343 asked for. A design that genuinely wants that pairing can widen this check; it should not
        /// quietly pass it.
        /// </para>
        /// </para>
        /// <para>
        /// <b>The best glass landing</b> is reported and gates nothing: the most glass one shot can colour,
        /// which since #344 is the size of the connected BODIES a landing reaches and no longer the count of
        /// panes touching it (<c>BallsMap.ColourTransparentGroup</c> is seeded from every transparent
        /// neighbour of the landing cell and then runs through the glass). Two bodies round one cell both go,
        /// so they are summed; two panes of one body are one payment. It is the design's payoff figure — and
        /// the number to read when a Mirage level feels flat — and it got much larger the day the rule changed,
        /// which is the point of the rule.
        /// </para>
        /// <para>
        /// <b>ALONE (#344), and it gates.</b> A landing that colours exactly ONE ball leaves that ball and the
        /// shot as a pair — two of a colour where three is the minimum — so the player has to come back and
        /// hit the same cell again before anything happens. That is <see cref="FindLonelyBalls"/>'s chore
        /// arriving through a different door, and that check cannot see it: glass has no colour to be lonely
        /// in until a shot gives it one.
        /// </para>
        /// <para>
        /// <b>⚠ IT IS ASKED OF THE LANDING AND NOT OF THE PANE, and the difference is a shipped level.</b>
        /// #344 states the rule as "transparent balls should only ever be generated in connected groups of two
        /// or more, never alone", on the reasoning that a lone pane can never be part of a match on the shot
        /// that colours it. <b>The Facet disproves that reasoning</b>: its clear rim is a one-cell taxicab ring,
        /// so its panes touch nothing — every one of them is a body of one — and the design turns exactly that
        /// into its teaching move, because a cell one step outside a diagonal ring touches the ring TWICE. Two
        /// panes go at once, the shot makes three, and the group completes on the landing that made it. A
        /// literal reading of the rule would have refused 36 of the Facet's 64 panes and destroyed a design
        /// whose own header explains why it is built that way. So what is checked is the property the rule was
        /// reaching for — <b>no pane may be coloured alone</b> — which refuses the truly isolated pane and
        /// leaves the Facet standing.
        /// </para>
        /// </summary>
        private static StrandedReport FindStrandedSpecials(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            XZLevel size = new(map.StageSizeX, map.StageSizeZ, map.Levels);
            byte top = (byte)(map.Levels - 1);

            StrandedReport report = new();

            //THE GLASS BODIES, labelled before anything else is asked, because #344 turned both questions
            //about the glass into questions about the BODY rather than about the ball. A landing colours
            //everything connected to what it touches, so "how much does one shot pay" is the size of the
            //bodies it reaches, and "is this pane any use" is whether its body is a single pane.
            int[,,] body = LabelGlassBodies(array, size, out List<int> bodySize);

            //Marked from the EMPTY side of the walk and counted after it, because the fault belongs to a body
            //of glass but is only visible from the cells a shot can land in. Per BODY and not per pane,
            //because a body is coloured all at once: every pane of it shares whatever its landings pay.
            bool[] bodyHasLanding = new bool[bodySize.Count];
            bool[] bodyPaysTwo = new bool[bodySize.Count];

            for (byte l = 0; l < map.Levels; l++)
                for (byte x = 0; x < map.StageSizeX; x++)
                    for (byte z = 0; z < map.StageSizeZ; z++)
                    {
                        XZLevel cell = new(x, z, l);
                        StaticBall ball = array[x, z, l];

                        //The landing pockets are the EMPTY cells, so the two halves of this walk look at
                        //opposite things: an occupied cell is asked whether it is glass, an empty one is
                        //asked how much glass a shot into it would colour.
                        if (ball == null)
                        {
                            int pays = 0;
                            bool attaches = false;

                            //A cell has at most twelve neighbours (four on its own level, up to four on each
                            //of the two adjacent ones), so the seen-list is a handful of ints on the stack.
                            Span<int> seen = stackalloc int[12];
                            int seenCount = 0;

                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            {
                                StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                                if (other == null) continue;

                                attaches = true;

                                //Whole bodies, counted once each: two panes of the same body round one
                                //landing are one payment, and two DIFFERENT bodies round it are two - the
                                //colouring reaches both, because it is seeded from every transparent
                                //neighbour of the cell before it starts walking.
                                int id = body[neighbour.X, neighbour.Z, neighbour.Level];
                                if (id == 0) continue;

                                bool already = false;
                                for (int s = 0; s < seenCount; s++)
                                    if (seen[s] == id) { already = true; break; }

                                if (already) continue;

                                seen[seenCount++] = id;
                                pays += bodySize[id - 1];
                            }

                            //A cell with nothing beside it is not a pocket, it is open air: a shot there
                            //would sail through and land somewhere else.
                            if (!attaches) continue;

                            if (pays > report.MostAtOnce) report.MostAtOnce = pays;

                            //WHAT THIS LANDING PAYS, recorded against every body it reaches. Two is the
                            //number that matters: colouring two balls makes three with the shot, all three
                            //connected through the cell the shot is standing in, so the group completes on
                            //the landing that made it. Colouring one leaves a pair and the player has to come
                            //back — which is a worse shot, not a broken level, unless it is the ONLY shot
                            //that body has.
                            for (int s = 0; s < seenCount; s++)
                            {
                                bodyHasLanding[seen[s] - 1] = true;
                                if (pays >= 2) bodyPaysTwo[seen[s] - 1] = true;
                            }

                            continue;
                        }

                        //THE ROCK'S CASE IS ASKED FIRST, because the guard below is what would swallow it
                        //(#343): a rock is the one kind that answers NO to Removable, so every line after
                        //that guard is unreachable for it. What is asked here is also the opposite question
                        //from the rest of this walk — not "can a shot reach this ball" but "can the player
                        //ever be rid of it" — and on the anchor course the answer is no by construction.
                        if (l == top && ball.Kind == BallKind.Rock)
                        {
                            report.CeilingRocks++;

                            if (report.Examples.Count < 3)
                                report.Examples.Add($"rock on the anchor course at cell ({x},{z}) on level {l}"
                                                    + ": nothing the player does can ever cut it down");
                        }

                        //⚠ THE INFECTION'S OWN REFUSAL, AND IT IS ASKED BEFORE THE MATCHABLE GUARD BELOW
                        //(#331) — for the reason the rock's is asked before the removable one, and it is the
                        //same shape of gap: a sick ball IS matchable, so every line after that guard is
                        //unreachable for it.
                        //
                        //A sick ball on the anchor course hardens into stone THERE on the very first tick,
                        //and #343 already settled what a rock on that course is: a ball hanging off a socket
                        //nothing the player can do will ever cut, i.e. one that is on the field for the whole
                        //level whatever they play. The difference from an authored rock is that this one
                        //arrives with no player agency at all — it is a fait accompli handed over by the
                        //author, on shot one. Lower down the field it is fair: the infection climbs, so it
                        //reaches the anchor course eventually, and how many shots the player has before it
                        //does is exactly the game being played.
                        if (l == top && ball.Kind == BallKind.Infectious)
                        {
                            report.CeilingInfection++;

                            if (report.Examples.Count < 3)
                                report.Examples.Add($"infectious ball on the anchor course at cell ({x},{z})"
                                                    + $" on level {l}: it hardens into stone up there on the"
                                                    + " first tick, and nothing the player does cuts it down");
                        }

                        //⚠ AND THE WELL'S OWN REFUSAL (#332), asked here for the frozen ball's and the sick
                        //ball's reason: a gravity well IS matchable, so the guard below is where it would be
                        //lost. A well BURIED IN THE BODY is a well no shot ever passes near — its field
                        //reaches GravityWells.RANGE, and inside a solid cluster there is no line through that
                        //sphere for a shot to fly along. It bends nothing, so it is a special that does
                        //nothing, and a mechanic nobody can meet does not read as one (#344's own verdict
                        //about a glass ball alone among its neighbours, arriving for the other kind of
                        //special). What is asked is whether the well has a free cell within its own reach —
                        //not merely beside it, because a shot needs a corridor and not a doorstep.
                        if (ball.Kind == BallKind.Gravity && !HasOpenSpaceWithin(array, size, cell,
                                Prazsky.BS3D.Physics.GravityWells.RANGE))
                        {
                            report.BuriedWells++;

                            if (report.Examples.Count < 3)
                                report.Examples.Add($"gravity well buried at cell ({x},{z}) on level {l}:"
                                                    + " no open space inside its own reach, so no shot ever"
                                                    + " passes near enough to be bent by it");
                        }

                        //A ball a landing beside it has to reach, stated as the property rather than as a
                        //list of kinds: removable, so it holds the level open, and not matchable, so no
                        //colour can take it. Transparent and Bomb both answer it; the rock answers no to the
                        //first half and an ordinary ball to the second.
                        if (BallKinds.Matchable(ball.Kind) || !BallKinds.Removable(ball.Kind)) continue;

                        //⚠ THE FROZEN BALL IS ASKED A DIFFERENT QUESTION AND THEN LEAVES THIS WALK (#329),
                        //because the question below is the wrong one for it and would have passed it in
                        //silence. Everything else here is opened by a shot LANDING beside it, so "is there an
                        //empty neighbour to shoot into" is the whole test. Ice is opened by a GROUP being
                        //cleared beside it — an empty cell next to a frozen ball buys nothing at all if
                        //nothing next to it can ever be matched.
                        //
                        //So what is asked is whether it has a matchable neighbour. Glass counts as one: a
                        //landing colours it and from that instant it is an ordinary ball that can complete a
                        //group. This is deliberately the CERTAIN half of the question — a frozen ball with no
                        //matchable neighbour can never thaw, on any level, in any order of play, and that is
                        //a level that never ends. Whether a reachable group actually gets completed in the
                        //shots the player has is the DYNAMIC half, and it is not guessed at here: SagProbe
                        //plays the level for real and thaws through the same ReleaseSameTypeCluster the game
                        //does, so a level whose ice only opens late is measured rather than estimated.
                        if (ball.Kind == BallKind.Frozen)
                        {
                            bool thawable = false;

                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            {
                                StaticBall beside = array[neighbour.X, neighbour.Z, neighbour.Level];

                                if (beside == null) continue;
                                if (BallKinds.Matchable(beside.Kind) || beside.Kind == BallKind.Transparent)
                                {
                                    thawable = true;
                                    break;
                                }
                            }

                            if (thawable) continue;

                            report.SealedIce++;

                            if (report.Examples.Count < 3)
                                report.Examples.Add($"frozen ball sealed in at cell ({x},{z}) on level {l}:"
                                                    + " no matchable neighbour, so no group can ever be"
                                                    + " cleared beside it and the ice never breaks");

                            continue;
                        }

                        if (l == top && ball.Kind == BallKind.Transparent)
                        {
                            report.Anchoring++;

                            if (report.Examples.Count < 3)
                                report.Examples.Add($"glass on the anchor course at cell ({x},{z}) on level {l}");
                        }


                        bool reachable = false;

                        foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                            if (array[neighbour.X, neighbour.Z, neighbour.Level] == null) { reachable = true; break; }

                        if (reachable) continue;

                        report.Walled++;

                        if (report.Examples.Count < 3)
                            report.Examples.Add($"{BallKinds.ToName(ball.Kind)} walled in at cell ({x},{z})"
                                                + $" on level {l}: no empty neighbour to shoot into");
                    }

            //A body every one of whose landings pays one is a body that can only ever be traded for a pair.
            //Only a body of ONE can be in this state — anything larger pays its own size to any landing that
            //touches it — so this is #344's "never alone" stated as what it is worth rather than as what it
            //looks like. A body with no landing at all is the WALLED case above and is already counted there.
            for (int b = 0; b < bodySize.Count; b++)
            {
                if (!bodyHasLanding[b] || bodyPaysTwo[b]) continue;

                report.AloneGlass += bodySize[b];

                if (report.Examples.Count < 3)
                    report.Examples.Add($"{bodySize[b]} glass ball(s) whose every landing pays one:"
                                        + " the shot and it are a pair, so nothing completes and the player"
                                        + " has to come back to the same cell");
            }

            return report;
        }

        /// <summary>
        /// Whether any cell within <paramref name="reach"/> world units of <paramref name="from"/> is empty —
        /// the question a gravity well is refused on (#332).
        /// <para>
        /// <b>Space inside its reach, not a free neighbour.</b> Every other special here is opened by a shot
        /// landing beside it, so a doorstep is enough; a well is met by a shot flying THROUGH its field, which
        /// needs a corridor. Measured in world units against <c>GravityWells.RANGE</c> rather than in cells,
        /// for the reason <c>BLAST_RADIUS</c> states: the lattice is anisotropic, so a cell count is a
        /// different distance in each axis and the field is a sphere.
        /// </para>
        /// </summary>
        private static bool HasOpenSpaceWithin(StaticBall[,,] array, XZLevel size, XZLevel from, float reach)
        {
            Vector3 centre = BallsMap.GetRealPosition((byte)from.X, (byte)from.Z, (byte)from.Level);
            float reachSquared = reach * reach;

            //A cell is 1.0 across at its widest here, so the index box that can hold the sphere is the reach
            //rounded up in each axis — generous in the vertical, where levels are 1/sqrt(2) apart, and the
            //distance test below does the real work.
            int span = (int)MathF.Ceiling(reach) + 1;

            for (int l = Math.Max(0, from.Level - span); l < Math.Min(size.Level, from.Level + span + 1); l++)
                for (int x = Math.Max(0, from.X - span); x < Math.Min(size.X, from.X + span + 1); x++)
                    for (int z = Math.Max(0, from.Z - span); z < Math.Min(size.Z, from.Z + span + 1); z++)
                    {
                        if (array[x, z, l] != null) continue;

                        if (Vector3.DistanceSquared(centre,
                                BallsMap.GetRealPosition((byte)x, (byte)z, (byte)l)) <= reachSquared)
                            return true;
                    }

            return false;
        }

        private sealed class StrandedReport
        {
            public int Walled;
            public int Anchoring;

            /// <summary>Rocks on the field's topmost level — see the ANCHORING paragraph above (#343).</summary>
            public int CeilingRocks;

            /// <summary>Panes some landing would colour by themselves — see the ALONE paragraph (#344).</summary>
            public int AloneGlass;

            /// <summary>
            /// <summary>
            /// Gravity wells with no open space inside their own reach (#332): a well no shot can fly near is
            /// a special that bends nothing. See the well's branch in <see cref="FindStrandedSpecials"/>.
            /// </summary>
            public int BuriedWells;

            /// <summary>
            /// Infectious balls on the field's topmost level (#331): stone on the anchor course from the first
            /// tick, which is #343's refusal arriving with no player agency at all. Asked ahead of the
            /// matchable guard in <see cref="FindStrandedSpecials"/>, since a sick ball is matchable.
            /// </summary>
            public int CeilingInfection;

            /// <summary>
            /// Frozen balls with no matchable neighbour (#329): ice no group can ever be cleared beside, so it
            /// never thaws and the level never ends. See the frozen branch in <see cref="FindStrandedSpecials"/>
            /// for why it is a question of its own rather than the walled-in one.
            /// </summary>
            public int SealedIce;

            public int MostAtOnce;
            public readonly List<string> Examples = new();
        }

        /// <summary>
        /// Numbers the connected bodies of <see cref="BallKind.Transparent"/> balls, one id per body starting
        /// at 1, and reports each body's size. Cell 0 means "not glass", so the array's own default is the
        /// answer for every other cell and nothing has to be pre-filled.
        /// <para>
        /// This is <c>BallsMap.ColourTransparentGroup</c>'s walk asked offline: the same neighbour relation
        /// over the same kind, which is what makes a body here exactly the set one landing turns into one
        /// colour. Written with an explicit stack rather than recursion because a body can be most of a level
        /// — the Solitaire's whole surface is one — and because this runs once per level per pack.
        /// </para>
        /// </summary>
        private static int[,,] LabelGlassBodies(StaticBall[,,] array, XZLevel size, out List<int> bodySize)
        {
            int[,,] body = new int[size.X, size.Z, size.Level];
            bodySize = new List<int>();

            Stack<XZLevel> toVisit = new();

            for (byte l = 0; l < size.Level; l++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                    {
                        if (body[x, z, l] != 0) continue;
                        if (array[x, z, l] == null || array[x, z, l].Kind != BallKind.Transparent) continue;

                        int id = bodySize.Count + 1;
                        int count = 0;

                        body[x, z, l] = id;
                        toVisit.Push(new XZLevel(x, z, l));

                        while (toVisit.Count > 0)
                        {
                            XZLevel at = toVisit.Pop();
                            count++;

                            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(at, size))
                            {
                                if (body[neighbour.X, neighbour.Z, neighbour.Level] != 0) continue;

                                StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                                if (other == null || other.Kind != BallKind.Transparent) continue;

                                body[neighbour.X, neighbour.Z, neighbour.Level] = id;
                                toVisit.Push(neighbour);
                            }
                        }

                        bodySize.Add(count);
                    }

            return body;
        }

        /// <summary>
        /// How many balls the best single shot of one colour would bring down: the standing group of that
        /// colour whose removal brings the most with it, plus everything that group was the last anchor for —
        /// <b>and, since #362, the best landing into the glass as well</b>, which on a level with a large
        /// clear body is a different and much larger number. This is the figure that decides whether a design
        /// is a level or a firework — a colour that drops the whole cluster means the anchor layer is one
        /// colour, and the level is over on the first lucky ball.
        /// </summary>
        /// <remarks>
        /// <b>EVERY group of the colour is tried, not merely the largest one (#98).</b> What is wanted here is
        /// how much comes DOWN, and that is not monotonic in group size: a small group can be the cluster's
        /// last anchor while a much larger one merely hangs off it. Testing only the largest also had to break
        /// ties arbitrarily — the scan kept the first group it found of the winning size — and <c>Column</c>
        /// shipped behind exactly that hole. Its anchor course was 21 balls of a single colour and its top two
        /// courses one 45-ball group, so <b>one shot took all 540 balls</b>; this test measured one of the two
        /// OTHER 45-ball groups of the same colour and reported a comfortable 33 %.
        /// <para>
        /// A fresh map per group, because the test destroys the one it measures and <see cref="BallsMap"/> has
        /// no clone. Its constructor builds its own array out of the data rather than aliasing it, so the
        /// caller's <paramref name="data"/> survives all of them. This is an offline tool and the cost is a
        /// rebuild per standing group, which is nothing against being told a level is safe when it is not.
        /// </para>
        /// <para>
        /// <b>⚠ THE STANDING-GROUP HALF ALONE WAS BLIND ON EVERY GLASS LEVEL, and #362 is where that was
        /// measured rather than reasoned about.</b> #344 made a landing colour the whole connected body of
        /// glass, so on <c>Solitaire</c> one shot turns 116 of 444 balls into one colour and takes them —
        /// while this test, modelling groups that are standing in the layout as authored, reported <i>"best
        /// single shot drops 22 (4 %)"</i>. A gate whose percentage is out by that factor is worse than no
        /// gate, because it is quoted. So <see cref="GlassLandingDrop"/> plays the landing instead of reading
        /// it, and the answer here is the larger of the two paths.
        /// </para>
        /// </remarks>
        private static int DropTest(BallPositionTypes data, BallType type)
        {
            BallsMap census = new(data);
            StaticBall[,,] array = census.GetStaticBallsArray();

            //The colour's distinct standing groups, collected once. GetConnectedSameTypeCells answers for a
            //CELL, so every member of a group it returns is struck off before the scan moves on.
            List<List<XZLevel>> groups = new();
            HashSet<int> claimed = new();

            int Key(XZLevel cell) => (cell.Level * census.StageSizeX + cell.X) * census.StageSizeZ + cell.Z;

            for (byte l = 0; l < census.Levels; l++)
                for (byte x = 0; x < census.StageSizeX; x++)
                    for (byte z = 0; z < census.StageSizeZ; z++)
                    {
                        if (array[x, z, l]?.Type != type) continue;

                        XZLevel cell = new(x, z, l);
                        if (claimed.Contains(Key(cell))) continue;

                        List<XZLevel> group = census.GetConnectedSameTypeCells(cell);
                        foreach (XZLevel member in group) claimed.Add(Key(member));

                        groups.Add(group);
                    }

            int worst = 0;

            foreach (List<XZLevel> group in groups)
            {
                BallsMap map = new(data);
                foreach (XZLevel cell in group) map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

                int dropped = group.Count + map.GetCellsDisconnectedFromCeiling().Count;
                if (dropped > worst) worst = dropped;
            }

            return Math.Max(worst, GlassLandingDrop(data, type));
        }

        /// <summary>
        /// How many balls the best landing <b>into the glass</b> would bring down for one colour: the shot
        /// colours whole connected bodies of <see cref="BallKind.Transparent"/> (#344), and whatever that
        /// makes of the colour then leaves by the ordinary match rule, along with everything it was holding
        /// up. Zero on a level with no glass in it, which is all but five of the set.
        /// </summary>
        /// <remarks>
        /// <b>It plays the landing rather than reading the layout, and it plays it in the handler's order</b>,
        /// which is the only way the two can agree: the ball is <i>put</i> in the cell, then
        /// <see cref="BallsMap.ColourTransparentGroup"/> runs, and only then is the group counted — exactly
        /// <c>BallContactEventHandler</c>'s sequence, and for its stated reason (colour before the count, or a
        /// shot that completes a group <i>through</i> the glass is not credited with it).
        /// <para>
        /// <b>What is counted is what leaves the CLUSTER</b>, so the shot's own ball is subtracted: it was
        /// never hanging there, and the percentage this feeds is priced against
        /// <c>GetRemovableBallsCount</c>. A landing whose group falls short of
        /// <c>BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE</c> takes nothing at all — the glass is coloured
        /// and stays hanging, which is a worse shot rather than a payout.
        /// </para>
        /// <para>
        /// <b>Only pockets that touch glass are tried</b>, and only pockets that touch <i>something</i> —
        /// a cell with nothing beside it is open air a shot would sail through, the same rule
        /// <see cref="FindStrandedSpecials"/> applies to its own walk. Every remaining empty cell is tried for
        /// every colour the level uses, which is a few thousand map rebuilds on the largest glass level and
        /// costs about a second: this is an offline gate, and #362 is what a cheaper one missed.
        /// </para>
        /// </remarks>
        private static int GlassLandingDrop(BallPositionTypes data, BallType type)
        {
            BallsMap census = new(data);
            StaticBall[,,] array = census.GetStaticBallsArray();
            XZLevel size = new(census.StageSizeX, census.StageSizeZ, census.Levels);

            int worst = 0;
            List<XZLevel> coloured = new();

            for (byte l = 0; l < census.Levels; l++)
                for (byte x = 0; x < census.StageSizeX; x++)
                    for (byte z = 0; z < census.StageSizeZ; z++)
                    {
                        if (array[x, z, l] != null) continue;

                        XZLevel cell = new(x, z, l);
                        bool touchesGlass = false;

                        foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(cell, size))
                        {
                            StaticBall other = array[neighbour.X, neighbour.Z, neighbour.Level];
                            if (other != null && other.Kind == BallKind.Transparent) { touchesGlass = true; break; }
                        }

                        if (!touchesGlass) continue;

                        BallsMap map = new(data);
                        map.PutBallAt(x, z, l, type);
                        map.ColourTransparentGroup(cell, type, coloured);

                        List<XZLevel> group = map.GetConnectedSameTypeCells(cell);
                        if (group.Count < BallsConstraintsBuilder.MINIMUM_CLUSTER_SIZE) continue;

                        foreach (XZLevel member in group)
                            map.RemoveBallAt((byte)member.X, (byte)member.Z, (byte)member.Level);

                        //Less the shot's own ball: the group includes the cell the shot is standing in, and
                        //that one never hung from the ceiling to begin with.
                        int dropped = group.Count - 1 + map.GetCellsDisconnectedFromCeiling().Count;
                        if (dropped > worst) worst = dropped;
                    }

            return worst;
        }

        /// <summary>
        /// How many balls each of the level's ceiling anchors carries, <b>after the single shot that leaves
        /// the worst such figure</b> — the load the layout puts on the glass, and the quantity #301 and #302
        /// turned out to be about.
        /// <para>
        /// <b>Only the field's topmost level is bonded to the glass</b> (<c>BallsConstraintsBuilder</c>'s
        /// build pass), so a level's entire mass hangs from however many cells its own top course happens to
        /// occupy. <see cref="DropTest"/> already asks what a shot <i>orphans</i>, and that is a question
        /// about the lattice: a cell still joined to the top level by any path passes it. This asks the other
        /// half, which is a question about <i>weight</i> — a shot that takes six of a level's twenty anchors
        /// orphans nothing at all and leaves 473 balls hanging on fourteen sockets.
        /// </para>
        /// <para>
        /// <b>⚠ It was measured, not guessed, and the instrument was <see cref="SagProbe"/>.</b> Hanging
        /// <see cref="Amphora"/> in the real simulation and shooting one group off it dropped the ceiling
        /// links from 20 to 14 with <i>nothing</i> orphaned, and the vase then descended five and a half
        /// units in a second — through a death line it had started four and a half above. Every gate this
        /// tool had passed that level, and passes it still: nothing floats, nothing stands alone, the best
        /// single shot takes 12 %.
        /// </para>
        /// <para>
        /// It is a ratio rather than a count because both halves matter: twenty anchors are generous under a
        /// 200-ball level and thin under a 900-ball one. The pack's own spread is what says where the line
        /// falls — see the <c>anchor load</c> column the validator prints, and the table in
        /// <c>docs/formats-and-tools.md</c>.
        /// </para>
        /// </summary>
        /// <returns>Balls per surviving anchor at the worst single shot, and the anchor count it leaves.</returns>
        private static (float Load, int Anchors, int Standing) WorstAnchorLoad(BallPositionTypes data)
        {
            BallsMap whole = new(data);
            StaticBall[,,] array = whole.GetStaticBallsArray();

            //Every distinct standing group of every colour, the walk DropTest makes - and for the same
            //reason it makes it over all of them rather than the largest (#98): how much a shot costs is not
            //monotonic in group size, and a small group can be the one holding the glass.
            List<List<XZLevel>> groups = new();
            HashSet<int> claimed = new();

            int Key(XZLevel cell) => (cell.Level * whole.StageSizeX + cell.X) * whole.StageSizeZ + cell.Z;

            for (byte l = 0; l < whole.Levels; l++)
                for (byte x = 0; x < whole.StageSizeX; x++)
                    for (byte z = 0; z < whole.StageSizeZ; z++)
                    {
                        if (array[x, z, l] == null) continue;

                        XZLevel cell = new(x, z, l);
                        if (claimed.Contains(Key(cell))) continue;

                        List<XZLevel> group = whole.GetConnectedSameTypeCells(cell);
                        foreach (XZLevel member in group) claimed.Add(Key(member));

                        if (group.Count >= MIN_GROUP) groups.Add(group);
                    }

            float worst = 0f;
            int worstAnchors = 0;
            int worstStanding = 0;

            foreach (List<XZLevel> group in groups)
            {
                BallsMap map = new(data);
                foreach (XZLevel cell in group) map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

                //What falls, falls - the orphans go with the group and neither weighs on the glass afterwards
                foreach (XZLevel cell in map.GetCellsDisconnectedFromCeiling())
                    map.RemoveBallAt((byte)cell.X, (byte)cell.Z, (byte)cell.Level);

                int anchors = CountCeilingAnchors(map);
                int standing = map.GetBallsCount();

                //A cleared field hangs nothing and is not a load; it is the drop test's business, not this one.
                if (standing == 0) continue;

                //No anchors under standing balls cannot happen - GetCellsDisconnectedFromCeiling has just
                //emptied exactly that case - so the division is safe, and the guard is here to say so.
                if (anchors == 0) continue;

                float load = standing / (float)anchors;
                if (load <= worst) continue;

                worst = load;
                worstAnchors = anchors;
                worstStanding = standing;
            }

            return (worst, worstAnchors, worstStanding);
        }

        /// <summary>
        /// How many cells of the field's topmost level are occupied — <b>every socket the whole cluster hangs
        /// from</b>, because that is the only level <c>BallsConstraintsBuilder.BuildBallsStructure</c> bonds
        /// to the glass.
        /// </summary>
        private static int CountCeilingAnchors(BallsMap map)
        {
            StaticBall[,,] array = map.GetStaticBallsArray();
            byte top = (byte)(map.Levels - 1);
            int anchors = 0;

            for (byte x = 0; x < map.StageSizeX; x++)
                for (byte z = 0; z < map.StageSizeZ; z++)
                    if (array[x, z, top] != null) anchors++;

            return anchors;
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

            /// <summary>
            /// Which scene the level plays in — the whole of what a level says about its backdrop, the
            /// scenes' parameters being fixed in code (level format 2). A block's five designs all name the
            /// same one; <see cref="DescribeBlock"/> is what reports it if they do not. Note the default is
            /// <see cref="SceneKind.City"/> rather than "unset", so a design that forgets to name a scene
            /// gets the city — the printout is where that shows.
            /// </summary>
            public SceneKind Scene;
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
            /// the five names are <c>pulse</c>, <c>bohemia</c>, <c>nocturne</c>, <c>mural</c> (#264's
            /// bass-led groove, which replaced the polka) and <c>ember</c> (#163's rock ballad, which the
            /// Coil took in #207).
            /// </para>
            /// </summary>
            public string Music;

            /// <summary>
            /// What the level's balls are made of, written into <c>Level.Balls</c> (#258) — and a property of
            /// the <b>block</b> for <see cref="Music"/>'s reason: the material changes when the chapter does,
            /// not when the level does. <see cref="DescribeBlock"/> reports it and says so when a block's ten
            /// disagree.
            /// <para>
            /// <b>Every block states one now</b>, and each states a different one — see the table of
            /// <c>BALLS_*</c> constants at the top of this file for which chapter hangs what and why. It was
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
            /// <see cref="MIRAGE_ROCK_TINT"/> for what the rocks are given and why it is not one of the
            /// colours the level plays.
            /// </para>
            /// </summary>
            public Func<float, float, int, int, BallKind> Kind;

            /// <inheritdoc cref="Kind"/>
            public Func<int, int, int, int, BallKind> BlockKind;
        }
    }
}
