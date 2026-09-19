using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace BS3D.Platform
{
    /// <summary>
    /// The pointer the game's front end is clicked with (#350): a stylized arrow <b>built procedurally at
    /// load</b> and published to the window through <see cref="Mouse.SetCursor"/>, in place of the stock
    /// Windows one.
    /// <para>
    /// <b>Why it is generated rather than authored.</b> There is no bitmap to ship, nothing for the content
    /// pipeline to carry into the release zip, and — the part that actually matters — the arrow is sized from
    /// the panel it will be drawn on instead of being one PNG exported at one size. A 32 px cursor authored
    /// for a 1080p screen is a speck on the 4K panel this project treats as its base, and the repository's
    /// standing rule is that the display's own resolution is what everything is framed for. The same reason
    /// puts its colours in code: the menu chrome is deliberately greyscale (see the palette block in
    /// <c>BS3DGame.Menu.cs</c>), and a pointer read out of a file would be the one part of it nobody could
    /// move when that palette does. It is the same argument <c>SurfaceTexture</c>, <c>Crosshair</c> and every
    /// mesh in this project already make.
    /// </para>
    /// <para>
    /// <b>It is greyscale, and that is twice deliberate.</b> The menu has to sit over twenty backdrops whose
    /// palettes are nothing alike, so an accent that reads as the game's own over a neon city fights an ochre
    /// desert — the chrome's rule, and the pointer is chrome. It also makes the bitmap <i>immune to a channel
    /// order mistake</i>: this texture leaves MonoGame as RGBA and reaches Windows as a BGRA GDI bitmap, and a
    /// grey arrow is byte-identical either way round. A coloured one would be the kind of bug that only shows
    /// on someone else's machine.
    /// </para>
    /// <para>
    /// <b>Set once, never per frame.</b> <see cref="Mouse.SetCursor"/> hands the window a GDI cursor handle;
    /// calling it every frame builds and abandons one per frame, which is the documented road to an
    /// intermittent MonoGame crash. It is called exactly once here, from <c>LoadContent</c>, and it stays set
    /// — the pointer's <i>visibility</i> is a separate thing that <see cref="Game.IsMouseVisible"/> owns and
    /// toggles freely (the play loop hides it and draws the procedural <c>Crosshair</c> instead), and hiding
    /// a cursor does not un-choose it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The shape is a signed distance field rather than a rasterized polygon, which is what lets one piece of
    /// arithmetic serve every size: the outline is a band at a fixed distance <i>outside</i> the silhouette,
    /// the shadow is the same field sampled from an offset point and softened over a few pixels, and the
    /// antialiasing is the coverage the distance already states. A supersampled polygon fill would need all
    /// three built separately and would still have to be re-tuned per size.
    /// </remarks>
    internal static class PointerCursor
    {
        //The arrow, in units where its own height is exactly 1: tip at the origin, down the left edge to the
        //spike, into the heel, out along the tail, back up its right side to the shoulder, and home along the
        //long diagonal. Leaner and sharper than the stock Windows arrow, which is the whole "stylized" of it —
        //the proportions are the OS pointer's stretched along Y, so it still reads instantly as a pointer.
        private static readonly Vector2[] ARROW =
        {
            new(0.000f, 0.000f),   //the tip, and the hot spot
            new(0.000f, 0.800f),   //foot of the left edge — the spike
            new(0.178f, 0.622f),   //the heel, where the spike meets the tail
            new(0.300f, 1.000f),   //the tail's left foot
            new(0.487f, 0.919f),   //the tail's right foot
            new(0.369f, 0.563f),   //the tail's right side, back at the body
            new(0.585f, 0.563f),   //the shoulder
        };

        //How tall the arrow stands, as a fraction of the display's height. Chosen against the stock pointer
        //rather than in the abstract: Windows' own arrow is ~20 px tall on a 1080p panel at 100 %, so 0.026
        //(28 px there, 56 on a 4K one) is the "large" the issue asks for without being a novelty.
        private const float HEIGHT_OF_DISPLAY = 0.026f;

        //And the floor and ceiling on that, for the panels the fraction alone would serve badly: a very small
        //display would get a pointer too fine to see, and Windows draws a cursor bitmap at its own size with
        //no say in the matter, so there is no upper limit but good taste.
        private const float MIN_HEIGHT = 26f;
        private const float MAX_HEIGHT = 72f;

        //The dark ring, as a fraction of the height. It sits entirely OUTSIDE the silhouette, so the arrow
        //keeps exactly the proportions above and the ring is what grows — an outline straddling the edge
        //would eat the tail, which is only 0.20 wide.
        private const float OUTLINE_OF_HEIGHT = 0.052f;
        private const float MIN_OUTLINE = 1.4f;

        //The drop shadow: how far down-right it falls, how far it is softened over, and how dark it lands.
        //It is the half of this that makes the pointer readable over a white snowfield AND over a black pit,
        //because the ring alone disappears into the second one.
        private const float SHADOW_OFFSET_OF_HEIGHT = 0.055f;
        private const float SHADOW_BLUR_OF_HEIGHT = 0.075f;
        private const float SHADOW_ALPHA = 0.38f;

        //Greys, in linear-free display space — this is a bitmap Windows composites, not a scene value. The
        //ring is a very dark grey rather than pure black, which reads as ink instead of as a hole; the body
        //runs bright at the tip (where the eye actually is) into the menu's own body grey at the tail.
        private const float OUTLINE_GREY = 0.055f;
        private const float FILL_GREY_TIP = 0.980f;
        private const float FILL_GREY_TAIL = 0.720f;

        //Both held for the life of the process, deliberately. The cursor is what the window is still pointing
        //at, so letting it be collected or disposed would destroy a handle Windows is drawing from; the
        //texture is kept beside it because whether MonoGame reads it again is an implementation detail, and
        //64x64x4 bytes is not a reason to find out the hard way.
        private static MouseCursor _cursor;
        private static Texture2D _texture;

        /// <summary>
        /// Builds the arrow and makes it the window's pointer. Safe to call more than once; everything after
        /// the first call is a no-op, which is what keeps it off any per-frame path by construction.
        /// </summary>
        internal static void Apply(GraphicsDevice device)
        {
            if (_cursor != null || device == null) return;

            //A stock arrow is a cosmetic loss and nothing here is worth a crash on a machine whose shell, GDI
            //or display metrics answer differently — the same judgement WindowIcon makes for the same reason.
            try
            {
                float height = MathHelper.Clamp(
                    GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height * HEIGHT_OF_DISPLAY,
                    MIN_HEIGHT, MAX_HEIGHT);

                float outline = MathF.Max(MIN_OUTLINE, height * OUTLINE_OF_HEIGHT);
                float shadowOffset = height * SHADOW_OFFSET_OF_HEIGHT;
                float shadowBlur = height * SHADOW_BLUR_OF_HEIGHT;

                //Room for the ring above and left of the tip, and for the ring plus the whole shadow below and
                //right of it. Asymmetric on purpose: the shadow only ever falls one way, and a uniform margin
                //would push the hot spot needlessly far into the bitmap.
                int lead = (int)MathF.Ceiling(outline) + 1;
                int trail = (int)MathF.Ceiling(outline + shadowOffset + shadowBlur) + 1;

                //Square, and sized by the height — the arrow is 0.56 as wide as it is tall, so this is the one
                //dimension that has to fit.
                int side = lead + (int)MathF.Ceiling(height) + trail;

                //The tip at the CENTRE of pixel [lead, lead], which is then the hot spot: the point the player
                //believes they are clicking has to be the point the arrow's own tip is drawn at, and half a
                //pixel of that is the most this can be out.
                Vector2 tip = new(lead + 0.5f, lead + 0.5f);

                Color[] pixels = Draw(side, tip, height, outline, shadowOffset, shadowBlur);

                _texture = new Texture2D(device, side, side);
                _texture.SetData(pixels);

                MouseCursor built = MouseCursor.FromTexture2D(_texture, lead, lead);
                Mouse.SetCursor(built);
                _cursor = built;
            }
            catch (Exception)
            {
                //Leaves the stock Windows arrow standing, and leaves _cursor null so nothing here is retried
                //on a path that would then be doing the work every frame.
                _texture?.Dispose();
                _texture = null;
            }
        }

        /// <summary>
        /// The bitmap itself: shadow, then ring, then body, each composited over the one under it in
        /// <b>premultiplied</b> alpha.
        /// </summary>
        /// <remarks>
        /// Premultiplied because that is what Windows' own icon blend wants, and it costs nothing to be right:
        /// the arrow's interior is fully opaque, where the two conventions agree exactly, and everywhere they
        /// disagree — the antialiased rim and the shadow — the colour is nearly black, where they agree again.
        /// So this is correct under the blend Windows performs and would be indistinguishable under the other.
        /// </remarks>
        private static Color[] Draw(int side, Vector2 tip, float height, float outline,
            float shadowOffset, float shadowBlur)
        {
            Color[] pixels = new Color[side * side];

            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    //Pixel centres, in the arrow's own unit space with the tip at its origin
                    Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) - tip) / height;

                    float d = SignedDistance(p, ARROW) * height;

                    //The same silhouette seen from up-left, dilated by the ring so the shadow is cast by what
                    //is actually drawn rather than by the geometry inside it, and softened over a few pixels
                    float shadowDistance =
                        SignedDistance(p - new Vector2(shadowOffset / height), ARROW) * height - outline;
                    float shadow = MathHelper.Clamp(1f - shadowDistance / shadowBlur, 0f, 1f) * SHADOW_ALPHA;

                    //One pixel of linear coverage at each edge: the distance field already states how far in
                    //or out the centre of this pixel is, so no supersampling is needed to find out
                    float ring = MathHelper.Clamp(outline - d + 0.5f, 0f, 1f);
                    float body = MathHelper.Clamp(0.5f - d, 0f, 1f);

                    //Bright where the eye is — at the tip — falling to the menu's body grey down the tail
                    float fill = MathHelper.Lerp(FILL_GREY_TIP, FILL_GREY_TAIL,
                        MathHelper.Clamp(p.Y, 0f, 1f));

                    float a = 0f, c = 0f;
                    Over(ref c, ref a, 0f, shadow);
                    Over(ref c, ref a, OUTLINE_GREY, ring);
                    Over(ref c, ref a, fill, body);

                    byte grey = (byte)MathF.Round(MathHelper.Clamp(c, 0f, 1f) * 255f);
                    byte alpha = (byte)MathF.Round(MathHelper.Clamp(a, 0f, 1f) * 255f);

                    pixels[y * side + x] = new Color(grey, grey, grey, alpha);
                }
            }

            return pixels;
        }

        /// <summary>One source over the accumulator, both premultiplied, one channel because it is grey.</summary>
        private static void Over(ref float grey, ref float alpha, float sourceGrey, float sourceAlpha)
        {
            grey = sourceGrey * sourceAlpha + grey * (1f - sourceAlpha);
            alpha = sourceAlpha + alpha * (1f - sourceAlpha);
        }

        /// <summary>
        /// Distance from <paramref name="p"/> to the polygon's boundary, negative inside it.
        /// </summary>
        /// <remarks>
        /// Distance to the nearest edge for the magnitude, an even-odd crossing count for the sign — so the
        /// winding of <see cref="ARROW"/> does not matter, which is one fewer thing that can be silently
        /// backwards. The joins it produces are round, and that is why the hot spot needs only
        /// <c>outline</c> pixels of room above and left of the tip rather than the much larger reach a mitred
        /// join would have at a 44 degree point.
        /// </remarks>
        private static float SignedDistance(Vector2 p, Vector2[] polygon)
        {
            float nearest = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[j], b = polygon[i];
                Vector2 edge = b - a, offset = p - a;

                float t = MathHelper.Clamp(Vector2.Dot(offset, edge) / Vector2.Dot(edge, edge), 0f, 1f);
                nearest = MathF.Min(nearest, (offset - edge * t).LengthSquared());

                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))
                    inside = !inside;
            }

            return inside ? -MathF.Sqrt(nearest) : MathF.Sqrt(nearest);
        }
    }
}
