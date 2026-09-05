using Microsoft.Xna.Framework;
using System;
using System.Runtime.InteropServices;

namespace BS3D.Platform
{
    /// <summary>
    /// The game's own mouse pointer: a large stylised arrow, drawn procedurally and published on the game's
    /// window (#350). Everywhere the pointer is visible — the main menu, the settings page, the level picker,
    /// the pause page — Windows draws this instead of its own arrow.
    /// <para>
    /// <b>It is drawn rather than authored</b>, which is the house habit for everything that is not a
    /// photograph: <c>SurfaceTexture</c>, every mesh in the game and the HUD's crosshair are procedural, and
    /// an arrow is a seven-point polygon. Nothing to keep in step with a build script, no second copy of the
    /// art, and the size is a constant rather than a re-export — which matters here, because "how big" is
    /// exactly the thing this issue was about.
    /// </para>
    /// <para>
    /// <b>⚠ It is published on the WINDOW and not through <c>Mouse.SetCursor</c>, and that is not a
    /// preference.</b> The first build used MonoGame's own call and the owner still saw the ordinary system
    /// arrow — and what was wrong was <b>not the cursor</b>. Measured on the running game, MonoGame builds a
    /// perfectly well formed one (a 1 bpp AND mask and a 32 bpp colour bitmap, 64×64) and
    /// <c>GetCursorInfo</c> reports that very handle as the showing cursor, complete with the arrow's own
    /// pixels. What it does not survive is the <b>window</b>: WindowsDX hosts the game on a WinForms form, and
    /// a form answers <c>WM_SETCURSOR</c> — sent as the pointer moves over the client area — by asserting
    /// <c>Control.Cursor</c>, which is <c>Cursors.Default</c>. Setting the control's own cursor is what makes
    /// it stick, and it is the same door <see cref="WindowIcon"/> goes through (<c>Control.FromHandle</c>) for
    /// the same reason. Verified from the running game by driving <b>relative</b> mouse movement — sixty
    /// steps of it, which is what a physical mouse sends and what a <c>SetCursorPos</c> teleport does not —
    /// and reading the cursor back: it stays the arrow throughout.
    /// </para>
    /// <para>
    /// The handle is built here rather than borrowed: <c>CreateIconIndirect</c> over a 32 bpp colour DIB and a
    /// 1 bpp mask, which is exactly the shape MonoGame's own cursor turned out to have — so nothing is lost by
    /// owning it, and what the window is handed is then a plain <c>HCURSOR</c> with no library between.
    /// <see cref="GameplayScreen"/> is untouched by all of this: it hides the pointer (<c>IsMouseVisible</c>,
    /// three writers, see <see cref="BS3DGame"/>) and draws its own procedural crosshair.
    /// </para>
    /// </summary>
    internal static class StylizedPointer
    {
        //The texture's side, in pixels. Windows' own arrow is 32 across and draws about 20 of that; at 64
        //this is a little over three times the pointer the desktop hands out, which is the "much bigger"
        //the request asked for while still leaving the tip a point rather than a wedge.
        private const int SIZE = 64;

        //How much of the texture the arrow's own height fills. The rest is the shadow's room: the drop is
        //offset down and right, so an arrow drawn to the very edge would have its shadow clipped.
        private const float ARROW_HEIGHT = 0.88f;

        //The classic pointer silhouette in arrow-local units, y down, the tip at the origin: down the left
        //edge, in to the notch, out along the tail, back up its right side and home along the shoulder. The
        //proportions are the ones every desktop uses, because a pointer that reads as anything else costs
        //the player a beat working out what it is.
        private static readonly Vector2[] ARROW =
        {
            new(0.00f, 0.00f),
            new(0.00f, 1.00f),
            new(0.24f, 0.76f),
            new(0.40f, 1.14f),
            new(0.57f, 1.07f),
            new(0.41f, 0.70f),
            new(0.72f, 0.66f),
        };

        //The dark edge, in pixels of the texture, and it is what makes the arrow readable over ANY of the
        //seventeen scenes: the front end's backdrop is a lit 3D arena that can be a bright desert or a night
        //city under the same pointer, and a white arrow alone disappears into the first while a dark one
        //disappears into the second.
        private const float OUTLINE = 2.6f;

        //The shadow's offset and how far it fades over. It is the only thing here that is not silhouette,
        //and it lifts the arrow off a busy backdrop - but the READABILITY rests on the outline above and
        //deliberately not on this. The alpha it needs is carried properly (BuildCursor premultiplies it into
        //a 32 bpp DIB), and the 1 bpp mask beside it means the arrow still reads as an arrow on any path that
        //ignores alpha altogether - a remote session, an old driver - where the shadow is simply not there.
        private static readonly Vector2 SHADOW_OFFSET = new(2.5f, 3.0f);
        private const float SHADOW_FADE = 3.5f;
        private const float SHADOW_ALPHA = 0.45f;

        //The fill, top to bottom: MENU_TEXT's near-white cooled slightly towards the silver the star tier
        //uses, so the pointer belongs to the same palette as the type it clicks on.
        private static readonly Vector3 FILL_TOP = new(0.96f, 0.96f, 0.97f);
        private static readonly Vector3 FILL_BOTTOM = new(0.74f, 0.79f, 0.86f);
        private static readonly Vector3 OUTLINE_COLOR = new(0.07f, 0.08f, 0.11f);

        //The arrow's own cursor handle and the WinForms wrapper the window is holding, both kept for the life
        //of the process. They are deliberately never destroyed, exactly as WindowIcon's two icon handles are
        //not: the window draws from this for as long as it lives, there is one game per process, and building
        //it again per resize would be work for nothing.
        private static IntPtr _handle;
        private static object _cursor;

        /// <summary>
        /// Builds the arrow and puts it on the window. Safe to call more than once — it rasterises on the
        /// first call only, so a resize or a second host cannot spend the rasteriser twice.
        /// </summary>
        /// <param name="windowHandle">The game window's <c>HWND</c> (<c>Game.Window.Handle</c>).</param>
        internal static void Publish(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return;

            //The hot spot is the tip, which is the point of a pointer: the polygon's first vertex, at the
            //arrow's own origin
            if (_handle == IntPtr.Zero) _handle = BuildCursor(Rasterize(), 0, 0);
            if (_handle == IntPtr.Zero) return;

            Apply(windowHandle);
        }

        /// <summary>
        /// States the pointer again, for the events that can put the window's own cursor back: a fullscreen
        /// toggle, a resize, a window the platform has recreated under the game. Never rasterises, and is not
        /// a per-frame path.
        /// </summary>
        internal static void Reassert(IntPtr windowHandle)
        {
            if (_handle != IntPtr.Zero) Apply(windowHandle);
        }

        //Hands the built handle to the window's own WinForms control, which is the only place it sticks: the
        //form answers WM_SETCURSOR by asserting Control.Cursor, so anything set underneath it lasts exactly
        //until the pointer next moves over the client area (measured - see the class remarks).
        private static void Apply(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return;

            System.Windows.Forms.Control control = System.Windows.Forms.Control.FromHandle(windowHandle);
            if (control == null) return;

            _cursor ??= new System.Windows.Forms.Cursor(_handle);

            control.Cursor = (System.Windows.Forms.Cursor)_cursor;
        }

        /// <summary>
        /// Turns the rasterised arrow into a Windows cursor handle: a 32 bpp top-down colour DIB carrying
        /// <b>premultiplied</b> BGRA, and a 1 bpp AND mask beside it.
        /// <para>
        /// Both halves are what they are for a reason. The colour is premultiplied because that is what
        /// Windows composites an alpha cursor from — straight alpha comes out with a bright halo around every
        /// edge pixel. The mask is a real silhouette rather than the all-zero one an alpha cursor strictly
        /// needs, so that a path which ignores the alpha (a remote session, a driver that draws cursors the
        /// old way) still gets an arrow-shaped cursor instead of a black tile.
        /// </para>
        /// </summary>
        private static IntPtr BuildCursor(Color[] pixels, int hotspotX, int hotspotY)
        {
            BITMAPINFO info = default;
            info.Size = Marshal.SizeOf<BITMAPINFO>();
            info.Width = SIZE;
            info.Height = -SIZE;   //negative: top-down, so row 0 is the top one, as the rasteriser wrote it
            info.Planes = 1;
            info.BitCount = 32;
            info.Compression = 0;  //BI_RGB

            IntPtr colour = CreateDIBSection(IntPtr.Zero, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (colour == IntPtr.Zero) return IntPtr.Zero;

            //Eight bytes a row at 64 wide, which is already the WORD alignment CreateBitmap wants
            const int MASK_STRIDE = SIZE / 8;

            byte[] bgra = new byte[SIZE * SIZE * 4];
            byte[] mask = new byte[SIZE * MASK_STRIDE];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                float alpha = pixel.A / 255f;

                bgra[i * 4 + 0] = (byte)(pixel.B * alpha);
                bgra[i * 4 + 1] = (byte)(pixel.G * alpha);
                bgra[i * 4 + 2] = (byte)(pixel.R * alpha);
                bgra[i * 4 + 3] = pixel.A;

                //A set bit is a TRANSPARENT one in an AND mask: the destination shows through there
                if (pixel.A < 128) mask[i / SIZE * MASK_STRIDE + i % SIZE / 8] |= (byte)(0x80 >> (i % 8));
            }

            Marshal.Copy(bgra, 0, bits, bgra.Length);

            IntPtr maskBitmap = CreateBitmap(SIZE, SIZE, 1, 1, mask);

            ICONINFO iconInfo = new()
            {
                IsIcon = false,   //a cursor, so the hot spot below is meaningful
                HotspotX = hotspotX,
                HotspotY = hotspotY,
                MaskBitmap = maskBitmap,
                ColorBitmap = colour,
            };

            IntPtr cursor = CreateIconIndirect(ref iconInfo);

            //CreateIconIndirect copies both bitmaps, so the originals are ours to release; the cursor itself
            //is kept for the life of the process (see the fields)
            if (maskBitmap != IntPtr.Zero) DeleteObject(maskBitmap);
            DeleteObject(colour);

            return cursor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public int Size, Width, Height;
            public short Planes, BitCount;
            public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;

            //The colour table a 32 bpp BI_RGB bitmap does not use, present so the struct is the size the API
            //reads it at
            public int Reserved0, Reserved1, Reserved2, Reserved3;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool IsIcon;
            public int HotspotX, HotspotY;
            public IntPtr MaskBitmap, ColorBitmap;
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO info, uint usage,
            out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitsPerPixel,
            byte[] bits);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO iconInfo);

        /// <summary>
        /// The arrow as pixels. Everything is decided by the <b>signed distance</b> to the silhouette rather
        /// than by a scanline fill, which is what antialiases the tip and the notch for free: the edge is a
        /// smoothstep over one pixel of that distance, so the diagonals come out clean at any size this
        /// constant is set to.
        /// </summary>
        private static Color[] Rasterize()
        {
            Color[] pixels = new Color[SIZE * SIZE];

            float scale = SIZE * ARROW_HEIGHT / 1.14f;

            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    //Sampled at the pixel's centre, in arrow units
                    Vector2 point = new((x + 0.5f) / scale, (y + 0.5f) / scale);

                    float distance = SignedDistance(point) * scale;
                    float shadowDistance = SignedDistance(point - SHADOW_OFFSET / scale) * scale;

                    //Three coverages, each a one-pixel ramp: the body, the outline that rings it, and the
                    //shadow that fades out under both
                    float body = Coverage(distance);
                    float ringed = Coverage(distance - OUTLINE);
                    float shadow = MathHelper.Clamp(1f - (shadowDistance - OUTLINE) / SHADOW_FADE, 0f, 1f) * SHADOW_ALPHA;

                    float gradient = MathHelper.Clamp((y + 0.5f) / (SIZE * ARROW_HEIGHT), 0f, 1f);
                    Vector3 fill = Vector3.Lerp(FILL_TOP, FILL_BOTTOM, gradient);

                    //Composited in one place, front to back: the body over the outline over the shadow. The
                    //alpha is what the three coverages add up to, and the colour is the front-most of them.
                    Vector3 colour = Vector3.Lerp(OUTLINE_COLOR, fill, body);
                    float alpha = MathHelper.Clamp(ringed + shadow * (1f - ringed), 0f, 1f);

                    //Left NON-premultiplied: MonoGame hands the texture's bytes to the platform cursor as
                    //straight RGBA, so multiplying the colour by the alpha here would darken every edge
                    //pixel of the arrow into a grey fringe.
                    pixels[y * SIZE + x] = new Color(colour.X, colour.Y, colour.Z, alpha);
                }

            return pixels;
        }

        //One pixel of ramp across the silhouette's edge, centred on it: 1 well inside, 0 well outside
        private static float Coverage(float distance) => MathHelper.Clamp(0.5f - distance, 0f, 1f);

        /// <summary>
        /// Distance from a point to the arrow's outline, negative inside it. The winding test and the
        /// distance are walked together over the same edges, which is the whole of a polygon SDF.
        /// </summary>
        private static float SignedDistance(Vector2 point)
        {
            float squared = float.MaxValue;
            bool inside = false;

            for (int i = 0, previous = ARROW.Length - 1; i < ARROW.Length; previous = i++)
            {
                Vector2 a = ARROW[previous];
                Vector2 b = ARROW[i];

                Vector2 edge = b - a;
                Vector2 offset = point - a;

                //The nearest point on this edge, clamped to the segment
                float t = MathHelper.Clamp(Vector2.Dot(offset, edge) / Vector2.Dot(edge, edge), 0f, 1f);
                Vector2 nearest = offset - edge * t;

                squared = MathF.Min(squared, Vector2.Dot(nearest, nearest));

                //The standard crossing test, on the same pair of vertices
                if ((a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < a.X + (point.Y - a.Y) / (b.Y - a.Y) * edge.X)
                    inside = !inside;
            }

            return inside ? -MathF.Sqrt(squared) : MathF.Sqrt(squared);
        }
    }
}
