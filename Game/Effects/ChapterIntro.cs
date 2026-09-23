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
    /// <b>And since #488 a scene may open with a PROLOGUE of cut-together shots</b> (<see cref="IntroShot"/>) —
    /// the cities, whose streets, plazas and canyons between towers are ninety units under the island and
    /// somewhere no spline round the arena can reach without flying through the city on its way; and since
    /// #530 the volcano, whose crater is a bowl on a summit the tour's stand outside the cone could only ever
    /// look at, never into (<see cref="VolcanoIntroShots"/>); and since #531 the aurora, whose wood stands
    /// outside the clearing the spline circles (<see cref="AuroraIntroShots"/>). The prologue
    /// plays first, shot by shot with a hard cut between each, then cuts to the tour. A cut is the one thing
    /// the blend cannot do and the one thing this needed: so an intro with a prologue is taken with a cut
    /// (the blend jumps to 1) and a skip during it hands back with one (the blend drops to 0), because a
    /// blend between the street and the gameplay pose is a straight line through the towers between them.
    /// After a prologue the tour flies only its LAST leg, the map and the arrival: the prologue has shown the
    /// place, and in a city the tour's two scene stands are points on a circle round the arena that runs
    /// through the towers (#433's own finding) — photographed on the neon city as a facade at arm's length
    /// straight after the cut. The map key stands inside the clearing the island is built in.
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

        //The prologue (#488): shots cut together ahead of the tour, or empty. Its length is added to the
        //tour's own, so the tour itself is the same flight with or without one.
        private IntroShot[] _prologue = Array.Empty<IntroShot>();
        private float _prologueSeconds;

        //How long the tour's last leg runs on its own after a prologue — the map and the arrival (see the
        //class doc). Long enough for one proper look at the cluster before the gun is handed over.
        private const float ARRIVAL_SECONDS = 4.5f;

        //The tour flown this time: all four keys, or after a prologue only the last two (their own arrays,
        //filled in Begin, so Frame walks whichever it is handed and allocates nothing).
        private readonly Vector3[] _tailPolar = new Vector3[2];
        private readonly Vector3[] _tailTargets = new Vector3[2];
        private float TourSeconds => _prologue.Length > 0 ? ARRIVAL_SECONDS : DURATION_SECONDS;

        //The whole intro, prologue and tour.
        private float TotalSeconds => _prologueSeconds + TourSeconds;

        //The shot, rolled once in Begin, so consecutive chapter openings are not the same flight at a
        //different scale: four keys of position and look-at, and the wide legs' field of view. The keys are
        //named by their SUBJECT — environment, arena, map, arrival — because that is the order the owner
        //asked the tour to take.
        //
        //THE POSITIONS ARE POLAR ABOUT THE CENTRE SINCE #409 — azimuth, elevation and radius, as one Vector3
        //per key — and the look-ats stay Cartesian. A Cartesian spline through four stands around an arena
        //bows INWARD between them (a Catmull-Rom's tangent at a key is the chord between its neighbours), and
        //when two neighbouring stands were near-opposite the chord ran through the arena's axis: the radius
        //floor then pushed the lens straight UP over the island, and it rode the floor's sphere looking down
        //into the funnel. Traced on Comet, the Nebula's opener: 81 degrees of elevation on the map leg in one
        //roll, and the arrival leg riding the floor for a quarter of the tour at 46 degrees in another — the
        //owner's "ends up looking down into the island from an odd angle". Interpolating the stands
        //themselves keeps every sample on a smooth orbit at the interpolated radius, and the azimuths are laid
        //along ONE continuous turn from the scene's stand to the gun (see Begin), so no leg ever crosses the
        //axis and the floor never has anything to do.
        private Vector3[] _polar = new Vector3[4];
        private Vector3[] _targets = new Vector3[4];
        private float _fovWide, _fovGame;

        //How much of the turn left after the arena key the map key takes; the rest is the arrival's.
        private const float MAP_KEY_TURN_SHARE = 0.6f;

        //The band the turn from the arena key to the gun should fall in, for a stand the scene fixed (see
        //ChooseTurn): under the floor the map and arrival legs would be a near-stand-still, over the ceiling
        //most of a lap in the tour's last two thirds — 75 to 200 degrees.
        private const float MIN_REMAINING_TURN = MathHelper.Pi * 0.42f;
        private const float MAX_REMAINING_TURN = MathHelper.Pi * 1.11f;

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
        /// <para>
        /// <paramref name="prologue"/> is the scene's own shots to cut together ahead of the tour (#488), or
        /// null for a scene that has none — every scene but the two cities, the volcano (#530) and the aurora
        /// (#531).
        /// </para>
        /// </summary>
        public void Begin(Vector3 centre, float gameDistance, float gameFov,
            Vector3 gamePosition, Vector3 gameTarget, Func<float, SceneViewpoint?> sceneViewpoint, Random random,
            IntroShot[] prologue = null)
        {
            _running = true;
            _elapsed = 0f;
            _centre = centre;
            _flooredFrames = 0;
            _deepestFloor = 1f;

            _prologue = prologue ?? Array.Empty<IntroShot>();
            _prologueSeconds = 0f;
            foreach (IntroShot shot in _prologue) _prologueSeconds += shot.Seconds;

            //Taken with a CUT when it opens on a shot of its own: the first shot is a street under the island,
            //and a blend in from the gameplay pose would glide there through the towers (see the class doc).
            if (_prologue.Length > 0) _blend = 1f;

            //How near the arena the flight is ever allowed to pass — see the clamp in Frame. Just inside the
            //gameplay stand-off, because the LAST key is the gameplay pose and must not be pushed anywhere.
            _minRadius = gameDistance * 0.92f;

            //The island's own figures, for the environment legs: the rim the opening look crosses, and the
            //top the arena's bowl sits under. Constants of the setting, not of the level.
            float islandRadius = ArenaIsland.RADIUS;
            float islandTopY = ArenaIsland.TOP_Y;

            //Where the gun stands, in the tour's own terms: the last key IS this pose, and every azimuth below
            //is laid out relative to it (#409).
            Vector3 toGun = gamePosition - centre;
            float gunAzimuth = MathF.Atan2(toGun.Z, toGun.X);
            float gunElevation = MathF.Atan2(toGun.Y, MathF.Sqrt(toGun.X * toGun.X + toGun.Z * toGun.Z));
            float gunRadius = toGun.Length();

            //How far one leg sweeps round the arena, and which way — rolled, so the same chapter opening twice
            //in two runs of the program is still two different flights. Both are only PROPOSALS here: the tour
            //has to end at the gameplay pose, and which way round it turns to get there is settled below with
            //the gun in view (#409), because a turn that ignored the gun was what lunged across the axis.
            float sweep = MathHelper.ToRadians(Lerp(random, 105f, 155f));
            int rolledSign = random.Next(2) == 0 ? 1 : -1;

            //THE STAND IS PLACED OFF THE GUN for a scene that is the same in every direction (#409): the tour
            //turns the sweep from the stand to the arena key and then a rolled remainder more to the gun, all
            //one way round, so the whole flight is one turn of about 195 to 325 degrees that ends on the gun.
            //It was a bare roll, and a bare roll can land the stand anywhere against the gun — one turn from
            //it too short to carry the map and the arrival, the other most of a lap. A landmark scene ignores
            //this bearing outright and has its stand chosen for it below.
            float remainingRolled = MathHelper.ToRadians(Lerp(random, 90f, 170f));
            float bearing = gunAzimuth - rolledSign * (sweep + remainingRolled);

            //Asked AFTER the roll and given it, because half the scenes build their viewpoint out of it —
            //see SceneRenderer.TryGetViewpoint. Null when nothing was handed in (a caller with no scene
            //renderer at all) or when the scene has no viewpoint of its own.
            SceneViewpoint? viewpoint = sceneViewpoint?.Invoke(bearing);

            //The first two keys as (azimuth, elevation, radius), before the direction of travel is known: the
            //arena key's azimuth is the stand plus a signed share of the sweep, so it is finished below.
            float azimuth0, elevation0, radius0, arenaShare, elevation1, radius1;

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

                azimuth0 = subjectBearing + MathHelper.ToRadians(view.BearingOffsetDegrees);
                elevation0 = _elev0 = MathHelper.ToRadians(view.ElevationDegrees);
                radius0 = gameDistance * view.DistanceScale;
                _targets[0] = view.LookAt;

                //KEY 1 IS THE SAME SUBJECT FROM FURTHER ROUND, and a move rather than a second still: a bit
                //over a third of the leg's sweep, nearer and a little higher, with the look-at eased a third
                //of the way back towards the arena. So the island and its cluster enter the frame from the
                //side while the scene is still the subject, instead of the shot cutting away to them.
                arenaShare = 0.38f;
                elevation1 = elevation0 + MathHelper.ToRadians(9f);
                radius1 = radius0 * 0.86f;
                _targets[1] = Vector3.Lerp(view.LookAt, centre, 0.34f);
            }
            else
            {
                //THE PRE-#289 SHOT, kept for a scene added without a viewpoint: wide and LOW, looking across
                //the island's far rim into whatever is behind it, then a sweep round onto the arena. It
                //frames the place in the only way that needs nothing said about the place — which is exactly
                //why it was worth replacing, and exactly why it is a safe thing to fall back to.
                _subject = "the rim";

                azimuth0 = bearing;
                elevation0 = _elev0 = MathHelper.ToRadians(Lerp(random, 8f, 16f));
                radius0 = gameDistance * Lerp(random, 1.9f, 2.4f);
                _targets[0] = centre + new Vector3(
                    -MathF.Cos(bearing) * islandRadius * Lerp(random, 0.8f, 1.0f),
                    islandTopY + 2f - centre.Y,
                    -MathF.Sin(bearing) * islandRadius * Lerp(random, 0.8f, 1.0f));

                arenaShare = 1f;
                elevation1 = MathHelper.ToRadians(Lerp(random, 22f, 32f));
                radius1 = gameDistance * Lerp(random, 1.5f, 1.8f);
                _targets[1] = new Vector3(centre.X, islandTopY + 3f, centre.Z);
            }

            //WHICH WAY ROUND (#409). The arena key sits a signed share of the sweep from the stand, and from
            //there the tour still has to reach the gun. A scene with no landmark had its stand placed so that
            //carrying on the rolled way round gets there in a turn worth having; a landmark's stand is where
            //the landmark put it, so the two directions are weighed against the gun (ChooseTurn) — and the
            //tour may swing one way to the arena key and come BACK the other to the gun, which for a stand
            //close to the gun's own bearing is the difference between a look around and a lap.
            int arenaSign, arrivalSign;
            float remaining;

            if (viewpoint.HasValue)
                ChooseTurn(azimuth0, arenaShare * sweep, gunAzimuth, rolledSign, out arenaSign, out arrivalSign, out remaining);
            else
            {
                arenaSign = arrivalSign = rolledSign;
                remaining = RemainingTurn(azimuth0 + arenaSign * sweep, gunAzimuth, arrivalSign);
            }

            float azimuth1 = azimuth0 + arenaSign * arenaShare * sweep;

            _polar[0] = new Vector3(azimuth0, elevation0, radius0);
            _polar[1] = new Vector3(azimuth1, elevation1, radius1);

            //KEY 2, THE MAP: on round again and up — the one proper look at the cluster, from high enough to
            //show the glass it hangs from. Most of the way round to the gun, so the arrival is the shorter leg.
            float elev2 = _elev2 = MathHelper.ToRadians(Lerp(random, 38f, 48f));
            _polar[2] = new Vector3(azimuth1 + arrivalSign * MAP_KEY_TURN_SHARE * remaining, elev2,
                gameDistance * Lerp(random, 1.25f, 1.45f));
            _targets[2] = centre;

            //KEY 3, THE ARRIVAL: the gameplay pose itself — its own azimuth, continued rather than wrapped, so
            //the spline turns through the remaining arc instead of unwinding the whole tour.
            _polar[3] = new Vector3(azimuth1 + arrivalSign * remaining, gunElevation, gunRadius);
            _targets[3] = gameTarget;

            //A touch wider than the gameplay frame at the start — an establishing shot reads the place, not
            //the subject close up — easing back to the ordinary frame on the way in, so the arrival key's
            //own frame is exactly what the player is handed.
            _fovWide = gameFov * Lerp(random, 0.96f, 1.10f);
            _fovGame = gameFov;

            _tailPolar[0] = _polar[2];
            _tailPolar[1] = _polar[3];
            _tailTargets[0] = _targets[2];
            _tailTargets[1] = _targets[3];
        }

        /// <summary>One frame. Call every frame regardless of <see cref="Engaged"/>; a no-op once it is not.</summary>
        public void Update(float elapsed)
        {
            if (_running)
            {
                _elapsed += elapsed;

                if (_elapsed >= TotalSeconds) End();
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

            //Out of a prologue shot it is a CUT back to the gun, not the ordinary ease: the ease is a straight
            //line from the lens to the gameplay pose, and from a street ninety units under the island that line
            //runs through the towers (see the class doc). From the tour it eases as it always has.
            if (_elapsed < _prologueSeconds) _blend = 0f;

            End();
            return true;
        }

        /// <summary>Drops everything, for a session being torn down under it.</summary>
        public void Reset()
        {
            _running = false;
            _blend = 0f;
            _elapsed = 0f;
            _prologue = Array.Empty<IntroShot>();
            _prologueSeconds = 0f;
        }

        /// <summary>What the roll picked, for the one log line the trigger writes.</summary>
        //ASCII only and invariant, for the same reason DropCinematic.Describe is: a console whose code page
        //mangles a degree sign, and a figure two machines might compare.
        public string Describe() => _prologue.Length > 0
            ? DescribePrologue() + string.Format(CultureInfo.InvariantCulture,
                "map {0:F0} out at {1:F0}deg -> game pose, {2:F1}s; {3:F1}s in all",
                _polar[2].Z, MathHelper.ToDegrees(_elev2), ARRIVAL_SECONDS, TotalSeconds)
            : string.Format(CultureInfo.InvariantCulture,
            "'{0}' {1:F0} out at {2:F0}deg -> in -> map {3:F0} out at {4:F0}deg -> game pose, {5:F1}s, turning {6:F0}deg",
            _subject, _polar[0].Z, MathHelper.ToDegrees(_elev0),
            _polar[2].Z, MathHelper.ToDegrees(_elev2),
            DURATION_SECONDS, MathHelper.ToDegrees(_polar[3].X - _polar[0].X));

        //"cut 'the street' 3.6s | 'the swing' 3.0s | cut -> " ahead of the tour's own description, or nothing.
        private string DescribePrologue()
        {
            if (_prologue.Length == 0) return string.Empty;

            var text = new System.Text.StringBuilder("cut ");
            for (int i = 0; i < _prologue.Length; i++)
                text.Append(i == 0 ? "" : " | ").Append('\'').Append(_prologue[i].Name).Append('\'').Append(' ')
                    .Append(_prologue[i].Seconds.ToString("F1", CultureInfo.InvariantCulture)).Append('s');

            return text.Append(" | cut -> ").ToString();
        }

        private Vector3 _centre;
        private float _elev0, _elev2, _minRadius;

        //The floor's own record (#519): how many frames of the last flight it pushed out, and how far under it
        //the deepest of them came, as a fraction of the floor. Zero in every flight since #409's polar sweep,
        //and End says so if that ever stops being true.
        private int _flooredFrames;
        private float _deepestFloor = 1f;

        //What the first two legs are of, for the one log line. Named rather than derived, so a shot that
        //fell back to the pre-#289 sweep says so in the record instead of reading as a scene's own choice.
        private string _subject = "the rim";

        //One line, only when the floor did work — a guard that fires silently is a guard nobody knows about
        //(#519). ASCII and invariant like Describe. Every other intro ends without a word, as it always has.
        private void End()
        {
            _running = false;

            if (_flooredFrames > 0)
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[intro] WARNING radius floor engaged on {0} frame(s), deepest {1:F3} of the floor - the polar sweep should never reach it (#519)",
                    _flooredFrames, _deepestFloor));
        }

        /// <summary>
        /// Builds this frame's pose: the four keys swept on a Catmull-Rom spline — positions and look-ats
        /// both, the segment picked by the clock's place in the whole and the remainder eased within it — so
        /// the flight's velocity is continuous through every key. The clock itself is smoothstepped over the
        /// whole tour, which is what leaves and arrives at rest.
        /// </summary>
        private void Frame()
        {
            //A prologue shot, while one is running: its own pose, and the next shot's first frame is a cut.
            if (_elapsed < _prologueSeconds)
            {
                float start = 0f;

                foreach (IntroShot shot in _prologue)
                {
                    if (_elapsed < start + shot.Seconds)
                    {
                        shot.Pose((_elapsed - start) / shot.Seconds, out Vector3 position, out Vector3 target);
                        Position = position;
                        Target = target;
                        FieldOfView = shot.FieldOfView;
                        return;
                    }

                    start += shot.Seconds;
                }
            }

            float t = Smooth(Saturate((_elapsed - _prologueSeconds) / TourSeconds));
            bool tail = _prologue.Length > 0;

            //The stands are swept in polar terms (#409): a sample is a point on an orbit at the interpolated
            //radius, so between two stands the lens goes ROUND the arena and never through it, whatever the
            //angle between them. The look-at stays a Cartesian sweep, which for points far outside the arena
            //is exactly what it should be.
            Vector3 polar = Spline(tail ? _tailPolar : _polar, t);

            Position = _centre + Orbit(polar.X, polar.Y, polar.Z);
            Target = Spline(tail ? _tailTargets : _targets, t);
            FieldOfView = MathHelper.Lerp(_fovWide, _fovGame, t);

            //⚠ NEVER THROUGH THE CLUSTER, and this is a floor rather than a taste. It was load-bearing while
            //the stands were splined in Cartesian terms: a Catmull-Rom's tangent at a key is the chord between
            //its neighbours, so a leg running from a far key to a near one bowed INWARD between the two, and
            //#289's opening legs stand as far out as the SCENE asks, which made that bow deep enough to fly the
            //lens through the balls — photographed on the volcano's opening as a frame of nothing but ball at
            //arm's length. The polar sweep above has no inward bow: the radius is interpolated between keys
            //that all stand at or beyond the gameplay stand-off, monotonically (1.7-2.4x the stand-off at the
            //scene's stand, 0.86 of that on the second key, 1.25-1.45x on the map key, 1x on arrival — and the
            //fallback shot's 1.9-2.4 / 1.5-1.8 the same way), and a Catmull-Rom on those never dips under its
            //last key: #519 probed twenty thousand rolls across every scene's DistanceScale and the fallback,
            //and the least radius of every flight was the arrival key itself. So this never fires now. It
            //stays because it is one line and the one thing it guards is the one thing the shot must never do —
            //and since #519 it SAYS SO if it ever does (see End), instead of hiding a symptom nobody would see.
            Vector3 away = Position - _centre;
            float radius = away.Length();

            if (radius > 1e-4f && radius < _minRadius)
            {
                _flooredFrames++;
                _deepestFloor = MathF.Min(_deepestFloor, radius / _minRadius);
                Position = _centre + away * (_minRadius / radius);
            }
        }

        /// <summary>A stand on the orbit about the centre: azimuth and elevation in radians, radius in units.</summary>
        private static Vector3 Orbit(float azimuth, float elevation, float radius) => new(
            MathF.Cos(azimuth) * MathF.Cos(elevation) * radius,
            MathF.Sin(elevation) * radius,
            MathF.Sin(azimuth) * MathF.Cos(elevation) * radius);

        /// <summary>
        /// Which way the arena leg swings and which way the tour then goes to the gun, for a stand the scene
        /// fixed (#409). Four candidates, tried in order of preference — carrying on the rolled way, carrying
        /// on the other way, swinging the rolled way and coming back, swinging the other way and coming back
        /// — and the first whose turn to the gun falls in the band is taken; if none does, the one nearest
        /// the band. The band is what stops the map and the arrival from being a stand-still at one end and
        /// a lap at the other: Space's planet stands 32 degrees off the gun, and carrying on either way from
        /// there was 270 to 340 degrees of turn in six seconds, which the trace measured at over 80 degrees a
        /// second through the middle of the tour.
        /// </summary>
        private static void ChooseTurn(float stand, float arenaTurn, float gunAzimuth, int rolledSign,
            out int arenaSign, out int arrivalSign, out float remaining)
        {
            Span<(int arena, int arrival)> candidates = stackalloc (int, int)[]
            {
                (rolledSign, rolledSign), (-rolledSign, -rolledSign), (rolledSign, -rolledSign), (-rolledSign, rolledSign)
            };

            arenaSign = arrivalSign = rolledSign;
            remaining = 0f;
            float best = float.MaxValue;

            foreach ((int arena, int arrival) in candidates)
            {
                float turn = RemainingTurn(stand + arena * arenaTurn, gunAzimuth, arrival);

                float outside = turn < MIN_REMAINING_TURN ? MIN_REMAINING_TURN - turn
                    : turn > MAX_REMAINING_TURN ? turn - MAX_REMAINING_TURN
                    : 0f;

                if (outside < best)
                {
                    best = outside;
                    arenaSign = arena;
                    arrivalSign = arrival;
                    remaining = turn;
                }

                if (outside == 0f) break;
            }
        }

        /// <summary>
        /// How far round, in <paramref name="sign"/>'s direction, a turn from <paramref name="from"/> has to go
        /// to reach <paramref name="to"/>: in (0, 2π], never negative and never zero, so a stand exactly on
        /// the gun's bearing is a full lap rather than no leg at all.
        /// </summary>
        private static float RemainingTurn(float from, float to, int sign)
        {
            float turn = (to - from) * sign;
            turn %= MathHelper.TwoPi;

            if (turn <= 0f) turn += MathHelper.TwoPi;

            return turn;
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
