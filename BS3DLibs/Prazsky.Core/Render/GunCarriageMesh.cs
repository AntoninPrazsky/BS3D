using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The gun carriage's frame, one mesh: two cheek plates with the trunnion pins they visibly hold the
    /// barrel by (shaped brackets with bearings since #403 — see "The cheeks"), the axle stubs the wheels turn
    /// on, and a <b>split trail</b> — two legs diverging down and back.
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
    /// caller's figure, arriving as <c>axleDrop</c> so the frame and the wheels it was sized
    /// around cannot drift apart. The pins live on <i>this</i> mesh and not the barrel's, and that is
    /// load-bearing: they are coaxial with the elevation axis, so a fixed pin looks identical however the
    /// tube elevates — but the Game's recoil slides the tube, and pins riding it would visibly tear along
    /// the plates that hold them.
    /// </para>
    /// <para>
    /// <b>Nothing of the frame stands where the tube can reach (#403), and that is geometry rather than a sweep
    /// of poses.</b> The carriage yaws with the aim, so the tube only elevates about the trunnion axis and
    /// recoils along its own: no steel of it is ever further off its axis than its widest station, and none
    /// wider than the plates' hub plane ever comes nearer the trunnions than <c>cheekHubRadius</c>.
    /// So the cheeks hug the tube (<c>cheekInnerX</c>) only inside that radius — the hub — and stand
    /// at <c>cheekReliefX</c>, clear of the widest steel, everywhere else; every part of the legs,
    /// their bands and collars included, is seated outboard of that same plane; and the axle is two stubs ending
    /// inside the plates, because at high elevation the breech sweeps the whole space between them. The first
    /// cut hugged the tube all the way down, as the plain boxes had, and the breech's base ring ran through the
    /// plates' lower back quarter from about 36° of elevation to the top, through the legs' roots, and through
    /// the axle bar outright from 52° — measured on the CPU against every elevation and recoil, and identical
    /// for the boxes.
    /// </para>
    /// <para>
    /// <b>The legs were two plain oriented boxes until #403</b>, with a small square plate for a foot — reported
    /// from play as far cruder than the wheels and the turned barrel, and they are the nearest thing to the
    /// play camera, which stands behind the gun with the trail running at the lens. Each is now a tapered,
    /// chamfered box girder with two bands clamped round it, seated in a socket on the cheek's outer face with a
    /// hinge on its ledge, leaving it through a collar, and ending in a raked spade with a ground edge and a
    /// gusset, with a lifting handle over its foot.
    /// </para>
    /// </summary>
    public class GunCarriageMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private const int AXLE_SEGMENTS = 12;

        //The axle stubs' inner ends, sunk this far into the relieved plate so no end shows
        private const float AXLE_STUB_BURY = 0.02f;

        #region The cheeks (#403)

        //A cheek was a plain box — reported right after the legs as the other crude block of the frame, the one
        //the legs run into. It is a bracket now, in the legs' own language: an arched top round the trunnion,
        //falling in tangent lines to the shoulders and on down to a chamfered foot, its outer face chamfered all
        //round, carrying a raised bearing for the trunnion (held by a cap-square's two bolts) and another for
        //the axle, with a rib between the two. Its inner side is two planes (see the class note): the hub round
        //the trunnion, standing in to hug the tube and chamfered towards it, and the relieved plate round that.
        //Everything proud of the outer face stays inside the wheel's inner plate (1.11 off the axis on the
        //shipped figures).

        //(The arch's radius about the trunnion axis is the caller's cheekTopY — the plates' top — for the reason
        //the axle drop is: the frame is sized in CannonRig, around the tube and the wheels it has to fit.)

        //Where the plate's front and back edges stop being vertical and lean in towards the arch, as a share of
        //the axle drop below the trunnions. It has to stay above the legs' roots (at 0.75 of the drop), or the
        //leaning edge would cut the corner the leg runs into — and it is the trail socket's top as well.
        private const float CHEEK_SHOULDER = 0.62f;
        private const float CHEEK_CHAMFER = 0.03f;       //round the outer face, of the plate's thickness
        private const float CHEEK_FOOT_CUT = 0.08f;      //the two bottom corners
        private const int CHEEK_ARCH_STEPS = 12;

        //The hub's round edge, as a polygon: and how far its back face sinks into the relieved plate, so the two
        //never share a plane
        private const int HUB_SEGMENTS = 48;
        private const float HUB_BURY = 0.01f;

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
        //The root is seated first: the most inboard corner of its section lands this far outboard of the
        //relieved plane, so nothing of the leg shows between the cheeks and nothing of it can meet the tube.
        private const float LEG_ROOT_SPARE = 0.005f;

        //The socket: the cheek's lower back quarter thickened outboard, from the shoulder down to the foot, its
        //front edge leaning back, chamfered like the plate, so the root is inside solid iron and the leg leaves
        //through a flat back wall that continues the cheek's own. Its thickness is what the root's outer face
        //needs at that wall, and it clears the wheel's inner plate by 0.03 on the shipped figures; its front edge
        //is well clear of the axle's bearing.
        private const float SOCKET_PROUD = 0.075f;         //beyond the cheek's outer face
        private const float SOCKET_FRONT_TOP = 0.62f;      //the front edge at the shoulder, as a share of the half-length
        private const float SOCKET_FRONT_BOTTOM = 0.36f;   //and at the foot

        //The hinge the leg swings from — a split trail's legs close together to travel — as a boss and pin head
        //standing on the socket's top ledge over the root. On the ledge rather than on the socket's outer face,
        //which is the one face near enough the wheel's inner plate to have no room for a boss. Sunk into the
        //ledge far enough to cover the ledge's outer chamfer under its rim.
        private const float HINGE_RADIUS = 0.035f;
        private const float HINGE_RISE = 0.02f;            //above the ledge
        private const float HINGE_BURY = 0.03f;            //and below it
        private const float HINGE_PIN_RADIUS = 0.018f;
        private const float HINGE_PIN_RISE = 0.05f;
        private const int HINGE_SEGMENTS = 16;

        //The collar the leg leaves the socket through, sleeving the line where an oblique girder crosses a flat
        //wall: it starts inside the wall and runs a little way out along the leg. Proud outward, up and down, and
        //only a hair inboard — enough to cover the leg's own inner face without the two fighting over the depth
        //buffer.
        private const float COLLAR_PROUD = 0.015f;
        private const float COLLAR_INNER_PROUD = 0.003f;
        private const float COLLAR_BEFORE = 0.02f;         //along the leg, inside the wall
        private const float COLLAR_AFTER = 0.04f;          //and out of it

        #endregion

        #region The trail legs (#403)

        //The leg's inner face and top face off its centre line — what its sections are anchored to, so a section
        //grows outward and downward from them as the leg deepens
        private const float LEG_INNER_FACE = 0.10f;
        private const float LEG_TOP_FACE = 0.08f;

        //A box girder, deep where the load comes into the cheek and slim where the spade takes it into the ground.
        //Its width is set by the corridor it leaves its socket through: between the relieved plane (clear of the
        //tube's widest steel) and the wheel's inner plate there is about 0.245 on the shipped figures, the collar
        //has to fit in it too, and at this width the girder clears the plate by 0.02. At 0.09 it ran into it.
        private const float LEG_HALF_WIDTH_ROOT = 0.075f;
        private const float LEG_HALF_WIDTH_FOOT = 0.075f;
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
        //A band stands proud inboard as well, and it may: by a third of the way back the leg has diverged far
        //past the relieved plane.
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

        /// <param name="cheekInnerX">Where the plates hug the tube, off its axis — a hair over the tube's plain
        /// outer radius — which since #403 is the hub's inner face and the hub's only.</param>
        /// <param name="cheekReliefX">The plates' inner plane everywhere outside the hub, clear of the tube's widest
        /// steel; also the plane every part of the trail legs is seated outboard of.</param>
        /// <param name="cheekHubRadius">How far from the trunnion axis the plates may hug the tube: short of the
        /// nearest the tube's wider steel ever comes, recoil included.</param>
        /// <param name="cheekThickness">Each plate's thickness along the axle, from the relieved plane.</param>
        /// <param name="cheekTopY">Top of the plates above the trunnion axis they hold — the radius of the arch
        /// the plates are rounded to about that axis (#403), so it has to clear the trunnion's bearing ring.</param>
        /// <param name="axleDrop">How far below the trunnions the axle runs — the wheels' centre height.</param>
        /// <param name="cheekHalfLength">Half the plates' run along the barrel.</param>
        /// <param name="axleRadius">The axle bar's radius.</param>
        /// <param name="axleHalfLength">Half the axle's span — each stub reaching into its wheel's hub.</param>
        /// <param name="trailEnd">Where the +X side's trail leg ends, in the carriage's own frame
        /// (x &gt; 0 outward, y &lt; 0 below the trunnions, z &gt; 0 back); the other leg mirrors it in X.
        /// The legs run from the cheeks' lower rear corners to here.</param>
        /// <param name="trunnionRadius">The trunnion pins' radius.</param>
        /// <param name="trunnionInnerX">Where each pin's buried end sits off the barrel's axis — inside the
        /// tube's wall (over the bore, under the outer profile) at every aim, so the joint never shows.</param>
        /// <param name="trunnionOuterX">Where each pin's boss ends, proud of the cheek's outer face.</param>
        public GunCarriageMesh(GraphicsDevice graphicsDevice, float cheekInnerX, float cheekReliefX,
            float cheekHubRadius, float cheekThickness, float cheekTopY, float axleDrop, float cheekHalfLength,
            float axleRadius, float axleHalfLength, Vector3 trailEnd, float trunnionRadius, float trunnionInnerX,
            float trunnionOuterX)
        {
            MeshBuilder builder = new();

            //The cheeks: plates reaching down past the axle, so the axle reads as carried by them rather than
            //floating alongside — shaped brackets since #403 (see "The cheeks")
            float cheekBottomY = -axleDrop - axleRadius * 1.6f;
            float cheekOuterX = cheekReliefX + cheekThickness;
            float shoulderY = -axleDrop * CHEEK_SHOULDER;

            for (int side = -1; side <= 1; side += 2)
            {
                AddCheek(builder, side, cheekInnerX, cheekReliefX, cheekOuterX, cheekHubRadius, cheekBottomY,
                    cheekHalfLength, shoulderY, cheekTopY);

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
                    new Vector3(side * cheekOuterX, -axleDrop * 0.75f, cheekHalfLength * 0.8f),
                    new Vector3(side * trailEnd.X, trailEnd.Y, trailEnd.Z),
                    side, cheekReliefX, (cheekOuterX + cheekOuterX + SOCKET_PROUD) * 0.5f, shoulderY, cheekHalfLength);

                //The trunnion pin through this cheek: from inside the barrel's wall out to a boss proud of
                //the plate, on the elevation axis itself — which is why it can sit still while the tube turns
                builder.AddTubeX(new Vector3(side * (trunnionInnerX + trunnionOuterX) * 0.5f, 0f, 0f),
                    (trunnionOuterX - trunnionInnerX) * 0.5f, trunnionRadius, AXLE_SEGMENTS);

                //The axle stub: from inside the plate out into the wheel's hub. Never a bar between the plates —
                //at high elevation the breech sweeps that whole space (see the class note)
                float stubInner = cheekReliefX + AXLE_STUB_BURY;
                builder.AddTubeX(new Vector3(side * (stubInner + axleHalfLength) * 0.5f, -axleDrop, 0f),
                    (axleHalfLength - stubInner) * 0.5f, axleRadius, AXLE_SEGMENTS);
            }

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);

            float reach = MathF.Max(axleHalfLength, MathF.Max(trailEnd.Z, axleDrop - trailEnd.Y));
            BoundingSphere = new BoundingSphere(new Vector3(0f, -axleDrop * 0.5f, trailEnd.Z * 0.35f), reach);
        }

        /// <summary>
        /// One cheek (see "The cheeks"): the plate — its side silhouette extruded from the relieved plane out to a
        /// chamfer that steps in to the outer face — and the hub standing in from it round the trunnion, to the
        /// plane that hugs the tube, chamfered on the face towards the tube.
        /// </summary>
        private static void AddCheek(MeshBuilder builder, float side, float hugX, float reliefX, float outerX,
            float hubRadius, float bottomY, float halfLength, float shoulderY, float archRadius)
        {
            //Both outlines in the plate's own (z, y), the second the first grown in by the chamfer, and built by
            //the same construction so they have the same corners in the same order
            Vector2[] rim = CheekOutline(halfLength, bottomY, shoulderY, archRadius);
            Vector2[] face = CheekOutline(halfLength - CHEEK_CHAMFER, bottomY + CHEEK_CHAMFER, shoulderY,
                archRadius - CHEEK_CHAMFER);

            AddSlab(builder, side, rim, face, reliefX, outerX);

            //The hub: the plate's silhouette cut to the hub's circle, extruded from inside the plate in to the hug.
            //Built by the slab with the side and the distances mirrored, which is what puts its chamfered face on
            //the tube's side of it rather than the plate's.
            Vector2[] hub = ClipToCircle(rim, hubRadius);
            AddSlab(builder, -side, hub, Inset(hub, CHEEK_CHAMFER), -(reliefX + HUB_BURY), -hugX);
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
        /// A convex polygon cut to the inside of a circle about the (z, y) origin — the trunnion axis — the circle
        /// taken as an inscribed polygon, so the cut lies just inside the radius and never outside it. Convex in,
        /// convex out, which the slab's fans rely on.
        /// </summary>
        private static Vector2[] ClipToCircle(Vector2[] polygon, float radius)
        {
            List<Vector2> result = new(polygon);

            for (int k = 0; k < HUB_SEGMENTS && result.Count > 0; k++)
            {
                float a0 = k * MathHelper.TwoPi / HUB_SEGMENTS;
                float a1 = (k + 1) * MathHelper.TwoPi / HUB_SEGMENTS;
                Vector2 edgeFrom = new(radius * MathF.Cos(a0), radius * MathF.Sin(a0));
                Vector2 edgeTo = new(radius * MathF.Cos(a1), radius * MathF.Sin(a1));

                List<Vector2> input = result;
                result = new List<Vector2>(input.Count + 2);

                //Sutherland–Hodgman against one edge: the circle runs counter-clockwise, so inside is to its left
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 p = input[i], q = input[(i + 1) % input.Count];
                    float sideP = Cross(edgeTo - edgeFrom, p - edgeFrom);
                    float sideQ = Cross(edgeTo - edgeFrom, q - edgeFrom);

                    if (sideP >= 0f) result.Add(p);
                    if ((sideP >= 0f) != (sideQ >= 0f)) result.Add(p + (q - p) * (sideP / (sideP - sideQ)));
                }
            }

            //A cut through a corner leaves two points on top of each other, and a zero-length edge has no normal
            List<Vector2> distinct = new(result.Count);

            foreach (Vector2 point in result)
                if (distinct.Count == 0 || Vector2.DistanceSquared(point, distinct[^1]) > 1e-8f) distinct.Add(point);

            if (distinct.Count > 1 && Vector2.DistanceSquared(distinct[0], distinct[^1]) <= 1e-8f)
                distinct.RemoveAt(distinct.Count - 1);

            return distinct.ToArray();
        }

        private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

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
        /// (its foot) — see the class note for what it is made of and the plane it is seated outboard of.
        /// </summary>
        /// <param name="side">−1 or +1: which side of the barrel's axis the leg stands, and so which way is outward.</param>
        /// <param name="reliefX">The plates' relieved plane off the axis, which the root is seated outboard of.</param>
        /// <param name="hingeX">Where the hinge stands off the axis, on the socket's ledge.</param>
        /// <param name="ledgeY">The socket's top ledge.</param>
        /// <param name="socketBackZ">The socket's back wall, which the leg leaves through its collar.</param>
        private static void AddTrailLeg(MeshBuilder builder, Vector3 start, Vector3 end, float side, float reliefX,
            float hingeX, float ledgeY, float socketBackZ)
        {
            //Seat the root (see LEG_ROOT_SPARE): find the section's most inboard corner from where the caller put
            //it, move the whole root by the difference, and take the frame again from there — the move turns the
            //leg a hair, far too little for a second pass to find anything
            LegFrame(start, end, side, out float length, out Vector3 direction, out Vector3 outward);

            float mostInboard = float.MaxValue;
            foreach (Vector3 corner in LegSection(start, end, 0f, outward, 0f, 0f, out _))
                mostInboard = MathF.Min(mostInboard, corner.X * side);

            start.X += side * (reliefX + LEG_ROOT_SPARE - mostInboard);
            LegFrame(start, end, side, out length, out direction, out outward);

            //The girder: two chamfered sections, root and foot, and the eight long faces between them. Flat
            //faces, each with its own normal: this is machined iron, and the edges are the point.
            Vector3[] root = LegSection(start, end, 0f, outward, 0f, 0f, out Vector3 rootCentre);
            Vector3[] foot = LegSection(start, end, 1f, outward, 0f, 0f, out Vector3 footCentre);

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
                    direction, outward, BAND_PROUD, BAND_PROUD);
            }

            //The collar at the socket's back wall: where the centre line crosses the wall, measured along the leg
            float exit = (socketBackZ - start.Z) / direction.Z;
            AddCollar(builder, start, end, (exit - COLLAR_BEFORE) / length, (exit + COLLAR_AFTER) / length,
                direction, outward, COLLAR_PROUD, COLLAR_INNER_PROUD);

            //The hinge on the socket's ledge, over the root: a boss sunk into the ledge, and a pin head on it
            Vector3 hinge = new(side * hingeX, ledgeY, start.Z);
            builder.AddTube(hinge + Vector3.Up * ((HINGE_RISE - HINGE_BURY) * 0.5f), Vector3.Up, Vector3.UnitZ,
                (HINGE_RISE + HINGE_BURY) * 0.5f, HINGE_RADIUS, HINGE_SEGMENTS);
            builder.AddTube(hinge + Vector3.Up * ((HINGE_RISE + HINGE_PIN_RISE) * 0.5f), Vector3.Up, Vector3.UnitZ,
                (HINGE_RISE + HINGE_PIN_RISE) * 0.5f, HINGE_PIN_RADIUS, HINGE_SEGMENTS);

            //The spade, centred on the foot so the foot's end is swallowed by the blade. Its plane holds the
            //outward axis and a down axis raked back along the leg's heading.
            Vector3 heading = Vector3.Normalize(new Vector3(direction.X, 0f, direction.Z));
            Vector3 bladeDown = Vector3.Normalize(-Vector3.Up + heading * MathF.Tan(SPADE_RAKE));
            Vector3 bladeNormal = Vector3.Normalize(Vector3.Cross(bladeDown, outward));
            if (Vector3.Dot(bladeNormal, heading) < 0f) bladeNormal = -bladeNormal;

            Vector3 bladeTop = end + outward * (LEG_HALF_WIDTH_FOOT - LEG_INNER_FACE)
                + Vector3.Up * (LEG_TOP_FACE + SPADE_RISE);

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
            Vector3 gussetTop = TopFaceCentre(start, end, gussetStation, outward);
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
                    + Vector3.Up * (LEG_TOP_FACE - HANDLE_BURY + (HANDLE_RISE + HANDLE_BURY) * MathF.Sin(phi));
            }

            builder.AddSweptTube(path, outward, HANDLE_BAR_RADIUS, HANDLE_SEGMENTS);
        }

        /// <summary>
        /// A leg's own frame: its length and direction, and <paramref name="outward"/> — horizontal, square to the
        /// leg, turned away from the barrel's axis.
        /// <para>
        /// <b>Its sections stand vertical, not square to the leg</b>, which is why there is no upright here: the
        /// sections take world up. A section square to a leg that both diverges and falls leans its inner face
        /// inboard at the bottom — by 0.03 on the shipped figures — and the corridor a leg leaves its socket
        /// through, between the tube's widest steel and the wheel, has no 0.03 to spare. Standing vertical, the
        /// inner and outer faces are vertical planes and only the top and bottom follow the fall.
        /// </para>
        /// </summary>
        private static void LegFrame(Vector3 start, Vector3 end, float side, out float length, out Vector3 direction,
            out Vector3 outward)
        {
            Vector3 along = end - start;
            length = along.Length();
            direction = along / length;

            outward = Vector3.Normalize(Vector3.Cross(Vector3.Up, direction));
            if (outward.X * side < 0f) outward = -outward;
        }

        /// <summary>
        /// A collar round the leg between two stations (0 at the root, 1 at the foot) — the bands and the socket's
        /// exit sleeve alike: the leg's own section grown by <paramref name="proud"/> outward, up and down and by
        /// <paramref name="innerProud"/> inboard, with a ring at either end closing it down onto the leg.
        /// </summary>
        private static void AddCollar(MeshBuilder builder, Vector3 start, Vector3 end, float from, float to,
            Vector3 direction, Vector3 outward, float proud, float innerProud)
        {
            Vector3[] legFrom = LegSection(start, end, from, outward, 0f, 0f, out Vector3 centreFrom);
            Vector3[] legTo = LegSection(start, end, to, outward, 0f, 0f, out Vector3 centreTo);
            Vector3[] rimFrom = LegSection(start, end, from, outward, proud, innerProud, out _);
            Vector3[] rimTo = LegSection(start, end, to, outward, proud, innerProud, out _);
            Vector3 inside = (centreFrom + centreTo) * 0.5f;

            for (int i = 0; i < SECTION_CORNERS; i++)
            {
                int j = (i + 1) % SECTION_CORNERS;

                AddFlatQuad(builder, rimFrom[i], rimFrom[j], rimTo[j], rimTo[i], inside);

                //The end rings lie in the sections' vertical planes, so each is turned away from a point just
                //inside the collar along the leg rather than given the leg's direction for a normal
                AddFlatQuad(builder, rimFrom[i], rimFrom[j], legFrom[j], legFrom[i], centreFrom + direction * 0.01f);
                AddFlatQuad(builder, rimTo[i], rimTo[j], legTo[j], legTo[i], centreTo - direction * 0.01f);
            }
        }

        /// <summary>
        /// The leg's chamfered section at a station, standing vertical (see <see cref="LegFrame"/>), optionally
        /// grown for a collar: outward, up and down by <paramref name="proud"/> and inboard by
        /// <paramref name="innerProud"/>. Its centre — ungrown — comes back as <paramref name="centre"/>.
        /// </summary>
        private static Vector3[] LegSection(Vector3 start, Vector3 end, float station, Vector3 outward,
            float proud, float innerProud, out Vector3 centre)
        {
            float halfWidth = MathHelper.Lerp(LEG_HALF_WIDTH_ROOT, LEG_HALF_WIDTH_FOOT, station);
            float halfDepth = MathHelper.Lerp(LEG_DEPTH_ROOT, LEG_DEPTH_FOOT, station) * 0.5f;
            centre = SectionCentre(Vector3.Lerp(start, end, station), outward, halfWidth, halfDepth);

            return Section(centre + outward * ((proud - innerProud) * 0.5f), outward,
                halfWidth + (proud + innerProud) * 0.5f, halfDepth + proud);
        }

        //A section's centre: offset from the leg's centre line so its inner face and its top face land on
        //LEG_INNER_FACE and LEG_TOP_FACE whatever the section's own width and depth
        private static Vector3 SectionCentre(Vector3 onLine, Vector3 outward, float halfWidth, float halfDepth) =>
            onLine + outward * (halfWidth - LEG_INNER_FACE) + Vector3.Up * (LEG_TOP_FACE - halfDepth);

        //The middle of the top face at a station along the leg (0 at the root, 1 at the foot)
        private static Vector3 TopFaceCentre(Vector3 start, Vector3 end, float station, Vector3 outward) =>
            Vector3.Lerp(start, end, station)
            + outward * (MathHelper.Lerp(LEG_HALF_WIDTH_ROOT, LEG_HALF_WIDTH_FOOT, station) - LEG_INNER_FACE)
            + Vector3.Up * LEG_TOP_FACE;

        //The eight corners of a chamfered rectangular section standing vertical, in order round it
        private static Vector3[] Section(Vector3 centre, Vector3 outward, float halfWidth, float halfDepth)
        {
            float cut = LEG_CHAMFER * MathF.Min(halfWidth, halfDepth);
            Vector3 up = Vector3.Up;

            return new[]
            {
                centre + outward * halfWidth + up * (halfDepth - cut),
                centre + outward * (halfWidth - cut) + up * halfDepth,
                centre - outward * (halfWidth - cut) + up * halfDepth,
                centre - outward * halfWidth + up * (halfDepth - cut),
                centre - outward * halfWidth - up * (halfDepth - cut),
                centre - outward * (halfWidth - cut) - up * halfDepth,
                centre + outward * (halfWidth - cut) - up * halfDepth,
                centre + outward * halfWidth - up * (halfDepth - cut),
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
