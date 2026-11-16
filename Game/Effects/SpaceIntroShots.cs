using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// Space's own shots for a chapter intro's prologue (#559): the station held still in front of the gas giant
    /// while the saucer crosses close to the lens (#1117), the drain seen from under the island against the stars,
    /// and the teal nebula with the volume drifting past the lens — cut together, then cut to the tour's last leg.
    /// The tour it replaces swung round the arena on one spline, and #409 had already found it "looking down into
    /// the island from an odd angle" in this very scene; each of these is about one thing the scene draws.
    /// <para>
    /// <b>Nothing out here has a surface but the island and the saucer</b>, so safety is the island's own figures
    /// (<see cref="ArenaIsland"/>) and the saucer's pass: the saucer's shot stands far outside the island and outside
    /// the saucer's loop, the craft crossing its frame a few dozen units off the lens; the drain shot stays twice the
    /// funnel's depth under its hole and well outside its axis, and the nebula shot stands outside the island's
    /// rim at a radius the gun cannot reach. The planet and the nebula are directions
    /// (<see cref="SpaceSceneConfig"/>), not places: every lens looks at a fixed point far out along one, so a
    /// dolly slides the one layer of this sky that has depth — the Star Nest volume — past a sky that does not.
    /// </para>
    /// <para>
    /// The lenses are absolute (see <see cref="GridIntroShots"/>), so the planet stands where it does in the
    /// frame in the scene menu's tour and in play alike. Built once when the intro begins.
    /// </para>
    /// <para>
    /// ⚠ <b>The saucer is re-cued on every frame of its shot</b> (<see cref="IntroShot.Track"/>), its moment counted on from
    /// how far into the shot the intro's clock is, as the desert's creatures are (<see cref="DesertIntroShots"/>): the craft
    /// flies on the wall clock, which a pause does not stop and a hitch does not hold to a frame's worth
    /// (<c>ChapterIntro.MAX_INTRO_STEP</c>), and the shot is laid on its own seconds. Cued once, on the shot's first frame, a
    /// hitch in a level's first seconds (the physics warm-up's slices among them) would bring the craft in early by what the
    /// intro's clock lost, and a pause would send it past before the frame it was laid for. A pause still lets it fly on
    /// under the menu; the shot's next frame puts it back.
    /// </para>
    /// </summary>
    internal static class SpaceIntroShots
    {
        //The saucer (#1117; the owner: "one shot of the intro animation static, with a big planet in the background, the
        //smaller island somewhere lower, and then an unexpected flying saucer flying through this static shot, relatively
        //close to the camera"). A lens standing still this far out from the axis, outside the saucer's loop
        //(SaucerFlight.ORBIT_RADIUS), this far over the island's deck and this many degrees round from straight opposite
        //the planet, a rolled way, looking at a point part-way from the station to the planet through a long lens: the
        //planet's whole disc in the frame's upper half and the station small under it to one side. Straight opposite, the
        //planet (10 degrees in radius) stands right over the station.
        private const float SAUCER_DISTANCE = 280f;
        private const float SAUCER_OVER_DECK = 2.5f;
        private const float SAUCER_OFFSET_DEGREES = 20f;
        private const float SAUCER_TOWARDS_PLANET = 0.52f;
        private const float SAUCER_FOV_DEGREES = 47f;
        internal const float SAUCER_SECONDS = 4.2f;

        //The craft, cued while the shot is on screen (SceneRenderer.CueSpaceSaucer) to cross the frame: its middle this far from
        //the lens, along a line this many degrees under the look and this many towards the planet's side of the frame, this far
        //into the shot, on SaucerPass's run. It always crosses right to left (the loop's way round, seen from outside it), so it
        //comes in from the planet's side on one roll and leaves by it on the other; and the run is oblique to the look, nearing
        //the lens as it crosses on one roll and drawing away on the other, the two rolls mirror images with the time reversed.
        //Turned this far, the widest frame the game is played at (2.4:1) cuts the run about as long before the moment as after
        //it on either roll, and a narrower one, which only crops the sides, cuts it shorter at both ends: the craft comes in
        //after most of a second of stillness and is gone a fifth of a second before the cut (SpaceIntroShotsTests holds them).
        internal const float PASS_DISTANCE = 34f;
        private const float PASS_DROP_DEGREES = 9f;
        private const float PASS_ASIDE_DEGREES = 18f;
        internal const float PASS_AT_SECONDS = 2.4f;

        //The drain from under the island: a crane up from this far under the funnel's hole to this far, this far
        //out from the axis, the look on the funnel's middle — the glass, the gold beads and the machined underside
        //against the stars, which is what OpenBelow exists to show.
        private const float DRAIN_FROM_BELOW = 34f, DRAIN_TO_BELOW = 22f;
        private const float DRAIN_OUT = 24f;
        private const float DRAIN_FOV_DEGREES = 60f;
        internal const float DRAIN_SECONDS = 2.7f;

        //The nebula: a sideways drift across the island from the side away from the nebula, this far out from the
        //axis, this far to one side of the line through it (so the cluster stands at the frame's edge rather
        //than in front of the nebula) and this far over the deck — clear of the gun, which stands on the rim —
        //looking along the nebula's own direction tipped down this far, so the deck's rim runs across the
        //bottom of the frame and gives the sky a foreground. ⚠ The first cut stood outside the rim on the
        //nebula's side looking out, and the frame was the nebula alone in the void, with no scale.
        private const float NEBULA_BACK = 50f;
        private const float NEBULA_ASIDE = 24f;
        private const float NEBULA_OVER_DECK = 7f;
        private const float NEBULA_DRIFT = 16f;
        private const float NEBULA_TIP_DEGREES = 7f;
        private const float NEBULA_FOV_DEGREES = 56f;
        internal const float NEBULA_SECONDS = 2.8f;

        //A fixed look-at far out along a direction stands in for the direction itself.
        private const float FAR = 5000f;

        private const int PATH_POINTS = IntroPaths.POINTS;

        /// <summary>The prologue for the space scene the renderer is drawing, or null when it has no config.</summary>
        /// <param name="scenes">The live scene renderer, whose saucer the first shot cues.</param>
        /// <param name="clock">The game's wall clock, read on every frame of the saucer's shot: the craft flies on it.</param>
        /// <param name="random">The intro's own roll.</param>
        public static IntroShot[] Build(SceneRenderer scenes, Func<float> clock, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Space) is not SpaceSceneConfig space) return null;
            return Build(space, scenes.CueSpaceSaucer, clock, random);
        }

        /// <summary>The prologue off the space config, the saucer's pass cued through <paramref name="cueSaucer"/> with the point
        /// its middle passes and the wall-clock moment it is there.</summary>
        internal static IntroShot[] Build(SpaceSceneConfig space, Action<Vector3, float> cueSaucer, Func<float> clock, Random random) =>
            new[]
            {
                Saucer(space, cueSaucer, clock, random),
                Drain(space, random),
                Nebula(space, random),
            };

        /// <summary>
        /// The establishing view, held still (#1117): the station small and low in front of the gas giant from far out, the lens
        /// standing where the saucer's loop passes between it and the station, and the craft cued on every frame of the shot
        /// (<see cref="IntroShot.Track"/>) to cross the frame close to the lens <see cref="PASS_AT_SECONDS"/> into it on the
        /// intro's own clock.
        /// </summary>
        internal static IntroShot Saucer(SpaceSceneConfig space, Action<Vector3, float> cueSaucer, Func<float> clock, Random random)
        {
            Vector3 planet = Direction(space.Planet.Direction, -Vector3.UnitZ);
            Vector3 centre = StationCentre;

            //Opposite the planet's bearing, turned a rolled way round so the planet stands to one side over the station
            float side = random.Next(2) == 0 ? 1f : -1f;
            float bearing = MathF.Atan2(planet.Z, planet.X) + MathF.PI + MathHelper.ToRadians(SAUCER_OFFSET_DEGREES) * side;
            var lens = new Vector3(MathF.Cos(bearing) * SAUCER_DISTANCE, ArenaIsland.TOP_Y + SAUCER_OVER_DECK,
                MathF.Sin(bearing) * SAUCER_DISTANCE);

            //Part-way from the station towards the planet, pinned
            Vector3 look = Vector3.Normalize(Vector3.Normalize(centre - lens) * (1f - SAUCER_TOWARDS_PLANET) + planet * SAUCER_TOWARDS_PLANET);

            //Where the craft's middle passes: down from the look about the horizontal across it, then turned about the vertical
            //towards the planet's side
            Vector3 across = Vector3.Normalize(Vector3.Cross(look, Vector3.Up));
            Vector3 towards = Vector3.Transform(look, Matrix.CreateFromAxisAngle(across, -MathHelper.ToRadians(PASS_DROP_DEGREES)));
            towards = Vector3.Normalize(Vector3.Transform(towards, Matrix.CreateRotationY(side * MathHelper.ToRadians(PASS_ASIDE_DEGREES))));
            Vector3 pass = lens + towards * PASS_DISTANCE;

            //Re-cued on every frame of the shot, its moment counted on from how far into the shot the intro's clock is (see the
            //class remarks)
            return new IntroShot("the saucer", new[] { lens, lens }, SAUCER_SECONDS, MathHelper.ToRadians(SAUCER_FOV_DEGREES),
                lookAt: lens + look * FAR,
                onFrame: seconds => cueSaucer?.Invoke(pass, (clock?.Invoke() ?? 0f) + PASS_AT_SECONDS - seconds));
        }

        /// <summary>
        /// Under the island: a crane rising towards the drain's hole from below and to one side — the side away
        /// from the planet, so the lens looks up across the funnel towards it.
        /// </summary>
        private static IntroShot Drain(SpaceSceneConfig space, Random random)
        {
            Vector3 planet = Direction(space.Planet.Direction, -Vector3.UnitZ);
            float bearing = MathF.Atan2(planet.Z, planet.X) + MathF.PI + (float)(random.NextDouble() - 0.5) * 0.8f;
            Vector3 out1 = new(MathF.Cos(bearing), 0f, MathF.Sin(bearing));

            float hole = ArenaIsland.FUNNEL_BOTTOM_Y;
            Vector3[] path = Line(out1 * DRAIN_OUT + Vector3.Up * (hole - DRAIN_FROM_BELOW),
                out1 * (DRAIN_OUT * 0.85f) + Vector3.Up * (hole - DRAIN_TO_BELOW));

            Vector3 funnel = new(0f, (hole + ArenaIsland.TOP_Y) * 0.5f, 0f);

            return new IntroShot("the drain", path, DRAIN_SECONDS, MathHelper.ToRadians(DRAIN_FOV_DEGREES), lookAt: funnel);
        }

        /// <summary>
        /// The teal nebula (<see cref="SpaceSceneConfig.NebulaTwo"/>, the one no other shot faces) over the deck:
        /// a sideways drift from behind the island, looking out across its rim at the nebula, so the volume's
        /// filaments slide past in front of a nebula that does not move and the machined deck gives it scale.
        /// </summary>
        private static IntroShot Nebula(SpaceSceneConfig space, Random random)
        {
            Vector3 nebula = Direction(space.NebulaTwo.Direction, Vector3.UnitX);
            Vector3 outward = Vector3.Normalize(new Vector3(nebula.X, 0f, nebula.Z) + new Vector3(1e-4f, 0f, 0f));
            Vector3 across = Vector3.Cross(Vector3.Up, outward) * (random.Next(2) == 0 ? 1f : -1f);

            Vector3 middle = -outward * NEBULA_BACK + across * NEBULA_ASIDE + Vector3.Up * (ArenaIsland.TOP_Y + NEBULA_OVER_DECK);
            Vector3[] path = Line(middle + across * (NEBULA_DRIFT * 0.5f), middle - across * (NEBULA_DRIFT * 0.5f));

            //The nebula's direction, tipped down about the horizontal axis across it.
            Vector3 tipAxis = Vector3.Normalize(Vector3.Cross(nebula, Vector3.Up));
            Vector3 look = Vector3.Transform(nebula, Matrix.CreateFromAxisAngle(tipAxis, -MathHelper.ToRadians(NEBULA_TIP_DEGREES)));

            return new IntroShot("the nebula", path, NEBULA_SECONDS, MathHelper.ToRadians(NEBULA_FOV_DEGREES),
                lookAt: middle + look * FAR);
        }

        //The middle of what hangs in space: the island's deck, with the cluster over it.
        private static Vector3 StationCentre => new(0f, ArenaIsland.TOP_Y + 6f, 0f);

        private static Vector3 Direction(Vec3 configured, Vector3 fallback)
        {
            Vector3 v = configured.ToVector3();
            return v.LengthSquared() > 1e-8f ? Vector3.Normalize(v) : fallback;
        }

        private static Vector3[] Line(Vector3 from, Vector3 to) => IntroPaths.Line(from, to, PATH_POINTS);
    }
}
