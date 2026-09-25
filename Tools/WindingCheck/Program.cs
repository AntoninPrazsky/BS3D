using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core;
using Prazsky.Core.Render;

namespace BS3D.Tools.WindingCheck
{
    /// <summary>
    /// Proves the triangle winding rule (CLAUDE.md "Triangle winding": procedural meshes wind <b>clockwise seen
    /// from outside</b>) mesh by mesh, without looking at a frame (#400).
    /// <para>
    /// It runs the meshes' <b>own constructors</b> — through the objects that own them where those can be built
    /// (the cannon rig, the island, the ceiling plate, the ball set, the forest and savanna scatters), directly
    /// otherwise — on a WARP device (DirectX's software rasteriser: no GPU, no window shown), reads every
    /// vertex and index buffer back, and judges every triangle two independent ways:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Against its own normals.</b> Clockwise from outside means the geometric normal
    /// <c>(b - a) × (c - a)</c> points <i>inward</i>, so against the outward vertex normals the lighting uses it
    /// must come out negative. A triangle where it comes out positive is wound backwards — or its normals are
    /// flipped, which is a shading defect of its own. Works on open sheets too.</item>
    /// <item><b>By signed volume</b>, per welded connected piece that is closed (every edge shared by exactly
    /// two triangles): a correctly wound closed shell encloses a <i>negative</i> volume. This one does not
    /// trust the normals at all, so it catches a piece wound backwards whose normals were flipped to match —
    /// the case that shades right from one side and is culled from the other.</item>
    /// </list>
    /// <para>
    /// A hand-run check, not a gate: a few parts are meant to fail it — the sky dome keeps its captured order
    /// on purpose (a dome is seen from inside), the editor's wire box is wound inward on purpose, and the
    /// drain's gold rims are ribbons drawn <c>CullNone</c> with nothing reading their facing. Those are listed in
    /// <see cref="Expected"/> with the reason, and still printed. The other <c>CullNone</c> sheets (palm fronds,
    /// bird wings, the drain's cone and pit) pass it anyway — and the cone and pit must, since
    /// <c>TwoSidedNormals</c> reads <c>SV_IsFrontFace</c>. Exit 0 always; <c>--selftest</c> feeds it one
    /// mesh flipped on purpose and exits 1 unless both judgements fire on it.
    /// </para>
    /// <para>
    /// Not covered: the scene grids and heightfields <c>SceneRenderer</c> fills straight into buffers (drawn
    /// <c>CullNone</c>, "the winding is moot on a heightfield"), and the Game's particle quads
    /// (<c>Blasts</c>, <c>Fireworks</c>, <c>Confetti</c>, … all <c>CullNone</c>).
    /// </para>
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// A triangle counts as disagreeing with its normals only past this cosine. Measured (2026-09-25): every
        /// part wound backwards sits at or near 1, and the only triangles between 0.05 and 0.18 are half of each
        /// quad in the baobab's foot flare, where the smoothed normals lean across the crease into the ground
        /// (<c>--detail Wood</c> shows them). Those shade softly, not backwards, and are not reported.
        /// </summary>
        private const float AGREEMENT_COS = 0.25f;

        /// <summary>Positions closer than this are welded when the pieces and the closed edges are found — the
        /// meshes split vertices at every crease and seam.</summary>
        private const float WELD = 1e-4f;

        /// <summary>
        /// Parts that are allowed to fail, by a substring of their path, with the reason. Everything else that
        /// fails is a finding.
        /// </summary>
        private static readonly (string PathPart, string Reason)[] Expected =
        {
            ("SkyDome", "the dome keeps its captured order on purpose: seen from inside (CLAUDE.md)"),
            ("WireBoxMesh", "wound inward on purpose: its faces front the camera inside the outline (its AddBox doc)"),
            ("FunnelRimsMesh", "zero-thickness ribbons drawn CullNone without TwoSidedNormals: no front face is read"),
        };

        private sealed class Part
        {
            public string Path;
            public Vector3[] Positions;
            public Vector3[] Normals;   //null when the declaration has none
            public int[] Indices;
        }

        private sealed class Verdict
        {
            public int Triangles, Degenerate, AgreeInward, AgreeOutward, Undecided;
            public int Pieces, ClosedPieces, ClosedNegative, ClosedPositive, InconsistentEdges;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            bool selfTest = args.Contains("--selftest");
            bool verbose = args.Contains("--verbose");
            int detailAt = Array.IndexOf(args, "--detail");
            string detail = detailAt >= 0 && detailAt + 1 < args.Length ? args[detailAt + 1] : null;

            using var form = new Form();   //never shown - DirectX only wants a window handle to hang the device on
            GraphicsAdapter.UseDriverType = GraphicsAdapter.DriverType.FastSoftware;
            var pp = new PresentationParameters
            {
                BackBufferWidth = 64, BackBufferHeight = 64, DeviceWindowHandle = form.Handle, IsFullScreen = false
            };
            using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, pp);

            if (selfTest) return SelfTest(device);

            string root = FindRepositoryRoot();
            var services = new GameServiceContainer();
            services.AddService(typeof(IGraphicsDeviceService), new DeviceService(device));
            string contentDir = Path.Combine(root, "Testbed", "bin", "net10.0-windows", "Content");
            if (!Directory.Exists(contentDir))
            {
                Console.Error.WriteLine($"No compiled Testbed content at {contentDir} - build Testbed.sln first.");
                return 2;
            }
            var content = new ContentManager(services, contentDir);
            Effect instancing = content.Load<Effect>("Shaders/InstancedModel");

            var parts = new List<Part>();
            foreach ((string name, Func<object> make) in Sources(device, instancing))
            {
                object owner;
                try { owner = make(); }
                catch (Exception e)
                {
                    Console.WriteLine($"  !! {name}: could not be built here ({e.GetType().Name}: {e.Message})");
                    continue;
                }
                Collect(owner, name, parts, new HashSet<object>(ReferenceEqualityComparer.Instance), device);
            }

            //The same buffer reached twice (a mesh shared by two owners) is judged once.
            int findings = 0, expected = 0;
            Console.WriteLine($"{"part",-72} {"tris",6} {"inward",7} {"OUTWARD",8} {"closed",7} {"vol<0",6} {"VOL>0",6}  verdict");
            foreach (Part part in parts)
            {
                Verdict v = Judge(part);
                bool normalsWrong = v.AgreeOutward > 0;
                bool volumeWrong = v.ClosedPositive > 0;
                string reason = Expected.FirstOrDefault(e => part.Path.Contains(e.PathPart)).Reason;
                string verdict = !normalsWrong && !volumeWrong ? "ok"
                    : reason != null ? "expected: " + reason
                    : "BACKWARDS";
                if (verdict == "BACKWARDS") findings++;
                else if (verdict != "ok") expected++;
                if (verbose || verdict != "ok")
                {
                    Console.WriteLine($"{Trim(part.Path, 72),-72} {v.Triangles,6} {v.AgreeInward,7} {v.AgreeOutward,8} " +
                        $"{v.ClosedPieces + "/" + v.Pieces,7} {v.ClosedNegative,6} {v.ClosedPositive,6}  {verdict}" +
                        (part.Normals == null ? " (no normals)" : ""));
                }
                if (detail != null && part.Path.Contains(detail, StringComparison.Ordinal)) Detail(part);
            }
            Console.WriteLine();
            Console.WriteLine($"{parts.Count} parts judged, {parts.Sum(p => p.Indices.Length / 3)} triangles: " +
                $"{findings} wound backwards, {expected} expected exceptions." + (verbose ? "" : " (--verbose lists the ok parts too)"));
            return 0;
        }

        /// <summary>
        /// Every mesh the three executables build, by the object that builds it. Owners are preferred, so the
        /// parameters are the real ones; the meshes built inside <c>SceneRenderer</c>, <c>CityRooftops</c> and the
        /// Game's trophy podium are constructed directly with their call sites' figures, since their owners need
        /// a whole scene.
        /// </summary>
        private static IEnumerable<(string, Func<object>)> Sources(GraphicsDevice d, Effect fx)
        {
            yield return ("CannonRig", () => new CannonRig(d, fx, 5, 1f));
            yield return ("ArenaIsland", () => new ArenaIsland(d, fx, 0.3f));
            yield return ("CeilingPlate", () => { var p = new CeilingPlate(d, fx); p.Fit(7f, 7f); return p; });
            yield return ("BallRenderSet", () => new BallRenderSet(d, fx));
            yield return ("ForestScatterRenderer", () => new ForestScatterRenderer(d, fx, new ForestSceneConfig(), 0.3f));
            yield return ("ForestFireflies", () => new ForestFireflies(d, fx, new ForestSceneConfig(), 0.3f));
            yield return ("SavannaScatter", () => new SavannaScatter(d, new SavannaSceneConfig(), (x, z) => 0f,
                Array.Empty<ScatterSpacing.Footprint>()));
            for (int dome = 1; dome <= 20; dome++)
            {
                int n = dome;
                yield return ($"SkyDome[{n}]", () => new SkyDome(d, n));
            }

            //SceneRenderer's own meshes, at its call sites' figures (TropicalSceneConfig's defaults).
            var tropical = new TropicalSceneConfig();
            for (int m = 0; m < 4; m++)
            {
                int s = m;
                yield return ($"PalmMesh[{s}]", () => new PalmMesh(d, tropical.Palms.TrunkRadius, tropical.Palms.Height,
                    tropical.Palms.FrondLength, 6100 + s));
            }
            yield return ("BirdMesh", () => new BirdMesh(d));
            yield return ("FoliageMesh(scrub)", () => new FoliageMesh(d, 1.2f, 1f, 0.8f, 6200, FoliageStyle.Scrub));
            yield return ("GrassTuftMesh", () => new GrassTuftMesh(d, 0.5f, 0.8f, 6230));
            yield return ("DeadwoodMesh", () => new DeadwoodMesh(d, 3f, 0.25f, 6260));
            yield return ("RockMesh(hearth)", () => new RockMesh(d, radius: 0.4f, height: 0.3f, irregularityPhase: 1.7f));
            yield return ("LatheMesh(moss cap)", () => MossCap(d, 1.5f, 1f, 0.57f));
            yield return ("SphereMesh(beacon)", () => new SphereMesh(d, 1f, 10, 6));

            //CityRooftops' props and the Game's own meshes.
            yield return ("RooftopMesh.Mast", () => RooftopMesh.CreateMast(d, true));
            yield return ("RooftopMesh.Dish", () => RooftopMesh.CreateDish(d, 0.4f));
            yield return ("RooftopMesh.DishRing", () => RooftopMesh.CreateDishRing(d, 0.4f));
            yield return ("RooftopMesh.SectorPole", () => RooftopMesh.CreateSectorPole(d));
            yield return ("RooftopMesh.Hvac", () => RooftopMesh.CreateHvac(d));
            yield return ("BoxMesh(unit)", () => new BoxMesh(d, 1f, 1f, 1f));
            yield return ("WireBoxMesh(editor AABB)", () => new WireBoxMesh(d, 4f, 3f, 2f, 0.05f));
            yield return ("TrophyMesh(plain)", () => new TrophyMesh(d, handles: false));
            yield return ("TrophyMesh(handles)", () => new TrophyMesh(d, handles: true));
            for (char c = ' '; c < (char)0x180; c++)
            {
                if (c == ' ' || !LetterShapes.Supports(c)) continue;
                char ch = c;
                yield return ($"LetterMesh['{ch}']", () => new LetterMesh(d, ch, 0.12f));
            }
        }

        /// <summary>SceneRenderer.BuildMossCap's profile, which is private there.</summary>
        private static LatheMesh MossCap(GraphicsDevice d, float radius, float height, float phase)
        {
            float capRadius = radius * 0.84f, crownY = height * 1.04f, rimY = height * 0.45f;
            var profile = new List<LathePoint>
            {
                new(0f, crownY, crease: true),
                new(capRadius * 0.34f, crownY, wobble: 1f),
                new(capRadius * 0.66f, rimY + (crownY - rimY) * 0.55f, wobble: 1f),
                new(capRadius, rimY, crease: true, wobble: 1f),
                new(capRadius * 0.74f, rimY - 0.18f, wobble: 1f),
                new(0f, rimY - 0.18f)
            };
            return new LatheMesh(d, profile, 16, irregularityAmplitude: capRadius * 0.30f, irregularityPhase: phase);
        }

        private static readonly HashSet<VertexBuffer> Seen = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Walks an owner's fields for meshes: anything that is an <see cref="IProceduralMesh"/>, and any object
        /// holding one index buffer beside a vertex buffer with positions (the sky dome keeps its own). Recurses
        /// into this repository's own types and into arrays, lists and dictionaries of them.
        /// </summary>
        private static void Collect(object o, string path, List<Part> parts, HashSet<object> visited, GraphicsDevice device)
        {
            if (o == null || !visited.Add(o)) return;
            Type t = o.GetType();

            if (o is IProceduralMesh mesh && mesh.VertexBuffer != null && mesh.IndexBuffer != null)
                Add(parts, $"{path} <{t.Name}>", mesh.VertexBuffer, mesh.IndexBuffer);

            if (o is IEnumerable seq && o is not string)
            {
                int i = 0;
                foreach (object item in seq)
                {
                    object value = item;
                    string key = i.ToString(CultureInfo.InvariantCulture);
                    if (item != null && item.GetType().IsGenericType && item.GetType().GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    {
                        key = item.GetType().GetProperty("Key").GetValue(item)?.ToString();
                        value = item.GetType().GetProperty("Value").GetValue(item);
                    }
                    if (value != null && IsOurs(value.GetType())) Collect(value, $"{path}[{key}]", parts, visited, device);
                    i++;
                }
                return;
            }

            if (!IsOurs(t)) return;

            var fields = new List<FieldInfo>();
            for (Type walk = t; walk != null && walk != typeof(object); walk = walk.BaseType)
                fields.AddRange(walk.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

            if (o is not IProceduralMesh)
            {
                var vbs = fields.Where(f => f.FieldType == typeof(VertexBuffer)).Select(f => (VertexBuffer)f.GetValue(o))
                    .Where(vb => vb != null && HasPosition(vb)).ToList();
                var ibs = fields.Where(f => f.FieldType == typeof(IndexBuffer)).Select(f => (IndexBuffer)f.GetValue(o))
                    .Where(ib => ib != null).ToList();
                if (vbs.Count == 1 && ibs.Count == 1) Add(parts, path, vbs[0], ibs[0]);
            }

            foreach (FieldInfo f in fields)
            {
                object value = f.GetValue(o);
                if (value == null || value is GraphicsResource || value is Delegate) continue;
                Type ft = value.GetType();
                bool container = value is IEnumerable && value is not string;
                if (!container && !IsOurs(ft)) continue;
                string name = f.Name.StartsWith("<") ? f.Name.Substring(1, f.Name.IndexOf('>') - 1) : f.Name;
                Collect(value, $"{path}.{name}", parts, visited, device);
            }
        }

        private static bool IsOurs(Type t) =>
            t.Namespace != null && t.Namespace.StartsWith("Prazsky", StringComparison.Ordinal) && !t.IsEnum && !t.IsPrimitive;

        private static bool HasPosition(VertexBuffer vb) =>
            vb.VertexDeclaration.GetVertexElements().Any(e => e.VertexElementUsage == VertexElementUsage.Position && e.UsageIndex == 0);

        private static void Add(List<Part> parts, string path, VertexBuffer vb, IndexBuffer ib)
        {
            if (!Seen.Add(vb)) return;
            parts.Add(Read(path, vb, ib));
        }

        /// <summary>Reads a buffer pair back into positions, normals (when the declaration has a float3 one) and indices.</summary>
        private static Part Read(string path, VertexBuffer vb, IndexBuffer ib)
        {
            AllowReadBack(vb);
            AllowReadBack(ib);
            VertexDeclaration decl = vb.VertexDeclaration;
            int stride = decl.VertexStride;
            var raw = new byte[vb.VertexCount * stride];
            vb.GetData(0, raw, 0, raw.Length, 1);
            VertexElement[] elements = decl.GetVertexElements();
            VertexElement pos = elements.First(e => e.VertexElementUsage == VertexElementUsage.Position && e.UsageIndex == 0);
            VertexElement? nrm = elements.Cast<VertexElement?>().FirstOrDefault(e =>
                e.Value.VertexElementUsage == VertexElementUsage.Normal && e.Value.UsageIndex == 0 &&
                e.Value.VertexElementFormat == VertexElementFormat.Vector3);

            var positions = new Vector3[vb.VertexCount];
            var normals = nrm.HasValue ? new Vector3[vb.VertexCount] : null;
            for (int i = 0; i < vb.VertexCount; i++)
            {
                positions[i] = V3(raw, i * stride + pos.Offset);
                if (normals != null) normals[i] = V3(raw, i * stride + nrm.Value.Offset);
            }

            int[] indices;
            if (ib.IndexElementSize == IndexElementSize.SixteenBits)
            {
                var s = new short[ib.IndexCount];
                ib.GetData(s);
                indices = s.Select(x => (int)(ushort)x).ToArray();
            }
            else
            {
                indices = new int[ib.IndexCount];
                ib.GetData(indices);
            }
            return new Part { Path = path, Positions = positions, Normals = normals, Indices = indices };
        }

        /// <summary>
        /// Every mesh here is uploaded <see cref="BufferUsage.WriteOnly"/>, and MonoGame refuses <c>GetData</c> on
        /// such a buffer with a managed check of its own; the DirectX read-back underneath (a staging copy) does
        /// not care. So the tool flips the managed flag on the buffers it reads — never in the game.
        /// </summary>
        private static void AllowReadBack(GraphicsResource buffer)
        {
            for (Type t = buffer.GetType(); t != null; t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    if (f.FieldType == typeof(BufferUsage)) f.SetValue(buffer, BufferUsage.None);
            }
        }

        private static Vector3 V3(byte[] raw, int at) =>
            new(BitConverter.ToSingle(raw, at), BitConverter.ToSingle(raw, at + 4), BitConverter.ToSingle(raw, at + 8));

        /// <summary>
        /// <c>--detail &lt;text&gt;</c>: every triangle of a part whose path contains the text that disagrees with
        /// its normals, with the cosine and where it is - to tell a whole piece wound backwards from a few
        /// slivers whose averaged normals lean across a crease.
        /// </summary>
        private static void Detail(Part p)
        {
            for (int t = 0; t < p.Indices.Length / 3; t++)
            {
                int ia = p.Indices[3 * t], ib = p.Indices[3 * t + 1], ic = p.Indices[3 * t + 2];
                Vector3 a = p.Positions[ia], b = p.Positions[ib], c = p.Positions[ic];
                Vector3 g = Vector3.Cross(b - a, c - a);
                if (g.LengthSquared() < 1e-14f || p.Normals == null) continue;
                Vector3 n = p.Normals[ia] + p.Normals[ib] + p.Normals[ic];
                if (n.LengthSquared() < 1e-12f) continue;
                float cos = Vector3.Dot(Vector3.Normalize(g), Vector3.Normalize(n));
                if (cos <= AGREEMENT_COS) continue;
                Vector3 m = (a + b + c) / 3f;
                Console.WriteLine($"    tri {t,6} (v {ia},{ib},{ic})  cos {cos,6:0.000}  area {g.Length() * 0.5f,9:0.00000}  at ({m.X:0.000}, {m.Y:0.000}, {m.Z:0.000})");
            }
        }

        private static Verdict Judge(Part p)
        {
            var v = new Verdict();
            int triangles = p.Indices.Length / 3;
            v.Triangles = triangles;

            //Weld positions so pieces and closed edges are found across split seams.
            var weld = new Dictionary<(long, long, long), int>();
            var welded = new int[p.Positions.Length];
            for (int i = 0; i < p.Positions.Length; i++)
            {
                Vector3 q = p.Positions[i] / WELD;
                var key = ((long)MathF.Round(q.X), (long)MathF.Round(q.Y), (long)MathF.Round(q.Z));
                if (!weld.TryGetValue(key, out int id)) weld[key] = id = weld.Count;
                welded[i] = id;
            }

            var parent = Enumerable.Range(0, weld.Count).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }

            var directed = new Dictionary<(int, int), int>();
            var live = new List<int>();
            for (int t = 0; t < triangles; t++)
            {
                int ia = p.Indices[3 * t], ib = p.Indices[3 * t + 1], ic = p.Indices[3 * t + 2];
                Vector3 a = p.Positions[ia], b = p.Positions[ib], c = p.Positions[ic];
                Vector3 g = Vector3.Cross(b - a, c - a);
                int wa = welded[ia], wb = welded[ib], wc = welded[ic];
                if (g.LengthSquared() < 1e-14f || wa == wb || wb == wc || wa == wc) { v.Degenerate++; continue; }
                live.Add(t);
                g.Normalize();

                if (p.Normals != null)
                {
                    Vector3 n = p.Normals[ia] + p.Normals[ib] + p.Normals[ic];
                    if (n.LengthSquared() < 1e-12f) v.Undecided++;
                    else
                    {
                        float cos = Vector3.Dot(g, Vector3.Normalize(n));
                        if (cos < -AGREEMENT_COS) v.AgreeInward++;
                        else if (cos > AGREEMENT_COS) v.AgreeOutward++;
                        else v.Undecided++;
                    }
                }

                parent[Find(wa)] = Find(wb);
                parent[Find(wb)] = Find(wc);
                foreach ((int x, int y) in new[] { (wa, wb), (wb, wc), (wc, wa) })
                    directed[(x, y)] = directed.GetValueOrDefault((x, y)) + 1;
            }

            //Per piece: closed when every directed edge appears exactly once and its reverse exactly once.
            var pieceTris = live.GroupBy(t => Find(welded[p.Indices[3 * t]])).ToList();
            v.Pieces = pieceTris.Count;
            foreach (var piece in pieceTris)
            {
                bool closed = true;
                double volume = 0;
                foreach (int t in piece)
                {
                    int wa = welded[p.Indices[3 * t]], wb = welded[p.Indices[3 * t + 1]], wc = welded[p.Indices[3 * t + 2]];
                    foreach ((int x, int y) in new[] { (wa, wb), (wb, wc), (wc, wa) })
                    {
                        int forward = directed[(x, y)], back = directed.GetValueOrDefault((y, x));
                        if (forward != 1 || back != 1) closed = false;
                        if (forward > 1) v.InconsistentEdges++;
                    }
                    Vector3 a = p.Positions[p.Indices[3 * t]], b = p.Positions[p.Indices[3 * t + 1]], c = p.Positions[p.Indices[3 * t + 2]];
                    volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                }
                if (!closed) continue;
                v.ClosedPieces++;
                if (volume < 0) v.ClosedNegative++;
                else if (volume > 0) v.ClosedPositive++;
            }
            return v;
        }

        /// <summary>
        /// BestPractices §10: a check's answer is evidence only once its failing branch has been seen to fire.
        /// A unit box (closed, outward normals) is judged as built, then with every triangle's winding reversed
        /// (normals untouched), then with the winding AND the normals reversed — the last is the case only the
        /// signed volume can see.
        /// </summary>
        private static int SelfTest(GraphicsDevice device)
        {
            using var box = new BoxMesh(device, 1f, 1f, 1f);
            Part asBuilt = Read("BoxMesh", box.VertexBuffer, box.IndexBuffer);

            Part flipped = Clone(asBuilt, "BoxMesh, winding reversed");
            for (int i = 0; i < flipped.Indices.Length; i += 3)
                (flipped.Indices[i + 1], flipped.Indices[i + 2]) = (flipped.Indices[i + 2], flipped.Indices[i + 1]);

            Part both = Clone(flipped, "BoxMesh, winding and normals reversed");
            for (int i = 0; i < both.Normals.Length; i++) both.Normals[i] = -both.Normals[i];

            Verdict a = Judge(asBuilt), b = Judge(flipped), c = Judge(both);
            Console.WriteLine($"as built:              inward {a.AgreeInward}, outward {a.AgreeOutward}, closed {a.ClosedPieces}/{a.Pieces}, vol<0 {a.ClosedNegative}, vol>0 {a.ClosedPositive}");
            Console.WriteLine($"winding reversed:      inward {b.AgreeInward}, outward {b.AgreeOutward}, closed {b.ClosedPieces}/{b.Pieces}, vol<0 {b.ClosedNegative}, vol>0 {b.ClosedPositive}");
            Console.WriteLine($"winding+normals rev.:  inward {c.AgreeInward}, outward {c.AgreeOutward}, closed {c.ClosedPieces}/{c.Pieces}, vol<0 {c.ClosedNegative}, vol>0 {c.ClosedPositive}");

            bool pass = a.AgreeOutward == 0 && a.ClosedPositive == 0 && a.ClosedNegative > 0
                && b.AgreeOutward == b.Triangles && b.ClosedPositive > 0
                && c.AgreeOutward == 0 && c.ClosedPositive > 0;
            Console.WriteLine(pass ? "selftest: both judgements fire on a flipped box" : "selftest: FAILED");
            return pass ? 0 : 1;
        }

        private static Part Clone(Part p, string path) => new()
        {
            Path = path,
            Positions = (Vector3[])p.Positions.Clone(),
            Normals = (Vector3[])p.Normals?.Clone(),
            Indices = (int[])p.Indices.Clone()
        };

        private static string Trim(string s, int n) => s.Length <= n ? s : "…" + s.Substring(s.Length - n + 1);

        private static string FindRepositoryRoot()
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Game.sln")) && Directory.Exists(Path.Combine(dir.FullName, "docs")))
                    return dir.FullName;
            throw new InvalidOperationException("Could not find the repository root above " + AppContext.BaseDirectory);
        }

        private sealed class DeviceService : IGraphicsDeviceService
        {
            public DeviceService(GraphicsDevice device) => GraphicsDevice = device;
            public GraphicsDevice GraphicsDevice { get; }
#pragma warning disable CS0067
            public event EventHandler<EventArgs> DeviceCreated, DeviceDisposing, DeviceReset, DeviceResetting;
#pragma warning restore CS0067
        }
    }
}
