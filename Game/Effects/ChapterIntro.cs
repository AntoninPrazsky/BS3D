using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
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
    /// the identical reason: this class owns no camera and no input, it is handed the level's own solved
    /// figures once at <see cref="Begin"/> and answers a pose every frame, and the caller Lerps between it
    /// and the ordinary gameplay pose by <see cref="Blend"/> — the one-reversible-scalar idiom precise aim
    /// and the drop cinematic both use, so an interrupted or skipped intro can never leave the camera
    /// anywhere the game did not put it.
    /// </para>
    /// <para>
    /// <b>Since the owner's ruling on the first cut, the tour is of the SCENE, not of the map.</b> The first
    /// version was one wide-to-close orbit around the hanging field — one subject, one look-at, the whole
    /// flight spent staring at the cluster — and the ruling was that a new chapter is a new <i>place</i>:
    /// the camera should fly through the environment, take a shot or two at what the new scene is, and only
    /// then come to the map and the gameplay view. The shot is therefore four key-framed poses — position
    /// AND look-at both, swept on a Catmull-Rom spline so the velocity is continuous through the keys —
    /// and the last key is the ordinary gameplay pose verbatim, so the hand of control back is a nudge
    /// between two identical poses and never a visible glide across the arena.
    /// </para>
    /// <para>
    /// <b>What is deliberately not here, unlike the drop cinematic.</b> There is no subject to follow — the
    /// cluster is hanging, not falling — so there is no time scale: the world runs at its ordinary speed
    /// throughout, because nothing about a fresh level's rest pose needs slowing down to be read. And the
    /// wide legs scale off the level's own solved stand-off, so they clear the arena the same way the
    /// ordinary gameplay camera already does, without needing that reasoning worked out a second time here.
    /// </para>
    /// </summary>
    internal sealed class ChapterIntro
    {
        public bool Engaged => _running || _blend > 0f;

        public float Blend => _blend;

        public Vector3 Position { get; private set; }

        public Vector3 Target { get; private set; }

        public float FieldOfView { get; private set; }

        //How long the tour runs before it ends on its own. A tour of the PLACE, not just the map: two legs
        //of environment, one of the map, one of arrival — long enough to read as a flight over a new scene,
        //short enough that a player eager to play is not held from a new chapter's very first shot for much
        //longer than the drop cinematic ever holds them from the next. Skippable regardless.
        private const float DURATION_SECONDS = 7f;

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

        //The shot, rolled once in Begin, so consecutive chapter openings are not the same flight at a
        //different scale: four keys of position and look-at, and the wide legs' field of view. The keys are
        //named by their SUBJECT — environment, arena, map, arrival — because that is the order the owner
        //asked the tour to take.
        private Vector3[] _positions = new Vector3[4];
        private Vector3[] _targets = new Vector3[4];
        private float _fovWide, _fovGame;

        /// <summary>
        /// Takes the camera. <paramref name="centre"/> is the point the ordinary gameplay camera already
        /// looks at — the field's own aim height on the gun's orbit axis. <paramref name="gameDistance"/> and
        /// <paramref name="gameFov"/> are the level's own solved stand-off and field of view
        /// (<c>GameCameraFit.Solve</c>'s), which the wide legs scale off so a tall level's intro stands back
        /// further exactly as its ordinary camera already does. And <paramref name="gamePosition"/> /
        /// <paramref name="gameTarget"/> are that ordinary pose verbatim, because the tour's LAST key is it:
        /// the flight ends where the gameplay camera stands, so the blend-out that hands control back is a
        /// nudge between two identical poses and never a visible glide.
        /// </summary>
        public void Begin(Vector3 centre, float gameDistance, float gameFov,
            Vector3 gamePosition, Vector3 gameTarget, Random random)
        {
            _running = true;
            _elapsed = 0f;
            _centre = centre;

            //The island's own figures, for the environment legs: the rim the opening look crosses, and the
            //top the arena's bowl sits under. Constants of the setting, not of the level.
            float islandRadius = ArenaIsland.RADIUS;
            float islandTopY = ArenaIsland.TOP_Y;

            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;

            //How far one leg sweeps round the arena, and in which direction — rolled, so the same chapter
            //opening twice in two runs of the program is still two different flights.
            float sweep = MathHelper.ToRadians(Lerp(random, 105f, 155f)) * (random.Next(2) == 0 ? 1f : -1f);

            Vector3 At(float azimuth, float elevation, float radius) => centre + new Vector3(
                MathF.Cos(azimuth) * MathF.Cos(elevation) * radius,
                MathF.Sin(elevation) * radius,
                MathF.Sin(azimuth) * MathF.Cos(elevation) * radius);

            //KEY 0, THE ENVIRONMENT: wide and LOW — near the height the scene itself reads at, horizon in
            //frame — and looking across the island's far rim into the land behind it, so the island and the
            //cluster enter the frame from the near side rather than filling it. This is the leg the owner's
            //ruling is about: the first thing a new chapter shows is the place.
            float elev0 = _elev0 = MathHelper.ToRadians(Lerp(random, 8f, 16f));
            _positions[0] = At(bearing, elev0, gameDistance * Lerp(random, 1.9f, 2.4f));
            _targets[0] = centre + new Vector3(
                -MathF.Cos(bearing) * islandRadius * Lerp(random, 0.8f, 1.0f),
                islandTopY + 2f - centre.Y,
                -MathF.Sin(bearing) * islandRadius * Lerp(random, 0.8f, 1.0f));

            //KEY 1, THE ARENA: a good sweep round, nearer and higher — the bowl, the drain and the island's
            //coping in one frame, the hanging field above them.
            float elev1 = MathHelper.ToRadians(Lerp(random, 22f, 32f));
            _positions[1] = At(bearing + sweep, elev1, gameDistance * Lerp(random, 1.5f, 1.8f));
            _targets[1] = new Vector3(centre.X, islandTopY + 3f, centre.Z);

            //KEY 2, THE MAP: on round again and up — the one proper look at the cluster, from high enough to
            //show the glass it hangs from.
            float elev2 = _elev2 = MathHelper.ToRadians(Lerp(random, 38f, 48f));
            _positions[2] = At(bearing + 2f * sweep, elev2, gameDistance * Lerp(random, 1.25f, 1.45f));
            _targets[2] = centre;

            //KEY 3, THE ARRIVAL: the gameplay pose itself, verbatim.
            _positions[3] = gamePosition;
            _targets[3] = gameTarget;

            //A touch wider than the gameplay frame at the start — an establishing shot reads the place, not
            //the subject close up — easing back to the ordinary frame on the way in, so the arrival key's
            //own frame is exactly what the player is handed.
            _fovWide = gameFov * Lerp(random, 0.96f, 1.10f);
            _fovGame = gameFov;
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
        /// out is the ordinary one — a skip is not a cut, it just stops waiting for the tour to finish.
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
            "env {0:F0} out at {1:F0}deg -> arena -> map {2:F0} out at {3:F0}deg -> game pose, {4:F1}s",
            (_positions[0] - _centre).Length(), MathHelper.ToDegrees(_elev0),
            (_positions[2] - _centre).Length(), MathHelper.ToDegrees(_elev2),
            DURATION_SECONDS);

        private Vector3 _centre;
        private float _elev0, _elev2;

        private void End() => _running = false;

        /// <summary>
        /// Builds this frame's pose: the four keys swept on a Catmull-Rom spline — positions and look-ats
        /// both, the segment picked by the clock's place in the whole and the remainder eased within it — so
        /// the flight's velocity is continuous through every key. The clock itself is smoothstepped over the
        /// whole tour, which is what leaves and arrives at rest.
        /// </summary>
        private void Frame()
        {
            float t = Smooth(Saturate(_elapsed / DURATION_SECONDS));

            Position = Spline(_positions, t);
            Target = Spline(_targets, t);
            FieldOfView = MathHelper.Lerp(_fovWide, _fovGame, t);
        }

        /// <summary>
        /// Catmull-Rom through four keys: three segments, the virtual endpoints beyond the ends clamped to
        /// the real ones (the standard treatment — the spline leaves key 0 along its own tangent and arrives
        /// at key 3 along its). <see cref="Vector3.CatmullRom"/> does the segment; all this does is pick it.
        /// </summary>
        private static Vector3 Spline(Vector3[] keys, float t)
        {
            float segment = Saturate(t) * (keys.Length - 1);
            int i = Math.Min((int)segment, keys.Length - 2);

            Vector3 p0 = keys[Math.Max(i - 1, 0)];
            Vector3 p3 = keys[Math.Min(i + 2, keys.Length - 1)];

            return Vector3.CatmullRom(p0, keys[i], keys[i + 1], p3, segment - i);
        }

        private static float Lerp(Random random, float from, float to) => from + (float)random.NextDouble() * (to - from);

        private static float Saturate(float value) => MathHelper.Clamp(value, 0f, 1f);

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
