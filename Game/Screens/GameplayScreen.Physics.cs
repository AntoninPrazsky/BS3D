using BepuPhysics;
using BS3D.Physics;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The simulation</b> — standing the Bepu world up, and the fixed-step frame it is advanced by.
    /// </summary>
    /// <remarks>
    /// Two orders here are load-bearing and neither may be "tidied": within one step it is
    /// <c>Timestep</c> → <c>_events.Flush()</c> → <c>ProcessQueuedContacts()</c>, per <b>step</b> and not per
    /// frame; and the accumulator is a fixed 1/120 s with a cap of four, which is a deliberate divergence from
    /// the Testbed's variable step and must not be unified with it. Split out of <c>GameplayScreen.cs</c>
    /// in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// Stands up the simulation and everything in it that does not move: the kinematic glass ceiling the
        /// cluster hangs from, and the island's floor — which is the drain's own surface, that being the only
        /// thing here a ball can rest on or run down.
        /// </summary>
        private void BuildPhysicsWorld()
        {
            //One world per level, torn down with the session (see TearDown for why the whole simulation goes
            //rather than being emptied). Everything this used to spell out is PhysicsWorld's: the dispatcher
            //before the contact stream that sizes its per-worker queues off it, the single ContactEvents
            //initialisation the simulation's own construction performs (#73 — initialising it a second time
            //hooks the freshness pass onto the timestepper twice), the solver description tuned together with
            //the contact material and the BallSocket spring, and the shot template whose bare shape index is
            //what gives a ball leaving at SHOOT_SPEED continuous collision detection.
            _world = new PhysicsWorld();

            BuildCeilingBody();

            //The island's whole floor, and it is the drain's own surface: the sloped cone plus the dished
            //stone ring from its rim out to the edge of the platform's walkable top. A ball that lands on the
            //ring rolls down its dish into the glass, runs down the cone and drops through the hole; past the
            //ring it falls off the island's edge into the scene, and either way the kill plane takes it.
            //FunnelPhysics' since #75 (the Testbed had the same eight triangles a segment), and it is handed
            //the very figures ArenaIsland draws from — the dish's depth included — so the collided surface and
            //the drawn one cannot drift apart. The ring stops short of the island's own radius —
            //ArenaIsland.FLOOR_RADIUS is IslandMesh.FloorRadius of it — because the coping falls away over the
            //last stretch, and a floor carried to the widest point would hold a ball up on air over the wash.
            FunnelPhysics.Build(_world.Simulation, _world.BufferPool, ArenaIsland.TOP_Y, ArenaIsland.FUNNEL_BOTTOM_Y,
                ArenaIsland.FUNNEL_TOP_RADIUS, ArenaIsland.FUNNEL_HOLE_RADIUS, ArenaIsland.FLOOR_RADIUS,
                ArenaIsland.DISH_DEPTH, ArenaIsland.FUNNEL_SEGMENTS);
        }

        #region The simulation's frame

        /// <summary>
        /// Advances the simulation, spending whatever the frame took out of an accumulator in whole steps of
        /// <see cref="PHYSICS_TIMESTEP"/>. Each step's contacts are flushed and handled before the next one is
        /// taken: a contact queued during a step describes a world the following step has already moved on from.
        /// </summary>
        private void StepPhysics(float elapsed)
        {
            _physicsAccumulator += elapsed;

            int steps = 0;

            while (_physicsAccumulator >= PHYSICS_TIMESTEP && steps < PHYSICS_MAX_STEPS_PER_FRAME)
            {
                //Whole fixed steps out of the accumulator, which is this game's own policy and deliberately
                //not the Testbed's one variable step per rendered frame — see PHYSICS_TIMESTEP. The mandatory
                //Timestep → Flush → contacts order INSIDE each step is PhysicsWorld.Step's, which is why the
                //work that belongs there is handed over rather than written after the call.
                _world.Step(PHYSICS_TIMESTEP, _processContacts);

                _physicsAccumulator -= PHYSICS_TIMESTEP;
                steps++;
            }

            //A frame that hitched must not try to catch the whole gap up — it would take even longer doing so
            //and fall further behind on the next frame, and the one after that. Drop what is left and let the
            //world run slow for that single frame instead.
            if (steps == PHYSICS_MAX_STEPS_PER_FRAME) _physicsAccumulator = 0f;

            RemoveFallenBalls(_shotBalls, scoreMisses: true);
            RemoveFallenBalls(_fallingBalls, scoreMisses: false);
        }

        /// <summary>
        /// Drops the balls that have left the game: those below <see cref="KILL_PLANE_Y"/>, having gone down
        /// the drain or over the island's edge.
        /// <para>
        /// <b>Falling below the map is the only thing that removes a ball</b> — deliberately, and this is where
        /// the game parts company with the Testbed. The Testbed also culls any ball whose body has gone to
        /// sleep, i.e. come to rest anywhere, which is cheap and keeps the scene tidy but means a ball that
        /// settles on the island's stone winks out in front of the player. A ball vanishing while it is plainly
        /// in shot reads as a bug, whatever it saves. So a ball that does come to rest stays there and stays
        /// visible — a rare case now rather than the routine one: the stone ring is a dish
        /// (<see cref="ArenaIsland.DISH_DEPTH"/>), so a ball that lands on it rolls into the drain and leaves
        /// through the hole instead of accumulating over the session, and anything inside the funnel's rim
        /// runs down its ~55° wall the way it always did. What can still rest is a pile propping itself up
        /// near the mouth; those fall asleep, and a sleeping body leaves Bepu's active set, so they are drawn
        /// but barely simulated.
        /// </para>
        /// <para>
        /// Only ever handed <see cref="_shotBalls"/> and <see cref="_fallingBalls"/>. Handing it the structure
        /// array would delete the cluster.
        /// </para>
        /// </summary>
        /// <param name="scoreMisses">
        /// True for shot balls, which may still be undecided when they go over the edge. A released ball was
        /// unregistered when it attached, so it can never be still listening and has no miss to score.
        /// </param>
        private void RemoveFallenBalls(List<PhysicsBall> balls, bool scoreMisses)
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BodyReference body = balls[i].BallReference;

                //No sleep cull here, deliberately — see the remarks above
                if (body.Pose.Position.Y >= KILL_PLANE_Y) continue;

                //Unregisters the listener if there still is one and then removes the body, in that one order
                //that is safe (PhysicsWorld.RetireBall owns it), and answers whether the ball was STILL
                //LISTENING — which is exactly "this shot resolved as nothing": it missed the island as well as
                //the cluster and fell straight past everything into the city. The far rarer of the two misses —
                //a shot that strikes the stone is spent the moment it does (see the handler) — but it is what
                //makes "every shot resolves exactly once" true rather than nearly true. Not folded into the
                //condition below: && would short-circuit the retire away for a falling ball.
                bool wasUndecided = _world.RetireBall(body);

                if (wasUndecided && scoreMisses) _score.Missed();

                balls.RemoveAt(i);
            }
        }

        #endregion
    }
}
