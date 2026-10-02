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
        /// Which ink a cell of a post is, 0 or 1: <b>by its row in z</b>, so each ink is one row of the post's section -
        /// cells side by side on their level and straight above one another, one connected group up the whole post on
        /// either parity. A post in two inks then takes exactly two shots to cut, and what one shot leaves is a row,
        /// two chains side by side. In one ink a post went in one shot and a level of two posts cleared in two, which
        /// the shortest-clear gate refuses.
        /// <para>
        /// ⚠ It split the posts by DIAGONAL first, the Coil's rope trick, and the review of the chapter's merge counted
        /// what that does to a post: the lattice's next level never sits at the other diagonal's offset, so one of the
        /// two inks was two separate one-cell columns - three groups a post, and two single chains of springs in
        /// series left standing once the connected ink was gone. Rows have neither fault.
        /// </para>
        /// </summary>
        private static int PostInk(int cz) => cz & 1;

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
                if (a >= 4) return PostInk(cz) == 0 ? BallType.Type1 : BallType.Type9;
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
                    return PostInk(cz) == 0 ? BallType.Type4 : BallType.Type11;
                if (BuntingMiddle(cx, cz, i)) return PostInk(cz) == 0 ? BallType.Type4 : BallType.Type11;
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
            return edge >= 4 ? 5 : edge >= 2 ? 4 : 3;
        }

        /// <summary>
        /// The posts the canvas is slung from: the four corners and the middle of the two sides the stripes run along.
        /// On the corners alone a cut stripe left each half hanging eleven cells long between two posts, and one run in
        /// five of the sag probe took it to the line; the middle posts halve that.
        /// </summary>
        private static bool HammockCorner(int cx, int cz)
        {
            bool edgeX = Math.Abs(cx) == HAMMOCK_POST || Math.Abs(cx) == HAMMOCK_POST + 1;
            bool edgeZ = Math.Abs(cz) == HAMMOCK_POST || Math.Abs(cz) == HAMMOCK_POST + 1;
            bool midX = cx == 0 || cx == -1, midZ = cz == 0 || cz == -1;

            //And since the chapter's review the middle of the other two sides too: the canvas spanned nine columns in x
            //between its two lines of posts, past the chapter's seven, so every span is now five at most
            return (edgeX && (edgeZ || midZ)) || (edgeZ && midX);
        }

        private static Design Hammock() => BigTop("Hammock", HAMMOCK_DEPTH, shots: 36, ceilingStep: 8,
            hz: 13f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
            {
                if (HammockCorner(cx, cz)) return i >= 5;
                if (Math.Abs(cx) > HAMMOCK_POST || Math.Abs(cz) > HAMMOCK_POST) return false;
                int low = HammockLow(cx, cz);
                return i == low || i == low + 1;
            },
            colour: (cx, cz, i) =>
            {
                //The posts in two inks (PostInk): in one, six shots dropped the whole canvas on a level of thirty
                if (HammockCorner(cx, cz)) return PostInk(cz) == 0 ? BallType.Type7 : BallType.Type9;
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

            //And tied to the glass at its middle too (the chapter's review): from post to post it ran nine columns, past
            //the chapter's seven, and a post cut late in the level let the beam swing its far sandbag under the line
            if (Math.Abs(cx) <= 1 && i == SANDBAGS_BEAM + 2) return true;

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
                if (Math.Abs(cx) >= 5) return PostInk(cz) == 0 ? BallType.Type4 : BallType.Type11;
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


        //--- 5. JUGGLER: five balls in the air ------------------------------------------------------------------
        //Five juggling balls caught mid-throw, each a round body of its own colour on a chain of another, hung at five
        //heights round the field. Each has a core of buckshot: take the ball's colour and the shell goes, the pellets
        //inside pour out after it - and a chain cut first takes the whole ball, pellets and all.

        private const byte JUGGLER_DEPTH = 10;

        /// <summary>The five balls: centre column and row, the level of the ball's middle, its colour and its chain's.</summary>
        private static readonly (int X, int Z, int Mid, BallType Body, BallType Chain)[] JUGGLER_BALLS =
        {
            (-4, -3, 6, BallType.Type1, BallType.Type4),
            (-3, 3, 3, BallType.Type3, BallType.Type7),
            (0, 0, 1, BallType.Type7, BallType.Type6),
            (3, -3, 4, BallType.Type2, BallType.Type1),
            (4, 3, 2, BallType.Type6, BallType.Type3),
        };

        /// <summary>Which ball a cell belongs to and whether it is the body (true) or the chain (false); -1 for neither.</summary>
        private static int JugglerPart(int cx, int cz, int i, out bool body, out bool core)
        {
            body = false;
            core = false;

            for (int b = 0; b < JUGGLER_BALLS.Length; b++)
            {
                var ball = JUGGLER_BALLS[b];
                int dx = cx - ball.X, dz = cz - ball.Z, dy = i - ball.Mid;

                //The body: three cells across and three levels tall, its eight corners off so it reads as round
                if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 && Math.Abs(dy) <= 1
                    && !(Math.Abs(dx) == 1 && Math.Abs(dz) == 1 && Math.Abs(dy) == 1))
                {
                    body = true;
                    core = dx == 0 && dz == 0 && dy == 0;
                    return b;
                }

                //The chain: two by two from the ball's top up to the glass
                if (i > ball.Mid + 1 && i <= JUGGLER_DEPTH - 1 && (dx == 0 || dx == 1) && (dz == 0 || dz == -1)) return b;
            }

            return -1;
        }

        private static Design Juggler() => BigTop("Juggler", JUGGLER_DEPTH, shots: 26, ceilingStep: 8,
            hz: 9f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) => JugglerPart(cx, cz, i, out _, out _) >= 0,
            colour: (cx, cz, i) =>
            {
                int b = JugglerPart(cx, cz, i, out bool body, out _);
                if (b < 0) return BallType.Type4;
                return body ? JUGGLER_BALLS[b].Body : JUGGLER_BALLS[b].Chain;
            },
            kind: (cx, cz, i) =>
            {
                JugglerPart(cx, cz, i, out _, out bool core);
                return core ? BallKind.Buckshot : BallKind.Normal;
            });

        //--- 6. TRAPEZE: a bar on two ropes, and the flyer hanging from it ---------------------------------------
        //The trapeze: a bar slung on two long ropes from the glass, and the flyer hanging from its middle by the hands -
        //a gold body, blue legs. Cut a rope and the bar swings down on the other like the real thing let go; the ropes
        //are two inks each, so it takes two shots to do.

        private const byte TRAPEZE_DEPTH = 10;
        private const int TRAPEZE_ROPE = 3;
        private const int TRAPEZE_BAR = 5;

        private static bool TrapezeFlyer(int cx, int cz, int i) => RigRow(cz) && Math.Abs(cx) <= 1 && i < TRAPEZE_BAR && i >= 1;

        private static Design Trapeze()
        {
            Design design = TrapezeRig();

            //THE TWO PLATFORMS (#257's crate, its first level): the boards a flyer stands on high either side of a
            //trapeze, here two crates level with the bar and out past the field's edge. Out past it so they never stand
            //in the cluster's own cells; level with the bar so a shot banked off the inside face comes back into the rig
            //from the side the player is not standing on. Straight shots pass under them to the bar and the flyer.
            design.Crates = new[]
            {
                new CrateSpec { X = -TRAPEZE_PLATFORM_X, Y = TRAPEZE_PLATFORM_Y, Z = -0.75f, Width = 3f, Height = TRAPEZE_PLATFORM_H, Depth = 3f },
                new CrateSpec { X = TRAPEZE_PLATFORM_X, Y = TRAPEZE_PLATFORM_Y, Z = -0.75f, Width = 3f, Height = TRAPEZE_PLATFORM_H, Depth = 3f },
            };

            return design;
        }

        //Where the platforms stand, in world units from the field's floor on its axis: fourteen out (the field is seven
        //and a half wide either side), and a little over the bar's own height - its layout level plus the field's offset,
        //in levels of one over root two. TEN OUT WAS TOO NEAR, and the review of #690 said why by arithmetic before a
        //capture showed it: from the low play camera at the end-on bearings, the line up to the top of the near rope
        //crossed the near platform, which then hid the rig's top. At fourteen, and two and a half tall, that line passes
        //under it by two units.
        private const float TRAPEZE_PLATFORM_X = 14f;
        private const float TRAPEZE_PLATFORM_H = 2.5f;
        private const float TRAPEZE_PLATFORM_Y = (BIGTOP_FIELD_LEVELS - TRAPEZE_DEPTH + TRAPEZE_BAR + 1.6f) * 0.70710678f;

        private static Design TrapezeRig() => BigTop("Trapeze", TRAPEZE_DEPTH, shots: 22, ceilingStep: 8,
            hz: 7f, damping: 0.5f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
                BigTopPost(cx, cz, i, -TRAPEZE_ROPE, TRAPEZE_BAR, TRAPEZE_DEPTH) || BigTopPost(cx, cz, i, TRAPEZE_ROPE, TRAPEZE_BAR, TRAPEZE_DEPTH)
                || (RigRow(cz) && Math.Abs(cx) <= TRAPEZE_ROPE && (i == TRAPEZE_BAR || i == TRAPEZE_BAR + 1))
                || TrapezeFlyer(cx, cz, i),
            colour: (cx, cz, i) =>
            {
                if (TrapezeFlyer(cx, cz, i)) return i >= 3 ? BallType.Type7 : BallType.Type3;
                if (i > TRAPEZE_BAR + 1)
                    return PostInk(cz) == 0 ? BallType.Type4 : BallType.Type11;
                return Math.Abs(cx) >= 2 ? BallType.Type1 : BallType.Type9;
            });

        //--- 7. CHANDELIER: a ring of crystal on four chains -------------------------------------------------------
        //The tent's chandelier: a flat ring hung level on four chains, a drop of crystal under every chain and a
        //pendant in the middle on a chain of its own. Take a chain's two inks and the ring tilts and swings from the
        //other three. The ring is small enough that, left on one chain, it still hangs clear of the line.

        private const byte CHANDELIER_DEPTH = 10;
        private const float CHANDELIER_RING = 3.2f;
        private const int CHANDELIER_RING_LOW = 4;

        private static bool ChandelierRing(int cx, int cz, int i)
        {
            if (i != CHANDELIER_RING_LOW && i != CHANDELIER_RING_LOW + 1) return false;
            float r = MathF.Sqrt(cx * cx + cz * cz);
            return MathF.Abs(r - CHANDELIER_RING) <= 0.8f;
        }

        /// <summary>Which of the four chains a column is (the cells on the ring's diagonals, two by two), or -1.</summary>
        private static int ChandelierChain(int cx, int cz)
        {
            int[] xs = { 2, -3, -3, 2 };
            int[] zs = { 2, 2, -3, -3 };
            for (int c = 0; c < 4; c++)
                if ((cx == xs[c] || cx == xs[c] + 1) && (cz == zs[c] || cz == zs[c] + 1)) return c;
            return -1;
        }

        private static bool ChandelierPendant(int cx, int cz, int i) =>
            (cx == 0 || cx == -1) && (cz == 0 || cz == -1) && i >= 2;

        private static Design Chandelier() => BigTop("Chandelier", CHANDELIER_DEPTH, shots: 34, ceilingStep: 8,
            hz: 9f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
                ChandelierRing(cx, cz, i)
                || (ChandelierChain(cx, cz) >= 0 && i >= CHANDELIER_RING_LOW - 2)
                || ChandelierPendant(cx, cz, i),
            colour: (cx, cz, i) =>
            {
                int chain = ChandelierChain(cx, cz);
                if (chain >= 0 && i > CHANDELIER_RING_LOW + 1)
                    return PostInk(cz) == 0 ? BallType.Type7 : BallType.Type9;
                if (chain >= 0 && i < CHANDELIER_RING_LOW) return BallType.Type5;
                if (ChandelierPendant(cx, cz, i)) return i >= 6 ? BallType.Type4 : BallType.Type6;

                //The ring in four arcs of two colours, so no one colour holds the whole round
                int quadrant = (cx >= 0 ? 1 : 0) + (cz >= 0 ? 2 : 0);
                return quadrant == 0 || quadrant == 3 ? BallType.Type1 : BallType.Type3;
            });

        //--- 8. BRIDGE: a rope bridge between two towers -------------------------------------------------------
        //A plank deck slung between two towers, a handrail rope either side and a hanger every other plank. The planks
        //alternate two woods, so each is a group: take one and the deck parts there, and what is left hangs on the
        //handrails. Take a handrail's hangers and the deck swings down off it.

        private const byte BRIDGE_TOP_DEPTH = 10;
        private const int BRIDGE_TOWER = 4;
        private static readonly int[] BRIDGE_DECK = { 3, 3, 4, 4 };
        private const int BRIDGE_RAIL_RISE = 3;

        private static bool BridgeTower(int cx, int cz, int i) =>
            (cx == BRIDGE_TOWER || cx == BRIDGE_TOWER + 1 || cx == -BRIDGE_TOWER || cx == -BRIDGE_TOWER - 1)
            && cz >= -3 && cz <= 2 && (cz <= -2 || cz >= 1) && i >= BRIDGE_DECK[3];

        private static bool BridgeDeck(int cx, int cz, int i)
        {
            int a = Math.Abs(cx);
            if (a >= BRIDGE_TOWER || cz < -2 || cz > 1) return false;
            return i == BRIDGE_DECK[a] || i == BRIDGE_DECK[a] + 1;
        }

        private static bool BridgeRail(int cx, int cz, int i)
        {
            int a = Math.Abs(cx);
            if (a >= BRIDGE_TOWER || (cz != -3 && cz != 2)) return false;
            int rail = BRIDGE_DECK[a] + BRIDGE_RAIL_RISE;
            return i == rail || i == rail + 1;
        }

        /// <summary>
        /// A hanger: the rail's row and the deck's edge row beside it, on the level between the two. Two cells and not
        /// one because a single cell under the rail touches the deck below it on one parity only.
        /// </summary>
        private static bool BridgeHanger(int cx, int cz, int i)
        {
            int a = Math.Abs(cx);
            if (a >= BRIDGE_TOWER || a % 2 != 0 || (cz != -3 && cz != -2 && cz != 1 && cz != 2)) return false;
            return i > BRIDGE_DECK[a] + 1 && i < BRIDGE_DECK[a] + BRIDGE_RAIL_RISE;
        }

        private static Design Footbridge() => BigTop("Footbridge", BRIDGE_TOP_DEPTH, shots: 32, ceilingStep: 9,
            hz: 11f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
                BridgeTower(cx, cz, i) || BridgeDeck(cx, cz, i) || BridgeRail(cx, cz, i)
                || BridgeHanger(cx, cz, i) || (BridgeDeckEnd(cx, cz, i)),
            colour: (cx, cz, i) =>
            {
                if (BridgeTower(cx, cz, i)) return PostInk(cz) == 0 ? BallType.Type4 : BallType.Type11;
                if (BridgeRail(cx, cz, i) || BridgeHanger(cx, cz, i)) return cz < 0 ? BallType.Type1 : BallType.Type3;
                return Math.Abs(cx) % 2 == 0 ? BallType.Type10 : BallType.Type7;
            });

        /// <summary>The deck's two ends, carried into the towers' gap so the planks seat on the towers.</summary>
        private static bool BridgeDeckEnd(int cx, int cz, int i)
        {
            int a = Math.Abs(cx);
            return (a == BRIDGE_TOWER || a == BRIDGE_TOWER + 1) && cz >= -1 && cz <= 0
                && (i == BRIDGE_DECK[3] || i == BRIDGE_DECK[3] + 1);
        }

        //--- 9. SAFETY NET: the net under the flyers ----------------------------------------------------------------
        //The safety net: a square mesh of rope on eight posts, bellying in the middle, its squares in a check of three
        //colours so it is cut a tile at a time. Holes everywhere: a shot through a hole flies on to the far side.

        private const byte NET_DEPTH = 8;
        private const int NET_HALF = 6;

        /// <summary>
        /// The eight posts, every one two by two: the four corners and the middle of each side. The corner posts were a
        /// single cell once - one chain of springs in series - and under the finer mesh they stretched until the net's
        /// corners hung under the line with the glass at rest, three probe runs in five.
        /// </summary>
        private static bool NetPost(int cx, int cz)
        {
            int ax = Math.Abs(cx), az = Math.Abs(cz);
            bool atEdgeX = ax == NET_HALF || ax == NET_HALF - 1, atEdgeZ = az == NET_HALF || az == NET_HALF - 1;
            bool atMidX = cx == 0 || cx == -1, atMidZ = cz == 0 || cz == -1;
            return (atEdgeX && (atEdgeZ || atMidZ)) || (atEdgeZ && atMidX);
        }

        private static int NetLow(int cx, int cz)
        {
            int edge = Math.Max(Math.Abs(cx), Math.Abs(cz));
            return edge >= 5 ? 4 : edge >= 3 ? 3 : 2;
        }

        /// <summary>
        /// The mesh: a rope on every other row and column, so each hole is one cell. A rope every third cell was a net of
        /// long single strands, springs in series, and a piece of it left on one post stretched down to the line in one
        /// probe run in five; the finer mesh carries every piece on several paths at once.
        /// </summary>
        private static bool NetStrand(int cx, int cz) =>
            Math.Abs(cx) <= NET_HALF && Math.Abs(cz) <= NET_HALF
            && ((cx + 100) % 2 == 0 || (cz + 100) % 2 == 0 || Math.Abs(cx) == NET_HALF || Math.Abs(cz) == NET_HALF);

        private static Design SafetyNet() => BigTop("SafetyNet", NET_DEPTH, shots: 44, ceilingStep: 10,
            hz: 13f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
            {
                if (NetPost(cx, cz)) return i >= NetLow(cx, cz);
                if (!NetStrand(cx, cz)) return false;
                int low = NetLow(cx, cz);
                return i == low || i == low + 1;
            },
            colour: (cx, cz, i) =>
            {
                //The posts in two inks each (PostInk's reason): with one ink a post went in a single shot, a corner of the
                //net was left on the posts beside it, and the probe hung it under the line four runs in five
                if (NetPost(cx, cz) && i > NetLow(cx, cz) + 1)
                    return PostInk(cz) == 0 ? BallType.Type4 : BallType.Type11;

                //The mesh in a check of nine tiles, four cells a side, in three colours: a third of the groups the
                //three-cell tiles made, which left the level more groups than shots
                int tx = Math.Clamp((cx + NET_HALF) / 4, 0, 2), tz = Math.Clamp((cz + NET_HALF) / 4, 0, 2);
                return ((tx + 2 * tz) % 3) switch { 0 => BallType.Type1, 1 => BallType.Type5, _ => BallType.Type7 };
            });

        //--- 10. BIG TOP: the tent itself, the finale ----------------------------------------------------------------
        //The tent the chapter plays in, hung upside down from the glass: a cone of canvas round a king post, its panels
        //striped red and cream like the canvas overhead, held at its rim by four quarter poles. Each panel is a group;
        //take them and the canvas that is left swings on the king post like an umbrella. Under it, a ring of bulbs.

        private const byte TENT_DEPTH = 12;
        private const int TENT_RIM = 5;

        /// <summary>The canvas's level at a radius: never more than one level a cell, so the two-level canvas stays one
        /// body from the king post to the rim on both parities.</summary>
        private static int TentLevelAt(float r) => (int)MathF.Round(10f - r * 0.9f);

        private static bool TentCanvas(int cx, int cz, int i, out float r)
        {
            r = MathF.Sqrt((cx + 0.5f) * (cx + 0.5f) + (cz + 0.5f) * (cz + 0.5f));
            if (r > TENT_RIM + 0.6f) return false;
            int level = TentLevelAt(r);
            return i == level || i == level + 1;
        }

        private static bool TentKingPost(int cx, int cz) => (cx == 0 || cx == -1) && (cz == 0 || cz == -1);

        private static bool TentQuarterPole(int cx, int cz) =>
            (Math.Abs(cx + 0.5f) >= 3.4f && Math.Abs(cx + 0.5f) <= 4.6f) && (Math.Abs(cz + 0.5f) >= 3.4f && Math.Abs(cz + 0.5f) <= 4.6f);

        private static Design BigTopTent() => BigTop("BigTop", TENT_DEPTH, shots: 36, ceilingStep: 8,
            hz: 9f, damping: 0.6f, fieldLevels: BIGTOP_FIELD_LEVELS,
            occupied: (cx, cz, i) =>
            {
                if (TentKingPost(cx, cz)) return i >= TentLevelAt(0f);
                if (TentQuarterPole(cx, cz)) return i >= TentLevelAt(MathF.Sqrt(2f) * 4f);
                return TentCanvas(cx, cz, i, out _);
            },
            colour: (cx, cz, i) =>
            {
                if (TentKingPost(cx, cz)) return PostInk(cz) == 0 ? BallType.Type7 : BallType.Type9;
                //The quarter poles in inks the canvas does not use (brown, silver): in the canvas's cream, a pole's row
                //joined the panel beside it, and a shot at the panel took half the pole with it (the review of #690)
                if (TentQuarterPole(cx, cz) && i > TentLevelAt(MathF.Sqrt(2f) * 4f) + 1)
                    return PostInk(cz) == 0 ? BallType.Type10 : BallType.Type11;
                float angle = MathF.Atan2(cz + 0.5f, cx + 0.5f);
                int panel = (int)MathF.Floor((angle + MathF.PI) / (MathF.PI / 4f)) % 8;
                return panel % 2 == 0 ? BallType.Type1 : BallType.Type4;
            });

        #endregion
    }
}
