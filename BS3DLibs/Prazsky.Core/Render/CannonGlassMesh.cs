using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The pane that glazes the cannon's loading window: a thin, curved shell of glass set into the rebate
    /// <see cref="CannonMesh"/> cuts along the top of the tube, with the <b>round that fires next notched out
    /// of its front edge</b> — open to the air, where the rest of the queue reads through blue glass.
    /// <para>
    /// The notch is <b>half an ellipse</b>, its semi-axis across the pane being the pane's full half-width and
    /// its rim reaching <paramref name="notchReach"/> back along the bore from the front ball's centre. It is a
    /// half and not a whole because there is no room for the other half: the queue is enclosed exactly, the
    /// muzzle face sitting one ball radius ahead of the front ball's centre (see <see cref="CannonMesh"/>), so
    /// glass ahead of the notch would have to fit in a gap of zero. The ellipse therefore opens forward through
    /// the muzzle face and its rim curves round the <i>back</i> of the queue.
    /// </para>
    /// <para>
    /// <b>How far back it reaches is the caller's figure, and #204 is why it is not simply a ball radius.</b>
    /// It was: the rim landed on the front ball's centre plane at the pane's cheeks and where that ball parts
    /// from the one behind it at the centreline. That reads correctly on the prop and hid the front ball anyway,
    /// because a player does not look along the bore — they look from the precise-aim lens, which stands above
    /// and behind the muzzle. From there the sight line grazing the old rim passed the front ball at a closest
    /// approach of 0.512 against its radius of 0.5: it missed the ball by 0.012, so <i>nothing</i> of the round
    /// about to fire was seen through open air and all of it was read through the pane. The issue reported "at
    /// best a slice"; the geometry says none. The reach is a derived constant on the rig now
    /// (<c>CannonRig.GLASS_NOTCH_REACH</c>), sized so no part of the front ball is behind glass from any angle
    /// across the window and the lens's own sight line clears the rim as well — which necessarily uncovers the
    /// second round too, and the issue licenses exactly that.
    /// </para>
    /// <para>
    /// So the pane no longer says on its own which ball fires next, and what does is #175's breathing mark on
    /// the muzzle slot. <b>That mark is now load-bearing rather than a reinforcement</b>: with two rounds open
    /// at the top of the window it is the only thing that distinguishes the first from the second.
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
        /// <param name="frontBallZ">Z of the head-of-queue ball's centre — what the notch is measured from, so
        /// the opening and the ball it uncovers cannot disagree.</param>
        /// <param name="ballRadius">One loaded ball's radius. The rim lands a radius back from the front ball's
        /// centre at the pane's <b>cheeks</b>, which is the seam between the first two rounds, so nothing of the
        /// front ball is behind glass at any angle across the window.</param>
        /// <param name="notchReach">How far back along the bore the rim reaches at the pane's <b>centreline</b>,
        /// from the front ball's centre — see the class remarks and #204. Must be at least
        /// <paramref name="ballRadius"/>; at exactly that the notch degenerates to a straight cut on the seam,
        /// and beyond it the difference is the ellipse's own semi-axis along the bore.</param>
        /// <param name="segments">Steps across the pane, which are also the steps around the notch's ellipse
        /// (they are one and the same boundary) — the notch is the curve the eye reads, so this is its
        /// smoothness rather than the barrel's.</param>
        public CannonGlassMesh(GraphicsDevice graphicsDevice, float boreRadius, float thickness, float seat,
            float slotHalfAngle, float slotEndZ, float frontBallZ, float ballRadius, float notchReach,
            int segments)
        {
            float inner = boreRadius;
            float outer = boreRadius + thickness;

            //The pane's own span: the window's, pulled in by the reveal on each cheek (an arc length, so it is
            //an angle at the bore) and ahead of the lip the window ends at
            float halfAngle = MathF.Max(slotHalfAngle - seat / boreRadius, 0f);
            float backZ = slotEndZ - seat;

            int spans = Math.Max(4, segments);

            //One station per step of the ellipse's own parameter, ψ from −π/2 to +π/2: across the pane the
            //angle runs as sin ψ and the notch's edge stands (boreHalf × cos ψ) behind the SEAM the rim lands
            //on at the cheeks, which is the ellipse. Parametrised that way rather than by the angle so the
            //stations stay even round the curve — sampling the angle evenly and solving for Z gives a boundary
            //whose steps grow without bound at the pane's edges, where the ellipse turns to run along the bore.
            var dirs = new Vector3[spans + 1];
            var frontZ = new float[spans + 1];
            var rimNormals = new Vector3[spans + 1];

            //The ellipse's semi-axis across the pane, as an arc length at the face the notch is read on — the
            //aspect the rim's own normals are turned by
            float arcHalf = halfAngle * outer;

            //Where the rim sits at the cheeks, and its semi-axis along the bore from there. The ellipse is
            //OFFSET rather than merely wider (#204): the cheeks stay on the first seam of the queue, which is
            //what keeps the front ball clear of glass at every angle across the window, and the centreline
            //reaches notchReach back from the front ball's centre, which is what clears the precise-aim lens's
            //own sight line. Clamped at zero so a reach of exactly a ball radius degenerates to a straight cut
            //on the seam instead of turning the ellipse inside out.
            float seamZ = frontBallZ + ballRadius;
            float boreHalf = MathF.Max(notchReach - ballRadius, 0f);

            for (int i = 0; i <= spans; i++)
            {
                float psi = -Constants.HALF_PI + MathHelper.Pi * i / spans;
                float sin = MathF.Sin(psi);
                float cos = MathF.Cos(psi);

                float angle = Constants.HALF_PI + halfAngle * sin;

                dirs[i] = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
                frontZ[i] = seamZ + boreHalf * cos;

                //The rim faces INTO the notch, which is the ellipse's inward normal: the gradient of
                //(s/arcHalf)² + (z/boreHalf)² at the station, negated. At the deepest point that is
                //straight down the bore towards the muzzle; at the pane's edges it is purely across the slot,
                //the boundary running along the bore there — which is also why the rim and the long edge meet
                //without a sliver. Written as the gradient times both semi-axes — (sin·boreHalf, cos·arcHalf)
                //rather than (sin/arcHalf, cos/boreHalf) — which is the same direction with no divide, so a
                //degenerate span cannot put an infinity into a normal.
                //
                //It has to be boreHalf and NOT ballRadius, which is the one thing #204 could have got wrong
                //silently: the semi-axis along the bore stopped being a ball radius when the ellipse was
                //offset, and a normal computed from the old one would light a rim whose shape it no longer
                //describes — wrong shading on the very curve this pane is read by, with nothing missing.
                Vector3 tangent = new(-dirs[i].Y, dirs[i].X, 0f);
                Vector3 gradient = tangent * (sin * boreHalf) + new Vector3(0f, 0f, cos * arcHalf);

                //A reach of exactly a ball radius leaves boreHalf at zero, and then the gradient vanishes at
                //the cheeks alone (sin = ±1, cos = 0) where every other station still has its Z term. That is
                //the flat-cut case, whose rim faces straight down the bore at every station, so it is answered
                //rather than normalized — the old ellipse could not reach zero on either axis and this one can.
                rimNormals[i] = gradient.LengthSquared() > 0f
                    ? -Vector3.Normalize(gradient)
                    : Vector3.Forward;
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

            //The two long edges, each facing the window cheek it is seated against. They start on the first
            //seam of the queue, the ellipse having landed there (it landed on the front ball's own centre plane
            //until #204, which is precisely what left that ball glazed).
            AddSideEdge(builder, dirs[0], inner, outer, frontZ[0], backZ, outward: -1f);
            AddSideEdge(builder, dirs[spans], inner, outer, frontZ[spans], backZ, outward: +1f);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);

            //Axis-centred and conservative, like the barrel's: the pane hugs the tube, so a sphere about the
            //bore's own axis costs nothing anyone measures. Measured from the front ball's centre rather than
            //from where the glass now actually starts, which only makes it looser and therefore still correct.
            float halfLength = (backZ - frontBallZ) * Constants.HALF;

            BoundingSphere = new BoundingSphere(new Vector3(0f, 0f, (frontBallZ + backZ) * Constants.HALF),
                MathF.Sqrt(outer * outer + halfLength * halfLength));
        }

        /// <summary>One long edge of the pane: a pane-thick quad at a fixed angle, from where the rim lands at
        /// the cheeks back to the pane's end, facing out of the window towards the cheek it is seated
        /// against.</summary>
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
