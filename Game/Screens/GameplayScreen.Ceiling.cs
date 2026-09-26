using BepuPhysics.Collidables;
using BepuPhysics;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;


namespace BS3D.Screens
{
    /// <summary>
    /// <b>The ceiling and its pressure</b> — the kinematic body the whole cluster hangs from, and what a step of
    /// its descent does to the world: the wake, the ripple through the cluster, the sound, the rumble and the
    /// tutorial card. The state machine that walks it down — a step queued, held, gated behind any cinematic
    /// already running, slid and flashed — is <see cref="CeilingDescent"/>'s since #582.
    /// </summary>
    /// <remarks>
    /// The sequence is deliberately one state machine in one place rather than a flag per stage — the stages
    /// exist because a step must not land in the middle of a spectacle the player earned, and that is only
    /// readable end to end, which is why it moved out whole into <see cref="CeilingDescent"/> (#582). The wake
    /// pass stays here with the body: a body Bepu has put to sleep does not answer a kinematic move, so a
    /// descent that arrives while the cluster is asleep has to wake it first (#78). Split out of
    /// <c>GameplayScreen.cs</c> in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// The glass plate, as physics. Kinematic rather than static because a <c>BallSocket</c> needs a body at
        /// both ends — Bepu constraints do not take statics — and the whole cluster hangs from this one.
        /// </summary>
        private void BuildCeilingBody()
        {
            //Sized off CeilingPlate's own footprint and thickness — the very figures FitCeilingToMap gave the
            //drawn box, asked of the one place that applies the margin, so the glass and the collidable cannot
            //be given different numbers.
            Box box = new(CeilingPlate.FootprintFor(_map.StageSizeX), CeilingPlate.THICKNESS,
                CeilingPlate.FootprintFor(_map.StageSizeZ));
            TypedIndex shape = _world.Simulation.Shapes.Add(box);

            BodyHandle handle = _world.Simulation.Bodies.Add(BodyDescription.CreateKinematic(
                new System.Numerics.Vector3(0f, _ceilingDescent.Y, 0f),
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));

            _ceiling = new KinematicBody(new BodyReference(handle, _world.Simulation.Bodies), handle);
        }

        /// <summary>
        /// Wakes the glass plate and the structure hanging from it, on the frame a descent begins (#78).
        /// <para>
        /// The descent moves the plate by writing its pose directly, and a sleeping body does not integrate:
        /// Bepu will not drag a sleeping cluster down because a kinematic's pose was overwritten under it. The
        /// cluster is <i>designed</i> to fall asleep between shots — both it and the plate carry
        /// <see cref="PhysicsWorld.SLEEP_THRESHOLD"/> — so a step coming due over a settled cluster had the
        /// glass slide straight through it, and the death-line walk read unchanged poses, which meant the
        /// ceiling-pressure loss could never fire from a settled cluster at all.
        /// </para>
        /// <para>
        /// <b>Both</b> are woken, rather than relying on one to reach the other. Waking a body wakes the whole
        /// sleeping set it belongs to, so one ball is enough for the cluster — the structure is a single
        /// connected constraint graph and its whole island comes up with whichever member is touched. Whether
        /// waking the <i>kinematic</i> plate would have reached the dynamics on its own was deliberately not
        /// relied on: a kinematic can be referenced by several sleeping sets at once and is not islanded with
        /// them the way a dynamic is. That is the reason for waking both and not a measurement — this was never
        /// tested with only the plate woken, because there is no reason to want the weaker guarantee. It runs
        /// once per step, not per frame.
        /// </para>
        /// </summary>
        private void WakeForDescent()
        {
            _world.Simulation.Awakener.AwakenBody(_ceiling.BodyHandle);

            if (_physicsBalls == null) return;

            for (int level = _physicsBalls.GetLength(2) - 1; level >= 0; level--)
                for (int x = 0; x < _physicsBalls.GetLength(0); x++)
                    for (int z = 0; z < _physicsBalls.GetLength(1); z++)
                        if (_physicsBalls[x, z, level] != null)
                        {
                            _world.Simulation.Awakener.AwakenBody(_physicsBalls[x, z, level].BallReference.Handle);
                            return;
                        }
        }

        /// <summary>
        /// Announces a step <see cref="CeilingDescent.Update"/> has just begun — everything a step does besides
        /// moving the glass, which <see cref="CeilingDescent"/> owns. The plate is about to be moved by writing its
        /// pose, so this wakes it first; then the wave, the sound, the rumble, the tutorial card and the log line,
        /// and last a tall level's aim clamp re-solved for where the step takes the column.
        /// </summary>
        /// <param name="feeding">Whether the step is one the tall-level feed asked for rather than the pressure.</param>
        /// <param name="waited">
        /// Seconds the step spent queued before it was let go — on the line because it is the one figure that
        /// says whether the deferral did anything, and a step that waited seconds is one that sat out a drop
        /// cinematic (see <see cref="CeilingDescent.Update"/>).
        /// </param>
        private void AnnounceCeilingStep(bool feeding, float waited)
        {
            //The plate is about to be moved by writing its pose, and both it and the cluster are very likely
            //asleep — measured asleep, in fact, on a step that came due over a settled cluster (#78)
            WakeForDescent();

            //The glass has lit up in the colour CeilingDescent picked for this step — red for the pressure, blue
            //for a feed, and why they differ is written there — and the wave it drives down through every ball
            //hanging on it says the same.
            Game.Balls.RippleAlarmColor = feeding ? RIPPLE_FEED_COLOR : RIPPLE_ALARM_COLOR;
            _ripple.StartFromTop(_physicsBalls);

            //And is heard (#500): from the plate, where it is, the feed's step softer than the pressure's
            Game.Audio.PlayCeilingStep(new Microsoft.Xna.Framework.Vector3(0f, _ceilingDescent.Y, 0f), feeding);

            //And felt (#378), the feed step soft for the same reason it is heard and seen soft.
            float ceilingRumbleScale = feeding ? CEILING_RUMBLE_FEED_SCALE : 1f;
            Game.Rumble.Kick(CEILING_RUMBLE_LEFT * ceilingRumbleScale, CEILING_RUMBLE_RIGHT * ceilingRumbleScale,
                CEILING_RUMBLE_SECONDS);

            //The tutorial's glass lesson fires on the step the shot count forced, on the frame the plate lights
            //(#189) — never on a feed step, which is a tall level's reward and would teach the pressure in the
            //wrong colour
            if (!feeding) _tutorial.Trigger(Tutorial.Lesson.Ceiling);

            Console.WriteLine($"[ceiling] Step to {_ceilingDescent.TargetY:F2} (death line {CEILING_DEATH_Y:F2})"
                + $", {(feeding ? "feeding" : "pressure")}"
                + $", shots fired {_run.Score.ShotsFired}, waited {waited:F2} s");

            //And a tall level's aim clamp follows the column down (#582) — it was solved once at the load and
            //loosened by every step after it
            ResolveTallAimLimit();
        }

        /// <summary>
        /// Brings a tall level's column back down to where the player can shoot it. Asked on every landing,
        /// which is the only thing that can change the cluster.
        /// <para>
        /// <b>On a tall level the descent is not only the pressure, it is how the level is delivered</b> — and
        /// a delivery driven by the shot count is the wrong shape, because how much of the column a shot
        /// takes is not a function of how many shots were fired. One good ball into a band can cut fifty
        /// loose and lift the underside four levels in an instant; a fixed cadence then leaves the player
        /// staring at a ceiling of empty lattice with the next band still overhead, and the level stalls.
        /// So the feed answers the <i>state</i>: however far the underside has climbed since the level was
        /// authored, the glass owes that many descents, and it is asked for them the moment it happens.
        /// </para>
        /// <para>
        /// It only ever adds descents. The shot-driven <c>ceilingStep</c> still runs underneath it and is
        /// still the pressure — the feed cannot relieve it, cannot raise the glass, and cannot push the
        /// cluster past the death line either: it brings the underside back to a height the level started
        /// at and stops, so what it hands the player is the same clearance they opened with.
        /// </para>
        /// <para>
        /// The steps are <b>queued</b> rather than taken (<see cref="CeilingDescent.Feed"/>), so a cascade that
        /// owes ten of them pours down one at a time through <see cref="CeilingDescent.Update"/>'s hold rather
        /// than arriving as one lurch — and so a feed landing on a drop cinematic waits it out like any other step.
        /// </para>
        /// </summary>
        private void FeedTallColumn()
        {
            if (!FieldIsTallerThanFrame || _map == null) return;

            //An empty map answers GetLowestOccupiedLevel with the field's TOP level — "the layout hangs
            //nowhere" — which reads here as the underside having climbed the whole field and queues a descent
            //for every level of it. Measured: 20 steps on the frame the column was cleared. There is nothing
            //left to feed, and the level is about to end anyway.
            if (_map.GetBallsCount() == 0) return;

            byte lowest = _map.GetLowestOccupiedLevel();
            int owed = _ceilingDescent.Feed(lowest, out float risen);

            if (owed <= 0) return;

            //A rare-event line like the rest of the [ceiling] family: it fires when a band goes, not per
            //frame, and it is the one figure that says whether the feed is keeping up with the player.
            Console.WriteLine($"[ceiling] feeding {owed} step(s): the underside has climbed to level {lowest}, "
                + $"{risen:F1} above where the level hung it");
        }

        /// <summary>
        /// Slides the ceiling body by one physics step's worth of <see cref="CeilingDescent.Slide"/> and refreshes
        /// the drawn world matrix to match. Called by <see cref="StepPhysics"/> before each step (#577), so the
        /// solver works against the moved body — see <see cref="CeilingDescent.Slide"/> for why.
        /// </summary>
        private void SlideCeiling(float step)
        {
            if (!_ceilingDescent.Slide(step)) return;

            _ceiling.BodyReference.Pose.Position = new System.Numerics.Vector3(0f, _ceilingDescent.Y, 0f);
            _ceiling.RefreshWorld();
        }
    }
}
