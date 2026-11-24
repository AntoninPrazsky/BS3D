using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Xunit.Abstractions;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #1192: a shot pushes the cluster it hits. Every check here hangs a level TWICE, on one worker thread each so the two
    /// worlds step identically (<see cref="HungLevel"/>), fires the same shot into both through the Game's own contact
    /// handler (<see cref="ShotRig"/>), one with the push and one without, and reads what the push alone did as the
    /// difference between the twins: how far it moved the balls, how far it stretched the sockets, where the shot landed.
    /// </summary>
    public class ShotPushTests(ITestOutputHelper output)
    {
        /// <summary>
        /// The promise: a shot moves the cluster it hits, visibly and within a third of a second (the farthest ball at least
        /// <c>0.03</c> units from where it would have been, where the push measured 0.06 on the dense One and 0.4 on the strand
        /// Pendant); within what the lattice holds (no socket stretched more than 0.05 of a diameter beyond what the same
        /// landing stretches it without the push, where it measured at most 0.02, and a heavy ball's whole envelope is 0.044);
        /// never with more momentum than the shot carried; and the shot lands in the very cell it lands in without the push.
        /// A cutter's strike pushes too. Shot four times as hard (<c>shotpush=4</c>, or a kind built to hit harder),
        /// <see cref="ShotPush.MAX_KICK"/> keeps the sockets inside the same bound.
        /// </summary>
        [Theory]
        [InlineData("One.json", 1f, BallKind.Normal)]
        [InlineData("Pennant.json", 1f, BallKind.Normal)]
        [InlineData("Pendant.json", 1f, BallKind.Normal)]
        [InlineData("Pendant.json", 4f, BallKind.Normal)]
        [InlineData("Colossus.json", 1f, BallKind.Cutter)]
        public void AShotMovesTheClusterItHitsWithinWhatTheLatticeHolds(string level, float scale, BallKind kind)
        {
            Response r = Fire(level, 1, 1f, scale, kind: kind);
            output.WriteLine(Row(level, 1, string.Create(CultureInfo.InvariantCulture, $"x{scale} {kind}"), r));

            Assert.NotNull(r.Landed);
            Assert.Equal(r.LandedWithout, r.Landed);

            float early = 0f;
            for (int step = 0; step <= (int)MathF.Round(0.3f / HungLevel.TIMESTEP); step++) early = MathF.Max(early, r.MaxDisplacement[step]);
            Assert.True(early >= 0.03f, $"the push moved no ball more than {early:0.000} in the first 0.3 s");

            Assert.True(r.PeakStretch - r.PeakStretchWithout < 0.05f,
                $"the push stretched a socket {r.PeakStretch - r.PeakStretchWithout:0.000} of a diameter beyond the landing's own");
            Assert.True(r.Push.Impulse <= scale * ShotRig.SHOOT_SPEED * BallsConstraintsBuilder.BALL_MASS * 1.01f,
                $"the push handed out {r.Push.Impulse:0.0}, more than the shot carried");
        }

        /// <summary>
        /// The push itself, on a cluster at rest: the momentum it hands out is the impulse it was given when no ball reaches
        /// <see cref="ShotPush.MAX_KICK"/>, no ball beyond <see cref="ShotPush.REACH"/> hops through the sockets is touched,
        /// every ball it reaches is awake afterwards, and an impulse a hundred times the shot's changes no ball's velocity by
        /// more than <see cref="ShotPush.MAX_KICK"/>.
        /// </summary>
        [Fact]
        public void ThePushHandsOutTheImpulseWithinItsReachAndItsCap()
        {
            using HungLevel hung = HungLevel.FromLevelFile(ShotRig.LevelPath("Kiln.json"), workerThreads: 1);
            hung.Run(8f);

            PhysicsBall struck = LowestBall(hung.Balls);
            XZLevel origin = struck.ArrayPosition;
            System.Numerics.Vector3 impulse = new(40f, 180f, 0f);

            Dictionary<PhysicsBall, System.Numerics.Vector3> before = Velocities(hung.Balls);
            bool anyAsleep = false;
            foreach (PhysicsBall ball in hung.Balls)
                if (ball != null && !ball.BallReference.Awake) anyAsleep = true;

            ShotPush.Result result = new ShotPush().Apply(hung.World.Simulation, hung.Balls, origin, impulse);

            System.Numerics.Vector3 handed = System.Numerics.Vector3.Zero;
            int changed = 0;
            foreach ((PhysicsBall ball, System.Numerics.Vector3 velocity) in before)
            {
                System.Numerics.Vector3 kick = ball.BallReference.Velocity.Linear - velocity;
                if (kick.LengthSquared() == 0f) continue;

                changed++;
                handed += kick / ball.BallReference.LocalInertia.InverseMass;
                Assert.True(kick.Length() <= ShotPush.MAX_KICK + 1e-3f, $"a ball's velocity changed by {kick.Length():0.00}");
                Assert.True(Hops(hung.Balls, origin, ball.ArrayPosition) <= ShotPush.REACH, "a ball beyond the push's reach was pushed");
                Assert.True(ball.BallReference.Awake, "a pushed ball was left asleep");
            }

            output.WriteLine($"Kiln: {changed} balls pushed, {result.Balls} reached, handed {handed}, cluster was {(anyAsleep ? "partly asleep" : "awake")}");
            Assert.Equal(result.Balls, changed);
            Assert.True(System.Numerics.Vector3.Distance(handed, impulse) < 0.01f * impulse.Length(),
                $"the push handed out {handed} for an impulse of {impulse}");

            //A hundred times the shot: every ball the walk reaches is held to the cap
            new ShotPush().Apply(hung.World.Simulation, hung.Balls, origin, impulse * 100f);
            foreach ((PhysicsBall ball, System.Numerics.Vector3 velocity) in Velocities(hung.Balls))
                Assert.True((velocity - before[ball]).Length() <= 2f * ShotPush.MAX_KICK + 1e-3f,
                    $"two pushes changed a ball's velocity by {(velocity - before[ball]).Length():0.00}");
        }

        /// <summary>
        /// #696's promise, kept under the push: shots in quick succession, each fired 0.3 s after the last landed into a cluster
        /// the last one set swinging, land in the cell their ghost showed, with both of the ghost's answers to a moving cluster
        /// as the Game fires (the shot keeps its promise, the ghost leads), and with <b>each of them alone</b>, so a change that
        /// broke either one fails here. Over <see cref="ShotAgreementTests"/>' six levels, sixteen shots each, they read 96 of
        /// 96 with both, 93 with the promise alone, 94 with the lead alone and 84 (87.5 %) with neither; held at
        /// <see cref="SUCCESSION_HELD"/>, which the two together and each alone clear by four shots or more and neither misses
        /// by five, so a solver that answers a shot or two differently on another machine (ARM64's) does not turn it.
        /// (With no push, Rainbow, Zigzag and Heart, which lose most with it, read 48 of 48 every way.)
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ShotsIntoAPushedClusterLandWhereTheirGhostShowed(bool promise, bool lead)
        {
            int compared = 0, same = 0;
            List<string> others = new();

            foreach (string level in new[] { "Pennant.json", "One.json", "Colossus.json", "Heart.json", "Rainbow.json", "Zigzag.json" })
            {
                (int c, int s, List<string> o) = Succession(level, 1192 + level.Length, 16, 0.3f, 1f, promise, lead);
                compared += c;
                same += s;
                others.AddRange(o);
            }

            output.WriteLine($"promise {promise}, lead {lead}: {same} of {compared} in the ghost's cell; {string.Join("; ", others)}");
            Assert.True(compared >= 80, $"only {compared} shots had a ghost to compare");
            Assert.True(same >= SUCCESSION_HELD * compared, $"{same} of {compared} landed in the ghost's cell: {string.Join("; ", others)}");
        }

        /// <summary>The share of <see cref="ShotsIntoAPushedClusterLandWhereTheirGhostShowed"/>'s shots that must land in the ghost's cell.</summary>
        private const float SUCCESSION_HELD = 0.92f;

        /// <summary>
        /// The swing a push starts is settled (<see cref="ShotPush.SETTLE_RATE"/>): a sheet and a strand a push set swaying fall
        /// asleep again within <see cref="SLEEP_WITHIN"/> seconds of the hit (measured 4.7 to 5.5 s; the same landings with no
        /// push slept in 3.7 and 3.8 s on the two sheets, and Pendant's strand not within fourteen), where with the push and no
        /// settle all three were still swaying, awake and stepped, fourteen seconds later. Its first
        /// second (<see cref="ShotPush.SETTLE_HOLD"/>) is left alone, so the hit still shows: half a second in, the farthest
        /// ball stands at least 0.2 from its unpushed twin.
        /// </summary>
        [Theory]
        [InlineData("Zigzag.json", 2)]
        [InlineData("Pennant.json", 2)]
        [InlineData("Pendant.json", 1)]
        public void APushedClusterFallsAsleepAgainWithinSeconds(string level, int seed)
        {
            Response r = Fire(level, seed, SLEEP_WITHIN);
            output.WriteLine(Row(level, seed, "settle", r));

            Assert.False(float.IsNaN(r.AwakeSeconds), $"the pushed cluster was still awake {SLEEP_WITHIN} s after the hit");
            Assert.True(r.At(0.5f) >= 0.2f, $"the push moved the farthest ball only {r.At(0.5f):0.000} in its first half second");
        }

        /// <summary>How long after a hit <see cref="APushedClusterFallsAsleepAgainWithinSeconds"/> allows.</summary>
        private const float SLEEP_WITHIN = 7f;

        /// <summary>
        /// A refused shot strikes too (#1192, "even when the ball does not attach"). Fired into a field filled to its walls,
        /// where every ball's two rings are full, the shot is refused at the ball it meets, and the cluster takes the momentum
        /// the touch hands over: the shot's velocity along the line between the two centres, once, never more than the whole
        /// momentum a landing hands over. The shot keeps only what runs across that line, so it slides off the ball rather
        /// than the solver bouncing it off and pushing the cluster a second time. Its twin, with the push off, does not move.
        /// </summary>
        [Fact]
        public void ARefusedShotPushesTheBallItMetOnceAndSlidesOff()
        {
            using RefusalRig pushed = new(1f);
            using RefusalRig still = new(0f);

            PhysicsBall shot = pushed.Fire(out int step);
            still.Fire(out _);

            Assert.True(step >= 0, "the shot was never refused");
            ShotPush.Result push = pushed.Handler.LastPush;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"refused at step {step}: {push.Balls} balls took {push.Impulse:0.0} of momentum, of the {ShotRig.SHOOT_SPEED * BallsConstraintsBuilder.BALL_MASS:0.0} the shot carried"));

            //A glance hands over the part of the momentum along the line between the centres, never the whole of it
            Assert.True(push.Balls > 0 && push.Impulse > 0f, "the refusal pushed nothing");
            Assert.True(push.Impulse < 0.99f * ShotRig.SHOOT_SPEED * BallsConstraintsBuilder.BALL_MASS,
                $"the refusal handed out {push.Impulse:0.0}, the whole of what the shot carried and not what the touch hands over");

            //Slid off: put back at the touch, nothing left of its velocity into the ball it met
            PhysicsBall met = Nearest(pushed.Hung.Balls, shot.BallReference.Pose.Position);
            System.Numerics.Vector3 normal = System.Numerics.Vector3.Normalize(shot.BallReference.Pose.Position - met.BallReference.Pose.Position);
            float into = -System.Numerics.Vector3.Dot(shot.BallReference.Velocity.Linear, normal);
            Assert.True(into < 0.05f * ShotRig.SHOOT_SPEED, $"the refused shot still moved {into:0.00} units a second into the ball it met");

            //And the cluster moved, where its twin's did not
            float farthest = 0f;
            for (int i = 0; i < (int)MathF.Round(0.3f / HungLevel.TIMESTEP); i++)
            {
                pushed.Hung.Run(HungLevel.TIMESTEP);
                still.Hung.Run(HungLevel.TIMESTEP);
                Compare(pushed.Hung.Balls, still.Hung.Balls, out float max, out _, out _, out _, out _);
                farthest = MathF.Max(farthest, max);
            }

            Assert.True(farthest >= 0.03f, $"the refusal moved no ball more than {farthest:0.000} from its twin in 0.3 s");
        }

        /// <summary>
        /// The push's walk is taken before the match takes anything (<see cref="ShotPush.Prepare"/>) and given after it: a shot
        /// that completes a group made of every ball beside its cell, a strand's dangling tip, still pushes the strand the group
        /// hung from. Walked after the release, from the emptied cell, it found nothing and pushed nothing at all.
        /// </summary>
        [Fact]
        public void AShotThatTakesEveryNeighbourStillPushesWhatTheyHungFrom()
        {
            //A strand of four from the glass, its two lowest of one colour, and a ball of that colour landed under its tip
            BallsMap map = new(7, 7, 10);
            List<XZLevel> strand = new() { new XZLevel(3, 3, 9) };
            BallType[] colours = { BallType.Type1, BallType.Type2, BallType.Type3, BallType.Type3 };
            XZLevel size = new(7, 7, 10);
            while (strand.Count < 5)
                foreach (XZLevel next in BallsMap.GetNeighboringCells(strand[^1], size))
                    if (next.Level == strand[^1].Level - 1) { strand.Add(next); break; }

            for (int i = 0; i < 4; i++) map.PutBallAt((byte)strand[i].X, (byte)strand[i].Z, (byte)strand[i].Level, colours[i]);

            using HungLevel hung = new(map, workerThreads: 1);
            hung.Run(1f);

            XZLevel under = strand[4];
            PhysicsBall landed = ClusterInvariantTests.Land(hung, under, BallType.Type3, live: true);
            System.Numerics.Vector3 impulse = new(0f, ShotRig.SHOOT_SPEED * BallsConstraintsBuilder.BALL_MASS, 0f);

            ShotPush push = new();
            push.Prepare(hung.Balls, under);

            List<PhysicsBall> released = new();
            BallsReleased result = BallsConstraintsBuilder.ReleaseSameTypeCluster(landed, hung.Balls, hung.Map, hung.World.Simulation, released);
            Assert.Equal(3, result.Matched);

            ShotPush.Result given = push.Deliver(hung.World.Simulation, hung.Balls, impulse);
            ShotPush.Result walkedAfter = new ShotPush().Apply(hung.World.Simulation, hung.Balls, under, impulse);
            output.WriteLine($"walked before the release: {given}; walked after it: {walkedAfter}");

            Assert.Equal(2, given.Balls);
            Assert.True(given.Impulse > 0f && given.Impulse < impulse.Length(), $"handed out {given.Impulse:0.0}");
            Assert.True(hung.Balls[strand[1].X, strand[1].Z, strand[1].Level].BallReference.Velocity.Linear.Y > 0.1f,
                "the ball the group hung from was not pushed");
            Assert.Equal(ShotPush.Result.None, walkedAfter);
        }

        private static PhysicsBall Nearest(PhysicsBall[,,] balls, System.Numerics.Vector3 to)
        {
            PhysicsBall nearest = null;
            float best = float.MaxValue;
            foreach (PhysicsBall ball in balls)
            {
                if (ball == null) continue;
                float d = System.Numerics.Vector3.DistanceSquared(ball.BallReference.Pose.Position, to);
                if (d < best) { best = d; nearest = ball; }
            }

            return nearest;
        }

        /// <summary>
        /// A field filled to its walls (4 x 4 x 4, every cell a ball), hung on one worker thread with the Game's own contact
        /// handler: every ball's two rings are full, so any shot into it is refused.
        /// </summary>
        private sealed class RefusalRig : IDisposable
        {
            private readonly List<PhysicsBall> _shots = new();
            private readonly List<PhysicsBall> _falling = new();

            public HungLevel Hung { get; }
            public BallContactEventHandler Handler { get; }

            public RefusalRig(float pushScale)
            {
                BallsMap map = new(4, 4, 4);
                for (byte x = 0; x < 4; x++)
                    for (byte z = 0; z < 4; z++)
                        for (byte l = 0; l < 4; l++)
                            map.PutBallAt(x, z, l, BallType.Type1);

                Hung = new HungLevel(map, workerThreads: 1);
                Hung.Run(1.5f);

                Handler = new BallContactEventHandler(Hung.World.Simulation, Hung.World.Events,
                    new KinematicBody(Hung.Ceiling, Hung.Ceiling.Handle), Hung.Map, Hung.Balls, _shots, _falling, Hung.WorldOffset)
                {
                    PushScale = pushScale,
                };
            }

            /// <summary>
            /// Fires a shot from below and to one side at the lowest ball nearest the field's middle, and steps until the
            /// handler refuses it (<paramref name="refusedAt"/>, -1 when it never is).
            /// </summary>
            public PhysicsBall Fire(out int refusedAt)
            {
                PhysicsBall target = null;
                foreach (PhysicsBall ball in Hung.Balls)
                    if (ball != null && ball.ArrayPosition.Level == 0 && ball.ArrayPosition.X == 1 && ball.ArrayPosition.Z == 1)
                        target = ball;

                //A glance, not a dead-centre hit: half a unit off the ball's centre, so the touch hands over only part of
                //what the shot carries and the rest is left to slide off with
                XVector3 at = target.BallReference.Pose.Position.ToXna();
                XVector3 muzzle = at + new XVector3(-4f, -8f, 1f);
                XVector3 velocity = XVector3.Normalize(at + new XVector3(0f, 0f, 0.5f) - muzzle) * ShotRig.SHOOT_SPEED;

                PhysicsBall shot = new()
                {
                    Type = BallType.Type13,
                    BallReference = Hung.World.AddShotBall(muzzle.ToNumerics(), velocity.ToNumerics(), Handler),
                };
                _shots.Add(shot);

                refusedAt = -1;
                for (int step = 0; step < 120; step++)
                {
                    Hung.World.Step(HungLevel.TIMESTEP, () => Handler.ProcessQueuedContacts());
                    if (Handler.LastPush.Balls > 0 || shot.Refused) { refusedAt = step; break; }
                }

                return shot;
            }

            public void Dispose() => Hung.Dispose();
        }

        private static PhysicsBall LowestBall(PhysicsBall[,,] balls)
        {
            PhysicsBall lowest = null;
            foreach (PhysicsBall ball in balls)
                if (ball != null && (lowest == null || ball.BallReference.Pose.Position.Y < lowest.BallReference.Pose.Position.Y))
                    lowest = ball;

            return lowest;
        }

        private static Dictionary<PhysicsBall, System.Numerics.Vector3> Velocities(PhysicsBall[,,] balls)
        {
            Dictionary<PhysicsBall, System.Numerics.Vector3> velocities = new();
            foreach (PhysicsBall ball in balls)
                if (ball != null) velocities[ball] = ball.BallReference.Velocity.Linear;

            return velocities;
        }

        /// <summary>Hops from <paramref name="from"/> to <paramref name="to"/> through occupied cells, as the push walks them.</summary>
        private static int Hops(PhysicsBall[,,] balls, XZLevel from, XZLevel to)
        {
            XZLevel size = XZLevel.FromArray(balls);
            Dictionary<XZLevel, int> hops = new() { [from] = 0 };
            Queue<XZLevel> queue = new();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                XZLevel cell = queue.Dequeue();
                if (cell.Equals(to)) return hops[cell];

                foreach (XZLevel next in BallsMap.GetNeighboringCells(cell, size))
                    if (!hops.ContainsKey(next) && balls[next.X, next.Z, next.Level] != null)
                    {
                        hops[next] = hops[cell] + 1;
                        queue.Enqueue(next);
                    }
            }

            return int.MaxValue;
        }

        /// <summary>One shot into a level and its twin: the push's own effect, step by step.</summary>
        internal sealed record Response(
            XZLevel? Landed, XZLevel? LandedWithout, ShotPush.Result Push,
            float[] MaxDisplacement, float[] MeanDisplacement, float PeakStretch, float PeakStretchWithout,
            float LowestDrop, int Balls, float AwakeSeconds = float.NaN, float AwakeSecondsWithout = float.NaN, float Deeper = 0f)
        {
            /// <summary>The largest any ball stood from its twin, over the whole run.</summary>
            public float Peak
            {
                get
                {
                    float peak = 0f;
                    foreach (float d in MaxDisplacement) peak = MathF.Max(peak, d);
                    return peak;
                }
            }

            public int PeakStep => Array.IndexOf(MaxDisplacement, Peak);

            /// <summary>The largest any ball stood from its twin at <paramref name="seconds"/> after the landing.</summary>
            public float At(float seconds) => MaxDisplacement[Math.Min(MaxDisplacement.Length - 1,
                (int)MathF.Round(seconds / HungLevel.TIMESTEP))];
        }

        /// <summary>A push given by hand in place of the handler's, for the report's sweep of the shape's own figures.</summary>
        internal readonly record struct Manual(float Scale, int Reach, float MaxKick, float Falloff = ShotPush.HOP_FALLOFF);

        /// <summary>
        /// Fires one aim (<see cref="ShotAgreementTests.RandomAim"/> under <paramref name="seed"/>) into a level and its twin
        /// and follows both for <paramref name="seconds"/>. The twin takes no push; the other the handler's own at
        /// <paramref name="scale"/>, or <paramref name="manual"/>'s given by hand right after the landing when it is set
        /// (the handler gives its push at the very end of that step's contact work, so the two are the same moment).
        /// </summary>
        internal static Response Fire(string level, int seed, float seconds, float scale = 1f, Manual? manual = null,
            BallKind kind = BallKind.Normal)
        {
            using ShotRig pushed = new(level, workerThreads: 1);
            using ShotRig still = new(level, workerThreads: 1);

            pushed.Handler.PushScale = manual == null ? scale : 0f;
            still.Handler.PushScale = 0f;

            Random rngA = new(seed), rngB = new(seed);
            (XVector3 muzzle, XVector3 target, float glance) = ShotAgreementTests.RandomAim(pushed.Hung.Balls, rngA);
            ShotAgreementTests.RandomAim(still.Hung.Balls, rngB);

            ShotResult a = pushed.Fire(muzzle, target, glance, kind: kind);
            ShotResult b = still.Fire(muzzle, target, glance, kind: kind);

            ShotPush.Result push = pushed.Handler.LastPush;
            if (manual is Manual m && a.Landed is XZLevel cell)
            {
                XVector3 velocity = XVector3.Normalize(target - muzzle) * ShotRig.SHOOT_SPEED;
                push = new ShotPush().Apply(pushed.Hung.World.Simulation, pushed.Hung.Balls, cell,
                    ShotPush.ImpulseOf(BallsConstraintsBuilder.BALL_MASS, velocity.ToNumerics(), kind, m.Scale), m.Reach, m.MaxKick, m.Falloff);
            }

            int steps = (int)MathF.Round(seconds / HungLevel.TIMESTEP);
            float[] max = new float[steps + 1], mean = new float[steps + 1];
            float stretch = 0f, stretchWithout = 0f, lowestDrop = 0f, deepestA = float.MaxValue, deepestB = float.MaxValue;
            int balls = 0;
            float awake = float.NaN, awakeWithout = float.NaN;

            for (int step = 0; step <= steps; step++)
            {
                if (step > 0)
                {
                    pushed.Hung.Run(HungLevel.TIMESTEP);
                    still.Hung.Run(HungLevel.TIMESTEP);
                }

                //When the whole cluster is asleep again, which is when its bodies stop costing a step
                if (float.IsNaN(awake) && !AnyAwake(pushed.Hung.Balls)) awake = step * HungLevel.TIMESTEP;
                if (float.IsNaN(awakeWithout) && !AnyAwake(still.Hung.Balls)) awakeWithout = step * HungLevel.TIMESTEP;

                Compare(pushed.Hung.Balls, still.Hung.Balls, out max[step], out mean[step], out balls, out float lowestA, out float lowestB);
                stretch = MathF.Max(stretch, PeakStretch(pushed.Hung.Balls));
                stretchWithout = MathF.Max(stretchWithout, PeakStretch(still.Hung.Balls));
                lowestDrop = MathF.Max(lowestDrop, lowestB - lowestA);
                deepestA = MathF.Min(deepestA, lowestA);
                deepestB = MathF.Min(deepestB, lowestB);
            }

            return new Response(a.Landed, b.Landed, push, max, mean, stretch, stretchWithout, lowestDrop, balls, awake, awakeWithout,
                deepestB - deepestA);
        }

        private static bool AnyAwake(PhysicsBall[,,] balls)
        {
            foreach (PhysicsBall ball in balls)
                if (ball != null && ball.BallReference.Awake) return true;

            return false;
        }

        /// <summary>How far each ball of one world stands from the same ball of its twin.</summary>
        private static void Compare(PhysicsBall[,,] a, PhysicsBall[,,] b, out float max, out float mean, out int count,
            out float lowestA, out float lowestB)
        {
            max = 0f;
            float sum = 0f;
            count = 0;
            lowestA = lowestB = float.MaxValue;

            for (int x = 0; x < a.GetLength(0); x++)
                for (int z = 0; z < a.GetLength(1); z++)
                    for (int l = 0; l < a.GetLength(2); l++)
                    {
                        PhysicsBall ballA = a[x, z, l], ballB = b[x, z, l];
                        if (ballA == null || ballB == null) continue;

                        XVector3 pa = ballA.BallReference.Pose.Position.ToXna(), pb = ballB.BallReference.Pose.Position.ToXna();
                        float d = (pa - pb).Length();
                        max = MathF.Max(max, d);
                        sum += d;
                        count++;
                        lowestA = MathF.Min(lowestA, pa.Y);
                        lowestB = MathF.Min(lowestB, pb.Y);
                    }

            mean = count > 0 ? sum / count : 0f;
        }

        /// <summary>
        /// The most any socket between two balls stands stretched past its length: every pair of lattice neighbours sits one
        /// diameter apart at rest, whatever the level (same level a unit apart, across levels half a unit each way and 1/√2 up).
        /// </summary>
        internal static float PeakStretch(PhysicsBall[,,] balls)
        {
            XZLevel size = XZLevel.FromArray(balls);
            float peak = 0f;

            for (int x = 0; x < size.X; x++)
                for (int z = 0; z < size.Z; z++)
                    for (int l = 0; l < size.Level; l++)
                    {
                        PhysicsBall ball = balls[x, z, l];
                        if (ball == null) continue;

                        foreach (XZLevel n in BallsMap.GetNeighboringCells(new XZLevel(x, z, l), size))
                        {
                            PhysicsBall other = balls[n.X, n.Z, n.Level];
                            if (other == null) continue;

                            float d = (ball.BallReference.Pose.Position - other.BallReference.Pose.Position).Length();
                            peak = MathF.Max(peak, d - 2f * BallsConstraintsBuilder.BALL_RADIUS);
                        }
                    }

            return peak;
        }

        private static string Cell(XZLevel? cell) => cell is XZLevel c ? $"({c.X},{c.Z},{c.Level})" : "none";

        private static string Row(string level, int seed, string label, Response r) => string.Format(CultureInfo.InvariantCulture,
            "{0,-14} seed {1,3} {2,-22} balls {3,4} push {4,3} balls {5,6:0.0} | peak {6:0.000} at {7:0.00} s | 0.1 s {8:0.000} 0.2 s {9:0.000} 0.3 s {10:0.000} 0.5 s {11:0.000} 1 s {12:0.000} 2 s {13:0.000} | mean at peak {14:0.000} | stretch {15:0.000} (without {16:0.000}) | lowest down {17:0.000}, deepest {21:0.000} under the twin's deepest | cell {18} | asleep after {19:0.00} s (without {20:0.00} s)",
            level, seed, label, r.Balls, r.Push.Balls, r.Push.Impulse, r.Peak, r.PeakStep * HungLevel.TIMESTEP,
            r.At(0.1f), r.At(0.2f), r.At(0.3f), r.At(0.5f), r.At(1f), r.At(2f), r.MeanDisplacement[r.PeakStep],
            r.PeakStretch, r.PeakStretchWithout, r.LowestDrop,
            r.Landed == null ? "none" : r.Landed.Equals(r.LandedWithout) ? "same" : "DIFFERS", r.AwakeSeconds, r.AwakeSecondsWithout, r.Deeper);

        /// <summary>
        /// The measurement the push's figures were chosen from, off unless <c>BS3D_SHOT_PUSH=&lt;aims per level&gt;</c> is set (it
        /// takes minutes): dense blocks, thin sheets and strands, each shot at with the shipped push and a sweep around it.
        /// </summary>
        [Fact]
        public void TheReportSaysHowFarAPushMovesTheCluster()
        {
            //How long each shot is followed (BS3D_SHOT_PUSH_SECONDS, 2 by default): long enough to see a pushed cluster fall
            //asleep again, which the asleep column reads, takes more
            float ReportSeconds = float.TryParse(Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SECONDS"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float seconds) && seconds > 0f ? seconds : 2f;

            if (!int.TryParse(Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH"), out int aims) || aims <= 0)
            {
                output.WriteLine("BS3D_SHOT_PUSH is not set: the measurement is skipped.");
                return;
            }

            string[] levels = (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_LEVELS")
                ?? "One.json,Kiln.json,Colossus.json,Pennant.json,Sail.json,Hammock.json,Pendant.json,Amphora.json,Rope.json,Zigzag.json")
                .Split(',');

            List<(string Label, float Scale, Manual? Manual)> configurations = new()
            {
                ("shipped", 1f, null),
            };

            string sweep = Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SWEEP");
            if (!string.IsNullOrEmpty(sweep))
                foreach (string item in sweep.Split(';'))
                {
                    string[] parts = item.Split(':');
                    Manual m = new(float.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        parts.Length > 3 ? float.Parse(parts[3], CultureInfo.InvariantCulture) : ShotPush.HOP_FALLOFF);
                    configurations.Add((string.Create(CultureInfo.InvariantCulture, $"x{m.Scale} r{m.Reach} k{m.MaxKick} f{m.Falloff}"), 0f, m));
                }

            foreach (string level in levels)
                for (int seed = 1; seed <= aims; seed++)
                    foreach ((string label, float scale, Manual? manual) in configurations)
                    {
                        Response r = Fire(level, seed, ReportSeconds, scale, manual);
                        if (r.Landed == null) { output.WriteLine($"{level} seed {seed}: no landing"); break; }
                        output.WriteLine(Row(level, seed, label, r));
                    }
        }

        /// <summary>
        /// Shots fired one after another into one hung level, each <paramref name="gap"/> seconds after the last landed, so
        /// every shot after the first meets a cluster the last one pushed (<see cref="ShotRig"/>, one worker thread): how many
        /// landed in the cell their ghost showed as they left. Every landing completes no group, so the cluster only grows.
        /// </summary>
        internal static (int Compared, int Same, List<string> Others) Succession(string level, int seed, int shots, float gap,
            float scale, bool promise = true, bool lead = true)
        {
            using ShotRig rig = new(level, workerThreads: 1);
            rig.Handler.PushScale = scale;
            rig.StampsPromise = promise;
            rig.Leads = lead;

            Random rng = new(seed);
            int compared = 0, same = 0;
            List<string> others = new();

            for (int i = 0; i < shots; i++)
            {
                (XVector3 muzzle, XVector3 target, float glance) = ShotAgreementTests.RandomAim(rig.Hung.Balls, rng);
                ShotResult result = rig.Fire(muzzle, target, glance);

                if (result.Outcome != ShotOutcome.NoGhost)
                {
                    compared++;
                    if (result.Outcome == ShotOutcome.SameCell) same++;
                    else others.Add($"{level} shot {i}: {result.Outcome} (promised {Cell(result.Promised)}, landed {Cell(result.Landed)})");
                }

                rig.Hung.Run(gap);
            }

            return (compared, same, others);
        }

        /// <summary>
        /// The measurement of what the push costs the ghost, off unless <c>BS3D_SHOT_PUSH_SUCCESSION=&lt;shots per level&gt;</c>
        /// is set: <see cref="ShotAgreementTests"/>' six levels, shots in quick succession with the push and without, at
        /// the gaps <c>BS3D_SHOT_PUSH_GAPS</c> lists (seconds between a landing and the next shot; 0.3 and 1 by default).
        /// </summary>
        [Fact]
        public void TheReportSaysHowOftenAPushedClusterStillTakesTheShotWhereTheGhostShowed()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SUCCESSION"), out int shots) || shots <= 0)
            {
                output.WriteLine("BS3D_SHOT_PUSH_SUCCESSION is not set: the measurement is skipped.");
                return;
            }

            string[] levels = (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_LEVELS")
                ?? "Pennant.json,One.json,Colossus.json,Heart.json,Rainbow.json,Zigzag.json").Split(',');
            string[] gaps = (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_GAPS") ?? "0.3,1").Split(',');

            //The push's scales to compare, none and the shipped one by default (BS3D_SHOT_PUSH_SCALES, comma-separated)
            static IEnumerable<float> Scales()
            {
                foreach (string scale in (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_SCALES") ?? "0,1").Split(','))
                    yield return float.Parse(scale, CultureInfo.InvariantCulture);
            }

            //Which of the ghost's two answers to a moving cluster each pass keeps: the promise (PhysicsBall.PromisedCell) and
            //the lead (ShotPlacement.TryFindFirstHitCurved's), as "promise:lead" pairs of 0 and 1 (BS3D_SHOT_PUSH_MODES)
            static IEnumerable<(bool, bool)> Modes()
            {
                foreach (string mode in (Environment.GetEnvironmentVariable("BS3D_SHOT_PUSH_MODES") ?? "0:0,1:1").Split(','))
                    yield return (mode[0] == '1', mode[2] == '1');
            }

            foreach (string gapText in gaps)
            {
                float gap = float.Parse(gapText, CultureInfo.InvariantCulture);

                foreach (float scale in Scales())
                    foreach ((bool promise, bool lead) in Modes())
                    {
                        int compared = 0, same = 0;
                        List<string> others = new();

                        foreach (string level in levels)
                        {
                            (int c, int s, List<string> o) = Succession(level, 1192 + level.Length, shots, gap, scale, promise, lead);
                            compared += c;
                            same += s;
                            others.AddRange(o);
                        }

                        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"gap {gap:0.0#} s, push x{scale}, promise {(promise ? "kept" : "not kept")}, {(lead ? "leading" : "not leading")}: {same} of {compared} in the ghost's cell ({100.0 * same / Math.Max(1, compared):0.0} %)"));
                        foreach (string other in others) output.WriteLine("  " + other);
                    }
            }
        }
    }
}
