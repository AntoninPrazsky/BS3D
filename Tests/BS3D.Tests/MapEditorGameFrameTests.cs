using MapEditor;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The map editor draws the setting round a map where a session puts it (#1210). The editor keeps a map in its raw grid
    /// frame, never centred; the island, the ceiling glass, the scene and the balls are drawn through
    /// <see cref="GameFrameCamera"/>, the editor's lens moved by the field's hang. What holds that to the Game without a frame:
    /// every cell's grid position plus the hang is where a session puts the cell (which centres the map on its top level's
    /// balls first, <see cref="BallsMap.Center"/>, and hangs it by the centred map's offset), for fields of either parity at
    /// the top, a layout inset unevenly in its field, and a field deep enough to be raised past the death line; and the
    /// moved lens sees a point of the game's frame exactly where the editor's own lens sees the grid point it stands for.
    /// </summary>
    public class MapEditorGameFrameTests
    {
        private const float EPSILON = 1e-4f;

        //(sizeX, sizeZ, levels, an inset of the top level's layout in X and Z): the default new map, a shipped field (Heart's,
        //its top level odd), a lopsided inset layout (the residual #561 was about), and Belfry's thirty levels, raised
        public static TheoryData<int, int, int, int, int> Fields() => new()
        {
            { 10, 10, 15, 0, 0 },
            { 15, 15, 18, 0, 0 },
            { 17, 13, 12, 3, 1 },
            { 15, 15, 30, 2, 5 },
        };

        //The editor's map: a top level whose balls stand from the inset to the far edge, so the session's centring has a
        //residual to leave, and a ball lower down
        private static BallsMap EditorMap(int sizeX, int sizeZ, int levels, int insetX, int insetZ)
        {
            BallsMap map = new((byte)sizeX, (byte)sizeZ, (byte)levels);
            byte top = (byte)(levels - 1);
            for (int x = insetX; x < sizeX; x++)
                for (int z = insetZ; z < sizeZ; z += 2)
                    map.PutBallAt((byte)x, (byte)z, top, BallType.Type1);
            map.PutBallAt(0, 0, 0, BallType.Type2);
            return map;
        }

        [Theory]
        [MemberData(nameof(Fields))]
        public void Every_cell_plus_the_hang_is_where_a_session_puts_it(int sizeX, int sizeZ, int levels, int insetX, int insetZ)
        {
            BallsMap editor = EditorMap(sizeX, sizeZ, levels, insetX, insetZ);
            Vector3 hang = GameFrameCamera.HangOffset(editor, out float editorTopY);

            //The session's map: the same layout, centred, hung by its own offset (GameplayScreen.FitFieldToMap)
            BallsMap session = EditorMap(sizeX, sizeZ, levels, insetX, insetZ);
            session.Center();
            Vector3 sessionOffset = ClusterHang.FitWorldOffset(session, out float sessionTopY);

            Assert.Equal(sessionTopY, editorTopY, 5);

            for (int level = 0; level < levels; level++)
                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                    {
                        XZLevel cell = new(x, z, level);
                        Vector3 inEditor = editor.GetRealCenteredPosition(cell) + hang;
                        Vector3 inSession = session.GetRealCenteredPosition(cell) + sessionOffset;
                        Assert.True(Vector3.Distance(inEditor, inSession) < EPSILON,
                            $"cell {x},{z},{level}: the editor hangs it at {inEditor}, a session at {inSession}");
                    }
        }

        [Theory]
        [MemberData(nameof(Fields))]
        public void The_field_hangs_over_the_island_at_the_top_a_session_gives_it(int sizeX, int sizeZ, int levels, int insetX, int insetZ)
        {
            BallsMap editor = EditorMap(sizeX, sizeZ, levels, insetX, insetZ);
            Vector3 hang = GameFrameCamera.HangOffset(editor, out float fieldTopY);

            //The middle of the top level stands on the island's axis, at the field's top: the plate hangs over it there
            byte top = (byte)(levels - 1);
            Vector3 near = editor.GetRealCenteredPosition(new XZLevel(0, 0, top)) + hang;
            Vector3 far = editor.GetRealCenteredPosition(new XZLevel(sizeX - 1, sizeZ - 1, top)) + hang;
            Vector3 middle = (near + far) * 0.5f;

            Assert.True(new Vector2(middle.X, middle.Z).Length() < EPSILON, $"the top level's middle stands at {middle}, off the island's axis");
            Assert.Equal(fieldTopY, middle.Y, 4);
            Assert.True(fieldTopY >= ClusterHang.FIELD_TOP_Y - EPSILON);

            //And the bottom level clears the death line, so the whole field hangs over the island's stone
            Vector3 bottom = editor.GetRealCenteredPosition(new XZLevel(0, 0, 0)) + hang;
            Assert.True(bottom.Y > ClusterHang.DEATH_Y, $"the bottom level hangs at {bottom.Y}, past the death line {ClusterHang.DEATH_Y}");
            Assert.True(bottom.Y > ArenaIsland.TOP_Y);
        }

        [Theory]
        [MemberData(nameof(Fields))]
        public void The_moved_lens_sees_a_point_of_the_game_frame_where_the_editor_sees_its_grid_point(int sizeX, int sizeZ, int levels,
            int insetX, int insetZ)
        {
            BallsMap map = EditorMap(sizeX, sizeZ, levels, insetX, insetZ);
            BasicCamera3D editorLens = new(new Vector3(31f, 14f, 47f), 16f / 9f) { Target = new Vector3(6f, 3f, 5f) };
            GameFrameCamera gameLens = new(editorLens);
            gameLens.Follow(map);

            Matrix editorViewProjection = editorLens.View * editorLens.Projection;
            Matrix gameViewProjection = gameLens.View * gameLens.Projection;

            Vector3[] gridPoints = { Vector3.Zero, new(3f, 7f, 2f), new(-20f, -15f, 30f), new(sizeX, levels, sizeZ) };
            foreach (Vector3 grid in gridPoints)
            {
                Vector4 seen = Vector4.Transform(new Vector4(grid, 1f), editorViewProjection);
                Vector4 seenMoved = Vector4.Transform(new Vector4(grid + gameLens.Shift, 1f), gameViewProjection);

                Assert.True(Vector4.Distance(seen, seenMoved) < 1e-3f, $"{grid}: {seen} through the editor's lens, {seenMoved} through the moved one");
            }

            //The lens's own pose moves with it, which what is placed round the lens reads (the sky's dome, the deck, the LODs)
            Assert.True(Vector3.Distance(gameLens.Position, editorLens.Position + gameLens.Shift) < EPSILON);
            Assert.True(Vector3.Distance(gameLens.Target, editorLens.Target + gameLens.Shift) < EPSILON);
            Assert.Equal(editorLens.Projection, gameLens.Projection);
        }
    }
}
