using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One planting drawn as shaped shrubs near the lens and as its domes beyond (#1195): on High and Ultra
    /// (<see cref="SceneRenderer.FineMeshes"/>) an instance whose foot stands within <see cref="RadiusFor"/> of this frame's lens,
    /// horizontally, is drawn by the buckets that carry this swap with <see cref="ScatterBucket.FineMeshes"/> <c>true</c> (its
    /// shrub's wood and leaf), and every other one by the bucket that carries it with <see cref="ScatterBucket.FineMeshes"/>
    /// <c>null</c> (the dome). On Low and Medium the dome bucket draws all of them from its own buffer, as it always did, and the
    /// Potato path never looks here. So what a plant is drawn as follows its distance from the lens frame by frame, as the aurora
    /// stand's spires do (<see cref="StandSpruces"/>, #1163), and a dome is left only where the owner allowed one: "only when they
    /// are very far from the camera and the detail would be too expensive".
    /// <para>
    /// The planting's instances are dealt round <see cref="Shapes"/> shrubs in turn (<see cref="ShapeOf"/>), the thorn bushes'
    /// rule (#883), so several shapes stand where one dome mesh did; each shape's buckets draw <see cref="Near"/> of their own.
    /// </para>
    /// <para>
    /// A pass (<see cref="Select"/>) takes the instances whose bounds meet its frustum and splits them by the lens: the camera's
    /// pass by the camera's frustum, the sun's by its map's box, both by the same lens (the sun's pass is handed the frame's,
    /// <c>BackdropServices.ShadowLens</c>), so a plant casts what it is drawn as, and nothing it casts follows the lens's turn.
    /// An instance outside the frustum puts nothing on the screen or into the map, so the cut is exact, and it spares the GPU
    /// the vertices of the plants behind the lens.
    /// </para>
    /// <para>
    /// Allocation-free: the arrays a pass fills and the instance buffers it uploads them to are made once, as long as the
    /// planting, and each pass fills and uploads them before its draws (<see cref="SetDataOptions.Discard"/>, so the GPU is not
    /// stalled on the pass before). The selection itself is device-free, so a test reaches the whole rule.
    /// </para>
    /// </summary>
    public sealed class ScatterSwap : IDisposable
    {
        /// <summary>The instances as planted: the dome's bucket's, in its order.</summary>
        public IReadOnlyList<ModelInstance> Placed => _placed;

        /// <summary>How many shrubs the instances are dealt round.</summary>
        public int Shapes { get; }

        /// <summary>How near the lens, horizontally from the foot, a plant is drawn shaped on High.</summary>
        public float Radius { get; }

        /// <summary>The same on Ultra (<see cref="SceneRenderer.FarDetail"/>, #1208's rung).</summary>
        public float FarDetailRadius { get; }

        /// <summary>This pass's plants within the radius, by the shape they wear (the first <see cref="NearCount"/> of each),
        /// drawn by that shape's buckets.</summary>
        public ModelInstance[][] Near { get; }
        public int[] NearCount { get; }

        /// <summary>This pass's plants beyond it (the first <see cref="FarCount"/>), drawn by the dome.</summary>
        public ModelInstance[] Far { get; }
        public int FarCount { get; private set; }

        private readonly ModelInstance[] _placed;

        //Each instance's bounds in the world: the sphere round every mesh it is drawn or cast with, at its frame (the plants never
        //move, so worked out once)
        private readonly BoundingSphere[] _bounds;

        private readonly DynamicVertexBuffer[] _nearBuffers;
        private readonly DynamicVertexBuffer _farBuffer;

        /// <param name="device">The device the instance buffers live on; null for a planting with no device (a test).</param>
        /// <param name="placed">The instances, as the dome's bucket holds them.</param>
        /// <param name="shapes">How many shrubs they are dealt round (<see cref="ShapeOf"/>).</param>
        /// <param name="local">The sphere round every mesh the plant is drawn or cast with, at scale 1 in its own space.</param>
        /// <param name="radius">The swap's radius on High.</param>
        /// <param name="farDetailRadius">The swap's radius on Ultra.</param>
        public ScatterSwap(GraphicsDevice device, IReadOnlyList<ModelInstance> placed, int shapes, BoundingSphere local, float radius,
            float farDetailRadius)
        {
            _placed = new ModelInstance[placed.Count];
            for (int i = 0; i < placed.Count; i++) _placed[i] = placed[i];
            Shapes = Math.Max(shapes, 1);
            Radius = radius;
            FarDetailRadius = farDetailRadius;

            Near = new ModelInstance[Shapes][];
            NearCount = new int[Shapes];
            for (int k = 0; k < Shapes; k++) Near[k] = new ModelInstance[Dealt(_placed.Length, Shapes, k)];
            Far = new ModelInstance[_placed.Length];

            _bounds = new BoundingSphere[_placed.Length];
            for (int i = 0; i < _placed.Length; i++)
            {
                Matrix world = _placed[i].World;
                float scale = MathF.Sqrt(world.M11 * world.M11 + world.M12 * world.M12 + world.M13 * world.M13);
                _bounds[i] = new BoundingSphere(Vector3.Transform(local.Center, world), local.Radius * scale);
            }

            if (device == null || _placed.Length == 0) return;
            _nearBuffers = new DynamicVertexBuffer[Shapes];
            for (int k = 0; k < Shapes; k++)
                if (Near[k].Length > 0)
                    _nearBuffers[k] = new DynamicVertexBuffer(device, ModelInstance.VertexDeclaration, Near[k].Length, BufferUsage.WriteOnly);
            _farBuffer = new DynamicVertexBuffer(device, ModelInstance.VertexDeclaration, _placed.Length, BufferUsage.WriteOnly);
        }

        /// <summary>The shrub the <paramref name="index"/>th instance wears: dealt round <paramref name="shapes"/> in turn.</summary>
        public static int ShapeOf(int index, int shapes) => index % shapes;

        /// <summary>How many of <paramref name="count"/> instances dealt round <paramref name="shapes"/> wear shape <paramref name="shape"/>.</summary>
        private static int Dealt(int count, int shapes, int shape) => count / shapes + (shape < count % shapes ? 1 : 0);

        /// <summary>The instances of <paramref name="placed"/> that wear shape <paramref name="shape"/> of <paramref name="shapes"/>,
        /// in their order: what that shape's buckets are built off.</summary>
        public static List<ModelInstance> Dealt(IReadOnlyList<ModelInstance> placed, int shapes, int shape)
        {
            var dealt = new List<ModelInstance>(Dealt(placed.Count, shapes, shape));
            for (int i = shape; i < placed.Count; i += shapes) dealt.Add(placed[i]);
            return dealt;
        }

        /// <summary>
        /// The radius the swap is made at this frame: <see cref="FarDetailRadius"/> on Ultra (<paramref name="farDetail"/>),
        /// <see cref="Radius"/> on High, and every plant under <see cref="RenderDetail.Maximum"/> (the trailer: a film must not pop).
        /// Only where <see cref="SceneRenderer.FineMeshes"/> is on does a caller make the swap at all.
        /// </summary>
        public float RadiusFor(bool farDetail) => RenderDetail.Maximum ? float.PositiveInfinity : farDetail ? FarDetailRadius : Radius;

        /// <summary>Whether a plant whose foot is at <paramref name="foot"/> is drawn shaped from <paramref name="lens"/>: within
        /// <paramref name="radius"/> of it horizontally.</summary>
        public static bool IsNear(Vector3 foot, Vector3 lens, float radius)
        {
            float dx = foot.X - lens.X, dz = foot.Z - lens.Z;
            return dx * dx + dz * dz < radius * radius;
        }

        /// <summary>
        /// This pass's split: of the plants whose bounds meet <paramref name="frustum"/>, those within <paramref name="radius"/> of
        /// <paramref name="lens"/> into <see cref="Near"/> by the shape each wears and the rest into <see cref="Far"/>, in the
        /// planting's order, and uploaded where there is a device.
        /// </summary>
        public void Select(BoundingFrustum frustum, Vector3 lens, float radius)
        {
            for (int k = 0; k < Shapes; k++) NearCount[k] = 0;
            int far = 0;
            for (int i = 0; i < _placed.Length; i++)
            {
                if (frustum.Contains(_bounds[i]) == ContainmentType.Disjoint) continue;
                if (IsNear(_placed[i].World.Translation, lens, radius))
                {
                    int k = ShapeOf(i, Shapes);
                    Near[k][NearCount[k]++] = _placed[i];
                }
                else Far[far++] = _placed[i];
            }
            FarCount = far;

            if (_farBuffer == null) return;
            for (int k = 0; k < Shapes; k++)
                if (NearCount[k] > 0) _nearBuffers[k].SetData(Near[k], 0, NearCount[k], SetDataOptions.Discard);
            if (far > 0) _farBuffer.SetData(Far, 0, far, SetDataOptions.Discard);
        }

        /// <summary>What <paramref name="bucket"/> (one carrying this swap) draws this pass, as its instance buffer and count: the near
        /// plants of its shape for a shrub's bucket (<see cref="ScatterBucket.FineMeshes"/> <c>true</c>), the far ones for the dome.</summary>
        public (VertexBuffer Instances, int Count) For(ScatterBucket bucket) =>
            bucket.FineMeshes == true ? (_nearBuffers?[bucket.SwapShape], NearCount[bucket.SwapShape]) : (_farBuffer, FarCount);

        public void Dispose()
        {
            if (_nearBuffers != null) foreach (DynamicVertexBuffer buffer in _nearBuffers) buffer?.Dispose();
            _farBuffer?.Dispose();
        }
    }
}
