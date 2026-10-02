using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using System;
using static BS3D.Tools.LevelGen.CampaignSet;

namespace BS3D.Tools.LevelGen
{
    /// <summary>
    /// <b>The Big Top</b> (#690): ten levels that hang like rope, cloth and rubber in the circus tent. Its designs, and
    /// the helpers no other block's designs use; the play order is <see cref="Main"/>'s and the block's name, music and
    /// ball style are in <see cref="CampaignSet"/>'s tables, as every other block file states.
    /// </summary>
    internal static partial class Program
    {
        #region The big top (#690)

        //THE CHAPTER'S IDEA IS THE PHYSICS, the Coil's (#207) one step further. The Coil hung its machines on slender
        //links at the builder's stiff spring, so a strand swings but does not stretch; every level here states a
        //SOFTER spring of its own (Design.Softness, the level file's "softness"), so its spans sag into catenaries,
        //bounce when they are hit and ripple along their length - the owner's "rope, cloth or rubber" (#690). The
        //prototype measured why it is per level and not per chapter: a strand is springs in series, so one figure
        //that lets a sheet breathe drops a chain through the line. Each design states the spring its own shape was
        //probed at, and the sag gate hangs it at exactly that.
        //
        //EVERY SHAPE IS DRAWN ALREADY HANGING. The menu's preview and the picker hang a level still, before any
        //simulation, so a rope drawn straight would read as a rod until the level starts. A span is drawn as the
        //catenary it will settle into, and the softness then adds the bounce and the ripple round that rest pose.
        //
        //THE FRAME AND THE PARITY RULE ARE THE COIL'S (RigCentre, RigRow): every table here is in centred indices,
        //members are two cells deep in z on the field's axis rows, and a sloped member steps one column at a time so
        //it touches the next level on both parities. Layout level 0 is the bottom of a design, Depth - 1 the course
        //against the glass.

        //NO SPAN LONGER THAN SEVEN COLUMNS BETWEEN TWO ANCHORS, and it is a rule about what is LEFT. A span cut free at
        //one end swings down and hangs from the other, and the first Tightrope's nine-column rope, let go of by one
        //post, would have hung most of the way to the line and stayed there - a loss no player could see coming from
        //the shot that caused it. Seven columns and the posts' own length keep any such remnant above the line with
        //the stretch the chapter's springs give it.

        /// <summary>The chapter's grid: odd, so the middle column is a cell, and wide enough for a span with posts.</summary>
        private const byte BIGTOP_GRID = 15;

        /// <summary>
        /// The chapter's field: two levels deeper than the standard, so every design hangs 1.4 units further above the
        /// death line. A soft span bounces below its rest pose when the level starts and again when it is hit, and the
        /// first probe of the chapter's spans had their bounce reaching the line inside the swing allowance at the
        /// standard depth. Deeper rather than a lower line (#690's open question): the line stays the rule the rest of
        /// the campaign plays by, and the room is bought where a level is drawn.
        /// </summary>
        private const byte BIGTOP_FIELD_LEVELS = 18;

        /// <summary>
        /// Inert: the big top replaces the sky and states its own light rig, so the dome number changes nothing. Every
        /// level names the same one anyway, so DescribeBlock has something to agree with - the Grid's practice.
        /// </summary>
        private const byte BIGTOP_SKY = 13;

        /// <summary>
        /// One big top level: the chapter's scene, music, material and the level's own spring round a drawing in
        /// centred indices (<paramref name="occupied"/> and <paramref name="colour"/> take cx, cz and the layout level).
        /// </summary>
        private static Design BigTop(string name, byte depth, int shots, int ceilingStep, float hz, float damping,
            Func<int, int, int, bool> occupied, Func<int, int, int, BallType> colour,
            Func<int, int, int, BallKind> kind = null, byte fieldLevels = FIELD_LEVELS, byte grid = BIGTOP_GRID)
        {
            Design design = new()
            {
                File = name + ".json",
                Name = name,
                Grid = grid,
                Depth = depth,
                FieldLevels = fieldLevels,
                Scene = SceneKind.Circus,
                Sky = BIGTOP_SKY,
                Music = MUSIC_BIGTOP,
                Balls = BALLS_BIGTOP,
                Softness = new SoftnessSpec { Frequency = hz, Damping = damping },
                Shots = shots,
                CeilingStep = ceilingStep,
                OccupiedBlock = (x, z, i, d) =>
                {
                    RigCentre(x, z, grid, out int cx, out int cz);
                    return occupied(cx, cz, i);
                },
                BlockColour = (x, z, i) =>
                {
                    RigCentre(x, z, grid, out int cx, out int cz);
                    return colour(cx, cz, i);
                },
            };

            if (kind != null)
            {
                design.BlockKind = (x, z, i, d) =>
                {
                    RigCentre(x, z, grid, out int cx, out int cz);
                    return kind(cx, cz, i);
                };
            }

            return design;
        }

        /// <summary>
        /// A post: a 2x2 column on the axis rows from the glass down to <paramref name="bottom"/>, standing at
        /// <paramref name="x0"/> and the column beside it outward.
        /// </summary>
        private static bool BigTopPost(int cx, int cz, int i, int x0, int bottom, int depth) =>
            RigRow(cz) && (cx == x0 || cx == x0 + Math.Sign(x0)) && i >= bottom && i <= depth - 1;

        /// <summary>
        /// Which diagonal of a post's 2x2 section a cell is on, 0 or 1: the Coil's trick (RigDiagonal) for a post. Each
        /// diagonal is a group of its own up the whole post, since on either parity a cell reaches its diagonal partner
        /// a level up, so a post in two inks takes two shots to cut. In one ink it took one, and a level of two posts
        /// was cleared in two shots, which the shortest-clear gate refuses.
        /// </summary>
        private static int PostDiagonal(int cx, int cz, int x0) => ((Math.Abs(cx - x0) + cz + 1) % 2 + 2) % 2;

        /// <summary>
        /// A rope two cells deep and two levels tall along x between the posts, its lower level at each column out from
        /// the middle read off <paramref name="lowAt"/> (index |cx|). Consecutive columns may step by one level only:
        /// the two-level section is what keeps a step a same-level neighbour on either parity (the Coil's Bridge cable).
        /// </summary>
        private static bool HangingRope(int cx, int cz, int i, int[] lowAt)
        {
            int a = Math.Abs(cx);
            if (!RigRow(cz) || a >= lowAt.Length) return false;
            return i == lowAt[a] || i == lowAt[a] + 1;
        }

        //--- 1. TIGHTROPE: the opener --------------------------------------------------------------------------
        //Two posts and one rope between them, drawn as the catenary it settles into. Three colours: the posts red,
        //the rope yellow at its ends and blue in the middle. The one idea the chapter teaches here is the release:
        //shoot a post's red away and the rope swings down off the other post like a trapeze let go.

        private const byte TIGHTROPE_DEPTH = 8;
        private static readonly int[] TIGHTROPE_ROPE = { 0, 0, 1, 2 };

        private static Design Tightrope() => BigTop("Tightrope", TIGHTROPE_DEPTH, shots: 20, ceilingStep: 8,
            hz: 8f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
                BigTopPost(cx, cz, i, -4, 2, TIGHTROPE_DEPTH) || BigTopPost(cx, cz, i, 4, 2, TIGHTROPE_DEPTH)
                || HangingRope(cx, cz, i, TIGHTROPE_ROPE),
            colour: (cx, cz, i) =>
            {
                int a = Math.Abs(cx);
                if (a >= 4) return PostDiagonal(cx, cz, Math.Sign(cx) * 4) == 0 ? BallType.Type1 : BallType.Type9;
                return a >= 2 ? BallType.Type7 : BallType.Type3;
            });

        //--- 2. BUNTING: two swags of pennants on three posts ---------------------------------------------------
        //A garland slung from a middle post to a post either side, a pennant hanging from each swag. A pennant hangs by
        //the rope above it, so taking that stretch of rope drops the flag with it - the cheap shot the tightrope
        //taught, read the other way round - and the middle post is what keeps each swag short enough that a cut one
        //never hangs to the line. On a grid of seventeen, so the two swags have room either side of the middle.

        private const byte BUNTING_GRID = 17;
        private const byte BUNTING_DEPTH = 10;
        private const int BUNTING_POST = 6;
        private const int BUNTING_ROPE_LOW = 6;

        /// <summary>The middle post: three columns wide, so it stands on the axis on both parities.</summary>
        private static bool BuntingMiddle(int cx, int cz, int i) => RigRow(cz) && Math.Abs(cx) <= 1 && i >= BUNTING_ROPE_LOW;

        /// <summary>
        /// A pennant under each swag, centred half a column out at ±3.5: three cells wide on the odd level under the
        /// rope, two on the even level below it, one on the odd level below that - each row centred by the lattice's
        /// half-cell shift on odd levels, which is why the cells differ on the two sides of the axis.
        /// </summary>
        private static bool BuntingFlag(int cx, int cz, int i)
        {
            if (!RigRow(cz)) return false;

            int down = BUNTING_ROPE_LOW - 1 - i;
            int a = cx >= 0 ? cx : -cx - 1;   //the negative side mirrored onto the positive one's cells

            return down switch
            {
                0 => a >= 2 && a <= 4,
                1 => cx >= 0 ? a == 3 || a == 4 : a == 2 || a == 3,
                2 => a == 3,
                _ => false,
            };
        }

        private static bool BuntingRope(int cx, int cz, int i) =>
            RigRow(cz) && Math.Abs(cx) >= 2 && Math.Abs(cx) <= BUNTING_POST - 1
            && (i == BUNTING_ROPE_LOW || i == BUNTING_ROPE_LOW + 1);

        private static Design Bunting() => BigTop("Bunting", BUNTING_DEPTH, shots: 26, ceilingStep: 8,
            hz: 8f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
                BigTopPost(cx, cz, i, -BUNTING_POST, BUNTING_ROPE_LOW, BUNTING_DEPTH)
                || BigTopPost(cx, cz, i, BUNTING_POST, BUNTING_ROPE_LOW, BUNTING_DEPTH)
                || BuntingMiddle(cx, cz, i) || BuntingRope(cx, cz, i) || BuntingFlag(cx, cz, i),
            colour: (cx, cz, i) =>
            {
                if (Math.Abs(cx) >= BUNTING_POST)
                    return PostDiagonal(cx, cz, Math.Sign(cx) * BUNTING_POST) == 0 ? BallType.Type4 : BallType.Type11;
                if (BuntingMiddle(cx, cz, i)) return (cx + 1 + cz + 1) % 2 == 0 ? BallType.Type4 : BallType.Type11;
                if (BuntingRope(cx, cz, i)) return BallType.Type5;
                return cx < 0 ? BallType.Type1 : BallType.Type7;
            },
            grid: BUNTING_GRID);

        //--- 3. HAMMOCK: a striped sheet on four posts ----------------------------------------------------------
        //Canvas slung between four corner posts, bellying in the middle, striped red and cream like the tent. Each
        //stripe is a group; a stripe taken splits the cloth along its length, and what is left sags deeper.

        private const byte HAMMOCK_DEPTH = 8;
        private const int HAMMOCK_POST = 5;

        private static int HammockLow(int cx, int cz)
        {
            //The belly: two levels deep in the middle, none at the corners, by the larger of the two offsets
            int edge = Math.Max(Math.Abs(cx), Math.Abs(cz));
            return edge >= 4 ? 4 : edge >= 2 ? 3 : 2;
        }

        private static bool HammockCorner(int cx, int cz) =>
            (Math.Abs(cx) == HAMMOCK_POST || Math.Abs(cx) == HAMMOCK_POST + 1)
            && (Math.Abs(cz) == HAMMOCK_POST || Math.Abs(cz) == HAMMOCK_POST + 1);

        private static Design Hammock() => BigTop("Hammock", HAMMOCK_DEPTH, shots: 30, ceilingStep: 8,
            hz: 11f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
            {
                if (HammockCorner(cx, cz)) return i >= 4;
                if (Math.Abs(cx) > HAMMOCK_POST || Math.Abs(cz) > HAMMOCK_POST) return false;
                int low = HammockLow(cx, cz);
                return i == low || i == low + 1;
            },
            colour: (cx, cz, i) =>
            {
                if (HammockCorner(cx, cz)) return BallType.Type7;
                int stripe = (cx + HAMMOCK_POST) / 2;
                return stripe % 2 == 0 ? BallType.Type1 : BallType.Type4;
            });

        //--- 4. SANDBAGS: buckshot on ropes ----------------------------------------------------------------------
        //A beam between two posts with three ropes hanging from it, a sandbag of buckshot at the end of each. A
        //sandbag never matches; it comes down only when the rope it hangs by is cut. Three ropes of three colours,
        //and the order they are cut in is the level.

        private const byte SANDBAGS_DEPTH = 10;
        private const int SANDBAGS_BEAM = 7;
        private static readonly int[] SANDBAGS_ROPES = { -3, 0, 3 };
        private static readonly int[] SANDBAGS_ROPE_BOTTOM = { 3, 2, 3 };

        private static int SandbagRope(int cx)
        {
            for (int r = 0; r < SANDBAGS_ROPES.Length; r++)
                if (cx == SANDBAGS_ROPES[r] || cx == SANDBAGS_ROPES[r] + 1) return r;
            return -1;
        }

        private static bool SandbagsOccupied(int cx, int cz, int i)
        {
            if (BigTopPost(cx, cz, i, -5, SANDBAGS_BEAM, SANDBAGS_DEPTH) || BigTopPost(cx, cz, i, 5, SANDBAGS_BEAM, SANDBAGS_DEPTH)) return true;
            if (!RigRow(cz)) return false;

            //The beam, two levels under the glass from post to post
            if (Math.Abs(cx) <= 4 && (i == SANDBAGS_BEAM || i == SANDBAGS_BEAM + 1)) return true;

            int rope = SandbagRope(cx);
            if (rope < 0) return false;

            //The rope down from the beam, then the bag: two levels of buckshot under it
            int bottom = SANDBAGS_ROPE_BOTTOM[rope];
            return i >= bottom - 2 && i < SANDBAGS_BEAM;
        }

        private static Design Sandbags() => BigTop("Sandbags", SANDBAGS_DEPTH, shots: 24, ceilingStep: 8,
            hz: 9f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: SandbagsOccupied,
            colour: (cx, cz, i) =>
            {
                //The posts white; the beam in three lengths, so no one colour carries the whole rig; the ropes red,
                //yellow and blue; the bags' own colour is never read (a buckshot cell carries one like any cell)
                if (Math.Abs(cx) >= 5) return PostDiagonal(cx, cz, Math.Sign(cx) * 5) == 0 ? BallType.Type4 : BallType.Type11;
                int rope = SandbagRope(cx);
                if (i < SANDBAGS_BEAM && rope >= 0)
                {
                    if (i < SANDBAGS_ROPE_BOTTOM[rope]) return BallType.Type10;
                    return rope == 0 ? BallType.Type1 : rope == 1 ? BallType.Type7 : BallType.Type3;
                }
                return Math.Abs(cx) >= 2 ? BallType.Type2 : BallType.Type9;
            },
            kind: (cx, cz, i) =>
            {
                int rope = SandbagRope(cx);
                return rope >= 0 && i < SANDBAGS_BEAM && i < SANDBAGS_ROPE_BOTTOM[rope] ? BallKind.Buckshot : BallKind.Normal;
            });

        #endregion
    }
}
