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
    /// <b>The ceiling and its pressure</b> — the kinematic body the whole cluster hangs from, and the state
    /// machine that walks it down: a step is queued, held, gated behind any cinematic already running, slid,
    /// flashed, and rippled through the cluster.
    /// </summary>
    /// <remarks>
    /// It is deliberately one sequence in one place rather than a flag per stage — the stages exist because a
    /// step must not land in the middle of a spectacle the player earned, and that is only readable end to
    /// end. The wake pass is part of it: a body Bepu has put to sleep does not answer a kinematic move, so a
    /// descent that arrives while the cluster is asleep has to wake it first (#78). Split out of
    /// <c>GameplayScreen.cs</c> in #72, and the first candidate for a real extraction.
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
                new System.Numerics.Vector3(0f, _ceilingY, 0f),
                new CollidableDescription(shape, 0.1f),
                new BodyActivityDescription(PhysicsWorld.SLEEP_THRESHOLD)));

            _ceiling = new KinematicBody(new BodyReference(handle, _world.Simulation.Bodies), handle);
        }

        /// <summary>
        /// Begins one step of the ceiling's descent: lowers the target by <see cref="CEILING_DESCENT_PER_STEP"/>,
        /// clamped at the death line so an overlong level cannot drive the glass through the gun. The body itself
        /// does not move here — <see cref="UpdateCeilingDescent"/> slides it to the target, which is what keeps a
        /// hundred constrained bodies from being jerked in a single write.
        /// </summary>
        /// <param name="waited">
        /// Seconds the step spent queued before it was let go — on the line because it is the one figure that
        /// says whether the deferral did anything, and a step that waited seconds is one that sat out a drop
        /// cinematic (see <see cref="ReleaseCeilingStep"/>).
        /// </param>
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

        private void StartCeilingDescent(float waited)
        {
            //No target to reach if the glass is already as low as it can go — further steps would be a no-op and
            //a needless log, and clamping here is what stops an inconsistent level (more steps than the geometry
            //allows) from scraping the body past the death line.
            if (_ceilingTargetY <= CEILING_DEATH_Y) return;

            _ceilingTargetY = MathF.Max(CEILING_DEATH_Y, _ceilingTargetY - CEILING_DESCENT_PER_STEP);
            _ceilingDescending = true;

            //The plate is about to be moved by writing its pose, and both it and the cluster are very likely
            //asleep — measured asleep, in fact, on a step that came due over a settled cluster (#78)
            WakeForDescent();

            //The descent itself is a slow slide of a translucent plate against a sky, which is very nearly
            //invisible while the player is watching the cluster — the pressure the whole rule exists to apply
            //was arriving unnoticed. So the glass says it: it lights up, and drives a wave down through every
            //ball hanging on it.
            //
            //In WHICH colour is the difference between a threat and a reward. A step the shot count forced is
            //the pressure and burns red. A step the FEED asked for is a tall level handing over more of its
            //column because the player just cleared a great deal of it — nothing has gone wrong, and a red
            //flash there tells them off for playing well. Feed steps are spent first, so a landing that
            //queues both kinds says the good news first.
            bool feeding = _ceilingFeedStepsQueued > 0;
            if (feeding) _ceilingFeedStepsQueued--;

            _ceilingFlashIsFeed = feeding;
            _ceilingFlashColor = feeding ? CEILING_FEED_COLOR : CEILING_FLASH_COLOR;
            Game.Balls.RippleAlarmColor = feeding ? RIPPLE_FEED_COLOR : RIPPLE_ALARM_COLOR;

            _ceilingFlash = 1f;
            StartCeilingRipple();

            Console.WriteLine($"[ceiling] Step to {_ceilingTargetY:F2} (death line {CEILING_DEATH_Y:F2})"
                + $", {(feeding ? "feeding" : "pressure")}"
                + $", shots fired {_score.ShotsFired}, waited {waited:F2} s");
        }

        /// <summary>
        /// Lets a queued ceiling step go, once it will not be read as a punishment for the shot that earned it.
        /// <para>
        /// The step comes due on the <b>frame the shot is fired</b>, but the shot leaves at 200 u/s and lands
        /// about a tenth of a second later — so the glass flashing red and driving its alarm wave down the
        /// cluster landed on top of the drop cinematic, and a player who had just cut a large group loose was
        /// shown the game's one punishment animation while watching their reward. It read as having done
        /// something wrong. Nothing was wrong; only the order was.
        /// </para>
        /// <para>
        /// So a step waits for two things: a short hold, long enough for the shot to land and a cinematic to
        /// engage if one is going to, and then for that cinematic to be over. It is a <b>count</b> rather than
        /// a flag because a level with <c>ceilingStep</c> of 1 steps on every shot, and two shots inside the
        /// hold must not lose one of them; and the hold is re-armed per release rather than shared, so queued
        /// steps come down one at a time instead of as a single double-height lurch.
        /// </para>
        /// </summary>
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
        /// The steps are <b>queued</b> rather than taken, so a cascade that owes ten of them pours down one
        /// at a time through <see cref="ReleaseCeilingStep"/>'s hold rather than arriving as one lurch — and
        /// so a feed landing on a drop cinematic waits it out like any other step.
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
            if (lowest <= _feedFloorLevel) return;

            //How far the underside has climbed out of reach, in world units, and how many whole descents
            //cover it. Whole ones only: a part-step owed now is owed again next landing, and rounding up
            //would walk the glass down a little further than the level was ever cleared.
            float risen = (lowest - _feedFloorLevel) / Constants.SQRT_TWO;
            int owed = (int)(risen / CEILING_DESCENT_PER_STEP) - _feedStepsQueued;

            if (owed <= 0) return;

            _feedStepsQueued += owed;
            _ceilingStepsPending += owed;
            _ceilingStepHold = CEILING_STEP_HOLD;

            //And these ones do not read as an alarm. A descent the ceiling forces on the player is a threat
            //and burns red; this one is the game handing over more of the column BECAUSE they cleared a lot
            //of it, so the glass and the wave go cold blue instead. Set here rather than at the descent,
            //because by the time a queued step comes down the reason it was queued is gone.
            _ceilingFeedStepsQueued += owed;

            //A rare-event line like the rest of the [ceiling] family: it fires when a band goes, not per
            //frame, and it is the one figure that says whether the feed is keeping up with the player.
            Console.WriteLine($"[ceiling] feeding {owed} step(s): the underside has climbed to level {lowest}, "
                + $"{risen:F1} above where the level hung it");
        }

        private void ReleaseCeilingStep(float elapsed)
        {
            if (_ceilingStepsPending <= 0) return;

            _ceilingStepWaited += elapsed;

            if (_ceilingStepHold > 0f) _ceilingStepHold -= elapsed;
            if (_ceilingStepHold > 0f || _cinematic.Engaged) return;

            _ceilingStepsPending--;
            _ceilingStepHold = CEILING_STEP_HOLD;

            StartCeilingDescent(_ceilingStepWaited);
            _ceilingStepWaited = 0f;
        }

        /// <summary>
        /// Slides the ceiling body toward <see cref="_ceilingTargetY"/> at <see cref="CEILING_DESCENT_SPEED"/>,
        /// one frame's worth at a time, and refreshes the drawn world matrix to match. Called before the physics
        /// step so the solver works against the moved body this frame, letting the contact between a descending
        /// cluster and anything below it resolve rather than interpenetrate.
        /// </summary>
        private void UpdateCeilingDescent(float elapsed)
        {
            //Ahead of the early return: the glow outlives the slide, and it has to keep fading once the plate
            //has arrived or the glass would stay red for the rest of the level
            if (_ceilingFlash > 0f) _ceilingFlash = MathF.Max(0f, _ceilingFlash - elapsed / CEILING_FLASH_SECONDS);

            if (!_ceilingDescending) return;

            //Equal within a hair means the slide is done — a frame that would otherwise move a thousandth of a
            //unit and never quite arrive. Snap, stop, and the matrix reflects the final pose exactly.
            if (MathF.Abs(_ceilingY - _ceilingTargetY) <= CEILING_DESCENT_SPEED * elapsed)
            {
                _ceilingY = _ceilingTargetY;
                _ceilingDescending = false;
            }
            else
            {
                _ceilingY -= CEILING_DESCENT_SPEED * elapsed;
            }

            _ceiling.BodyReference.Pose.Position = new System.Numerics.Vector3(0f, _ceilingY, 0f);
            _ceiling.RefreshWorld();
        }
    }
}
