using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BS3D.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BS3D.Tools.WindingCheck
{
    internal static partial class Program
    {
        /// <summary>
        /// <c>--portrait &lt;text&gt; --out &lt;png&gt; [--look x,y,z --from x,y,z] [--fov deg] [--size w,h]</c>: one mesh drawn alone on a magenta
        /// ground, culled as the game culls it (clockwise is the front), so a hole in a wall shows the colour a sky would.
        /// An instrument for the openness audit's before and after (#974): the part is the first assembled one whose path
        /// contains the text; without <c>--look</c> and <c>--from</c> the camera stands off the first exposed ring, on the side
        /// facing away from the mesh, looking into the opening. WARP draws it, so no GPU and no window.
        /// </summary>
        private static int Portrait(GraphicsDevice device, List<Part> parts, string text, string[] args)
        {
            Part part = Assemble(parts).FirstOrDefault(p => p.Path.Contains(text, StringComparison.Ordinal));
            if (part == null)
            {
                Console.Error.WriteLine($"no part contains \"{text}\"");
                return 2;
            }

            string Arg(string name) { int at = Array.IndexOf(args, name); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
            Vector3? Vec(string name)
            {
                string v = Arg(name);
                if (v == null) return null;
                float[] f = v.Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                return new Vector3(f[0], f[1], f[2]);
            }

            Vector3 look, from;
            List<MeshOpenness.OpenLoop> rings = ExposedLoopsOf(part);
            Vector3 centroid = part.Positions.Aggregate(Vector3.Zero, (a, p) => a + p) / part.Positions.Length;
            if (Vec("--look") is Vector3 l && Vec("--from") is Vector3 f)
            {
                look = l;
                from = f;
            }
            else if (rings.Count > 0)
            {
                MeshOpenness.OpenLoop ring = rings[0];
                look = ring.Centre;
                float size = ring.Points.Max(p => Vector3.Distance(p, ring.Centre));
                Vector3 n = Vector3.Normalize(Vector3.Cross(ring.Points[0] - ring.Centre, ring.Points[Math.Max(1, ring.Points.Count / 3)] - ring.Centre));
                if (float.IsNaN(n.X) || n.LengthSquared() < 0.5f) n = Vector3.UnitY;
                if (Vector3.Dot(n, ring.Centre - centroid) < 0f) n = -n;
                from = look + n * (size * 5f) + Vector3.Cross(n, Vector3.UnitY) * (size * 0.5f);
            }
            else
            {
                look = centroid;
                float reach = part.Positions.Max(p => Vector3.Distance(p, centroid));
                from = centroid + new Vector3(0.6f, 0.4f, 1f) * (reach * 2.5f);
            }

            int width = 640, height = 480;
            if (Arg("--size") is string sz) { string[] w = sz.Split(','); width = int.Parse(w[0]); height = int.Parse(w[1]); }
            float fov = Arg("--fov") != null ? float.Parse(Arg("--fov"), CultureInfo.InvariantCulture) : 40f;

            var vertices = Vertices(part);
            using var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24);
            device.SetRenderTarget(target);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(255, 0, 255), 1f, 0);
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;

            var effect = new BasicEffect(device)
            {
                VertexColorEnabled = false,
                LightingEnabled = true,
                DiffuseColor = new Vector3(0.78f, 0.74f, 0.66f),
                AmbientLightColor = new Vector3(0.28f),
                PreferPerPixelLighting = true,
                View = Matrix.CreateLookAt(from, look, MathF.Abs(Vector3.Normalize(look - from).Y) > 0.97f ? Vector3.UnitX : Vector3.UnitY),
                Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(fov), width / (float)height, 0.01f, 1000f),
                World = Matrix.Identity,
            };
            effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.5f));
            effect.DirectionalLight0.DiffuseColor = new Vector3(0.85f);
            effect.DirectionalLight1.Enabled = true;
            effect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(0.5f, 0.2f, 0.7f));
            effect.DirectionalLight1.DiffuseColor = new Vector3(0.3f);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, part.Indices, 0, part.Indices.Length / 3);
            }

            device.SetRenderTarget(null);
            string path = Arg("--out") ?? "portrait.png";
            using (FileStream stream = File.Create(path)) target.SaveAsPng(stream, width, height);
            Console.WriteLine($"portrait of {part.Path}: {rings.Count} exposed rings; from ({from.X:0.###}, {from.Y:0.###}, {from.Z:0.###}) looking at ({look.X:0.###}, {look.Y:0.###}, {look.Z:0.###}) -> {path}");
            return 0;
        }
    }
}
