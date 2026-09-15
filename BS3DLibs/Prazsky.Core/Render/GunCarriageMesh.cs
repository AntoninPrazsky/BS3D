using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The gun carriage's frame, one mesh: two cheek plates with the trunnion pins they visibly hold the
    /// barrel by (shaped brackets with bearings since #403 — see "The cheeks"), the axle the wheels turn on,
    /// and a <b>split trail</b> — two legs diverging down and back.
    /// The split is not a look:
    /// the barrel is modelled about its trunnions with half its length behind them, so at high elevation the
    /// breech sweeps down and back exactly where a single central trail would stand, and the recoil stroke
    /// throws it further still — the breech has to dip <i>between</i> the trail's legs, which is what a split
    /// trail is for on a real gun too.
    /// <para>
    /// Local frame is <see cref="CannonMesh"/>'s: the muzzle side towards −Z, up +Y, origin at the trunnions —
    /// but the carriage is drawn <b>level</b> (yawed with the aim's heading, never pitched with it; see
    /// <c>Cannon.CarriageWorld</c>), because the whole point of trunnions is that the tube elevates and the
    /// carriage does not. The wheels are <see cref="OmniWheelMesh"/>'s own instances on the axle line, not part
    /// of this mesh — they spin, this does not.
    /// </para>
    /// <para>
    /// Origin quirk worth naming: the trunnion pins are the only thing here on y = 0 — the rest of the frame
    /// hangs entirely below the trunnion axis it is drawn at, and how far below (the axle drop) is the
    /// caller's figure, arriving as <paramref name="axleDrop"/> so the frame and the wheels it was sized
    /// around cannot drift apart. The pins live on <i>this</i> mesh and not the barrel's, and that is
    /// load-bearing: they are coaxial with the elevation axis, so a fixed pin looks identical however the
    /// tube elevates — but the Game's recoil slides the tube, and pins riding it would visibly tear along
    /// the plates that hold them.
    /// </para>
    /// <para>
    /// <b>The legs were two plain oriented boxes until #403</b>, with a small square plate for a foot — reported
    /// from play as far cruder than the wheels and the turned barrel, and they are the nearest thing to the
    /// play camera, which stands behind the gun with the trail running at the lens. Each is now a tapered,
    /// chamfered box girder with two bands clamped round it, seated in a socket on the cheek's outer face and
    /// leaving it through a collar, ending in a raked spade with a ground edge and a gusset, with a lifting
    /// handle over its foot. <b>The breech dips between the legs, so a leg may not stand further inboard or
    /// higher than the plain box did</b>: its top face is the box's exactly (<see cref="LEG_TOP_FACE"/>), and
    /// its inner face is the box's (<see cref="LEG_INNER_FACE"/>) carried <i>outboard</i> with the root, which
    /// sits on the cheek's inner plane (see <see cref="LEG_ROOT_SPARE"/>). The girder grew outward and
    /// downward only, which makes it no worse a neighbour for the breech than the box was, by construction
    /// rather than by a sweep of every elevation; the bands and the collar are the exceptions, and each sits
    /// only where arithmetic says the tube cannot reach.
    /// </para>
    /// </summary>
    public class GunCarriageMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private const int AXLE_SEGMENTS = 12;

        #region The cheeks (#403)

        //A cheek was a plain box — reported right after the legs as the other crude block of the frame, the one
        //the legs run into. It is a bracket now, in the legs' own language: an arched top round the trunnion,
        //falling in tangent lines to the shoulders and on down to a chamfered foot, its outer face chamfered all
        //round, carrying a raised bearing for the trunnion (held by a cap-square's two bolts) and another for
        //the axle, with a rib between the two.
        //
        //⚠ THE INNER FACE STAYS EXACTLY WHERE THE BOX'S WAS — it hugs the tube (cheekInnerX) — and nothing is
        //added inboard of it. The tube elevates and recoils only in the carriage's own YZ plane, so the cheek's
        //side silhouette is free, and everything proud of the outer face stays inside the wheel's inner plate
        //(1.11 off the axis on the shipped figures).

        //(The arch's radius about the trunnion axis is the caller's cheekTopY — the plates' top — for the reason
        //the axle drop is: the frame is sized in CannonRig, around the tube and the wheels it has to fit.)

        //Where the plate's front and back edges stop being vertical and lean in towards the arch, as a share of
        //the axle drop below the trunnions. It has to stay above the legs' roots (at 0.75 of the drop), or the
        //leaning edge would cut the corner the leg runs into — and it is the trail socket's top as well.
        private const float CHEEK_SHOULDER = 0.62f;
        private const float CHEEK_CHAMFER = 0.03f;       //round the outer face, of the plate's thickness
        private const float CHEEK_FOOT_CUT = 0.08f;      //the two bottom corners
        private const int CHEEK_ARCH_STEPS = 12;

        //The two bearings, as multiples of what they carry. The trunnion's ring stands proud by at most this, and
        //never so far that the pin stops being proud of it — the pin is what visibly holds the tube.
        private const float TRUNNION_BEARING_SCALE = 1.55f;
        private const float TRUNNION_BEARING_PROUD = 0.035f;
        private const float AXLE_BEARING_SCALE = 1.45f;
        private const float AXLE_BEARING_PROUD = 0.04f;
        private const int BEARING_SEGMENTS = 20;

        //The cap-square's bolts: hexagonal heads below the trunnion's ring, either side of the rib
        private const float BOLT_RADIUS = 0.04f;
        private const float BOLT_PROUD = 0.03f;
        private const float BOLT_ORBIT = 0.36f;          //off the trunnion axis
        private const float BOLT_ANGLE = 0.70f;          //radians either side of straight down
        private const int BOLT_SEGMENTS = 6;

        //The rib from one bearing to the other, a chamfered pad on the outer face
        private const float RIB_HALF_WIDTH = 0.05f;
        private const float RIB_PROUD = 0.025f;
        private const float RIB_CHAMFER = 0.015f;

        #endregion

        #region Where a leg meets its cheek (#403)

        //⚠ A LEG IS WIDER THAN THE PLATE IT RUNS INTO, and the first cut of the new legs showed it: a girder
        //0.22 across entering a plate 0.14 thick came out through BOTH faces — a sliver inboard, between the
        //cheeks where the play camera looks, and a skewed octagon of intersection lines outboard and at the back
        //edge. Reported from play as the leg visibly tunnelling through the block. No width of leg fits a plate
        //that thin, so the plate is thickened where the leg goes in instead, and the leg's exit is sleeved.
        //
        //The root is seated first: the most inboard corner of its section — the inner face less the lean of the
        //leg's upright, which is not vertical on a leg that both diverges and falls, and on the shipped figures
        //reaches 0.03 further in than the face itself — lands this far outboard of the cheek's inner plane. So
        //nothing of the leg shows between the cheeks, and every part of it moved AWAY from the tube.
        private const float LEG_ROOT_SPARE = 0.005f;

        //The socket: the cheek's lower back quarter thickened outboard, from the shoulder down to the foot, its
        //front edge leaning back, chamfered like the plate, so the root is inside solid iron and the leg leaves
        //through a flat back wall that continues the cheek's own. Its thickness is what the root's outer face
        //needs at that wall; its front edge is well clear of the axle's bearing.
        private const float SOCKET_PROUD = 0.14f;          //beyond the cheek's outer face
        private const float SOCKET_FRONT_TOP = 0.62f;      //the front edge at the shoulder, as a share of the half-length
        private const float SOCKET_FRONT_BOTTOM = 0.36f;   //and at the foot

        //The hinge the leg swings from, a boss on the socket's outer face over the root with a pin through it: a
        //split trail's legs close together to travel. It is the part nearest the wheel's inner plate, which the
        //pin clears by 0.02 on the shipped figures.
        private const float HINGE_BOSS_RADIUS = 0.08f;
        private const float HINGE_BOSS_PROUD = 0.02f;
        private const float HINGE_PIN_RADIUS = 0.035f;
        private const float HINGE_PIN_PROUD = 0.03f;
        private const int HINGE_SEGMENTS = 16;

        //The collar the leg leaves the socket through, sleeving the line where an oblique girder crosses a flat
        //wall: it starts inside the wall and runs a little way out along the leg. Proud outward, up and down, and
        //only a hair inboard — enough to cover the leg's own inner face without the two fighting over the depth
        //buffer, and never further in than the plain box stood.
        private const float COLLAR_PROUD = 0.015f;
        private const float COLLAR_INNER_PROUD = 0.003f;
        private const float COLLAR_BEFORE = 0.02f;         //along the leg, inside the wall
        private const float COLLAR_AFTER = 0.05f;          //and out of it

        #endregion

        #region The trail legs (#403)

        //The two faces of the first cut's box the leg keeps, off its centre line (see the class note)
        private const float LEG_INNER_FACE = 0.10f;
        private const float LEG_TOP_FACE = 0.08f;

        //A box girder tapering to its foot, the shape a real split trail's legs have: deep where the load comes
        //into the cheek, slim where the spade takes it into the ground. The root's width is bounded by the wheel
        //— a leg leaves its socket inside the wheel's inner plate, and the collar round it has to clear that
        //plate too — and its depth by the rollers' circle it passes on its way back.
        private const float LEG_HALF_WIDTH_ROOT = 0.09f;
        private const float LEG_HALF_WIDTH_FOOT = 0.085f;
        private const float LEG_DEPTH_ROOT = 0.34f;
        private const float LEG_DEPTH_FOOT = 0.15f;

        //Each long edge cut back by this share of the section's smaller half-extent. On a dark iron that barely
        //reflects, a chamfer is what lets the form read at all: a narrow face at a second angle to the light,
        //running the leg's whole length, where the box's faces met at an edge the eye could not find.
        private const float LEG_CHAMFER = 0.35f;
        private const int SECTION_CORNERS = 8;

        //Two bands clamped round each leg, proud of it on every face: the device the barrel's girdle and base
        //ring already are, so the frame and the tube it carries speak one language. Photographed without them,
        //the tapered, chamfered girder still read as a stick from the play camera — a long run of dark iron
        //does, however it is sectioned — and a band's two lit edges are what break the length into a rhythm.
        //
        //⚠ A BAND STANDS PROUD INBOARD AND ABOVE, which the kept faces forbid at the root, so it may only sit
        //where the breech cannot reach, and that is arithmetic rather than a look. The carriage yaws with the
        //aim and the tube only elevates and recoils in the carriage's own YZ plane, so nothing of the tube is
        //ever further off its axis than its widest steel — the base ring, 0.845 (CannonRig's bore and wall plus
        //CannonMesh.BASE_RING). A leg's most inboard edge stands at about 0.785 + 0.64·station off that axis on
        //the shipped figures, so a band proud by BAND_PROUD clears the base ring from station 0.12 on. The first
        //is at well over twice that.
        private static readonly float[] BAND_STATIONS = { 0.34f, 0.49f };
        private const float BAND_PROUD = 0.018f;
        private const float BAND_HALF_LENGTH = 0.045f;

        //The spade: a pointed blade across the foot, raked so the point sweeps back away from the gun, which is
        //the way a spade has to lean to bite as the recoil shoves the carriage back onto it. The point reaches a
        //hundredth past the wheels' ground line, so it reads as dug in rather than resting. Ground to an edge:
        //the shoulders keep most of the thickness and the point a third of it.
        private const float SPADE_HALF_WIDTH = 0.27f;
        private const float SPADE_SHOULDER = 0.20f;
        private const float SPADE_POINT = 0.32f;
        private const float SPADE_RAKE = 0.35f;       //radians, about 20 degrees off the vertical
        private const float SPADE_HALF_THICKNESS = 0.03f;
        private const float SPADE_SHOULDER_GRIND = 0.7f;
        private const float SPADE_POINT_GRIND = 0.35f;
        private const float SPADE_RISE = 0.02f;       //the blade's top edge over the leg's top face

        //The gusset bracing the blade back onto the leg's top
        private const float GUSSET_REACH = 0.30f;     //along the leg from its foot
        private const float GUSSET_DROP = 0.18f;      //down the blade's face
        private const float GUSSET_HALF_THICKNESS = 0.018f;

        //The lifting handle over the foot: an arch of round bar on the leg's top, between two stations of the
        //leg's length. Read from the play camera, which looks straight down the trail, as the one curved line
        //on a frame otherwise made of flats.
        private const float HANDLE_FROM = 0.62f;     //clear of the second band, short of the gusset
        private const float HANDLE_TO = 0.84f;
        private const float HANDLE_RISE = 0.16f;
        private const float HANDLE_BAR_RADIUS = 0.028f;
        private const float HANDLE_BURY = 0.02f;     //both ends sunk this far into the top face, so no joint shows
        private const int HANDLE_STEPS = 10;
        private const int HANDLE_SEGMENTS = 8;

        #endregion

        /// <param name="cheekInnerX">Inner face of each cheek plate off the barrel's axis — a hair over the
        /// tube's outer radius, so the plates hug the barrel without clipping it.</param>
        /// <param name="cheekThickness">Each plate's thickness along the axle.</param>
        /// <param name="cheekTopY">Top of the plates above the trunnion axis they hold — the radius of the arch
        /// the plates are rounded to about that axis (#403), so it has to clear the trunnion's bearing ring.</param>
        /// <param name="axleDrop">How far below the trunnions the axle runs — the wheels' centre height.</param>
        /// <param name="cheekHalfLength">Half the plates' run along the barrel.</param>
        /// <param name="axleRadius">The axle bar's radius.</param>
        /// <param name="axleHalfLength">Half the axle's length — reaching into the wheels' hubs.</param>
        /// <param name="trailEnd">Where the +X side's trail leg ends, in the carriage's own frame
        /// (x &gt; 0 outward, y &lt; 0 below the trunnions, z &gt; 0 back); the other leg mirrors it in X.
        /// The legs run from the cheeks' lower rear corners to here.</param>
        /// <param name="trunnionRadius">The trunnion pins' radius.</param>
        /// <param name="trunnionInnerX">Where each pin's buried end sits off the barrel's axis — inside the
        /// tube's wall (over the bore, under the outer profile) at every aim, so the joint never shows.</param>
        /// <param name="trunnionOuterX">Where each pin's boss ends, proud of the cheek's outer face.</param>
        public GunCarriageMesh(GraphicsDevice graphicsDevice, float cheekInnerX, float cheekThickness,
            float cheekTopY, float axleDrop, float cheekHalfLength, float axleRadius, float axleHalfLength,
            Vector3 trailEnd, float trunnionRadius, float trunnionInnerX, float trunnionOuterX)
        {
            MeshBuilder builder = new();

            //The cheeks: plates reaching down past the axle, so the axle reads as carried by them rather than
            //floating alongside — shaped brackets since #403 (see "The cheeks")
            float cheekBottomY = -axleDrop - axleRadius * 1.6f;
            float cheekCentreX = cheekInnerX + cheekThickness * 0.5f;
            float cheekOuterX = cheekInnerX + cheekThickness;
            float shoulderY = -axleDrop * CHEEK_SHOULDER;

            for (int side = -1; side <= 1; side += 2)
            {
                AddCheek(builder, side, cheekInnerX, cheekOuterX, cheekBottomY, cheekHalfLength, shoulderY, cheekTopY);

                //The trunnion's bearing ring, proud of the plate but never of the pin through it
                float bearingProud = MathF.Min(TRUNNION_BEARING_PROUD, (trunnionOuterX - cheekOuterX) * 0.6f);
                float bearingRadius = trunnionRadius * TRUNNION_BEARING_SCALE;
                builder.AddTubeX(new Vector3(side * (cheekOuterX + bearingProud * 0.5f), 0f, 0f),
                    bearingProud * 0.5f, bearingRadius, BEARING_SEGMENTS);

                //Its cap-square's two bolts, below it either side of the rib
                for (int bolt = -1; bolt <= 1; bolt += 2)
                {
                    builder.AddTubeX(new Vector3(side * (cheekOuterX + BOLT_PROUD * 0.5f),
                            -BOLT_ORBIT * MathF.Cos(BOLT_ANGLE), bolt * BOLT_ORBIT * MathF.Sin(BOLT_ANGLE)),
                        BOLT_PROUD * 0.5f, BOLT_RADIUS, BOLT_SEGMENTS);
                }

                //The axle's bearing ring
                float axleBearingRadius = axleRadius * AXLE_BEARING_SCALE;
                builder.AddTubeX(new Vector3(side * (cheekOuterX + AXLE_BEARING_PROUD * 0.5f), -axleDrop, 0f),
                    AXLE_BEARING_PROUD * 0.5f, axleBearingRadius, BEARING_SEGMENTS);

                //And the rib between the two rings, its ends just under each
                float ribTop = -bearingRadius * 0.9f;
                float ribBottom = -axleDrop + axleBearingRadius * 0.9f;
                AddPad(builder,
                    new Vector3(side * cheekOuterX, (ribTop + ribBottom) * 0.5f, 0f),
                    new Vector3(0f, (ribTop - ribBottom) * 0.5f, 0f),
                    new Vector3(0f, 0f, RIB_HALF_WIDTH),
                    new Vector3(side * RIB_PROUD, 0f, 0f),
                    RIB_CHAMFER);

                //The socket the leg is seated in (see "Where a leg meets its cheek")
                AddSocket(builder, side, cheekOuterX, cheekBottomY, cheekHalfLength, shoulderY);

                //A trail leg, from the cheek's lower rear corner, diverging outward as it falls back to the foot
                AddTrailLeg(builder,
                    new Vector3(side * cheekCentreX, -axleDrop * 0.75f, cheekHalfLength * 0.8f),
                    new Vector3(side * trailEnd.X, trailEnd.Y, trailEnd.Z),
                    side, cheekInnerX, cheekOuterX + SOCKET_PROUD, cheekHalfLength);

                //The trunnion pin through this cheek: from inside the barrel's wall out to a boss proud of
                //the plate, on the elevation axis itself — which is why it can sit still while the tube turns
                builder.AddTubeX(new Vector3(side * (trunnionInnerX + trunnionOuterX) * 0.5f, 0f, 0f),
                    (trunnionOuterX - trunnionInnerX) * 0.5f, trunnionRadius, AXLE_SEGMENTS);
            }

            //The axle, through both cheeks and into the wheels' hubs
            builder.AddTubeX(new Vector3(0f, -axleDrop, 0f), axleHalfLength, axleRadius, AXLE_SEGMENTS);

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);

            float reach = MathF.Max(axleHalfLength, MathF.Max(trailEnd.Z, axleDrop - trailEnd.Y));
            BoundingSphere = new BoundingSphere(new Vector3(0f, -axleDrop * 0.5f, trailEnd.Z * 0.35f), reach);
        }

        /// <summary>
        /// One cheek (see "The cheeks"): its side silhouette extruded across the plate, from the inner face — flat,
        /// where the box's was — out to a chamfer that steps in to the outer face.
        /// </summary>
        private static void AddCheek(MeshBuilder builder, float side, float innerX, float outerX, float bottomY,
            float halfLength, float shoulderY, float archRadius)
        {
            //Both outlines in the plate's own (z, y), the second the first grown in by the chamfer, and built by
            //the same construction so they have the same corners in the same order
            Vector2[] rim = CheekOutline(halfLength, bottomY, shoulderY, archRadius);
            Vector2[] face = CheekOutline(halfLength - CHEEK_CHAMFER, bottomY + CHEEK_CHAMFER, shoulderY,
                archRadius - CHEEK_CHAMFER);

            AddSlab(builder, side, rim, face, innerX, outerX);
        }

        /// <summary>
        /// The trail socket on a cheek's outer face (see "Where a leg meets its cheek"): the plate's lower back
        /// quarter, thickened. Its inner face starts where the cheek's chamfer does, so the two back walls meet
        /// edge to edge in one flat wall and the cheek's own chamfer there is swallowed rather than notched.
        /// </summary>
        private static void AddSocket(MeshBuilder builder, float side, float cheekOuterX, float bottomY,
            float halfLength, float shoulderY)
        {
            Vector2[] rim =
            {
                new(halfLength, shoulderY),
                new(halfLength, bottomY + CHEEK_FOOT_CUT),
                new(halfLength - CHEEK_FOOT_CUT, bottomY),
                new(halfLength * SOCKET_FRONT_BOTTOM, bottomY),
                new(halfLength * SOCKET_FRONT_TOP, shoulderY),
            };

            AddSlab(builder, side, rim, Inset(rim, CHEEK_CHAMFER), cheekOuterX - CHEEK_CHAMFER,
                cheekOuterX + SOCKET_PROUD);
        }

        /// <summary>
        /// A convex outline in (z, y) extruded across a slab from <paramref name="innerX"/> to
        /// <paramref name="outerX"/> off the barrel's axis: the inner face, the edge walls up to where the chamfer
        /// starts, the chamfer, and the outer face <paramref name="face"/> — the rim grown in, with the same
        /// corners in the same order. Every face flat, with its own normal.
        /// </summary>
        private static void AddSlab(MeshBuilder builder, float side, Vector2[] rim, Vector2[] face, float innerX,
            float outerX)
        {
            float chamferX = outerX - CHEEK_CHAMFER;
            Vector3 middle = At(Centroid(rim), (innerX + outerX) * 0.5f, side);
            Vector3 inward = new(-side, 0f, 0f);

            for (int i = 0; i < rim.Length; i++)
            {
                int j = (i + 1) % rim.Length;

                //The inner face and the outer face, as fans from their own centres
                builder.AddTriangle(At(Centroid(rim), innerX, side), At(rim[i], innerX, side), At(rim[j], innerX, side),
                    inward, inward, inward, inward);
                builder.AddTriangle(At(Centroid(face), outerX, side), At(face[i], outerX, side), At(face[j], outerX, side),
                    -inward, -inward, -inward, -inward);

                //The edge wall, across the slab up to where the chamfer starts
                AddFlatQuad(builder, At(rim[i], innerX, side), At(rim[j], innerX, side),
                    At(rim[j], chamferX, side), At(rim[i], chamferX, side), middle);

                //And the chamfer, stepping in to the outer face
                AddFlatQuad(builder, At(rim[i], chamferX, side), At(rim[j], chamferX, side),
                    At(face[j], outerX, side), At(face[i], outerX, side), middle);
            }
        }

        /// <summary>
        /// A cheek's side silhouette in (z, y), in order round it: the two cut foot corners, straight up the front
        /// to the shoulder, a tangent line to the arch, over the arch about the trunnion axis, and the mirror image
        /// down the back. Tangent, so the arch and the leaning edges meet without a crease — and convex, which the
        /// fans that close its faces rely on.
        /// </summary>
        private static Vector2[] CheekOutline(float halfLength, float bottomY, float shoulderY, float archRadius)
        {
            Vector2[] points = new Vector2[CHEEK_ARCH_STEPS + 7];
            int n = 0;

            points[n++] = new Vector2(-halfLength + CHEEK_FOOT_CUT, bottomY);
            points[n++] = new Vector2(-halfLength, bottomY + CHEEK_FOOT_CUT);
            points[n++] = new Vector2(-halfLength, shoulderY);

            //Where a line from each shoulder touches the arch on its upper side: the shoulder's own angle about
            //the axis, turned towards the top by the tangent's half-angle acos(r / d)
            Vector2 front = new(-halfLength, shoulderY), back = new(halfLength, shoulderY);
            float frontAngle = MathF.Atan2(front.Y, front.X) - MathF.Acos(archRadius / front.Length()) + MathHelper.TwoPi;
            float backAngle = MathF.Atan2(back.Y, back.X) + MathF.Acos(archRadius / back.Length());

            for (int k = 0; k <= CHEEK_ARCH_STEPS; k++)
            {
                float angle = MathHelper.Lerp(frontAngle, backAngle, k / (float)CHEEK_ARCH_STEPS);
                points[n++] = new Vector2(archRadius * MathF.Cos(angle), archRadius * MathF.Sin(angle));
            }

            points[n++] = new Vector2(halfLength, shoulderY);
            points[n++] = new Vector2(halfLength, bottomY + CHEEK_FOOT_CUT);
            points[n] = new Vector2(halfLength - CHEEK_FOOT_CUT, bottomY);

            return points;
        }

        /// <summary>
        /// A convex polygon grown in by <paramref name="distance"/> on every edge: each corner moves along its
        /// bisector until both of its edges are that far in. Either winding.
        /// </summary>
        private static Vector2[] Inset(Vector2[] polygon, float distance)
        {
            int count = polygon.Length;
            float area = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % count];
                area += a.X * b.Y - b.X * a.Y;
            }

            //Counter-clockwise puts the inside to the left of every edge, clockwise to the right
            float turn = area > 0f ? 1f : -1f;
            Vector2[] inset = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                Vector2 here = polygon[i];
                Vector2 inPrevious = LeftNormal(here - polygon[(i + count - 1) % count]) * turn;
                Vector2 inNext = LeftNormal(polygon[(i + 1) % count] - here) * turn;

                inset[i] = here + (inPrevious + inNext) * (distance / (1f + Vector2.Dot(inPrevious, inNext)));
            }

            return inset;
        }

        private static Vector2 LeftNormal(Vector2 edge) => Vector2.Normalize(new Vector2(-edge.Y, edge.X));

        //A point of a cheek's (z, y) outline at a distance off the barrel's axis, on the given side
        private static Vector3 At(Vector2 zy, float x, float side) => new(side * x, zy.Y, zy.X);

        private static Vector2 Centroid(Vector2[] points)
        {
            Vector2 sum = Vector2.Zero;
            foreach (Vector2 point in points) sum += point;

            return sum / points.Length;
        }

        //A flat quad shaded by its own geometric normal, turned away from `inside`
        private static void AddFlatQuad(MeshBuilder builder, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(normal, (a + b + c + d) * 0.25f - inside) < 0f) normal = -normal;
            normal = Vector3.Normalize(normal);

            builder.AddQuad(a, b, c, d, normal, normal, normal, normal, normal);
        }

        /// <summary>
        /// A chamfered pad standing on a face — the cheek's rib: a rectangle <paramref name="halfU"/> by
        /// <paramref name="halfV"/> about <paramref name="baseCentre"/> on the face, rising by
        /// <paramref name="rise"/> to a top grown in by <paramref name="chamfer"/> all round. Its base is buried in
        /// the face it stands on, so it has none.
        /// </summary>
        private static void AddPad(MeshBuilder builder, Vector3 baseCentre, Vector3 halfU, Vector3 halfV, Vector3 rise,
            float chamfer)
        {
            Vector3 topU = halfU - Vector3.Normalize(halfU) * chamfer;
            Vector3 topV = halfV - Vector3.Normalize(halfV) * chamfer;
            Vector3 topCentre = baseCentre + rise;
            Vector3 inside = baseCentre + rise * 0.5f;

            Vector3[] bottom = { baseCentre - halfU - halfV, baseCentre + halfU - halfV, baseCentre + halfU + halfV, baseCentre - halfU + halfV };
            Vector3[] top = { topCentre - topU - topV, topCentre + topU - topV, topCentre + topU + topV, topCentre - topU + topV };

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                AddFlatQuad(builder, bottom[i], bottom[j], top[j], top[i], inside);
            }

            AddFlatQuad(builder, top[0], top[1], top[2], top[3], baseCentre);
        }

        /// <summary>
        /// One leg of the split trail, from <paramref name="start"/> (inside its socket) to <paramref name="end"/>
        /// (its foot) — see the class note for what it is made of and the faces it may not cross.
        /// </summary>
        /// <param name="side">−1 or +1: which side of the barrel's axis the leg stands, and so which way is outward.</param>
        /// <param name="cheekInnerX">The cheek's inner plane off the axis, which the root is seated against.</param>
        /// <param name="socketOuterX">The socket's outer face off the axis, where the hinge stands.</param>
        /// <param name="socketBackZ">The socket's back wall, which the leg leaves through its collar.</param>
        private static void AddTrailLeg(MeshBuilder builder, Vector3 start, Vector3 end, float side, float cheekInnerX,
            float socketOuterX, float socketBackZ)
        {
            //Seat the root (see LEG_ROOT_SPARE): find the section's most inboard corner from where the caller put
            //it, move the whole root outboard by the difference, and take the frame again from there — the move
            //turns the leg a hair, far too little for a second pass to find anything
            LegFrame(start, end, side, out float length, out Vector3 direction, out Vector3 outward, out Vector3 upright);

            float mostInboard = float.MaxValue;
            foreach (Vector3 corner in LegSection(start, end, 0f, outward, upright, 0f, 0f, out _))
                mostInboard = MathF.Min(mostInboard, corner.X * side);

            start.X += side * (cheekInnerX + LEG_ROOT_SPARE - mostInboard);
            LegFrame(start, end, side, out length, out direction, out outward, out upright);

            //The girder: two chamfered sections, root and foot, and the eight long faces between them. Flat
            //faces, each with its own normal: this is machined iron, and the edges are the point.
            Vector3[] root = LegSection(start, end, 0f, outward, upright, 0f, 0f, out Vector3 rootCentre);
            Vector3[] foot = LegSection(start, end, 1f, outward, upright, 0f, 0f, out Vector3 footCentre);

            for (int i = 0; i < SECTION_CORNERS; i++)
            {
                int j = (i + 1) % SECTION_CORNERS;

                Vector3 normal = Vector3.Cross(root[j] - root[i], foot[i] - root[i]);
                if (Vector3.Dot(normal, (root[i] + root[j]) * 0.5f - rootCentre) < 0f) normal = -normal;
                normal = Vector3.Normalize(normal);

                builder.AddQuad(root[i], root[j], foot[j], foot[i], normal, normal, normal, normal, normal);

                //The two ends: the root's is inside the socket and the foot's inside the spade, but a cap costs
                //two triangles and an open end would show the moment either figure is retuned
                builder.AddTriangle(rootCentre, root[i], root[j], -direction, -direction, -direction, -direction);
                builder.AddTriangle(footCentre, foot[i], foot[j], direction, direction, direction, direction);
            }

            foreach (float station in BAND_STATIONS)
            {
                AddCollar(builder, start, end, station - BAND_HALF_LENGTH / length, station + BAND_HALF_LENGTH / length,
                    direction, outward, upright, BAND_PROUD, BAND_PROUD);
            }

            //The collar at the socket's back wall: where the centre line crosses the wall, measured along the leg
            float exit = (socketBackZ - start.Z) / direction.Z;
            AddCollar(builder, start, end, (exit - COLLAR_BEFORE) / length, (exit + COLLAR_AFTER) / length,
                direction, outward, upright, COLLAR_PROUD, COLLAR_INNER_PROUD);

            //The hinge on the socket's outer face, over the root section's middle
            Vector3 hinge = new(side * socketOuterX, start.Y + LEG_TOP_FACE - LEG_DEPTH_ROOT * 0.5f, start.Z);
            builder.AddTubeX(hinge + new Vector3(side * HINGE_BOSS_PROUD * 0.5f, 0f, 0f), HINGE_BOSS_PROUD * 0.5f,
                HINGE_BOSS_RADIUS, HINGE_SEGMENTS);
            builder.AddTubeX(hinge + new Vector3(side * HINGE_PIN_PROUD * 0.5f, 0f, 0f), HINGE_PIN_PROUD * 0.5f,
                HINGE_PIN_RADIUS, HINGE_SEGMENTS);

            //The spade, centred on the foot so the foot's end is swallowed by the blade. Its plane holds the
            //outward axis and a down axis raked back along the leg's heading.
            Vector3 heading = Vector3.Normalize(new Vector3(direction.X, 0f, direction.Z));
            Vector3 bladeDown = Vector3.Normalize(-Vector3.Up + heading * MathF.Tan(SPADE_RAKE));
            Vector3 bladeNormal = Vector3.Normalize(Vector3.Cross(bladeDown, outward));
            if (Vector3.Dot(bladeNormal, heading) < 0f) bladeNormal = -bladeNormal;

            Vector3 bladeTop = end + outward * (LEG_HALF_WIDTH_FOOT - LEG_INNER_FACE)
                + upright * (LEG_TOP_FACE + SPADE_RISE);

            builder.AddPlate(new[]
                {
                    bladeTop - outward * SPADE_HALF_WIDTH,
                    bladeTop + outward * SPADE_HALF_WIDTH,
                    bladeTop + outward * SPADE_HALF_WIDTH + bladeDown * SPADE_SHOULDER,
                    bladeTop + bladeDown * SPADE_POINT,
                    bladeTop - outward * SPADE_HALF_WIDTH + bladeDown * SPADE_SHOULDER,
                },
                new[]
                {
                    SPADE_HALF_THICKNESS,
                    SPADE_HALF_THICKNESS,
                    SPADE_HALF_THICKNESS * SPADE_SHOULDER_GRIND,
                    SPADE_HALF_THICKNESS * SPADE_POINT_GRIND,
                    SPADE_HALF_THICKNESS * SPADE_SHOULDER_GRIND,
                },
                bladeNormal);

            //The gusset: a triangle in the leg's own vertical plane, from the leg's top short of the foot to the
            //blade's gun-side face and down it
            float gussetStation = 1f - GUSSET_REACH / length;
            Vector3 gussetTop = TopFaceCentre(start, end, gussetStation, outward, upright);
            Vector3 bladeBack = bladeTop - bladeNormal * SPADE_HALF_THICKNESS;
            Vector3 gussetFoot = bladeBack + bladeDown * GUSSET_DROP;

            builder.AddPlate(new[] { gussetTop, bladeBack, gussetFoot },
                new[] { GUSSET_HALF_THICKNESS, GUSSET_HALF_THICKNESS, GUSSET_HALF_THICKNESS },
                Vector3.Normalize(Vector3.Cross(bladeBack - gussetTop, gussetFoot - gussetTop)));

            //The lifting handle: a half-ellipse of round bar over the top face, both feet sunk into it. The
            //stations are spaced on the ellipse's own parameter, so its legs rise square out of the leg.
            Vector3[] path = new Vector3[HANDLE_STEPS + 1];
            float handleOffset = MathHelper.Lerp(LEG_HALF_WIDTH_ROOT, LEG_HALF_WIDTH_FOOT, (HANDLE_FROM + HANDLE_TO) * 0.5f)
                - LEG_INNER_FACE;

            for (int k = 0; k <= HANDLE_STEPS; k++)
            {
                float phi = k / (float)HANDLE_STEPS * MathHelper.Pi;
                float station = MathHelper.Lerp(HANDLE_FROM, HANDLE_TO, 0.5f - 0.5f * MathF.Cos(phi));

                path[k] = Vector3.Lerp(start, end, station) + outward * handleOffset
                    + upright * (LEG_TOP_FACE - HANDLE_BURY + (HANDLE_RISE + HANDLE_BURY) * MathF.Sin(phi));
            }

            builder.AddSweptTube(path, outward, HANDLE_BAR_RADIUS, HANDLE_SEGMENTS);
        }

        //A leg's own frame. Upright is world up with the leg's fall taken out, so it points up for either leg;
        //outward is square to both — which makes it horizontal — and turned away from the barrel's axis.
        private static void LegFrame(Vector3 start, Vector3 end, float side, out float length, out Vector3 direction,
            out Vector3 outward, out Vector3 upright)
        {
            Vector3 along = end - start;
            length = along.Length();
            direction = along / length;

            upright = Vector3.Normalize(Vector3.Up - direction * Vector3.Dot(direction, Vector3.Up));
            outward = Vector3.Cross(upright, direction);
            if (outward.X * side < 0f) outward = -outward;
        }

        /// <summary>
        /// A collar round the leg between two stations (0 at the root, 1 at the foot) — the bands and the socket's
        /// exit sleeve alike: the leg's own section grown by <paramref name="proud"/> outward, up and down and by
        /// <paramref name="innerProud"/> inboard, with a ring at either end closing it down onto the leg.
        /// </summary>
        private static void AddCollar(MeshBuilder builder, Vector3 start, Vector3 end, float from, float to,
            Vector3 direction, Vector3 outward, Vector3 upright, float proud, float innerProud)
        {
            Vector3[] legFrom = LegSection(start, end, from, outward, upright, 0f, 0f, out Vector3 centreFrom);
            Vector3[] legTo = LegSection(start, end, to, outward, upright, 0f, 0f, out Vector3 centreTo);
            Vector3[] rimFrom = LegSection(start, end, from, outward, upright, proud, innerProud, out _);
            Vector3[] rimTo = LegSection(start, end, to, outward, upright, proud, innerProud, out _);
            Vector3 inside = (centreFrom + centreTo) * 0.5f;

            for (int i = 0; i < SECTION_CORNERS; i++)
            {
                int j = (i + 1) % SECTION_CORNERS;

                AddFlatQuad(builder, rimFrom[i], rimFrom[j], rimTo[j], rimTo[i], inside);

                builder.AddQuad(rimFrom[i], rimFrom[j], legFrom[j], legFrom[i],
                    -direction, -direction, -direction, -direction, -direction);
                builder.AddQuad(rimTo[i], rimTo[j], legTo[j], legTo[i],
                    direction, direction, direction, direction, direction);
            }
        }

        /// <summary>
        /// The leg's chamfered section at a station, optionally grown for a collar: outward, up and down by
        /// <paramref name="proud"/> and inboard by <paramref name="innerProud"/>. Its centre — ungrown — comes back
        /// as <paramref name="centre"/>.
        /// </summary>
        private static Vector3[] LegSection(Vector3 start, Vector3 end, float station, Vector3 outward, Vector3 upright,
            float proud, float innerProud, out Vector3 centre)
        {
            float halfWidth = MathHelper.Lerp(LEG_HALF_WIDTH_ROOT, LEG_HALF_WIDTH_FOOT, station);
            float halfDepth = MathHelper.Lerp(LEG_DEPTH_ROOT, LEG_DEPTH_FOOT, station) * 0.5f;
            centre = SectionCentre(Vector3.Lerp(start, end, station), outward, upright, halfWidth, halfDepth);

            return Section(centre + outward * ((proud - innerProud) * 0.5f), outward, upright,
                halfWidth + (proud + innerProud) * 0.5f, halfDepth + proud);
        }

        //A section's centre: offset from the leg's centre line so its inner face and its top face land on the
        //box's (LEG_INNER_FACE, LEG_TOP_FACE) whatever the section's own width and depth
        private static Vector3 SectionCentre(Vector3 onLine, Vector3 outward, Vector3 upright, float halfWidth,
            float halfDepth) =>
            onLine + outward * (halfWidth - LEG_INNER_FACE) + upright * (LEG_TOP_FACE - halfDepth);

        //The middle of the top face at a station along the leg (0 at the root, 1 at the foot)
        private static Vector3 TopFaceCentre(Vector3 start, Vector3 end, float station, Vector3 outward, Vector3 upright) =>
            Vector3.Lerp(start, end, station)
            + outward * (MathHelper.Lerp(LEG_HALF_WIDTH_ROOT, LEG_HALF_WIDTH_FOOT, station) - LEG_INNER_FACE)
            + upright * LEG_TOP_FACE;

        //The eight corners of a chamfered rectangular section, in order round it
        private static Vector3[] Section(Vector3 centre, Vector3 outward, Vector3 upright, float halfWidth, float halfDepth)
        {
            float cut = LEG_CHAMFER * MathF.Min(halfWidth, halfDepth);

            return new[]
            {
                centre + outward * halfWidth + upright * (halfDepth - cut),
                centre + outward * (halfWidth - cut) + upright * halfDepth,
                centre - outward * (halfWidth - cut) + upright * halfDepth,
                centre - outward * halfWidth + upright * (halfDepth - cut),
                centre - outward * halfWidth - upright * (halfDepth - cut),
                centre - outward * (halfWidth - cut) - upright * halfDepth,
                centre + outward * (halfWidth - cut) - upright * halfDepth,
                centre + outward * halfWidth - upright * (halfDepth - cut),
            };
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
