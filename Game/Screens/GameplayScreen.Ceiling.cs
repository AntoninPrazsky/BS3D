using BepuPhysics.Collidables;
using BepuPhysics;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
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
        /// <b>Both</b> are woken, rather than relying on one to reach the other, and <b>every ball</b> of the cluster
        /// rather than one. Waking a body wakes the whole sleeping set it belongs to, but a cluster that has been shot
        /// apart is several islands (a kinematic plate joins none of them: it is referenced by several sleeping sets
        /// at once and is not islanded with them the way a dynamic is), and waking one ball wakes only its own — the
        /// other column would be left asleep at its old height while the plate moved away from it, which for a descent
        /// hides the loss line's walk from that column and for the brake (#213) leaves a rescued cluster half
        /// rescued. It was one ball until #213, on the reading that the structure is one constraint graph, which
        /// stops being true at the first shot that cuts it. Balls already awake are skipped, so a cluster in motion
        /// costs a flag read each. It runs once per step, not per frame.
        /// </para>
        /// </summary>
        private void WakeForDescent()
        {
            _world.Simulation.Awakener.AwakenBody(_ceiling.BodyHandle);

            if (_physicsBalls == null) return;

            for (int level = _physicsBalls.GetLength(2) - 1; level >= 0; level--)
                for (int x = 0; x < _physicsBalls.GetLength(0); x++)
                    for (int z = 0; z < _physicsBalls.GetLength(1); z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball != null && !ball.BallReference.Awake)
                            _world.Simulation.Awakener.AwakenBody(ball.BallReference.Handle);
                    }
        }

        /// <summary>
        /// Announces a step <see cref="CeilingDescent.Update"/> has just begun — everything a step does besides
        /// moving the glass, which <see cref="CeilingDescent"/> owns. The plate is about to be moved by writing its
        /// pose, so this wakes it first; then the wave, the sound, the rumble, the tutorial card and the log line,
        /// and last a tall level's aim clamp lifted, if the step brings the column whole into the frame (#842).
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
            if (!feeding)
            {
                _tutorial.Trigger(Tutorial.Lesson.Ceiling);

                //And the Brake's card (#705), armed only on a level that grants one: a pressure step is the moment it has a
                //step to give back
                _tutorial.Trigger(Tutorial.Lesson.Brake);
            }

            Console.WriteLine($"[ceiling] Step to {_ceilingDescent.TargetY:F2} (death line {CEILING_DEATH_Y:F2})"
                + $", {(feeding ? "feeding" : "pressure")}"
                + $", shots fired {_run.Score.ShotsFired}, waited {waited:F2} s");

            //And a tall level's aim clamp, held since the load, lets go once the column hangs whole in the frame (#842)
            LiftTallAimOnceFramed();
            MarkBrakeReachStale();
        }

        /// <summary>
        /// Announces the brake <see cref="CeilingDescent.Brake"/> has just begun (#213) — the lift's counterpart of
        /// <see cref="AnnounceCeilingStep"/>, in the feed's own soft blue for the same reason: it is good news the
        /// player asked for. The plate and the cluster are woken first (both are very likely asleep, and the plate
        /// is about to be moved by writing its pose), then the wave, the plate's sound and a soft rumble (#836). A tall
        /// level's aim clamp is held at the load's height (#842), so a lift leaves it alone.
        /// </summary>
        private void AnnounceCeilingBrake()
        {
            WakeForDescent();

            Game.Balls.RippleAlarmColor = RIPPLE_FEED_COLOR;
            _ripple.StartFromTop(_physicsBalls);

            Game.Audio.PlayCeilingStep(new Microsoft.Xna.Framework.Vector3(0f, _ceilingDescent.Y, 0f), feed: true);

            //And felt (#836: the owner felt nothing when the Brake lifted the glass), over the same slide a descent takes
            Game.Rumble.Kick(BRAKE_RUMBLE_LEFT, BRAKE_RUMBLE_RIGHT, CEILING_RUMBLE_SECONDS);

            Console.WriteLine($"[ceiling] Brake to {_ceilingDescent.TargetY:F2} (rest {_ceilingDescent.RestY:F2}"
                + $", death line {CEILING_DEATH_Y:F2}), {_run.PowerupCharges[(int)PowerupKind.Brake]} left");

            MarkBrakeReachStale();
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
        /// So the feed answers the <i>state</i>: once the underside has climbed out of the gun's reach, the glass
        /// owes the descents that bring it back <b>into reach</b> (<see cref="StepsIntoReach"/>), and it is asked
        /// for them the moment it happens.
        /// </para>
        /// <para>
        /// <b>Into reach and no further (#843, the owner's rule).</b> Until then it owed as many descents as the
        /// underside had climbed since the level was authored, so it walked the column all the way back to the
        /// height it opened at — 193 blue steps against 15 red over The Spectrum in the owner's run, the feed and
        /// not the shot count bringing a tall level's glass low. A feed exists so the player can shoot something
        /// at all; only the pressure may bring the glass very low.
        /// </para>
        /// <para>
        /// It only ever adds descents. The shot-driven <c>ceilingStep</c> still runs underneath it and is
        /// still the pressure — the feed cannot relieve it, cannot raise the glass (only the player's brake does, #213, and never
        /// past where the level hung it), and cannot push the
        /// cluster past the death line either: it brings the underside back into reach and stops, never lower than
        /// the height the level started at, so what it hands the player is never less clearance than they opened with.
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

            //The cluster has just changed, and with it what a brake's lift would leave in reach
            MarkBrakeReachStale();

            //An empty map answers GetLowestOccupiedLevel with the field's TOP level — "the layout hangs
            //nowhere" — which reads here as the underside having climbed the whole field and queues a descent
            //for every level of it. Measured: 20 steps on the frame the column was cleared. There is nothing
            //left to feed, and the level is about to end anyway.
            if (_map.GetBallsCount() == 0) return;

            byte lowest = _map.GetLowestOccupiedLevel();
            int owed = _ceilingDescent.Feed(lowest, StepsIntoReach(lowest), out float risen, out int heightOwed);

            //A rare-event line like the rest of the [ceiling] family: it fires when a band goes, not per frame, and
            //it is the one figure that says whether the feed is keeping up with the player — and, beside it, how
            //many the climb alone would have asked for before #843. Printed too when the underside has just climbed
            //and stayed in reach, so a band that went WITHOUT a feed is as visible as one that brought one
            bool climbed = lowest > _feedWatchedLowest;
            _feedWatchedLowest = lowest;

            if (owed <= 0 && !(climbed && heightOwed > 0)) return;

            Console.WriteLine($"[ceiling] feeding {owed} step(s) into reach: the underside has climbed to level {lowest}, "
                + $"{risen:F1} above where the level hung it ({heightOwed} would have taken it back there)");
        }

        //The underside's level at the last landing the feed was asked about: what tells a landing that lifted the
        //underside from one that did not, for the log line above. Taken at the load (FitFieldToMap).
        private byte _feedWatchedLowest;

        /// <summary>
        /// How many descents bring a tall column's underside, at <paramref name="lowest"/>, within the gun's reach
        /// (#843): the fewest steps, counted from where the glass will hang once every queued step has come down
        /// (<see cref="CeilingDescent.QueuedTargetY"/>), after which every cell of the underside's level is a shot
        /// the gun can take facing it under its clamp — zero when it already is.
        /// <para>
        /// The same question in the same arithmetic the clamp was solved with (<see cref="AimReachability"/>, the
        /// gun at its rest stance, the cluster's horizontal reach held at the load), so the feed and the clamp
        /// cannot disagree about what "in reach" means: a feed that stopped a hair short of what the clamp allows
        /// would leave the underside one degree out of reach for good. The whole level and not one ball, because
        /// the gun orbits and the facing shot is the steepest one; asked of a level, the player can take the
        /// underside from wherever the gun stands.
        /// </para>
        /// <para>
        /// On every landing of a tall level, never per frame; the first step asked about passes on almost every
        /// landing, and the walk is bounded by the glass reaching the death line.
        /// </para>
        /// </summary>
        private int StepsIntoReach(byte lowest)
        {
            float hangY = QueuedHangY;

            //No further than the glass can go: past the death line BeginStep takes nothing, so a step asked for there
            //would only be a log line
            int most = Math.Max(0, (int)MathF.Ceiling(
                (_ceilingDescent.QueuedTargetY - CEILING_DEATH_Y) / CeilingDescent.CEILING_DESCENT_PER_STEP));

            for (int steps = 0; steps < most; steps++)
                if (UndersideInReachAt(lowest, hangY - steps * CeilingDescent.CEILING_DESCENT_PER_STEP))
                    return steps;

            return most;
        }

        /// <summary>
        /// Where level 0 of the lattice will hang once every step already queued has come down: the cluster's load-time
        /// offset lowered by <see cref="CeilingDescent.QueuedTargetY"/>'s descent below rest.
        /// </summary>
        private float QueuedHangY => _clusterWorldOffset.Y - (_ceilingDescent.RestY - _ceilingDescent.QueuedTargetY);

        /// <summary>
        /// Whether every cell of a tall column's underside level, with level 0 of the lattice hung at
        /// <paramref name="hangY"/>, is a facing shot the gun can take under its clamp — <see cref="StepsIntoReach"/>'s one
        /// question, asked from the gun's rest stance and within the cluster's horizontal reach held at the load (#843).
        /// </summary>
        private bool UndersideInReachAt(byte lowest, float hangY) =>
            AimReachability.Check(_map, _cannon.RestRadius, RestTrunnionY, _cannon.ElevationLimit, lowest,
                new Vector3(_clusterWorldOffset.X, hangY, _clusterWorldOffset.Z), _tallAimHeldHorizontal).Pass;

        //Whether a brake's lift would still leave a tall column's underside in reach (#843), and whether that answer has
        //to be asked again. Read every frame by the HUD's chip and on every press; asked again only when something it
        //depends on moves (MarkBrakeReachStale's callers), so the frame pays for one reachability walk per event
        private bool _brakeKeepsReach = true;
        private bool _brakeReachStale = true;

        /// <summary>
        /// Whether the brake has a step to give back <b>and</b>, on a tall level, giving it back leaves the column's
        /// underside in reach (#843) — what the press asks and what the chip shows, one answer for both.
        /// <para>
        /// The second half exists because the feed stops the underside just inside the gun's reach. A pressure step
        /// lowers it past that edge, and the brake may give that back; but once the underside sits at the edge, a lift
        /// takes it out of reach, and the next landing's feed brings it straight back down — the glass rising blue and
        /// falling blue, and the charge spent on nothing. Refused instead, the charge is kept, as for any brake with
        /// nothing to give.
        /// </para>
        /// </summary>
        private bool BrakeCanLift => _ceilingDescent.CanBrake && BrakeKeepsReach();

        /// <summary>The cached half of <see cref="BrakeCanLift"/>; see there.</summary>
        private bool BrakeKeepsReach()
        {
            if (!_brakeReachStale) return _brakeKeepsReach;
            _brakeReachStale = false;

            if (!FieldIsTallerThanFrame || _map == null || _map.GetBallsCount() == 0) return _brakeKeepsReach = true;

            //The lift the brake would make (CeilingDescent.Brake: one step, never past the rest), carried onto where the
            //glass will hang once the steps still queued have come down, which the brake does not cancel
            float lift = MathF.Min(CeilingDescent.CEILING_DESCENT_PER_STEP, _ceilingDescent.RestY - _ceilingDescent.TargetY);

            return _brakeKeepsReach = UndersideInReachAt(_map.GetLowestOccupiedLevel(), QueuedHangY + lift);
        }

        /// <summary>
        /// Says <see cref="BrakeKeepsReach"/> has to be asked again: a landing (the underside moved), a step queued or
        /// begun, a brake, a lifted clamp, a load or a resize.
        /// </summary>
        private void MarkBrakeReachStale() => _brakeReachStale = true;

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
