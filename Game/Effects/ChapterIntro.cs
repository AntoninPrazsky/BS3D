using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace BS3D.Effects
{
    /// <summary>
    /// The establishing shot for a new chapter (#267): when a level is the first entry of a new <b>block</b>
    /// — a new scene, dome, music theme and style all at once — the camera tours the arena before handing
    /// control to the gun, so the player registers "this is a new place" rather than discovering it mid-aim.
    /// The menu's own fly-in (<c>BackdropScreen</c>, #254/#261) does the equivalent for the front end; this
    /// is gameplay's counterpart, and there was none before #267.
    /// <para>
    /// <b>A pose and a blend — not a mode</b>, the same shape <see cref="DropCinematic"/> already is, and for
    /// the identical reason: this class owns no camera and no input, it is handed a pivot and a stand-off once
    /// at <see cref="Begin"/> and answers a pose every frame, and the caller Lerps between it and the ordinary
    /// gameplay pose by <see cref="Blend"/> — the one-reversible-scalar idiom precise aim and the drop
    /// cinematic both use, so an interrupted or skipped intro can never leave the camera anywhere the game did
    /// not put it.
    /// </para>
    /// <para>
    /// <b>What is deliberately not here, unlike the drop cinematic.</b> There is no subject to follow — the
    /// cluster is hanging, not falling — so there is no time scale: the world runs at its ordinary speed
    /// throughout, because nothing about a fresh level's rest pose needs slowing down to be read. And the arc
    /// is a plain wide-to-close orbit around one fixed point rather than a three-leg key-framed one: there is
    /// no drain to look through and no "which side of the island" question the way a released group's fall
    /// asks (see <see cref="DropCinematic.KeepBallsInSight"/>). The wide end is scaled off the level's own
    /// solved stand-off, so it clears the arena the same way the ordinary gameplay camera already does,
    /// without needing that reasoning worked out a second time here.
    /// </para>
    /// </summary>
    internal sealed class ChapterIntro
    {
        public bool Engaged => _running || _blend > 0f;

        public float Blend => _blend;

        public Vector3 Position { get; private set; }

        public Vector3 Target { get; private set; }

        public float FieldOfView { get; private set; }

        //How long the wide-to-close pose interpolation runs before the intro ends on its own. Long enough to
        //register as a tour rather than a flash, short enough that a player eager to play is not held from a
        //new chapter's very first shot for much longer than the drop cinematic ever holds them from the next.
        private const float DURATION_SECONDS = 5f;

        //The grab is gentle, unlike the drop cinematic's — nothing here is racing a falling body, so there is
        //no reason for the take to be abrupt. The release is NOT gentler than the drop cinematic's own,
        //though, and that is measured rather than a style choice: Engaged gates the gun strictly on
        //_blend > 0f, and an exponential does not reach the 0.002 snap below until about 6.2 taus in — at a
        //first-cut 0.5 that is a 3.1 SECOND dead zone after the tour visually looks over, during which a
        //player who has already skipped is still holding a gun that will not fire. Matched to the drop
        //cinematic's own BLEND_OUT_TAU instead, which is exactly this same gate and was already tuned against
        //it: control is back within about a second of the tour ending, skip or not.
        private const float BLEND_IN_TAU = 0.35f;
        private const float BLEND_OUT_TAU = 0.16f;

        //A skip is ignored for this long after the intro starts, for the drop cinematic's own reason: whatever
        //button just advanced into this level (Next Level, a menu Play) is one of the buttons that skips, and
        //a player who arrived with it still held would otherwise never see the shot begin.
        private const float SKIP_LOCKOUT = 0.3f;

        private bool _running;
        private float _blend;
        private float _elapsed;

        //The shot, rolled once in Begin, so consecutive chapter opens are not the same orbit at a different
        //scale. Everything here is picked or derived from the level's own solved camera, which is what stops
        //this needing its own island-clearance reasoning — see the class remarks.
        private Vector3 _centre;
        private float _azimuth, _orbitRate;
        private float _radiusWide, _radiusClose;
        private float _elevationWide, _elevationClose;
        private float _fov;

        /// <summary>
        /// Takes the camera. <paramref name="centre"/> is the point the ordinary gameplay camera already
        /// looks at — the field's own aim height on the gun's orbit axis — so the tour circles the very thing
        /// the player is about to be handed rather than a second point invented for the shot.
        /// <paramref name="gameDistance"/> and <paramref name="gameFov"/> are the level's own solved stand-off
        /// and field of view (<c>GameCameraFit.Solve</c>'s), which the wide end scales off so a tall level's
        /// intro stands back further exactly as its ordinary camera already does.
        /// </summary>
        public void Begin(Vector3 centre, float gameDistance, float gameFov, Random random)
        {
            _running = true;
            _elapsed = 0f;
            _centre = centre;

            _azimuth = (float)random.NextDouble() * MathHelper.TwoPi;

            //A slow, one-directional drift — no swing to resolve the way the drop cinematic's does, since
            //there is no player bearing to return the frame to. It just has to not be static.
            _orbitRate = MathHelper.ToRadians(Lerp(random, 4f, 9f)) * (random.Next(2) == 0 ? 1f : -1f);

            _radiusWide = gameDistance * Lerp(random, 1.8f, 2.4f);
            _radiusClose = gameDistance * Lerp(random, 1.15f, 1.35f);

            _elevationWide = MathHelper.ToRadians(Lerp(random, 28f, 42f));
            _elevationClose = MathHelper.ToRadians(Lerp(random, 10f, 18f));

            //A touch wider than the gameplay frame at the start — an establishing shot reads the place, not
            //the subject close up — settling towards the ordinary frame as the orbit closes in.
            _fov = gameFov * Lerp(random, 0.96f, 1.10f);
        }

        /// <summary>One frame. Call every frame regardless of <see cref="Engaged"/>; a no-op once it is not.</summary>
        public void Update(float elapsed)
        {
            if (_running)
            {
                _elapsed += elapsed;

                if (_elapsed >= DURATION_SECONDS) End();
            }

            float target = _running ? 1f : 0f;
            float tau = _running ? BLEND_IN_TAU : BLEND_OUT_TAU;

            _blend = target + (_blend - target) * MathF.Exp(-elapsed / tau);

            //Snapped at the ends, exactly as the drop cinematic's own blend is: an exponential never actually
            //arrives, and "the gameplay pose bit for bit" has to be reachable or the camera is left a hair off.
            if (target == 0f && _blend < 0.002f) _blend = 0f;
            if (target == 1f && _blend > 0.998f) _blend = 1f;

            if (!Engaged) return;

            Frame();
        }

        /// <summary>
        /// The player has seen enough. Ignored for <see cref="SKIP_LOCKOUT"/> after the start, and the ease
        /// out is the ordinary one — a skip is not a cut, it just stops waiting for the orbit to finish.
        /// </summary>
        public bool TrySkip()
        {
            if (!_running || _elapsed < SKIP_LOCKOUT) return false;

            End();
            return true;
        }

        /// <summary>Drops everything, for a session being torn down under it.</summary>
        public void Reset()
        {
            _running = false;
            _blend = 0f;
            _elapsed = 0f;
        }

        /// <summary>What the roll picked, for the one log line the trigger writes.</summary>
        //ASCII only and invariant, for the same reason DropCinematic.Describe is: a console whose code page
        //mangles a degree sign, and a figure two machines might compare.
        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "radius {0:F0}->{1:F0}, elevation {2:F0}deg->{3:F0}deg, {4:F1}s",
            _radiusWide, _radiusClose, MathHelper.ToDegrees(_elevationWide), MathHelper.ToDegrees(_elevationClose),
            DURATION_SECONDS);

        private void End() => _running = false;

        /// <summary>
        /// Builds this frame's pose: a smoothstepped wide-to-close orbit around <see cref="_centre"/>, which
        /// is both the pivot and the look-at target throughout — there is nothing else in frame for the shot
        /// to drift towards, unlike the drop cinematic's drain axis.
        /// </summary>
        private void Frame()
        {
            float t = Smooth(Saturate(_elapsed / DURATION_SECONDS));

            float radius = MathHelper.Lerp(_radiusWide, _radiusClose, t);
            float elevation = MathHelper.Lerp(_elevationWide, _elevationClose, t);
            float azimuth = _azimuth + _orbitRate * _elapsed;

            float horizontal = MathF.Cos(elevation) * radius;

            Position = _centre + new Vector3(
                MathF.Cos(azimuth) * horizontal,
                MathF.Sin(elevation) * radius,
                MathF.Sin(azimuth) * horizontal);

            Target = _centre;
            FieldOfView = _fov;
        }

        private static float Lerp(Random random, float from, float to) => from + (float)random.NextDouble() * (to - from);

        private static float Saturate(float value) => MathHelper.Clamp(value, 0f, 1f);

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
