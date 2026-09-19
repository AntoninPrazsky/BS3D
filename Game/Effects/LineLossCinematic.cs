using Microsoft.Xna.Framework;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The camera's answer to losing on the line (#434): the moment the cluster crosses the floor's net, the
    /// lens is taken off the player and flown in at the crossing point, and the result screen is held back for
    /// a beat so the moment can register.
    /// <para>
    /// <b>It is the loss-side counterpart of <see cref="DropCinematic"/></b> and is built the same way — a pose
    /// and a blend, not a mode. The camera path composes it exactly as it composes that one, and
    /// <c>CameraTakeoverEngaged</c> counts it, so the gun stops answering and the aim stops being read for the
    /// same reason and through the same gate.
    /// </para>
    /// <para>
    /// <b>Why the ending waits.</b> Until #434 a line loss went straight from the frame a ball crossed to the
    /// result page — the owner's report is that there was no way to see what had happened or why. The hold is
    /// the whole point of the feature and not a delay bolted to it: the net is flaring, the camera is arriving,
    /// and a player who has just lost gets a second to watch it rather than a page of numbers.
    /// </para>
    /// <para>
    /// <b>It is not skippable, and that is deliberate</b> where the drop cinematic is. That one runs on a win
    /// and a player who wants the next level should not have to sit through it; this one is under two seconds
    /// and is the explanation of a loss. A skip here would be a player dismissing the only answer the game
    /// gives them to <i>why</i>.
    /// </para>
    /// </summary>
    internal sealed class LineLossCinematic
    {
        //How the beat is spent: the fly-in, then the hold at the crossing point with the net at full flare.
        //The result page takes the camera from here, so nothing is spent flying back out.
        private const float MOVE_IN = 0.55f;
        private const float HOLD = 1.15f;

        //How close the lens comes to the crossing point, and how far round the arena it stands to get there.
        //Near enough that the beam and the ball that crossed it are both large in frame; not so near that the
        //cluster above fills it, because what is being explained is the LINE and not the cluster.
        private const float STAND_OFF = 13f;

        //And how far above the net the lens rides. Slightly over it, looking very slightly down: a lens under
        //the line would frame the crossing against the drain's dark throat, where a red beam reads as nothing.
        private const float RISE = 3.2f;

        //The blend in and out of the player's own pose, both eased. The out is longer than the in: arriving has
        //to be quick to feel immediate, and leaving is the result page's own arrival, which must not snatch.
        private const float BLEND_IN = 0.30f;
        private const float BLEND_OUT = 0.45f;

        //A touch tighter than the play camera, which is what makes the arrival read as leaning IN rather than
        //as sliding sideways.
        private const float FIELD_OF_VIEW_SCALE = 0.86f;

        private bool _running;
        private float _elapsed;
        private float _playerFov;

        private Vector3 _from;
        private Vector3 _crossing;
        private Vector3 _to;

        /// <summary>Whether the camera is being taken, or is still on its way back to the player's pose.</summary>
        public bool Engaged => _running || _blend > 0f;

        /// <summary>How much of this pose is mixed into the player's, 0…1.</summary>
        public float Blend => _blend;

        private float _blend;

        public Vector3 Position { get; private set; }

        public Vector3 Target { get; private set; }

        public float FieldOfView { get; private set; }

        /// <summary>Whether the ending it is holding back may now be shown.</summary>
        public bool HoldExpired => _elapsed >= MOVE_IN + HOLD;

        /// <summary>
        /// Takes the camera towards <paramref name="crossing"/> — where the lowest ball met the line.
        /// </summary>
        /// <param name="crossing">The world point the cluster crossed at.</param>
        /// <param name="playerEye">Where the lens is now, which is what it flies from and blends out of.</param>
        /// <param name="arenaCentre">The arena's own centre, which decides which side the lens comes in on:
        /// <b>outside</b> the crossing point rather than between it and the middle, so the shot looks inward
        /// across the beam and the island's rim is behind it instead of the open sky.</param>
        /// <param name="playerFov">The field of view being used now, so the tightening is relative.</param>
        public void Begin(Vector3 crossing, Vector3 playerEye, Vector3 arenaCentre, float playerFov)
        {
            _running = true;
            _elapsed = 0f;
            _blend = 0f;
            _playerFov = playerFov;
            _from = playerEye;
            _crossing = crossing;

            //Outward from the centre, flattened: a stand computed in three dimensions would put the lens under
            //the island whenever the crossing point was low, which is most of the time — the line IS low.
            Vector3 outward = new(crossing.X - arenaCentre.X, 0f, crossing.Z - arenaCentre.Z);

            //A crossing dead over the middle has no outward direction of its own; the lens then comes from
            //where the player already was, which keeps the move short and never degenerates.
            if (outward.LengthSquared() < 0.01f)
            {
                outward = new Vector3(playerEye.X - arenaCentre.X, 0f, playerEye.Z - arenaCentre.Z);
                if (outward.LengthSquared() < 0.01f) outward = Vector3.UnitZ;
            }

            outward.Normalize();

            _to = crossing + outward * STAND_OFF + Vector3.Up * RISE;
        }

        public void Update(float elapsed)
        {
            if (_running)
            {
                _elapsed += elapsed;

                //Eased all the way in, so the arrival has no corner in it: the lens leaves the player at rest
                //and settles at the crossing point at rest, and everything between is one move.
                float t = MathHelper.Clamp(_elapsed / MOVE_IN, 0f, 1f);
                float eased = MathHelper.SmoothStep(0f, 1f, t);

                Position = Vector3.Lerp(_from, _to, eased);
                Target = _crossing;
                FieldOfView = MathHelper.Lerp(_playerFov, _playerFov * FIELD_OF_VIEW_SCALE, eased);

                _blend = MathF.Min(1f, _blend + elapsed / BLEND_IN);
            }
            else if (_blend > 0f)
            {
                _blend = MathF.Max(0f, _blend - elapsed / BLEND_OUT);
            }
        }

        /// <summary>Lets the camera go, which starts the blend back. Called when the ending is shown.</summary>
        public void Release() => _running = false;

        /// <summary>Back to nothing — a fresh level has no loss behind it.</summary>
        public void Reset()
        {
            _running = false;
            _blend = 0f;
            _elapsed = 0f;
        }

        public string Describe() =>
            $"crossing at ({_crossing.X:F1}, {_crossing.Y:F1}, {_crossing.Z:F1}), "
            + $"in {MOVE_IN:F2}s + hold {HOLD:F2}s";
    }
}
