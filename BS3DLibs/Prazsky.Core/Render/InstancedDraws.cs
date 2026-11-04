using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Every instanced draw in the libraries goes through here (#1048), so a platform can settle what MonoGame leaves
    /// behind after one. <b>Call <see cref="DrawInstanced(GraphicsDevice, PrimitiveType, int, int, int, int)"/>, never
    /// <c>GraphicsDevice.DrawInstancedPrimitives</c> itself.</b>
    /// </summary>
    /// <remarks>
    /// Why: an instanced draw sets a vertex attribute's divisor to 1 for each per-instance element. The next draw from a
    /// vertex buffer sets every divisor it uses again, but a draw of user primitives (<c>DrawUserPrimitives</c>,
    /// <c>DrawUserIndexedPrimitives</c>: every SpriteBatch, so Myra and the HUD, and the Potato path's scale-up quad)
    /// puts it back to 0 only where MonoGame is built for desktop OpenGL or DirectX. MonoGame 3.8.5 and 3.8.6 skip the
    /// reset under <c>#if !(GLES || MONOMAC)</c> in <c>VertexDeclaration.OpenGL.cs</c>. So on Android an attribute the
    /// last instanced draw stepped per instance stays so, and every vertex of the following sprite reads the same
    /// position: on the phone not one pixel of the menus, the HUD or a plain SpriteBatch square was drawn while the 3D
    /// was whole. The Android build answers <see cref="AfterEach"/>; Windows and the Pi leave it null and pay nothing.
    /// </remarks>
    public static class InstancedDraws
    {
        /// <summary>
        /// What the platform does after every instanced draw: null where MonoGame needs nothing (Windows, DesktopGL), the
        /// divisor reset on Android (<c>GameAndroid/Platform/GlesInstancing.cs</c>). Set once, before the first frame.
        /// </summary>
        public static Action<GraphicsDevice> AfterEach { get; set; }

        /// <summary><c>DrawInstancedPrimitives</c>, then <see cref="AfterEach"/>.</summary>
        public static void DrawInstanced(this GraphicsDevice device, PrimitiveType primitiveType, int baseVertex, int startIndex,
            int primitiveCount, int instanceCount)
        {
            device.DrawInstancedPrimitives(primitiveType, baseVertex, startIndex, primitiveCount, instanceCount);
            AfterEach?.Invoke(device);
        }

        /// <summary><c>DrawInstancedPrimitives</c> from <paramref name="baseInstance"/>, then <see cref="AfterEach"/>.</summary>
        public static void DrawInstanced(this GraphicsDevice device, PrimitiveType primitiveType, int baseVertex, int startIndex,
            int primitiveCount, int baseInstance, int instanceCount)
        {
            device.DrawInstancedPrimitives(primitiveType, baseVertex, startIndex, primitiveCount, baseInstance, instanceCount);
            AfterEach?.Invoke(device);
        }
    }
}
