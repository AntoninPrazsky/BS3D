using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using MathHelper = Microsoft.Xna.Framework.MathHelper;
using XVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Tests
{
    /// <summary>
    /// #1144: the aim beam's far end is a region the ghost sits in, not a point beside it, and the rest of the beam is
    /// what it was. <see cref="AimBeam.Lay"/> is asked without a graphics device, and the light its pieces put
    /// somewhere is reckoned as <c>ShotTrail.fx</c> draws it (<see cref="Light"/>, and on a lens's screen
    /// <see cref="ScreenLight"/>): the alpha across a piece <c>(1 - |x|/w)²</c>, its quarter-length fades along, and the
    /// additive blend weighing the alpha twice.
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
        /// The light <paramref name="piece"/> puts at <paramref name="at"/>, in units of a dash's core at the muzzle, as
        /// <c>ShotTrail.fx</c> draws it, for a point in the plane the billboard turns into: a point off the axis is reckoned
        /// at its whole distance from it, which is what the beam shows there to a lens square to that offset. The profile
        /// along the axis and straight across it, for the tests of the layout's own shape; what a lens sees at a ghost
        /// that stands off the line in any direction is <see cref="ScreenLight"/>.
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

            float alpha = across * across * SmoothStep(0f, 0.25f, t) * SmoothStep(1f, 0.75f, t) * piece.Alpha;
            return alpha * alpha;
        }

        //HLSL's smoothstep, edges either way round
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static float LightAt(ReadOnlySpan<AimBeam.Piece> pieces, XVector3 at)
        {
            float sum = 0f;
            foreach (AimBeam.Piece piece in pieces) sum += Light(piece, at);
            return sum;
        }

        /// <summary>
        /// The light <paramref name="piece"/> puts on the pixel where a lens at <paramref name="camera"/> sees
        /// <paramref name="at"/>: the ray from the lens through the point is met with the billboard, which turns about its
        /// axis to face the lens (<c>ShotTrail.fx</c>'s side, square to the axis and to the line to the lens; taken here
        /// once at the piece's middle, where the shader takes it at each end), and the light is read where it meets it.
        /// Depth is not asked, which flatters every layout alike: the ball hit hides whatever of the beam lies behind it.
        /// </summary>
        private static float ScreenLight(in AimBeam.Piece piece, XVector3 camera, XVector3 at)
        {
            XVector3 axis = piece.Head - piece.Tail;
            float length = axis.Length();
            if (length < 1e-5f) return 0f;

            XVector3 direction = axis / length;
            XVector3 side = XVector3.Cross(direction, camera - (piece.Tail + piece.Head) * 0.5f);
            if (side.Length() < 1e-5f) return 0f;
            side.Normalize();
            XVector3 normal = XVector3.Cross(direction, side);

            XVector3 ray = at - camera;
            float facing = XVector3.Dot(ray, normal);
            if (MathF.Abs(facing) < 1e-6f) return 0f;

            float reach = XVector3.Dot(piece.Tail - camera, normal) / facing;
            if (reach <= 0f) return 0f;

            XVector3 hit = camera + ray * reach - piece.Tail;
            float t = XVector3.Dot(hit, direction) / length;
            if (t < 0f || t > 1f) return 0f;

            float width = piece.TailWidth + (piece.HeadWidth - piece.TailWidth) * t;
            float across = 1f - MathF.Abs(XVector3.Dot(hit, side)) / width;
            if (across <= 0f) return 0f;

            float alpha = across * across * SmoothStep(0f, 0.25f, t) * SmoothStep(1f, 0.75f, t) * piece.Alpha;
            return alpha * alpha;
        }

        private static float ScreenLightAt(ReadOnlySpan<AimBeam.Piece> pieces, XVector3 camera, XVector3 at)
        {
            float sum = 0f;
            foreach (AimBeam.Piece piece in pieces) sum += ScreenLight(piece, camera, at);
            return sum;
        }

        private static AimBeam.Piece[] Lay(XVector3[] path, float phase, bool openEnded, float spread, XVector3 viewer)
        {
            AimBeam.Piece[] pieces = new AimBeam.Piece[AimBeam.MAX_PIECES];
            int count = AimBeam.Lay(path, phase, openEnded, spread, viewer, pieces);
            return pieces[..count];
        }

        //The tests of the layout's own shape: a lens square to the beam at its far end, so the glow past the touch runs
        //along the flight and the profile reckoned by Light is the one the lens sees
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

        /// <summary>
        /// Whether the ghost of <paramref name="aim"/>, seen from <paramref name="camera"/>, sits inside the far end of
        /// <paramref name="pieces"/>: the pixel the beam points at (the touch's) is lit, at least half the light the glow is
        /// laid with there, and the ghost's centre is inside that light, its pixel getting at least a tenth of the touch's.
        /// </summary>
        private static bool GhostInside(AimBeam.Piece[] pieces, XVector3 camera, Aim aim)
        {
            float touch = ScreenLightAt(pieces, camera, aim.Path[^1]);
            return touch >= 0.5f * AimBeam.GLOW_LIGHT && ScreenLightAt(pieces, camera, aim.Ghost) >= 0.1f * touch;
        }

        /// <summary>
        /// <b>The ghost sits inside the beam's far end, on screen</b>, which is the issue's whole promise. For each measured
        /// aim the beam is laid as the Game lays it (<see cref="AimBeam.LANDING_SPREAD"/>, for the lens it is seen from) and
        /// the ghost's centre is looked at from that lens (<see cref="GhostInside"/>): from the Game's overview, which sees
        /// the beam nearly end on and is the hardest view, and from lenses 10, 20, 45 and 90 degrees off the beam on four
        /// sides of it. A ghost standing off the line towards a lens or away from it shows on screen along the beam, past
        /// the touch, which only a far end laid square to the lens covers. Held for 98 % of the aims from every lens
        /// (measured: 471 of 478 from the overview, at least 474 from each of the others; the ghosts left out stand at the
        /// patch's side edge, 0.57 to 0.67 across the beam on screen).
        /// <para>
        /// <b>And the same question must fail where it should</b> (BestPractices.md §10): the line the cone replaces (no
        /// spread) for nearly every aim from every lens (it holds none), and from the overview a far end laid along the
        /// flight past the touch, as it is for a lens side on, for a third of them (328 of 478 inside).
        /// </para>
        /// </summary>
        [Fact]
        public void TheGhostSitsInsideTheBeamsFarEndOnScreen()
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

            int alongTheFlight = 0;
            Check("the overview", aim => Overview(aim.Path[0]));

            foreach (float degrees in new[] { 10f, 20f, 45f, 90f })
                for (int side = 0; side < 4; side++)
                {
                    int s = side;
                    Check($"{degrees} degrees off the beam, side {side}", aim => OffBeam(aim.Path, degrees, s));
                }

            output.WriteLine($"from the overview, a far end laid along the flight past the touch: {alongTheFlight} of {aims.Count}");
            Assert.True(alongTheFlight <= 0.85 * aims.Count,
                $"{alongTheFlight} of {aims.Count} ghosts sat inside a far end laid along the flight from the overview: " +
                "the question cannot tell the run past the touch square to the lens from one along the flight");

            void Check(string name, Func<Aim, XVector3> lens)
            {
                int inside = 0, insideWithoutSpread = 0;
                bool overview = name == "the overview";

                foreach (Aim aim in aims)
                {
                    XVector3 camera = lens(aim);

                    if (GhostInside(Lay(aim.Path, 0f, false, AimBeam.LANDING_SPREAD, camera), camera, aim)) inside++;
                    if (GhostInside(Lay(aim.Path, 0f, false, 0f, camera), camera, aim)) insideWithoutSpread++;

                    //The far end laid for a lens side on, where the run past the touch is the flight's own direction
                    if (overview && GhostInside(Lay(aim.Path, 0f, false, AimBeam.LANDING_SPREAD, SideOn(aim.Path)), camera, aim))
                        alongTheFlight++;
                }

                output.WriteLine($"{name}: inside the far end {inside} of {aims.Count} ({100f * inside / aims.Count:0.0} %), " +
                    $"inside the plain line {insideWithoutSpread}");

                Assert.True(inside >= 0.98 * aims.Count, $"from {name} only {inside} of {aims.Count} ghosts sit inside the beam's far end");
                Assert.True(insideWithoutSpread <= 0.2 * aims.Count,
                    $"from {name} {insideWithoutSpread} of {aims.Count} ghosts already sat inside the plain line: the question cannot tell the cone from it");
            }
        }

        //A straight flight of the length the measured beams run (15 to 28 units, 20 at the median), from a muzzle under the cluster
        private static readonly XVector3 Muzzle = new(0f, -8f, 16f);
        private static readonly XVector3 Touch = new(0f, 4f, 0f);

        private static XVector3 OnLine(float fraction) => XVector3.Lerp(Muzzle, Touch, fraction);

        /// <summary>
        /// <b>The near end is the line it always was</b>, which is what the overview reads the aim off: up to where the
        /// hand-over begins, a beam that ends in a region lays exactly the dashes, widths and light of a plain one.
        /// </summary>
        [Fact]
        public void TheNearEndIsTheLineItAlwaysWas()
        {
            XVector3[] path = [Muzzle, Touch];
            float total = XVector3.Distance(Muzzle, Touch);

            foreach (float phase in new[] { 0f, 0.13f, 0.41f, 0.66f })
            {
                AimBeam.Piece[] cone = Lay(path, phase, false, AimBeam.LANDING_SPREAD);
                AimBeam.Piece[] line = Lay(path, phase, false, 0f);

                AimBeam.Piece[] nearCone = Array.FindAll(cone, Near);
                AimBeam.Piece[] nearLine = Array.FindAll(line, Near);

                Assert.NotEmpty(nearLine);
                Assert.Equal(nearLine, nearCone);
                Assert.All(nearCone, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH && p.Alpha == 1f));

                //And a third of the way out, which the overview cannot do without, every dash is there at full light
                Assert.Contains(nearCone, p => XVector3.Distance(Muzzle, p.Head) > total / 3f);
            }

            bool Near(AimBeam.Piece p) => XVector3.Distance(Muzzle, p.Head) <= 0.5f * total && p.TailWidth <= AimBeam.WIDTH;
        }

        /// <summary>
        /// <b>The cone opens once over the whole flight, not once per leg</b> (a shot bent by a gravity well, #332): laid
        /// along a flight cut into legs, the widths never fall back at a knot, the far end is as wide as a straight
        /// beam's, and the dashes crawl on through the knots, lit over the same stretches as on the uncut line.
        /// </summary>
        [Fact]
        public void TheConeOpensOverTheWholeFlightNotPerLeg()
        {
            XVector3[] straight = [Muzzle, Touch];
            XVector3[] cut = [Muzzle, OnLine(0.17f), OnLine(0.48f), OnLine(0.71f), OnLine(0.93f), Touch];

            //The widths along the cut flight, in the order the cone runs: never narrower further out
            AimBeam.Piece[] pieces = Lay(cut, 0.3f, false, AimBeam.LANDING_SPREAD);
            List<(float Distance, float Width)> widths = new();
            foreach (AimBeam.Piece p in pieces)
            {
                widths.Add((XVector3.Distance(Muzzle, p.Tail), p.TailWidth));
                widths.Add((XVector3.Distance(Muzzle, p.Head), p.HeadWidth));
            }

            //Dashes and glow are two layers with two widths: each is held to its own
            foreach (bool glow in new[] { false, true })
            {
                List<(float Distance, float Width)> layer = widths.FindAll(w => (w.Width > 2f * AimBeam.WIDTH) == glow);
                layer.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                for (int i = 1; i < layer.Count; i++)
                    if (layer[i].Distance - layer[i - 1].Distance > 1e-3f)
                        Assert.True(layer[i].Width >= layer[i - 1].Width - 1e-4f,
                            $"the {(glow ? "glow" : "dashes")} narrow from {layer[i - 1].Width:0.000} to {layer[i].Width:0.000} at {layer[i].Distance:0.00} along a cut flight");
            }

            float farCut = MaxWidth(pieces), farStraight = MaxWidth(Lay(straight, 0.3f, false, AimBeam.LANDING_SPREAD));
            Assert.Equal(farStraight, farCut, 3);
            Assert.Equal(AimBeam.FarWidth(AimBeam.LANDING_SPREAD), farCut, 3);

            //The light along the axis and a hand's width off it, the cut flight against the uncut: the glow is the same
            //chords either way, and a dash cut at a knot is the same stretch of light in two pieces
            for (float f = 0.02f; f < 1.04f; f += 0.01f)
            {
                XVector3 point = OnLine(f);
                float straightLight = LightAt(Lay(straight, 0.3f, false, AimBeam.LANDING_SPREAD), point);
                float cutLight = LightAt(pieces, point);

                //A dash cut at a knot fades into and out of the cut on each side, so a point within a dash's fade of a knot may
                //differ; anywhere else the two must agree
                if (!NearKnot(f)) Assert.Equal(straightLight, cutLight, 3);
            }

            bool NearKnot(float f)
            {
                foreach (float knot in new[] { 0.17f, 0.48f, 0.71f, 0.93f })
                    if (MathF.Abs(f - knot) < 0.04f) return true;
                return false;
            }

            static float MaxWidth(AimBeam.Piece[] pieces)
            {
                float most = 0f;
                foreach (AimBeam.Piece p in pieces) most = MathF.Max(most, MathF.Max(p.TailWidth, p.HeadWidth));
                return most;
            }
        }

        /// <summary>
        /// <b>A beam that reaches nothing is the fading line it was</b>: no cone (there is no region to show) whatever
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
                Assert.All(pieces, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH));

                //Laid out to the end and not past it: the farthest dash's head within a period of it, along the flight
                float farthest = 0f;
                foreach (AimBeam.Piece p in pieces) farthest = MathF.Max(farthest, AlongPath(path, p.Head));
                Assert.True(farthest >= length - 1.1f - 1e-3f && farthest <= length + 1e-3f,
                    $"an open beam {length:0.0} long is laid out to {farthest:0.0}");

                AimBeam.Piece last = pieces[^1];
                Assert.True(last.Alpha < 0.5f, $"the last dash of an open beam {length:0.0} long is at {last.Alpha:0.00}, not dying away");
                Assert.True(pieces[0].Alpha == 1f);
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
        /// well's, a crate's) can end a hair past its last knot, or on it. The glow still runs <c>OVERRUN</c> past the touch
        /// along the leg before, as it does on a flight without that knot, and a beam that reaches nothing still fades out
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
            output.WriteLine($"the glow's farthest head along the flight: {plain:0.000} without the knot, {hair:0.000} with one " +
                $"a hair short of the touch, {on:0.000} with one on it");
            Assert.True(plain > XVector3.Distance(Muzzle, Touch) + 1f);
            Assert.Equal(plain, hair, 2);
            Assert.Equal(plain, on, 2);

            XVector3 far = Muzzle + direction * 24f;
            foreach (XVector3[] open in new[] { new[] { Muzzle, far - direction * 0.0005f, far }, new[] { Muzzle, far, far } })
            {
                AimBeam.Piece[] pieces = Lay(open, 0.2f, true, 0f);
                Assert.NotEmpty(pieces);
                Assert.True(pieces[0].Alpha == 1f && pieces[^1].Alpha < 0.5f,
                    $"an open beam with a knot at its end runs from {pieces[0].Alpha:0.00} to {pieces[^1].Alpha:0.00}, not the fading line");
            }
        }

        /// <summary>
        /// <b>No spread is a plain line ending at the touch</b>, which is what a cutter's beam is (it strikes the ball the
        /// beam ends on): the line's width and full light everywhere, nothing past the touch.
        /// </summary>
        [Fact]
        public void NoSpreadIsAPlainLineEndingAtTheTouch()
        {
            float total = XVector3.Distance(Muzzle, Touch);
            AimBeam.Piece[] pieces = Lay([Muzzle, Touch], 0.5f, false, 0f);

            Assert.NotEmpty(pieces);
            Assert.All(pieces, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH && p.Alpha == 1f));
            Assert.All(pieces, p => Assert.True(XVector3.Distance(Muzzle, p.Head) <= total + 1e-3f));
        }

        /// <summary>
        /// GamePi's <c>PotatoOutput.fxh</c> <c>ToDisplay</c>, for one channel: exposure, Narkowicz's ACES fit, the sRGB
        /// curve. The Potato <c>ShotTrail.fx</c> adds this of each piece's light, where the desktop curves the sum.
        /// </summary>
        private static float ToDisplay(float radiance)
        {
            float x = radiance * 1.1f;
            float aces = Math.Clamp(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f);
            return aces <= 0.0031308f ? aces * 12.92f : 1.055f * MathF.Pow(aces, 1f / 2.4f) - 0.055f;
        }

        /// <summary>
        /// <b>On the Potato path the glow's body is the desktop's</b>, over a black backdrop: the Potato effect curves each
        /// piece's light by itself and adds the results, so the glow's overlapping chords would come out far brighter
        /// than the desktop's curved sum, and <see cref="AimBeam.POTATO_GLOW_GAIN"/> takes that back. Reckoned in display
        /// values for a channel of one at the beam's radiance boost (3.5), level with the median ghost and a fifth of a
        /// ball off the axis; held within a tenth. The same reckoning at the desktop's own light shows what the gain is for.
        /// </summary>
        [Fact]
        public void OnThePotatoPathTheGlowsBodyIsTheDesktops()
        {
            const float radiance = 3.5f;
            XVector3[] path = [Muzzle, Touch];
            XVector3 side = XVector3.Normalize(XVector3.Cross(Touch - Muzzle, XVector3.UnitX));

            float Desktop(XVector3 at) => ToDisplay(radiance * LightAt(Lay(path, 0f, false, AimBeam.LANDING_SPREAD), at));

            float Potato(XVector3 at, float gain)
            {
                AimBeam.Piece[] pieces = new AimBeam.Piece[AimBeam.MAX_PIECES];
                int count = AimBeam.Lay(path, 0f, false, AimBeam.LANDING_SPREAD, SideOn(path), pieces, gain);

                float sum = 0f;
                for (int i = 0; i < count; i++) sum += ToDisplay(radiance * Light(pieces[i], at));
                return sum;
            }

            //Level with the median ghost, 0.34 short of the touch
            XVector3 Off(float distance) => Touch - XVector3.Normalize(Touch - Muzzle) * 0.34f + side * distance;

            foreach (float off in new[] { 0f, 0.2f, 0.45f, 0.65f })
                output.WriteLine($"{off:0.00} off the axis: desktop {Desktop(Off(off)):0.00}, Potato at the desktop's light " +
                    $"{Potato(Off(off), 1f):0.00}, at POTATO_GLOW_GAIN {Potato(Off(off), AimBeam.POTATO_GLOW_GAIN):0.00} (over 1 clips)");

            float desktop = Desktop(Off(0.2f)), potato = Potato(Off(0.2f), AimBeam.POTATO_GLOW_GAIN), ungained = Potato(Off(0.2f), 1f);
            Assert.True(MathF.Abs(potato - desktop) <= 0.1f * desktop,
                $"the Potato glow's body is {potato:0.000} where the desktop's is {desktop:0.000}");
            Assert.True(ungained > 1.3f * desktop,
                $"at the desktop's light the Potato glow's body is {ungained:0.000} against {desktop:0.000}: the gain has nothing to take back");
        }

        /// <summary>
        /// The glow's chords add up to the light asked for: the overlap the dimming assumes (<see cref="AimBeam.GLOW_OVERLAP"/>)
        /// is the shader's fade summed at that spacing, reckoned here along a long straight run, and it holds within a few
        /// per cent along it, which is what keeps the patch from banding.
        /// </summary>
        [Fact]
        public void TheGlowsChordsAddUpWithoutBanding()
        {
            XVector3[] path = [Muzzle, Touch];
            AimBeam.Piece[] pieces = Lay(path, 0f, false, AimBeam.LANDING_SPREAD);

            //Over the stretch where the hand-over has finished and the dashes are gone, short of where the chords stop
            List<float> light = new();
            for (float f = 0.985f; f <= 1.0f; f += 0.001f) light.Add(LightAt(pieces, OnLine(f)));

            float least = Percentile(light, 0), most = Percentile(light, 1);
            output.WriteLine($"the glow's light on the axis over the last 0.3 before the touch: {least:0.000} to {most:0.000}");
            Assert.True(least >= 0.9f * most, $"the glow bands: {least:0.000} to {most:0.000} along a stretch it should hold");
        }
    }
}
