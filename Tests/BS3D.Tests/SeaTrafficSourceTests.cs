using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The horizon's sink (#1125) is the one thing that hides a vessel's jump from one end of its lane to the other, and it
    /// lives in <c>Rig.fx</c>, which no test can run without a graphics device: <see cref="SeaTrafficTests"/> hold its C# mirror
    /// (<c>SeaTrafficLayout.Sink</c>), so these read the shader's drop and which draws take it. The drop is the squared excess
    /// over <c>SinkFrom</c> over <c>SinkScale</c>, taken off the world position before it is projected and before the pixel
    /// shader's cut at the waterline reads it; the effect is loaded with <c>SINK_SCALE</c>; the vessels draw with
    /// <c>SINK_FROM</c>, and the platforms and the lighthouse, which stand still, with <c>NO_SINK</c>. And the sea's
    /// <c>Prepare</c> is what starts the vessels' passage again when the Game selects it.
    /// </summary>
    public class SeaTrafficSourceTests
    {
        private static string FindFile(string relative)
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }

            throw new FileNotFoundException($"No {relative} was found above the test assembly.");
        }

        //A source without its comments and with its whitespace run together, so a word in a note is not taken for code and a
        //line broken differently still reads
        private static string Code(params string[] relative)
        {
            string text = File.ReadAllText(FindFile(Path.Combine(relative)));
            text = Regex.Replace(text, @"//[^\n]*", string.Empty);
            return Regex.Replace(text, @"\s+", " ");
        }

        //The body of a function or method, from its signature to the closing brace at the same depth
        private static string Body(string code, string signature)
        {
            int start = code.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"no {signature}");

            int open = code.IndexOf('{', start), depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1);
            }

            throw new InvalidDataException($"The body of {signature} never closes.");
        }

        private static string Scenes(string file) => Code("BS3DLibs", "Prazsky.Core", "Render", "Scenes", file);

        [Fact]
        public void TheShaderSinksByTheSquaredExcessBeforeItProjectsAndCuts()
        {
            //Seen to fire with the drop made linear (past / SinkScale), and with it moved after output.World is taken
            string vs = Body(Code("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Rig.fx"), "RigVertexOutput RigVS(");

            Match past = Regex.Match(vs, @"float past = max\( ?length\( ?world\.xz - CameraPosition\.xz ?\) - SinkFrom, 0(\.0)? ?\);");
            Assert.True(past.Success, "RigVS does not take how far past SinkFrom from the lens a vertex stands");
            Match drop = Regex.Match(vs, @"world\.y -= past \* past / SinkScale;");
            Assert.True(drop.Success, "RigVS does not drop a vertex by the square of how far past SinkFrom it stands over SinkScale");
            Assert.True(past.Index < drop.Index, "RigVS drops a vertex before it knows how far past SinkFrom it stands");

            Match projected = Regex.Match(vs, @"output\.Position = mul\( ?mul\( ?world, View ?\), Projection ?\);");
            Match cut = Regex.Match(vs, @"output\.World = world\.xyz;");
            Assert.True(projected.Success && projected.Index > drop.Index, "RigVS projects the world position before it has sunk");
            Assert.True(cut.Success && cut.Index > drop.Index, "RigVS hands the pixel shader's cut at the waterline the position before it has sunk");
        }

        [Fact]
        public void TheEffectIsLoadedWithTheSinksScaleAndOnlyTheVesselsDrawWithItsStart()
        {
            //Seen to fire with LoadEffect setting no SinkScale, the rigs' draw setting no SinkFrom, and the lighthouse drawn with
            //SINK_FROM
            string rigs = Scenes("OilRigs.cs");
            Assert.Contains("effect.Parameters[\"SinkScale\"].SetValue(SeaTrafficLayout.SINK_SCALE);", Body(rigs, "internal static Effect LoadEffect("));
            Assert.Contains("_sinkFrom.SetValue(SeaTrafficLayout.NO_SINK);", Body(rigs, "public void Draw(in SceneFrame frame, float waterY, bool fineMeshes)"));

            string traffic = Scenes("SeaTraffic.cs");
            Assert.Contains("_sinkFrom.SetValue(sinkFrom);", Body(traffic, "private void DrawPart("));
            string draw = Body(traffic, "public void Draw(in SceneFrame frame, float waterY)");
            Assert.Matches(@"DrawPart\(_lighthouse, [^;]*, SeaTrafficLayout\.NO_SINK\);", draw);
            Assert.Matches(@"DrawPart\(SeaTrafficLayout\.ShipAt\([^;]*, SeaTrafficLayout\.SINK_FROM\);", draw);
            Assert.Matches(@"DrawPart\(SeaTrafficLayout\.YachtAt\([^;]*, SeaTrafficLayout\.SINK_FROM\);", draw);
        }

        [Fact]
        public void TheRigsAreDrawnFineWhereTheTierAndTheReachSay()
        {
            //#1169's finer rigs reach the screen only through this wiring, which no test can run without a device: the sea hands
            //the tier's FineMeshes to the rigs' draw, which picks each rig's mesh through DrawsFine from the lens, and DrawRig
            //draws the finer's triangles where it is told to. DrawsFine reads the tier and the distance and nothing else: the
            //reach is a depth rule the trailer's maxdetail leaves (OilRigTests). Seen to fire with SeaBackdrop passing false, the
            //nearer rig drawn coarse whatever DrawsFine says, and RenderDetail.Maximum back in DrawsFine.
            Assert.Contains("_rigs.Draw(frame, _seaConfig.LevelY, Services.FineMeshes);", Scenes("SeaBackdrop.cs"));

            string rigs = Scenes("OilRigs.cs");
            string draw = Body(rigs, "public void Draw(in SceneFrame frame, float waterY, bool fineMeshes)");
            Assert.Contains("Vector3 lens = frame.Camera.Position;", draw);
            Assert.Contains("DrawRig(_nearFrame, DrawsFine(fineMeshes, lens, _nearFoot));", draw);
            Assert.Contains("DrawRig(_farFrame, DrawsFine(fineMeshes, lens, _farFoot));", draw);
            Assert.Contains("_graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, fine ? _fineStart : 0, fine ? _fineTriangles : _coarseTriangles);",
                Body(rigs, "private void DrawRig("));

            int start = rigs.IndexOf("internal static bool DrawsFine(", StringComparison.Ordinal);
            Assert.True(start >= 0, "no DrawsFine");
            string drawsFine = rigs.Substring(start, rigs.IndexOf(';', start) - start);
            Assert.DoesNotContain("RenderDetail", drawsFine);
            Assert.Contains("fineMeshes &&", drawsFine);
        }

        [Fact]
        public void TheSurfaceStandsStillPastStillAtAndOnlyItsHeightDoes()
        {
            //What the lighthouse and the vessels stand in (#1170, SeaTrafficTests.AllOfItStandsWhereTheSurfaceStandsStillWhoeverLooks):
            //Sea.fx's vertex program scales the swell's displacement by the ramp from StillFrom to StillAt off the arena, after
            //the swell is summed and before the vertex is placed, and scales nothing else, so the light the pixel shader reads
            //(the rest position's fade, the long waves' normal, the crest) is the camera's alone; the sea pushes both distances.
            //Seen to fire with the ramp taken out, with it moved onto OceanSurface's amplitude, and with SeaBackdrop setting no
            //StillAt.
            string vs = Body(Code("BS3DLibs", "Prazsky.Shaders", "Content", "Shaders", "Sea.fx"), "SeaVertexOutput SeaVS(");

            Match swell = Regex.Match(vs, @"OceanSurface\( ?restXZ, ampScale \* \( ?1\.0 - calm ?\) \* shore, disp, slope, fold, crest ?\);");
            Assert.True(swell.Success, "SeaVS sums the swell at another amplitude than the camera's fade, the island's calm and the shore");
            Match still = Regex.Match(vs, @"if \( ?StillAt > 0\.0 ?\) disp \*= saturate\( ?\( ?StillAt - length\( ?restXZ ?\) ?\) / max\( ?StillAt - StillFrom, 1\.0 ?\) ?\);");
            Assert.True(still.Success, "SeaVS does not still the displacement from StillFrom to StillAt off the arena");
            Match placed = Regex.Match(vs, @"float3 worldPosition = float3\( ?restXZ\.x \+ disp\.x, SeaLevelY \+ disp\.y, restXZ\.y \+ disp\.z ?\);");
            Assert.True(placed.Success && swell.Index < still.Index && still.Index < placed.Index,
                "SeaVS does not still the displacement between summing the swell and placing the vertex");
            Assert.Contains("output.Rest = float3(restXZ, ampScale * (1.0 - calm) * shore);", vs);
            Assert.DoesNotMatch(@"StillAt[^;]*(slope|crest|ampScale)", vs);

            string apply = Body(Scenes("SeaBackdrop.cs"), "private void ApplySeaParameters(");
            Assert.Contains("_seaEffect.Parameters[\"StillFrom\"].SetValue(_seaConfig.StillFrom);", apply);
            Assert.Contains("_seaEffect.Parameters[\"StillAt\"].SetValue(_seaConfig.StillAt);", apply);
        }

        [Fact]
        public void SelectingTheSeaStartsTheVesselsPassageAgain()
        {
            //SceneRenderer.Prepare is how the Game says it selected a scene; the sea hands it to its traffic, whose draws read
            //their seconds off the clock that restarts. Seen to fire with SeaBackdrop's Prepare override taken out.
            Assert.Matches(@"public override void Prepare\(\) => _traffic\.Select\(\);", Scenes("SeaBackdrop.cs"));

            string traffic = Scenes("SeaTraffic.cs");
            Assert.Contains("public void Select() => _clock.Select();", traffic);
            Assert.Contains("float time = _clock.Read(frame.Time);", Body(traffic, "public void Draw(in SceneFrame frame, float waterY)"));
        }
    }
}
