using BepuPhysics;
using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The impossible shot</b> (#230): the session's half of <see cref="Wormhole"/> — which shots count, and what
    /// happens to them. Three shots in a row from the same pose that all fly clean out past the island
    /// (<see cref="RepeatedMiss"/>) open a hole in front of the last one, and every shot still in the air out there
    /// is swallowed. Nothing about play changes: a swallowed shot is a miss retired through the door every other miss
    /// goes through, a little earlier than the kill plane would have retired it.
    /// </summary>
    internal sealed partial class GameplayScreen
    {
        /// <summary>
        /// How far from the island's axis a shot has to be, flying outward, to have escaped: past the island's own
        /// radius with a margin, where the island's floor is the only thing a ball could meet and it is behind it.
        /// </summary>
        private const float ESCAPE_RADIUS = ArenaIsland.RADIUS + 3f;

        /// <summary>
        /// How far from the hole a shot of the run may be and still be pulled back into it, in world units. A shot
        /// covers 200 units a second, so this is the earlier shots of a quick run, still on their way out of the scene.
        /// </summary>
        private const float SWALLOW_REACH = 250f;

        /// <summary>Where each shot still being watched was fired from and along, keyed by its body's handle.</summary>
        private readonly Dictionary<int, (Vector3 Muzzle, Vector3 Direction)> _firedPoses = new(16);

        private readonly RepeatedMiss _repeatMiss = new();

        /// <summary>
        /// Watches the shots in the air for one leaving the arena undecided — an escape, reported with the pose it
        /// was fired from — and for one resolving any other way, which breaks the run. Called once a frame after the
        /// step. A landing leaves <see cref="_shotBalls"/> before this sees it, so <see cref="OnBallLanded"/> breaks
        /// the run itself.
        /// </summary>
        private void WatchForEscapes()
        {
            if (LevelOver) return;

            for (int i = _shotBalls.Count - 1; i >= 0; i--)
            {
                BodyReference body = _shotBalls[i].BallReference;
                int key = body.Handle.Value;
                if (!_firedPoses.TryGetValue(key, out var fired)) continue;

                //It struck the stone: spent, and not out past the island - the run is over
                if (!_world.Events.IsListener(body.CollidableReference))
                {
                    _firedPoses.Remove(key);
                    _repeatMiss.Break();
                    continue;
                }

                System.Numerics.Vector3 at = body.Pose.Position;
                System.Numerics.Vector3 velocity = body.Velocity.Linear;
                if (at.X * at.X + at.Z * at.Z < ESCAPE_RADIUS * ESCAPE_RADIUS) continue;
                if (at.X * velocity.X + at.Z * velocity.Z <= 0f) continue;

                _firedPoses.Remove(key);
                if (!_repeatMiss.Escaped(fired.Muzzle, fired.Direction) || _wormhole.Active) continue;

                OpenWormhole(at.ToXna(), velocity.ToXna());
                return;
            }
        }

        /// <summary>
        /// Opens the hole <see cref="Wormhole.AHEAD"/> in front of the shot that completed the run and swallows every
        /// shot out past the arena within <see cref="SWALLOW_REACH"/> of it: retired here and now, each through
        /// <see cref="OnShotSpent"/> if it was still undecided — exactly the kill plane's door — and handed to the
        /// effect as a drawing.
        /// </summary>
        private void OpenWormhole(Vector3 at, Vector3 velocity)
        {
            Matrix view = Camera.View;
            Vector3 centre = KeepInView(at + Vector3.Normalize(velocity) * Wormhole.AHEAD, view, Camera.Projection);
            _wormhole.Open(centre, new Vector3(view.M11, view.M21, view.M31), new Vector3(view.M12, view.M22, view.M32),
                Game.Audio);

            Console.WriteLine($"[wormhole] opened at ({centre.X:0.0}, {centre.Y:0.0}, {centre.Z:0.0})");

            for (int i = _shotBalls.Count - 1; i >= 0; i--)
            {
                PhysicsBall ball = _shotBalls[i];
                BodyReference body = ball.BallReference;

                Vector3 position = body.Pose.Position.ToXna();
                if (position.X * position.X + position.Z * position.Z < ESCAPE_RADIUS * ESCAPE_RADIUS) continue;
                if (Vector3.DistanceSquared(position, centre) > SWALLOW_REACH * SWALLOW_REACH) continue;

                _wormhole.Swallow(position, body.Velocity.Linear.ToXna(), ball.Type, ball.Kind);

                _firedPoses.Remove(body.Handle.Value);
                bool wasUndecided = _world.RetireBall(body);
                _shotBalls.RemoveAt(i);

                //After the list is right again: OnShotSpent can end the level, and everything it reads should see the
                //ball gone. Never once the level is over, OnShotSpent's own rule.
                if (wasUndecided && !LevelOver) OnShotSpent();
            }
        }

        //How much of the frame the hole's disc is kept inside, as a fraction of the half-width and half-height: inside
        //the HUD's side columns (the cluster profile on the left, the chips and the score on the right), all of the height
        private const float VIEW_REACH_X = 0.72f;
        private const float VIEW_REACH_Y = 0.9f;

        /// <summary>
        /// Moves a hole that would open outside the frame back into it, at the same distance from the lens. The play
        /// camera does not turn with the barrel, so a miss at the edge of the traverse leaves the frame long before it
        /// leaves the island — at 16:9, a shot at 45° escapes some 35° off the axis, with the frame's edge at 36° —
        /// and a hole opened where it escaped was a sliver at the screen's edge. Brought in, the hole stands at the
        /// frame's side and the shot is visibly hauled back into it, which is the better joke anyway. Its whole disc is
        /// kept inside the HUD's side columns as well (<see cref="VIEW_REACH_X"/>): the first capture that brought it in
        /// to the very edge opened it under the cluster profile's panel. A hole already that far in is not moved at all.
        /// </summary>
        private static Vector3 KeepInView(Vector3 centre, Matrix view, Matrix projection)
        {
            Vector3 local = Vector3.Transform(centre, view);
            float depth = -local.Z;

            //Behind the lens, which no shot that left the barrel forwards can be - left where it is rather than guessed at
            if (depth <= Wormhole.RADIUS) return centre;

            //How far off the axis the frame reaches at this depth, x and y, less the hole's own radius and a margin
            float reachX = depth * VIEW_REACH_X / projection.M11 - Wormhole.RADIUS * 1.15f;
            float reachY = depth * VIEW_REACH_Y / projection.M22 - Wormhole.RADIUS * 1.15f;
            if (reachX <= 0f || reachY <= 0f) return centre;

            if (MathF.Abs(local.X) <= reachX && MathF.Abs(local.Y) <= reachY) return centre;

            local.X = Math.Clamp(local.X, -reachX, reachX);
            local.Y = Math.Clamp(local.Y, -reachY, reachY);
            return Vector3.Transform(local, Matrix.Invert(view));
        }

        /// <summary>A fresh level: nothing watched, no run under way, no hole open.</summary>
        private void ResetWormhole()
        {
            _firedPoses.Clear();
            _repeatMiss.Reset();
            _wormhole.Reset();
        }
    }
}
