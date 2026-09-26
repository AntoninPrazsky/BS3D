using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;

namespace BS3D.Platform
{
    /// <summary>
    /// Submits the frame's recorded Direct3D 11 commands to the GPU now rather than at Present (#634), so the GPU works
    /// through <see cref="FrameLimiter.WaitForCompositor"/> instead of starting after it — see
    /// <c>BS3DGame.PaceFrame</c> for the measurement that asked for it.
    /// </summary>
    /// <remarks>
    /// MonoGame does not expose <c>ID3D11DeviceContext::Flush</c>, so this reaches its immediate context through the
    /// private <c>_d3dContext</c> field and binds a delegate to SharpDX's <c>Flush</c> once per context — a device
    /// reset can replace the context, so the field is read each frame and the delegate rebuilt when it changes (a field
    /// read of a reference, no allocation). A MonoGame that renamed the field makes this a no-op, said once on the
    /// console: the game then paces exactly as before #634's move, dropping frames on a heavy scene but never failing.
    /// </remarks>
    internal sealed class GpuFlush
    {
        private static readonly FieldInfo ContextField =
            typeof(GraphicsDevice).GetField("_d3dContext", BindingFlags.NonPublic | BindingFlags.Instance);

        private object _context;
        private Action _flush;
        private bool _reported;

        /// <summary>Flushes <paramref name="device"/>'s immediate context, or does nothing where it cannot be reached.</summary>
        public void Flush(GraphicsDevice device)
        {
            object context = ContextField?.GetValue(device);

            if (!ReferenceEquals(context, _context))
            {
                _context = context;
                _flush = context == null ? null
                    : (Action)Delegate.CreateDelegate(typeof(Action), context, "Flush", false, false);

                if (_flush == null && !_reported)
                {
                    _reported = true;
                    Console.WriteLine("[pacing] MonoGame's device context was not found; frames are not flushed before the compositor wait");
                }
            }

            _flush?.Invoke();
        }
    }
}
