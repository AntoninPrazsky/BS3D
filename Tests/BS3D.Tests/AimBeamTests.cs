using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using MathHelper = Microsoft.Xna.Framework.MathHelper;
using XVector2 = Microsoft.Xna.Framework.Vector2;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #1144 and #1196: where the shot snaps into a lattice cell, the aim beam's far end splits into a funnel of crisp lines
    /// the ghost sits inside, and the rest of the beam is the line it always was. <see cref="AimBeam.Lay"/> is asked without
    /// a graphics device. What a lens sees of the funnel is reckoned by projecting the rays' pieces onto its screen
    /// (<see cref="Lens"/>); the dashes' light as <c>ShotTrail.fx</c>'s streak draws it (<see cref="Light"/>): the alpha
    /// across a piece <c>(1 - |x|/w)²</c>, its quarter-length fades along, and the additive blend weighing the alpha twice.
    /// </summary>
    public class AimBeamTests(ITestOutputHelper output)
    {
        private static readonly string[] RigLevels =
            ["Pennant.json", "One.json", "Colossus.json", "Heart.json", "Rainbow.json", "Zigzag.json"];

        /// <summary>One aim of the measurement: the beam's flight as the Game draws it and the ghost's centre.</summary>
        private readonly record struct Aim(XVector3[] Path, XVector3 Ghost, float Lateral, float Along);

        /// <summary>
        /// The aims <c>ShotAgreementTests</c> sweeps, each asked as the Game asks (<c>GameplayScreen.UpdateShotPreview</c>:
        /// the curved sweep with the world's gravity, its path, then the cell and the drift) on a level hung and settled
        /// once, since nothing is fired. The lateral and along offsets are the ghost's from the line through the beam's
        /// end, along its last leg.
        /// </summary>
        private static List<Aim> Measure(int aimsPerLevel)
        {
            List<Aim> aims = new();
            List<XVector3> path = new();

            foreach (string level in RigLevels)
            {
                using HungLevel hung = HungLevel.FromLevelFile(ShotRig.LevelPath(level));
                hung.Run(1.5f);

                Random rng = new(1144 + level.Length);

                for (int i = 0; i < aimsPerLevel; i++)
                {
                    (XVector3 muzzle, XVector3 target, _) = ShotAgreementTests.RandomAim(hung.Balls, rng);
                    XVector3 velocity = XVector3.Normalize(target - muzzle) * ShotRig.SHOOT_SPEED;

                    if (!ShotPlacement.TryFindFirstHitCurved(hung.Balls, muzzle, velocity, 2f * BallsConstraintsBuilder.BALL_RADIUS,
                            null, out PhysicsBall hit, out XVector3 contact, path, null, worldGravity: true)) continue;

                    if (!ShotPlacement.TrySolveAgainstBall(hung.Map, hit, contact, hung.WorldOffset, out XZLevel cell,
                            out XVector3 drift)) continue;

                    XVector3 ghost = ShotPlacement.CellWorldPosition(hung.Map, cell, hung.WorldOffset, drift);
                    XVector3 end = path[^1];
                    XVector3 axis = XVector3.Normalize(path[^1] - path[^2]);

                    XVector3 offset = ghost - end;
                    float along = XVector3.Dot(offset, axis);

                    aims.Add(new Aim(path.ToArray(), ghost, (offset - axis * along).Length(), along));
                }
            }

            return aims;
        }

        /// <summary>
        /// A pinhole lens at <see cref="Eye"/> looking at a point: where a world point lands on its screen, in units of the
        /// image plane one unit in front of it (a frame's pixels are these times its pixels per unit), and how deep it is.
        /// </summary>
        private readonly record struct Lens(XVector3 Eye, XVector3 Forward, XVector3 Right, XVector3 Up)
        {
            public static Lens LookingAt(XVector3 eye, XVector3 target)
            {
                XVector3 forward = XVector3.Normalize(target - eye);
                XVector3 helper = MathF.Abs(forward.Y) < 0.95f ? XVector3.UnitY : XVector3.UnitX;
                XVector3 right = XVector3.Normalize(XVector3.Cross(forward, helper));
                return new Lens(eye, forward, right, XVector3.Cross(right, forward));
            }

            public float Depth(XVector3 point) => XVector3.Dot(point - Eye, Forward);

            public XVector2 Project(XVector3 point)
            {
                XVector3 q = point - Eye;
                float z = XVector3.Dot(q, Forward);
                return new XVector2(XVector3.Dot(q, Right) / z, XVector3.Dot(q, Up) / z);
            }
        }

        /// <summary>
        /// The light a dash <paramref name="piece"/> puts at <paramref name="at"/>, in units of a dash's core, as
        /// <c>ShotTrail.fx</c>'s streak draws it, for a point in the plane the billboard turns into.
        /// </summary>
        private static float Light(in AimBeam.Piece piece, XVector3 at)
        {
            XVector3 axis = piece.Head - piece.Tail;
            float length = axis.Length();
            if (length < 1e-5f) return 0f;

            XVector3 direction = axis / length;
            float t = XVector3.Dot(at - piece.Tail, direction) / length;
            if (t < 0f || t > 1f) return 0f;

            float distance = (at - (piece.Tail + direction * (t * length))).Length();
            float width = piece.TailWidth + (piece.HeadWidth - piece.TailWidth) * t;
            float across = 1f - distance / width;
            if (across <= 0f) return 0f;

            float alpha = across * across * SmoothStep(0f, 0.25f, t) * SmoothStep(1f, 0.75f, t) * piece.TailAlpha;
            return alpha * alpha;
        }

        //HLSL's smoothstep, edges either way round
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        //The dashes' light at a point: the rays add none (they are laid over the frame, not added to it)
        private static float DashLightAt(ReadOnlySpan<AimBeam.Piece> pieces, XVector3 at)
        {
            float sum = 0f;
            foreach (AimBeam.Piece piece in pieces)
                if (piece.Kind == AimBeam.PieceKind.Dash) sum += Light(piece, at);
            return sum;
        }

        private static AimBeam.Piece[] Lay(XVector3[] path, float phase, bool openEnded, float spread, XVector3 viewer)
        {
            AimBeam.Piece[] pieces = new AimBeam.Piece[AimBeam.MAX_PIECES];
            int count = AimBeam.Lay(path, phase, openEnded, spread, viewer, pieces);
            return pieces[..count];
        }

        //The tests of the layout's own shape: a lens square to the beam at its far end
        private static AimBeam.Piece[] Lay(XVector3[] path, float phase, bool openEnded, float spread) =>
            Lay(path, phase, openEnded, spread, SideOn(path));

        //A lens square to the far leg at the touch, twenty-five units off it
        private static XVector3 SideOn(XVector3[] path)
        {
            XVector3 far = XVector3.Normalize(path[^1] - path[^2]);
            XVector3 helper = MathF.Abs(far.Y) < 0.9f ? XVector3.UnitY : XVector3.UnitX;
            return path[^1] + XVector3.Normalize(XVector3.Cross(far, helper)) * 25f;
        }

        private static float Percentile(List<float> values, double p)
        {
            List<float> sorted = new(values);
            sorted.Sort();
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * sorted.Count))];
        }

        /// <summary>
        /// The Game's overview lens for an aim: behind the gun along its bearing (outwards from the field's axis, where
        /// <c>RandomAim</c> stands its muzzles) by the stand-off the fit puts the gun in front of it, and below the muzzle
        /// by the lens's height under the trunnions and about a unit more, which the muzzle stands over them. It sees the
        /// beam ten-odd degrees off end on, which is where the far end is hardest to read.
        /// </summary>
        private static XVector3 Overview(XVector3 muzzle) =>
            muzzle + XVector3.Normalize(new XVector3(muzzle.X, 0f, muzzle.Z)) * GameCameraFit.CANNON_CAMERA_STANDOFF
            + XVector3.UnitY * (GameCameraFit.CAMERA_HEIGHT - 1f);

        /// <summary>
        /// A lens <paramref name="degrees"/> off the far leg at the touch, on side <paramref name="side"/> of it (0 to 3, a
        /// quarter turn round the leg apart, 0 above it), as far from the touch as the overview stands.
        /// </summary>
        private static XVector3 OffBeam(XVector3[] path, float degrees, int side)
        {
            XVector3 far = XVector3.Normalize(path[^1] - path[^2]);
            XVector3 up = XVector3.Normalize(XVector3.UnitY - far * XVector3.Dot(XVector3.UnitY, far));
            XVector3 right = XVector3.Cross(far, up);
            XVector3 off = side switch { 0 => up, 1 => right, 2 => -up, _ => -right };

            float angle = MathHelper.ToRadians(degrees);
            float distance = XVector3.Distance(Overview(path[0]), path[^1]);
            return path[^1] - (far * MathF.Cos(angle) - off * MathF.Sin(angle)) * distance;
        }

        /// <summary>A ray's alpha, against <see cref="AimBeam.RAY_ALPHA"/>, below which it no longer counts as outlining anything.</summary>
        private const float VISIBLE = 0.2f;

        //One lane of the rays, its pieces in the order laid (from the split outwards)
        private static List<AimBeam.Piece> RayLane(AimBeam.Piece[] pieces, int lane)
        {
            List<AimBeam.Piece> ray = new();
            foreach (AimBeam.Piece p in pieces)
                if (p.Kind == AimBeam.PieceKind.Ray && p.Lane == lane) ray.Add(p);
            return ray;
        }

        private const int OUTER = (AimBeam.RAYS - 1) / 2;

        /// <summary>
        /// Whether <paramref name="ghost"/>, seen through <paramref name="lens"/>, sits inside the funnel
        /// <paramref name="pieces"/> outline: between the two outer rays on screen, at a point of their run where they are
        /// still at least <see cref="VISIBLE"/> of the alpha they split off the line with. The outer rays are cut at the
        /// same stations, so the funnel on screen is the run of quadrilaterals between their pieces.
        /// </summary>
        private static bool GhostInside(AimBeam.Piece[] pieces, Lens lens, XVector3 ghost)
        {
            List<AimBeam.Piece> left = RayLane(pieces, -OUTER), right = RayLane(pieces, OUTER);
            if (left.Count == 0 || left.Count != right.Count) return false;

            XVector2 g = lens.Project(ghost);

            for (int j = 0; j < left.Count; j++)
            {
                XVector2 a = lens.Project(left[j].Tail), b = lens.Project(left[j].Head);
                XVector2 c = lens.Project(right[j].Head), d = lens.Project(right[j].Tail);
                if (!InTriangle(g, a, b, c) && !InTriangle(g, a, c, d)) continue;

                //How far along the piece the ghost stands, by its place between the quadrilateral's two ends
                XVector2 tail = (a + d) * 0.5f, axis = (b + c) * 0.5f - tail;
                float t = axis.LengthSquared() > 0f ? Math.Clamp(XVector2.Dot(g - tail, axis) / axis.LengthSquared(), 0f, 1f) : 0f;

                if (MathHelper.Lerp(left[j].TailAlpha, left[j].HeadAlpha, t) >= VISIBLE * AimBeam.RAY_ALPHA) return true;
            }

            return false;
        }

        private static bool InTriangle(XVector2 p, XVector2 a, XVector2 b, XVector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f, positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);

            static float Cross(XVector2 u, XVector2 v) => u.X * v.Y - u.Y * v.X;
        }

        /// <summary>
        /// <b>The ghost sits inside the funnel the beam splits into, on screen</b>, which is the issue's whole promise. For
        /// each measured aim the beam is laid as the Game lays it (<see cref="AimBeam.LANDING_SPREAD"/>, for the lens it is
        /// seen from) and the ghost's centre is looked at through that lens (<see cref="GhostInside"/>): from the Game's
        /// overview, which sees the beam nearly end on and is the hardest view, and from lenses 10, 20, 45 and 90 degrees off
        /// the beam on four sides of it. A ghost standing off the line towards a lens or away from it shows on screen along
        /// the beam, past the touch or short of it, which only rays laid on past the touch square to the lens cover. Held
        /// for 98 % of the aims from every lens.
        /// <para>
        /// <b>And the same question must fail where it should</b> (BestPractices.md §10): the plain line (no spread) holds
        /// no ghost, and from the overview a funnel laid square to another lens (one side on, where the rays spread across
        /// the flight's plane with that lens and run past the touch along the flight) holds far fewer.
        /// </para>
        /// </summary>
        [Fact]
        public void TheGhostSitsInsideTheFunnelOnScreen()
        {
            List<Aim> aims = Measure(80);
            Assert.True(aims.Count >= 400, $"only {aims.Count} of 480 aims had a ghost");

            List<float> lateral = aims.ConvertAll(a => a.Lateral);
            List<float> along = aims.ConvertAll(a => a.Along);
            output.WriteLine($"{aims.Count} aims with a ghost. Ghost off the line through the touch: median {Percentile(lateral, 0.5):0.000}, " +
                $"95th {Percentile(lateral, 0.95):0.000}, 99th {Percentile(lateral, 0.99):0.000}, most {Percentile(lateral, 1):0.000}; " +
                $"along it {Percentile(along, 0):0.000} to {Percentile(along, 1):0.000}, median {Percentile(along, 0.5):0.000}");

            Assert.True(Percentile(lateral, 0.99) <= AimBeam.LANDING_SPREAD,
                $"the ghost stands up to {Percentile(lateral, 0.99):0.000} off the line at the 99th percentile, past LANDING_SPREAD {AimBeam.LANDING_SPREAD}");

            //The overview's own angle off the far leg, so the figure the lens is described by is the one measured
            List<float> overviewAngles = aims.ConvertAll(a => MathHelper.ToDegrees(MathF.Acos(XVector3.Dot(
                XVector3.Normalize(a.Path[^1] - a.Path[^2]), XVector3.Normalize(a.Path[^1] - Overview(a.Path[0]))))));
            output.WriteLine($"the overview sees the far leg {Percentile(overviewAngles, 0):0.0} to {Percentile(overviewAngles, 1):0.0} degrees off end on, " +
                $"{Percentile(overviewAngles, 0.5):0.0} at the median");

            int squareToAnother = 0;
            Check("the overview", aim => Overview(aim.Path[0]));

            foreach (float degrees in new[] { 10f, 20f, 45f, 90f })
                for (int side = 0; side < 4; side++)
                {
                    int s = side;
                    Check($"{degrees} degrees off the beam, side {side}", aim => OffBeam(aim.Path, degrees, s));
                }

            output.WriteLine($"from the overview, a funnel laid square to a lens side on: {squareToAnother} of {aims.Count}");
            Assert.True(squareToAnother <= 0.85 * aims.Count,
                $"{squareToAnother} of {aims.Count} ghosts sat inside a funnel laid for a lens side on, seen from the overview: " +
                "the question cannot tell a funnel square to the lens from one in another plane");

            void Check(string name, Func<Aim, XVector3> eye)
            {
                int inside = 0, insideWithoutSpread = 0;
                bool overview = name == "the overview";

                foreach (Aim aim in aims)
                {
                    XVector3 camera = eye(aim);
                    Lens lens = Lens.LookingAt(camera, aim.Path[^1]);

                    if (GhostInside(Lay(aim.Path, 0f, false, AimBeam.LANDING_SPREAD, camera), lens, aim.Ghost)) inside++;
                    if (GhostInside(Lay(aim.Path, 0f, false, 0f, camera), lens, aim.Ghost)) insideWithoutSpread++;

                    if (overview && GhostInside(Lay(aim.Path, 0f, false, AimBeam.LANDING_SPREAD, SideOn(aim.Path)), lens, aim.Ghost))
                        squareToAnother++;
                }

                output.WriteLine($"{name}: inside the funnel {inside} of {aims.Count} ({100f * inside / aims.Count:0.0} %), " +
                    $"inside the plain line {insideWithoutSpread}");

                Assert.True(inside >= 0.98 * aims.Count, $"from {name} only {inside} of {aims.Count} ghosts sit inside the funnel");
                Assert.True(insideWithoutSpread <= 0.2 * aims.Count,
                    $"from {name} {insideWithoutSpread} of {aims.Count} ghosts already sat inside the plain line: the question cannot tell the funnel from it");
            }
        }

        /// <summary>
        /// <b>The rays read as separate lines from every lens, and never as rungs.</b> Where the funnel is open, neighbouring
        /// rays stand apart on screen by at least three times a ray's own width there (so the gap between them is twice the
        /// line), from the overview and from the lenses round the beam, for every measured aim: the funnel does not collapse
        /// into one smear. And every ray is one unbroken line, each piece starting where the one before it ended, so there
        /// are no dashes on them to stand level with each other across the funnel and read as a ladder (#1144's widened
        /// dashes did, from the overview).
        /// <para>
        /// <b>Seen to fail</b>: from the overview, a funnel laid square to a lens side on folds onto the line for most aims.
        /// </para>
        /// </summary>
        [Fact]
        public void TheRaysStandApartOnScreenAndAreUnbroken()
        {
            List<Aim> aims = Measure(40);
            Assert.True(aims.Count >= 200, $"only {aims.Count} of 240 aims had a ghost");

            List<float> overviewRatios = new(), allRatios = new(), squareToAnother = new();
            List<float> openingPixels = new(), overrunPixels = new(), gapPixels = new(), dashDraws = new(), rayDraws = new();

            //Pixels a unit of the image plane is at a 1080-line frame through the play lens the capture logs (39.4 degrees)
            float pixelsPerUnit = 540f / MathF.Tan(MathHelper.ToRadians(39.4f) * 0.5f);

            foreach (Aim aim in aims)
            {
                List<XVector3> eyes = [Overview(aim.Path[0])];
                foreach (float degrees in new[] { 10f, 45f, 90f })
                    for (int side = 0; side < 4; side++) eyes.Add(OffBeam(aim.Path, degrees, side));

                for (int e = 0; e < eyes.Count; e++)
                {
                    Lens lens = Lens.LookingAt(eyes[e], aim.Path[^1]);
                    AimBeam.Piece[] pieces = Lay(aim.Path, 0f, false, AimBeam.LANDING_SPREAD, eyes[e]);

                    float ratio = OpenSeparation(pieces, lens);
                    allRatios.Add(ratio);

                    if (e == 0)
                    {
                        overviewRatios.Add(ratio);
                        squareToAnother.Add(OpenSeparation(Lay(aim.Path, 0f, false, AimBeam.LANDING_SPREAD, SideOn(aim.Path)), lens));

                        //What the overview's frame shows of it, for the record
                        List<AimBeam.Piece> middle = RayLane(pieces, 0);
                        openingPixels.Add((lens.Project(middle[^1].Tail) - lens.Project(middle[0].Tail)).Length() * pixelsPerUnit);
                        overrunPixels.Add((lens.Project(middle[^1].Head) - lens.Project(middle[^1].Tail)).Length() * pixelsPerUnit);
                        List<AimBeam.Piece> next = RayLane(pieces, 1);
                        gapPixels.Add((lens.Project(next[^1].Tail) - lens.Project(middle[^1].Tail)).Length() * pixelsPerUnit);

                        //And what it costs: a draw a piece
                        int rays = Array.FindAll(pieces, p => p.Kind == AimBeam.PieceKind.Ray).Length;
                        rayDraws.Add(rays);
                        dashDraws.Add(pieces.Length - rays);
                    }

                    //Unbroken: each lane a chain of pieces, head to tail
                    for (int lane = -OUTER; lane <= OUTER; lane++)
                    {
                        List<AimBeam.Piece> ray = RayLane(pieces, lane);
                        Assert.True(ray.Count >= 2, $"lane {lane} is {ray.Count} pieces");
                        for (int j = 1; j < ray.Count; j++)
                            Assert.True(XVector3.Distance(ray[j - 1].Head, ray[j].Tail) < 1e-4f,
                                $"lane {lane} breaks between its pieces {j - 1} and {j}: a gap of {XVector3.Distance(ray[j - 1].Head, ray[j].Tail):0.000}");
                    }
                }
            }

            output.WriteLine($"neighbouring rays apart on screen where the funnel is open, in rays' widths: overview least {Percentile(overviewRatios, 0):0.0}, " +
                $"median {Percentile(overviewRatios, 0.5):0.0}; every lens least {Percentile(allRatios, 0):0.0}; " +
                $"laid square to a lens side on, from the overview: median {Percentile(squareToAnother, 0.5):0.0}, least {Percentile(squareToAnother, 0):0.0}");
            output.WriteLine($"through the overview at a 1080-line frame: the opening {Percentile(openingPixels, 0.5):0} px on screen at the median " +
                $"({Percentile(openingPixels, 0):0} to {Percentile(openingPixels, 1):0}), the run past the touch {Percentile(overrunPixels, 0.5):0} px, " +
                $"neighbouring rays {Percentile(gapPixels, 0.5):0.0} px apart at the touch ({Percentile(gapPixels, 0):0.0} to {Percentile(gapPixels, 1):0.0})");
            output.WriteLine($"draws a beam: {Percentile(dashDraws, 0.5):0} dash pieces at the median ({Percentile(dashDraws, 0):0} to {Percentile(dashDraws, 1):0}), " +
                $"{Percentile(rayDraws, 0.5):0} ray pieces ({Percentile(rayDraws, 0):0} to {Percentile(rayDraws, 1):0})");

            Assert.True(Percentile(allRatios, 0) >= 3f, $"neighbouring rays stand as close as {Percentile(allRatios, 0):0.00} of a ray's width on screen");
            Assert.True(Percentile(squareToAnother, 0.5) < 3f,
                $"a funnel laid square to a lens side on keeps its rays {Percentile(squareToAnother, 0.5):0.00} widths apart from the overview: the measure cannot tell it");
        }

        /// <summary>
        /// The least separation on screen of neighbouring rays, in units of a ray's screen width there, at the stations
        /// where the funnel is fully open (each lane's last two pieces start there: the touch, and where it opened before
        /// it). Measured square to the ray's own screen direction there, since two rays offset only along it lie on top of
        /// each other however far apart the two points are.
        /// </summary>
        private static float OpenSeparation(AimBeam.Piece[] pieces, Lens lens)
        {
            float least = float.MaxValue;
            for (int lane = -OUTER; lane < OUTER; lane++)
            {
                List<AimBeam.Piece> a = RayLane(pieces, lane), b = RayLane(pieces, lane + 1);
                foreach (int back in new[] { 1, 2 })
                {
                    XVector3 p = a[^back].Tail, q = b[^back].Tail;
                    XVector2 run = lens.Project(a[^back].Head) - lens.Project(p);
                    if (run.LengthSquared() < 1e-12f) return 0f;
                    run.Normalize();

                    XVector2 apart = lens.Project(q) - lens.Project(p);
                    float across = MathF.Abs(apart.X * run.Y - apart.Y * run.X);
                    float width = 2f * AimBeam.RAY_WIDTH / lens.Depth((p + q) * 0.5f);
                    least = MathF.Min(least, across / width);
                }
            }

            return least;
        }

        //A straight flight of the length the measured beams run (15 to 28 units, 20 at the median), from a muzzle under the cluster
        private static readonly XVector3 Muzzle = new(0f, -8f, 16f);
        private static readonly XVector3 Touch = new(0f, 4f, 0f);

        private static XVector3 OnLine(float fraction) => XVector3.Lerp(Muzzle, Touch, fraction);

        /// <summary>
        /// <b>The near end is the line it always was</b>, which is what the overview reads the aim off: up to where it
        /// splits, a beam that ends in a funnel lays exactly the dashes, widths and light of a plain one, and past it no
        /// dash at all.
        /// </summary>
        [Fact]
        public void TheNearEndIsTheLineItAlwaysWas()
        {
            XVector3[] path = [Muzzle, Touch];
            float total = XVector3.Distance(Muzzle, Touch);

            foreach (float phase in new[] { 0f, 0.13f, 0.41f, 0.66f })
            {
                AimBeam.Piece[] funnel = Lay(path, phase, false, AimBeam.LANDING_SPREAD);
                AimBeam.Piece[] line = Lay(path, phase, false, 0f);

                AimBeam.Piece[] nearFunnel = Array.FindAll(funnel, p => p.Kind == AimBeam.PieceKind.Dash);
                AimBeam.Piece[] nearLine = Array.FindAll(line, p => XVector3.Distance(Muzzle, p.Head) <= 0.5f * total);

                Assert.NotEmpty(nearLine);
                Assert.Equal(nearLine, Array.FindAll(nearFunnel, p => XVector3.Distance(Muzzle, p.Head) <= 0.5f * total));
                Assert.All(nearFunnel, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH
                    && p.TailAlpha == 1f && p.HeadAlpha == 1f, $"a dash of the funnel's near end is {p}"));

                //A third of the way out, which the overview cannot do without, every dash is there at full light; and none
                //is laid past where the line splits, a little over half way
                Assert.Contains(nearFunnel, p => XVector3.Distance(Muzzle, p.Head) > total / 3f);
                Assert.All(nearFunnel, p => Assert.True(XVector3.Distance(Muzzle, p.Head) <= 0.56f * total,
                    $"a dash of a funnel beam reaches {XVector3.Distance(Muzzle, p.Head) / total:0.00} of the flight"));
            }
        }

        /// <summary>
        /// <b>The far end is crisp lines, not light</b> (the owner on #1144's glow: "it works more like a glow, and it is not
        /// obvious what it is"). A funnel beam is the dashes and <see cref="AimBeam.RAYS"/> rays and nothing else: every
        /// piece is a dash of the line's width or a ray of its own thin one, none wider than the line; each ray leaves the
        /// line at <see cref="AimBeam.RAY_ALPHA"/> and fades along its run to nothing at its far end; and the rays' colour is
        /// under the glare pass's threshold for every tint, so they bloom nowhere. The outer rays end
        /// <see cref="AimBeam.LANDING_SPREAD"/> off the middle one.
        /// </summary>
        [Fact]
        public void TheFarEndIsCrispLinesNotLight()
        {
            XVector3[] path = [Muzzle, Touch];
            AimBeam.Piece[] pieces = Lay(path, 0.3f, false, AimBeam.LANDING_SPREAD);

            Assert.All(pieces, p => Assert.True(p.TailWidth <= AimBeam.WIDTH && p.HeadWidth <= AimBeam.WIDTH, $"a piece wider than the line: {p}"));

            HashSet<int> lanes = new();
            foreach (AimBeam.Piece p in pieces)
            {
                if (p.Kind != AimBeam.PieceKind.Ray) continue;
                lanes.Add(p.Lane);
                Assert.True(p.TailWidth == AimBeam.RAY_WIDTH && p.HeadWidth == AimBeam.RAY_WIDTH, $"a ray of another width: {p}");
            }

            Assert.Equal(AimBeam.RAYS, lanes.Count);

            List<AimBeam.Piece> middle = RayLane(pieces, 0);
            for (int lane = -OUTER; lane <= OUTER; lane++)
            {
                List<AimBeam.Piece> ray = RayLane(pieces, lane);
                Assert.Equal(AimBeam.RAY_ALPHA, ray[0].TailAlpha);
                Assert.Equal(0f, ray[^1].HeadAlpha);
                foreach (AimBeam.Piece p in ray) Assert.True(p.HeadAlpha <= p.TailAlpha, $"lane {lane} brightens along its run: {p}");

                //How far it ends up off the middle one, at the touch
                float off = XVector3.Distance(ray[^1].Tail, middle[^1].Tail);
                Assert.Equal(AimBeam.LANDING_SPREAD * MathF.Abs(lane) / OUTER, off, 3);
            }

            //The colour, over every corner of the colour cube and a sweep between: under the glare threshold (0.55)
            float brightest = 0f;
            for (int r = 0; r <= 4; r++)
                for (int g = 0; g <= 4; g++)
                    for (int b = 0; b <= 4; b++)
                    {
                        XVector3 linear = ColorSpace.SrgbToLinear(new XVector3(r, g, b) / 4f);
                        brightest = MathF.Max(brightest, ColorSpace.Luminance(AimBeam.RayColor(linear)));
                    }

            output.WriteLine($"the rays' colour is at most {brightest:0.000} of luminance over 125 tints");
            Assert.True(brightest <= AimBeam.RAY_LUMINANCE + 1e-5f && AimBeam.RAY_LUMINANCE < 0.55f,
                $"a ray's colour reaches {brightest:0.000} of luminance, at or over the glare threshold");
        }

        /// <summary>
        /// <b>The funnel opens once over the whole flight, not once per leg</b> (a shot bent by a gravity well, #332): laid
        /// along a straight flight cut into legs it is the uncut flight's funnel, piece for piece, and the dashes crawl on
        /// through the knots, lit over the same stretches as on the uncut line. Along a bent flight the middle ray runs on
        /// the flight, cut where it bends, and the outer rays open steadily to the spread whatever the bend.
        /// </summary>
        [Fact]
        public void TheFunnelOpensOverTheWholeFlightNotPerLeg()
        {
            XVector3[] straight = [Muzzle, Touch];
            XVector3[] cut = [Muzzle, OnLine(0.17f), OnLine(0.48f), OnLine(0.62f), OnLine(0.71f), OnLine(0.86f), OnLine(0.93f), Touch];
            XVector3 viewer = SideOn(straight);

            AimBeam.Piece[] straightPieces = Lay(straight, 0.3f, false, AimBeam.LANDING_SPREAD, viewer);
            AimBeam.Piece[] cutPieces = Lay(cut, 0.3f, false, AimBeam.LANDING_SPREAD, viewer);

            //The rays: the same pieces, the knots of a straight flight cutting none of them
            for (int lane = -OUTER; lane <= OUTER; lane++)
            {
                List<AimBeam.Piece> a = RayLane(straightPieces, lane), b = RayLane(cutPieces, lane);
                Assert.Equal(a.Count, b.Count);
                for (int j = 0; j < a.Count; j++)
                {
                    Assert.True(XVector3.Distance(a[j].Tail, b[j].Tail) < 1e-3f && XVector3.Distance(a[j].Head, b[j].Head) < 1e-3f,
                        $"lane {lane}'s piece {j} lies elsewhere on a flight cut into legs: {a[j]} against {b[j]}");
                    Assert.Equal(a[j].TailAlpha, b[j].TailAlpha, 3);
                    Assert.Equal(a[j].HeadAlpha, b[j].HeadAlpha, 3);
                }
            }

            //The dashes: a dash cut at a knot is the same stretch of light in two pieces; within a dash's fade of a knot the
            //two may differ, anywhere else they must agree
            for (float f = 0.02f; f < 0.56f; f += 0.01f)
                if (!NearKnot(f)) Assert.Equal(DashLightAt(straightPieces, OnLine(f)), DashLightAt(cutPieces, OnLine(f)), 3);

            //A flight bent by a well: a quarter circle's worth of turn spread over its far half, in steps of the Game's knots
            List<XVector3> bentList = [Muzzle];
            XVector3 at = Muzzle, heading = XVector3.Normalize(Touch - Muzzle);
            for (int i = 0; i < 14; i++)
            {
                if (i >= 6) heading = XVector3.Normalize(heading + XVector3.UnitX * 0.12f);
                at += heading * (5f / 3f);
                bentList.Add(at);
            }

            XVector3[] bent = bentList.ToArray();
            AimBeam.Piece[] bentPieces = Lay(bent, 0.3f, false, AimBeam.LANDING_SPREAD);

            List<AimBeam.Piece> centre = RayLane(bentPieces, 0);
            Assert.True(centre.Count > RayLane(straightPieces, 0).Count, $"the rays of a bent flight are {centre.Count} pieces, no more than a straight one's");
            //Every piece but the last, which runs on past the touch, ends on the flight; and every knot of the flight past the
            //split lies within a hundredth (RAY_CHORD_TOLERANCE) of the middle ray
            List<XVector3> centreLine = [centre[0].Tail];
            for (int j = 0; j + 1 < centre.Count; j++)
            {
                Assert.True(DistanceToPath(bent, centre[j].Head) < 1e-3f, $"the middle ray's knot {j} lies {DistanceToPath(bent, centre[j].Head):0.000} off a bent flight");
                centreLine.Add(centre[j].Head);
            }

            for (int i = 1; i + 1 < bent.Length; i++)
                if (AlongPath(bent, bent[i]) > 0.56f * AlongPath(bent, bent[^1]))
                    Assert.True(DistanceToPath(centreLine.ToArray(), bent[i]) < 0.0101f,
                        $"knot {i} of a bent flight lies {DistanceToPath(centreLine.ToArray(), bent[i]):0.000} off the middle ray");

            //The outer rays open with the distance along the whole flight, from the split to REGION_BEHIND short of the
            //touch: at every knot they are cut at, as far off the middle one as that distance says, and never narrower
            float flown = AlongPath(bent, bent[^1]), split = AimBeam.SPLIT_FROM * flown;
            float open = MathF.Max(flown - AimBeam.REGION_BEHIND, (split + flown) * 0.5f);

            for (int lane = -OUTER; lane <= OUTER; lane += 2 * OUTER)
            {
                List<AimBeam.Piece> ray = RayLane(bentPieces, lane);
                float last = 0f;
                for (int j = 0; j + 1 < ray.Count; j++)
                {
                    float off = XVector3.Distance(ray[j].Head, centre[j].Head);
                    float expected = AimBeam.LANDING_SPREAD * Math.Clamp((AlongPath(bent, centre[j].Head) - split) / (open - split), 0f, 1f);
                    Assert.True(MathF.Abs(off - expected) < 2e-3f,
                        $"lane {lane} stands {off:0.000} off the middle ray at its knot {j} of a bent flight, where the distance flown says {expected:0.000}");
                    Assert.True(off >= last - 1e-4f, $"lane {lane} narrows from {last:0.000} to {off:0.000} at a knot of a bent flight");
                    last = off;
                }

                Assert.Equal(AimBeam.LANDING_SPREAD, last, 3);
            }

            static bool NearKnot(float f)
            {
                foreach (float knot in new[] { 0.17f, 0.48f })
                    if (MathF.Abs(f - knot) < 0.04f) return true;
                return false;
            }
        }

        //How far a point lies from the nearest point of a flight
        private static float DistanceToPath(XVector3[] path, XVector3 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                XVector3 leg = path[i + 1] - path[i];
                float t = Math.Clamp(XVector3.Dot(point - path[i], leg) / leg.LengthSquared(), 0f, 1f);
                best = MathF.Min(best, XVector3.Distance(point, path[i] + leg * t));
            }

            return best;
        }

        /// <summary>
        /// <b>A beam that reaches nothing is the fading line it was</b>: no rays (there is no region to show) whatever
        /// spread is handed, nothing laid past its end, and its light dying away towards the tip.
        /// <para>
        /// <b>And so is the longest one the Game hands over</b>: a flight bent by a gravity well that reaches nothing is a
        /// knot every step, a hundred units long (<c>ShotPlacement</c>'s flight budget), and it is laid whole, out to its
        /// end, with the fade on its last leg; a cap on the dashes short of that would stop the line at full light in the
        /// air, as if something were there.
        /// </para>
        /// </summary>
        [Fact]
        public void AnOpenBeamIsTheOldFadingLine()
        {
            XVector3 far = Muzzle + XVector3.Normalize(Touch - Muzzle) * 24f;
            Check([Muzzle, far], 24f);

            //Sixty steps of 1.667, the flight turning a little at each knot as a well bends it
            XVector3[] bent = new XVector3[61];
            XVector3 at = Muzzle, heading = XVector3.Normalize(Touch - Muzzle);
            bent[0] = at;
            for (int i = 1; i < bent.Length; i++)
            {
                heading = XVector3.Normalize(heading + XVector3.UnitX * 0.02f);
                at += heading * (100f / 60f);
                bent[i] = at;
            }

            float total = 0f;
            for (int i = 0; i + 1 < bent.Length; i++) total += XVector3.Distance(bent[i], bent[i + 1]);
            Check(bent, total);

            void Check(XVector3[] path, float length)
            {
                AimBeam.Piece[] pieces = Lay(path, 0.2f, true, AimBeam.LANDING_SPREAD);

                Assert.NotEmpty(pieces);
                Assert.All(pieces, p => Assert.True(p.Kind == AimBeam.PieceKind.Dash && p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH));

                //Laid out to the end and not past it: the farthest dash's head within a period of it, along the flight
                float farthest = 0f;
                foreach (AimBeam.Piece p in pieces) farthest = MathF.Max(farthest, AlongPath(path, p.Head));
                Assert.True(farthest >= length - 1.1f - 1e-3f && farthest <= length + 1e-3f,
                    $"an open beam {length:0.0} long is laid out to {farthest:0.0}");

                AimBeam.Piece last = pieces[^1];
                Assert.True(last.TailAlpha < 0.5f, $"the last dash of an open beam {length:0.0} long is at {last.TailAlpha:0.00}, not dying away");
                Assert.True(pieces[0].TailAlpha == 1f);
            }
        }

        //How far along `path` the point nearest `point` lies
        private static float AlongPath(XVector3[] path, XVector3 point)
        {
            float best = float.MaxValue, at = 0f, legStart = 0f;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                XVector3 leg = path[i + 1] - path[i];
                float length = leg.Length();
                float t = length > 0f ? Math.Clamp(XVector3.Dot(point - path[i], leg) / (length * length), 0f, 1f) : 0f;
                float distance = XVector3.Distance(point, path[i] + leg * t);
                if (distance < best) { best = distance; at = legStart + t * length; }
                legStart += length;
            }

            return at;
        }

        /// <summary>
        /// <b>A knot laid within a thousandth of the far end does not take the far end with it</b>: a stepped flight (a
        /// well's, a crate's) can end a hair past its last knot, or on it. The rays still run <c>OVERRUN</c> past the touch
        /// along the leg before, as they do on a flight without that knot, and a beam that reaches nothing still fades out
        /// over that leg rather than over a last leg of nothing (which would leave no dash lit at all).
        /// </summary>
        [Fact]
        public void AKnotAtTheFarEndKeepsTheFarEnd()
        {
            XVector3 direction = XVector3.Normalize(Touch - Muzzle);

            float Farthest(XVector3[] path)
            {
                float most = 0f;
                foreach (AimBeam.Piece p in Lay(path, 0f, false, AimBeam.LANDING_SPREAD, SideOn([Muzzle, Touch])))
                    most = MathF.Max(most, XVector3.Dot(p.Head - Muzzle, direction));
                return most;
            }

            float plain = Farthest([Muzzle, Touch]);
            float hair = Farthest([Muzzle, Touch - direction * 0.0005f, Touch]), on = Farthest([Muzzle, Touch, Touch]);
            output.WriteLine($"the rays' farthest head along the flight: {plain:0.000} without the knot, {hair:0.000} with one " +
                $"a hair short of the touch, {on:0.000} with one on it");
            Assert.True(plain > XVector3.Distance(Muzzle, Touch) + 1f);
            Assert.Equal(plain, hair, 2);
            Assert.Equal(plain, on, 2);

            XVector3 far = Muzzle + direction * 24f;
            foreach (XVector3[] open in new[] { new[] { Muzzle, far - direction * 0.0005f, far }, new[] { Muzzle, far, far } })
            {
                AimBeam.Piece[] pieces = Lay(open, 0.2f, true, 0f);
                Assert.NotEmpty(pieces);
                Assert.True(pieces[0].TailAlpha == 1f && pieces[^1].TailAlpha < 0.5f,
                    $"an open beam with a knot at its end runs from {pieces[0].TailAlpha:0.00} to {pieces[^1].TailAlpha:0.00}, not the fading line");
            }
        }

        /// <summary>
        /// <b>No spread is a plain line ending at the touch</b>, which is what a cutter's beam is (it strikes the ball the
        /// beam ends on): dashes of the line's width at full light all the way, no rays, nothing past the touch.
        /// </summary>
        [Fact]
        public void NoSpreadIsAPlainLineEndingAtTheTouch()
        {
            float total = XVector3.Distance(Muzzle, Touch);
            AimBeam.Piece[] pieces = Lay([Muzzle, Touch], 0.5f, false, 0f);

            Assert.NotEmpty(pieces);
            Assert.All(pieces, p => Assert.True(p.Kind == AimBeam.PieceKind.Dash && p.TailWidth == AimBeam.WIDTH
                && p.HeadWidth == AimBeam.WIDTH && p.TailAlpha == 1f));
            Assert.All(pieces, p => Assert.True(XVector3.Distance(Muzzle, p.Head) <= total + 1e-3f));
            Assert.Contains(pieces, p => XVector3.Distance(Muzzle, p.Head) > total - 1.1f);
        }
    }
}
