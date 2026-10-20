using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A doum palm for the savanna (#451, the owner's ask off the plants reference sheet): the one palm
    /// whose trunk <b>forks</b> — once, and often again — each fork ending in a head of stiff fan-shaped
    /// leaves on stalks. That forking is the whole silhouette, which is why it is not the tropical beach's
    /// <see cref="PalmMesh"/> (a single bowed trunk under drooping pinnate fronds) planted somewhere else.
    /// Two meshes, two materials: the wood (trunks, stalks) and the fans.
    /// </summary>
    public sealed class DoumPalmMesh : IDisposable
    {
        public IProceduralMesh Wood { get; }
        public IProceduralMesh Fronds { get; }

        /// <summary>
        /// How far the trunk's foot reaches from the axis, at scale 1 (#658): what a scatter sinks the palm by, so the
        /// lowest ground under that circle is where it stands and no side of the foot hangs in the air on a slope.
        /// </summary>
        public float BaseRadius { get; }

        /// <summary>What the palm is made of, as slabs at scale 1 (#653): the trunk to its fork and the crown of heads.</summary>
        public Slab[] Volume { get; }

        //The foot's radius as a multiple of the radius the trunk holds to its fork
        private const float FOOT_FLARE = 1.25f;

        //A fork's or a head's closed end bulges this much past its last ring, as a share of its radius (a hemisphere): the
        //pieces that start there (the arms, the sub-arms, the stalks) begin inside it, the arms at this share of the radius
        //they leave, so the whole of their first ring is in the dome and not on its skin (the section has seven sides, whose
        //flats stand at 0.9 of the corners' radius: a ring that reaches past them is open)
        private const float JOINT_DOME = 1f;
        private const float ARM_START = 0.75f;

        //The skirt of exposed roots at the foot (#670), in trunk radii: how many roots at least (up to four more), how
        //high up the trunk they leave it and how far out they reach; and how steeply their tips dive, as a share of the
        //reach past the foot's ring the palm is sunk by, so a root on a slope's downhill side still ends in the sand
        private const int SKIRT_ROOTS = 12;
        private const float SKIRT_TOP = 2.8f;
        private const float SKIRT_REACH = 2.8f;
        private const float SKIRT_DIVE = 0.45f;

        //Where a root starts, in trunk radii off the axis (#938): inside the trunk, with its first ring clear of the seven-sided
        //section's flats and of the lean the trunk has by that height (it was 0.7, with the ring touching the surface)
        private const float SKIRT_START = 0.45f;

        /// <param name="device">The device the buffers are created on.</param>
        /// <param name="height">The palm's height to the top of its heads.</param>
        /// <param name="seed">Rolls the forks, the heads and every leaf.</param>
        public DoumPalmMesh(GraphicsDevice device, float height, int seed)
            : this(device, Build(height, seed))
        {
        }

        private DoumPalmMesh(GraphicsDevice device, DoumGeometry g)
        {
            BaseRadius = g.BaseRadius;
            Wood = new UploadedMesh(device, g.WoodVertices, g.WoodIndices, g.WoodBounds);
            Fronds = new UploadedMesh(device, g.LeafVertices, g.LeafIndices, g.LeafBounds);
            Volume = g.Volume;
        }

        /// <summary>
        /// The palm as lists, with no device (#938): what the constructor uploads, and what a test reads to hold the wood shut.
        /// Every run of wood is one sweep with its free end closed, and a fork's or head's end is a dome the next pieces start
        /// inside, so no ring stands open where wood meets wood; the leaves' ribbons are open by design.
        /// </summary>
        internal static DoumGeometry Build(float height, int seed)
        {
            Random rng = new(seed);
            var wood = new List<VertexPositionNormalTexture>();
            var woodIdx = new List<short>();
            var leaf = new List<VertexPositionNormalTexture>();
            var leafIdx = new List<short>();

            float trunkR = height * 0.032f * (0.9f + 0.2f * (float)rng.NextDouble());
            const int SEG = 7;

            //The trunk to the first fork, leaning a little the way the tree will spread.
            float forkY = height * (0.38f + 0.12f * (float)rng.NextDouble());
            float leanA = (float)rng.NextDouble() * MathHelper.TwoPi;
            Vector3 fork = new(MathF.Cos(leanA) * height * 0.03f, forkY, MathF.Sin(leanA) * height * 0.03f);
            //Its foot is a flat floor under the ground; its top a dome the two arms start inside (they leave at an angle, and a
            //flat cut would stand open between them)
            TubeGeometry.AddSweep(wood, woodIdx, SEG, new[] { new Vector3(0f, -height * 0.02f, 0f), fork }, new[] { trunkR * FOOT_FLARE, trunkR },
                capStart: true, capEnd: true, endDome: JOINT_DOME);

            //The foot (#670): the doum's skirt of exposed roots, a cone of them fanning out of the trunk into the sand
            //as the references drew it, where the trunk stood on the plain as a cut post. Off their own dice, so the
            //palm above is the palm it was.
            Random footDice = new(unchecked(seed * 31 + 670));
            int skirt = SKIRT_ROOTS + footDice.Next(5);
            for (int r = 0; r < skirt; r++)
            {
                float ra = MathHelper.TwoPi * r / skirt + (float)(footDice.NextDouble() - 0.5) * 0.35f;
                Vector3 rd = new(MathF.Cos(ra), 0f, MathF.Sin(ra));
                Vector3 leave = rd * (trunkR * SKIRT_START) + Vector3.Up * (trunkR * SKIRT_TOP * (0.7f + 0.5f * (float)footDice.NextDouble()));
                float rootReach = trunkR * SKIRT_REACH * (0.85f + 0.3f * (float)footDice.NextDouble());
                Vector3 end = rd * rootReach - Vector3.Up * ((rootReach - trunkR * FOOT_FLARE) * SKIRT_DIVE + trunkR * 0.2f);
                TubeGeometry.AddSweep(wood, woodIdx, 4, new[] { leave, end }, new[] { trunkR * 0.24f, trunkR * 0.1f }, capEnd: true);
            }

            //Two arms from the fork, opposite each other and leaning apart; one of them may fork again
            //half-way up. Every arm ends in a head.
            var heads = new List<(Vector3 at, Vector3 lean)>();
            float armA = (float)rng.NextDouble() * MathHelper.TwoPi;
            float reach = 0f;
            for (int arm = 0; arm < 2; arm++)
            {
                float a = armA + arm * MathF.PI + (float)(rng.NextDouble() - 0.5) * 0.5f;
                Vector3 dir = new(MathF.Cos(a), 0f, MathF.Sin(a));
                float rise = height - forkY;
                float spread = height * (0.12f + 0.1f * (float)rng.NextDouble());
                bool again = arm == 0 && rng.NextDouble() < 0.6;
                Vector3 top = fork + dir * spread + Vector3.Up * (rise * (again ? 0.55f : 1f));
                Vector3 mid = fork + dir * (spread * 0.6f) + Vector3.Up * (rise * (again ? 0.3f : 0.55f));
                //One sweep through the knee; it leaves the fork's dome from inside it and ends in a dome of its own, which the
                //next fork's arms or the head's stalks start inside
                TubeGeometry.AddSweep(wood, woodIdx, SEG, new[] { fork, mid, top }, new[] { trunkR * ARM_START, trunkR * 0.92f, trunkR * 0.8f },
                    capEnd: true, endDome: JOINT_DOME);
                if (again)
                {
                    for (int sub = 0; sub < 2; sub++)
                    {
                        float sa = a + (sub == 0 ? 0.9f : -0.9f) + (float)(rng.NextDouble() - 0.5) * 0.4f;
                        Vector3 sdir = new(MathF.Cos(sa), 0f, MathF.Sin(sa));
                        Vector3 stop = top + sdir * (height * 0.1f) + Vector3.Up * (rise * (0.35f + 0.1f * (float)rng.NextDouble()));
                        TubeGeometry.AddSweep(wood, woodIdx, SEG, new[] { top, stop }, new[] { trunkR * 0.8f * ARM_START, trunkR * 0.65f },
                            capEnd: true, endDome: JOINT_DOME);
                        heads.Add((stop, sdir));
                        reach = MathF.Max(reach, new Vector2(stop.X, stop.Z).Length());
                    }
                }
                else
                {
                    heads.Add((top, dir));
                    reach = MathF.Max(reach, new Vector2(top.X, top.Z).Length());
                }
            }

            //A head: seven to ten stalks radiating up and out, each carrying a fan of stiff blades that
            //spreads in the plane of the stalk — a doum's leaf is a fan, held up rather than hanging.
            float stalkLen = height * 0.13f;
            float bladeLen = height * 0.17f;
            foreach ((Vector3 at, Vector3 lean) in heads)
            {
                int leaves = 7 + rng.Next(4);
                float baseYaw = (float)rng.NextDouble() * MathHelper.TwoPi;
                for (int l = 0; l < leaves; l++)
                {
                    float yaw = baseYaw + MathHelper.TwoPi * l / leaves + (float)(rng.NextDouble() - 0.5) * 0.4f;
                    float pitch = 0.15f + 0.9f * (float)rng.NextDouble(); //radians above horizontal
                    Vector3 d = Vector3.Normalize(new Vector3(MathF.Cos(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Sin(yaw) * MathF.Cos(pitch)));
                    Vector3 stalkEnd = at + d * (stalkLen * (0.8f + 0.4f * (float)rng.NextDouble()));
                    TubeGeometry.AddSweep(wood, woodIdx, 4, new[] { at, stalkEnd }, new[] { trunkR * 0.18f, trunkR * 0.08f }, capEnd: true);

                    //The fan: blades from the stalk's end, spread ±65° about the stalk's own direction in a
                    //plane rolled about it, each blade drooping a little the further from the middle it is.
                    Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.Up, d));
                    Vector3 lift = Vector3.Cross(d, side);
                    float roll = (float)(rng.NextDouble() - 0.5) * 1.2f;
                    Vector3 spreadAxis = side * MathF.Cos(roll) + lift * MathF.Sin(roll);
                    Vector3 fanNormal = Vector3.Normalize(Vector3.Cross(d, spreadAxis));
                    int blades = 7;
                    float len = bladeLen * (0.85f + 0.3f * (float)rng.NextDouble());
                    for (int b = 0; b < blades; b++)
                    {
                        float ang = MathHelper.ToRadians(-65f + 130f * b / (blades - 1));
                        Vector3 bd = Vector3.Normalize(d * MathF.Cos(ang) + spreadAxis * MathF.Sin(ang));
                        float droop = 0.12f * (1f - MathF.Cos(ang));
                        Vector3 tip = stalkEnd + bd * len - Vector3.Up * (len * droop);
                        Vector3 across = Vector3.Normalize(Vector3.Cross(bd, fanNormal)) * (len * 0.055f);
                        Vector3 n = Vector3.Normalize(Vector3.Cross(across, tip - stalkEnd));
                        TubeGeometry.AddRibbon(leaf, leafIdx, stalkEnd - across * 0.6f, stalkEnd + across * 0.6f, tip + across * 0.15f, tip - across * 0.15f, n);
                        TubeGeometry.AddRibbon(leaf, leafIdx, stalkEnd - across * 0.6f, stalkEnd + across * 0.6f, tip + across * 0.15f, tip - across * 0.15f, -n);
                    }
                    reach = MathF.Max(reach, new Vector2(stalkEnd.X, stalkEnd.Z).Length() + len);
                }
            }

            var geometry = new DoumGeometry
            {
                BaseRadius = trunkR * FOOT_FLARE,
                WoodVertices = wood, WoodIndices = woodIdx, LeafVertices = leaf, LeafIndices = leafIdx,
                WoodBounds = new BoundingSphere(new Vector3(0f, height * 0.5f, 0f), reach + height * 0.55f),
                LeafBounds = new BoundingSphere(new Vector3(0f, height * 0.85f, 0f), reach + height * 0.3f),

                //The trunk to its fork and the crown of heads, as slabs (#653): the crown is a squat cylinder over the heads' reach
                Volume = new Slab[]
                {
                    new(0f, forkY, trunkR * 1.1f),
                    new(height * 0.7f, height * 1.05f, MathF.Max(trunkR, reach * 0.8f)),
                },
            };
            return geometry;
        }

        public void Dispose()
        {
            (Wood as IDisposable)?.Dispose();
            (Fronds as IDisposable)?.Dispose();
        }
    }

    /// <summary>A doum palm as lists, before any device (#938): <see cref="DoumPalmMesh.Build"/>'s answer.</summary>
    internal sealed class DoumGeometry
    {
        public float BaseRadius;
        public List<VertexPositionNormalTexture> WoodVertices;
        public List<short> WoodIndices;
        public BoundingSphere WoodBounds;
        public List<VertexPositionNormalTexture> LeafVertices;
        public List<short> LeafIndices;
        public BoundingSphere LeafBounds;
        public Slab[] Volume;
    }
}
