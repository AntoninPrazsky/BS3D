using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Camera;

namespace MapEditor
{
    /// <summary>
    /// <b>The editor's lens, seen from the game's frame</b> (#1210). The editor keeps a map in its raw grid frame (the
    /// selector, the field's outline, the six views and the flying camera all work there, the map never centred), while
    /// the setting the map is drawn in (the sky, the scene, the island, the ceiling glass) and the balls themselves stand
    /// in the frame a session hangs the field in: the middle of the top level on the island's axis, the top at
    /// <see cref="ClusterHang.FIELD_TOP_Y"/> or raised past the death line. This camera is <see cref="Inner"/> moved by
    /// that hang (<see cref="Follow"/>), so everything drawn through it stands round the field exactly where a session
    /// puts it, and on the screen and in the depth buffer it meets what the inner camera draws in the grid frame.
    /// <para>
    /// The hang is <see cref="ClusterHang.FitWorldOffset"/> of the <b>uncentred</b> map (<see cref="HangOffset"/>): a cell's
    /// grid position plus it is the cell's position in a session, which centres the map first
    /// (<see cref="BallsMap.Center"/>) and adds the offset of the centred one. The two agree because the offset's X and Z
    /// are taken off the top level's own corners in whichever frame the map is in; <c>MapEditorGameFrameTests</c> holds it.
    /// </para>
    /// <para>
    /// Plain geometry with no device, so <c>Tests/BS3D.Tests</c> compiles it in.
    /// </para>
    /// </summary>
    public sealed class GameFrameCamera : ICamera
    {
        public GameFrameCamera(ICamera inner) => Inner = inner;

        /// <summary>The editor's own camera, in the grid frame.</summary>
        public ICamera Inner { get; }

        /// <summary>What a grid position adds to stand in the game's frame: the field's hang, as <see cref="Follow"/> last set it.</summary>
        public Vector3 Shift { get; private set; }

        /// <summary>The Y the field's top level hangs at in the game's frame (the ceiling glass hangs over it), as <see cref="Follow"/> last set it.</summary>
        public float FieldTopY { get; private set; }

        /// <summary>
        /// The offset a session hangs <paramref name="map"/> by, applied to the map's grid positions as the editor keeps them
        /// (uncentred), and the Y its top level ends up at. See the class remarks.
        /// </summary>
        public static Vector3 HangOffset(BallsMap map, out float fieldTopY) => ClusterHang.FitWorldOffset(map, out fieldTopY);

        /// <summary>
        /// Takes this frame's hang of <paramref name="map"/> and the inner camera's pose: call it once a frame, before anything
        /// is drawn through this camera (the view is worked out here, not on every read).
        /// </summary>
        public void Follow(BallsMap map)
        {
            Shift = HangOffset(map, out float fieldTopY);
            FieldTopY = fieldTopY;

            //A point of the game's frame is seen where the grid point it stands for is: the shift taken off before the view
            View = Matrix.CreateTranslation(-Shift) * Inner.View;
        }

        public Matrix View { get; private set; } = Matrix.Identity;

        public Matrix Projection => Inner.Projection;

        public Vector3 Position => Inner.Position + Shift;

        public Vector3 Target => Inner.Target + Shift;

        public Vector3 Up => Inner.Up;

        public float NearPlane { get => Inner.NearPlane; set => Inner.NearPlane = value; }

        public float FarPlane { get => Inner.FarPlane; set => Inner.FarPlane = value; }
    }
}
