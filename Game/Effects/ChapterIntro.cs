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
    /// <b>And since #289 the scene says WHAT the first two keys are of.</b> The ruling above was answered by
    /// pointing the opening legs across the island's far rim on a rolled bearing — which is a fair shot of a
    /// sea and a poor one of a volcano, because it framed whatever happened to be over there rather than
    /// whatever the scene is FOR. Each backdrop now names a subject and how far out and how high to stand
    /// for it (<see cref="SceneRenderer.TryGetViewpoint"/>), and the tour opens on that and sweeps round it.
    /// The roll did not go away — the scenes that are the same in every direction build their point out of
    /// it, so a savanna still names its campfire while a meadow still comes in from anywhere.
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
        //
        //It was 7 s until #289, and the owner's word for what they wanted instead was "slower". The first
        //two legs now frame something the SCENE itself named rather than a rolled bearing, and a subject
        //worth pointing at is worth staying on. The whole shot is still a fraction of the time the level it
        //opens takes to play, and it plays eleven times in a campaign.
        private const float DURATION_SECONDS = 9.5f;

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
        /// <para>
        /// <paramref name="sceneViewpoint"/> is asked, with the bearing this shot rolled, what the live scene
        /// would have a camera look at (#289) — a function rather than a value because the roll happens in
        /// here and half the scenes answer out of it. Null, or a null answer, falls back to the pre-#289
        /// sweep across the island's rim.
        /// </para>
        /// </summary>
        public void Begin(Vector3 centre, float gameDistance, float gameFov,
            Vector3 gamePosition, Vector3 gameTarget, Func<float, SceneViewpoint?> sceneViewpoint, Random random)
        {
            _running = true;
            _elapsed = 0f;
            _centre = centre;

            //How near the arena the flight is ever allowed to pass — see the clamp in Frame. Just inside the
            //gameplay stand-off, because the LAST key is the gameplay pose and must not be pushed anywhere.
            _minRadius = gameDistance * 0.92f;

            //The island's own figures, for the environment legs: the rim the opening look crosses, and the
            //top the arena's bowl sits under. Constants of the setting, not of the level.
            float islandRadius = ArenaIsland.RADIUS;
            float islandTopY = ArenaIsland.TOP_Y;

            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;

            //How far one leg sweeps round the arena, and in which direction — rolled, so the same chapter
            //opening twice in two runs of the program is still two different flights.
            float sweep = MathHelper.ToRadians(Lerp(random, 105f, 155f)) * (random.Next(2) == 0 ? 1f : -1f);

            //Asked AFTER the roll and given it, because half the scenes build their viewpoint out of it —
            //see SceneRenderer.TryGetViewpoint. Null when nothing was handed in (a caller with no scene
            //renderer at all) or when the scene has no viewpoint of its own.
            SceneViewpoint? viewpoint = sceneViewpoint?.Invoke(bearing);

            Vector3 At(float azimuth, float elevation, float radius) => centre + new Vector3(
                MathF.Cos(azimuth) * MathF.Cos(elevation) * radius,
                MathF.Sin(elevation) * radius,
                MathF.Sin(azimuth) * MathF.Cos(elevation) * radius);

            //THE FIRST TWO KEYS ARE THE SCENE'S OWN SINCE #289, when it has a viewpoint to give. They were a
            //rolled bearing and a look across the island's far rim whatever the backdrop was — a fair shot
            //of a sea and a poor one of a volcano, whose cone was as likely to be behind the camera as in
            //front of it. SceneRenderer.TryGetViewpoint names the subject and says how far out
            //and how high to stand for it. The roll is still here and still does the work: the scenes that
            //are the same in every direction build their viewpoint FROM the bearing handed in.
            if (viewpoint is SceneViewpoint view)
            {
                _subject = view.Name;

                //The subject's own bearing from the arena, which is what the offset is measured against — a
                //landmark scene ignores the roll outright, so where to stand has to be found from where the
                //landmark IS rather than from where the roll happened to point.
                Vector3 toSubject = view.LookAt - centre;
                float subjectBearing = MathF.Atan2(toSubject.Z, toSubject.X);

                float stand = subjectBearing + MathHelper.ToRadians(view.BearingOffsetDegrees);
                float elevScene = _elev0 = MathHelper.ToRadians(view.ElevationDegrees);

                _positions[0] = At(stand, elevScene, gameDistance * view.DistanceScale);
                _targets[0] = view.LookAt;

                //KEY 1 IS THE SAME SUBJECT FROM FURTHER ROUND, and a move rather than a second still: a bit
                //over a third of the leg's sweep, nearer and a little higher, with the look-at eased a third
                //of the way back towards the arena. So the island and its cluster enter the frame from the
                //side while the scene is still the subject, instead of the shot cutting away to them.
                _positions[1] = At(stand + sweep * 0.38f, elevScene + MathHelper.ToRadians(9f),
                    gameDistance * view.DistanceScale * 0.86f);
                _targets[1] = Vector3.Lerp(view.LookAt, centre, 0.34f);
            }
            else
            {
                //THE PRE-#289 SHOT, kept for a scene added without a viewpoint: wide and LOW, looking across
                //the island's far rim into whatever is behind it, then a sweep round onto the arena. It
                //frames the place in the only way that needs nothing said about the place — which is exactly
                //why it was worth replacing, and exactly why it is a safe thing to fall back to.
                _subject = "the rim";

                float elev0 = _elev0 = MathHelper.ToRadians(Lerp(random, 8f, 16f));
                _positions[0] = At(bearing, elev0, gameDistance * Lerp(random, 1.9f, 2.4f));
                _targets[0] = centre + new Vector3(
                    -MathF.Cos(bearing) * islandRadius * Lerp(random, 0.8f, 1.0f),
                    islandTopY + 2f - centre.Y,
                    -MathF.Sin(bearing) * islandRadius * Lerp(random, 0.8f, 1.0f));

                float elev1 = MathHelper.ToRadians(Lerp(random, 22f, 32f));
                _positions[1] = At(bearing + sweep, elev1, gameDistance * Lerp(random, 1.5f, 1.8f));
                _targets[1] = new Vector3(centre.X, islandTopY + 3f, centre.Z);
            }

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
            "'{0}' {1:F0} out at {2:F0}deg -> in -> map {3:F0} out at {4:F0}deg -> game pose, {5:F1}s",
            _subject, (_positions[0] - _centre).Length(), MathHelper.ToDegrees(_elev0),
            (_positions[2] - _centre).Length(), MathHelper.ToDegrees(_elev2),
            DURATION_SECONDS);

        private Vector3 _centre;
        private float _elev0, _elev2, _minRadius;

        //What the first two legs are of, for the one log line. Named rather than derived, so a shot that
        //fell back to the pre-#289 sweep says so in the record instead of reading as a scene's own choice.
        private string _subject = "the rim";

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

            //⚠ NEVER THROUGH THE CLUSTER, and this is a floor rather than a taste. Every key stands well
            //outside the hanging field, but a Catmull-Rom's tangent at a key is the chord between its
            //neighbours, so a leg running from a far key to a near one bows INWARD between the two. #289's
            //opening legs stand as far out as the SCENE asks rather than at a fixed multiple of the level's
            //own stand-off, which made that bow deep enough to fly the lens through the balls — photographed
            //on the volcano's opening as a frame of nothing but ball at arm's length. Floored here rather
            //than by pulling the keys in, because where to stand is the part a scene is allowed to choose;
            //and it cannot disturb the arrival, since key 3 is the gameplay pose and stands at exactly the
            //stand-off this is a fraction of.
            Vector3 away = Position - _centre;
            float radius = away.Length();

            if (radius > 1e-4f && radius < _minRadius) Position = _centre + away * (_minRadius / radius);
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
