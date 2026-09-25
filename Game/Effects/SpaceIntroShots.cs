using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// Space's own shots for a chapter intro's prologue (#559): the station hanging beside the gas giant, the
    /// drain seen from under the island against the stars, and the teal nebula with the volume drifting past
    /// the lens — cut together, then cut to the tour's last leg. The tour it replaces swung round the arena on
    /// one spline, and #409 had already found it "looking down into the island from an odd angle" in this very
    /// scene; each of these is about one thing the scene draws.
    /// <para>
    /// <b>Nothing out here has a surface but the island</b>, so safety is the island's own figures
    /// (<see cref="ArenaIsland"/>): the establishing shot stands ninety units off, the drain shot stays twice the
    /// funnel's depth under its hole and well outside its axis, and the nebula shot stands outside the island's
    /// rim at a radius the gun cannot reach. The planet and the nebula are directions
    /// (<see cref="SpaceSceneConfig"/>), not places: every lens looks at a fixed point far out along one, so a
    /// dolly slides the one layer of this sky that has depth — the Star Nest volume — past a sky that does not.
    /// </para>
    /// <para>
    /// The lenses are absolute (see <see cref="GridIntroShots"/>), so the planet stands where it does in the
    /// frame in the scene menu's tour and in play alike. Built once when the intro begins.
    /// </para>
    /// </summary>
    internal static class SpaceIntroShots
    {
        //The station: a lens this far out, this many degrees below the arena's centre and this many degrees
        //round from straight opposite the planet, dollying in, looking at a point part-way from the station to
        //the planet — so the island and its cluster stand in one half of the frame and the planet's lit limb in
        //the other. Straight opposite, the planet (10 degrees in radius) hides behind the station exactly.
        private const float STATION_FROM = 90f, STATION_TO = 74f;
        private const float STATION_BELOW_DEGREES = 7f;
        private const float STATION_OFFSET_DEGREES = 30f;
        private const float STATION_TOWARDS_PLANET = 0.45f;
        private const float STATION_FOV_DEGREES = 56f;
        private const float STATION_SECONDS = 3.4f;

        //The drain from under the island: a crane up from this far under the funnel's hole to this far, this far
        //out from the axis, the look on the funnel's middle — the glass, the gold beads and the machined underside
        //against the stars, which is what OpenBelow exists to show.
        private const float DRAIN_FROM_BELOW = 34f, DRAIN_TO_BELOW = 22f;
        private const float DRAIN_OUT = 24f;
        private const float DRAIN_FOV_DEGREES = 60f;
        private const float DRAIN_SECONDS = 3.0f;

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
        private const float NEBULA_SECONDS = 3.0f;

        //A fixed look-at far out along a direction stands in for the direction itself.
        private const float FAR = 5000f;

        private const int PATH_POINTS = 64;

        /// <summary>The prologue for the space scene the renderer is drawing, or null when it has no config.</summary>
        public static IntroShot[] Build(SceneRenderer scenes, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Space) is not SpaceSceneConfig space) return null;

            return new[]
            {
                Station(space, random),
                Drain(space, random),
                Nebula(space, random),
            };
        }

        /// <summary>
        /// The establishing view: the whole station — the island, its drain and the cluster over it — from far
        /// out and a little under it, beside the gas giant.
        /// </summary>
        private static IntroShot Station(SpaceSceneConfig space, Random random)
        {
            Vector3 planet = Direction(space.Planet.Direction, -Vector3.UnitZ);
            Vector3 centre = StationCentre;

            //Opposite the planet's bearing, turned a rolled way round so the two stand side by side.
            float bearing = MathF.Atan2(planet.Z, planet.X) + MathF.PI
                + MathHelper.ToRadians(STATION_OFFSET_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
            float below = MathHelper.ToRadians(STATION_BELOW_DEGREES);
            Vector3 outward = new(MathF.Cos(bearing) * MathF.Cos(below), -MathF.Sin(below), MathF.Sin(bearing) * MathF.Cos(below));

            Vector3[] path = Line(centre + outward * STATION_FROM, centre + outward * STATION_TO);

            //Part-way from the station towards the planet, seen from where the dolly starts, pinned there.
            Vector3 look = Vector3.Normalize(Vector3.Normalize(centre - path[0]) * (1f - STATION_TOWARDS_PLANET) + planet * STATION_TOWARDS_PLANET);

            return new IntroShot("the station", path, STATION_SECONDS, MathHelper.ToRadians(STATION_FOV_DEGREES),
                lookAt: path[0] + look * STATION_FROM);
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

        private static Vector3[] Line(Vector3 from, Vector3 to)
        {
            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++) path[i] = Vector3.Lerp(from, to, i / (float)(PATH_POINTS - 1));

            return path;
        }
    }
}
