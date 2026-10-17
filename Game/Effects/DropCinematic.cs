using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Globalization;

namespace BS3D.Effects
{
    /// <summary>
    /// The reward shot: when a shot cuts a big enough group loose, the camera lets go of the gun and follows
    /// the wreckage down — the fall, the run into the glass drain, and (where the scene has anywhere to fall
    /// to) the balls dropping out of the hole underneath the island. The player watches what they just did
    /// instead of being told about it by a number in the corner.
    /// <para>
    /// <b>It is a pose and a blend — not a mode, and not a slowed clock.</b> This class owns no camera, no
    /// simulation and no input; it is handed the subject's position each frame and answers where the lens
    /// should be and how much of the frame is its rather than the gun's. The world runs at ordinary game
    /// time all the way through (#877): a fall in slow motion with a moved camera reads as a replay, and the
    /// shot is the player's own, live success. The caller
    /// interpolates between its pose and the ordinary gameplay pose by <see cref="Blend"/>, which is the
    /// same one-reversible-scalar idiom precise aim uses: at 0 the gameplay pose comes back bit for bit, so
    /// an interrupted or skipped cinematic cannot leave the camera anywhere the game did not put it.
    /// </para>
    /// <para>
    /// <b>The arc is driven by the balls, not by a stopwatch.</b> Every framing parameter is a function of
    /// how far the subject has fallen between the height it was released at and the kill plane, so the
    /// camera cannot run ahead of a slow collapse or lag a fast one, and the shot ends when the drop does.
    /// A wall-clock cap is the only timer, and it exists because a ball the game never culls can still come
    /// to rest — rarely, now that the stone ring is a dish that rolls a landed ball into the drain, but a
    /// pile propping itself up near the mouth still can — and would otherwise hold the camera for ever.
    /// </para>
    /// <para>
    /// <b>On the sea, the lens is pulled under with the subject rather than on the leg's own schedule
    /// alone</b> (#193, <see cref="SubmergeElevationPull"/>). The fall leg's elevation is positive by
    /// construction — the shot opens looking down at the release from above — and a subject that sinks
    /// under the water well before that leg is due to end used to leave the lens floating above a surface
    /// the submerge fade was already, correctly, hiding the cluster behind: the shot filmed nothing for its
    /// first stretch and the balls re-materialised mid-air as the schedule caught up. The pull reads exactly
    /// the same "is the subject under the water" question the fade answers with, off the sea's own level.
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

        /// <summary>True while the cinematic itself runs (not its blend back), and how long it has: what the HUD's
        /// skip hint reads (#499), so the hint can wait out <see cref="SKIP_LOCKOUT"/> and a moment more.</summary>
        public bool Running => _running;
        public float Elapsed => _elapsed;

        /// <summary>Why the shot ended, for the one log line a shot's end writes: the drop was over, it stalled, the
        /// backstop was reached or it was skipped. Empty while it runs.</summary>
        public string EndedBy { get; private set; } = "";

        /// <summary>How much of the frame is the cinematic's, 0 to 1. Ease, never a cut.</summary>
        public float Blend => _blend;

        public Vector3 Position { get; private set; }

        public Vector3 Target { get; private set; }

        public float FieldOfView { get; private set; }

        /// <summary>The dutch tilt, in radians, for <see cref="Prazsky.Core.Camera.RecoilCamera.BaseRoll"/>.</summary>
        public float Roll { get; private set; }

        #endregion

        #region Dials

        //The trigger's dials - the floor, the share of the level and the margin over the level's best - are
        //Prazsky.BS3D.DropTrigger's since #719, with the reasoning for each: one copy of the rule, which the game, the
        //tests and `LevelGen --drops` (the measurement the dials are set by) all read.

        //Wall-clock ceiling on one shot. It is a backstop rather than a pace: the game only culls at the
        //kill plane (see RemoveFallenBalls), and a ball can still come to rest short of it — rarely since
        //the stone ring dished, but a pile propping itself up near the drain's mouth still does — so "the
        //subject is gone" is not a condition that always arrives.
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

        //One full sweep of the tilt over this long, then level again — Saturate on the phase, so a shot that
        //outlasts it ends level rather than tipping back the other way.
        private const float ROLL_PERIOD = 3.2f;

        //What counts as the drop having stopped being one (see Stalled). In game seconds.
        private const float STALL_DROP = 1f;
        private const float STALL_SECONDS = 0.9f;

        //The most one frame adds to the stall's clock. The world a hitched frame lives through is capped by the physics'
        //steps per frame, so a long frame (the first draw of a pass, a capture's half second, a window dragged) has simulated
        //far less than it lasted; counted whole it could end a shot on its own. Slow motion used to hide this by scaling the
        //frame down (#877).
        private const float STALL_FRAME_MAX = 0.1f;

        //Heights the arc is measured against. The island's stone and the drain's throat are where the three
        //interesting things happen, so they are the anchors rather than a linear run to the kill plane. Both
        //stay const, which is why ArenaIsland states them as constants rather than as properties.
        private const float ISLAND_Y = ArenaIsland.TOP_Y;
        private const float FUNNEL_BOTTOM_Y = ArenaIsland.FUNNEL_BOTTOM_Y;

        //How far clear of the stone a lens is held. Small: it is a guard against the arc grazing the platform,
        //not the thing that decides where the shot stands.
        private const float STONE_CLEARANCE = 1.5f;

        //How far under the throat's foot the arc's last leg runs: the shot is at its end that far below it.
        private const float BELOW_LEG = 12f;

        //Taken off the mouth's own radius before the sight line is fitted through it, so a ball that is off
        //the axis — and they all are, they scatter — is inside the aperture rather than exactly on its edge.
        private const float MOUTH_MARGIN = 3f;

        //THE OPEN SCENES' WAY PAST THE STONE (#407, #878). A shot that goes under the island has to get from above
        //the top face to under the underside, and the only continuous way round a drum is round its OUTSIDE. The
        //lens's own radius is what takes it there (Frame): lifted, whatever the elevation, until the lens stands
        //RIM_REACH from the drain's axis, so the lens is clear of the rim all the way down and there is no frame on
        //which it is pushed. RIM_MARGIN over the drum's own clearance keeps KeepOutsideRim - the net behind it, which
        //eases a reach under RIM_REACH out over RIM_APPROACH units of lens height - and PushOutOfIsland, the last
        //resort, from ever having to fire.
        private const float RIM_APPROACH = 6f;
        private const float RIM_MARGIN = 7f;
        private const float RIM_REACH = ArenaIsland.RADIUS + STONE_CLEARANCE + RIM_MARGIN;
        private const float RIM_SOFTNESS = 2f;

        //And how fast the lens DIVES once the balls are under the stone: the elevation is pulled towards the
        //OUT leg's own angle, smoothly (Smoother), over this many units of the subject's descent below the top face.
        //The band's stone side hides the balls from a lens outside the rim, so the lens has to be under the underside
        //by the time they are a few units into the throat: 12 units of descent is about a second, and the lens is
        //under the drum when the subject is 3 to 4 units down. Over 3 it was a plunge of 90 units a second that
        //stopped dead (#878). The same shape as SUBMERGE_PULL_DEPTH, for the same reason: a pulled angle keeps the
        //orbit's own geometry, a clamped height does not.
        private const float STONE_DIVE_DEPTH = 12f;

        //...and no longer than this after the subject went under the top face, whatever it does there: a group that lingers
        //in the mouth (the real one sinks at a third of the speed a falling ball does) would otherwise hold the lens beside
        //the drum's blank wall for as long as it lingered.
        private const float STONE_DIVE_SECONDS = 1.1f;

        //How many units the SUBJECT has to be under the sea before the elevation pull (#193) is fully
        //engaged — see SubmergeElevationPull. The report was the eye still floating above the surface with the
        //subject already 3-7 units under it; the stone's dive (STONE_DIVE_DEPTH), which starts higher up, is
        //what keeps the lens under the water in time since #878, and this pull is the stronger of the two rather
        //than a second one on top. It answers a different question from the fade's own band
        //(UNDERWATER_FADE_DEPTH in SceneRenderer) — how fast the CAMERA ought to give up on a dry angle, not how
        //deep the WATER reads as opaque.
        private const float SUBMERGE_PULL_DEPTH = 8f;

        #endregion

        private bool _running;
        private float _blend;
        private float _elapsed;
        private float _startY;
        private bool _subjectSeen;
        private float _stallY, _stallSeconds;

        //The lens follows a smoothed centre rather than the raw one. The centre no longer jumps when the kill
        //plane takes a ball (DropFocus fades each one out before it goes, #616), so this is the camera
        //operator's own ease rather than a repair.
        private Vector3 _pivot;

        //How much of the subject is in the drain (DropFocus.FunnelShare): while it is, the look-at goes all the
        //way to the drain's axis rather than 60 % of it (#616).
        private float _funnelShare;

        //The legs' progress, and the two elevation pulls, held at their furthest this shot (#616). They are
        //functions of the pivot's height, and a subject whose lowest balls leave first can rise: fed straight
        //through, the arc stepped back and the lens was asked to un-dive — the "cannot decide where to look".
        //A pivot that climbs may slow the arc; it cannot reverse it.
        private float _fall, _drain, _below, _dive, _submerge, _depth;

        //When the subject first went under the island's top face (the shot's own seconds), or -1 while it has not.
        private float _crossedAt;

        //The shot, chosen once in Begin. Everything here is either rolled or picked from the scene, which
        //is what stops the tenth cinematic of a session from being the first one again.
        private float _azimuth, _orbitRate;
        private float _radiusFall, _radiusDrain, _radiusOut;
        private float _elevationFall, _elevationDrain, _elevationOut;
        private float _rollAmplitude, _fov;
        private bool _openBelow;

        //The sea's own mean water level (#193), and whether this shot is even over one — the only scene
        //with water a camera can get under (SceneRenderer.LensSubmergedAmount's own note). Read once at
        //Begin rather than asked of a live SceneRenderer every frame, the same "handed a value, not a
        //reference" rule the rest of this class's inputs already follow.
        private bool _isSea;
        private float _seaLevelY;

        /// <summary>
        /// Takes the camera. <paramref name="centre"/> is where the released group is at the moment it is
        /// cut loose, and <paramref name="playerEye"/> where the player is watching from — the shot is set
        /// up relative to that bearing so the cut is a swing around the drop rather than a jump to the far
        /// side of it, which is the difference between a camera move and losing the player entirely.
        /// <paramref name="seaLevelY"/> is <see cref="SceneRenderer.SeaLevelY"/> — meaningless off the sea,
        /// so the caller may pass anything there; <see cref="_isSea"/> is what actually gates its use.
        /// </summary>
        public void Begin(SceneKind scene, Vector3 centre, Vector3 playerEye, int balls, Random random,
            float seaLevelY)
        {
            _running = true;
            EndedBy = "";
            _elapsed = 0f;
            _startY = centre.Y;
            _pivot = centre;
            _funnelShare = 0f;
            _fall = _drain = _below = _dive = _submerge = _depth = 0f;
            _crossedAt = -1f;
            _subjectSeen = true;
            _stallY = centre.Y;
            _stallSeconds = 0f;

            _isSea = scene == SceneKind.Sea;
            _seaLevelY = seaLevelY;

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

            //Where the shot may stand, and it is the scene's business rather than the roll's — see
            //SceneRenderer.OpenBelow, which is where the question is answered since #75.
            _openBelow = SceneRenderer.OpenBelow(scene);

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

            //A touch tighter than the gameplay frame, which is solved to fit the whole field and is wider
            //than anything wants for a close subject
            _fov = GameplayScreen.GAME_FOV * Lerp(random, 0.80f, 0.94f);
        }

        /// <summary>
        /// One frame. <paramref name="subjectAlive"/> is false once there is nothing left to film — the kill
        /// plane has taken every ball of the group, or the drain has emptied of it (<c>DropFocus.TryResolve</c>)
        /// — which is the shot's natural end. <paramref name="funnelShare"/> is <c>DropFocus.FunnelShare</c>.
        /// </summary>
        public void Update(float elapsed, bool subjectAlive, Vector3 subjectCentre, float funnelShare)
        {
            if (_running)
            {
                _elapsed += elapsed;

                if (subjectAlive)
                {
                    _subjectSeen = true;
                    _funnelShare = funnelShare;

                    //Critically damped enough to keep up with a falling body without ringing, and framed in
                    //Exp so the follow is the same at any frame rate
                    _pivot = subjectCentre + (_pivot - subjectCentre) * MathF.Exp(-elapsed / 0.10f);
                }

                //Over when the drop is: every ball culled, the drop stalled, or the backstop reached. A
                //subject that was never seen alive at all ends it immediately rather than holding an empty
                //frame.
                if (!subjectAlive || !_subjectSeen) End("the subject was gone");
                else if (Stalled(elapsed)) End("the drop stalled");
                else if (_elapsed >= MAX_SECONDS) End("the backstop");
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

            End("skipped");
            return true;
        }

        /// <summary>
        /// Has the drop stopped being a drop? The subject descending by less than <see cref="STALL_DROP"/>
        /// over <see cref="STALL_SECONDS"/> means the last of it has stopped falling — wedged in a
        /// slow-draining pile near the drain's mouth, or (rarely, since the stone ring dished) at rest where
        /// only the kill plane could remove it (see <c>RemoveFallenBalls</c>) — so waiting for that plane
        /// leaves the camera holding on a straggler, which is a shot with nothing in it. It is what a first
        /// pass did, and the sea's last frames were an empty blue rectangle with one ball in the corner.
        /// <para>
        /// Measured in the frame's own seconds, which are the world's (the cinematic never slows the clock, #877), each
        /// frame counting at most <see cref="STALL_FRAME_MAX"/>.
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

            _stallSeconds += MathF.Min(elapsed, STALL_FRAME_MAX);

            return _stallSeconds >= STALL_SECONDS;
        }

        /// <summary>What the roll picked, for the one log line the trigger writes.</summary>
        //ASCII only, and invariant: this goes to a console whose code page mangles anything else (an arrow and
        //a degree sign both came back as "r" on a Czech console, which made the figures unreadable), and the
        //machine's locale must not decide what a decimal point is in a log two machines get compared from.
        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "radius {0:F0}->{1:F0}, elevation {2:F0}deg->{3:F0}deg, tilt {4:F1}deg, {5}",
            _radiusFall, _radiusOut,
            MathHelper.ToDegrees(_elevationFall), MathHelper.ToDegrees(_elevationOut),
            MathHelper.ToDegrees(_rollAmplitude), _openBelow ? "under the island" : "over the mouth");

        /// <summary>Drops everything, for a session being torn down under it.</summary>
        public void Reset()
        {
            _running = false;
            _blend = 0f;
            _elapsed = 0f;
            Roll = 0f;
        }

        private void End(string why)
        {
            _running = false;
            EndedBy = why;
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
            //Each held at its furthest this shot (#616 — see _fall), so the arc never runs backwards.
            float fall = _fall = MathF.Max(_fall, Saturate((_startY - _pivot.Y) / MathF.Max(_startY - ISLAND_Y, 1f)));
            float drain = _drain = MathF.Max(_drain, Saturate((ISLAND_Y - _pivot.Y) / (ISLAND_Y - FUNNEL_BOTTOM_Y)));
            float below = _below = MathF.Max(_below, Saturate((FUNNEL_BOTTOM_Y - _pivot.Y) / BELOW_LEG));

            //The three legs are sequential by construction — each only starts once the one before it has
            //finished — so averaging them gives one monotonic run from the release to the last of the drop,
            //and every framing parameter is a three-key curve over it rather than a set of states.
            //In an open scene the three are one curve through the same three anchors with no corner at the island's top or
            //the throat's foot (#878): summed, the progress ran at a different rate on each side of the anchor, and the
            //lens's climb or dive changed speed in one frame exactly as it passed the rim.
            float depth = _depth = MathF.Max(_depth, _startY - _pivot.Y);
            float progress = _openBelow ? SmoothProgress(depth) : (fall + drain + below) / 3f;

            float radius = Key3(_radiusFall, _radiusDrain, _radiusOut, progress);
            float elevation = Key3(_elevationFall, _elevationDrain, _elevationOut, progress);

            //Pulled towards the OUT leg's own diving angle as the subject sinks under the sea (#193), on top
            //of whatever the scheduled progress above already asked for. See SubmergeElevationPull for why
            //this exists: the fall leg's angle is positive by construction, so without it a subject already
            //under the water sits below a lens still floating above it, filming a cluster the submerge fade
            //correctly hides until the schedule alone eventually catches up.
            float submerge = _submerge = MathF.Max(_submerge, SubmergeElevationPull());

            //And towards the same angle as the subject goes under the STONE in an open scene (#407): the lens
            //passes the platform round its rim, where the drum's side hides the balls, so it dives for the
            //underside — where they show through the glass — about as fast as they go into the throat.
            if (_crossedAt < 0f && _pivot.Y < ISLAND_Y) _crossedAt = _elapsed;

            float dive = _dive = MathF.Max(_dive, StoneDivePull());

            //One pull towards the OUT angle, the stronger of the two (#878): applied one after the other they added, and on the
            //sea (the island's top above the water) the lens plunged on both at once, at twice the speed of either.
            float pull = MathF.Max(submerge, dive);
            if (pull > 0f) elevation = MathHelper.Lerp(elevation, _elevationOut, pull);

            //An open scene's lens never comes within the island's reach (the drum seen from outside), by its own radius and not
            //by being pushed: the radius is lifted until the lens stands clear of the rim, whatever the angle it looks from
            //(#878). KeepOutsideRim, behind it, is the net.
            if (_openBelow)
            {
                float pivotOffAxis = MathF.Sqrt(_pivot.X * _pivot.X + _pivot.Z * _pivot.Z);
                radius = SoftMax(radius, (RIM_REACH + pivotOffAxis) / MathF.Max(MathF.Cos(elevation), 0.3f), RIM_SOFTNESS);
            }

            float azimuth = _azimuth + _orbitRate * _elapsed;

            float horizontal = MathF.Cos(elevation) * radius;

            Vector3 lens = _pivot + new Vector3(
                MathF.Cos(azimuth) * horizontal,
                MathF.Sin(elevation) * radius,
                MathF.Sin(azimuth) * horizontal);

            Position = KeepBallsInSight(lens);

            //What it looks at drifts from the balls themselves towards the drain's axis as they go into it,
            //so the shot ends framed on the hole rather than on whichever ball was last to fall. Most of the
            //way while the subject is a scatter, and all of it while the drain holds the subject (#616): the
            //funnel is then what the shot is of, and the balls in it are inside its radius anyway.
            Vector3 axis = new(0f, _pivot.Y, 0f);
            Target = Vector3.Lerp(_pivot, axis, Smooth(drain) * MathHelper.Lerp(0.6f, 1f, _funnelShare));

            FieldOfView = _fov;
            Roll = _rollAmplitude * MathF.Sin(MathHelper.Pi * Saturate(_elapsed / ROLL_PERIOD));
        }

        /// <summary>
        /// The arc's progress, 0 to 1, for a subject <paramref name="depth"/> units below where it was released: a third at the
        /// island's top, two thirds at the throat's foot and all of it <see cref="BELOW_LEG"/> under that. A monotone cubic
        /// through those four points (the slope at each a harmonic mean of the two legs', so it is zero nowhere and never
        /// overshoots), which makes the rate continuous where the legs meet.
        /// </summary>
        private float SmoothProgress(float depth)
        {
            float d1 = MathF.Max(_startY - ISLAND_Y, 1f);
            float d2 = d1 + (ISLAND_Y - FUNNEL_BOTTOM_Y);
            float d3 = d2 + BELOW_LEG;

            if (depth >= d3) return 1f;

            float m0 = (1f / 3f) / d1, m1 = (1f / 3f) / (d2 - d1), m2 = (1f / 3f) / BELOW_LEG;
            float s1 = 2f * m0 * m1 / (m0 + m1), s2 = 2f * m1 * m2 / (m1 + m2);

            if (depth < d1) return Hermite(0f, 1f / 3f, m0, s1, depth / d1, d1);
            if (depth < d2) return Hermite(1f / 3f, 2f / 3f, s1, s2, (depth - d1) / (d2 - d1), d2 - d1);

            return Hermite(2f / 3f, 1f, s2, m2, (depth - d2) / BELOW_LEG, BELOW_LEG);
        }

        //A cubic Hermite segment of width `width`, from value a to b with slopes sa and sb (per unit of depth), at t in 0..1.
        private static float Hermite(float a, float b, float sa, float sb, float t, float width)
        {
            float t2 = t * t, t3 = t2 * t;

            return (2f * t3 - 3f * t2 + 1f) * a + (t3 - 2f * t2 + t) * sa * width
                + (-2f * t3 + 3f * t2) * b + (t3 - t2) * sb * width;
        }

        /// <summary>
        /// How hard <see cref="Frame"/> should pull the elevation towards the OUT leg's own diving angle
        /// because the subject is already under the sea (#193), 0 at or above the surface to 1 a full
        /// <see cref="SUBMERGE_PULL_DEPTH"/> under it. Off the sea this is always 0 — <see cref="_isSea"/>
        /// gates it, so the pull can never fire on a scene <see cref="_seaLevelY"/> means nothing for.
        /// <para>
        /// <b>Why pull the angle rather than clamp the lens Y directly.</b> A straight <c>MathF.Min</c> on
        /// the final position would crush the lens down to the surface while its X/Z stayed wherever the
        /// FALL leg's wide, distant radius put them — a grazing, far-off shot rather than a natural dive.
        /// Pulling the elevation instead keeps the orbit's own radius/azimuth geometry intact and simply
        /// asks the lens to look more steeply down (and, once close enough, more steeply UP through the
        /// hull it just crossed) — which is what a camera operator diving after a falling subject actually
        /// does, and what <see cref="KeepBallsInSight"/>'s own island logic already does for the same reason
        /// on the opaque side of this problem.
        /// </para>
        /// </summary>
        private float SubmergeElevationPull() =>
            _isSea ? Smoother(Saturate((_seaLevelY - _pivot.Y) / SUBMERGE_PULL_DEPTH)) : 0f;

        /// <summary>
        /// How hard <see cref="Frame"/> should pull the elevation towards the OUT leg's diving angle because the
        /// subject is under the island's top face in an open scene (#407), 0 at the face to 1 a full
        /// <see cref="STONE_DIVE_DEPTH"/> under it. Always 0 in a solid-terrain scene, whose shot climbs and
        /// looks down the throat and has no underside to dive for.
        /// </summary>
        private float StoneDivePull() =>
            !_openBelow || _crossedAt < 0f ? 0f : MathF.Max(
                Smoother(Saturate((ISLAND_Y - _pivot.Y) / STONE_DIVE_DEPTH)),
                Smoother(Saturate((_elapsed - _crossedAt) / STONE_DIVE_SECONDS)));

        //Whether the shot may go UNDER the island is SceneRenderer.OpenBelow since #75, and it is defined there
        //as the exact complement of IsSolidTerrainScene — the two hand-kept lists this file and the host each
        //carried are one decision now. The reasoning is the library's in full: the two cities continue
        //underneath the arena and fall away into a canyon, the sea is water the lens can dive into, and the
        //three sky-replacing scenes are nothing at all, so from below the balls read through the translucent
        //cone and then pour out of the hole; the solid-terrain scenes have the island's footprint cut out of a
        //flat clearing and a near-black pit shaft hugging the glass, so the ground is a lid and there is no
        //vantage under there that can see a ball. Those shots stay over the stone and look down through the
        //mouth, which is the only hole in it.

        /// <summary>
        /// Moves the lens off anything solid it would otherwise sit inside or try to look through. Every rule
        /// here serves one requirement — <b>the balls stay on screen for as much of the drop as possible</b> —
        /// and the only opaque thing between a lens and them is the island itself.
        /// </summary>
        private Vector3 KeepBallsInSight(Vector3 lens)
        {
            //A solid-terrain scene's lens stays above the stone: the island's top is an opaque annulus from the
            //mouth out to its rim, and from below it a shot looks at the underside of the platform with the drop
            //hidden behind it. An open scene's goes round the rim instead and has no such floor to be lifted by
            //(#878): a floor that applied only while the subject was above the top, and the rim's push only after it,
            //was a switch the lens crossed with its position jumping 9 units out of the way.
            if (!_openBelow) lens.Y = MathF.Max(lens.Y, ISLAND_Y + STONE_CLEARANCE);
            else lens = PushOutOfIsland(KeepOutsideRim(lens));

            //The mouth is the only hole in the stone, so a lens above it can see a ball below it only by
            //looking through that hole: the sight line has to cross the mouth plane inside the rim. The lens
            //lies in the cone that runs from the ball up through the rim, and clamping its horizontal reach
            //to satisfy that is what swings the shot overhead as the balls run deeper — the further down they
            //are, the narrower the set of places that can still see them. It is the whole of how a
            //solid-terrain scene is filmed.
            //
            //⚠ AND IT IS NOT APPLIED IN AN OPEN SCENE ANY MORE (#407). It used to be "a safety net in the
            //others", and in the others it was the fault the owner reported: a lens on its way UNDER the
            //island is above the top face for a few frames after the balls have gone below it, and in those
            //frames this cone dragged it in over the mouth — 27.7 units of reach down to 12.7 over eight
            //frames in the trace — and then, the moment the lens's own height dropped below the face, let go
            //of it in a single 16-unit jump back out. Too close and then a jump back, exactly as reported. An
            //open scene's lens is heading for the underside, where the balls show through the glass anyway,
            //so the honest answer for those few frames is to let the annulus hide them and get under it
            //fast (StoneDivePull), not to fit the lens through a hole it is about to leave behind.
            if (!_openBelow && lens.Y > ISLAND_Y && _pivot.Y < ISLAND_Y - 0.5f)
            {
                float depth = ISLAND_Y - _pivot.Y;
                float height = lens.Y - _pivot.Y;

                float maxReach = (ArenaIsland.FUNNEL_TOP_RADIUS - MOUTH_MARGIN) * height / depth;

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
        /// The net behind the open scenes' continuous way past the platform (#407): while the lens is within the
        /// drum's own height band its horizontal reach is held past the rim (<see cref="RIM_APPROACH"/>,
        /// <see cref="RIM_MARGIN"/>), the floor eased in as it comes down on the band from above and out as it
        /// leaves it below. The reach is measured from the drain's axis, the drum's own centre, as the mouth cone
        /// measures it.
        /// <para>
        /// Since #878 <see cref="Frame"/> lifts the radius so the lens's reach is already past the rim, and this
        /// has nothing to move: it stood in for that, and it is on always, never switched on by where the subject
        /// is, because the switch was the hop (a 9-unit shove sideways in two frames as the subject crossed the
        /// island's top, with the lens inside the rim's reach). It is also what <see cref="PushOutOfIsland"/>
        /// used to do in one frame - a 5.5-unit drop between two frames - and the push stays behind this as the
        /// last resort.
        /// </para>
        /// </summary>
        private static Vector3 KeepOutsideRim(Vector3 lens)
        {
            float top = ISLAND_Y + STONE_CLEARANCE;
            float bottom = ISLAND_Y - ArenaIsland.EDGE_HEIGHT - STONE_CLEARANCE;

            float outside = lens.Y > top ? lens.Y - top : lens.Y < bottom ? bottom - lens.Y : 0f;
            float weight = Smooth(1f - Saturate(outside / RIM_APPROACH));

            if (weight <= 0f) return lens;

            const float floor = RIM_REACH;

            Vector2 flat = new(lens.X, lens.Z);
            float reach = flat.Length();

            //The weight scales the MOVE and not the floor. Scaling the floor put the whole push into the few
            //frames where the scaled floor crossed the lens's own reach (a lens 5 units inside the rim was
            //pushed out over two frames and let back in over two), which is a smaller version of the snap this
            //replaces; scaling the move spreads it over the whole approach band, whatever the lens's own reach.
            if (reach < floor && reach > 1e-3f)
            {
                flat *= MathHelper.Lerp(1f, floor / reach, weight);
                lens.X = flat.X;
                lens.Z = flat.Y;
            }

            return lens;
        }

        /// <summary>
        /// The island as the solid it is — a drum of stone from the top face down to the underside. A lens
        /// inside it sees the inside of the platform and nothing else, which is what a shot that swung low
        /// used to drive straight through. Pushed out through the face on the side the balls are on, so the
        /// escape never puts the platform between the two. Since #407 it is the last resort behind
        /// <see cref="KeepOutsideRim"/>, which keeps the lens out of the drum continuously.
        /// </summary>
        private static Vector3 PushOutOfIsland(Vector3 lens)
        {
            float top = ISLAND_Y + STONE_CLEARANCE;
            float bottom = ISLAND_Y - ArenaIsland.EDGE_HEIGHT - STONE_CLEARANCE;

            if (lens.Y >= top || lens.Y <= bottom) return lens;
            if (new Vector2(lens.X, lens.Z).Length() > ArenaIsland.RADIUS + STONE_CLEARANCE) return lens;

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

        //The larger of two values with its corner rounded (a smooth maximum): never below either, the same as it where they are
        //far apart, and with a slope that changes over a stretch instead of in one frame.
        private static float SoftMax(float a, float b, float softness) =>
            0.5f * (a + b + MathF.Sqrt((a - b) * (a - b) + softness * softness));

        private static float Smoother(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
