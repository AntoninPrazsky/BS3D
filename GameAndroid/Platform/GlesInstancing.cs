using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;

namespace BS3D.Platform
{
    /// <summary>
    /// What MonoGame's GLES backend leaves after an instanced draw, settled (#1048; the why is on
    /// <see cref="InstancedDraws"/>): every vertex attribute's divisor put back to 0, so the next SpriteBatch does not
    /// step its vertices per instance, and MonoGame told its attribute state is stale, so the next instanced draw from
    /// the same buffers sets its divisors again rather than trusting a cache this reset has made wrong.
    /// </summary>
    internal static class GlesInstancing
    {
        private const int GL_MAX_VERTEX_ATTRIBS = 0x8869;

        [DllImport("libGLESv3.so", EntryPoint = "glVertexAttribDivisor")]
        private static extern void VertexAttribDivisor(int index, int divisor);

        [DllImport("libGLESv2.so", EntryPoint = "glGetIntegerv")]
        private static extern void GetIntegerv(int name, out int value);

        //How many attributes there are to reset, asked once on the GL thread (the first instanced draw)
        private static int _attributes;

        //GraphicsDevice._attribsDirty, the flag ApplyAttribs reads before trusting its cached bindings - a STATIC field in
        //MonoGame 3.8.6's GraphicsDevice.OpenGL.cs, as are the bindings it guards. Boxed once, so setting it allocates nothing.
        private static FieldInfo _attribsDirty;
        private static readonly object TRUE = true;

        internal static void Install()
        {
            _attribsDirty = typeof(GraphicsDevice).GetField("_attribsDirty", BindingFlags.Static | BindingFlags.NonPublic);
            if (_attribsDirty == null)
                Console.WriteLine("[render] MonoGame has no _attribsDirty: an instanced draw repeated from the same buffers may lose its divisors");
            InstancedDraws.AfterEach = Settle;
        }

        private static void Settle(GraphicsDevice device)
        {
            if (_attributes == 0)
            {
                GetIntegerv(GL_MAX_VERTEX_ATTRIBS, out _attributes);
                if (_attributes <= 0) _attributes = 16;
                Console.WriteLine($"[render] GLES instancing: the divisors of {_attributes} attributes are reset after every instanced draw");
            }

            for (int i = 0; i < _attributes; i++) VertexAttribDivisor(i, 0);
            _attribsDirty?.SetValue(null, TRUE);
        }
    }
}
