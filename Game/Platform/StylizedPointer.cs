using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace BS3D.Platform
{
    /// <summary>
    /// The game's own mouse pointer: a large stylised arrow, drawn procedurally and published once as the
    /// hardware cursor (#350). Everywhere the pointer is visible — the main menu, the settings page, the
    /// level picker, the pause page — Windows draws this instead of its own arrow.
    /// <para>
    /// <b>It is drawn rather than authored</b>, which is the house habit for everything that is not a
    /// photograph: <c>SurfaceTexture</c>, every mesh in the game and the HUD's crosshair are procedural, and
    /// an arrow is a seven-point polygon. Nothing to keep in step with a build script, no second copy of the
    /// art, and the size is a constant rather than a re-export — which matters here, because "how big" is
    /// exactly the thing this issue was about.
    /// </para>
    /// <para>
    /// <b>The cursor is set ONCE.</b> <c>Mouse.SetCursor</c> per frame is a documented source of intermittent
    /// crashes in MonoGame, and there is nothing to re-set: the pointer's own visibility is
    /// <c>Game.IsMouseVisible</c>'s business (three writers, see <see cref="BS3DGame"/>), and hiding a cursor
    /// does not forget which one it is. <see cref="GameplayScreen"/> is untouched by all of this — it hides
    /// the pointer and draws its own procedural crosshair.
    /// </para>
    /// <para>
    /// It stays in the executable next to <see cref="WindowIcon"/> for the same reason that does: it is the
    /// OS's idea of this window, and the libraries target plain <c>net10.0</c>.
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
        //deliberately not on this: how faithfully Windows composites a cursor's partial alpha varies with
        //the path the handle was made through, and the capture rig used to verify this (DrawIconEx onto a
        //GDI DC) draws a cursor through its MASK, so a soft edge is not visible there either way. The arrow
        //is built to read with the shadow reduced to nothing, and it does.
        private static readonly Vector2 SHADOW_OFFSET = new(2.5f, 3.0f);
        private const float SHADOW_FADE = 3.5f;
        private const float SHADOW_ALPHA = 0.45f;

        //The fill, top to bottom: MENU_TEXT's near-white cooled slightly towards the silver the star tier
        //uses, so the pointer belongs to the same palette as the type it clicks on.
        private static readonly Vector3 FILL_TOP = new(0.96f, 0.96f, 0.97f);
        private static readonly Vector3 FILL_BOTTOM = new(0.74f, 0.79f, 0.86f);
        private static readonly Vector3 OUTLINE_COLOR = new(0.07f, 0.08f, 0.11f);

        private static bool _published;

        /// <summary>
        /// Builds the arrow and hands it to Windows. Safe to call more than once — it does the work on the
        /// first call only, so a device reset or a second host cannot spend the rasteriser twice.
        /// </summary>
        internal static void Publish(GraphicsDevice device)
        {
            if (_published || device == null) return;

            _published = true;

            //The whole cursor is one Texture2D the size of SIZE, and it is deliberately NOT disposed: the
            //cursor Windows is drawing owns it for the life of the process, exactly as WindowIcon's handles
            //own theirs, and there is one game per process.
            Texture2D texture = new(device, SIZE, SIZE);

            texture.SetData(Rasterize());

            //The hot spot is the tip, which is the point of a pointer: the polygon's first vertex, at the
            //arrow's own origin, in texture pixels.
            Mouse.SetCursor(MouseCursor.FromTexture2D(texture, 0, 0));
        }

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
