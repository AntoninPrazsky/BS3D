using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// Stands in for the gameplay screen's one constant the drop cinematic reads (its field of view, which the path never
    /// uses): <c>DropCinematic</c> is compiled into the tests, and the screen it names needs a device.
    /// </summary>
    internal static class GameplayScreen
    {
        public static readonly float GAME_FOV = MathF.PI / 4.2f;
    }
}

namespace BS3D.Tests
{
    /// <summary>
    /// One drop cinematic flown against a subject falling the way a released group falls (measured off a scripted drop: free
    /// fall from the release at about 8.6 units/s^2, into the drain's throat, slowing in the funnel), stepped at a fixed rate,
    /// so a test can hold the path itself - the lens position the cinematic hands the camera - to a bound over every roll.
    /// </summary>
    internal static class DropCinematicFlight
    {
        public const float SEA_LEVEL_Y = -13f;
        public const float RELEASE_Y = -0.7f;
        public const float KILL_Y = -40f;

        /// <summary>One frame of a flight: the lens, what it looks at and the subject it follows.</summary>
        internal readonly record struct Frame(float Time, Vector3 Lens, Vector3 Target, Vector3 Subject, float Blend);

        /// <summary>Where the group's centre is at <paramref name="t"/> seconds.</summary>
        public static Vector3 Subject(float t)
        {
            const float g = 8.6f;
            float y = RELEASE_Y - 0.5f * g * t * t;

            //The throat takes the speed off it: past the funnel's foot it creeps down, as the scripted drops did
            const float footY = -29f;
            if (y < footY)
            {
                float tFoot = MathF.Sqrt(2f * (footY - RELEASE_Y) / -g);
                y = footY - 1.3f * (t - tFoot);
            }

            float x = MathHelper.Lerp(2.8f, -0.3f, Math.Min(1f, t / 3f));
            float z = MathHelper.Lerp(0.4f, 0f, Math.Min(1f, t / 3f));

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// A group that falls as far as the island's top face like any other and then lingers in the drain's mouth, sinking at
        /// 3.5 units a second, a third of what a falling ball does (the real ones sink about that fast there).
        /// </summary>
        public static Vector3 LingeringSubject(float t)
        {
            Vector3 fall = Subject(t);
            const float faceY = -8.5f;
            if (fall.Y > faceY) return fall;

            float tFace = MathF.Sqrt(2f * (faceY - RELEASE_Y) / -8.6f);

            return new Vector3(fall.X, faceY - 3.5f * (t - tFace), fall.Z);
        }

        /// <summary>
        /// Flies a shot to its end (or <paramref name="seconds"/>, whichever is first) at <paramref name="frameSeconds"/> a
        /// frame, the player watching from in front of the field, rolled by <paramref name="seed"/>.
        /// </summary>
        public static List<Frame> Fly(SceneKind scene, int seed, int balls, float frameSeconds = 1f / 60f, float seconds = 6.5f,
            Func<float, Vector3> subjectAt = null)
        {
            subjectAt ??= Subject;

            DropCinematic cinematic = new();
            Vector3 start = subjectAt(0f);
            Vector3 playerEye = new(0f, 6f, 38f);

            cinematic.Begin(scene, start, playerEye, balls, new Random(seed), SEA_LEVEL_Y);

            List<Frame> frames = new();

            for (float t = 0f; t <= seconds; t += frameSeconds)
            {
                Vector3 subject = subjectAt(t);
                bool alive = subject.Y > KILL_Y;
                float funnelShare = MathHelper.Clamp((-8.5f - subject.Y) / 3f, 0f, 1f);

                cinematic.Update(frameSeconds, alive, subject, funnelShare);
                if (!cinematic.Running) break;

                frames.Add(new Frame(t, cinematic.Position, cinematic.Target, subject, cinematic.Blend));
            }

            return frames;
        }
    }
}
