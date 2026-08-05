using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The gun carriage's frame, one mesh: two cheek plates the barrel's trunnions ride in, the axle the
    /// wheels turn on, and a <b>split trail</b> — two beams diverging down and back. The split is not a look:
    /// the barrel is modelled about its trunnions with half its length behind them, so at high elevation the
    /// breech sweeps down and back exactly where a single central trail would stand, and the recoil stroke
    /// throws it further still — the breech has to dip <i>between</i> the trail's legs, which is what a split
    /// trail is for on a real gun too.
    /// <para>
    /// Local frame is <see cref="CannonMesh"/>'s: the muzzle side towards −Z, up +Y, origin at the trunnions —
    /// but the carriage is drawn <b>level</b> (yawed with the aim's heading, never pitched with it; see
    /// <c>Cannon.CarriageWorld</c>), because the whole point of trunnions is that the tube elevates and the
    /// carriage does not. The wheels are <see cref="GunWheelMesh"/>'s own instances on the axle line, not part
    /// of this mesh — they spin, this does not.
    /// </para>
    /// <para>
    /// Origin quirk worth naming: nothing here touches y = 0 — the frame hangs entirely below the trunnion
    /// axis it is drawn at, and how far below (the axle drop) is the caller's figure, arriving as
    /// <paramref name="axleDrop"/> so the frame and the wheels it was sized around cannot drift apart.
    /// </para>
    /// </summary>
    public class GunCarriageMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private const int AXLE_SEGMENTS = 12;

        /// <param name="cheekInnerX">Inner face of each cheek plate off the barrel's axis — a hair over the
        /// tube's outer radius, so the plates hug the barrel without clipping it.</param>
        /// <param name="cheekThickness">Each plate's thickness along the axle.</param>
        /// <param name="cheekTopY">Top edge of the plates, a little above the trunnion axis they hold.</param>
        /// <param name="axleDrop">How far below the trunnions the axle runs — the wheels' centre height.</param>
        /// <param name="cheekHalfLength">Half the plates' run along the barrel.</param>
        /// <param name="axleRadius">The axle bar's radius.</param>
        /// <param name="axleHalfLength">Half the axle's length — reaching into the wheels' hubs.</param>
        /// <param name="trailEnd">Where the +X side's trail leg ends, in the carriage's own frame
        /// (x &gt; 0 outward, y &lt; 0 below the trunnions, z &gt; 0 back); the other leg mirrors it in X.
        /// The legs run from the cheeks' lower rear corners to here.</param>
        public GunCarriageMesh(GraphicsDevice graphicsDevice, float cheekInnerX, float cheekThickness,
            float cheekTopY, float axleDrop, float cheekHalfLength, float axleRadius, float axleHalfLength,
            Vector3 trailEnd)
        {
            MeshBuilder builder = new();

            //The cheeks: plain plates from just above the trunnion line down past the axle, so the axle reads
            //as carried by them rather than floating alongside
            float cheekBottomY = -axleDrop - axleRadius * 1.6f;
            float cheekCentreY = (cheekTopY + cheekBottomY) * 0.5f;
            float cheekHalfHeight = (cheekTopY - cheekBottomY) * 0.5f;
            float cheekCentreX = cheekInnerX + cheekThickness * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                builder.AddBox(new Vector3(side * cheekCentreX, cheekCentreY, 0f),
                    new Vector3(cheekThickness * 0.5f, 0f, 0f),
                    new Vector3(0f, cheekHalfHeight, 0f),
                    new Vector3(0f, 0f, cheekHalfLength));

                //A trail leg: an oriented beam from the cheek's lower rear corner, diverging outward as it
                //falls back — the basis is built off its own direction, so the beam's faces stay square to it
                Vector3 start = new(side * cheekCentreX, -axleDrop * 0.75f, cheekHalfLength * 0.8f);
                Vector3 end = new(side * trailEnd.X, trailEnd.Y, trailEnd.Z);

                Vector3 along = (end - start) * 0.5f;
                Vector3 direction = Vector3.Normalize(along);
                Vector3 sideways = Vector3.Normalize(Vector3.Cross(Vector3.Up, direction));
                Vector3 upright = Vector3.Cross(direction, sideways);

                builder.AddBox((start + end) * 0.5f, sideways * 0.10f, upright * 0.08f, along);

                //The leg's foot: a small spade plate standing across the end, the detail that says the trail
                //is meant to bite ground rather than merely stop
                builder.AddBox(end + direction * 0.06f, sideways * 0.16f, upright * 0.16f, direction * 0.05f);
            }

            //The axle, through both cheeks and into the wheels' hubs
            builder.AddTubeX(new Vector3(0f, -axleDrop, 0f), axleHalfLength, axleRadius, AXLE_SEGMENTS);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);

            float reach = MathF.Max(axleHalfLength, MathF.Max(trailEnd.Z, axleDrop - trailEnd.Y));
            BoundingSphere = new BoundingSphere(new Vector3(0f, -axleDrop * 0.5f, trailEnd.Z * 0.35f), reach);
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}
