using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #878: the drop cinematic's lens flies one continuous curve past the island's rim and under it. The owner saw the
    /// camera descend beside the rim and hop away from the edge; in the trace the lens was pushed 9 units sideways in two
    /// frames the instant the subject crossed the island's top (a switch between two ways of keeping it clear), then
    /// dived 90 units a second into a stop. The real <c>DropCinematic</c> is flown (<see cref="DropCinematicFlight"/>)
    /// against a subject falling the way a released group falls, over a few dozen rolls, and the lens's acceleration -
    /// the second difference of its position - is held to a bound along the whole flight. A check is evidence only once its
    /// failing branch has been seen to fire (BestPractices section 10): the bound is exercised below on a path with the
    /// hop in it, and was run against the cinematic as it stood before this change (see the issue's trace: 4000 units/s^2
    /// at the median and 18000 at the worst roll, against under 400 now).
    /// </summary>
    public class DropCinematicPathTests
    {
        private const float DT = 1f / 60f;

        /// <summary>Open scenes: the island floats in them, so the shot goes round the rim and under the island.</summary>
        private static readonly SceneKind[] OPEN_SCENES =
        {
            SceneKind.Space, SceneKind.Sea, SceneKind.Dream, SceneKind.City, SceneKind.NeonCity, SceneKind.Cavern, SceneKind.Storm,
        };

        /// <summary>
        /// The most the lens may accelerate, in units a second squared. The smooth path peaks near 400 at the worst of sixty
        /// rolls (the dive's own bend, ending about 80 units a second); the hop was over 4000 at the median.
        /// </summary>
        private const float MAX_ACCELERATION = 800f;

        [Theory]
        [InlineData(60f)]
        [InlineData(144f)]
        public void The_lens_of_an_open_scene_never_steps_on_its_way_under_the_island(float hertz)
        {
            float dt = 1f / hertz;

            foreach (SceneKind scene in OPEN_SCENES)
                foreach (int balls in new[] { 12, 64 })
                    for (int seed = 1; seed <= 40; seed++)
                    {
                        List<DropCinematicFlight.Frame> frames = DropCinematicFlight.Fly(scene, seed, balls, dt);
                        Assert.True(frames.Count > 2 * hertz, $"{scene} {balls} balls seed {seed}: the shot ended after {frames.Count} frames");

                        (float acceleration, float time) = MaxAcceleration(frames, dt);
                        Assert.True(acceleration < MAX_ACCELERATION,
                            $"{scene} {balls} balls seed {seed} at {hertz} Hz: the lens accelerates {acceleration:F0} units/s^2 at t={time:F2} s");
                    }
        }

        [Fact]
        public void A_lingering_group_does_not_hold_the_lens_beside_the_drum_for_long()
        {
            //The real group sinks slowly through the mouth, and a dive measured in the subject's descent alone would hold the
            //lens at the drum's blank wall for as long as it lingered: the lens is under the underside a second and a half after
            //the subject passed the top face, and the path is as smooth as the fast one's
            const float underside = ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT - 1.5f;

            foreach (SceneKind scene in OPEN_SCENES)
                for (int seed = 1; seed <= 20; seed++)
                {
                    List<DropCinematicFlight.Frame> frames = DropCinematicFlight.Fly(scene, seed, 64, DT, 8f, DropCinematicFlight.LingeringSubject);

                    float crossed = frames.Find(f => f.Subject.Y < ArenaIsland.TOP_Y).Time;
                    DropCinematicFlight.Frame later = frames.Find(f => f.Time >= crossed + 1.5f);

                    Assert.True(later.Lens.Y < underside, $"{scene} seed {seed}: the lens is at y {later.Lens.Y:F1} 1.5 s after the subject went under the face");

                    (float acceleration, float time) = MaxAcceleration(frames, DT);
                    Assert.True(acceleration < MAX_ACCELERATION, $"{scene} seed {seed}: the lens accelerates {acceleration:F0} units/s^2 at t={time:F2} s");
                }
        }

        [Fact]
        public void The_lens_of_an_open_scene_stays_clear_of_the_islands_rim()
        {
            foreach (SceneKind scene in OPEN_SCENES)
                foreach (int balls in new[] { 12, 64 })
                    for (int seed = 1; seed <= 40; seed++)
                        foreach (DropCinematicFlight.Frame frame in DropCinematicFlight.Fly(scene, seed, balls))
                        {
                            float reach = new Vector2(frame.Lens.X, frame.Lens.Z).Length();
                            bool inDrum = frame.Lens.Y < ArenaIsland.TOP_Y && frame.Lens.Y > ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT;

                            Assert.False(inDrum && reach < ArenaIsland.RADIUS,
                                $"{scene} {balls} balls seed {seed} t={frame.Time:F2}: the lens is inside the drum at reach {reach:F1}");

                            //Not only outside the stone: past the clearance the cinematic holds, with no push to find it
                            Assert.True(reach > ArenaIsland.RADIUS + 1.5f || frame.Lens.Y > ArenaIsland.TOP_Y + 1.5f
                                    || frame.Lens.Y < ArenaIsland.TOP_Y - ArenaIsland.EDGE_HEIGHT - 1.5f,
                                $"{scene} {balls} balls seed {seed} t={frame.Time:F2}: the lens at reach {reach:F1}, y {frame.Lens.Y:F1} is inside the rim's clearance");
                        }
        }

        [Fact]
        public void A_solid_terrain_scene_flies_the_path_it_always_did()
        {
            //Sampled off the cinematic before #878 (Volcano, 64 balls, seed 1, sixty frames a second): the closed scenes' shot
            //climbs and looks down the throat, has no rim to go round and was left alone
            (int frame, Vector3 lens)[] samples =
            {
                (100, new Vector3(-6.272f, 4.865f, 27.310f)),
                (200, new Vector3(0.352f, -4.549f, 13.028f)),
                (300, new Vector3(4.364f, -3.570f, 12.567f)),
                (388, new Vector3(7.579f, -3.142f, 10.948f)),
            };

            List<DropCinematicFlight.Frame> frames = DropCinematicFlight.Fly(SceneKind.Volcano, 1, 64);

            foreach ((int frame, Vector3 lens) in samples)
                Assert.True(Vector3.Distance(frames[frame].Lens, lens) < 0.01f,
                    $"frame {frame}: the lens is at {frames[frame].Lens}, it was at {lens}");
        }

        [Fact]
        public void The_bound_fires_on_a_hop()
        {
            //The path as the owner saw it: a lens gliding down beside the rim that is shoved nine units out in two frames
            List<DropCinematicFlight.Frame> hop = new();
            for (int i = 0; i < 120; i++)
            {
                float t = i / 60f;
                float push = i < 60 ? 0f : i < 62 ? (i - 59) * 4.5f : 9f;
                hop.Add(new DropCinematicFlight.Frame(t, new Vector3(20f + push, 5f - 10f * t, 0f), Vector3.Zero, Vector3.Zero, 1f));
            }

            List<DropCinematicFlight.Frame> glide = new();
            for (int i = 0; i < 120; i++)
            {
                float t = i / 60f;
                glide.Add(new DropCinematicFlight.Frame(t, new Vector3(20f + 4.5f * t * t, 5f - 10f * t, 0f), Vector3.Zero, Vector3.Zero, 1f));
            }

            Assert.True(MaxAcceleration(hop, 1f / 60f).acceleration > MAX_ACCELERATION);
            Assert.True(MaxAcceleration(glide, 1f / 60f).acceleration < MAX_ACCELERATION);
        }

        /// <summary>The largest second difference of the lens, over the frames the cinematic fully owns, and when.</summary>
        private static (float acceleration, float time) MaxAcceleration(List<DropCinematicFlight.Frame> frames, float dt)
        {
            float worst = 0f, at = 0f;

            for (int i = 2; i < frames.Count; i++)
            {
                //The first frames are the blend's own and the pose is not the camera's yet
                if (frames[i].Blend < 0.99f) continue;

                float acceleration = ((frames[i].Lens - 2f * frames[i - 1].Lens + frames[i - 2].Lens) / (dt * dt)).Length();
                if (acceleration > worst) (worst, at) = (acceleration, frames[i].Time);
            }

            return (worst, at);
        }
    }
}
