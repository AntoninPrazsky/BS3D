using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The trophy cup's applied ornament (#429): the stones, the metal settings that hold them and the beads
    /// of a beaded moulding. Each is a UNIT mesh standing on its own <b>+Y</b> — the stone's dome and the
    /// setting's rim face up, and y = 0 is the surface they sit on — so
    /// <see cref="TrophyMesh.Ornament"/> can size one and turn it onto any point of the cup, and one mesh
    /// instanced many times is one draw call however many stones a tier carries.
    /// <para>
    /// <b>They are separate meshes because they are separate materials.</b> A stone is a dielectric with a
    /// colour of its own and the cup is a metal (or crystal), and one <see cref="InstancedModelRenderer"/> is
    /// one material; building the stones into <see cref="TrophyMesh"/> would have made every sapphire gold.
    /// </para>
    /// <para>
    /// <b>The stones are cut since #1250</b> (<see cref="CutStoneMesh"/>, a round brilliant's crown): the owner asked for
    /// cut stones "like we have the glass ceiling" in place of #429's cabochons, which followed the Crown of Saint
    /// Wenceslas, and a stone's facets are what throw a different light as the cup turns.
    /// </para>
    /// </summary>
    public static class TrophyOrnaments
    {
        //Facets around a setting. A big stone is about 3.6 % of the cup's height across, so at the dolly's near end - the
        //cup most of the frame high - a few dozen pixels: 32 keeps the collet's outline round there.
        private const int STONE_SEGMENTS = 32;

        /// <summary>
        /// A cut stone of radius 1 at its girdle (y = 0), the crown standing up to <see cref="CutStoneMesh.TableY"/>, with a
        /// back under the girdle that the setting hides (#1250).
        /// </summary>
        public static CutStoneMesh CreateStone(GraphicsDevice device) => new(device);

        /// <summary>
        /// A collet: the raised metal ring a cabochon sits in, with a rolled rim that laps just over the
        /// stone's girdle and a foot that spreads onto the cup. Traced as a closed ring — up the inside, over
        /// the roll, down the outside and back in underneath — so it faces the stone, the sky, the room and
        /// the cup in turn (see <see cref="LatheMesh"/> on profile direction).
        /// </summary>
        public static LatheMesh CreateSetting(GraphicsDevice device) => new(device, new LathePoint[]
        {
            new(0.90f, -0.30f, crease: true),
            new(0.90f, 0.08f),
            new(0.95f, 0.19f),
            new(1.05f, 0.24f),
            new(1.15f, 0.20f),
            new(1.21f, 0.09f),
            new(1.26f, -0.02f, crease: true),
            new(1.34f, -0.08f, crease: true),
            new(1.34f, -0.30f, crease: true),
            new(0.90f, -0.30f, crease: true)
        }, STONE_SEGMENTS);

        /// <summary>
        /// One bead of a beaded moulding, radius 1. Coarse, because a bead is under one percent of the cup's
        /// height: ten slices is round at the size it is ever seen, and a row of seventy of them is still one
        /// draw of a few thousand triangles.
        /// </summary>
        public static SphereMesh CreateBead(GraphicsDevice device) => new(device, 1f, 10, 6);
    }
}
