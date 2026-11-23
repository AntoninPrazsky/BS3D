using BepuPhysics;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.BS3D.Scoring;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The loose ball's timeout (#1191, <see cref="LooseBallTimeout"/>): a released ball that wedged in the field goes after its
    /// time, dissolving the whole way, and is retired once, with nothing counted for it again; one below the field, or falling
    /// out of it, is left its fall; the spare volley's balls never go; the field's floor follows the glass down to the death
    /// line; the Game's cull takes what it should and ages only the released balls; and the drawn dissolve carries the dead
    /// weight's mark on to the whole ball and never goes back.
    /// <para>
    /// What only a run checks: that <c>GameplayScreen.FireSpareBall</c> marks the volley's balls <c>Untimed</c>, and that the
    /// ball's <c>Vanish</c> reaches the instance (<c>ClusterCollector.Collect</c> into <c>BallDrawFrame.AddOriented</c>); the
    /// tests compile neither the Game's screen nor a frame's buckets.
    /// </para>
    /// </summary>
    public class LooseBallTimeoutTests
    {
        //The frame the Game ages its released balls by: a 60 Hz frame is two of the world's fixed steps
        private const float FRAME = 2f * HungLevel.TIMESTEP;

        //GameplayScreen.KILL_PLANE_Y, which the tests cannot reference (DropFocusTests' copy)
        private const float KILL_PLANE_Y = -42f;

        //CeilingDescent.CEILING_DESCENT_PER_STEP, which the tests cannot reference (SagProbe's copy)
        private const float DESCENT_PER_STEP = 0.6f;

        /// <summary>The field's floor of a hung level whose glass is still at rest, as the Game reads it (<c>FieldFloorY</c>).</summary>
        private static float FloorAtRest(HungLevel hung) => ClusterHang.FieldFloorY(hung.WorldOffset.Y, 0f);

        /// <summary>
        /// The owner's case: a group the cluster let go of comes to rest up in the field instead of falling (a shelf under it,
        /// the dead weight's scripted level of docs/game-feedback/dead-weight.md), is still there whole just before
        /// <see cref="LooseBallTimeout.FIELD_SECONDS"/>, and is gone from the list and the simulation once its dissolve has run -
        /// each ball retired exactly once, the release scored once and nothing scored for the going.
        /// </summary>
        [Fact]
        public void AGroupStuckInTheFieldIsGoneAfterItsTimeAndCountedOnce()
        {
            using HungLevel hung = new(Shelf(out XZLevel groupCell, out int groupSize));
            float floorY = FloorAtRest(hung);

            //The cluster settled first, as a level does before the first shot lands
            hung.Run(1f);

            var released = new List<PhysicsBall>();
            BallsReleased result = BallsConstraintsBuilder.ReleaseSameTypeCluster(
                hung.Balls[groupCell.X, groupCell.Z, groupCell.Level], hung.Balls, hung.Map, hung.World.Simulation, released);

            Assert.Equal(groupSize, result.Matched);
            Assert.Equal(groupSize, released.Count);

            //Scored where the Game scores it: once, at the landing that released it
            var keeper = new ScoreKeeper();
            keeper.Landed(result.Matched, result.Orphaned, result.Destroyed);
            int scored = keeper.Score;

            var handles = new List<BodyHandle>();
            foreach (PhysicsBall ball in released) handles.Add(ball.BallReference.Handle);

            var retired = new Dictionary<int, int>();
            float clock = 0f;

            //Just short of its time: every ball still there, still whole, and still up in the field - which is what makes this
            //the case at all (a group that fell through would prove nothing about the timeout)
            while (clock + FRAME < LooseBallTimeout.FIELD_SECONDS)
            {
                Frame(hung, released, floorY, retired);
                clock += FRAME;
            }

            Assert.Equal(groupSize, released.Count);
            foreach (PhysicsBall ball in released)
            {
                Assert.Equal(0f, ball.Vanish);
                Assert.True(ball.BallReference.Pose.Position.Y > floorY,
                    $"a released ball fell to y {ball.BallReference.Pose.Position.Y:F2}, under the field's floor at {floorY:F2}: the shelf did not hold it");
            }

            //And past its time and its dissolve, with a frame to spare: gone, every one, once
            while (clock < LooseBallTimeout.FIELD_SECONDS + LooseBallTimeout.VANISH_SECONDS + 2f * FRAME)
            {
                Frame(hung, released, floorY, retired);
                clock += FRAME;
            }

            Assert.Empty(released);
            Assert.Equal(groupSize, retired.Count);
            foreach (BodyHandle handle in handles)
            {
                Assert.Equal(1, retired[handle.Value]);
                Assert.False(hung.World.Simulation.Bodies.BodyExists(handle));
            }

            Assert.Equal(scored, keeper.Score);
            Assert.Equal(groupSize, keeper.MatchedBalls);
            Assert.Equal(0, keeper.OrphanedBalls);
        }

        //How long a ball below the field is aged without going: past the longest drain measured (Pennant's clearing shot,
        //232 balls, up to 13 s to the kill plane) with room to spare
        private const float LONG_DRAIN_SECONDS = 60f;

        /// <summary>
        /// A ball below the field's floor is on its way out (the drain, the island's edge) and is left its fall however long
        /// it takes; one above the floor goes at <see cref="LooseBallTimeout.FIELD_SECONDS"/>, over
        /// <see cref="LooseBallTimeout.VANISH_SECONDS"/>. Aged without stepping the world, so each ball stands where it was put.
        /// </summary>
        [Fact]
        public void ABallBelowTheFieldIsLeftItsFall()
        {
            using HungLevel hung = new(OneBall());
            float floorY = FloorAtRest(hung);

            PhysicsBall below = Loose(hung, floorY - 3f);
            PhysicsBall above = Loose(hung, floorY + 3f);

            float belowGoneAt = AgeUntilGone(below, floorY, LONG_DRAIN_SECONDS, out float belowStartedAt);
            float aboveGoneAt = AgeUntilGone(above, floorY, LONG_DRAIN_SECONDS, out float aboveStartedAt);

            Assert.InRange(aboveStartedAt, LooseBallTimeout.FIELD_SECONDS, LooseBallTimeout.FIELD_SECONDS + FRAME);
            Assert.InRange(aboveGoneAt, LooseBallTimeout.FIELD_SECONDS + LooseBallTimeout.VANISH_SECONDS,
                LooseBallTimeout.FIELD_SECONDS + LooseBallTimeout.VANISH_SECONDS + 2f * FRAME);

            Assert.True(float.IsNaN(belowStartedAt), $"a ball under the field's floor began to go at {belowStartedAt:F2} s");
            Assert.Equal(float.PositiveInfinity, belowGoneAt);
            Assert.Equal(0f, below.Vanish);
        }

        /// <summary>
        /// A ball still in the field past its time but falling out of it (faster than <see cref="LooseBallTimeout.FALLING_SPEED"/>:
        /// knocked off a ledge late, the last of a big drop) is left its fall; the moment it stands still in the field again it
        /// goes. The world is not stepped, so the ball keeps the speed it was given where it stands.
        /// </summary>
        [Fact]
        public void ABallFallingOutOfTheFieldIsLeftItsFallUntilItStops()
        {
            using HungLevel hung = new(OneBall());
            float floorY = FloorAtRest(hung);

            PhysicsBall ball = Loose(hung, floorY + 3f);
            ball.BallReference.Velocity.Linear = new System.Numerics.Vector3(0f, -2f * LooseBallTimeout.FALLING_SPEED, 0f);

            for (float clock = 0f; clock < LooseBallTimeout.FIELD_SECONDS + 2f; clock += FRAME)
                Assert.False(LooseBallTimeout.Advance(ball, FRAME, floorY));

            Assert.Equal(0f, ball.Vanish);

            ball.BallReference.Velocity.Linear = System.Numerics.Vector3.Zero;
            LooseBallTimeout.Advance(ball, FRAME, floorY);

            Assert.True(ball.Vanish > 0f, "a ball that stopped in the field past its time did not begin to go");
        }

        /// <summary>
        /// The dissolve, once begun, runs to its end wherever the ball goes and however fast: a ball that started to go in the
        /// field and then dropped under the floor does not come back whole.
        /// </summary>
        [Fact]
        public void ADissolveOnceBegunRunsToItsEnd()
        {
            using HungLevel hung = new(OneBall());
            float floorY = FloorAtRest(hung);

            PhysicsBall ball = Loose(hung, floorY + 3f);
            for (float clock = 0f; ball.Vanish <= 0f && clock < LONG_DRAIN_SECONDS; clock += FRAME)
                LooseBallTimeout.Advance(ball, FRAME, floorY);

            Assert.True(ball.Vanish > 0f, "a ball in the field never began to go");
            float vanish = ball.Vanish;
            ball.BallReference.Pose.Position = new System.Numerics.Vector3(0f, floorY - 3f, 0f);
            ball.BallReference.Velocity.Linear = new System.Numerics.Vector3(0f, -2f * LooseBallTimeout.FALLING_SPEED, 0f);

            Assert.False(LooseBallTimeout.Advance(ball, FRAME, floorY));
            Assert.True(ball.Vanish > vanish, "the dissolve stopped when the ball dropped under the field's floor");
        }

        /// <summary>
        /// The world's clock: a frame in which the world took no step (a pause) ages nothing, and the spare volley's balls
        /// (<see cref="PhysicsBall.Untimed"/>) are never aged at all, wherever they fly.
        /// </summary>
        [Fact]
        public void APauseAgesNothingAndTheVolleysBallsNeverGo()
        {
            using HungLevel hung = new(OneBall());
            float floorY = FloorAtRest(hung);

            PhysicsBall paused = Loose(hung, floorY + 3f);
            for (int i = 0; i < 10_000; i++) Assert.False(LooseBallTimeout.Advance(paused, 0f, floorY));
            Assert.Equal(0f, paused.LooseSeconds);

            PhysicsBall volley = Loose(hung, floorY + 3f);
            volley.Untimed = true;

            for (float clock = 0f; clock < LONG_DRAIN_SECONDS; clock += FRAME)
                Assert.False(LooseBallTimeout.Advance(volley, FRAME, floorY));

            Assert.Equal(0f, volley.Vanish);
            Assert.Equal(0f, volley.LooseSeconds);
        }

        /// <summary>
        /// What an ordinary released ball is drawn with (<see cref="BallRenderSet.ReleasedDissolve"/>): the dead weight's mark
        /// alone before it goes, the whole ball gone at the end, and the going rising all the way from the mark rather than
        /// standing still until the vanish has passed it.
        /// </summary>
        [Fact]
        public void TheLastDissolveCarriesTheMarkOnToTheWholeBall()
        {
            Assert.Equal(BallRenderSet.DEAD_DISSOLVE, BallRenderSet.ReleasedDissolve(1f, 0f), 5);
            Assert.Equal(1f, BallRenderSet.ReleasedDissolve(1f, 1f), 5);
            Assert.Equal(1f, BallRenderSet.ReleasedDissolve(0f, 1f), 5);
            Assert.Equal(0.3f, BallRenderSet.ReleasedDissolve(0f, 0.3f), 5);

            float previous = BallRenderSet.ReleasedDissolve(1f, 0f);
            for (int i = 1; i <= 100; i++)
            {
                float drawn = BallRenderSet.ReleasedDissolve(1f, i / 100f);
                Assert.True(drawn > previous, $"the drawn dissolve stood still at vanish {i / 100f:F2}");
                previous = drawn;
            }
        }

        /// <summary>
        /// A ball that has begun to go never gets pixels back: nudged into moving again during its going (faster than the mark
        /// allows, <see cref="ClusterCollector.DEAD_WEIGHT_SPEED"/>, slower than a fall), its mark holds under the going
        /// (<c>ClusterCollector.AdvanceDeadWeight</c>), so the drawn dissolve (<see cref="BallRenderSet.ReleasedDissolve"/>)
        /// only ever climbs, to the whole ball. Both clocks run as the Game runs them: the timeout in the cull, then the mark
        /// in the draw's walk.
        /// </summary>
        [Fact]
        public void AGoingBallNeverGetsItsPixelsBack()
        {
            using HungLevel hung = new(OneBall());
            float floorY = FloorAtRest(hung);

            PhysicsBall ball = Loose(hung, floorY + 3f);
            for (float clock = 0f; ball.Vanish <= 0f && clock < LONG_DRAIN_SECONDS; clock += FRAME)
            {
                LooseBallTimeout.Advance(ball, FRAME, floorY);
                ClusterCollector.AdvanceDeadWeight(ball, FRAME, floorY);
            }

            Assert.True(ball.Vanish > 0f, "a ball at rest in the field never began to go");
            Assert.Equal(1f, ball.DeadWeight);

            ball.BallReference.Velocity.Linear = new System.Numerics.Vector3(
                (ClusterCollector.DEAD_WEIGHT_SPEED + LooseBallTimeout.FALLING_SPEED) * 0.5f, 0f, 0f);

            float previous = BallRenderSet.ReleasedDissolve(ball.DeadWeight, ball.Vanish);
            bool gone = false;

            for (float clock = 0f; !gone && clock < LONG_DRAIN_SECONDS; clock += FRAME)
            {
                gone = LooseBallTimeout.Advance(ball, FRAME, floorY);
                float drawn = BallRenderSet.ReleasedDissolve(ClusterCollector.AdvanceDeadWeight(ball, FRAME, floorY), ball.Vanish);

                Assert.True(drawn >= previous, $"a going ball got pixels back: its dissolve fell from {previous:F3} to {drawn:F3}");
                previous = drawn;
            }

            Assert.True(gone, "a going ball never went");
            Assert.Equal(1f, previous, 5);
        }

        /// <summary>
        /// The Game's cull of one ball (<see cref="LooseBallTimeout.Cull"/>, what <c>RemoveFallenBalls</c> asks of every ball it
        /// holds): the kill plane takes any ball; a cutter among the released balls goes at once, spent, and one among the shots
        /// is still flying; a shot is never aged however long it stands in the field, while a released ball beside it goes
        /// when its time and its dissolve have run.
        /// </summary>
        [Fact]
        public void TheCullTakesWhatTheGameTakesAndAgesOnlyTheReleased()
        {
            using HungLevel hung = new(OneBall());
            float floorY = FloorAtRest(hung);

            PhysicsBall fallen = Loose(hung, KILL_PLANE_Y - 1f);
            Assert.Equal(BallExit.KillPlane, LooseBallTimeout.Cull(fallen, released: false, KILL_PLANE_Y, FRAME, floorY));
            Assert.Equal(BallExit.KillPlane, LooseBallTimeout.Cull(fallen, released: true, KILL_PLANE_Y, FRAME, floorY));

            PhysicsBall cutter = Loose(hung, floorY + 3f);
            cutter.Kind = BallKind.Cutter;
            Assert.Equal(BallExit.Stays, LooseBallTimeout.Cull(cutter, released: false, KILL_PLANE_Y, FRAME, floorY));
            Assert.Equal(BallExit.SpentCutter, LooseBallTimeout.Cull(cutter, released: true, KILL_PLANE_Y, FRAME, floorY));
            Assert.Equal(0f, cutter.LooseSeconds);

            PhysicsBall shot = Loose(hung, floorY + 3f);
            PhysicsBall loose = Loose(hung, floorY + 3f);
            float looseGoneAt = float.NaN;

            for (float clock = 0f; clock < LONG_DRAIN_SECONDS; clock += FRAME)
            {
                Assert.Equal(BallExit.Stays, LooseBallTimeout.Cull(shot, released: false, KILL_PLANE_Y, FRAME, floorY));

                if (float.IsNaN(looseGoneAt)
                    && LooseBallTimeout.Cull(loose, released: true, KILL_PLANE_Y, FRAME, floorY) == BallExit.Dissolved)
                    looseGoneAt = clock + FRAME;
            }

            Assert.Equal(0f, shot.LooseSeconds);
            Assert.Equal(0f, shot.Vanish);
            Assert.InRange(looseGoneAt, LooseBallTimeout.FIELD_SECONDS + LooseBallTimeout.VANISH_SECONDS,
                LooseBallTimeout.FIELD_SECONDS + LooseBallTimeout.VANISH_SECONDS + 2f * FRAME);
        }

        /// <summary>
        /// The field's floor follows the glass down (<see cref="ClusterHang.FieldFloorY"/>) and stops at the death line. On a
        /// 16-level field, once the glass has stepped the cluster's bottom near the line, a ball wedged on its lowest balls stands
        /// under the floor the load hung, and is due all the same; while a ball at rest on the island's stone, under the line,
        /// is never due however far the glass has come.
        /// </summary>
        [Fact]
        public void TheFloorFollowsTheGlassDownToTheLine()
        {
            var map = new BallsMap(5, 5, 16);
            map.PutBallAt(2, 2, 15, BallType.Type1);
            float loadFloorY = ClusterHang.FitWorldOffset(map, out _).Y;

            Assert.Equal(loadFloorY, ClusterHang.FieldFloorY(loadFloorY, 0f));
            Assert.True(loadFloorY > ClusterHang.DEATH_Y + 4f * DESCENT_PER_STEP,
                $"the 16-level field's floor hangs at {loadFloorY:F2}, too near the line at {ClusterHang.DEATH_Y:F2} to show the case");

            //Resting on two of the cluster's lowest balls with the line just under them: a ball's centre 0.87 over theirs
            float wedgedY = ClusterHang.DEATH_Y + Constants.HALF + 0.87f;
            float late = ClusterHang.FieldFloorY(loadFloorY, 6f * DESCENT_PER_STEP);

            Assert.True(wedgedY < loadFloorY, "the wedged ball stands over the load's floor, so the case shows nothing");
            Assert.True(LooseBallTimeout.IsDue(LooseBallTimeout.FIELD_SECONDS, wedgedY, late, 0f),
                $"a ball wedged on the cluster at {wedgedY:F2}, the glass six steps down, is not due (the floor at {late:F2})");

            float stoneY = ArenaIsland.TOP_Y + Constants.HALF;
            for (int steps = 0; steps < 60; steps++)
            {
                float floor = ClusterHang.FieldFloorY(loadFloorY, steps * DESCENT_PER_STEP);

                Assert.True(floor >= ClusterHang.DEATH_Y, $"the floor went under the line {steps} steps down, to {floor:F2}");
                Assert.False(LooseBallTimeout.IsDue(LooseBallTimeout.FIELD_SECONDS + 10f, stoneY, floor, 0f),
                    $"a ball at rest on the island's stone is due {steps} steps down");
            }
        }

        /// <summary>
        /// One of the Game's frames over the released balls, as <c>GameplayScreen.StepPhysics</c> spends it: the world's
        /// steps, then the cull (<c>RemoveFallenBalls</c>, through the same <see cref="LooseBallTimeout.Cull"/>): the kill
        /// plane first, then the timeout, each ball aged once and a ball that leaves retired through the kill plane's own door.
        /// </summary>
        private static void Frame(HungLevel hung, List<PhysicsBall> released, float floorY, Dictionary<int, int> retired)
        {
            hung.Run(FRAME);

            for (int i = released.Count - 1; i >= 0; i--)
            {
                PhysicsBall ball = released[i];
                if (LooseBallTimeout.Cull(ball, released: true, KILL_PLANE_Y, FRAME, floorY) == BallExit.Stays) continue;

                int handle = ball.BallReference.Handle.Value;
                retired[handle] = retired.TryGetValue(handle, out int n) ? n + 1 : 1;

                hung.World.RetireBall(ball.BallReference);
                released.RemoveAt(i);
            }
        }

        /// <summary>Ages a ball a frame at a time, the world standing still, until it is gone; when its dissolve began and when it ended.</summary>
        private static float AgeUntilGone(PhysicsBall ball, float floorY, float limit, out float startedAt)
        {
            startedAt = float.NaN;
            float clock = 0f;

            while (clock < limit)
            {
                bool gone = LooseBallTimeout.Advance(ball, FRAME, floorY);
                clock += FRAME;

                if (float.IsNaN(startedAt) && ball.Vanish > 0f) startedAt = clock;
                if (gone) return clock;
            }

            return float.PositiveInfinity;
        }

        /// <summary>A released ball standing at <paramref name="y"/> on the field's axis, as the Game's loose balls are made.</summary>
        private static PhysicsBall Loose(HungLevel hung, float y) => new()
        {
            BallReference = hung.World.AddLooseBall(new System.Numerics.Vector3(0f, y, 0f), System.Numerics.Vector3.Zero),
            Type = BallType.Type2,
        };

        /// <summary>A field with one ball under the glass: something to hang, for the tests that only need a world.</summary>
        private static BallsMap OneBall()
        {
            var map = new BallsMap(5, 5, 6);
            map.PutBallAt(2, 2, 5, BallType.Type1);
            return map;
        }

        /// <summary>
        /// The dead weight's scripted level (docs/game-feedback/dead-weight.md), with a match where its bomb was: a 9 x 9 field,
        /// a 7 x 7 plate on its seventh level held up to the glass by a pillar at each corner, and above the plate's middle, a
        /// level clear of it, a 3 x 3 group of one colour hung from the glass by a chain of another. Released, the group drops
        /// onto the plate and stays there, up in the field.
        /// </summary>
        private static BallsMap Shelf(out XZLevel groupCell, out int groupSize)
        {
            var map = new BallsMap(9, 9, 12);
            XZLevel size = map.GetStaticBallsArraySize();
            int top = size.Level - 1;
            const int PLATE = 6, GROUP = PLATE + 2;

            for (int x = 1; x <= 7; x++)
                for (int z = 1; z <= 7; z++)
                    map.PutBallAt((byte)x, (byte)z, PLATE, BallType.Type3);

            foreach ((int x, int z) in new[] { (1, 1), (7, 1), (1, 7), (7, 7) })
                Climb(map, new XZLevel(x, z, PLATE), top, BallType.Type3, size);

            groupSize = 0;
            for (int x = 3; x <= 5; x++)
                for (int z = 3; z <= 5; z++)
                {
                    map.PutBallAt((byte)x, (byte)z, GROUP, BallType.Type2);
                    groupSize++;
                }

            groupCell = new XZLevel(4, 4, GROUP);
            Climb(map, groupCell, top, BallType.Type5, size);

            return map;
        }

        /// <summary>A column of balls from <paramref name="from"/> up to <paramref name="top"/>, each a lattice neighbour of the one under it, the nearest to the axis the column started on.</summary>
        private static void Climb(BallsMap map, XZLevel from, int top, BallType type, XZLevel size)
        {
            XZLevel at = from;
            while (at.Level < top)
            {
                XZLevel next = at;
                float best = float.MaxValue;

                foreach (XZLevel neighbour in BallsMap.GetNeighboringCells(at, size))
                {
                    if (neighbour.Level != at.Level + 1) continue;

                    float distance = MathF.Abs(neighbour.X - from.X) + MathF.Abs(neighbour.Z - from.Z);
                    if (distance >= best) continue;

                    best = distance;
                    next = neighbour;
                }

                Assert.NotEqual(at, next);
                map.PutBallAt((byte)next.X, (byte)next.Z, (byte)next.Level, type);
                at = next;
            }
        }
    }
}
