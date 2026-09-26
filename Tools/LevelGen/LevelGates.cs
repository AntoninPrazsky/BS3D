using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// Every check a written level is read back through before it may ship — <see cref="Validate"/> and the
    /// gates it calls. They read the level <b>file</b> the way the game does, never the design that wrote it, and
    /// they are functions of a <c>BallsMap</c> or a <c>BallPositionTypes</c> alone: nothing here knows which
    /// campaign a level belongs to. See <see cref="Program"/>'s own doc for what they enforce and why. Split
    /// out of <c>Program.cs</c> in #597, unchanged.
    /// </summary>
    internal static class LevelGates
    {
        /// <summary>
        /// The smallest standing group a ball may belong to. It is <c>MINIMUM_CLUSTER_SIZE</c> less the one
        /// ball the player is about to land: a pair plus a shot is three and falls, a lone ball plus a shot
        /// is two and does nothing. Stated here rather than read off the physics constant because it is a
        /// statement about <i>authoring</i>, and the arithmetic linking them is the point of it.
        /// </summary>
        internal const int MIN_GROUP = 3 - 1;

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
        /// <b><see cref="Program.One"/> is the design that has visited both ends of this band, and which end it sits
        /// at is decided by where the colour boundaries run.</b> Banded a course a colour it topped the pack
        /// outright at <b>74 %</b>, because a course of a pyramid is load-bearing — everything under it hangs
        /// off it, so the band that goes takes the tip down with it. Turned onto the pyramid's <i>walls</i>
        /// instead (#234's third pass) the same shape, the same three colours and the same 385 balls measure
        /// <b>30 %</b>: a shell hangs off the plate it reaches and not off the shell inside it, so nothing
        /// rides a wall down. The figure moved by 44 points with no change to the geometry at all.
        /// </para>
        /// <para>
        /// The bottom of the band is where the dial is turned the other way: <see cref="Program.Zebra"/> at 9 % and
        /// <see cref="Program.Elephant"/> at 21 % are pictures drawn in two and three inks, so their symbols are not one
        /// group at all, and <see cref="Program.Lantern"/> at 6 % is a wall of panes with no plate anywhere. That is the
        /// whole difficulty ramp of #194's blocks, stated in one column of numbers.
        /// </para>
        /// </summary>
        private const int ONE_SHOT_PERCENT = 90;

        /// <summary>Whether <c>--clear</c> was asked for: the shortest-clear gate then reports a beam line as
        /// well as its floor (#458). It changes what is printed and never what is refused — see
        /// <see cref="ClearProbe"/>.</summary>
        internal static bool DeepClear;

        /// <summary>
        /// Reads the file back the way the game does and reports what it actually got. A design is only
        /// worth shipping if the loader agrees with it, every ball hangs off the glass, and every colour
        /// has somewhere to be matched.
        /// </summary>
        /// <returns>
        /// Whether the level passes all three: nothing floating free of the glass, no ball standing alone,
        /// and no colour whose best single shot is the whole cluster.
        /// </returns>
        internal static bool Validate(Design design, string path, int repaired)
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
            int rocks = 0, glass = 0, bombs = 0, zaps = 0, acids = 0, frozen = 0, infectious = 0, wells = 0,
                heavies = 0;

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
                        //A heavy ball is matchable as well (#333), so it is here on the well's terms exactly:
                        //it needs nothing the stranded walk asks of a colourless special, and the walk holds a
                        //refusal of its own for it — a mass with nothing under it.
                        if (ball.Kind == BallKind.Heavy) heavies++;

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
            int specials = rocks + glass + bombs + zaps + acids + frozen + infectious + wells + heavies;

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
                Console.WriteLine($"    heavy balls with a load: {(stranded.InertHeavy == 0 ? "all" : $"NO - {stranded.InertHeavy} CARRY NOTHING")}");
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

            //HOW FEW SHOTS EMPTY IT (#458), and it is the question every line above turns its back on: each of
            //them reads ONE cut, and Saturn's fault was the second one. See ClearProbe for what a move is, what
            //it deliberately does not play, and why the anchor course's colour count answers most of the pack
            //before a move is played.
            ClearProbe.Reading clear = ClearProbe.Measure(loaded.Map, DeepClear);
            Console.WriteLine($"    shortest clear: {clear.Describe()}"
                              + (clear.TooCheap
                                  ? $"  <-- MATCHED AWAY IN UNDER {ClearProbe.MINIMUM_CLEAR_SHOTS} SHOTS"
                                  : string.Empty));

            return disconnected == 0 && lonely.Alone == 0 && !oneShot && margin >= 1
                   && stranded.Walled == 0 && stranded.Anchoring == 0 && stranded.CeilingRocks == 0
                   && stranded.AloneGlass == 0 && stranded.SealedIce == 0 && stranded.CeilingInfection == 0
                   && stranded.BuriedWells == 0 && stranded.InertHeavy == 0 && !clear.TooCheap;
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
        /// it), which is the hardest level in the game and is meant to be, to <see cref="Program.Horn"/>'s 20, where
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
        /// levels were built on it (<see cref="Program.Seam"/> 26 anchors, <see cref="Program.Cairn"/> 44,
        /// <see cref="Program.Obsidian"/> 58).
        /// </para>
        /// <para>
        /// <b>The remedy the block already had is <see cref="Program.Keystone"/>'s</b>: stone stops one course short
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

            //A cell has at most twelve neighbours (four on its own level, up to four on each of the two
            //adjacent ones), so the seen-list is a handful of ints on the stack - allocated ONCE, here, and
            //reset per cell by its count (#588). A stackalloc inside the walk below is released only on return,
            //so it grew the frame by 48 B for every empty cell (CA2014) and a field about 2.4x the Organ's would
            //have overflowed the stack: an uncatchable kill with no message.
            Span<int> seen = stackalloc int[BallsMap.MAX_NEIGHBORS];

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

                        //⚠ AND THE HEAVY BALL'S OWN REFUSAL (#333), asked here for the third time running and
                        //for the same reason: a heavy ball IS matchable, so the guard below is where it would
                        //be lost. It is the WELL'S refusal turned upside down. A well needs open space around
                        //it or no shot ever flies near enough to be bent; a heavy ball needs BALLS UNDER IT or
                        //there is nothing for its mass to pull on. What the kind does is make what hangs off
                        //it hang lower — that is the whole mechanic, and the physics is the only thing that
                        //ever says so — and a heavy ball with an empty lattice beneath it says it to nothing.
                        //It is then an ordinary ball that costs the solver a mass ratio, which is a mechanic
                        //nobody can meet (#344's verdict once more, arriving through the tenth kind).
                        //
                        //The test is one course down and not the whole load path below it, deliberately: a
                        //single ball hanging off a heavy one already droops visibly (the figures are on
                        //BallsConstraintsBuilder.HEAVY_MASS_RATIO), so anything stricter would be this gate
                        //deciding how much droop is enough — which is a design question and belongs to the
                        //sag probe, where it is measured rather than asserted.
                        if (ball.Kind == BallKind.Heavy && l > 0 && !HasBallBelow(array, size, cell))
                        {
                            report.InertHeavy++;

                            if (report.Examples.Count < 3)
                                report.Examples.Add($"heavy ball carrying nothing at cell ({x},{z}) on level"
                                                    + $" {l}: the lattice under it is empty, so its mass has"
                                                    + " nothing to pull down and the kind is invisible");
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

        /// <summary>
        /// Whether the cell has a ball on the course <b>below</b> it, i.e. whether anything hangs off it at all
        /// (#333). The heavy ball's own question, and the cheapest honest form of it: level is the vertical
        /// axis and the cluster hangs from the top, so one course down is where a mass's load goes.
        /// <para>
        /// It asks the neighbour walk rather than the cell straight underneath, because the lattice is packed
        /// hexagonally — odd levels are shifted by half a cell in X and Z, so <c>[x, z, l - 1]</c> is not
        /// reliably a neighbour at all and which diagonal offsets are depends on the level's parity.
        /// <c>BallsMap.GetNeighboringCells</c> is the one place that knows it.
        /// </para>
        /// </summary>
        private static bool HasBallBelow(StaticBall[,,] array, XZLevel size, XZLevel from)
        {
            foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(from, size))
                if (neighbour.Level < from.Level && array[neighbour.X, neighbour.Z, neighbour.Level] != null)
                    return true;

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
            /// Heavy balls with an empty lattice under them (#333): a mass with nothing to pull down is a
            /// mechanic that never shows. The well's refusal turned upside down — see the heavy branch in
            /// <see cref="FindStrandedSpecials"/>.
            /// </summary>
            public int InertHeavy;

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
        /// <see cref="Program.Amphora"/> in the real simulation and shooting one group off it dropped the ceiling
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
    }
}
