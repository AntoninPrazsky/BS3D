using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;

namespace BS3D.Platform
{
    /// <summary>
    /// What MonoGame's GLES backend leaves after an instanced draw, settled (#1048; the why is on
    /// <see cref="InstancedDraws"/>): the divisor of every attribute the draw had enabled put back to 0, so the next
    /// SpriteBatch does not step its vertices per instance, and MonoGame told its attribute state is stale, so the next
    /// instanced draw from the same buffers sets its divisors again rather than trusting a cache this reset has made wrong.
    /// </summary>
    internal static class GlesInstancing
    {
        private const int GL_MAX_VERTEX_ATTRIBS = 0x8869;

        [DllImport("libGLESv3.so", EntryPoint = "glVertexAttribDivisor")]
        private static extern void VertexAttribDivisor(int index, int divisor);

        [DllImport("libGLESv2.so", EntryPoint = "glGetIntegerv")]
        private static extern void GetIntegerv(int name, out int value);

        //MonoGame's own fields (GraphicsDevice.OpenGL.cs, 3.8.5 and 3.8.6), all STATIC: _attribsDirty, the flag ApplyAttribs
        //reads before trusting its cached bindings, and _enabledVertexAttributes, the attribute locations the last draw
        //left enabled - the only ones an instanced draw can have given a divisor. The flag's value is boxed once, so
        //setting it allocates nothing.
        private static FieldInfo _attribsDirty;
        private static List<int> _enabled;
        private static readonly object TRUE = true;

        //Without MonoGame's list: every attribute the context has, asked once on the GL thread
        private static int _allAttributes;

        internal static void Install()
        {
            const BindingFlags STATIC = BindingFlags.Static | BindingFlags.NonPublic;
            _attribsDirty = typeof(GraphicsDevice).GetField("_attribsDirty", STATIC);
            _enabled = typeof(GraphicsDevice).GetField("_enabledVertexAttributes", STATIC)?.GetValue(null) as List<int>;
            Console.WriteLine($"[render] GLES instancing: after every instanced draw the divisors of "
                + (_enabled != null ? "the enabled attributes" : "every attribute") + " are reset"
                + (_attribsDirty != null ? " and MonoGame's attribute cache is marked stale"
                    : " - MonoGame has no _attribsDirty: an instanced draw repeated from the same buffers may lose its divisors"));
            InstancedDraws.AfterEach = Settle;
        }

        private static void Settle(GraphicsDevice device)
        {
            if (_enabled != null)
            {
                for (int i = 0; i < _enabled.Count; i++) VertexAttribDivisor(_enabled[i], 0);
            }
            else
            {
                if (_allAttributes == 0)
                {
                    GetIntegerv(GL_MAX_VERTEX_ATTRIBS, out _allAttributes);
                    if (_allAttributes <= 0) _allAttributes = 16;
                }
                for (int i = 0; i < _allAttributes; i++) VertexAttribDivisor(i, 0);
            }

            _attribsDirty?.SetValue(null, TRUE);
        }
    }
}
