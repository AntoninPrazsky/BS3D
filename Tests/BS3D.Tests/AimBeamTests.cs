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
    /// #1144 and #1196: the aim beam is one dashed line from the muzzle to the ball it reaches. Where the shot snaps into a
    /// lattice cell its far dashes widen a little along one smooth curve, lengthen with it and fade out, each along itself,
    /// where the lens sees the beam reach the ball it touches; a cutter's line stops there at full strength. Never bolder
    /// than the line and never over that ball. <see cref="AimBeam.Lay"/> is asked without a graphics device. The dashes'
    /// light is reckoned as <c>ShotTrail.fx</c>'s streak draws it: the alpha across a piece <c>(1 - |x|/w)²</c>, lerped along
    /// it from its tail's to its head's (<c>TrailHeadFade</c>), its quarter-length fades along, and the additive blend
    /// weighing the alpha twice; in the world (<see cref="Light"/>) or on a lens's screen (<see cref="ScreenLight"/>).
    /// </summary>
    public class AimBeamTests(ITestOutputHelper output)
    {
        private static readonly string[] RigLevels =
            ["Pennant.json", "One.json", "Colossus.json", "Heart.json", "Rainbow.json", "Zigzag.json"];

        /// <summary>
        /// One aim of the measurement: the beam's flight as the Game draws it, the centre of the ball it touches, and the
        /// ghost's centre with its offsets from the line through the beam's end, along its last leg.
        /// </summary>
        private readonly record struct Aim(XVector3[] Path, XVector3 Hit, XVector3 Ghost, float Lateral, float Along);

        /// <summary>
        /// The aims <c>ShotAgreementTests</c> sweeps, each asked as the Game asks (<c>GameplayScreen.UpdateShotPreview</c>:
        /// the curved sweep with the world's gravity, its path, then the cell and the drift) on a level hung and settled
        /// once, since nothing is fired.
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

                    aims.Add(new Aim(path.ToArray(), hit.BallReference.Pose.Position.ToXna(), ghost, (offset - axis * along).Length(),
                        along));
                }
            }

            return aims;
        }

        /// <summary>
        /// A pinhole lens at <see cref="Eye"/> looking at a point: where a world point lands on its screen, in units of the
        /// image plane one unit in front of it (a frame's pixels are these times <see cref="PixelsPerUnit"/>), and how deep it is.
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

        /// <summary>Pixels a unit of the image plane is at a 1080-line frame through the play lens the capture logs (39.4 degrees).</summary>
        private static readonly float PixelsPerUnit = 540f / MathF.Tan(MathHelper.ToRadians(39.4f) * 0.5f);

        //A piece's alpha a fraction of the way from its tail to its head, as the shader lerps it (TrailHeadFade)
        private static float AlphaAt(in AimBeam.Piece piece, float t) => piece.TailAlpha + (piece.HeadAlpha - piece.TailAlpha) * t;

        //A piece's half-width a fraction of the way from its tail to its head, as the shader lerps it
        private static float WidthAt(in AimBeam.Piece piece, float t) => piece.TailWidth + (piece.HeadWidth - piece.TailWidth) * t;

        //The light at a piece's core a fraction of the way along it, in units of a dash's core: its alpha there through its
        //quarter fades, squared
        private static float CoreLight(in AimBeam.Piece piece, float t)
        {
            float alpha = AlphaAt(piece, t) * SmoothStep(0f, 0.25f, t) * SmoothStep(1f, 0.75f, t);
            return alpha * alpha;
        }

        //The light a dash ends on: at three quarters of its length, where its own fade-out begins, which is the most it puts
        //anywhere along its last quarter
        private static float EndLight(in AimBeam.Piece piece)
        {
            float alpha = AlphaAt(piece, 0.75f);
            return alpha * alpha;
        }

        /// <summary>
        /// The last point of the beam whose core still carries <paramref name="threshold"/> of a dash's light, and the piece
        /// it lies on: the head of what reads as the line.
        /// </summary>
        private static (XVector3 Point, AimBeam.Piece Piece) LastLit(AimBeam.Piece[] pieces, float threshold)
        {
            for (int i = pieces.Length - 1; i >= 0; i--)
                for (int k = 100; k >= 0; k--)
                    if (CoreLight(pieces[i], k / 100f) >= threshold)
                        return (XVector3.Lerp(pieces[i].Tail, pieces[i].Head, k / 100f), pieces[i]);

            return (pieces[0].Tail, pieces[0]);
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
            float across = 1f - distance / WidthAt(piece, t);
            if (across <= 0f) return 0f;

            float alpha = across * across * SmoothStep(0f, 0.25f, t) * SmoothStep(1f, 0.75f, t) * AlphaAt(piece, t);
            return alpha * alpha;
        }

        /// <summary>
        /// The light a dash <paramref name="piece"/> puts at a point <paramref name="at"/> of <paramref name="lens"/>'s screen,
        /// in units of a dash's core: the billboard seen through the lens, its half-width at each point that point's world
        /// half-width over its depth (the billboard turns square to the view ray), its along coordinate interpolated as the
        /// rasterizer does, perspective-correct.
        /// </summary>
        private static float ScreenLight(in AimBeam.Piece piece, Lens lens, XVector2 at)
        {
            float zTail = lens.Depth(piece.Tail), zHead = lens.Depth(piece.Head);
            if (zTail <= 0.05f || zHead <= 0.05f) return 0f;

            XVector2 a = lens.Project(piece.Tail), b = lens.Project(piece.Head), ab = b - a;
            float lengthSquared = ab.LengthSquared();
            float s = lengthSquared > 1e-14f ? XVector2.Dot(at - a, ab) / lengthSquared : 0f;
            if (s < 0f || s > 1f) return 0f;

            float u = s / zHead / ((1f - s) / zTail + s / zHead);
            float halfWidth = WidthAt(piece, u) / (zTail + (zHead - zTail) * u);
            float across = 1f - (at - (a + ab * s)).Length() / halfWidth;
            if (across <= 0f) return 0f;

            float alpha = across * across * SmoothStep(0f, 0.25f, u) * SmoothStep(1f, 0.75f, u) * AlphaAt(piece, u);
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
        /// The most light the beam puts anywhere over the disc a ball of the cluster shows on <paramref name="lens"/>'s
        /// screen, in units of a dash's core: sampled over rings out to nine-tenths of its radius.
        /// </summary>
        private static float LightOverBall(AimBeam.Piece[] pieces, Lens lens, XVector3 centre)
        {
            XVector2 c = lens.Project(centre);
            float radius = BallsConstraintsBuilder.BALL_RADIUS / lens.Depth(centre);
            float most = 0f;

            for (int ring = 0; ring <= 9; ring++)
                for (int k = 0; k < (ring == 0 ? 1 : 36); k++)
                {
                    float angle = MathF.Tau * k / 36f;
                    XVector2 at = c + new XVector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * ring / 10f);

                    float sum = 0f;
                    foreach (AimBeam.Piece piece in pieces) sum += ScreenLight(piece, lens, at);
                    most = MathF.Max(most, sum);
                }

            return most;
        }

        private static AimBeam.Piece[] Lay(XVector3[] path, float phase, AimBeam.FarEnd farEnd, XVector3 ballTouched, XVector3 viewer)
        {
            AimBeam.Piece[] pieces = new AimBeam.Piece[AimBeam.MAX_PIECES];
            int count = AimBeam.Lay(path, phase, farEnd, ballTouched, viewer, pieces);
            return pieces[..count];
        }

        //The tests of the layout's own shape: the ball touched straight ahead of the touch, seen by a lens square to the
        //beam there, from which the beam reaches the ball at the touch, so a strike is the plain line run to the touch
        private static AimBeam.Piece[] Lay(XVector3[] path, float phase, AimBeam.FarEnd farEnd) =>
            Lay(path, phase, farEnd, BallAhead(path), SideOn(path));

        //The centre of a ball the flight touches head on at its end
        private static XVector3 BallAhead(XVector3[] path) =>
            path[^1] + XVector3.Normalize(path[^1] - path[^2]) * BallsConstraintsBuilder.BALL_RADIUS;

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
        /// The lens leaning in half way (precise aim's blend 0.57, as #1144's half-lean captures stood): from the overview
        /// towards precise aim's own, <see cref="PreciseAim.BACK"/> behind the muzzle along the aim and
        /// <see cref="PreciseAim.RISE"/> over the bore. The beam is drawn at <c>1 - blend</c> there.
        /// </summary>
        private static XVector3 LeaningIn(XVector3[] path, float blend)
        {
            XVector3 aim = XVector3.Normalize(path[1] - path[0]);
            XVector3 up = XVector3.Normalize(XVector3.UnitY - aim * XVector3.Dot(XVector3.UnitY, aim));
            XVector3 ads = path[0] - aim * PreciseAim.BACK + up * PreciseAim.RISE;
            return XVector3.Lerp(Overview(path[0]), ads, blend);
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

        private static float Total(XVector3[] path)
        {
            float total = 0f;
            for (int i = 0; i + 1 < path.Length; i++) total += XVector3.Distance(path[i], path[i + 1]);
            return total;
        }

        //A straight flight of the length the measured beams run (15 to 28 units, 20 at the median), from a muzzle under the cluster
        private static readonly XVector3 Muzzle = new(0f, -8f, 16f);
        private static readonly XVector3 Touch = new(0f, 4f, 0f);

        private static XVector3 OnLine(float fraction) => XVector3.Lerp(Muzzle, Touch, fraction);

        /// <summary>
        /// <b>The near end is the plain line</b>, which is what the overview reads the aim off: up to where the far part
        /// begins, a beam that lands in a cell lays exactly the dashes, widths and light of a plain one.
        /// </summary>
        [Fact]
        public void TheNearEndIsThePlainLine()
        {
            XVector3[] path = [Muzzle, Touch];
            float total = Total(path);

            foreach (float phase in new[] { 0f, 0.13f, 0.41f, 0.66f })
            {
                AimBeam.Piece[] landing = Lay(path, phase, AimBeam.FarEnd.Landing);
                AimBeam.Piece[] plain = Lay(path, phase, AimBeam.FarEnd.Strike);

                bool Near(AimBeam.Piece p) => XVector3.Distance(Muzzle, p.Head) <= AimBeam.FAR_FROM * total;

                AimBeam.Piece[] nearLanding = Array.FindAll(landing, Near);
                Assert.NotEmpty(nearLanding);
                Assert.Equal(Array.FindAll(plain, Near), nearLanding);
                Assert.All(nearLanding, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH
                    && p.TailAlpha == 1f && p.HeadAlpha == 1f, $"a dash of the near end is {p}"));

                //A third of the way out, which the overview cannot do without, every dash is there at full light
                Assert.Contains(nearLanding, p => XVector3.Distance(Muzzle, p.Head) > total / 3f);
            }
        }

        /// <summary>
        /// <b>One dashed line from the muzzle to the touch, and nothing else</b> (the owner on the fan of lines: "a thick
        /// rake: it covers the ball, goes behind it"). Every piece lies on the flight, end to end in order from the muzzle,
        /// with a gap after each dash; none runs past the touch; and the last dash ends within a period of it, so the line
        /// reaches the ball rather than stopping short. Over flights straight and bent, at several phases of the crawl.
        /// </summary>
        [Fact]
        public void OneDashedLineFromTheMuzzleToTheTouch()
        {
            foreach (XVector3[] path in new[] { new[] { Muzzle, Touch }, Bent() })
            {
                float total = Total(path);

                foreach (float phase in new[] { 0f, 0.21f, 0.5f, 0.77f })
                {
                    AimBeam.Piece[] pieces = Lay(path, phase, AimBeam.FarEnd.Landing);
                    Assert.NotEmpty(pieces);

                    float last = 0f, gaps = 0f;
                    foreach (AimBeam.Piece p in pieces)
                    {
                        Assert.True(DistanceToPath(path, p.Tail) < 1e-3f && DistanceToPath(path, p.Head) < 1e-3f,
                            $"a piece off the flight: {p}");

                        float tail = AlongPath(path, p.Tail), head = AlongPath(path, p.Head);
                        Assert.True(tail >= last - 1e-3f && head > tail, $"a piece out of order or backwards at {tail:0.00} after {last:0.00}");
                        if (tail > last + 1e-3f) gaps++;
                        last = head;
                    }

                    Assert.True(last <= total + 1e-3f, $"the beam runs to {last:0.000}, past the touch at {total:0.000}");
                    Assert.True(last >= total - AimBeam.SPACING * AimBeam.FAR_STRETCH, $"the beam stops at {last:0.00} of {total:0.00}");
                    Assert.True(gaps >= pieces.Length / 2, $"{gaps} gaps between {pieces.Length} pieces: a rod, not dashes");
                }
            }
        }

        //A flight bent by a well: a quarter circle's worth of turn spread over its far half, in steps of the Game's knots
        private static XVector3[] Bent()
        {
            List<XVector3> bent = [Muzzle];
            XVector3 at = Muzzle, heading = XVector3.Normalize(Touch - Muzzle);
            for (int i = 0; i < 14; i++)
            {
                if (i >= 6) heading = XVector3.Normalize(heading + XVector3.UnitX * 0.12f);
                at += heading * (5f / 3f);
                bent.Add(at);
            }

            return bent.ToArray();
        }

        /// <summary>
        /// <b>The far dashes widen along one smooth curve: no corner, no step, nothing that reads as prongs.</b> The curve
        /// (<see cref="AimBeam.Ease"/> over <see cref="AimBeam.FarAlong"/>) is flat where it leaves the plain line and where
        /// it reaches the touch, and in between its slope changes gradually: sampled finely, the width never narrows, never
        /// jumps, and its slope never turns by more than a smooth curve's. The pieces carry it: each one's two widths are
        /// the curve's at its two ends, so a dash is a slim trapezoid and the dashes widen one after another. And it stays
        /// narrow: at the touch <see cref="AimBeam.FAR_WIDTH"/>, under the measured landing region's reach.
        /// <para>
        /// <b>Seen to fail</b> with the smoothstep replaced by a straight ramp: the slope turns at once where the far part
        /// begins and where it ends.
        /// </para>
        /// </summary>
        [Fact]
        public void TheFarDashesWidenAlongOneSmoothCurve()
        {
            const float farFrom = 10f, total = 20f;
            const int samples = 2000;
            float step = (total + 2f - (farFrom - 2f)) / samples;

            float previousWidth = AimBeam.FarWidth(AimBeam.FarAlong(farFrom - 2f, farFrom, total));
            float previousSlope = 0f, mostTurn = 0f, mostJump = 0f;

            for (int i = 1; i <= samples; i++)
            {
                float width = AimBeam.FarWidth(AimBeam.FarAlong(farFrom - 2f + i * step, farFrom, total));
                float slope = (width - previousWidth) / step;

                Assert.True(width >= previousWidth - 1e-6f, $"the width narrows at {farFrom - 2f + i * step:0.00}");
                mostJump = MathF.Max(mostJump, width - previousWidth);
                mostTurn = MathF.Max(mostTurn, MathF.Abs(slope - previousSlope));

                previousWidth = width;
                previousSlope = slope;
            }

            //A smoothstep's slope over the far part, at most 1.5 * (FAR_WIDTH - WIDTH) / (total - farFrom) units a unit, turns
            //by 6 * (FAR_WIDTH - WIDTH) / (total - farFrom)² a unit at the most: a step's worth of that, with room for rounding.
            //A ramp would turn by its whole slope in one step at each end
            float smoothTurn = 6f * (AimBeam.FAR_WIDTH - AimBeam.WIDTH) / ((total - farFrom) * (total - farFrom)) * step;
            output.WriteLine($"the width's slope turns by at most {mostTurn:0.00000} a step of {step:0.000} (a smoothstep's bound {smoothTurn:0.00000}); " +
                $"its biggest step {mostJump:0.00000}");
            Assert.True(mostTurn <= 1.05f * smoothTurn + 1e-5f, $"the width's slope turns by {mostTurn:0.00000} in one step: a corner");

            Assert.Equal(AimBeam.WIDTH, AimBeam.FarWidth(AimBeam.FarAlong(farFrom, farFrom, total)));
            Assert.Equal(AimBeam.FAR_WIDTH, AimBeam.FarWidth(AimBeam.FarAlong(total, farFrom, total)));
            Assert.True(AimBeam.FAR_WIDTH < AimBeam.LANDING_SPREAD, "the far end as wide as the landing region");

            //The pieces carry the curve, dash after dash
            XVector3[] path = [Muzzle, Touch];
            float flown = Total(path);
            float last = 0f;
            foreach (AimBeam.Piece p in Lay(path, 0.3f, AimBeam.FarEnd.Landing))
            {
                float tail = XVector3.Distance(Muzzle, p.Tail), head = XVector3.Distance(Muzzle, p.Head);
                Assert.Equal(AimBeam.FarWidth(AimBeam.FarAlong(tail, AimBeam.FAR_FROM * flown, flown)), p.TailWidth, 5);
                Assert.Equal(AimBeam.FarWidth(AimBeam.FarAlong(head, AimBeam.FAR_FROM * flown, flown)), p.HeadWidth, 5);
                Assert.True(p.TailWidth >= last - 1e-6f && p.HeadWidth >= p.TailWidth, $"a dash narrower than the one before it: {p}");
                last = p.HeadWidth;
            }

            Assert.True(last > AimBeam.WIDTH, "no dash widens");
        }

        /// <summary>
        /// <b>The far dashes fade out by the ball, each along itself, and the far end never grows bolder than the line</b>
        /// (the owner: "turn transparent, and they stop at the ball"; "it must not disturb: thin, not as conspicuous and
        /// bold"). Over every phase of the crawl's whole cycle (<see cref="AimBeam.SPACING"/> over <see cref="AimBeam.CRAWL"/>)
        /// on a side-on flight, and at eight phases of it over the measured aims through the Game's overview: the alpha
        /// never rises from the muzzle out, piece after piece and along each; the light a dash adds across its whole width
        /// (its alpha squared times its width) and at its brightest point (its alpha squared) is never over the line's
        /// anywhere along it; and the last dash drawn ends on next to nothing, whichever dash the crawl has just carried
        /// into the ball. The curve itself falls smoothly to nothing at the ball and the light a unit of the beam carries
        /// never rises over the line's.
        /// <para>
        /// <b>Seen to fail</b> with the fade left out (the far dashes at full alpha): their light grows with their width.
        /// And with what was reviewed (one alpha a dash, its middle's, the fade <c>1 - along³</c> and a sliver measured
        /// against the far period): the last dash drawn ended on 0.36 of the line's light 2.4 units short of the ball side on,
        /// and through the overview on 0.21 at the median, 0.39 at the 95th percentile. Each of those alone fails too: one
        /// alpha a dash (0.14), the old fade lerped along (0.23), a sliver against the period (0.15) or against the dash's
        /// own length whatever its light (0.12).
        /// </para>
        /// </summary>
        [Fact]
        public void TheFarDashesFadeOutByTheBallAndNeverGrowBolder()
        {
            //A tenth of the line's light at the most, where the last dash's own fade-out begins
            const float EndBound = 0.1f;

            XVector3[] path = [Muzzle, Touch];
            float total = Total(path), cycle = AimBeam.SPACING / AimBeam.CRAWL;

            //Every beam laid, held to never bolder once the last dashes are asked about
            List<AimBeam.Piece[]> laid = new();

            float worst = 0f, worstPhase = 0f, worstShort = 0f, mostShort = 0f;
            int phases = 0;
            for (float phase = 0f; phase < cycle; phase += 0.001f, phases++)
            {
                AimBeam.Piece[] pieces = Lay(path, phase, AimBeam.FarEnd.Landing);
                laid.Add(pieces);

                AimBeam.Piece final = pieces[^1];
                float light = EndLight(final), shortOfTouch = total - XVector3.Distance(Muzzle, final.Head);
                mostShort = MathF.Max(mostShort, shortOfTouch);
                if (light > worst) (worst, worstPhase, worstShort) = (light, phase, shortOfTouch);
            }

            output.WriteLine($"side on, over {phases} phases of the crawl's {cycle:0.0000} s cycle: the last dash drawn ends on at most " +
                $"{worst:0.000} of the line's light (phase {worstPhase:0.000}, its head {worstShort:0.00} short of the touch); " +
                $"it ends at most {mostShort:0.00} short");
            //Over the measured aims through the Game's overview, at eight phases across the cycle
            List<float> ends = new();
            foreach (Aim aim in Measure(40))
            {
                XVector3 eye = Overview(aim.Path[0]);
                for (int k = 0; k < 8; k++)
                {
                    AimBeam.Piece[] pieces = Lay(aim.Path, k * cycle / 8f, AimBeam.FarEnd.Landing, aim.Hit, eye);
                    laid.Add(pieces);
                    ends.Add(EndLight(pieces[^1]));
                }
            }

            output.WriteLine($"through the overview over {ends.Count} aims and phases: the last dash drawn ends on {Percentile(ends, 0.5):0.000} " +
                $"of the line's light at the median, {Percentile(ends, 0.95):0.000} at the 95th percentile, {Percentile(ends, 1):0.000} at the most");
            Assert.True(worst <= EndBound, $"at phase {worstPhase:0.000} the last dash ends on {worst:0.000} of the line's light, {worstShort:0.00} short of the ball");
            Assert.True(Percentile(ends, 1) <= EndBound, $"through the overview the last dash drawn ends on up to {Percentile(ends, 1):0.000} of the line's light");

            foreach (AimBeam.Piece[] pieces in laid) NeverBolder(pieces);

            //The curve itself, sampled: the light at a dash's core smoothly to nothing at the ball, never back up, and flat
            //where it leaves the plain line
            float previous = 1f;
            for (int i = 0; i <= 100; i++)
            {
                float alpha = AimBeam.FarAlpha(i / 100f, AimBeam.FarWidth(i / 100f));
                float light = alpha * alpha;
                Assert.True(light <= previous + 1e-6f && previous - light < 0.05f, $"the fade jumps from {previous:0.000} to {light:0.000} at {i}%");
                previous = light;
            }

            Assert.Equal(1f, AimBeam.FarAlpha(0f, AimBeam.WIDTH));
            Assert.Equal(0f, AimBeam.FarAlpha(1f, AimBeam.FAR_WIDTH));

            //The light a unit of the beam carries: a dash's across-light, the same share of every period lit
            for (float along = 0f; along <= 1f; along += 0.05f)
            {
                float alpha = AimBeam.FarAlpha(along, AimBeam.FarWidth(along));
                float perUnit = alpha * alpha * AimBeam.FarWidth(along);
                Assert.True(perUnit <= AimBeam.WIDTH + 1e-5f, $"at {along:0.00} of the far part a unit of the beam carries {perUnit:0.0000}");
            }

            //Piece after piece from the muzzle out, and along each as the shader lerps it
            static void NeverBolder(AimBeam.Piece[] pieces)
            {
                float lastAlpha = float.MaxValue;
                foreach (AimBeam.Piece p in pieces)
                {
                    Assert.True(p.TailAlpha <= lastAlpha + 1e-6f && p.HeadAlpha <= p.TailAlpha + 1e-6f,
                        $"a dash brighter than the beam before it: {p}");
                    lastAlpha = p.HeadAlpha;

                    for (int k = 0; k <= 10; k++)
                    {
                        float alpha = AlphaAt(p, k / 10f);
                        Assert.True(alpha * alpha * WidthAt(p, k / 10f) <= AimBeam.WIDTH * 1.0001f && alpha * alpha <= 1.0001f,
                            $"a dash bolder than the line {k}0 % of the way along it: {p}");
                    }
                }
            }
        }

        /// <summary>
        /// <b>The beam stops at the ball it hits: nothing of it lies over that ball or past it, in the world or on screen</b>
        /// (the owner on the fan: "it covers the ball, goes behind it"). For every measured aim, laid for the Game's overview
        /// and for the lens leaning in half way, both as a landing's beam and as a cutter's strike: no piece reaches past the
        /// touch along the flight or comes inside the ball; the beam ends where the lens sees it reach the ball's disc
        /// (<see cref="AimBeam.VisibleEnd"/>: the sight line there grazes the ball), short of the touch from the overview, and
        /// no dash is laid past it; and the light the beam puts anywhere over that disc, at the beam's opacity for the lens
        /// (<c>1 - blend</c> leaning in), is under a fiftieth of a dash's core for 95 % of the aims: a landing's faded out
        /// there, and a strike's too, at full strength up to the disc's edge.
        /// <para>
        /// <b>And the same question must fail where it should</b> (BestPractices.md §10): the plain line run to the touch
        /// (a cutter's beam before #1196, and the whole beam before #1144) lies over the ball it hits from the overview for
        /// most aims, and so does the far part run to the touch rather than to the disc.
        /// </para>
        /// </summary>
        [Fact]
        public void TheBeamStopsAtTheBallItHits()
        {
            List<Aim> aims = Measure(40);
            Assert.True(aims.Count >= 200, $"only {aims.Count} of 240 aims had a ghost");

            const float LeanBlend = 0.57f, Visible = 0.02f;
            List<float> overview = new(), leaning = new(), strikeOverview = new(), strikeLeaning = new();
            List<float> plainOverview = new(), toTheTouch = new(), shortOfTouch = new(), gapPx = new();

            foreach (Aim aim in aims)
            {
                float total = Total(aim.Path);

                foreach ((XVector3 eye, float opacity, bool fromOverview) in new[]
                {
                    (Overview(aim.Path[0]), 1f, true),
                    (LeaningIn(aim.Path, LeanBlend), 1f - LeanBlend, false)
                })
                {
                    Lens lens = Lens.LookingAt(eye, aim.Path[^1]);
                    float end = AimBeam.VisibleEnd(aim.Path, total, aim.Hit, eye);
                    Assert.True(end <= total, $"the beam reaches the ball at {end:0.000}, past the touch at {total:0.000}");

                    AimBeam.Piece[] pieces = Lay(aim.Path, 0.37f, AimBeam.FarEnd.Landing, aim.Hit, eye);
                    AimBeam.Piece[] strike = Lay(aim.Path, 0.37f, AimBeam.FarEnd.Strike, aim.Hit, eye);

                    //Laid no further than where the lens sees the beam reach the ball, which is never past the touch
                    foreach (AimBeam.Piece[] laid in new[] { pieces, strike })
                        foreach (AimBeam.Piece p in laid)
                        {
                            float reach = AlongPath(aim.Path, p.Head);
                            Assert.True(reach <= end + 1e-3f, $"a dash reaches {reach - end:0.000} past where the beam meets the ball: {p}");
                            Assert.True(XVector3.Distance(p.Head, aim.Hit) >= BallsConstraintsBuilder.BALL_RADIUS - 1e-3f,
                                $"a dash inside the ball it hits: {p}");
                        }

                    //A strike's is the plain line all the way
                    Assert.All(strike, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH
                        && p.TailAlpha == 1f && p.HeadAlpha == 1f, $"a strike's dash is {p}"));

                    //Where the beam ends, the sight line grazes the ball, unless that is the touch itself
                    if (end < total)
                    {
                        XVector3 at = PointOnPath(aim.Path, end), sight = XVector3.Normalize(at - eye);
                        XVector3 toBall = aim.Hit - eye;
                        float miss = (toBall - sight * XVector3.Dot(toBall, sight)).Length();
                        Assert.True(MathF.Abs(miss - BallsConstraintsBuilder.BALL_RADIUS) < 0.01f,
                            $"the beam ends where its sight line passes {miss:0.000} from the ball's centre");
                    }

                    //Its light goes as the square of the opacity the Game draws it at
                    (fromOverview ? overview : leaning).Add(LightOverBall(pieces, lens, aim.Hit) * opacity * opacity);
                    (fromOverview ? strikeOverview : strikeLeaning).Add(LightOverBall(strike, lens, aim.Hit) * opacity * opacity);

                    if (fromOverview)
                    {
                        shortOfTouch.Add(total - end);

                        //The plain line and the far part run to the touch, as a lens that cannot see the ball's disc lays them
                        plainOverview.Add(LightOverBall(Lay(aim.Path, 0.37f, AimBeam.FarEnd.Strike, aim.Hit, SideOn(aim.Path)), lens, aim.Hit));
                        toTheTouch.Add(LightOverBall(Lay(aim.Path, 0.37f, AimBeam.FarEnd.Landing, aim.Hit, SideOn(aim.Path)), lens, aim.Hit));

                        //How far on screen the line, where its core still carries a tenth of the light, stops short of the ball's disc
                        XVector3 lastLit = LastLit(pieces, 0.1f).Point;
                        gapPx.Add(((lens.Project(lastLit) - lens.Project(aim.Hit)).Length()
                            - BallsConstraintsBuilder.BALL_RADIUS / lens.Depth(aim.Hit)) * PixelsPerUnit);
                    }
                }
            }

            output.WriteLine($"from the overview the beam reaches the ball's disc {Percentile(shortOfTouch, 0.5):0.00} short of the touch at the median " +
                $"({Percentile(shortOfTouch, 0):0.00} to {Percentile(shortOfTouch, 1):0.00}); where its core still carries a tenth of the light " +
                $"it stops {Percentile(gapPx, 0.5):0} px short of the disc at a 1080-line frame (95th {Percentile(gapPx, 0.95):0})");
            output.WriteLine($"the most light over the ball hit, in units of a dash's core: a landing from the overview median {Percentile(overview, 0.5):0.000}, " +
                $"95th {Percentile(overview, 0.95):0.000}, most {Percentile(overview, 1):0.000}; leaning in half way median {Percentile(leaning, 0.5):0.000}, " +
                $"95th {Percentile(leaning, 0.95):0.000}; a strike from the overview median {Percentile(strikeOverview, 0.5):0.000}, " +
                $"95th {Percentile(strikeOverview, 0.95):0.000}, most {Percentile(strikeOverview, 1):0.000}; leaning in 95th {Percentile(strikeLeaning, 0.95):0.000}; " +
                $"the plain line run to the touch median {Percentile(plainOverview, 0.5):0.000}; " +
                $"the far part run to the touch median {Percentile(toTheTouch, 0.5):0.000}, 95th {Percentile(toTheTouch, 0.95):0.000}");

            Assert.True(Percentile(overview, 0.95) <= Visible, $"from the overview the beam lays {Percentile(overview, 0.95):0.000} of its light over the ball it hits at the 95th percentile");
            Assert.True(Percentile(leaning, 0.95) <= Visible, $"leaning in the beam lays {Percentile(leaning, 0.95):0.000} of its light over the ball it hits at the 95th percentile");
            Assert.True(Percentile(strikeOverview, 0.95) <= Visible && Percentile(strikeLeaning, 0.95) <= Visible,
                $"a strike lays {Percentile(strikeOverview, 0.95):0.000} (overview) and {Percentile(strikeLeaning, 0.95):0.000} (leaning in) of its light over the ball it hits at the 95th percentile");
            Assert.True(Percentile(plainOverview, 0.5) > Visible,
                $"the plain line run to the touch lays only {Percentile(plainOverview, 0.5):0.000} of its light over the ball it hits: the question cannot tell");
            Assert.True(Percentile(toTheTouch, 0.95) > Visible,
                $"the far part run to the touch lays only {Percentile(toTheTouch, 0.95):0.000} over the ball at the 95th percentile: the question cannot tell");
        }

        //The point at a distance along a flight
        private static XVector3 PointOnPath(XVector3[] path, float distance)
        {
            for (int i = 0; i + 1 < path.Length; i++)
            {
                float length = XVector3.Distance(path[i], path[i + 1]);
                if (distance <= length) return XVector3.Lerp(path[i], path[i + 1], length > 0f ? distance / length : 0f);
                distance -= length;
            }

            return path[^1];
        }

        /// <summary>
        /// <b>The lens fades the far dashes and does not move them</b>: where a landing's dashes lie and how wide they are is
        /// the flight's, so a lens that sees the beam reach the ball elsewhere (from the side, from the overview, leaning in)
        /// lays every dash both of them lay in the same place at the same widths, and only their light differs. So the far
        /// dashes hold still while the camera leans in or is shaken by a shot.
        /// <para>
        /// <b>Seen to fail</b> with the far part laid from where the lens sees the beam reach the ball instead of from the
        /// touch: the far dashes slide with the lens.
        /// </para>
        /// </summary>
        [Fact]
        public void TheLensFadesTheFarDashesAndDoesNotMoveThem()
        {
            foreach (XVector3[] path in new[] { new[] { Muzzle, Touch }, Bent() })
            {
                float total = Total(path);
                XVector3 ball = BallAhead(path);
                XVector3[] eyes = [SideOn(path), Overview(path[0]), LeaningIn(path, 0.57f)];

                float[] ends = Array.ConvertAll(eyes, eye => AimBeam.VisibleEnd(path, total, ball, eye));
                output.WriteLine($"the lenses see the beam reach the ball at {string.Join(", ", Array.ConvertAll(ends, e => e.ToString("0.00")))} of {total:0.00}");

                //On the straight flight the overview and the lens leaning in see it well short of the touch (the bent one
                //turns away from them, and they see it reach the ball nearly at the touch)
                if (path.Length == 2)
                    Assert.True(ends[0] - MathF.Max(ends[1], ends[2]) > 1f, "the lenses see the beam reach the ball at the same point: the question cannot tell");

                foreach (float phase in new[] { 0f, 0.3f, 0.6f })
                {
                    AimBeam.Piece[] reference = Lay(path, phase, AimBeam.FarEnd.Landing, ball, eyes[0]);
                    int compared = 0;

                    for (int e = 1; e < eyes.Length; e++)
                    {
                        AimBeam.Piece[] pieces = Lay(path, phase, AimBeam.FarEnd.Landing, ball, eyes[e]);

                        //Every piece clear of the shorter beam's end, which clips the last one there
                        for (int i = 0; i < pieces.Length && AlongPath(path, pieces[i].Head) < ends[e] - 1e-3f; i++)
                        {
                            Assert.True(XVector3.Distance(reference[i].Tail, pieces[i].Tail) < 1e-4f
                                && XVector3.Distance(reference[i].Head, pieces[i].Head) < 1e-4f,
                                $"lens {e} lays piece {i} elsewhere: {pieces[i]} against {reference[i]}");
                            Assert.Equal(reference[i].TailWidth, pieces[i].TailWidth, 5);
                            Assert.Equal(reference[i].HeadWidth, pieces[i].HeadWidth, 5);
                            if (AlongPath(path, pieces[i].Tail) > AimBeam.FAR_FROM * total) compared++;
                        }
                    }

                    Assert.True(compared > 0, "no far dash compared");
                }
            }
        }

        /// <summary>
        /// <b>The far dashes stay longer than wide on screen, so they never read as the rungs of a ladder</b> (#1144's widened
        /// dashes did, from the overview, which sees the beam six to twenty degrees off end on). Through the overview, each
        /// far dash still carrying a tenth of the line's light shows a half-light length along the beam (two thirds of its
        /// length, past its quarter fades) over its half-light width across it (a third of its width). At its squattest such
        /// dash a beam is longer than wide for the median aim, and for 95 % of the aims no squatter than the plain line's own
        /// dashes over the same stretch: the widening never shortens a dash on screen against its width. (Seen within six
        /// degrees of end on even the plain line's dashes show as dots, which the owner never called rungs.)
        /// <para>
        /// <b>Seen to fail</b> with the period and the share of it lit held at the line's (<c>FAR_STRETCH</c> 1,
        /// <c>FAR_DASH_FRACTION</c> at <c>DASH_FRACTION</c>): the widened dashes come out squatter than the line's.
        /// </para>
        /// </summary>
        [Fact]
        public void TheFarDashesStayLongerThanWideOnScreen()
        {
            List<Aim> aims = Measure(40);
            List<float> squattest = new(), plainSquattest = new(), againstPlain = new(), nearAspect = new(), reach = new();
            List<float> muzzlePx = new(), middlePx = new(), endPx = new(), drawn = new();

            foreach (Aim aim in aims)
            {
                float total = Total(aim.Path);
                XVector3 eye = Overview(aim.Path[0]);
                Lens lens = Lens.LookingAt(eye, aim.Path[^1]);
                float end = AimBeam.VisibleEnd(aim.Path, total, aim.Hit, eye);

                AimBeam.Piece[] pieces = Lay(aim.Path, 0.37f, AimBeam.FarEnd.Landing, aim.Hit, eye);
                drawn.Add(pieces.Length);

                float least = Squattest(pieces), plain = Squattest(Lay(aim.Path, 0.37f, AimBeam.FarEnd.Strike, aim.Hit, eye));
                if (least < float.MaxValue && plain < float.MaxValue)
                {
                    squattest.Add(least);
                    plainSquattest.Add(plain);
                    againstPlain.Add(least / plain);
                }

                //The half-light width on screen at the muzzle's first dash, half way and the last dash with a tenth of the
                //light, for the record
                muzzlePx.Add(HalfLightPixels(pieces[0], lens));
                middlePx.Add(HalfLightPixels(Nearest(pieces, aim.Path, 0.5f * total), lens));
                (XVector3 lastLit, AimBeam.Piece lastVisible) = LastLit(pieces, 0.1f);
                endPx.Add(HalfLightPixels(lastVisible, lens));
                reach.Add(AlongPath(aim.Path, lastLit) / total);

                //The squattest dash with a tenth of the light over the far part, [FAR_FROM * end, end] of the flight
                float Squattest(AimBeam.Piece[] laid)
                {
                    float most = float.MaxValue;
                    foreach (AimBeam.Piece p in laid)
                    {
                        XVector3 middle = (p.Tail + p.Head) * 0.5f;
                        float length = (lens.Project(p.Head) - lens.Project(p.Tail)).Length() * (2f / 3f);
                        float width = (p.TailWidth + p.HeadWidth) / lens.Depth(middle) / 3f;

                        float at = AlongPath(aim.Path, middle);
                        if (laid == pieces && at < AimBeam.FAR_FROM * end && at > 0.1f * end) nearAspect.Add(length / width);
                        if (at >= AimBeam.FAR_FROM * end && at <= end && p.TailAlpha * p.TailAlpha >= 0.1f) most = MathF.Min(most, length / width);
                    }

                    return most;
                }
            }

            output.WriteLine($"through the overview, the far dashes' half-light length over width at a beam's squattest: median {Percentile(squattest, 0.5):0.00}, " +
                $"5th percentile {Percentile(squattest, 0.05):0.00}, least {Percentile(squattest, 0):0.00}; the plain line's over the same stretch " +
                $"median {Percentile(plainSquattest, 0.5):0.00}, 5th percentile {Percentile(plainSquattest, 0.05):0.00}; the beam's over the plain line's " +
                $"5th percentile {Percentile(againstPlain, 0.05):0.00}, median {Percentile(againstPlain, 0.5):0.00}; the near dashes' median {Percentile(nearAspect, 0.5):0.00}");
            output.WriteLine($"half-light width at a 1080-line frame: the muzzle's dash {Percentile(muzzlePx, 0.5):0.0} px, half way {Percentile(middlePx, 0.5):0.0} px, " +
                $"the last dash with a tenth of the light {Percentile(endPx, 0.5):0.0} px (the light falling under a tenth at {Percentile(reach, 0.5):0.00} of the flight); " +
                $"draws a beam {Percentile(drawn, 0.5):0} at the median ({Percentile(drawn, 0):0} to {Percentile(drawn, 1):0})");

            Assert.True(Percentile(squattest, 0.5) >= 1f, $"the median beam's squattest far dash is {Percentile(squattest, 0.5):0.00} as long as wide on screen: rungs");
            Assert.True(Percentile(againstPlain, 0.05) >= 1f,
                $"at the 5th percentile a beam's squattest far dash is {Percentile(againstPlain, 0.05):0.00} of the plain line's there: the widening squashes it");
        }

        //A dash's half-light width across it on a lens's screen, in pixels of a 1080-line frame: a third of its width at its middle
        private static float HalfLightPixels(AimBeam.Piece piece, Lens lens) =>
            (piece.TailWidth + piece.HeadWidth) / lens.Depth((piece.Tail + piece.Head) * 0.5f) / 3f * PixelsPerUnit;

        private static AimBeam.Piece Nearest(AimBeam.Piece[] pieces, XVector3[] path, float distance)
        {
            AimBeam.Piece best = pieces[0];
            foreach (AimBeam.Piece p in pieces)
                if (MathF.Abs(AlongPath(path, (p.Tail + p.Head) * 0.5f) - distance) < MathF.Abs(AlongPath(path, (best.Tail + best.Head) * 0.5f) - distance))
                    best = p;
            return best;
        }

        /// <summary>
        /// <b>Where the ghost stands against the far end</b>, re-measured and reported (#1144): the ghost's centre off the line
        /// through the touch over the aims (held under <see cref="AimBeam.LANDING_SPREAD"/> at the 99th percentile, which is
        /// the constant's own claim), how many of them <see cref="AimBeam.FAR_WIDTH"/> still reaches, and through the overview
        /// how far on screen the ghost stands from the touch and from where the line's core falls under a tenth of the light.
        /// </summary>
        [Fact]
        public void WhereTheGhostStandsAgainstTheFarEnd()
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

            int reached = aims.FindAll(a => a.Lateral <= AimBeam.FAR_WIDTH).Count;
            int reachedWithBall = aims.FindAll(a => a.Lateral <= AimBeam.FAR_WIDTH + BallsConstraintsBuilder.BALL_RADIUS).Count;

            List<float> angles = new(), fromTouch = new(), fromEnd = new();
            foreach (Aim aim in aims)
            {
                Lens lens = Lens.LookingAt(Overview(aim.Path[0]), aim.Path[^1]);
                angles.Add(MathHelper.ToDegrees(MathF.Acos(XVector3.Dot(XVector3.Normalize(aim.Path[^1] - aim.Path[^2]),
                    XVector3.Normalize(aim.Path[^1] - Overview(aim.Path[0]))))));

                XVector2 ghost = lens.Project(aim.Ghost);
                fromTouch.Add((ghost - lens.Project(aim.Path[^1])).Length() * PixelsPerUnit);

                XVector3 lastLit = LastLit(Lay(aim.Path, 0.37f, AimBeam.FarEnd.Landing, aim.Hit, Overview(aim.Path[0])), 0.1f).Point;
                fromEnd.Add((ghost - lens.Project(lastLit)).Length() * PixelsPerUnit);
            }

            output.WriteLine($"FAR_WIDTH {AimBeam.FAR_WIDTH} reaches the ghost's centre in {reached} of {aims.Count} aims ({100f * reached / aims.Count:0} %) " +
                $"and some of the ghost in {reachedWithBall} ({100f * reachedWithBall / aims.Count:0} %)");
            output.WriteLine($"the overview sees the far leg {Percentile(angles, 0):0.0} to {Percentile(angles, 1):0.0} degrees off end on, {Percentile(angles, 0.5):0.0} at the median");
            output.WriteLine($"through the overview at a 1080-line frame the ghost stands {Percentile(fromTouch, 0.5):0} px from the touch at the median " +
                $"(95th {Percentile(fromTouch, 0.95):0}) and {Percentile(fromEnd, 0.5):0} px from where the line's core falls under a tenth of the light (95th {Percentile(fromEnd, 0.95):0})");
        }

        /// <summary>
        /// <b>The far part is laid once over the whole flight, not once per leg</b> (a shot bent by a gravity well, #332):
        /// laid along a straight flight cut into legs it is the uncut flight's light, point for point away from a knot, the far
        /// part included; along a bent flight every piece lies on the flight and its widths are the curve's at the distance
        /// flown, so the dashes widen once over the whole of it.
        /// </summary>
        [Fact]
        public void TheFarPartIsLaidOverTheWholeFlightNotPerLeg()
        {
            XVector3[] straight = [Muzzle, Touch];
            XVector3[] cut = [Muzzle, OnLine(0.17f), OnLine(0.48f), OnLine(0.62f), OnLine(0.71f), OnLine(0.86f), OnLine(0.93f), Touch];

            AimBeam.Piece[] straightPieces = Lay(straight, 0.3f, AimBeam.FarEnd.Landing);
            AimBeam.Piece[] cutPieces = Lay(cut, 0.3f, AimBeam.FarEnd.Landing);

            //A dash cut at a knot is the same dash in two pieces, end to end: the same two ends, and the same width and
            //alpha at either end, carried on unbroken through the knot
            List<List<AimBeam.Piece>> dashes = new();
            foreach (AimBeam.Piece p in cutPieces)
            {
                if (dashes.Count > 0 && XVector3.Distance(dashes[^1][^1].Head, p.Tail) < 1e-4f) dashes[^1].Add(p);
                else dashes.Add([p]);
            }

            Assert.Equal(straightPieces.Length, dashes.Count);
            Assert.True(cutPieces.Length > straightPieces.Length, "no dash of the cut flight was cut at a knot");
            for (int i = 0; i < straightPieces.Length; i++)
            {
                AimBeam.Piece whole = straightPieces[i], first = dashes[i][0], last = dashes[i][^1];
                Assert.True(XVector3.Distance(whole.Tail, first.Tail) < 1e-3f && XVector3.Distance(whole.Head, last.Head) < 1e-3f,
                    $"dash {i} lies elsewhere on a flight cut into legs: {whole} against {first} .. {last}");
                Assert.Equal(whole.TailWidth, first.TailWidth, 4);
                Assert.Equal(whole.HeadWidth, last.HeadWidth, 4);
                Assert.Equal(whole.TailAlpha, first.TailAlpha, 4);
                Assert.Equal(whole.HeadAlpha, last.HeadAlpha, 4);
                for (int k = 1; k < dashes[i].Count; k++)
                {
                    Assert.Equal(dashes[i][k - 1].HeadWidth, dashes[i][k].TailWidth, 5);
                    Assert.Equal(dashes[i][k - 1].HeadAlpha, dashes[i][k].TailAlpha, 5);
                }
            }

            //And so, away from a knot (where a piece's own quarter fades fall), the same light on the plain part of the line
            for (float f = 0.02f; f < AimBeam.FAR_FROM; f += 0.01f)
                if (!NearKnot(f)) Assert.Equal(LightAt(straightPieces, OnLine(f)), LightAt(cutPieces, OnLine(f)), 3);

            XVector3[] bent = Bent();
            float flown = Total(bent);
            AimBeam.Piece[] bentPieces = Lay(bent, 0.3f, AimBeam.FarEnd.Landing);

            Assert.Contains(bentPieces, p => p.HeadWidth > AimBeam.WIDTH);
            foreach (AimBeam.Piece p in bentPieces)
            {
                Assert.True(DistanceToPath(bent, p.Tail) < 1e-3f && DistanceToPath(bent, p.Head) < 1e-3f, $"a piece off a bent flight: {p}");
                float expected = AimBeam.FarWidth(AimBeam.FarAlong(AlongPath(bent, p.Head), AimBeam.FAR_FROM * flown, flown));
                Assert.True(MathF.Abs(p.HeadWidth - expected) < 2e-3f,
                    $"a piece of a bent flight {p.HeadWidth:0.000} wide where the distance flown says {expected:0.000}");
            }

            static bool NearKnot(float f)
            {
                foreach (float knot in new[] { 0.17f, 0.48f, 0.62f, 0.71f, 0.86f, 0.93f })
                    if (MathF.Abs(f - knot) < 0.04f) return true;
                return false;
            }
        }

        /// <summary>
        /// <b>A beam that reaches nothing is the fading line it was</b>: no far part (there is no landing to show), the line's
        /// width all the way, one alpha a dash, nothing laid past its end, and its light dying away towards the tip.
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

            Check(bent, Total(bent));

            void Check(XVector3[] path, float length)
            {
                AimBeam.Piece[] pieces = Lay(path, 0.2f, AimBeam.FarEnd.OpenAir);

                Assert.NotEmpty(pieces);
                Assert.All(pieces, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH && p.TailAlpha == p.HeadAlpha));

                //Laid out to the end and not past it: the farthest dash's head within a period of it, along the flight
                float farthest = 0f;
                foreach (AimBeam.Piece p in pieces) farthest = MathF.Max(farthest, AlongPath(path, p.Head));
                Assert.True(farthest >= length - AimBeam.SPACING - 1e-3f && farthest <= length + 1e-3f,
                    $"an open beam {length:0.0} long is laid out to {farthest:0.0}");

                Assert.True(pieces[^1].HeadAlpha < 0.5f, $"the last dash of an open beam {length:0.0} long is at {pieces[^1].HeadAlpha:0.00}, not dying away");
                Assert.True(pieces[0].TailAlpha == 1f);
            }
        }

        /// <summary>
        /// <b>A knot laid within a thousandth of the far end does not take the far end with it</b>: a stepped flight (a
        /// well's, a crate's) can end a hair past its last knot, or on it. A landing beam still widens and fades to the touch
        /// as it does on a flight without that knot, and a beam that reaches nothing still fades out over that leg rather than
        /// over a last leg of nothing (which would leave no dash lit at all).
        /// </summary>
        [Fact]
        public void AKnotAtTheFarEndKeepsTheFarEnd()
        {
            XVector3 direction = XVector3.Normalize(Touch - Muzzle);

            AimBeam.Piece[] plain = Lay([Muzzle, Touch], 0f, AimBeam.FarEnd.Landing);
            foreach (XVector3[] knotted in new[] { new[] { Muzzle, Touch - direction * 0.0005f, Touch }, new[] { Muzzle, Touch, Touch } })
            {
                AimBeam.Piece[] pieces = Lay(knotted, 0f, AimBeam.FarEnd.Landing);
                Assert.Equal(plain.Length, pieces.Length);
                for (int i = 0; i < plain.Length; i++)
                {
                    Assert.True(XVector3.Distance(plain[i].Head, pieces[i].Head) < 1e-3f, $"piece {i} moved: {plain[i]} against {pieces[i]}");
                    Assert.Equal(plain[i].HeadWidth, pieces[i].HeadWidth, 3);
                    Assert.Equal(plain[i].TailAlpha, pieces[i].TailAlpha, 3);
                    Assert.Equal(plain[i].HeadAlpha, pieces[i].HeadAlpha, 3);
                }
            }

            XVector3 far = Muzzle + direction * 24f;
            foreach (XVector3[] open in new[] { new[] { Muzzle, far - direction * 0.0005f, far }, new[] { Muzzle, far, far } })
            {
                AimBeam.Piece[] pieces = Lay(open, 0.2f, AimBeam.FarEnd.OpenAir);
                Assert.NotEmpty(pieces);
                Assert.True(pieces[0].TailAlpha == 1f && pieces[^1].HeadAlpha < 0.5f,
                    $"an open beam with a knot at its end runs from {pieces[0].TailAlpha:0.00} to {pieces[^1].HeadAlpha:0.00}, not the fading line");
            }
        }

        /// <summary>
        /// <b>A strike is the plain line, stopping at the ball</b>, which is what a cutter's beam is (it strikes the ball the
        /// beam ends on and has no cell to show): dashes of the line's width at full light all the way, the period the line's,
        /// a dash within a period of where the lens sees the beam reach the ball and none past it. From the side that is the
        /// touch; from the overview, short of it, so the line does not run on over the ball it strikes.
        /// </summary>
        [Fact]
        public void AStrikeIsThePlainLineStoppingAtTheBall()
        {
            XVector3[] path = [Muzzle, Touch];
            float total = Total(path);
            XVector3 ball = BallAhead(path);

            foreach (XVector3 eye in new[] { SideOn(path), Overview(Muzzle) })
            {
                float end = AimBeam.VisibleEnd(path, total, ball, eye);
                AimBeam.Piece[] pieces = Lay(path, 0.5f, AimBeam.FarEnd.Strike, ball, eye);

                Assert.NotEmpty(pieces);
                Assert.All(pieces, p => Assert.True(p.TailWidth == AimBeam.WIDTH && p.HeadWidth == AimBeam.WIDTH && p.TailAlpha == 1f && p.HeadAlpha == 1f));
                Assert.All(pieces, p => Assert.True(XVector3.Distance(Muzzle, p.Head) <= end + 1e-3f));
                Assert.Contains(pieces, p => XVector3.Distance(Muzzle, p.Head) > end - AimBeam.SPACING);

                //The first dash may be clipped at the muzzle; from the second on they stand a period apart
                for (int i = 2; i < pieces.Length; i++)
                    Assert.Equal(AimBeam.SPACING, XVector3.Distance(pieces[i - 1].Tail, pieces[i].Tail), 3);
            }

            Assert.Equal(total, AimBeam.VisibleEnd(path, total, ball, SideOn(path)), 3);
            Assert.True(AimBeam.VisibleEnd(path, total, ball, Overview(Muzzle)) < total - 0.5f, "the overview sees the beam reach the ball at the touch");
        }
    }
}
