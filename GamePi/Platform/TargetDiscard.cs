using System;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// Tells the GPU that the depth of the render target being left will not be read again (#801): GamePi's half of the
    /// seam, through <c>glInvalidateFramebuffer</c> (ARB_invalidate_subdata, which the Pi's Mesa V3D exposes on its GL 3.1
    /// context). The V3D is a tiler: at the end of a target's pass it writes every tile's colour and depth out to memory
    /// unless told they are dead, and MonoGame never tells it. Discarding the Potato scene target's depth before it is
    /// scaled up measured 21.96 -> 20.37 ms on Pennant at a render of 1912x1076 (the target at nearly native, against
    /// 19.1 drawn straight into the back buffer). A driver without the call leaves this a no-op.
    /// </summary>
    internal static class TargetDiscard
    {
        private const int GL_FRAMEBUFFER = 0x8D40;
        private const int GL_DEPTH_ATTACHMENT = 0x8D00;
        private const int GL_STENCIL_ATTACHMENT = 0x8D20;

        //Depth and stencil both: MonoGame may back a Depth24 target with a packed depth-stencil buffer, and naming an
        //attachment the framebuffer does not have is allowed and ignored
        private static readonly int[] DEPTH_AND_STENCIL = { GL_DEPTH_ATTACHMENT, GL_STENCIL_ATTACHMENT };

        private delegate void InvalidateFramebuffer(int target, int count, int[] attachments);

        private static InvalidateFramebuffer _invalidate;
        private static bool _resolved;

        /// <summary>Discards the depth and stencil of the framebuffer bound now. Call before switching away from it.</summary>
        internal static void DiscardDepth()
        {
            if (!_resolved)
            {
                _resolved = true;
                IntPtr entry = Sdl.GlGetProcAddress("glInvalidateFramebuffer");
                if (entry != IntPtr.Zero) _invalidate = Marshal.GetDelegateForFunctionPointer<InvalidateFramebuffer>(entry);
            }

            _invalidate?.Invoke(GL_FRAMEBUFFER, DEPTH_AND_STENCIL.Length, DEPTH_AND_STENCIL);
        }
    }
}
