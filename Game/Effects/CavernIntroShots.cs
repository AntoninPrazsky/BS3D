using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The cavern's own shots for a chapter intro's prologue (#559): the whole chamber from high on its wall,
    /// a glide low over the river onto a crystal cluster, and a crane up beside a god ray into the glowworms —
    /// cut together, then cut to the tour's last leg. The owner's verdict on the scenes without a prologue was
    /// "neck-breaking turns instead of cuts, and the camera should always attend to one concrete thing": the
    /// tour is one spline round the arena, and every one of these things stands a hundred units and more out
    /// of it, on the wall or under the ceiling, where the spline never goes.
    /// <para>
    /// <b>Everything is placed where <c>Cavern.fx</c> places it</b> — the clusters through
    /// <see cref="SceneRenderer.CavernCrystalCenter"/> and the shafts through
    /// <see cref="SceneRenderer.CavernGodRayXZ"/>, the shader's own arithmetic on the host — and the shell is
    /// a cylinder, a ceiling plane and a river plane with nothing between them but the island, so a lens kept
    /// inside those planes, off the wall and well clear of the island's axis cannot be inside anything. Built
    /// once when the intro begins; nothing here runs per frame.
    /// </para>
    /// </summary>
    internal static class CavernIntroShots
    {
        //The chamber: a slow arc this far round the axis, at this fraction of the cave's radius and this high
        //over the river, the lens on a point across the cave low over the water — the river's glow, the far
        //wall's crystals and the shafts all in one frame, and the island small in the middle of it.
        private const float CHAMBER_RADIUS_FRACTION = 0.70f;
        private const float CHAMBER_HEIGHT = 84f;
        private const float CHAMBER_DROP = 10f;
        private const float CHAMBER_ARC_DEGREES = 18f;
        private const float CHAMBER_SECONDS = 3.2f;

        //The crystals: a glide this high over the river towards one of the low clusters, from this far out of
        //it to this far, off to one side of the line straight in by this angle so the cluster turns as it nears.
        //⚠ Only the LOW clusters (their centre within this of the water): a high one seen from over the river
        //is a lit patch on a wall looked up at, and the river's glint under it — half the picture — is gone.
        private const float CRYSTAL_GLIDE_HEIGHT = 4.5f;
        private const float CRYSTAL_FROM = 95f;
        private const float CRYSTAL_TO = 44f;
        private const float CRYSTAL_SIDE_DEGREES = 22f;
        private const float CRYSTAL_LOW = 20f;
        private const float CRYSTAL_SECONDS = 3.2f;

        //The glowworms: a crane from low over the river to high up the cave, this far out from a god ray's
        //shaft, drifting this far across it, the lens pinned on the shaft under the ceiling — so the shaft
        //climbs through the frame into the constellation on the vault.
        private const float RAY_STAND_OFF = 55f;
        private const float RAY_DRIFT = 18f;
        private const float RAY_FROM_HEIGHT = 8f;
        private const float RAY_TO_HEIGHT = 70f;
        private const float RAY_LOOK_UNDER_CEILING = 25f;
        private const float RAY_SECONDS = 3.2f;

        private const int PATH_POINTS = 96;

        /// <summary>
        /// The prologue for the cavern being drawn, or null when the renderer has no cavern config.
        /// <paramref name="fieldOfView"/> is the intro's own gameplay frame, which each shot widens from.
        /// </summary>
        public static IntroShot[] Build(SceneRenderer scenes, float fieldOfView, Random random)
        {
            if (scenes?.GetSceneConfig(SceneKind.Cavern) is not CavernSceneConfig cavern) return null;

            return new[]
            {
                Chamber(cavern, fieldOfView, random),
                Crystals(scenes, cavern, fieldOfView, random),
                Glowworms(scenes, cavern, fieldOfView, random),
            };
        }

        /// <summary>The whole chamber, from high on its wall across to the far side: the establishing view.</summary>
        private static IntroShot Chamber(CavernSceneConfig cavern, float fieldOfView, Random random)
        {
            float waterY = cavern.Water.LevelY;
            float radius = cavern.Rock.CaveRadius * CHAMBER_RADIUS_FRACTION;
            float bearing = (float)random.NextDouble() * MathHelper.TwoPi;
            float arc = MathHelper.ToRadians(CHAMBER_ARC_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                float b = bearing + arc * s;
                path[i] = new Vector3(MathF.Cos(b) * radius, waterY + CHAMBER_HEIGHT - CHAMBER_DROP * s, MathF.Sin(b) * radius);
            }

            //Across the cave and low: the look swings with the arc, so the lens pans the far wall rather
            //than holding one patch of it.
            float middle = bearing + arc * 0.5f + MathHelper.Pi;
            Vector3 lookAt = new(MathF.Cos(middle) * radius * 0.45f, waterY + 18f, MathF.Sin(middle) * radius * 0.45f);

            return new IntroShot("the cavern", path, CHAMBER_SECONDS, fieldOfView * 1.25f, lookAt: lookAt);
        }

        /// <summary>A glide low over the river onto one of the low crystal clusters.</summary>
        private static IntroShot Crystals(SceneRenderer scenes, CavernSceneConfig cavern, float fieldOfView, Random random)
        {
            float waterY = cavern.Water.LevelY;

            //The low clusters, and one of them rolled.
            int chosen = 0, lowCount = 0;
            for (int k = 0; k < SceneRenderer.CAVERN_CRYSTAL_COUNT; k++)
            {
                if (scenes.CavernCrystalCenter(k).Y > waterY + CRYSTAL_LOW) continue;
                lowCount++;
                if (random.Next(lowCount) == 0) chosen = k;
            }

            Vector3 crystal = scenes.CavernCrystalCenter(chosen);
            Vector2 inward = -Vector2.Normalize(new Vector2(crystal.X, crystal.Z));
            float side = MathHelper.ToRadians(CRYSTAL_SIDE_DEGREES) * (random.Next(2) == 0 ? 1f : -1f);
            Vector2 approach = Rotate(inward, side);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float d = MathHelper.Lerp(CRYSTAL_FROM, CRYSTAL_TO, i / (float)(PATH_POINTS - 1));
                path[i] = new Vector3(crystal.X + approach.X * d, waterY + CRYSTAL_GLIDE_HEIGHT, crystal.Z + approach.Y * d);
            }

            //A little over the cluster's centre: its tallest spar stands up from there, and the look over the
            //water keeps the river's glint of it in the bottom of the frame.
            return new IntroShot("the crystals", path, CRYSTAL_SECONDS, fieldOfView * 1.1f, lookAt: crystal + new Vector3(0f, 4f, 0f));
        }

        /// <summary>A crane up beside a god ray, looking up it into the glowworms on the vault.</summary>
        private static IntroShot Glowworms(SceneRenderer scenes, CavernSceneConfig cavern, float fieldOfView, Random random)
        {
            float waterY = cavern.Water.LevelY;
            Vector2 beam = scenes.CavernGodRayXZ(random.Next(SceneRenderer.CAVERN_RAY_COUNT));

            //Outboard of the shaft, so the lens looks in and up across the vault's middle, where the worms are
            //densest, rather than into the wall at arm's length.
            Vector2 outward = Vector2.Normalize(beam);
            Vector2 across = new Vector2(-outward.Y, outward.X) * (random.Next(2) == 0 ? 1f : -1f);

            var path = new Vector3[PATH_POINTS];
            for (int i = 0; i < PATH_POINTS; i++)
            {
                float s = i / (float)(PATH_POINTS - 1);
                Vector2 plan = beam + outward * RAY_STAND_OFF + across * ((s - 0.5f) * RAY_DRIFT);
                path[i] = new Vector3(plan.X, waterY + MathHelper.Lerp(RAY_FROM_HEIGHT, RAY_TO_HEIGHT, s), plan.Y);
            }

            Vector3 lookAt = new(beam.X, cavern.Rock.CeilingY - RAY_LOOK_UNDER_CEILING, beam.Y);
            return new IntroShot("the glowworms", path, RAY_SECONDS, fieldOfView * 1.3f, lookAt: lookAt);
        }

        private static Vector2 Rotate(Vector2 v, float angle)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }
}
