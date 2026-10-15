using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The Cutter's saw blade (#766): a thin steel disc in its own XZ plane, centred at the origin, whose rim is a ring of
    /// hooked teeth - the round the Cut power-up loads is a ball with this blade through its equator. Drawn on the ball's
    /// own instances after the ball, by <c>BallRenderSet</c>, in <c>InstancedModelCutterBlade</c>.
    /// <para>
    /// <b>Sized for a ball of radius 0.5</b>, which is every ball. The disc starts well inside the ball (<see cref="HUB_RADIUS"/>,
    /// so its inner edge is hidden at every LOD of the sphere) and only the teeth show: the gullets just past the surface,
    /// the tips at <see cref="TIP_RADIUS"/>, under the gun's bore (<c>CannonRig.BORE_RADIUS</c> 0.6), so a loaded cutter's
    /// blade never pokes through the barrel's wall at any aim - a level disc inside a tilted tube reaches the tube's axis
    /// no further than its own radius. <c>BS3D.Tests</c> holds that.
    /// </para>
    /// <para>
    /// <b>Nothing in it is mathematically regular</b> (#640): every tooth's tip and lean is nudged off its neighbours' by a
    /// fixed hash, and two teeth are chipped short, so a spinning blade shows that it is turning and reads as a used tool
    /// rather than a gear. The design references (C:\Users\panrd\AI\sd\out\766-*) drew a saw set into a ball's equator,
    /// its teeth standing a sixth of the radius past it.
    /// </para>
    /// <para>
    /// Flat-shaded (a face, a face, and the rim's own facets) and wound through <see cref="MeshBuilder"/>; open on its
    /// inside, where nothing can see it. <c>Tools/WindingCheck</c> finds it through <c>BallRenderSet</c>.
    /// </para>
    /// </summary>
    public sealed class SawBladeMesh : IProceduralMesh, IDisposable
    {
        /// <summary>How many teeth the rim has.</summary>
        public const int TEETH = 24;

        /// <summary>Where the disc starts, inside the ball: below the coarsest sphere's chords at the equator (0.487).</summary>
        public const float HUB_RADIUS = 0.40f;

        /// <summary>The gullets between the teeth, just past the ball's surface.</summary>
        public const float GULLET_RADIUS = 0.528f;

        /// <summary>The teeth's tips, the blade's whole reach: under the bore's 0.6.</summary>
        public const float TIP_RADIUS = 0.585f;

        /// <summary>Half the blade's thickness: a twentieth of a ball, enough to show a lit edge-on line.</summary>
        public const float HALF_THICKNESS = 0.011f;

        //Each tooth's outline over its own pitch, as (share of the pitch, share of the tooth's height): the rounded gullet,
        //the long back rising to the tip, and the short hooked face falling back. The last point is the next gullet's.
        private static readonly Vector2[] TOOTH_PROFILE =
        {
            new(0.00f, 0.00f), new(0.07f, -0.05f), new(0.30f, 0.30f), new(0.58f, 0.68f), new(0.80f, 1.00f), new(0.88f, 0.42f),
        };

        //The two chipped teeth and how much of their height is left, and how far the hash moves every other tip
        private static readonly int[] CHIPPED = { 5, 16 };
        private const float CHIPPED_HEIGHT = 0.45f;
        private const float TIP_JITTER = 0.12f;
        private const float LEAN_JITTER = 0.035f;

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        public SawBladeMesh(GraphicsDevice device)
        {
            Vector2[] outline = Outline();

            MeshBuilder builder = new();
            Vector3 up = Vector3.UnitY;

            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];

                //The hub's points under each outline point, along its own direction
                Vector2 ha = Vector2.Normalize(a) * HUB_RADIUS, hb = Vector2.Normalize(b) * HUB_RADIUS;

                Vector3 topA = new(a.X, HALF_THICKNESS, a.Y), topB = new(b.X, HALF_THICKNESS, b.Y);
                Vector3 hubTopA = new(ha.X, HALF_THICKNESS, ha.Y), hubTopB = new(hb.X, HALF_THICKNESS, hb.Y);
                Vector3 bottomA = new(a.X, -HALF_THICKNESS, a.Y), bottomB = new(b.X, -HALF_THICKNESS, b.Y);
                Vector3 hubBottomA = new(ha.X, -HALF_THICKNESS, ha.Y), hubBottomB = new(hb.X, -HALF_THICKNESS, hb.Y);

                //The two faces, from the hub out to the rim
                builder.AddQuad(hubTopA, hubTopB, topB, topA, up, up, up, up, up);
                builder.AddQuad(hubBottomA, hubBottomB, bottomB, bottomA, -up, -up, -up, -up, -up);

                //And the rim between them, facing away from the arbor across its own segment
                Vector2 along = b - a;
                Vector3 outward = Vector3.Normalize(new Vector3(along.Y, 0f, -along.X));
                if (Vector3.Dot(outward, new Vector3(a.X + b.X, 0f, a.Y + b.Y)) < 0f) outward = -outward;

                builder.AddQuad(topA, topB, bottomB, bottomA, outward, outward, outward, outward, outward);
            }

            (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
            VertexBuffer = vertices;
            IndexBuffer = indices;
            PrimitiveCount = primitives;
            BoundingSphere = new BoundingSphere(Vector3.Zero, TIP_RADIUS);
        }

        /// <summary>
        /// The rim's outline in the blade's plane (X, Z), round the arbor in order: every tooth's profile at its own pitch,
        /// its tip nudged by a fixed hash, the chipped ones cut short. Public so the tests can measure the reach.
        /// </summary>
        public static Vector2[] Outline()
        {
            int perTooth = TOOTH_PROFILE.Length;
            Vector2[] outline = new Vector2[TEETH * perTooth];
            float pitch = MathHelper.TwoPi / TEETH;

            for (int tooth = 0; tooth < TEETH; tooth++)
            {
                float height = (TIP_RADIUS - GULLET_RADIUS) * (1f - TIP_JITTER * Hash(tooth, 1));
                if (Array.IndexOf(CHIPPED, tooth) >= 0) height *= CHIPPED_HEIGHT;

                float lean = LEAN_JITTER * (2f * Hash(tooth, 2) - 1f);

                for (int k = 0; k < perTooth; k++)
                {
                    Vector2 step = TOOTH_PROFILE[k];

                    //The tip and its neighbours lean together; the gullet stays on the pitch, so the teeth never overlap
                    float share = step.X + (step.Y > 0.5f ? lean : 0f);
                    float angle = (tooth + share) * pitch;
                    float radius = GULLET_RADIUS + height * step.Y;

                    outline[tooth * perTooth + k] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                }
            }

            return outline;
        }

        //A fixed hash of a tooth and a channel, in [0, 1): the same blade every run
        private static float Hash(int tooth, int channel)
        {
            uint h = (uint)(tooth * 374761393 + channel * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;

            return (h & 0xFFFFFF) / 16777216f;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer = null;
        }
    }
}
