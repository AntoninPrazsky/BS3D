using System;
using Android.Opengl;

namespace BS3D.Platform
{
    /// <summary>
    /// The Android build's namesake of <c>Game/Platform/GpuCaps.cs</c> (#1064): what the phone's OpenGL ES offers, asked
    /// through a throwaway 1x1 pbuffer context before MonoGame makes its own, because the renderer is chosen before the
    /// game object exists (<c>Program.Start</c>).
    /// <para>
    /// The desktop's renderer draws into float colour targets - the HDR scene target and its layers (RGBA16F) and the
    /// sun's shadow map (R32F) - which OpenGL ES 3.2 renders into by itself and 3.0 and 3.1 only with
    /// <c>EXT_color_buffer_float</c>. A phone with neither draws through the Potato path, whose targets are 8-bit. Below
    /// OpenGL ES 3.0 nothing of the desktop's runs (instancing, the effects' GLSL ES 3 context).
    /// </para>
    /// </summary>
    internal static class GpuCaps
    {
        private const string FLOAT_TARGETS = "EXT_color_buffer_float";

        internal static bool DesktopPath(string hidden, out string why)
        {
            why = null;

            string version, extensions;
            try
            {
                (version, extensions) = Ask();
            }
            catch (Exception ex)
            {
                //Unknown is not weak: the desktop path is the default, and a phone that cannot run it says so at the first draw
                Console.WriteLine($"[gpu] could not ask the GPU ({ex.GetType().Name}: {ex.Message}): the desktop's renderer is tried");
                return true;
            }

            //A capability hidden for a test (gpumissing=), so the fallback can be seen firing on a phone that has it
            if (!string.IsNullOrWhiteSpace(hidden))
            {
                foreach (string missing in hidden.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    extensions = extensions.Replace(missing, "");
                    if (version.Contains("OpenGL ES " + missing)) version = "OpenGL ES 3.0 (gpumissing=" + missing + ")";
                }
            }

            Console.WriteLine($"[gpu] {version}; {FLOAT_TARGETS} {(extensions.Contains(FLOAT_TARGETS) ? "yes" : "no")}");

            (int major, int minor) = ParseVersion(version);
            if (major < 3)
            {
                why = $"OpenGL ES {major}.{minor} - the desktop's renderer needs 3.0";
                return false;
            }

            if ((major, minor).CompareTo((3, 2)) < 0 && !extensions.Contains(FLOAT_TARGETS))
            {
                why = $"OpenGL ES {major}.{minor} without {FLOAT_TARGETS} - no float render targets for the desktop's renderer";
                return false;
            }

            return true;
        }

        //"OpenGL ES 3.2 V@0842..." -> (3, 2)
        private static (int, int) ParseVersion(string version)
        {
            int at = version.IndexOf("OpenGL ES ", StringComparison.Ordinal);
            if (at < 0) return (0, 0);
            string[] parts = version.Substring(at + 10).Split('.', ' ');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int a) ? a : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int b) ? b : 0;
            return (major, minor);
        }

        private static (string, string) Ask()
        {
            EGLDisplay display = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);
            int[] eglVersion = new int[2];
            if (!EGL14.EglInitialize(display, eglVersion, 0, eglVersion, 1)) throw new InvalidOperationException("eglInitialize");

            int[] configAttributes = { EGL14.EglRenderableType, EGLExt.EglOpenglEs3BitKhr, EGL14.EglSurfaceType, EGL14.EglPbufferBit, EGL14.EglNone };
            EGLConfig[] configs = new EGLConfig[1];
            int[] found = new int[1];
            if (!EGL14.EglChooseConfig(display, configAttributes, 0, configs, 0, 1, found, 0) || found[0] == 0)
                return ("OpenGL ES 2.0 (no ES 3 configuration)", "");

            int[] contextAttributes = { EGL14.EglContextClientVersion, 3, EGL14.EglNone };
            EGLContext context = EGL14.EglCreateContext(display, configs[0], EGL14.EglNoContext, contextAttributes, 0);
            int[] surfaceAttributes = { EGL14.EglWidth, 1, EGL14.EglHeight, 1, EGL14.EglNone };
            EGLSurface surface = EGL14.EglCreatePbufferSurface(display, configs[0], surfaceAttributes, 0);

            try
            {
                if (!EGL14.EglMakeCurrent(display, surface, surface, context)) throw new InvalidOperationException("eglMakeCurrent");
                return (GLES20.GlGetString(GLES20.GlVersion) ?? "", GLES20.GlGetString(GLES20.GlExtensions) ?? "");
            }
            finally
            {
                //Not eglTerminate: the display is the one MonoGame takes next
                EGL14.EglMakeCurrent(display, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext);
                EGL14.EglDestroySurface(display, surface);
                EGL14.EglDestroyContext(display, context);
            }
        }
    }
}
