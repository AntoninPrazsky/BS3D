using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.Core.Render;

namespace BS3D.Tools.WindingCheck
{
    /// <summary>
    /// <c>--export-cannon &lt;dir&gt;</c> (#949): the game's own cannon for the landing page's WebGL canvas, written as
    /// <c>cannon.bin</c> and <c>cannon.json</c>. The meshes are the rig's own (<see cref="CannonRig"/>, built here through its
    /// constructor on the WARP device the winding check already has, and read back as the check reads them); every figure that
    /// joins them (the wheels' radius, track and drop under the trunnions, the rollers' seats on a wheel, the recoil's stroke,
    /// the parts' colours, the ball colours) is read off the game's own constants, so no number is copied by hand.
    /// <para>
    /// <b>The binary</b>: every part's vertices, then every part's indices. A vertex is twelve bytes: the position as three
    /// signed 16-bit integers over <c>positionScale</c> and a zero, then the normal as three signed bytes over 127 and a zero,
    /// both read by WebGL as normalised integers. The indices are 16-bit, a part's counted from its own first vertex.
    /// Matrices are the game's (row vectors, the translation in the fourth row), sixteen numbers in row order: a WebGL
    /// program that uploads them as they are and multiplies <c>matrix * vector</c> composes them exactly as the game does.
    /// </para>
    /// </summary>
    internal static partial class Program
    {
        private static int ExportCannon(GraphicsDevice device, Effect effect, string directory)
        {
            //The game's own magazine: five rounds a unit apart, as both executables build it
            const int MAGAZINE = 5;
            const float SPACING = 1f;
            using var rig = new CannonRig(device, effect, MAGAZINE, SPACING);

            //The rig's meshes by the fields that hold them, in the order the page draws them
            (string Name, string Field)[] parts =
            {
                ("barrel", "_mesh"), ("glass", "_glassMesh"), ("collar", "_collarMesh"),
                ("carriage", "_carriageMesh"), ("wheel", "_wheelMesh"), ("roller", "_rollerMesh"),
            };

            var read = new List<(string Name, Part Part)>();
            foreach ((string name, string field) in parts)
            {
                object value = typeof(CannonRig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(rig);
                if (value is not IProceduralMesh mesh || mesh.VertexBuffer == null || mesh.IndexBuffer == null)
                {
                    Console.Error.WriteLine($"[export] CannonRig.{field} is not a mesh: the rig changed, and this list with it");
                    return 3;
                }
                Part part = Read(name, mesh.VertexBuffer, mesh.IndexBuffer);
                if (part.Normals == null || part.Positions.Length > ushort.MaxValue)
                {
                    Console.Error.WriteLine($"[export] {name}: {(part.Normals == null ? "no normals" : "more vertices than 16-bit indices reach")}");
                    return 3;
                }
                read.Add((name, part));
            }

            //One scale for every position, the widest coordinate of any part, so the page needs one figure
            float extent = read.SelectMany(p => p.Part.Positions).Max(v => MathF.Max(MathF.Abs(v.X), MathF.Max(MathF.Abs(v.Y), MathF.Abs(v.Z))));
            float scale = extent * 1.0001f;

            using var bin = new MemoryStream();
            using var writer = new BinaryWriter(bin);
            var entries = new Dictionary<string, object>();
            int vertexStart = 0;
            foreach ((string name, Part part) in read)
            {
                for (int i = 0; i < part.Positions.Length; i++)
                {
                    Vector3 p = part.Positions[i], n = part.Normals[i];
                    writer.Write(Quantise16(p.X / scale)); writer.Write(Quantise16(p.Y / scale)); writer.Write(Quantise16(p.Z / scale)); writer.Write((short)0);
                    writer.Write(Quantise8(n.X)); writer.Write(Quantise8(n.Y)); writer.Write(Quantise8(n.Z)); writer.Write((sbyte)0);
                }
                entries[name] = new Dictionary<string, int> { ["vertexStart"] = vertexStart, ["vertexCount"] = part.Positions.Length };
                vertexStart += part.Positions.Length;
            }
            int indexOffset = (int)bin.Length;
            int indexStart = 0;
            foreach ((string name, Part part) in read)
            {
                foreach (int i in part.Indices) writer.Write((ushort)i);
                var entry = (Dictionary<string, int>)entries[name];
                entry["indexStart"] = indexStart;
                entry["indexCount"] = part.Indices.Length;
                indexStart += part.Indices.Length;
            }
            writer.Flush();

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "cannon.bin"), bin.ToArray());

            //The figures that join the parts, off the rig's and the gun's own constants
            Matrix[] seats = (Matrix[])typeof(CannonRig).GetField("_rollerSeats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(rig);
            Part barrel = read.First(p => p.Name == "barrel").Part;
            var figures = new Dictionary<string, object>
            {
                ["wheelRadius"] = RigConstant("WHEEL_RADIUS"),
                ["wheelTrack"] = RigConstant("WHEEL_TRACK"),
                ["axleDrop"] = RigConstant("AXLE_DROP"),
                ["rollerRadius"] = RigConstant("ROLLER_RADIUS"),
                ["ballRadius"] = RigConstant("BALL_RADIUS"),
                ["muzzle"] = -barrel.Positions.Min(v => v.Z),
                ["magazine"] = MAGAZINE,
                ["magazineSpacing"] = SPACING,
                ["pivotToFrontBall"] = Round(rig.PivotToFrontBall),
                ["recoilBack"] = Cannon.RECOIL_BACK,
                ["recoilDecay"] = Cannon.RECOIL_DECAY,
                ["carriageRecoilBack"] = Cannon.CARRIAGE_RECOIL_BACK,
                ["rollerSeats"] = seats.Select(Row).ToArray(),
            };

            //Each part's colour (linear radiance, as the renderers take it), its sky reflection and its alpha
            var materials = new Dictionary<string, object>
            {
                ["barrel"] = Material(CannonRig.STEEL_COLOR, CannonRig.SPECULAR_AMBIENT_STRENGTH, 1f),
                ["glass"] = Material(CannonRig.GLASS_COLOR, CannonRig.GLASS_SPECULAR_AMBIENT, CannonRig.GLASS_ALPHA),
                ["collar"] = Material(RigVector("COLLAR_STEEL"), CannonRig.SPECULAR_AMBIENT_STRENGTH, 1f),
                ["carriage"] = Material(CannonRig.FRAME_COLOR, CannonRig.FRAME_SPECULAR_AMBIENT, 1f),
                ["wheel"] = Material(RigVector("WHEEL_COLOR"), RigConstant("WHEEL_SPECULAR_AMBIENT"), 1f),
                ["roller"] = Material(RigVector("ROLLER_COLOR"), RigConstant("ROLLER_SPECULAR_AMBIENT"), 1f),
            };

            //The thirteen ball colours, authored sRGB as a ball's tint is
            var balls = Enum.GetValues<BallType>().Select(t => Vec(BasicEffectParamsProvider.GetDiffuseTintByType(t))).ToArray();

            var json = new Dictionary<string, object>
            {
                ["about"] = "The cannon of Bubble Shooter 3D, exported from the game's own meshes by Tools/WindingCheck --export-cannon (#949). "
                    + "Positions are 16-bit over positionScale, normals bytes over 127; matrices are the game's, row vectors, in row order.",
                ["positionScale"] = scale,
                ["vertexStride"] = 12,
                ["indexOffset"] = indexOffset,
                ["parts"] = entries,
                ["materials"] = materials,
                ["figures"] = figures,
                ["balls"] = balls,
            };
            string text = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(directory, "cannon.json"), text.Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));

            int triangles = read.Sum(p => p.Part.Indices.Length / 3);
            Console.WriteLine($"[export] {read.Count} parts, {vertexStart} vertices, {triangles} triangles: cannon.bin {bin.Length} bytes, cannon.json {text.Length} chars, in {directory}");
            return 0;
        }

        private static short Quantise16(float v) => (short)Math.Clamp(MathF.Round(v * 32767f), -32767f, 32767f);
        private static sbyte Quantise8(float v) => (sbyte)Math.Clamp(MathF.Round(v * 127f), -127f, 127f);

        private static float RigConstant(string name) =>
            Convert.ToSingle(typeof(CannonRig).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null),
                CultureInfo.InvariantCulture);

        private static Vector3 RigVector(string name) =>
            (Vector3)typeof(CannonRig).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);

        private static float[] Vec(Vector3 v) => new[] { Round(v.X), Round(v.Y), Round(v.Z) };

        private static float Round(float v) => MathF.Round(v, 5);

        private static Dictionary<string, object> Material(Vector3 color, float sky, float alpha) =>
            new() { ["color"] = Vec(color), ["sky"] = Round(sky), ["alpha"] = Round(alpha) };

        private static float[] Row(Matrix m) => new[]
        {
            Round(m.M11), Round(m.M12), Round(m.M13), Round(m.M14), Round(m.M21), Round(m.M22), Round(m.M23), Round(m.M24),
            Round(m.M31), Round(m.M32), Round(m.M33), Round(m.M34), Round(m.M41), Round(m.M42), Round(m.M43), Round(m.M44),
        };
    }
}
