using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The gun carriage's frame, one mesh: two cheek plates with the trunnion pins they visibly hold the
    /// barrel by, the axle the wheels turn on, and a <b>split trail</b> — two legs diverging down and back.
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
    /// chamfered box girder with two bands clamped round it, hinged into a knuckle on the cheek, ending in a
    /// raked spade with a ground edge and a gusset, with a lifting handle over its foot. <b>Two faces of the old
    /// box are kept exactly</b>: its inner face and its top face (<see cref="LEG_INNER_FACE"/>,
    /// <see cref="LEG_TOP_FACE"/>). The breech dips between the legs, so nothing the leg gained may stand
    /// inboard of the one or above the other — the girder grew outward and downward only, which makes it no
    /// worse a neighbour for the breech than the box was, by construction rather than by a sweep of every
    /// elevation. The bands are the one exception, and they sit only where arithmetic says the tube cannot
    /// reach (see <see cref="BAND_STATIONS"/>).
    /// </para>
    /// </summary>
    public class GunCarriageMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        private const int AXLE_SEGMENTS = 12;

        #region The trail legs (#403)

        //The two faces of the first cut's box the leg keeps, off its centre line (see the class note): inboard
        //of the one and above the other is where the breech goes.
        private const float LEG_INNER_FACE = 0.10f;
        private const float LEG_TOP_FACE = 0.08f;

        //A box girder tapering to its foot, the shape a real split trail's legs have: deep and broad where the
        //load comes into the cheek, slim where the spade takes it into the ground. The root's depth is bounded
        //by the wheel — a leg leaves the cheek inside the wheel's inner plate, and deeper than this its belly
        //would pass through the rollers' circle as it runs back past them.
        private const float LEG_HALF_WIDTH_ROOT = 0.11f;
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
        //CannonMesh.BASE_RING). A leg's inner face stands at about 0.75 + 0.70·station off that axis on the
        //shipped figures, so a band proud by BAND_PROUD clears the base ring from station 0.16 on. The first is
        //at twice that.
        private static readonly float[] BAND_STATIONS = { 0.34f, 0.49f };
        private const float BAND_PROUD = 0.018f;
        private const float BAND_HALF_LENGTH = 0.045f;

        //The hinge knuckle the leg swings from, standing on the cheek's outer face with a pin head on top — a
        //split trail's legs close together to travel, and the knuckle is what says so. Its radius is bounded by
        //the wheel's inner plate, which it clears by 0.04.
        private const float KNUCKLE_RADIUS = 0.075f;
        private const float KNUCKLE_HALF_LENGTH = 0.2f;
        private const float PIN_HEAD_RADIUS = 0.045f;
        private const float PIN_HEAD_HALF_LENGTH = 0.02f;
        private const int KNUCKLE_SEGMENTS = 12;

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
        /// <param name="cheekTopY">Top edge of the plates, a little above the trunnion axis they hold.</param>
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

                //A trail leg, from the cheek's lower rear corner, diverging outward as it falls back to the foot
                AddTrailLeg(builder,
                    new Vector3(side * cheekCentreX, -axleDrop * 0.75f, cheekHalfLength * 0.8f),
                    new Vector3(side * trailEnd.X, trailEnd.Y, trailEnd.Z),
                    side, cheekCentreX + cheekThickness * 0.5f);

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
        /// One leg of the split trail, from <paramref name="start"/> (buried in the cheek) to
        /// <paramref name="end"/> (its foot) — see the class note for what it is made of and the two faces it
        /// may not cross.
        /// </summary>
        /// <param name="side">−1 or +1: which side of the barrel's axis the leg stands, and so which way is outward.</param>
        /// <param name="cheekOuterX">The cheek's outer face off the axis, where the knuckle stands.</param>
        private static void AddTrailLeg(MeshBuilder builder, Vector3 start, Vector3 end, float side, float cheekOuterX)
        {
            Vector3 along = end - start;
            float length = along.Length();
            Vector3 direction = along / length;

            //The leg's own frame. Upright is world up with the leg's fall taken out, so it points up for either
            //leg; outward is square to both — which makes it horizontal — and turned away from the barrel's axis.
            Vector3 upright = Vector3.Normalize(Vector3.Up - direction * Vector3.Dot(direction, Vector3.Up));
            Vector3 outward = Vector3.Cross(upright, direction);
            if (outward.X * side < 0f) outward = -outward;

            //The girder: two chamfered sections, root and foot, and the eight long faces between them. Flat
            //faces, each with its own normal: this is machined iron, and the edges are the point.
            Vector3 rootCentre = SectionCentre(start, outward, upright, LEG_HALF_WIDTH_ROOT, LEG_DEPTH_ROOT * 0.5f);
            Vector3 footCentre = SectionCentre(end, outward, upright, LEG_HALF_WIDTH_FOOT, LEG_DEPTH_FOOT * 0.5f);
            Vector3[] root = Section(rootCentre, outward, upright, LEG_HALF_WIDTH_ROOT, LEG_DEPTH_ROOT * 0.5f);
            Vector3[] foot = Section(footCentre, outward, upright, LEG_HALF_WIDTH_FOOT, LEG_DEPTH_FOOT * 0.5f);

            for (int i = 0; i < SECTION_CORNERS; i++)
            {
                int j = (i + 1) % SECTION_CORNERS;

                Vector3 normal = Vector3.Cross(root[j] - root[i], foot[i] - root[i]);
                if (Vector3.Dot(normal, (root[i] + root[j]) * 0.5f - rootCentre) < 0f) normal = -normal;
                normal = Vector3.Normalize(normal);

                builder.AddQuad(root[i], root[j], foot[j], foot[i], normal, normal, normal, normal, normal);

                //The two ends: the root's is inside the cheek and the foot's inside the spade, but a cap costs
                //two triangles and an open end would show the moment either figure is retuned
                builder.AddTriangle(rootCentre, root[i], root[j], -direction, -direction, -direction, -direction);
                builder.AddTriangle(footCentre, foot[i], foot[j], direction, direction, direction, direction);
            }

            foreach (float station in BAND_STATIONS)
                AddBand(builder, Vector3.Lerp(start, end, station), station, direction, outward, upright);

            //The hinge knuckle on the cheek's outer face, centred on the root section's depth, and its pin head
            Vector3 knuckle = new(side * (cheekOuterX + KNUCKLE_RADIUS),
                start.Y + LEG_TOP_FACE - LEG_DEPTH_ROOT * 0.5f, start.Z);

            builder.AddTube(knuckle, Vector3.Up, Vector3.UnitZ, KNUCKLE_HALF_LENGTH, KNUCKLE_RADIUS, KNUCKLE_SEGMENTS);
            builder.AddTube(knuckle + Vector3.Up * (KNUCKLE_HALF_LENGTH + PIN_HEAD_HALF_LENGTH), Vector3.Up,
                Vector3.UnitZ, PIN_HEAD_HALF_LENGTH, PIN_HEAD_RADIUS, KNUCKLE_SEGMENTS);

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

        //One band round the leg (see BAND_STATIONS): a short collar whose section is the leg's own there grown by
        //BAND_PROUD on every side, closed down onto the leg by a ring at either end
        private static void AddBand(MeshBuilder builder, Vector3 onLine, float station, Vector3 direction,
            Vector3 outward, Vector3 upright)
        {
            float halfWidth = MathHelper.Lerp(LEG_HALF_WIDTH_ROOT, LEG_HALF_WIDTH_FOOT, station);
            float halfDepth = MathHelper.Lerp(LEG_DEPTH_ROOT, LEG_DEPTH_FOOT, station) * 0.5f;
            Vector3 centre = SectionCentre(onLine, outward, upright, halfWidth, halfDepth);

            Vector3[] leg = Section(centre, outward, upright, halfWidth, halfDepth);
            Vector3[] band = Section(centre, outward, upright, halfWidth + BAND_PROUD, halfDepth + BAND_PROUD);
            Vector3 half = direction * BAND_HALF_LENGTH;

            for (int i = 0; i < SECTION_CORNERS; i++)
            {
                int j = (i + 1) % SECTION_CORNERS;

                Vector3 normal = Vector3.Cross(band[j] - band[i], direction);
                if (Vector3.Dot(normal, (band[i] + band[j]) * 0.5f - centre) < 0f) normal = -normal;
                normal = Vector3.Normalize(normal);

                builder.AddQuad(band[i] - half, band[j] - half, band[j] + half, band[i] + half,
                    normal, normal, normal, normal, normal);

                builder.AddQuad(band[i] - half, band[j] - half, leg[j] - half, leg[i] - half,
                    -direction, -direction, -direction, -direction, -direction);
                builder.AddQuad(band[i] + half, band[j] + half, leg[j] + half, leg[i] + half,
                    direction, direction, direction, direction, direction);
            }
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
