using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Globalization;

namespace BS3D
{
    /// <summary>
    /// The reward shot: when a shot cuts a big enough group loose, the camera lets go of the gun and follows
    /// the wreckage down — the fall, the run into the glass drain, and (where the scene has anywhere to fall
    /// to) the balls dropping out of the hole underneath the island. The player watches what they just did
    /// instead of being told about it by a number in the corner.
    /// <para>
    /// <b>It is a pose, a time scale and a blend — not a mode.</b> This class owns no camera, no simulation
    /// and no input; it is handed the subject's position each frame and answers where the lens should be,
    /// how fast the world should run, and how much of the frame is its rather than the gun's. The caller
    /// interpolates between its pose and the ordinary gameplay pose by <see cref="Blend"/>, which is the
    /// same one-reversible-scalar idiom precise aim uses: at 0 the gameplay pose comes back bit for bit, so
    /// an interrupted or skipped cinematic cannot leave the camera anywhere the game did not put it.
    /// </para>
    /// <para>
    /// <b>The arc is driven by the balls, not by a stopwatch.</b> Every framing parameter is a function of
    /// how far the subject has fallen between the height it was released at and the kill plane, so the
    /// camera cannot run ahead of a slow collapse or lag a fast one, and the shot ends when the drop does.
    /// A wall-clock cap is the only timer, and it exists solely because balls that come to rest on the stone
    /// ring are never culled and would otherwise hold the camera for ever.
    /// </para>
    /// </summary>
    internal sealed class DropCinematic
    {
        #region What the frame asks it

        /// <summary>
        /// True while this owns any part of the frame — running, or easing back to the gameplay pose. The
        /// caller locks the gun's controls on exactly this, so control returns at the instant the camera
        /// finishes handing the frame back rather than at the instant the balls stop being interesting.
        /// </summary>
        public bool Engaged => _running || _blend > 0f;

        /// <summary>How much of the frame is the cinematic's, 0 to 1. Ease, never a cut.</summary>
        public float Blend => _blend;

        /// <summary>
        /// What to multiply the frame's elapsed time by before spending it on the simulation. 1 whenever
        /// this is not running, so the world is never left running slow.
        /// <para>
        /// Slowing the simulation rather than the camera is the point: released balls fall a handful of
        /// units under real gravity and are on the stone in about a second, which is too quick to read.
        /// Because the fixed timestep is untouched and only the accumulated time fed to it is scaled, the
        /// simulation is as deterministic and as stable slowed as it is at speed.
        /// </para>
        /// </summary>
        public float TimeScale { get; private set; } = 1f;

        public Vector3 Position { get; private set; }

        public Vector3 Target { get; private set; }

        public float FieldOfView { get; private set; }

        /// <summary>The dutch tilt, in radians, for <see cref="RecoilCamera.BaseRoll"/>.</summary>
        public float Roll { get; private set; }

        #endregion

        #region Dials

        /// <summary>
        /// How many released balls a shot has to cut loose before it earns a cinematic. Matched and orphaned
        /// together: a shot that drops three of its own colour and brings six more down with it is exactly
        /// the shot worth watching, and the scorer already pays double for the orphans.
        /// </summary>
        public const int MIN_BALLS = 6;

        //Wall-clock ceiling on one shot. It is a backstop rather than a pace: balls that come to rest on the
        //island's stone ring are never culled (see RemoveFallenBalls), so "the subject is gone" is not a
        //condition that always arrives.
        private const float MAX_SECONDS = 6.5f;

        //The camera grabs the frame faster than it gives it back: the grab has to keep up with balls that
        //are already falling, while the hand-back is the player being returned to a gun they are about to
        //aim, and a snap there reads as a glitch.
        private const float BLEND_IN_TAU = 0.09f;
        private const float BLEND_OUT_TAU = 0.16f;

        //A skip is ignored for this long after the cinematic starts. The shot that triggered it was fired
        //with the very button that skips, and a player firing in bursts would otherwise never see one —
        //the same reason the splash refuses to be dismissed on its first frame.
        private const float SKIP_LOCKOUT = 0.3f;

        //Slow motion eases in after the release has been seen at full speed, and is let go once the balls
        //are through the drain — the drop out of the hole reads better at speed, and handing the world back
        //to the player already at 1x means nothing lurches when control returns.
        private const float SLOWMO_DELAY = 0.18f;
        private const float SLOWMO_FADE = 0.35f;

        //One full sweep of the tilt over this long, then level again — Saturate on the phase, so a shot that
        //outlasts it ends level rather than tipping back the other way.
        private const float ROLL_PERIOD = 3.2f;

        //What counts as the drop having stopped being one (see Stalled). In simulated seconds.
        private const float STALL_DROP = 1f;
        private const float STALL_SECONDS = 0.9f;

        //Heights the arc is measured against. The island's stone and the drain's throat are where the three
        //interesting things happen, so they are the anchors rather than a linear run to the kill plane.
        private const float ISLAND_Y = BS3DGame.ISLAND_Y;
        private const float FUNNEL_BOTTOM_Y = BS3DGame.FUNNEL_BOTTOM_Y;

        //How far clear of the stone a lens is held. Small: it is a guard against the arc grazing the platform,
        //not the thing that decides where the shot stands.
        private const float STONE_CLEARANCE = 1.5f;

        //Taken off the mouth's own radius before the sight line is fitted through it, so a ball that is off
        //the axis — and they all are, they scatter — is inside the aperture rather than exactly on its edge.
        private const float MOUTH_MARGIN = 3f;

        #endregion

        private bool _running;
        private float _blend;
        private float _elapsed;
        private float _startY;
        private bool _subjectSeen;
        private float _stallY, _stallSeconds;

        //The lens follows a smoothed centre rather than the raw one: balls leave the subject as the kill
        //plane takes them, and a mean that loses a member jumps. Smoothing also just looks like a camera
        //operator rather than a solver.
        private Vector3 _pivot;

        //The shot, chosen once in Begin. Everything here is either rolled or picked from the scene, which
        //is what stops the tenth cinematic of a session from being the first one again.
        private float _azimuth, _orbitRate;
        private float _radiusFall, _radiusDrain, _radiusOut;
        private float _elevationFall, _elevationDrain, _elevationOut;
        private float _rollAmplitude, _slowMotion, _fov;
        private bool _openBelow;

        /// <summary>
        /// Takes the camera. <paramref name="centre"/> is where the released group is at the moment it is
        /// cut loose, and <paramref name="playerEye"/> where the player is watching from — the shot is set
        /// up relative to that bearing so the cut is a swing around the drop rather than a jump to the far
        /// side of it, which is the difference between a camera move and losing the player entirely.
        /// </summary>
        public void Begin(SceneKind scene, Vector3 centre, Vector3 playerEye, int balls, Random random)
        {
            _running = true;
            _elapsed = 0f;
            _startY = centre.Y;
            _pivot = centre;
            _subjectSeen = true;
            _stallY = centre.Y;
            _stallSeconds = 0f;

            //Where the player already is, so the swing is measured from their own view of the field
            Vector3 toEye = playerEye - centre;
            float playerBearing = MathF.Atan2(toEye.Z, toEye.X);

            //Which side to swing to, and how far. Never past a quarter turn: further and the player loses
            //which way the field is facing, and has to find it again when control comes back.
            float swing = MathHelper.ToRadians(Lerp(random, 18f, 62f)) * (random.Next(2) == 0 ? 1f : -1f);
            _azimuth = playerBearing + swing;

            //A slow arc over the shot. Signed independently of the swing, so the camera sometimes comes to
            //rest and sometimes carries on past.
            _orbitRate = MathHelper.ToRadians(Lerp(random, 5f, 17f)) * (random.Next(2) == 0 ? 1f : -1f);

            //Big collapses are framed wider, because there is more of them to fit
            float size = MathHelper.Clamp(balls / 18f, 0f, 1f);

            //How near the shot plays. Rolled per cinematic — the same drop should not always be the same
            //distance away — then widened for the size of the group.
            float scale = Lerp(random, 0.82f, 1.32f) + size * 0.35f;

            _radiusFall = 26f * scale;
            _radiusDrain = 21f * scale;
            _radiusOut = 24f * scale;

            //Where the shot may stand, and it is the scene's business rather than the roll's — see OpenBelow.
            _openBelow = OpenBelow(scene);

            //The arc's shape, and the two families genuinely differ. Both start over the falling group; from
            //there an open scene drops under the island to watch the balls through the glass and out of the
            //hole, while a closed one climbs instead and ends looking straight down the throat, because the
            //mouth is the only hole in the stone and from underneath there is nothing to see at all.
            _elevationFall = MathHelper.ToRadians(Lerp(random, 12f, 30f));

            _elevationDrain = MathHelper.ToRadians(_openBelow ? Lerp(random, -4f, 9f) : Lerp(random, 26f, 42f));
            _elevationOut = MathHelper.ToRadians(_openBelow ? Lerp(random, -38f, -18f) : Lerp(random, 48f, 72f));

            //Tilt: a third of the shots stay level, because a tilt that happens every time stops reading as
            //emphasis and starts reading as a broken horizon.
            _rollAmplitude = random.Next(3) == 0 ? 0f : MathHelper.ToRadians(Lerp(random, 4f, 11f)) * (random.Next(2) == 0 ? 1f : -1f);

            //How slow it goes. A range rather than a figure, so consecutive cinematics have their own weight.
            _slowMotion = Lerp(random, 0.32f, 0.55f);

            //A touch tighter than the gameplay frame, which is solved to fit the whole field and is wider
            //than anything wants for a close subject
            _fov = GameplayScreen.GAME_FOV * Lerp(random, 0.80f, 0.94f);
        }

        /// <summary>
        /// One frame. <paramref name="subjectAlive"/> is false once the kill plane has taken every ball of
        /// the group, which is the shot's natural end.
        /// </summary>
        public void Update(float elapsed, bool subjectAlive, Vector3 subjectCentre)
        {
            if (_running)
            {
                _elapsed += elapsed;

                if (subjectAlive)
                {
                    _subjectSeen = true;

                    //Critically damped enough to keep up with a falling body without ringing, and framed in
                    //Exp so the follow is the same at any frame rate
                    _pivot = subjectCentre + (_pivot - subjectCentre) * MathF.Exp(-elapsed / 0.10f);
                }

                //Over when the drop is: every ball culled, the drop stalled, or the backstop reached. A
                //subject that was never seen alive at all ends it immediately rather than holding an empty
                //frame.
                if (!subjectAlive || !_subjectSeen || Stalled(elapsed) || _elapsed >= MAX_SECONDS) End();
            }

            float target = _running ? 1f : 0f;
            float tau = _running ? BLEND_IN_TAU : BLEND_OUT_TAU;

            _blend = target + (_blend - target) * MathF.Exp(-elapsed / tau);

            //Snapped at the ends, exactly as the precise-aim blend is: an exponential never actually arrives,
            //and "the gameplay pose bit for bit" has to be reachable or the camera is left a hair off for ever
            if (target == 0f && _blend < 0.002f) _blend = 0f;
            if (target == 1f && _blend > 0.998f) _blend = 1f;

            if (!Engaged)
            {
                TimeScale = 1f;
                Roll = 0f;
                return;
            }

            Frame();
        }

        /// <summary>
        /// The player has seen enough. Ignored for <see cref="SKIP_LOCKOUT"/> after the start, and the ease
        /// out is the ordinary one — a skip is not a cut, it just stops waiting for the balls.
        /// </summary>
        public bool TrySkip()
        {
            if (!_running || _elapsed < SKIP_LOCKOUT) return false;

            End();
            return true;
        }

        /// <summary>
        /// Has the drop stopped being a drop? The subject descending by less than <see cref="STALL_DROP"/>
        /// over <see cref="STALL_SECONDS"/> means the last of it has come to rest on the island's stone,
        /// where nothing will ever remove it (see <c>RemoveFallenBalls</c>) — so waiting for the kill plane
        /// leaves the camera holding on a straggler, which is a shot with nothing in it. It is what a first
        /// pass did, and the sea's last frames were an empty blue rectangle with one ball in the corner.
        /// <para>
        /// Measured in <b>simulated</b> seconds, not wall-clock ones. In slow motion a group that has only
        /// just been released covers well under a unit of real time's worth of fall, and a wall-clock timer
        /// would call that a stall and cut the shot at the moment it began.
        /// </para>
        /// </summary>
        private bool Stalled(float elapsed)
        {
            if (_pivot.Y < _stallY - STALL_DROP)
            {
                _stallY = _pivot.Y;
                _stallSeconds = 0f;

                return false;
            }

            _stallSeconds += elapsed * TimeScale;

            return _stallSeconds >= STALL_SECONDS;
        }

        /// <summary>What the roll picked, for the one log line the trigger writes.</summary>
        //ASCII only, and invariant: this goes to a console whose code page mangles anything else (an arrow and
        //a degree sign both came back as "r" on a Czech console, which made the figures unreadable), and the
        //machine's locale must not decide what a decimal point is in a log two machines get compared from.
        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "radius {0:F0}->{1:F0}, elevation {2:F0}deg->{3:F0}deg, tilt {4:F1}deg, slow-mo {5:F2}, {6}",
            _radiusFall, _radiusOut,
            MathHelper.ToDegrees(_elevationFall), MathHelper.ToDegrees(_elevationOut),
            MathHelper.ToDegrees(_rollAmplitude), _slowMotion, _openBelow ? "under the island" : "over the mouth");

        /// <summary>Drops everything, for a session being torn down under it.</summary>
        public void Reset()
        {
            _running = false;
            _blend = 0f;
            _elapsed = 0f;
            TimeScale = 1f;
            Roll = 0f;
        }

        private void End()
        {
            _running = false;

            //Handed back at speed. The blend still has to run out, and a world left in slow motion while the
            //player is being given the gun back is the one thing worse than never slowing it at all.
            TimeScale = 1f;
        }

        /// <summary>
        /// Builds this frame's pose from how far the subject has fallen. The three anchors are the island's
        /// top, the drain's throat and the kill plane, and each parameter is carried between them by a
        /// smoothstep — so the arc has no stages to snap between, only weights that move.
        /// </summary>
        private void Frame()
        {
            //Progress through each leg of the drop. Guarded divisors: a group released from below the island
            //top (a late collapse into an already-drained field) would otherwise divide by nothing.
            float fall = Saturate((_startY - _pivot.Y) / MathF.Max(_startY - ISLAND_Y, 1f));
            float drain = Saturate((ISLAND_Y - _pivot.Y) / (ISLAND_Y - FUNNEL_BOTTOM_Y));
            float below = Saturate((FUNNEL_BOTTOM_Y - _pivot.Y) / 12f);

            //The three legs are sequential by construction — each only starts once the one before it has
            //finished — so averaging them gives one monotonic run from the release to the last of the drop,
            //and every framing parameter is a three-key curve over it rather than a set of states.
            float progress = (fall + drain + below) / 3f;

            float radius = Key3(_radiusFall, _radiusDrain, _radiusOut, progress);
            float elevation = Key3(_elevationFall, _elevationDrain, _elevationOut, progress);

            float azimuth = _azimuth + _orbitRate * _elapsed;

            float horizontal = MathF.Cos(elevation) * radius;

            Vector3 lens = _pivot + new Vector3(
                MathF.Cos(azimuth) * horizontal,
                MathF.Sin(elevation) * radius,
                MathF.Sin(azimuth) * horizontal);

            Position = KeepBallsInSight(lens);

            //What it looks at drifts from the balls themselves towards the drain's axis as they go into it,
            //so the shot ends framed on the hole rather than on whichever ball was last to fall
            Vector3 axis = new(0f, _pivot.Y, 0f);
            Target = Vector3.Lerp(_pivot, axis, Smooth(drain) * 0.6f);

            FieldOfView = _fov;
            Roll = _rollAmplitude * MathF.Sin(MathHelper.Pi * Saturate(_elapsed / ROLL_PERIOD));

            //Real time for a beat, then slow, then back to real time as the balls clear the throat. The
            //ramps are smoothed at both ends: a step change in the time scale is a visible hitch, not a
            //slow-motion effect.
            float into = Smooth(Saturate((_elapsed - SLOWMO_DELAY) / SLOWMO_FADE));
            float outOf = Smooth(below);

            TimeScale = MathHelper.Lerp(1f, _slowMotion, into * (1f - outOf));
        }

        /// <summary>
        /// Whether the shot may go <b>under</b> the island. It is a property of the scene and not of the roll,
        /// because it is a question about what is opaque down there.
        /// <para>
        /// The two cities continue underneath the arena and fall away into a canyon, the sea is water the lens
        /// can dive into, and space is nothing at all — in each, the drain's glass is translucent, so from
        /// below the balls read right through the cone and then pour out of the hole. The solid-terrain scenes
        /// are a flat clearing with the island's footprint cut out of it and a <b>near-black pit shaft hugging
        /// the glass</b>: the ground is a lid, the shaft is opaque, and there is no vantage under there that
        /// can see a ball at all. Those shots stay over the stone and look down through the mouth, which is
        /// the only hole in it.
        /// </para>
        /// </summary>
        private static bool OpenBelow(SceneKind scene) =>
            scene is SceneKind.City or SceneKind.NeonCity or SceneKind.Sea or SceneKind.Space or SceneKind.Dream;

        /// <summary>
        /// Moves the lens off anything solid it would otherwise sit inside or try to look through. Every rule
        /// here serves one requirement — <b>the balls stay on screen for as much of the drop as possible</b> —
        /// and the only opaque thing between a lens and them is the island itself.
        /// </summary>
        private Vector3 KeepBallsInSight(Vector3 lens)
        {
            //While the balls are still above the stone, so is the lens. The island's top is an opaque annulus
            //from the mouth out to its rim: from below it a shot looks at the underside of the platform with
            //the drop hidden behind it.
            bool aboveStone = !_openBelow || _pivot.Y > ISLAND_Y;

            if (aboveStone) lens.Y = MathF.Max(lens.Y, ISLAND_Y + STONE_CLEARANCE);
            else lens = PushOutOfIsland(lens);

            //The mouth is the only hole in the stone, so a lens above it can see a ball below it only by
            //looking through that hole: the sight line has to cross the mouth plane inside the rim. The lens
            //lies in the cone that runs from the ball up through the rim, and clamping its horizontal reach
            //to satisfy that is what swings the shot overhead as the balls run deeper — the further down they
            //are, the narrower the set of places that can still see them. It is the whole of how a
            //solid-terrain scene is filmed, and a safety net in the others.
            if (lens.Y > ISLAND_Y && _pivot.Y < ISLAND_Y - 0.5f)
            {
                float depth = ISLAND_Y - _pivot.Y;
                float height = lens.Y - _pivot.Y;

                float maxReach = (BS3DGame.FUNNEL_TOP_RADIUS - MOUTH_MARGIN) * height / depth;

                Vector2 flat = new(lens.X, lens.Z);
                float reach = flat.Length();

                if (reach > maxReach && reach > 1e-3f)
                {
                    flat *= maxReach / reach;
                    lens.X = flat.X;
                    lens.Z = flat.Y;
                }
            }

            return lens;
        }

        /// <summary>
        /// The island as the solid it is — a drum of stone from the top face down to the underside. A lens
        /// inside it sees the inside of the platform and nothing else, which is what a shot that swung low
        /// used to drive straight through. Pushed out through the face on the side the balls are on, so the
        /// escape never puts the platform between the two.
        /// </summary>
        private static Vector3 PushOutOfIsland(Vector3 lens)
        {
            float top = ISLAND_Y + STONE_CLEARANCE;
            float bottom = ISLAND_Y - BS3DGame.ISLAND_EDGE_HEIGHT - STONE_CLEARANCE;

            if (lens.Y >= top || lens.Y <= bottom) return lens;
            if (new Vector2(lens.X, lens.Z).Length() > BS3DGame.ISLAND_RADIUS + STONE_CLEARANCE) return lens;

            //Only reached with the balls already under the stone (the caller's other branch has them above),
            //so down is the side that keeps them in shot
            lens.Y = bottom;

            return lens;
        }

        /// <summary>
        /// A three-key curve over the drop: <paramref name="a"/> at the release, <paramref name="b"/> at the
        /// halfway mark (the balls entering the drain) and <paramref name="c"/> at the end. Smoothstepped
        /// within each half, so the value arrives at the middle key with no corner in it.
        /// </summary>
        private static float Key3(float a, float b, float c, float t) => t < 0.5f
            ? MathHelper.Lerp(a, b, Smooth(t * 2f))
            : MathHelper.Lerp(b, c, Smooth((t - 0.5f) * 2f));

        private static float Lerp(Random random, float from, float to) => from + (float)random.NextDouble() * (to - from);

        private static float Saturate(float value) => MathHelper.Clamp(value, 0f, 1f);

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
