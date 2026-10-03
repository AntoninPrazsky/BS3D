using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>What one shot of the rig did, set against what the landing preview promised it (#696).</summary>
    internal enum ShotOutcome
    {
        /// <summary>It stuck in the cell the ghost showed.</summary>
        SameCell,

        /// <summary>It stuck, in another cell than the ghost's.</summary>
        OtherCell,

        /// <summary>The ghost showed a cell and the shot never stuck: it was refused, or flew on to the stone.</summary>
        Missed,

        /// <summary>The shot had not resolved when the run ended (rolling, wedged).</summary>
        Unresolved,

        /// <summary>The ghost showed nothing, so there is nothing to compare: the aim was off the cluster.</summary>
        NoGhost,
    }

    /// <summary>
    /// One shot: the aim, the cell the ghost promised and the one it landed in, and how far the simulated flight stood
    /// from the preview's integration at its worst step before the landing.
    /// </summary>
    internal readonly record struct ShotResult(ShotOutcome Outcome, XZLevel? Promised, XZLevel? Landed, float GlanceOffset,
        int Steps, float FlightDeviation);

    /// <summary>
    /// A real level hung in the real simulation with the Game's own contact handler on it, and shots fired the way the
    /// Game fires them (<c>SHOOT_SPEED</c> 200, one 1/120 s step at a time, the handler's contact work inside the step),
    /// each compared with what the landing preview said before it left (#696). The instrument the issue's own
    /// <c>logshots</c> runs were: <c>logshots</c> prints one line per shot from a window, this does the same over
    /// hundreds of shots with no window, so a change to how a shot is handled can be measured rather than judged.
    /// <para>
    /// <b>The preview is asked exactly as the Game asks it</b> (<see cref="ShotPlacement.TryFindFirstHitCurved"/> with the
    /// world's gravity, then <see cref="ShotPlacement.TrySolveAgainstBall"/>, <c>GameplayScreen.Draw</c>), from the
    /// barrel's line, and a shot is fired from there the moment after.
    /// </para>
    /// </summary>
    internal sealed class ShotRig : IDisposable
    {
        public const float SHOOT_SPEED = 200f;

        private const int MAX_STEPS = 360;

        private readonly HungLevel _hung;
        private readonly BallContactEventHandler _handler;
        private readonly List<PhysicsBall> _shots = new();
        private readonly List<PhysicsBall> _falling = new();
        private XZLevel? _landed;
        private bool _spent;

        public HungLevel Hung => _hung;

        /// <param name="levelFile">A file of <c>Game/Levels</c>.</param>
        /// <param name="settleSeconds">How long the level hangs before anything is fired at it: a level is long settled by the
        /// time a player fires, and a cluster still sagging is a moving target no ghost can promise.</param>
        public ShotRig(string levelFile, float settleSeconds = 1.5f)
        {
            _hung = HungLevel.FromLevelFile(LevelPath(levelFile));
            _hung.Run(settleSeconds);

            KinematicBody ceiling = new(_hung.Ceiling, _hung.Ceiling.Handle);
            _handler = new BallContactEventHandler(_hung.World.Simulation, _hung.World.Events, ceiling, _hung.Map, _hung.Balls,
                _shots, _falling, _hung.WorldOffset);
            _handler.BallLanded += landing => _landed = landing.Cell;
            _handler.ShotSpent += () => _spent = true;
        }

        /// <summary>Fires one shot from <paramref name="muzzle"/> at <paramref name="target"/> (a point in world space) and plays it out.</summary>
        /// <param name="glance">Only carried into the result, for the report.</param>
        /// <param name="type">The ball's colour. One no level uses by default, so a landing completes no group and the structure
        /// stays as it was.</param>
        public ShotResult Fire(XVector3 muzzle, XVector3 target, float glance, BallType type = BallType.Type13)
        {
            XVector3 aim = XVector3.Normalize(target - muzzle);
            XVector3 velocity = aim * SHOOT_SPEED;

            XZLevel? promised = null;
            if (ShotPlacement.TryFindFirstHitCurved(_hung.Balls, muzzle, velocity, 2f * BallsConstraintsBuilder.BALL_RADIUS, null,
                    out PhysicsBall hit, out XVector3 contact, worldGravity: true)
                && ShotPlacement.TrySolveAgainstBall(_hung.Map, hit, contact, _hung.WorldOffset, out XZLevel cell, out _))
                promised = cell;

            if (promised == null) return new ShotResult(ShotOutcome.NoGhost, null, null, glance, 0, 0f);

            _landed = null;
            _spent = false;

            PhysicsBall shot = new() { Type = type, BallReference = _hung.World.AddShotBall(muzzle.ToNumerics(), velocity.ToNumerics(), _handler) };
            _shots.Add(shot);

            //The preview's own integration of the flight, step by step: semi-implicit Euler at the simulation's step with
            //the world's gravity, which is what ShotPlacement steps through. The shot's distance from it is what the ghost's
            //honesty rests on
            XVector3 euler = muzzle, eulerVelocity = velocity;
            float deviation = 0f;

            int steps = 0;
            for (; steps < MAX_STEPS; steps++)
            {
                _hung.World.Step(HungLevel.TIMESTEP, () => _handler.ProcessQueuedContacts());
                if (_landed != null || _spent) { steps++; break; }

                eulerVelocity.Y += Constants.EARTH_GRAVITY * HungLevel.TIMESTEP;
                euler += eulerVelocity * HungLevel.TIMESTEP;
                deviation = MathF.Max(deviation, (shot.BallReference.Pose.Position.ToXna() - euler).Length());
            }

            if (_landed == null && !_spent)
            {
                if (_hung.World.Events.IsListener(shot.BallReference.CollidableReference)) _hung.World.Events.Unregister(shot.BallReference.CollidableReference);
                _shots.Remove(shot);
                _hung.World.Simulation.Bodies.Remove(shot.BallReference.Handle);
                return new ShotResult(ShotOutcome.Unresolved, promised, null, glance, steps, deviation);
            }

            if (_landed == null)
            {
                _shots.Remove(shot);
                if (_hung.World.Simulation.Bodies.BodyExists(shot.BallReference.Handle)) _hung.World.RetireBall(shot.BallReference);
                return new ShotResult(ShotOutcome.Missed, promised, null, glance, steps, deviation);
            }

            return new ShotResult(_landed.Value.Equals(promised.Value) ? ShotOutcome.SameCell : ShotOutcome.OtherCell, promised, _landed,
                glance, steps, deviation);
        }

        public void Dispose() => _hung.Dispose();

        public static string LevelPath(string file)
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Game", "Levels", file);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException(file);
        }
    }
}
