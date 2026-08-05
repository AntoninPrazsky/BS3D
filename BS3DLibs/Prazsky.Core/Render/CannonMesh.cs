using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A procedurally generated cannon barrel: a profiled, thick-walled tube along the Z axis with a long
    /// loading window cut in the top, so the balls queued inside show through it. The muzzle (local −Z) is
    /// open — the shot leaves through it — and wears the classic swell; the breech, the end the player's
    /// camera looks at, is <b>closed</b> by a dome carrying a cascabel knob, so the gun no longer shows the
    /// same open hole at both ends. Between them the outer surface runs the profile every muzzle-loader ran:
    /// swell, slim chase, a girdle ring ahead of the trunnions, the reinforce taper thickening towards the
    /// base ring, and the breech band the dome springs from.
    /// <para>
    /// The window deliberately stops <paramref name="chamberDepth"/> short of the breech face, and behind
    /// that face the dome hides a chamber cavity of the same depth: together they are where the freshest
    /// round waits out the post-shot glide (<c>Prazsky.BS3D.Magazine</c> parks it there — see
    /// <c>CannonRig.CHAMBER_DEPTH</c>), which is what an <i>open</i> breech used to be for. The slot's cut
    /// edges are closed by rim faces following the outer profile, so the wall never reads as paper-thin.
    /// </para>
    /// <para>
    /// The caller orients it with <see cref="Matrix.CreateWorld(Vector3, Vector3, Vector3)"/> so the muzzle
    /// points down the aim direction and the window (local +Y) faces up, and draws the queued balls in a
    /// line along the same axis. Wound clockwise seen from outside via <see cref="MeshBuilder"/>, so the
    /// outward face is the front one under MonoGame's default back-face culling.
    /// </para>
    /// </summary>
    public class CannonMesh : IProceduralMesh, IDisposable
    {
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>The cascabel's pole: the mesh's furthest station behind the breech face, in the frame of
        /// the constructor's Z arguments. The breech side outreaches the muzzle side now, which is what the
        /// camera fit's box around the gun has to hold (<c>CannonRig.BarrelReach</c> reads it).</summary>
        public float PoleZ { get; }

        #region The barrel's own styling

        //The silhouette's figures: radii as offsets from the base outer radius (bore + wall), so a retuned
        //wall carries the whole profile with it; lengthwise placements as distances behind the muzzle face
        //or ahead of the breech face, so a longer magazine stretches only the plain chase between them.
        //Tuned at the shipped bore/wall (0.6/0.14) and a tube ~5 long; the ordering assumes at least ~4.5
        //of tube (girdle behind swell, slot's end ahead of the base ring), which the 5-ball magazine gives.
        private const float MUZZLE_FACE_LIP = 0.025f; //the flat muzzle face's outer edge
        private const float SWELL_CREST = 0.055f;     //the muzzle swell's high point
        private const float SWELL_NECK = -0.045f;     //where the swell sweeps back in
        private const float CHASE_DIP = -0.075f;      //the slimmest steel on the gun, ahead of the girdle
        private const float GIRDLE_RING = 0.01f;      //the girdle's proud band
        private const float GIRDLE_SEAT = -0.045f;    //the tube on either side of the girdle
        private const float BREECH_TAPER = 0.035f;    //the reinforce's rise arriving at the base ring
        private const float BASE_RING = 0.105f;       //the thickest steel on the gun
        private const float BREECH_BAND = 0.06f;      //the short band the dome springs from

        private const float SWELL_CREST_Z = 0.12f;    //behind the muzzle face
        private const float SWELL_NECK_Z = 0.40f;
        private const float CHASE_DIP_Z = 0.62f;
        private const float GIRDLE_FRONT_Z = 1.35f;   //the girdle stands ahead of the carriage's cheeks
        private const float GIRDLE_BACK_Z = 1.58f;
        private const float BASE_RING_FRONT_Z = 0.32f; //ahead of the breech face
        private const float BASE_RING_BACK_Z = 0.16f;

        //The breech dome and its cascabel, one revolved curve from the breech band to the axis: (distance
        //behind the breech face, radius as a fraction of the band it springs from). The first five are the
        //dome's fall, then the neck pinches in and the knob closes on the pole. The dome's belly must stay
        //clear of the chamber cavity it encloses (chamberDepth deep, a bore wide); these figures keep
        //≥ 0.15 of steel over it throughout at the shipped bore and chamber depth.
        private static readonly Vector2[] DOME_PROFILE =
        {
            new(0.12f, 0.956f),
            new(0.24f, 0.863f),
            new(0.36f, 0.706f),
            new(0.46f, 0.500f),
            new(0.53f, 0.325f),
            new(0.57f, 0.213f), //the cascabel's neck
            new(0.62f, 0.250f), //its knob
            new(0.67f, 0.231f),
            new(0.71f, 0.150f),
            new(0.74f, 0f),     //the pole
        };

        //Arc steps closing the chamber cavity — a quarter-ellipse from the bore's back edge to the axis
        private const int CHAMBER_ARCS = 5;

        #endregion

        /// <summary>One ring of the revolved profile: where it stands, how wide it is, and whether the runs
        /// meeting there keep their own normals (a ring's flat step must not smooth into the band beside
        /// it — <see cref="LathePoint.Crease"/>'s convention).</summary>
        private readonly struct Station
        {
            public readonly float Z;
            public readonly float R;
            public readonly bool Crease;

            public Station(float z, float r, bool crease)
            {
                Z = z;
                R = r;
                Crease = crease;
            }
        }

        /// <param name="boreRadius">Inner radius of the tube; a little over the ball radius so a ball nests inside.</param>
        /// <param name="wallThickness">Radial thickness of the wall at its plainest; the profile swells and dips around it.</param>
        /// <param name="frontZ">Z of the muzzle face, and <paramref name="backZ"/> of the breech face the dome closes.</param>
        /// <param name="slotHalfAngle">Half-width of the top window, in radians, measured from straight up.</param>
        /// <param name="slotEndZ">Where the window stops, ahead of <paramref name="backZ"/> — the closed run
        /// between them is the hood the parked round hides under.</param>
        /// <param name="chamberDepth">How deep the cavity inside the dome runs behind the breech face.</param>
        /// <param name="segments">Angular segments across the solid wall arc; the window and the closed
        /// revolution reuse the same density, so their stations line up and the seams are watertight.</param>
        public CannonMesh(GraphicsDevice graphicsDevice, float boreRadius, float wallThickness, float frontZ, float backZ,
            float slotHalfAngle, float slotEndZ, float chamberDepth, int segments)
        {
            float outer = boreRadius + wallThickness;

            //Angular stations. The window is centred on straight up (+Y); the solid wall arc covers the rest
            //at the caller's segment count, and the window's own span is appended at the same density — the
            //full-revolution parts (the hood, the dome) then share the wall's exact stations, so the seam
            //where the slot ends cannot crack open.
            float start = Constants.HALF_PI + slotHalfAngle;
            float sweep = MathHelper.TwoPi - 2f * slotHalfAngle;
            int windowSegments = Math.Max(3, (int)MathF.Ceiling(2f * slotHalfAngle * segments / sweep));
            int ringSegments = segments + windowSegments;

            var dirs = new Vector3[ringSegments + 1];

            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i <= segments
                    ? start + sweep * i / segments
                    : start + sweep + 2f * slotHalfAngle * (i - segments) / windowSegments;

                dirs[i] = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
            }

            //The ring closes on the wall's first station exactly, not within float noise of it
            dirs[ringSegments] = dirs[0];

            MeshBuilder builder = new();

            //=== The outer profile, muzzle face to cascabel pole ==================================

            //The reinforce taper is one straight run from the girdle to the base ring; the station the
            //window's end must land on sits partway along it, on the same line, so the shading crosses the
            //slot's end without a seam even though the tessellation changes there.
            float taperFrontZ = frontZ + GIRDLE_BACK_Z;
            float taperBackZ = backZ - BASE_RING_FRONT_Z;
            float seatR = outer + GIRDLE_SEAT;
            float taperEndR = outer + BREECH_TAPER;
            float slotEndR = MathHelper.Lerp(seatR, taperEndR, (slotEndZ - taperFrontZ) / (taperBackZ - taperFrontZ));
            float springR = outer + BREECH_BAND;

            var outerProfile = new List<Station>
            {
                new(frontZ, outer + MUZZLE_FACE_LIP, crease: true),
                new(frontZ + SWELL_CREST_Z, outer + SWELL_CREST, crease: false),
                new(frontZ + SWELL_NECK_Z, outer + SWELL_NECK, crease: false),
                new(frontZ + CHASE_DIP_Z, outer + CHASE_DIP, crease: false),
                new(frontZ + GIRDLE_FRONT_Z, seatR, crease: true),
                new(frontZ + GIRDLE_FRONT_Z, outer + GIRDLE_RING, crease: true),
                new(taperFrontZ, outer + GIRDLE_RING, crease: true),
                new(taperFrontZ, seatR, crease: true),
            };

            int slotEndIndex = outerProfile.Count;
            outerProfile.Add(new Station(slotEndZ, slotEndR, crease: false));

            outerProfile.Add(new Station(taperBackZ, taperEndR, crease: true));
            outerProfile.Add(new Station(taperBackZ, outer + BASE_RING, crease: true));
            outerProfile.Add(new Station(backZ - BASE_RING_BACK_Z, outer + BASE_RING, crease: true));
            outerProfile.Add(new Station(backZ - BASE_RING_BACK_Z, springR, crease: true));
            outerProfile.Add(new Station(backZ, springR, crease: false));

            foreach (Vector2 point in DOME_PROFILE)
                outerProfile.Add(new Station(backZ + point.X, springR * point.Y, crease: false));

            //The slotted run spans only the wall arc; the hooded run from the slot's end back is the full
            //revolution. They share the mid-taper station, so the surface is one skin.
            Revolve(builder, dirs, outerProfile, 0, slotEndIndex + 1, 0, segments, inward: false);
            Revolve(builder, dirs, outerProfile, slotEndIndex, outerProfile.Count - slotEndIndex, 0, ringSegments,
                inward: false);

            //=== The bore and the chamber cavity ==================================================

            var boreSlotted = new List<Station>
            {
                new(frontZ, boreRadius, crease: true),
                new(slotEndZ, boreRadius, crease: true),
            };

            Revolve(builder, dirs, boreSlotted, 0, boreSlotted.Count, 0, segments, inward: true);

            //Under the hood the bore closes into the dome's cavity: a quarter-ellipse the parked round's
            //back hemisphere nests into. Smooth at the spring — the ellipse leaves the cylinder tangent.
            var boreHooded = new List<Station>
            {
                new(slotEndZ, boreRadius, crease: true),
                new(backZ, boreRadius, crease: false),
            };

            for (int arc = 1; arc <= CHAMBER_ARCS; arc++)
            {
                float phi = Constants.HALF_PI * arc / CHAMBER_ARCS;
                boreHooded.Add(new Station(backZ + chamberDepth * MathF.Sin(phi), boreRadius * MathF.Cos(phi),
                    crease: false));
            }

            Revolve(builder, dirs, boreHooded, 0, boreHooded.Count, 0, ringSegments, inward: true);

            //=== The faces that close the cuts ====================================================

            //The muzzle face: the annular front of the wall, across the solid arc only — the window runs
            //out through it, as it always did
            float muzzleFaceR = outer + MUZZLE_FACE_LIP;

            for (int i = 0; i < segments; i++)
            {
                builder.AddQuad(
                    dirs[i] * boreRadius + new Vector3(0f, 0f, frontZ),
                    dirs[i + 1] * boreRadius + new Vector3(0f, 0f, frontZ),
                    dirs[i + 1] * muzzleFaceR + new Vector3(0f, 0f, frontZ),
                    dirs[i] * muzzleFaceR + new Vector3(0f, 0f, frontZ),
                    Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward);
            }

            //The window's two cut edges, wall-thick strips following the outer profile so the wall never
            //reads as paper-thin; each faces into the slot, like the rims they replace
            AddSlotCheek(builder, outerProfile, slotEndIndex, dirs[0], boreRadius, tangentTowardSlot: -1f);
            AddSlotCheek(builder, outerProfile, slotEndIndex, dirs[segments], boreRadius, tangentTowardSlot: +1f);

            //And the window's back edge: the arc that stops the slot ahead of the hood, facing the muzzle —
            //the lip the parked round waits behind
            for (int i = segments; i < ringSegments; i++)
            {
                builder.AddQuad(
                    dirs[i] * boreRadius + new Vector3(0f, 0f, slotEndZ),
                    dirs[i + 1] * boreRadius + new Vector3(0f, 0f, slotEndZ),
                    dirs[i + 1] * slotEndR + new Vector3(0f, 0f, slotEndZ),
                    dirs[i] * slotEndR + new Vector3(0f, 0f, slotEndZ),
                    Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward);
            }

            (VertexBuffer, IndexBuffer, PrimitiveCount) = builder.Build(graphicsDevice);

            float widest = 0f;
            foreach (Station station in outerProfile) widest = MathF.Max(widest, station.R);

            float poleZ = outerProfile[^1].Z;
            PoleZ = poleZ;

            float halfLength = (poleZ - frontZ) * Constants.HALF;

            BoundingSphere = new BoundingSphere(new Vector3(0f, 0f, (frontZ + poleZ) * Constants.HALF),
                MathF.Sqrt(widest * widest + halfLength * halfLength));
        }

        /// <summary>
        /// One run of profile stations revolved about Z between the given angular stations. Normals are the
        /// profile's own, turned a quarter towards +radius in the (z, r) plane and smoothed across smooth
        /// junctions (<see cref="Station.Crease"/> holds each side's own, <see cref="LatheMesh"/>'s
        /// convention); <paramref name="inward"/> flips them for surfaces seen from the bore. A station on
        /// the axis closes the run with a fan to the pole.
        /// </summary>
        private static void Revolve(MeshBuilder builder, Vector3[] dirs, List<Station> profile, int first, int count,
            int firstDir, int dirCount, bool inward)
        {
            int spans = count - 1;
            var spanNormals = new Vector2[spans];

            for (int k = 0; k < spans; k++)
            {
                Station a = profile[first + k];
                Station b = profile[first + k + 1];

                //(z, r) → the outward normal (nz, nr): a plain cylinder's comes out radial
                spanNormals[k] = Vector2.Normalize(new Vector2(-(b.R - a.R), b.Z - a.Z));
                if (inward) spanNormals[k] = -spanNormals[k];
            }

            for (int k = 0; k < spans; k++)
            {
                Station s0 = profile[first + k];
                Station s1 = profile[first + k + 1];

                Vector2 n0 = !s0.Crease && k > 0 ? Vector2.Normalize(spanNormals[k - 1] + spanNormals[k]) : spanNormals[k];
                Vector2 n1 = !s1.Crease && k < spans - 1 ? Vector2.Normalize(spanNormals[k] + spanNormals[k + 1]) : spanNormals[k];

                for (int i = firstDir; i < firstDir + dirCount; i++)
                {
                    Vector3 mid = Vector3.Normalize(dirs[i] + dirs[i + 1]);
                    Vector3 face = Turn(mid, spanNormals[k]);

                    if (s1.R <= 1e-5f)
                    {
                        //The profile closes on the axis: a fan whose pole points straight along it
                        Vector3 poleNormal = spanNormals[k].X > 0f ? Vector3.Backward : Vector3.Forward;

                        builder.AddTriangle(
                            At(dirs[i], s0), At(dirs[i + 1], s0), new Vector3(0f, 0f, s1.Z),
                            Turn(dirs[i], n0), Turn(dirs[i + 1], n0), poleNormal, face);
                    }
                    else
                    {
                        builder.AddQuad(
                            At(dirs[i], s0), At(dirs[i + 1], s0), At(dirs[i + 1], s1), At(dirs[i], s1),
                            Turn(dirs[i], n0), Turn(dirs[i + 1], n0), Turn(dirs[i + 1], n1), Turn(dirs[i], n1), face);
                    }
                }
            }
        }

        /// <summary>One cut edge of the window: wall-thick quads at a fixed angle from the bore out to the
        /// outer profile, one per profile span, all facing into the slot.</summary>
        private static void AddSlotCheek(MeshBuilder builder, List<Station> outerProfile, int slotEndIndex,
            Vector3 radial, float boreRadius, float tangentTowardSlot)
        {
            Vector3 faceNormal = new Vector3(-radial.Y, radial.X, 0f) * tangentTowardSlot;

            for (int k = 0; k < slotEndIndex; k++)
            {
                Station s0 = outerProfile[k];
                Station s1 = outerProfile[k + 1];

                //A ring's step spans no length; the strips on either side already tile the jump
                if (s1.Z - s0.Z < 1e-5f) continue;

                builder.AddQuad(
                    radial * boreRadius + new Vector3(0f, 0f, s0.Z),
                    radial * boreRadius + new Vector3(0f, 0f, s1.Z),
                    radial * s1.R + new Vector3(0f, 0f, s1.Z),
                    radial * s0.R + new Vector3(0f, 0f, s0.Z),
                    faceNormal, faceNormal, faceNormal, faceNormal, faceNormal);
            }
        }

        private static Vector3 At(Vector3 dir, Station station) =>
            new(dir.X * station.R, dir.Y * station.R, station.Z);

        /// <summary>A profile-plane normal (nz, nr) stood up at one angular station.</summary>
        private static Vector3 Turn(Vector3 dir, Vector2 normal) =>
            new(dir.X * normal.Y, dir.Y * normal.Y, normal.X);

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer?.Dispose();
            IndexBuffer = null;
        }
    }
}
