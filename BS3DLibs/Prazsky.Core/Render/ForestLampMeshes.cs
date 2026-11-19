using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The meshes of the forest's lamps and of its wind turbine (#1166), as <see cref="MeshBuilder"/> geometry with no device,
    /// so the tests read what is drawn: every piece a closed solid wound clockwise seen from outside (<see cref="MeshBuilder"/>
    /// winds each triangle against the face normal it is given), a run of pipe a chain of capped tubes with a ball at every
    /// bend, so nothing has an open end (<c>WindingCheck --openness</c>).
    /// <para>
    /// A lamp is built in its pole's frame (<see cref="ForestLampLayout"/>: the foot at the origin, up +Y, the arm towards -Z),
    /// in four materials: the pole's weathered wood; the iron (the gooseneck arm and its brace, the cap on the pole's top, the
    /// conduit running down the pole's back to the ground and its junction box, the socket and the shade's enamelled shell);
    /// the shade's white enamel lining; and the bulb. The references (<c>C:\Users\panrd\AI\sd\out\1166-*</c>) draw old rural
    /// lamps this way: a cone shade on a curved arm, the wire running down the pole.
    /// </para>
    /// <para>
    /// The turbine is built in its own frame (<see cref="ForestTurbineLayout"/>: the foot at the origin, the rotor facing -Z)
    /// in three pieces: what stands still (the tapered tower, the nacelle and the spinner, which is round about the rotor's axis
    /// and so need not turn), the rotor's three blades about the hub (the XY plane, turned about Z), and the aviation light.
    /// </para>
    /// </summary>
    internal static class ForestLampMeshes
    {
        private const int POLE_SIDES = 10, PIPE_SIDES = 8, SHADE_SIDES = 18;

        //The shade's frame: its rim's plane, under the arm's end; and its profile's scale
        private static readonly Vector3 SHADE = new(0f, ForestLampLayout.POLE_LENGTH - ForestLampLayout.SHADE_DROP, -ForestLampLayout.ARM_REACH);
        private const float K = ForestLampLayout.SHADE_SCALE;
        private static Matrix ShadePlacement => Matrix.CreateScale(K) * Matrix.CreateTranslation(SHADE);

        //The shade's enamelled shell, its profile from the axis at the top of the collar round the rim and back in under the
        //lining (LatheMesh's rule: top, outside, underside); and the lining, a thin cone just under the shell's inside
        private static readonly LathePoint[] SHADE_PROFILE =
        {
            new(0f, 0.44f), new(0.10f, 0.44f, crease: true), new(0.10f, 0.36f, crease: true), new(0.36f, 0.20f), new(0.58f, 0.04f),
            new(0.64f, 0f, crease: true), new(0.62f, -0.03f, crease: true), new(0.57f, 0.00f, crease: true), new(0.13f, 0.31f, crease: true),
            new(0f, 0.31f),
        };

        private static readonly LathePoint[] LINING_PROFILE =
        {
            new(0f, 0.29f), new(0.13f, 0.29f, crease: true), new(0.555f, -0.005f, crease: true), new(0.545f, -0.015f, crease: true),
            new(0.125f, 0.28f, crease: true), new(0f, 0.28f),
        };

        //The crossarm across the pole's back near its top, and where its two insulators stand on it
        private const float CROSSARM_DROP = 0.5f, CROSSARM_HALF = 1.2f, CROSSARM_HALF_HEIGHT = 0.09f, CROSSARM_HALF_DEPTH = 0.08f;
        private const float INSULATOR_X = 0.95f;
        private static float CrossarmZ => ForestLampLayout.POLE_TOP_RADIUS + CROSSARM_HALF_DEPTH - 0.02f;
        private static float CrossarmTop => ForestLampLayout.POLE_LENGTH - CROSSARM_DROP + CROSSARM_HALF_HEIGHT;

        /// <summary>The pole: a tapered round of weathered wood from its sunk foot to its top, flat-topped under the cap, and the
        /// crossarm bolted across its back that once carried the line (the references' old poles all wear one).</summary>
        public static MeshBuilder Wood()
        {
            var wood = new MeshBuilder();
            const float L = ForestLampLayout.POLE_LENGTH;
            wood.AddLathe(new[]
            {
                new LathePoint(0f, L), new LathePoint(ForestLampLayout.POLE_TOP_RADIUS, L, crease: true),
                new LathePoint((ForestLampLayout.POLE_TOP_RADIUS + ForestLampLayout.POLE_RADIUS) * 0.5f, L * 0.5f),
                new LathePoint(ForestLampLayout.POLE_RADIUS, 0f, crease: true), new LathePoint(0f, 0f),
            }, POLE_SIDES, Matrix.Identity, Vector2.Zero);
            wood.AddBox(new Vector3(0f, L - CROSSARM_DROP, CrossarmZ), new Vector3(CROSSARM_HALF, 0f, 0f),
                new Vector3(0f, CROSSARM_HALF_HEIGHT, 0f), new Vector3(0f, 0f, CROSSARM_HALF_DEPTH));
            return wood;
        }

        /// <summary>The ironwork: the arm, its brace, the pole's cap, the conduit and its box, the socket and the shade's shell.</summary>
        public static MeshBuilder Iron()
        {
            var iron = new MeshBuilder();
            const float L = ForestLampLayout.POLE_LENGTH;

            //The cap over the pole's end grain: a shallow cone, its skirt round the top
            iron.AddLathe(new[]
            {
                new LathePoint(0f, L + 0.14f), new LathePoint(0.26f, L - 0.02f, crease: true), new LathePoint(0.26f, L - 0.08f, crease: true),
                new LathePoint(0f, L - 0.08f),
            }, POLE_SIDES, Matrix.Identity, Vector2.Zero);

            //The gooseneck: out of the pole, rising a little, over and down into the shade's collar
            Pipe(iron, 0.06f, new Vector3[]
            {
                new(0f, L - 1.10f, 0f), new(0f, L - 1.10f, -0.55f), new(0f, L - 0.98f, -1.25f), new(0f, L - 1.02f, -1.85f),
                new(0f, L - 1.12f, -2.17f), SHADE + new Vector3(0f, 0.52f * K, 0f), SHADE + new Vector3(0f, 0.40f * K, 0f),
            });

            //Its brace, from lower down the pole up under the arm
            Pipe(iron, 0.035f, new Vector3[] { new(0f, L - 2.6f, -0.10f), new(0f, L - 1.03f, -1.25f) });

            //A band clamping the arm and the brace to the pole
            iron.AddLathe(new[]
            {
                new LathePoint(0f, L - 0.95f), new LathePoint(0.245f, L - 0.95f, crease: true), new LathePoint(0.245f, L - 1.25f, crease: true),
                new LathePoint(0f, L - 1.25f),
            }, POLE_SIDES, Matrix.Identity, Vector2.Zero);

            //The wire's conduit down the pole's back (+Z), standing off the taper, into the ground; its box at a man's height
            float Back(float h) => MathHelper.Lerp(ForestLampLayout.POLE_RADIUS, ForestLampLayout.POLE_TOP_RADIUS, h / L) + 0.045f;
            Pipe(iron, 0.045f, new Vector3[]
            {
                new(0f, -0.3f, Back(0f)), new(0f, L - 1.4f, Back(L - 1.4f)), new(0f, L - 1.15f, Back(L - 1.15f) - 0.05f),
                new(0f, L - 1.15f, 0f),
            });
            iron.AddBox(new Vector3(0f, 3.0f, Back(3f) + 0.06f), new Vector3(0.13f, 0f, 0f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0f, 0.07f));

            //The crossarm's two braces from the pole, and the insulators' pins
            foreach (float side in new[] { -1f, 1f })
            {
                Pipe(iron, 0.025f, new Vector3[]
                {
                    new(0f, L - CROSSARM_DROP - 0.75f, CrossarmZ - 0.06f), new(side * 0.7f, L - CROSSARM_DROP - 0.05f, CrossarmZ),
                });
                Pipe(iron, 0.025f, new Vector3[] { new(side * INSULATOR_X, CrossarmTop - 0.05f, CrossarmZ), new(side * INSULATOR_X, CrossarmTop + 0.1f, CrossarmZ) });
            }

            //The socket under the shade's crown, and the shell
            Pipe(iron, 0.07f, new[] { SHADE + new Vector3(0f, 0.32f * K, 0f), SHADE + new Vector3(0f, 0.20f * K, 0f) });
            iron.AddLathe(SHADE_PROFILE, SHADE_SIDES, ShadePlacement, Vector2.Zero);
            return iron;
        }

        /// <summary>The shade's white enamel lining, which the bulb lights, and the crossarm's two porcelain insulators, of the
        /// same white glaze.</summary>
        public static MeshBuilder Lining()
        {
            var lining = new MeshBuilder();
            lining.AddLathe(LINING_PROFILE, SHADE_SIDES, ShadePlacement, Vector2.Zero);

            //A pin insulator: two sheds over a waist, its top grooved for a line no longer there
            LathePoint[] insulator =
            {
                new(0f, 0.30f), new(0.05f, 0.30f), new(0.065f, 0.27f, crease: true), new(0.11f, 0.215f), new(0.11f, 0.195f, crease: true),
                new(0.06f, 0.18f, crease: true), new(0.09f, 0.11f), new(0.09f, 0.095f, crease: true), new(0.045f, 0.08f, crease: true),
                new(0f, 0.08f),
            };
            foreach (float side in new[] { -1f, 1f })
                lining.AddLathe(insulator, 10, Matrix.CreateTranslation(side * INSULATOR_X, CrossarmTop - 0.08f, CrossarmZ), Vector2.Zero);
            return lining;
        }

        /// <summary>The bulb: a pear of glass under the socket, at <see cref="ForestLampLayout.BULB"/>.</summary>
        public static MeshBuilder Bulb()
        {
            var bulb = new MeshBuilder();
            float r = ForestLampLayout.BULB_RADIUS;
            bulb.AddLathe(new[]
            {
                new LathePoint(0f, r * 1.7f), new LathePoint(r * 0.55f, r * 1.55f), new LathePoint(r * 0.75f, r * 0.9f),
                new LathePoint(r, 0.1f * r), new LathePoint(r * 0.85f, -0.6f * r), new LathePoint(r * 0.45f, -0.95f * r), new LathePoint(0f, -r),
            }, 10, Matrix.CreateTranslation(ForestLampLayout.BULB), Vector2.Zero);
            return bulb;
        }

        //--- The turbine --------------------------------------------------------------------------------------------

        /// <summary>The turbine's standing part: the tapered tower, the nacelle on it and the spinner on the rotor's axis.</summary>
        public static MeshBuilder TurbineBody()
        {
            var body = new MeshBuilder();
            const float H = ForestTurbineLayout.TOWER_HEIGHT;
            body.AddLathe(new[]
            {
                new LathePoint(0f, H + 0.2f), new LathePoint(ForestTurbineLayout.TOWER_TOP_RADIUS, H + 0.2f, crease: true),
                new LathePoint((ForestTurbineLayout.TOWER_TOP_RADIUS + ForestTurbineLayout.TOWER_RADIUS) * 0.5f, H * 0.45f),
                new LathePoint(ForestTurbineLayout.TOWER_RADIUS, 0f, crease: true), new LathePoint(0f, 0f),
            }, 18, Matrix.Identity, Vector2.Zero);

            //A flange ring where the tower's sections meet, a third of the way up
            body.AddLathe(new[]
            {
                new LathePoint(0f, H * 0.36f + 0.25f), new LathePoint(2.62f, H * 0.36f + 0.25f, crease: true),
                new LathePoint(2.62f, H * 0.36f - 0.25f, crease: true), new LathePoint(0f, H * 0.36f - 0.25f),
            }, 18, Matrix.Identity, Vector2.Zero);

            //The nacelle: a rounded body along Z (the lathe's Y turned onto +Z), taller than it is wide, its nose to -Z
            Matrix nacelle = Matrix.CreateScale(1f, 1f, 1.18f) * Matrix.CreateRotationX(MathHelper.PiOver2) * Matrix.CreateTranslation(0f, H + 2.2f, 0.6f);
            body.AddLathe(new[]
            {
                new LathePoint(0f, 5.4f), new LathePoint(1.3f, 5.2f), new LathePoint(1.85f, 4.4f), new LathePoint(2.0f, 2.5f),
                new LathePoint(2.0f, -3.2f), new LathePoint(1.85f, -4.4f), new LathePoint(1.2f, -4.9f), new LathePoint(0f, -5.0f),
            }, 14, nacelle, Vector2.Zero);

            //The spinner, its back inside the nacelle's nose and its point upwind
            Matrix spinner = Matrix.CreateRotationX(MathHelper.PiOver2) * Matrix.CreateTranslation(ForestTurbineLayout.HUB);
            body.AddLathe(new[]
            {
                new LathePoint(0f, 1.6f), new LathePoint(1.55f, 1.6f, crease: true), new LathePoint(1.6f, 0.4f), new LathePoint(1.4f, -0.9f),
                new LathePoint(0.85f, -2.2f), new LathePoint(0f, -2.9f),
            }, 14, spinner, Vector2.Zero);

            //The aviation light's mast on the roof, towards the tail
            body.AddBox(ForestTurbineLayout.BEACON - new Vector3(0f, 0.8f, 0f), new Vector3(0.18f, 0f, 0f), new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0f, 0.18f));
            return body;
        }

        /// <summary>The rotor's three blades about the hub, in the XY plane: tapering from a broad root to a narrow tip, a little
        /// swept back at the tip and pitched, so each catches the light differently as it turns.</summary>
        public static MeshBuilder TurbineRotor()
        {
            var rotor = new MeshBuilder();
            float tip = ForestTurbineLayout.ROTOR_RADIUS;

            //Along the blade (r) and across it (c): the leading edge on the negative side
            (float R, float C, float Half)[] outline =
            {
                (1.0f, -0.95f, 0.45f), (5.5f, -1.45f, 0.38f), (tip * 0.6f, -0.95f, 0.18f), (tip, -0.25f, 0.06f),
                (tip, 0.25f, 0.06f), (tip * 0.6f, 1.0f, 0.14f), (5.5f, 1.85f, 0.32f), (1.0f, 0.95f, 0.45f),
            };
            const float PITCH = 0.14f;

            for (int b = 0; b < 3; b++)
            {
                float a = b * MathHelper.TwoPi / 3f;
                Vector3 along = new(MathF.Cos(a), MathF.Sin(a), 0f);
                Vector3 across = new(-MathF.Sin(a), MathF.Cos(a), 0f);
                Vector3 normal = Vector3.UnitZ * MathF.Cos(PITCH) - across * MathF.Sin(PITCH);
                Vector3 pitched = across * MathF.Cos(PITCH) + Vector3.UnitZ * MathF.Sin(PITCH);

                var points = new Vector3[outline.Length];
                var half = new float[outline.Length];
                for (int i = 0; i < outline.Length; i++)
                {
                    points[i] = along * outline[i].R + pitched * outline[i].C;
                    half[i] = outline[i].Half;
                }
                rotor.AddPlate(points, half, normal);
            }

            return rotor;
        }

        /// <summary>The aviation light: a small round lamp on its mast.</summary>
        public static MeshBuilder TurbineBeacon()
        {
            var beacon = new MeshBuilder();
            Sphere(beacon, ForestTurbineLayout.BEACON, 0.55f, 10);
            return beacon;
        }

        //--- Pieces -------------------------------------------------------------------------------------------------

        //A run of pipe through the points: a capped tube along each leg, and a ball of the pipe's radius at every bend and at
        //both ends, so every join is round and no end is open
        private static void Pipe(MeshBuilder builder, float radius, IReadOnlyList<Vector3> points)
        {
            for (int k = 0; k + 1 < points.Count; k++)
            {
                Vector3 leg = points[k + 1] - points[k];
                float length = leg.Length();
                if (length < 1e-4f) continue;
                Vector3 axis = leg / length;
                Vector3 reference = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
                builder.AddTube((points[k] + points[k + 1]) * 0.5f, axis, reference, length * 0.5f, radius, PIPE_SIDES);
            }

            foreach (Vector3 point in points) Sphere(builder, point, radius, PIPE_SIDES);
        }

        //A ball about a point, a closed lathe
        private static void Sphere(MeshBuilder builder, Vector3 centre, float radius, int sides)
        {
            const int RINGS = 6;
            var profile = new LathePoint[RINGS + 1];
            for (int k = 0; k <= RINGS; k++)
            {
                float angle = k / (float)RINGS * MathHelper.Pi;
                profile[k] = new LathePoint(k == 0 || k == RINGS ? 0f : MathF.Sin(angle) * radius, MathF.Cos(angle) * radius);
            }
            builder.AddLathe(profile, sides, Matrix.CreateTranslation(centre), Vector2.Zero);
        }
    }
}
