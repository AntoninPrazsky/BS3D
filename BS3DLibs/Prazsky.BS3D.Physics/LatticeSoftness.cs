using BepuPhysics;
using BepuPhysics.Constraints;
using Prazsky.BS3D.GameStructure;
using System.Collections.Generic;

namespace Prazsky.BS3D.Physics
{
    /// <summary>
    /// <b>The lattice's own spring, chosen per structure</b> (#690, step 1: a prototype). Every socket between two balls is
    /// built at <see cref="BallsConstraintsBuilder.SPRING_SETTINGS"/> (15 Hz, critically damped), which makes a dense
    /// lattice one rigid body. A chapter of levels that sag, swing and bend like rope or cloth needs that spring softer,
    /// and per level: #617 measured that one global softening cannot serve the pack, because a strand is a chain of
    /// springs in series and sags many times as far as a block.
    /// <para>
    /// <b>Only the sockets between balls.</b> The sockets to the glass stay the builder's own: they are the anchors, and
    /// the start-of-level swing (<see cref="ClusterStartSwing"/>) eases them back to the builder's spring, which a softer
    /// anchor would fight. They are told apart by the handle slots' own convention, the one
    /// <see cref="ClusterStartSwing"/> reads: a top-level ball's <see cref="PhysicsBall.HandlesTop"/> holds its socket to
    /// the glass and nothing else, every other slot holds sockets to balls.
    /// </para>
    /// <para>
    /// Applied after the fact, by re-describing each socket in place (anchors untouched), rather than threaded through
    /// every connect call: the build pass, the runtime attach and their helpers all make sockets, and a parameter missed
    /// in one of them would leave stiff sockets in a soft structure without a word. Re-describing what was built is one
    /// walk over the handles every structure already keeps.
    /// </para>
    /// </summary>
    public static class LatticeSoftness
    {
        //Scratch for the walks below, per thread like the builder's own (#585): ApplyToBall runs on every landing of a soft
        //lattice, on the contact path, and BestPractices.md keeps that path free of per-landing allocations
        [System.ThreadStatic] private static List<ConstraintHandle> t_handles;
        [System.ThreadStatic] private static HashSet<int> t_done;
        private static List<ConstraintHandle> Handles => t_handles ??= new List<ConstraintHandle>(64);
        private static HashSet<int> Done => t_done ??= new HashSet<int>();
        /// <summary>Re-describes every socket between two balls of <paramref name="balls"/> with <paramref name="spring"/>.</summary>
        /// <returns>How many sockets were re-described (a same-level pair is visited from both ends, and counted once).</returns>
        public static int Apply(PhysicsBall[,,] balls, Simulation simulation, SpringSettings spring)
        {
            int topLevel = balls.GetLength(2) - 1;
            List<ConstraintHandle> handles = Handles;
            handles.Clear();

            for (int x = 0; x < balls.GetLength(0); x++)
                for (int z = 0; z < balls.GetLength(1); z++)
                    for (int l = 0; l <= topLevel; l++)
                    {
                        PhysicsBall ball = balls[x, z, l];
                        if (ball == null) continue;

                        //Each cross-level pair is in the lower ball's top slots and the upper ball's bottom ones, so the
                        //bottom slots alone visit it once; a same-level pair is in both balls' middle slots
                        ball.HandlesBottom.CollectStored(handles);
                        ball.HandlesMiddle.CollectStored(handles);
                    }

            return Describe(simulation, handles, spring);
        }

        /// <summary>
        /// The same for one freshly attached ball (<see cref="BallsConstraintsBuilder.AttachBallToStructure"/>): its
        /// sockets to its neighbours, never its socket to the glass.
        /// </summary>
        public static void ApplyToBall(PhysicsBall ball, int topLevel, Simulation simulation, SpringSettings spring)
        {
            List<ConstraintHandle> handles = Handles;
            handles.Clear();

            ball.HandlesBottom.CollectStored(handles);
            ball.HandlesMiddle.CollectStored(handles);
            if (ball.ArrayPosition.Level != topLevel) ball.HandlesTop.CollectStored(handles);

            Describe(simulation, handles, spring);
        }

        private static int Describe(Simulation simulation, List<ConstraintHandle> handles, SpringSettings spring)
        {
            Solver solver = simulation.Solver;
            HashSet<int> done = Done;
            done.Clear();

            foreach (ConstraintHandle handle in handles)
            {
                if (!done.Add(handle.Value)) continue;
                if (!solver.ConstraintExists(handle)) continue;
                if (solver.HandleToConstraint[handle.Value].TypeId != BallSocket.ConstraintTypeId) continue;

                solver.GetDescription(handle, out BallSocket socket);
                socket.SpringSettings = spring;
                solver.ApplyDescription(handle, socket);
            }

            return done.Count;
        }
    }
}
