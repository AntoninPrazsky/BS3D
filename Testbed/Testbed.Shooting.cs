using BepuPhysics;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace Testbed
{
    /// <summary>
    /// <b>A ball's whole life</b> — loaded in the magazine, fired, and removed again once it can no longer
    /// affect anything — plus the teardown of a whole structure when a map is swapped.
    /// </summary>
    /// <remarks>
    /// Split out of <c>Testbed.cs</c> in #73. What lands where is deliberately <i>not</i> here: a contact is
    /// <c>Physics/BallContactEventHandler</c>'s, and the cull rule is this executable's own — it retires a
    /// sleeping ball as well as one past the kill plane, where the Game culls on the plane alone (a ball
    /// winking out on the stone in front of a player reads as a bug whatever it saves). Removal order is the
    /// one thing here that cannot be reordered: constraints before bodies, and unregister before remove
    /// (<see cref="PhysicsWorld.RetireBall"/>), a listener being keyed on a collidable reference Bepu will hand
    /// to the next body added.
    /// </remarks>
    public partial class Testbed
    {

        private void InitializeShooting()
        {
            //No shot-ball template here: the body description every shot is stamped from is PhysicsWorld's, built
            //once with the simulation and copied per shot rather than held as a field and written over. Its SWEPT
            //collidable — a bounded speculative margin plus ContinuousDetection.Continuous — is what gives the
            //shot continuous collision detection, which at SHOOT_MULTIPLIER it cannot do without.
            _shotBalls = new List<PhysicsBall>();
            _fallingBalls = new List<PhysicsBall>();

            //The constructor deals a full queue, so the player has something to read from the first frame. The
            //next-colour policy is handed in and no hooks are: the Testbed has no per-slot state to carry through
            //an advance (the colour transmutation is the Game's).
            _magazine = new Magazine(RandomBallType);
        }

        private static BallType RandomBallType() =>
            (BallType)RANDOM.Next((int)BallType.Type1, (int)BallType.Type13 + 1);

        private void ShootBall(Vector3? targetOverride = null)
        {
            //In game mode the shot leaves from the ball the player watched sitting at the head of the queue,
            //not from the pivot in the middle of the barrel, so the drawn ball and the physics one that
            //replaces it are at the same place and the shot reads as that ball leaving the bore
            var sourcePosition = _gameMode ? _cannon.MuzzlePosition(_cannonRig.PivotToFrontBall) : _camera.Position;
            var shootTarget = targetOverride ?? (_gameMode ? _cannon.AimTarget : _camera.Target);

            var direction = shootTarget - sourcePosition;
            direction.Normalize();
            Vector3 launchDirection = direction; //unit, before it is scaled to a velocity below
            direction *= SHOOT_MULTIPLIER;

            PhysicsBall ball = new()
            {
                //Added to the simulation and registered as a contact listener in one call, in that order — a
                //listener is keyed on a collidable reference, so the body has to exist first. This is the only
                //place anything is registered, which is what makes "every listener is a shot in the air" true;
                //RetireBall is the unregister the TODO that stood here asked for.
                //ToNumerics is the framework's own crossing into Bepu's vector type, which this file used to
                //write out by hand here and call by name two hundred lines below
                BallReference = _world.AddShotBall(sourcePosition.ToNumerics(), direction.ToNumerics(), _eventHandler),
                Type = _magazine.Peek() //The colour the player saw loaded at the muzzle - so aiming for it means something
            };

            //Advance the magazine: the fired ball's slot empties, the queue shifts up and a new one loads
            _magazine.Advance();

            //The gun's own answer (#115): the tube thrown back in its cradle, the carriage lurching a beat
            //behind it — both the shared Cannon's, drawing only. Only where the GUN fired: a free-mode shot
            //leaves the camera, and a rain of test balls must not rattle a gun that did nothing.
            if (_gameMode) _cannon.KickRecoil();

            _shotBalls.Add(ball);
            InvalidateBallCounts();

            //Give the shot its launch smear: a colour streak at the muzzle, along the shot, fading over its own
            //short life (aged in Update, drawn in Draw). Only the ball's authored tint is handed over - decoding
            //it to linear, lifting its peak off the floor and boosting it to a glowing radiance is the smear's
            //own rule, and it was written out here and in the Game identically until #76.
            _smears.Add(sourcePosition, launchDirection, BasicEffectParamsProvider.GetDiffuseTintByType(ball.Type));
        }

        /// <summary>
        /// Y below which a ball is considered fallen out of the world. Set below the funnel's hole
        /// (<see cref="ArenaIsland.FUNNEL_BOTTOM_Y"/>) so a ball that drops through it falls a visible distance into the
        /// drop below the platform before it is removed.
        /// </summary>
        private static readonly float KILL_PLANE_Y = -42f;

        /// <summary>
        /// Removes balls that can no longer affect gameplay from the simulation and from the given list:
        /// balls that fell below <see cref="KILL_PLANE_Y"/> and balls that came to rest on the ground
        /// (their body fell asleep - flying or rolling bodies never sleep).
        /// </summary>
        /// <returns>Number of removed balls.</returns>
        private int RemoveFallenBalls(List<PhysicsBall> balls)
        {
            int removed = 0;

            for (int i = balls.Count - 1; i >= 0; i--)
            {
                BodyReference body = balls[i].BallReference;

                //The sleep cull is deliberately the Testbed's alone: the Game culls on the kill plane only,
                //because a ball that settles on the island's stone winking out in front of the player reads as a
                //bug whatever it saves (docs/game-session.md). What the two share to the line is the retire below.
                if (body.Pose.Position.Y >= KILL_PLANE_Y && body.Awake) continue;

                //Unregisters the ball's listener if it still has one, then removes the body — that order being
                //PhysicsWorld.RetireBall's whole point, a listener being keyed on a collidable reference Bepu is
                //free to hand to the next body added. Its answer (whether the shot was still unresolved) is what
                //the Game scores a miss on; nothing here keeps score.
                _world.RetireBall(body);
                balls.RemoveAt(i);
                removed++;

#if DEBUG
                Console.WriteLine("Removed a fallen ball from the simulation");
#endif
            }

            return removed;
        }

        /// <summary>
        /// Debug action (End): releases the whole hanging structure at once. The balls move into
        /// <see cref="_fallingBalls"/>, so <see cref="RemoveFallenBalls"/> culls them once they come
        /// to rest — leaving them in <see cref="_physicsBalls"/> kept the pile on the ground alive
        /// (and generating contact constraints) forever.
        /// </summary>
        private void ReleaseAllBalls()
        {
            if (_physicsBalls == null || _map == null) return;

            if (BallsConstraintsBuilder.ReleaseAllBalls(_physicsBalls, _map, _world.Simulation, _fallingBalls) > 0)
                InvalidateBallCounts();
        }

        private void RemoveAllConstraints()
        {
            if (_physicsBalls == null || _physicsBalls.Rank != 3) return;

            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (byte level = 0; level < size.Level; level++)
                for (byte x = 0; x < size.X; x++)
                    for (byte z = 0; z < size.Z; z++)
                        _physicsBalls[x, z, level]?.RemoveAllConstraints(_world.Simulation);
        }
    }
}
