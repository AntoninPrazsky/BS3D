using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using static BS3D.Tests.PlayViewOpenings;

namespace BS3D.Tests
{
    /// <summary>
    /// The sea's oil platforms (#969, #1169), held over the very triangles the two meshes are built of (<see cref="OilRigs.Mesh"/>):
    /// no two faces of different paint in one plane facing one way, which the depth buffer cannot tell apart (the owner saw a
    /// wall flicker); every triangle wound clockwise seen from outside; the finer mesh drawn all through the chapter intro's run
    /// at the nearer rig and never from a play view, its reach a depth rule the trailer's maxdetail leaves; nothing under the
    /// deck farther out than the jacket's reach; and the finer mesh carrying what the references show.
    /// </summary>
    public class OilRigTests
    {
        private readonly ITestOutputHelper _output;

        public OilRigTests(ITestOutputHelper output) => _output = output;

        private static readonly SeaSceneConfig Sea = new();

        //The most two faces may share seen along them on the finer mesh, square units: about a pixel at the far edge of
        //FINE_REACH, where a junction of two members leaves a sliver of one face in the other's plane
        private const float FINE_SPECK = 0.1f;

        private static IEnumerable<(string Name, OilRig Rig)> Rigs()
        {
            yield return ("the nearer rig", OilRigLayout.Near(Sea.LevelY));
            yield return ("the farther rig", OilRigLayout.Far(Sea.LevelY));
        }

        private readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Vector3 Out, uint Paint, int Index, int Part);

        //A rig's parts and their triangles, each with the way it faces out (clockwise seen from outside, so against its
        //(b - a) x (c - a)), its paint (the colour and the kind in one word) and the part it belongs to
        private sealed record Model(List<RigPart> Parts, List<Matrix> Inverses, List<Face> Faces);

        private static Model Build(OilRig rig, bool fine)
        {
            var parts = new List<RigPart>();
            OilRigs.Parts(rig, fine, parts.Add);
            var faces = new List<Face>();
            var mesh = new List<RigVertex>();
            for (int p = 0; p < parts.Count; p++)
            {
                mesh.Clear();
                OilRigs.Emit(mesh, parts[p]);
                for (int i = 0; i < mesh.Count; i += 3)
                {
                    Vector3 a = mesh[i].Position, b = mesh[i + 1].Position, c = mesh[i + 2].Position;
                    Vector3 g = Vector3.Cross(b - a, c - a);
                    if (g.LengthSquared() < 1e-10f) continue;
                    faces.Add(new Face(a, b, c, -Vector3.Normalize(g), mesh[i].Color.PackedValue, faces.Count, p));
                }
            }
            return new Model(parts, parts.Select(part => Matrix.Invert(part.World)).ToList(), faces);
        }

        //Whether a point stands inside a part's solid (a tube's and the helideck's as their inscribed round, which is inside them)
        private static bool Inside(Model model, int part, Vector3 point)
        {
            const float E = 1e-3f;
            RigPart p = model.Parts[part];
            Vector3 l = Vector3.Transform(point, model.Inverses[part]), h = p.Half;
            switch (p.Shape)
            {
                case RigShape.Tube:
                    return MathF.Abs(l.X) < h.X - E && new Vector2(l.Y, l.Z).Length() < h.Y * MathF.Cos(MathF.PI / p.Sides) - E;
                case RigShape.Helideck:
                    return MathF.Abs(l.Y) < h.Y - E && new Vector2(l.X, l.Z).Length() < h.X * MathF.Cos(MathF.PI / p.Sides) - E;
                default:
                    return MathF.Abs(l.X) < h.X - E && MathF.Abs(l.Y) < h.Y - E && MathF.Abs(l.Z) < h.Z - E;
            }
        }

        /// <summary>
        /// Every pair of triangles that face one way (within 1.8 degrees), lie within <paramref name="gap"/> of one plane, overlap
        /// seen along it by more than <paramref name="speck"/> and are painted differently, unless what stands just outside the
        /// outer of the two is a third part's solid (both buried in it, as a post's foot and a column's are in the deck they
        /// stand on): the pairs the depth buffer can lose between where they can be seen. Bucketed by the way they face.
        /// </summary>
        private static List<(Face First, Face Second, float Apart, float Area)> Fighting(Model model, float gap, float speck)
        {
            const float SAME_WAY = 0.9995f, BUCKET = 50f;
            var buckets = new Dictionary<(int, int, int), List<Face>>();
            (int, int, int) Key(Vector3 n) => ((int)MathF.Round(n.X * BUCKET), (int)MathF.Round(n.Y * BUCKET), (int)MathF.Round(n.Z * BUCKET));
            foreach (Face f in model.Faces)
            {
                if (!buckets.TryGetValue(Key(f.Out), out List<Face> list)) buckets[Key(f.Out)] = list = new List<Face>();
                list.Add(f);
            }

            var found = new List<(Face, Face, float, float)>();
            foreach (Face f in model.Faces)
            {
                (int x, int y, int z) = Key(f.Out);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (!buckets.TryGetValue((x + dx, y + dy, z + dz), out List<Face> others)) continue;
                            foreach (Face g in others)
                            {
                                if (g.Index <= f.Index || g.Paint == f.Paint || Vector3.Dot(f.Out, g.Out) < SAME_WAY) continue;
                                float plane = Vector3.Dot(f.Out, f.A);
                                float apart = MathF.Max(MathF.Abs(Vector3.Dot(f.Out, g.A) - plane), MathF.Max(MathF.Abs(Vector3.Dot(f.Out, g.B) - plane), MathF.Abs(Vector3.Dot(f.Out, g.C) - plane)));
                                if (apart >= gap) continue;
                                float area = OverlapSeenAlong(f, g, out Vector3 middle);
                                if (area <= speck) continue;

                                //Just outside the outer of the two, over the middle of what they share
                                float outer = MathF.Max(0f, (Vector3.Dot(f.Out, g.A) + Vector3.Dot(f.Out, g.B) + Vector3.Dot(f.Out, g.C)) / 3f - plane);
                                Vector3 outside = middle + f.Out * (outer + 0.05f);
                                bool buried = false;
                                for (int p = 0; p < model.Parts.Count && !buried; p++)
                                    buried = p != f.Part && p != g.Part && Inside(model, p, outside);
                                if (!buried) found.Add((f, g, apart, area));
                            }
                        }
            }
            return found;
        }

        //The area two triangles cover in common, seen along the first's facing (Sutherland-Hodgman, both convex), and the middle
        //of what they share on the first's plane
        private static float OverlapSeenAlong(Face f, Face g, out Vector3 middle)
        {
            Vector3 n = f.Out, u = Vector3.Normalize(Vector3.Cross(n, MathF.Abs(n.Y) < 0.9f ? Vector3.Up : Vector3.UnitX)), w = Vector3.Cross(n, u);
            Vector2 P(Vector3 p) => new(Vector3.Dot(p, u), Vector3.Dot(p, w));
            List<Vector2> clip = Ccw(new List<Vector2> { P(f.A), P(f.B), P(f.C) });
            List<Vector2> subject = Ccw(new List<Vector2> { P(g.A), P(g.B), P(g.C) });
            for (int i = 0; i < clip.Count && subject.Count > 0; i++)
            {
                Vector2 e0 = clip[i], e1 = clip[(i + 1) % clip.Count];
                float Side(Vector2 p) => (e1.X - e0.X) * (p.Y - e0.Y) - (e1.Y - e0.Y) * (p.X - e0.X);
                var kept = new List<Vector2>();
                for (int k = 0; k < subject.Count; k++)
                {
                    Vector2 s0 = subject[k], s1 = subject[(k + 1) % subject.Count];
                    float d0 = Side(s0), d1 = Side(s1);
                    if (d0 >= 0f) kept.Add(s0);
                    if ((d0 >= 0f) != (d1 >= 0f)) kept.Add(s0 + (s1 - s0) * (d0 / (d0 - d1)));
                }
                subject = kept;
            }

            Vector2 sum = Vector2.Zero;
            foreach (Vector2 q in subject) sum += q;
            Vector2 mid = subject.Count > 0 ? sum / subject.Count : Vector2.Zero;
            middle = u * mid.X + w * mid.Y + n * Vector3.Dot(n, f.A);
            return MathF.Abs(Area(subject));
        }

        private static float Area(List<Vector2> polygon)
        {
            float twice = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                twice += a.X * b.Y - b.X * a.Y;
            }
            return twice * 0.5f;
        }

        private static List<Vector2> Ccw(List<Vector2> triangle)
        {
            if (Area(triangle) < 0f) triangle.Reverse();
            return triangle;
        }

        [Fact]
        public void NoTwoFacesOfDifferentPaintLieInOnePlaneFacingOneWay()
        {
            //The owner (#1169): "z-fighting is visible on them (a wall flickering)". Two faces facing one way in one plane, in
            //different paint, are a coin toss for the depth buffer at every pixel they share: the white module's end and the
            //orange's lay in the upper deck's end faces' planes over the 1.5 units they stood into its slab. The coarser mesh is
            //drawn from as far as the farther rig's far corner from the play view, some 1250 out, where a 24-bit depth step at
            //the Game's near plane (0.05) is about 1.9: no pair within COARSE_GAP over any area. The finer is drawn only within
            //FINE_REACH of a rig's foot, a step of about 0.21 at its far corner: no pair that can be seen within FINE_GAP over
            //more than FINE_SPECK, a few pixels at the intro's run (where members cross, a face of one runs on inside the other
            //and nothing shows). Seen to fire with the lower modules back as they stood (4 pairs on the coarser mesh, 0 apart over
            //39 and 3 square units: their ends in the upper deck's end planes), with the windows 0.2 proud (129 on the finer),
            //with the coarser lamps' stems painted as the railing (8: a stem's top under its lamp's) and the helideck's foot in
            //the deck's grey (2).
            foreach ((string name, OilRig rig) in Rigs())
                foreach ((bool fine, float gap, float speck) in new[] { (false, OilRigs.COARSE_GAP, 1e-3f), (true, OilRigs.FINE_GAP, FINE_SPECK) })
                {
                    var pairs = Fighting(Build(rig, fine), gap, speck);
                    string mesh = fine ? "finer" : "coarser";
                    foreach (var (first, second, apart, area) in pairs.Take(6))
                        _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}, {1}: triangles {2} and {3} face {4} {5:F2} apart over {6:F2} square units at {7}",
                            name, mesh, first.Index, second.Index, first.Out, apart, area, first.A));
                    Assert.True(pairs.Count == 0, $"{name}'s {mesh} mesh has {pairs.Count} pairs of differently painted faces within {gap} of one plane");
                }
        }

        [Fact]
        public void EveryTriangleIsWoundClockwiseSeenFromOutside()
        {
            //CLAUDE.md "Triangle winding": a triangle's (b - a) x (c - a) points away from the normals it is lit by (a tube's sides
            //are lit smooth, within half a side of the face). The rigs are drawn culling the faces turned away, so a backwards
            //triangle would show the part's far side through it; WindingCheck reads indexed meshes only. Seen to fire with the
            //builder's swap taken out.
            foreach (bool fine in new[] { false, true })
            {
                List<RigVertex> mesh = OilRigs.Mesh(new OilRig(Vector3.Zero, 0f, 1f), fine);
                Assert.True(mesh.Count > 0 && mesh.Count % 3 == 0);
                for (int i = 0; i < mesh.Count; i += 3)
                {
                    Vector3 geometric = Vector3.Normalize(Vector3.Cross(mesh[i + 1].Position - mesh[i].Position, mesh[i + 2].Position - mesh[i].Position));
                    for (int k = 0; k < 3; k++)
                        Assert.True(Vector3.Dot(geometric, mesh[i + k].Normal) < 0f, $"{(fine ? "finer" : "coarser")} mesh: triangle {i / 3} faces its normal");
                }
                _output.WriteLine($"the {(fine ? "finer" : "coarser")} mesh: {mesh.Count / 3} triangles a rig");
            }
        }

        [Fact]
        public void TheFinerMeshIsDrawnAllThroughTheRunAtTheRigAndNeverFromAPlayView()
        {
            //What #1169 is for: the chapter intro's run passes the nearer rig 250 to 140 off, and the finer mesh must stand there
            //from its first frame to its last; a play view stands 800 and more from either rig and draws both coarse, so play
            //costs nothing. And the tier decides first: Low and Medium draw the coarser at any distance. A lens over the rig is
            //held to the reach as one beside it is. Seen to fire with FINE_REACH at 200, and with the reach taken across the
            //water alone.
            OilRig near = OilRigLayout.Near(Sea.LevelY), far = OilRigLayout.Far(Sea.LevelY);
            Assert.True(OilRigs.DrawsFine(true, near.Foot + new Vector3(OilRigs.FINE_REACH - 1f, 0f, 0f), near.Foot));
            Assert.False(OilRigs.DrawsFine(true, near.Foot + new Vector3(OilRigs.FINE_REACH + 1f, 0f, 0f), near.Foot));
            Assert.False(OilRigs.DrawsFine(true, near.Foot + new Vector3(30f, OilRigs.FINE_REACH, 0f), near.Foot));
            Assert.False(OilRigs.DrawsFine(false, near.Foot, near.Foot));

            float farthest = 0f;
            for (int seed = 1; seed <= 24; seed++)
            {
                IntroShot run = Array.Find(SeaIntroShots.Shots(Sea, new Vector3(0.69f, 0.22f, -0.69f), GAME_FOV, new Random(seed)), shot => shot.Name == "the rig");
                for (int i = 0; i <= 40; i++)
                {
                    run.Pose(i / 40f, out Vector3 lens, out _);
                    farthest = MathF.Max(farthest, Vector2.Distance(new Vector2(lens.X, lens.Z), new Vector2(near.Foot.X, near.Foot.Z)));
                    Assert.True(OilRigs.DrawsFine(true, lens, near.Foot), $"seed {seed}: the run's lens at {i}/40 draws the nearer rig coarse");
                }
            }

            float nearest = float.MaxValue;
            foreach (float aspect in ASPECTS)
                foreach (string file in Shipped.LevelFiles())
                    foreach ((_, Vector3 lens, _, _) in Open(file, aspect).Poses)
                        foreach (OilRig rig in new[] { near, far })
                        {
                            nearest = MathF.Min(nearest, Vector2.Distance(new Vector2(lens.X, lens.Z), new Vector2(rig.Foot.X, rig.Foot.Z)));
                            Assert.False(OilRigs.DrawsFine(true, lens, rig.Foot), $"{file} at {aspect:F2}: a play view draws a rig fine");
                        }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "the run's lens stands at most {0:F0} from the nearer rig's foot, a play view's at least {1:F0} from either; fine within {2}",
                farthest, nearest, OilRigs.FINE_REACH));
        }

        //A 24-bit depth step at view depth z under the Game's projection (RecoilCamera's planes): two surfaces nearer each
        //other than this along one ray, the depth buffer may not tell apart. The play view's near plane: exact for a play view,
        //and the run's lens, far from the drain, draws with a farther plane (RecoilCamera.NearPlaneAt), whose step is finer
        private static float DepthStep(float z)
        {
            var camera = new RecoilCamera();
            float n = camera.NearPlane, f = camera.FarPlane;
            return z * z * (f - n) / (f * n * 16777216f);
        }

        [Fact]
        public void TheFinerMeshsReachIsADepthRuleThatHoldsUnderMaxDetail()
        {
            //Why the trailer's maxdetail (RenderDetail.Maximum, #1178) does not lift FINE_REACH as it lifts the cost rules: the
            //finer mesh keeps its faces only FINE_GAP apart. Within the reach no part of a rig stands farther from the lens than
            //the reach plus the rig's own (three dimensions, as DrawsFine measures), where a depth step is still under FINE_GAP;
            //from every play view a step at the finer mesh's nearest point, along the view, is over it, so drawn there its
            //windows, decks and stairs would fight. Seen to fire with FINE_REACH at 450.
            var near = new List<Vector3>();
            foreach ((string name, OilRig rig) in Rigs())
            {
                List<RigVertex> mesh = OilRigs.Mesh(rig, true);
                float own = mesh.Max(v => Vector3.Distance(v.Position, rig.Foot));
                float farthest = OilRigs.FINE_REACH + own;
                _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}: within the reach no part stands past {1:F0}, a depth step there {2:F3}",
                    name, farthest, DepthStep(farthest)));
                Assert.True(DepthStep(farthest) < OilRigs.FINE_GAP, $"{name}: a depth step at the finer mesh's farthest point from a lens within the reach is over FINE_GAP");
                near.AddRange(mesh.Select(v => v.Position));
            }

            float least = float.MaxValue;
            foreach (float aspect in ASPECTS)
                foreach (string file in Shipped.LevelFiles())
                    foreach ((_, Vector3 lens, Vector3 target, _) in Open(file, aspect).Poses)
                    {
                        Vector3 forward = Vector3.Normalize(target - lens);
                        foreach (Vector3 v in near)
                        {
                            float z = Vector3.Dot(v - lens, forward);
                            if (z > 0f) least = MathF.Min(least, z);
                        }
                    }
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "from a play view the finer mesh stands {0:F0} deep at the nearest, a depth step there {1:F3} against FINE_GAP {2}",
                least, DepthStep(least), OilRigs.FINE_GAP));
            Assert.True(DepthStep(least) > OilRigs.FINE_GAP, "a play view stands near enough a rig to draw its finer mesh without its faces fighting");
        }

        [Fact]
        public void NothingLowOnTheJacketStandsFartherOutThanItsReach()
        {
            //OilRig.JacketReach is what the intro's notes and the swell's test reckon a rig's footprint down at the water by.
            //Every corner of every part's box under the jacket's top, on both meshes and both rigs. Seen to fire with
            //JACKET_REACH at 36 (the boat landing's corners stand 37.4 out).
            foreach ((string name, OilRig rig) in Rigs())
                foreach (bool fine in new[] { false, true })
                {
                    float widest = 0f;
                    OilRigs.Parts(rig, fine, part =>
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 h = part.Half;
                            Vector3 corner = Vector3.Transform(new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z), part.World);
                            if (corner.Y >= rig.JacketTop - 1e-3f) continue;
                            float reach = Vector2.Distance(new Vector2(corner.X, corner.Z), new Vector2(rig.Foot.X, rig.Foot.Z));
                            widest = MathF.Max(widest, reach);
                            Assert.True(reach <= rig.JacketReach + 1e-3f, $"{name}: a {part.Piece} part stands {reach:F2} out low on the jacket, past {rig.JacketReach:F2}");
                        }
                    });
                    _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0}, {1}: low on the jacket out to {2:F1} of {3:F1}", name, fine ? "finer" : "coarser", widest, rig.JacketReach));
                }
        }

        [Fact]
        public void TheFinerRigCarriesWhatTheReferencesShow()
        {
            //The references (#1169) read at the intro's 140 to 250: railings round the decks, stairs, pipework, a crane boom of
            //members, lifeboats, a lattice derrick, a helideck with its net and markings. The coarser mesh is the 47 boxes it had,
            //a stem under each corner lamp and a foot under the helideck.
            var coarse = new Dictionary<RigPiece, int>();
            var fine = new Dictionary<RigPiece, int>();
            var shapes = new Dictionary<RigShape, int>();
            OilRigs.Parts(OilRigLayout.Near(Sea.LevelY), false, part => coarse[part.Piece] = coarse.GetValueOrDefault(part.Piece) + 1);
            OilRigs.Parts(OilRigLayout.Near(Sea.LevelY), true, part =>
            {
                fine[part.Piece] = fine.GetValueOrDefault(part.Piece) + 1;
                shapes[part.Shape] = shapes.GetValueOrDefault(part.Shape) + 1;
            });

            Assert.Equal(52, coarse.Values.Sum());
            Assert.True(fine.GetValueOrDefault(RigPiece.Railing) >= 100, "the finer rig has no railings to speak of");
            Assert.True(fine.GetValueOrDefault(RigPiece.Stair) >= 50, "the finer rig has no stairs to speak of");
            Assert.True(fine.GetValueOrDefault(RigPiece.Lifeboat) >= 4, "the finer rig has no lifeboats");
            Assert.True(fine.GetValueOrDefault(RigPiece.Pipe) >= 10, "the finer rig has no pipework");
            Assert.True(fine.GetValueOrDefault(RigPiece.Derrick) >= 100, "the finer rig's derrick is not a lattice of members");
            Assert.True(fine.GetValueOrDefault(RigPiece.Crane) >= 30, "the finer rig's crane boom is not a lattice of members");
            Assert.True(fine.GetValueOrDefault(RigPiece.Window) >= 30, "the finer rig's modules have no windows");
            Assert.Equal(1, shapes.GetValueOrDefault(RigShape.Helideck));
            _output.WriteLine("the finer rig's parts: " + string.Join(", ", fine.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")));
        }
    }
}
