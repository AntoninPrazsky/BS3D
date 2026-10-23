using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BS3D.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BS3D.Tools.WindingCheck
{
    /// <summary>
    /// <c>--openness</c> (#974): whether every procedural mesh of every scene is shut. The winding check says which way a
    /// face points, never whether the solid has a hole, and under back-face culling an open end shows what is behind it
    /// through the wall (the hay roll's ends, the savanna's knees, the palm's foot). It reads the same buffers the winding
    /// check does and runs <see cref="MeshOpenness.ExposedLoops(IReadOnlyList{VertexPositionNormalTexture}, IReadOnlyList{int}, float)"/>
    /// - the very code the logic tests stand on - over each one: an open ring is <b>exposed</b> unless every point of it lies
    /// inside another solid of the same mesh.
    /// <para>
    /// <b>A gate.</b> Exit 1 when a mesh has an exposed ring that <see cref="Allowed"/> does not name, or more of them than
    /// its entry allows. Everything open on purpose is in that one table with the reason, as the winding check's
    /// <see cref="Expected"/> is: two-sided cards, CullNone ribbons, a rim under the ground, a rim inside the solid it is
    /// drawn with. Add a mesh that is open and the gate fails until it is closed or named there.
    /// </para>
    /// <para>
    /// <b>Assemblies.</b> Some solids are drawn as several buffers that only close together: a trunk and the crown it ends
    /// in, the island's cap and body, a tree's twigs and the branch ends they start on. <see cref="Assemblies"/> joins the
    /// parts that are one object before they are judged, so a ring buried in its neighbour's buffer is seen as buried.
    /// </para>
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Stands for "any number": a sheet's rings are counted by the card, and a card is open by design.</summary>
        private const int ANY = int.MaxValue;

        /// <summary>
        /// Like <see cref="ANY"/>, but only for a mesh that really is a heap of cards: one open edge or more to every triangle (a
        /// leaf card is two triangles and four open edges). A solid in the same family - the savanna's buckets hold both - is
        /// judged as any other, with no rings allowed.
        /// </summary>
        private const int CARDS = int.MaxValue - 1;

        /// <summary>
        /// A planted object's foot ring this far under its own ground level (the part's y = 0) is under the earth and
        /// excused; one at the ground, like the palm's was, is not: a slope under it shows the lip.
        /// </summary>
        private const float SUNK = -0.01f;

        /// <summary>
        /// The meshes allowed to have exposed rings, by a substring of their path, how many at most, and why. The first
        /// entry that matches a part rules it. <see cref="ANY"/> skips counting (a card sheet: thousands of rings, none a
        /// hole); a number is a ceiling the part must stay at or under, so a mesh that gets <i>more</i> open fails.
        /// <para>
        /// A row that is not about cards is a mesh someone has looked at, with a reason that can be checked; a row that only waits
        /// for a fix names the issue that will close it. Take a row out when its mesh is shut and the gate is stricter for it.
        /// </para>
        /// </summary>
        private static readonly (string PathPart, int Max, string Reason)[] Allowed =
        {
            //---- first, because the first row that fits rules: the bank is a mesh of the savanna's buckets too
            ("<BermMesh>", 2, "the bank's inner rim lies inside the drum and its outer one under the terrain it was built off (IslandBerm)"),

            //---- two-sided sheets and cards: open edges are the outline of a leaf
            ("PalmMesh[0].Fronds", ANY, "CullNone leaflet ribbons, shaded per face (Palm.fx): a sheet's outline"),
            ("PalmMesh[1].Fronds", ANY, "CullNone leaflet ribbons, shaded per face (Palm.fx): a sheet's outline"),
            ("PalmMesh[2].Fronds", ANY, "CullNone leaflet ribbons, shaded per face (Palm.fx): a sheet's outline"),
            ("PalmMesh[3].Fronds", ANY, "CullNone leaflet ribbons, shaded per face (Palm.fx): a sheet's outline"),
            ("SavannaScatter.Buckets", CARDS, "the savanna's leaf cards and grass fans (two-sided ribbons)"),
            ("ForestDressing._owned", CARDS, "the forest's leaf and fern cards on High and Ultra (#962: two-sided ribbons)"),
            ("SavannaScatter._meshes[30].Leaves", ANY, "the savanna's leaf cards (two-sided ribbons)"),
            (".Leaves <UploadedMesh>", ANY, "the meadow's leaf cards (two-sided ribbons)"),
            ("BirdMesh", ANY, "the wings are single sheets and the body a shell: drawn CullNone, a bird is 0.9 across"),
            ("FunnelRimsMesh", ANY, "zero-thickness ribbons drawn CullNone (as the winding check names them)"),
            ("FunnelMesh", ANY, "the drain's cone and pit are surfaces drawn CullNone with TwoSidedNormals: their rims are the throat and the lip"),

            //---- open where nothing can see it, as the mesh's own remarks say
            ("CavernMeshes(coarse).Rock", ANY, "the cavern's shell and lake bed are sheets seen from inside the hall: the shell's lower rim stands under the bed, the bed's outline 30 units outside the wall, and where an island's subdivided cells meet the plain ones their points lie on the plain edge's line (T-junctions, no gap; CavernGeometry.AddFloor)"),
            ("ArenaIsland._driftMesh", 1, "a sheet: its inner edge is inside the drum and its two ends taper to nothing (SandDriftMesh)"),
            ("BallRenderSet._bladeMesh", 2, "the saw's inner edges lie inside the ball it is set through (SawBladeMesh)"),
            ("PotatoKindMesh.MineStuds", 8, "each stud's base ring lies on the ball it studs"),

            //---- the gun, reviewed one by one: none shows the inside of anything
            ("CannonRig._mesh", 4, "T-junctions at the girdle's and the taper's steps: a cheek's long edge meets two shorter ones that cover it exactly"),
            ("CannonRig._glassMesh", 1, "the pane is one thin curved sheet laid in the window's rebate, its rim on the rebate"),
            ("CannonRig._collarMesh", 2, "a band round the muzzle: both rims lie on the tube's swell"),
            ("CannonRig._carriageMesh", 1, "a socket's face laid flush on the cheek's outer plane: coplanar with the plate that hides it"),
            ("CannonRig._wheelMesh", 1, "the hub's inner face, where the axle stub goes in"),

            //---- twig bases the burial test cannot call inside; the logic tests hold where each one starts
            ("MeadowScatter._owned[6].Wood", 2, "a shrub twig's base ring on the thin end of a stem: the hexagonal stem leaves under 0.001 of the shrub's height to spare"),
            ("MeadowScatter._owned[8].Wood", 4, "a shrub twig's base ring on the thin end of a stem: the hexagonal stem leaves under 0.001 of the shrub's height to spare"),
            ("MeadowScatter._owned[10].Wood", 3, "a shrub twig's base ring on the thin end of a stem: the hexagonal stem leaves under 0.001 of the shrub's height to spare"),
            ("MeadowScatter._owned[4] {FineWood+Twigs}", 2, "the willow's twig bases on a fine branch's end, which arrives a few degrees off the coarse branch's direction"),

            //---- the spruces on High and Ultra (#960): every bough roots in the stem with its root ring left open, and the leader's foot is a rim round it
            ("{FineStem+FineCrown+FineSnow}", 10, "a bough's open 4-point root ring, inside the stem's circle (SpruceCrownTests) yet not wholly inside its eight-facet solid (up to nine a crown), and the leader's open 5-point foot, a rim round the stem (SpruceCrownGeometry.STEM_IN_LEADER)"),
            ("{FineStem+FineCrownFar+FineSnowFar}", 6, "the same on the far crown (up to five root rings and the leader's foot)"),
        };

        /// <summary>
        /// Parts that are one object, joined before they are judged: a pattern naming each member (group <c>key</c> says
        /// which object), and the label of the joined part. A part in no assembly is judged alone.
        /// </summary>
        private static readonly (Regex Member, string Label)[] Assemblies =
        {
            (new Regex(@"^(?<key>ArenaIsland\._islandMeshes\[\d+\])\.(Cap|Body) "), "Cap+Body"),
            (new Regex(@"^(?<key>[^ ]*(?: aurora)?\._coniferMeshes\[\d+\])\.(Trunk|Crown) "), "Trunk+Crown"),
            (new Regex(@"^(?<key>[^ ]*(?: aurora)?\._coniferMeshes\[\d+\])\.(FineStem|FineCrown|FineSnow) "), "FineStem+FineCrown+FineSnow"),
            (new Regex(@"^(?<key>[^ ]*(?: aurora)?\._coniferMeshes\[\d+\])\.(FineStem|FineCrownFar|FineSnowFar) "), "FineStem+FineCrownFar+FineSnowFar"),
            (new Regex(@"^(?<key>MeadowScatter\._owned\[[0-4]\])\.(Wood|Twigs) "), "Wood+Twigs"),
            (new Regex(@"^(?<key>MeadowScatter\._owned\[[0-4]\])\.(FineWood|Twigs) "), "FineWood+Twigs"),
            (new Regex(@"^(?<key>ForestDressing\._owned\[\d+\])\.(FineWood|Twigs) "), "FineWood+Twigs"),
            (new Regex(@"^(?<key>PartyHat)\._meshes\[[0-4]\] "), "stripes"),
        };

        /// <summary>Objects planted on the ground: their own y = 0 is the earth, so a foot ring sunk under it is buried.</summary>
        private static readonly string[] PlantedPrefixes =
        {
            "ForestScatterRenderer", "SavannaScatter", "MeadowScatter", "VolcanoPlanting", "MoonPlanting", "PalmMesh", "DeadwoodMesh",
            "RockMesh", "FoliageMesh", "GrassTuftMesh", "LatheMesh(moss",
        };

        private sealed record OpenVerdict(int Triangles, int OpenEdges, int Exposed, string Status, long Milliseconds);

        /// <summary>The part's path in the table's terms, and what became of it.</summary>
        private sealed record Judged(string Path, OpenVerdict Verdict, string Reason);

        private static VertexPositionNormalTexture[] Vertices(Part part)
        {
            var vertices = new VertexPositionNormalTexture[part.Positions.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new VertexPositionNormalTexture(part.Positions[i], part.Normals != null ? part.Normals[i] : Vector3.Zero, Vector2.Zero);
            return vertices;
        }

        private static List<MeshOpenness.OpenLoop> ExposedLoopsOf(Part part)
        {
            bool planted = PlantedPrefixes.Any(p => part.Path.StartsWith(p, StringComparison.Ordinal));
            return MeshOpenness.ExposedLoops(Vertices(part), part.Indices, planted ? SUNK : float.NegativeInfinity);
        }

        /// <summary>The parts as they are judged: assemblies joined, every other part as it is.</summary>
        private static List<Part> Assemble(List<Part> parts)
        {
            var joined = new List<Part>();
            var groups = new Dictionary<(int, string), List<Part>>();
            var inGroup = new HashSet<Part>();
            foreach (Part part in parts)
                for (int a = 0; a < Assemblies.Length; a++)
                {
                    Match m = Assemblies[a].Member.Match(part.Path);
                    if (!m.Success) continue;
                    var key = (a, m.Groups["key"].Value);
                    if (!groups.TryGetValue(key, out List<Part> list)) groups[key] = list = new List<Part>();
                    list.Add(part);
                    inGroup.Add(part);
                }

            foreach (Part part in parts)
                if (!inGroup.Contains(part)) joined.Add(part);

            foreach (((int a, string key), List<Part> members) in groups)
            {
                var positions = new List<Vector3>();
                var normals = new List<Vector3>();
                var indices = new List<int>();
                foreach (Part member in members)
                {
                    int offset = positions.Count;
                    positions.AddRange(member.Positions);
                    for (int i = 0; i < member.Positions.Length; i++) normals.Add(member.Normals != null ? member.Normals[i] : Vector3.Zero);
                    indices.AddRange(member.Indices.Select(i => i + offset));
                }
                joined.Add(new Part
                {
                    Path = $"{key} {{{Assemblies[a].Label}}}",
                    Positions = positions.ToArray(), Normals = normals.ToArray(), Indices = indices.ToArray()
                });
            }
            return joined;
        }

        /// <summary>One part against the table: its counts, and whether the gate lets it stand.</summary>
        private static Judged JudgeOpenness(Part part)
        {
            var clock = Stopwatch.StartNew();
            var vertices = Vertices(part);
            int open = MeshOpenness.OpenEdges(vertices, part.Indices).Count;

            (string PathPart, int Max, string Reason) allowance = Allowed.FirstOrDefault(e => part.Path.Contains(e.PathPart, StringComparison.Ordinal));
            if (open == 0)
                return new Judged(part.Path, new OpenVerdict(part.Indices.Length / 3, 0, 0, "shut", clock.ElapsedMilliseconds), null);
            bool cards = allowance.Max == ANY || (allowance.Max == CARDS && open >= part.Indices.Length / 3);
            if (allowance.PathPart != null && cards)
                return new Judged(part.Path, new OpenVerdict(part.Indices.Length / 3, open, -1, "open on purpose", clock.ElapsedMilliseconds), allowance.Reason);

            int exposed = ExposedLoopsOf(part).Count;
            int allowed = allowance.PathPart == null || allowance.Max == CARDS ? 0 : allowance.Max;
            string status = exposed == 0 ? "shut"
                : allowed == 0 ? "OPEN"
                : exposed <= allowed ? "open on purpose"
                : "OPEN (more than the " + allowed + " allowed)";
            return new Judged(part.Path, new OpenVerdict(part.Indices.Length / 3, open, exposed, status, clock.ElapsedMilliseconds),
                status == "open on purpose" ? allowance.Reason : null);
        }

        private static int Openness(List<Part> parts, bool verbose, string detail, bool plant)
        {
            if (plant) parts = parts.Concat(new[] { PlantedOpenBox() }).ToList();

            List<Part> judged = Assemble(parts);
            var results = judged.Select(JudgeOpenness).ToList();

            Console.WriteLine($"{"part",-76} {"tris",7} {"open edges",11} {"rings",6}  verdict");
            int findings = 0, purposeful = 0, shut = 0;
            for (int i = 0; i < results.Count; i++)
            {
                Judged r = results[i];
                bool open = r.Verdict.Status.StartsWith("OPEN", StringComparison.Ordinal);
                if (open) findings++;
                else if (r.Verdict.Status == "open on purpose") purposeful++;
                else shut++;

                if (verbose || r.Verdict.Status != "shut")
                    Console.WriteLine($"{Trim(r.Path, 76),-76} {r.Verdict.Triangles,7} {r.Verdict.OpenEdges,11} " +
                        $"{(r.Verdict.Exposed < 0 ? "-" : r.Verdict.Exposed.ToString(CultureInfo.InvariantCulture)),6}  {r.Verdict.Status}" +
                        (r.Reason != null ? ": " + r.Reason : ""));
                if (detail != null && r.Path.Contains(detail, StringComparison.Ordinal)) Loops(judged[i]);
            }

            foreach ((string pathPart, _, _) in Allowed)
                if (!judged.Any(p => p.Path.Contains(pathPart, StringComparison.Ordinal)))
                    Console.WriteLine($"note: nothing is called \"{pathPart}\" any more; its row in Allowed can go");

            Console.WriteLine();
            Console.WriteLine($"{results.Count} meshes judged ({parts.Count} parts), {results.Sum(r => r.Verdict.Triangles)} triangles: " +
                $"{shut} shut, {purposeful} open on purpose, {findings} OPEN." + (verbose ? "" : " (--verbose lists the shut ones too)"));
            return findings == 0 ? 0 : 1;
        }

        /// <summary><c>--detail &lt;text&gt;</c> with <c>--openness</c>: every exposed ring of a part whose path contains the text.</summary>
        private static void Loops(Part part)
        {
            foreach (MeshOpenness.OpenLoop loop in ExposedLoopsOf(part))
            {
                Vector3 min = new(float.MaxValue), max = new(float.MinValue);
                foreach (Vector3 p in loop.Points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                Console.WriteLine($"    ring of {loop.Points.Count,4} at ({loop.Centre.X:0.000}, {loop.Centre.Y:0.000}, {loop.Centre.Z:0.000}) " +
                    $"size ({max.X - min.X:0.000}, {max.Y - min.Y:0.000}, {max.Z - min.Z:0.000})");
                if (loop.Points.Count <= 24)
                    foreach (Vector3 p in loop.Points.OrderBy(q => q.Z).ThenBy(q => q.Y))
                        Console.WriteLine($"        ({p.X:0.0000}, {p.Y:0.0000}, {p.Z:0.0000})");
            }
        }

        /// <summary>A unit box with the two triangles of its top face left out: one open ring, in the middle of nowhere.</summary>
        private static Part PlantedOpenBox()
        {
            Part box = BoxPart("PLANTED open box (--plant-open)");
            box.Indices = box.Indices.Take(box.Indices.Length - 6).ToArray();
            return box;
        }

        /// <summary>The 12 triangles of a unit cube, welded at its eight corners.</summary>
        private static Part BoxPart(string path)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++) corners[i] = new Vector3((i & 1) * 2f - 1f, ((i >> 1) & 1) * 2f - 1f, ((i >> 2) & 1) * 2f - 1f) * 0.5f;
            int[] quads =
            {
                0, 1, 3, 2,  4, 6, 7, 5,  0, 2, 6, 4,  1, 5, 7, 3,  0, 4, 5, 1,  2, 3, 7, 6
            };
            var indices = new List<int>();
            for (int q = 0; q < 6; q++)
            {
                int a = quads[q * 4], b = quads[q * 4 + 1], c = quads[q * 4 + 2], d = quads[q * 4 + 3];
                indices.AddRange(new[] { a, b, c, a, c, d });
            }
            return new Part { Path = path, Positions = corners, Normals = null, Indices = indices.ToArray() };
        }

        /// <summary>
        /// BestPractices section 10 for the openness gate: the real judging code is fed a shut box (must pass), the same box
        /// with a face left out (must be refused), the same open box under a name the table lists (must stand) and a tube
        /// with one end inside a box and the other free (must be refused for the free end alone).
        /// </summary>
        private static int OpennessSelfTest(GraphicsDevice device)
        {
            Part shut = BoxPart("SELFTEST shut box");
            Part hole = BoxPart("SELFTEST open box");
            hole.Indices = hole.Indices.Take(hole.Indices.Length - 6).ToArray();

            Judged a = JudgeOpenness(shut), b = JudgeOpenness(hole);

            //A tube whose one end lies inside a box and whose other end is free: one exposed ring, not two
            Part buried = BoxPart("SELFTEST tube in a box");
            var positions = new List<Vector3>(buried.Positions);
            var indices = new List<int>(buried.Indices);
            const int SIDES = 8;
            int ring0 = positions.Count;
            for (int s = 0; s < SIDES; s++)
                positions.Add(new Vector3(MathF.Cos(MathHelper.TwoPi * s / SIDES) * 0.1f, 0.1f * MathF.Sin(MathHelper.TwoPi * s / SIDES), 0f));
            int ring1 = positions.Count;
            for (int s = 0; s < SIDES; s++)
                positions.Add(new Vector3(MathF.Cos(MathHelper.TwoPi * s / SIDES) * 0.1f, 0.1f * MathF.Sin(MathHelper.TwoPi * s / SIDES), 2f));
            for (int s = 0; s < SIDES; s++)
            {
                int n = (s + 1) % SIDES;
                indices.AddRange(new[] { ring0 + s, ring1 + s, ring0 + n, ring0 + n, ring1 + s, ring1 + n });
            }
            buried.Positions = positions.ToArray();
            buried.Indices = indices.ToArray();
            Judged c = JudgeOpenness(buried);

            Part listedHole = BoxPart("SELFTEST FunnelMesh stand-in");
            listedHole.Indices = listedHole.Indices.Take(listedHole.Indices.Length - 6).ToArray();
            Judged d = JudgeOpenness(listedHole);

            Console.WriteLine($"shut box:               {a.Verdict.Status} ({a.Verdict.Exposed} rings)");
            Console.WriteLine($"box, a face missing:    {b.Verdict.Status} ({b.Verdict.Exposed} rings)");
            Console.WriteLine($"tube, one end in a box: {c.Verdict.Status} ({c.Verdict.Exposed} rings)");
            Console.WriteLine($"open box named in the table: {d.Verdict.Status}");

            bool pass = a.Verdict.Status == "shut" && b.Verdict.Status == "OPEN" && b.Verdict.Exposed == 1
                && c.Verdict.Status == "OPEN" && c.Verdict.Exposed == 1
                && d.Verdict.Status == "open on purpose";
            Console.WriteLine(pass ? "selftest: the openness gate refuses an open mesh, and sees a buried end and a listed mesh for what they are"
                : "selftest: the openness gate FAILED");
            return pass ? 0 : 1;
        }
    }
}
