using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The pane that glazes the cannon's loading window: a thin, curved shell of glass set into the rebate
    /// <see cref="CannonMesh"/> cuts along the top of the tube, with the <b>head-of-queue ball's own extent
    /// notched out of its front edge</b>. So the round that is about to fire is open to the air and the four
    /// queued behind it read through blue glass — which is the whole point of the pane: it says which ball
    /// fires next without a mark on the HUD, by covering every ball but that one.
    /// <para>
    /// The notch is <b>half an ellipse</b>, semi-axes a ball radius along the bore and the pane's full
    /// half-width across it, centred on the front ball. It is a half and not a whole because there is no room
    /// for the other half: the queue is enclosed exactly, the muzzle face sitting one ball radius ahead of the
    /// front ball's centre (see <see cref="CannonMesh"/>), so glass ahead of the notch would have to fit in a
    /// gap of zero. The ellipse therefore opens forward through the muzzle face and its rim curves round the
    /// <i>back</i> of the front ball, landing at rest exactly where that ball parts from the one behind it —
    /// which is what makes the post-shot glide read: the next round slides out from under the glass into the
    /// notch as it takes the muzzle slot.
    /// </para>
    /// <para>
    /// It is a shell rather than a surface: an outer face, the underside seen up the bore through the muzzle,
    /// and rim strips closing the notch and the two long edges, so the glass has visible thickness where it
    /// is cut and cannot be looked into edge-on. The pane is held off the steel it is set into by a small
    /// reveal on every cut edge — the two cheeks of the window and the lip the window ends at — because a
    /// glass face flush with a steel one is two coplanar surfaces fighting over the depth buffer. The reveal
    /// reads as the shadow line of a pane seated in a frame, which is what it is.
    /// </para>
    /// <para>
    /// Sized so it stays <b>inside</b> the rebate at every station of the barrel's profile: the pane's outer
    /// radius is the bore plus its own thickness, which is under the slimmest steel the window crosses (the
    /// chase dip), so the pane never proves proud of the tube. Wound clockwise seen from outside through
    /// <see cref="MeshBuilder"/>, like every procedural mesh here.
    /// </para>
    /// </summary>
    public class CannonGlassMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <param name="boreRadius">Inner radius of the tube: where the pane is seated, so it lies at the
        /// bottom of the window's rebate with the steel rim standing proud around it.</param>
        /// <param name="thickness">Radial thickness of the glass. Kept under the slimmest steel the window
        /// crosses, or the pane would prove proud of the tube where the chase dips.</param>
        /// <param name="seat">The reveal the pane is held off the window's steel by, in world units, on both
        /// cheeks and at the lip the window ends at — see the class remarks on why it is not flush.</param>
        /// <param name="slotHalfAngle">Half-width of the window itself, in radians from straight up
        /// (<c>CannonRig.SLOT_HALF_ANGLE</c>); the pane spans it less the reveal.</param>
        /// <param name="slotEndZ">Where the window stops, towards the breech; the pane stops the reveal ahead
        /// of it.</param>
        /// <param name="frontBallZ">Z of the head-of-queue ball's centre — the notch's own centre, so the
        /// opening and the ball it uncovers cannot disagree.</param>
        /// <param name="ballRadius">The notch's semi-axis along the bore: a ball radius, so the opening is the
        /// front ball's own extent.</param>
        /// <param name="segments">Steps across the pane, which are also the steps around the notch's ellipse
        /// (they are one and the same boundary) — the notch is the curve the eye reads, so this is its
        /// smoothness rather than the barrel's.</param>
        public CannonGlassMesh(GraphicsDevice graphicsDevice, float boreRadius, float thickness, float seat,
            float slotHalfAngle, float slotEndZ, float frontBallZ, float ballRadius, int segments)
        {
            float inner = boreRadius;
            float outer = boreRadius + thickness;

            //The pane's own span: the window's, pulled in by the reveal on each cheek (an arc length, so it is
            //an angle at the bore) and ahead of the lip the window ends at
            float halfAngle = MathF.Max(slotHalfAngle - seat / boreRadius, 0f);
            float backZ = slotEndZ - seat;

            int spans = Math.Max(4, segments);

            //One station per step of the ellipse's own parameter, ψ from −π/2 to +π/2: across the pane the
            //angle runs as sin ψ and the notch's edge stands (ball radius × cos ψ) behind the front ball's
            //centre, which is the ellipse. Parametrised that way rather than by the angle so the stations
            //stay even round the curve — sampling the angle evenly and solving for Z gives a boundary whose
            //steps grow without bound at the pane's edges, where the ellipse turns to run along the bore.
            var dirs = new Vector3[spans + 1];
            var frontZ = new float[spans + 1];
            var rimNormals = new Vector3[spans + 1];

            //The ellipse's semi-axis across the pane, as an arc length at the face the notch is read on — the
            //aspect the rim's own normals are turned by
            float arcHalf = halfAngle * outer;

            for (int i = 0; i <= spans; i++)
            {
                float psi = -Constants.HALF_PI + MathHelper.Pi * i / spans;
                float sin = MathF.Sin(psi);
                float cos = MathF.Cos(psi);

                float angle = Constants.HALF_PI + halfAngle * sin;

                dirs[i] = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
                frontZ[i] = frontBallZ + ballRadius * cos;

                //The rim faces INTO the notch, which is the ellipse's inward normal: the gradient of
                //(s/arcHalf)² + (z/ballRadius)² at the station, negated. At the deepest point that is
                //straight down the bore towards the muzzle; at the pane's edges it is purely across the slot,
                //the boundary running along the bore there — which is also why the rim and the long edge meet
                //without a sliver. Written as the gradient times both semi-axes — (sin·ballRadius, cos·arcHalf)
                //rather than (sin/arcHalf, cos/ballRadius) — which is the same direction with no divide, so a
                //degenerate span cannot put an infinity into a normal.
                Vector3 tangent = new(-dirs[i].Y, dirs[i].X, 0f);

                rimNormals[i] = -Vector3.Normalize(tangent * (sin * ballRadius) + new Vector3(0f, 0f, cos * arcHalf));
            }

            MeshBuilder builder = new();

            for (int i = 0; i < spans; i++)
            {
                Vector3 d0 = dirs[i];
                Vector3 d1 = dirs[i + 1];
                Vector3 mid = Vector3.Normalize(d0 + d1);

                Vector3 outer0Front = d0 * outer + new Vector3(0f, 0f, frontZ[i]);
                Vector3 outer1Front = d1 * outer + new Vector3(0f, 0f, frontZ[i + 1]);
                Vector3 outer1Back = d1 * outer + new Vector3(0f, 0f, backZ);
                Vector3 outer0Back = d0 * outer + new Vector3(0f, 0f, backZ);

                Vector3 inner0Front = d0 * inner + new Vector3(0f, 0f, frontZ[i]);
                Vector3 inner1Front = d1 * inner + new Vector3(0f, 0f, frontZ[i + 1]);
                Vector3 inner1Back = d1 * inner + new Vector3(0f, 0f, backZ);
                Vector3 inner0Back = d0 * inner + new Vector3(0f, 0f, backZ);

                //The face the sky is reflected off, and the underside a lens looking up the bore sees. Both
                //smooth around the arc; along the bore the surface is a cylinder, so two stations are exact.
                builder.AddQuad(outer0Front, outer1Front, outer1Back, outer0Back, d0, d1, d1, d0, mid);
                builder.AddQuad(inner0Front, inner1Front, inner1Back, inner0Back, -d0, -d1, -d1, -d0, -mid);

                //The notch's cut edge, the one the eye reads the oval off
                Vector3 rim0 = rimNormals[i];
                Vector3 rim1 = rimNormals[i + 1];

                builder.AddQuad(inner0Front, inner1Front, outer1Front, outer0Front,
                    rim0, rim1, rim1, rim0, Vector3.Normalize(rim0 + rim1));

                //And the back edge, facing the lip the window ends at across the reveal
                builder.AddQuad(inner0Back, inner1Back, outer1Back, outer0Back,
                    Vector3.Backward, Vector3.Backward, Vector3.Backward, Vector3.Backward, Vector3.Backward);
            }

            //The two long edges, each facing the window cheek it is seated against. They start at the front
            //ball's centre plane, the ellipse having landed there.
            AddSideEdge(builder, dirs[0], inner, outer, frontZ[0], backZ, outward: -1f);
            AddSideEdge(builder, dirs[spans], inner, outer, frontZ[spans], backZ, outward: +1f);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);

            //Axis-centred and conservative, like the barrel's: the pane hugs the tube, so a sphere about the
            //bore's own axis costs nothing anyone measures
            float halfLength = (backZ - frontBallZ) * Constants.HALF;

            BoundingSphere = new BoundingSphere(new Vector3(0f, 0f, (frontBallZ + backZ) * Constants.HALF),
                MathF.Sqrt(outer * outer + halfLength * halfLength));
        }

        /// <summary>One long edge of the pane: a pane-thick quad at a fixed angle, from the front ball's centre
        /// plane back to the pane's end, facing out of the window towards the cheek it is seated against.</summary>
        private static void AddSideEdge(MeshBuilder builder, Vector3 radial, float inner, float outer,
            float frontZ, float backZ, float outward)
        {
            Vector3 faceNormal = new Vector3(-radial.Y, radial.X, 0f) * outward;

            builder.AddQuad(
                radial * inner + new Vector3(0f, 0f, frontZ),
                radial * inner + new Vector3(0f, 0f, backZ),
                radial * outer + new Vector3(0f, 0f, backZ),
                radial * outer + new Vector3(0f, 0f, frontZ),
                faceNormal, faceNormal, faceNormal, faceNormal, faceNormal);
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
