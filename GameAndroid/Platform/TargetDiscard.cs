using System;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/TargetDiscard.cs</c> (#801, #1048): a phone's GPU is a tiler as
    /// the Pi's is, so the scaled 3D's depth is declared dead before the target is left, or the tiler writes it out to
    /// memory. <c>glInvalidateFramebuffer</c> is OpenGL ES 3.0, exported by the system's <c>libGLESv3.so</c>; it is
    /// called only once the context has said it is ES 3 or newer.
    /// </summary>
    internal static class TargetDiscard
    {
        private const int GL_FRAMEBUFFER = 0x8D40;
        private const int GL_DEPTH_ATTACHMENT = 0x8D00;
        private const int GL_STENCIL_ATTACHMENT = 0x8D20;
        private const int GL_VERSION = 0x1F02;
        private static readonly int[] DEPTH_AND_STENCIL = { GL_DEPTH_ATTACHMENT, GL_STENCIL_ATTACHMENT };

        private static bool _resolved;
        private static bool _available;

        [DllImport("libGLESv3.so", EntryPoint = "glInvalidateFramebuffer")]
        private static extern void InvalidateFramebuffer(int target, int count, int[] attachments);

        [DllImport("libGLESv2.so", EntryPoint = "glGetString")]
        private static extern IntPtr GetString(int name);

        internal static void DiscardDepth()
        {
            if (!_resolved)
            {
                _resolved = true;
                try
                {
                    //"OpenGL ES 3.2 V@0800.0 ..." - the major version is the character after "OpenGL ES "
                    string version = Marshal.PtrToStringAnsi(GetString(GL_VERSION)) ?? "";
                    const string PREFIX = "OpenGL ES ";
                    _available = version.StartsWith(PREFIX, StringComparison.Ordinal) && version.Length > PREFIX.Length
                        && version[PREFIX.Length] >= '3';
                    Console.WriteLine($"[render] {version}: the scene target's depth is {(_available ? "" : "not ")}discarded");
                }
                catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
                {
                    Console.WriteLine($"[render] No glInvalidateFramebuffer: {e.GetType().Name} - the scene target's depth is not discarded");
                }
            }
            if (_available) InvalidateFramebuffer(GL_FRAMEBUFFER, DEPTH_AND_STENCIL.Length, DEPTH_AND_STENCIL);
        }
    }
}
