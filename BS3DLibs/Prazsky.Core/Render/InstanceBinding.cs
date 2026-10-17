using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Binds a mesh and its per-instance buffer for an instanced draw without allocating. Every instanced draw in
    /// the process goes through here.
    /// <para>
    /// <c>GraphicsDevice.SetVertexBuffers</c> takes <c>params VertexBufferBinding[]</c>, so the natural call - two
    /// bindings written inline - builds a new two-element array on every draw. With about seventy-five instanced
    /// draws a frame that was the whole of the frame thread's steady allocation: 4.2 KB a frame on the main menu
    /// and 2.3 to 4.2 KB in play, nearly all of it <c>VertexBufferBinding[]</c> (BestPractices.md §3).
    /// </para>
    /// <para>
    /// One array serves every call. That is safe because MonoGame copies the bindings out of it element by element
    /// (<c>VertexBufferBindings.Set</c>) and keeps nothing of the array itself, and because every draw is made on the
    /// frame thread. The slots are cleared after the call, so the array does not keep a disposed buffer alive.
    /// </para>
    /// </summary>
    internal static class InstanceBinding
    {
        private static readonly VertexBufferBinding[] Pair = new VertexBufferBinding[2];

        /// <summary>
        /// Binds <paramref name="geometry"/> (per vertex, from <paramref name="geometryOffset"/>) to slot 0 and
        /// <paramref name="instances"/> (one element per instance, from <paramref name="firstInstance"/>) to slot 1.
        /// </summary>
        internal static void Set(GraphicsDevice device, VertexBuffer geometry, int geometryOffset, VertexBuffer instances, int firstInstance)
        {
            Pair[0] = new VertexBufferBinding(geometry, geometryOffset, 0);
            Pair[1] = new VertexBufferBinding(instances, firstInstance, 1);
            device.SetVertexBuffers(Pair);
            Pair[0] = default;
            Pair[1] = default;
        }
    }
}
