using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;

namespace BS3D.Effects
{
    /// <summary>
    /// The cup the player just won (#183): a trophy presented close to the lens on the result screen, turning
    /// and swaying, in one of four tiers taken from the level's star rating — Bronze, Silver, Gold and, at the
    /// top, a Diamond cup with handles.
    /// <para>
    /// It is the third beat of an ending that already had two. The fanfare states the win, the fireworks
    /// answer it <c>CELEBRATION_DELAY</c> later, and the result page arrives with the numbers; what none of
    /// them did was say <b>how well</b> the player did in anything but a row of stars and a figure. A cup is
    /// the same information as an object, and an object can be handed to somebody.
    /// </para>
    /// <para>
    /// <b>It is placed against the frame, not against the world.</b> The result page releases the camera onto
    /// a slow orbit around the island, so anything left standing in the arena swings out of shot within a few
    /// seconds and spends the rest of the ending behind the score panel. Anchored to the lens it stays exactly
    /// where it was put while the whole arena turns behind it, which is what reads as the cup being
    /// <i>presented</i> rather than merely being somewhere. Where it sits is stated in <b>normalised device
    /// coordinates</b> and turned into a world offset through the camera's own projection, so it holds its
    /// place in the frame at any field of view and any aspect ratio rather than drifting off the side of an
    /// ultrawide.
    /// </para>
/// <para>
/// <b>It holds focus while the rest of the frame goes soft</b> (#225, reversing this class's own first
/// answer, which had the cup soften with the arena and called that deliberate). It is drawn into the
/// pipeline's sharp foreground layer rather than the HDR scene, so the result page's defocus melts the
/// arena, the fireworks and the confetti into bokeh <b>behind</b> the cup while the cup itself stays
/// crisp — #178's argument is that the ending is watched first and softened afterwards, and the cup is
/// the thing being watched. The move changes nothing about its light: the layer holds linear radiance
/// like the scene, the composite pass tonemaps it through the same exposure, ACES curve and film grain,
/// and its bright pass still feeds the bloom pyramid — so "extremely shiny" still costs nothing to get
/// here, and the metal keeps the glare its finishes were tuned against.
/// </para>
    /// </summary>
    public sealed class TrophyPodium : IDisposable
    {
        /// <summary>The rating that earns each cup. Index 0 is unused — a level with no stars was not cleared.</summary>
        public const int TIERS = 4;

        //WHERE IT SITS IN THE FRAME, in normalised device coordinates: -1 is the left edge and the bottom, +1
        //the right and the top. Left of centre and a little low, which is the one part of the result page that
        //stays clear — the heading, the stars, the breakdown panel and the three buttons are all centred, and
        //the arena's own horizon sits across the middle.
        private const float NDC_X = -0.60f, NDC_Y = -0.22f;

        //How far in front of the lens. Near enough to be the biggest thing in the frame, far enough that the
        //gun the camera is still sitting behind on the first frames of the page cannot intersect it.
        private const float DISTANCE = 3.1f;

        //World height of the cup at rest. Read against DISTANCE and the NDC placement rather than on its own:
        //at a 45-degree vertical field this fills about a third of the frame's height.
        private const float SIZE = 1.25f;

        //The reveal. Shorter than the camera's release (ORBIT_EASE_SECONDS, 2.5) so the cup has arrived by the
        //time the lens stops moving, and far shorter than the defocus delay, so it is fully formed and holding
        //the frame to itself for a good two seconds before the arena starts going soft behind it.
        private const float REVEAL_SECONDS = 0.9f;

        //THE DANCE. A slow turn so every side of the cup is seen and the handles read as handles, a bob, and a
        //lean that PRECESSES rather than swinging in one plane — the lean is applied about the cup's own Z and
        //the turn about its Y afterwards, so the tilt travels around the axis instead of rocking like a
        //metronome. A cup that merely spins reads as a menu prop; the wobble is what makes it look held up.
        private const float SPIN_RATE = 0.85f;          //radians a second
        private const float BOB_RATE = 1.9f, BOB_DEPTH = 0.055f;
        private const float LEAN_RATE = 0.7f, LEAN_ANGLE = 0.085f;

        //How hard the cup lands. The scale overshoots and settles rather than easing flatly to one: an object
        //presented to a player is thrown up rather than faded in, and a fifth over is the difference between
        //"it appeared" and "here".
        private const float OVERSHOOT = 0.20f;

        private readonly TrophyMesh _plainMesh, _handledMesh;
        private readonly InstancedModelRenderer[] _renderers = new InstancedModelRenderer[TIERS + 1];
        private readonly BasicEffectParams[] _materials = new BasicEffectParams[TIERS + 1];

        private int _tier;              //0 = nothing to show
        private float _reveal;          //0..1
        private float _clock;           //the dance's own clock, free-running while a cup is up

        /// <summary>True while a cup is being shown, so a caller can skip the draw entirely.</summary>
        public bool Active => _tier > 0;

        /// <param name="ambientIntensity">
        /// The scene's flat ambient fill, the figure the rest of the setting is drawn with. Handed in rather
        /// than assumed, so the cup sits in the same light as the island it is presented over.
        /// </param>
        public TrophyPodium(GraphicsDevice device, Effect instancingEffect, float ambientIntensity)
        {
            _plainMesh = new TrophyMesh(device, handles: false);
            _handledMesh = new TrophyMesh(device, handles: true);

            Vector3 ambient = Vector3.One * ambientIntensity;

            //THE FOUR TIERS. Every one is drawn on the METAL path (Metalness = 1) with the sky reflected at
            //full strength, which is the funnel's gold rims' setup and the reason a cup here looks like metal
            //rather than like coloured plastic: a metal's reflectance IS its specular colour, so bronze
            //reflects the dome in bronze and silver in white. The diffuse is what holds the tier apart under a
            //dark dome, the specular is what does it under a bright one, and both are stated per tier because
            //either alone fails in one of the game's thirteen scenes.
            //
            //The specular POWER climbs with the tier as well, which is most of what says "better": a bronze
            //cup is a cast, slightly rough thing with a broad highlight, and a diamond one is polished to a
            //point. Nothing about the geometry changes between the first three — only the finish.

            //A METAL'S DIFFUSE IS DARK, and the first version of these got that wrong in a way worth recording:
            //authored at the diffuse a painted surface would take (0.66 for the gold) and reflecting the sky at
            //full strength on top, every cup came out of the tonemap as flat pale plastic — a bright, even,
            //shadowless shape with no highlight anywhere on it, because the diffuse alone was already near the
            //top of the curve and the reflection pushed it over. Metals have almost no diffuse; what colours
            //them is their REFLECTANCE. So the diffuse is roughly a third of what it was and carries only
            //enough to hold the tier apart under a dark dome, the specular carries the hue, and the sky
            //reflection is dialled back off full so there is somewhere left for a highlight to be brighter than.
            //
            //The specular POWER climbs with the tier, which is most of what says "better": a bronze cup is a
            //cast, slightly rough thing with a broad highlight, and a diamond one is polished to a point.

            //Bronze: a cast, warm, slightly rough metal.
            AddTier(device, instancingEffect, 1, _plainMesh,
                diffuse: new Vector3(0.330f, 0.170f, 0.070f),
                specular: new Vector3(0.85f, 0.52f, 0.28f), power: 45f,
                specularAmbient: 0.20f, emissive: Vector3.Zero, ambient);

            //Silver: neutral, and the tightest highlight of the three plain cups.
            AddTier(device, instancingEffect, 2, _plainMesh,
                diffuse: new Vector3(0.400f, 0.415f, 0.450f),
                specular: new Vector3(0.88f, 0.90f, 0.95f), power: 95f,
                specularAmbient: 0.24f, emissive: Vector3.Zero, ambient);

            //Gold: the funnel rims' hue, which is the one metal this game has already tuned against every
            //dome — no reason to invent a second one — but at a metal's diffuse rather than a band's.
            AddTier(device, instancingEffect, 3, _plainMesh,
                diffuse: new Vector3(0.470f, 0.320f, 0.080f),
                specular: new Vector3(0.98f, 0.78f, 0.40f), power: 80f,
                specularAmbient: 0.24f, emissive: Vector3.Zero, ambient);

            //Diamond: the top tier, and the only one that is not simply a better metal. It takes the HANDLED
            //mesh, so it is told apart by its SHAPE before any colour has been read — which matters because
            //the three below it differ only in hue, and hue is the first thing a dark scene takes away. It
            //carries a small emissive term as well, so it is the one cup that is faintly a light source rather
            //than only a reflector; small, because the first pass had it at three times this and the cup came
            //out of the glare pass as a white blob with no shape left in it at all.
            AddTier(device, instancingEffect, 4, _handledMesh,
                diffuse: new Vector3(0.300f, 0.470f, 0.560f),
                specular: new Vector3(0.96f, 0.99f, 1.00f), power: 150f,
                specularAmbient: 0.30f, emissive: new Vector3(0.020f, 0.045f, 0.060f), ambient);
        }

        private void AddTier(GraphicsDevice device, Effect effect, int tier, TrophyMesh mesh,
            Vector3 diffuse, Vector3 specular, float power, float specularAmbient, Vector3 emissive, Vector3 ambient)
        {
            _renderers[tier] = new InstancedModelRenderer(device, mesh, diffuse, effect)
            {
                Metalness = 1f,
                SpecularAmbientStrength = specularAmbient
            };

            _materials[tier] = new BasicEffectParams(ambient, specular, power, emissive);
        }

        /// <summary>
        /// Show the cup for a rating. Called from the result page's <c>Enter</c>, so a retry that earns a
        /// different rating presents a different cup — the reveal restarts with the page, exactly as the star
        /// row's does. A rating of zero (or a level that was lost) shows nothing.
        /// </summary>
        public void Present(int stars)
        {
            int tier = Math.Clamp(stars, 0, TIERS);

            //Restarted rather than continued even when the tier is unchanged: landing back on this page is a
            //new ending and owes the player the cup arriving again, not one already sitting there.
            _tier = tier;
            _reveal = 0f;
            _clock = 0f;
        }

        /// <summary>Takes the cup away at once. Called when a level is built and when the session is torn down.</summary>
        public void Hide() => _tier = 0;

        /// <summary>Advances the reveal and the dance. A no-op while nothing is being shown.</summary>
        public void Update(float elapsed)
        {
            if (_tier <= 0) return;

            _clock += elapsed;
            _reveal = MathF.Min(1f, _reveal + elapsed / REVEAL_SECONDS);
        }

        /// <summary>
        /// Draws the cup, placed against the frame rather than against the world.
        /// </summary>
        /// <remarks>
        /// The offset is derived from the camera's own <b>projection</b> rather than from a viewport or a
        /// stored aspect ratio: at a distance <c>d</c> the frame's half-height is <c>d / M22</c> and its
        /// half-width <c>d / M11</c>, whatever the field of view and whatever the window shape. So the cup
        /// holds its place in the composition on a 4:3 laptop panel and on an ultrawide alike, and it survives
        /// the field of view being changed by the release without a line of code knowing about it.
        /// </remarks>
        public void Draw(ICamera camera)
        {
            if (_tier <= 0) return;

            Matrix view = camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);
            Vector3 forward = -new Vector3(view.M13, view.M23, view.M33);

            Matrix projection = camera.Projection;
            float halfHeight = DISTANCE / projection.M22;
            float halfWidth = DISTANCE / projection.M11;

            //Smoothstep on the reveal, then the overshoot: the scale passes one and comes back, which is what
            //makes it land rather than arrive. Sin(pi*t) is zero at both ends, so nothing has to be clamped
            //and the cup is exactly SIZE once the reveal is done.
            float eased = MathHelper.SmoothStep(0f, 1f, _reveal);
            float scale = SIZE * eased * (1f + OVERSHOOT * MathF.Sin(MathF.PI * eased));

            //And it rises into its place, from a little under it. Tied to the same eased value, so there is
            //one motion rather than two that can disagree about when they finished.
            float rise = (1f - eased) * -0.45f;
            float bob = MathF.Sin(_clock * BOB_RATE) * BOB_DEPTH * eased;

            Vector3 position = camera.Position
                + forward * DISTANCE
                + right * (NDC_X * halfWidth)
                + up * (NDC_Y * halfHeight + rise + bob);

            float spin = _clock * SPIN_RATE;
            float lean = MathF.Sin(_clock * LEAN_RATE) * LEAN_ANGLE * eased;

            //Centred on its own middle before anything turns it, or the cup would swing around its foot like
            //a hammer rather than turning on the spot.
            Matrix world =
                Matrix.CreateTranslation(0f, -TrophyMesh.HEIGHT * 0.5f, 0f)
                * Matrix.CreateScale(scale)
                * Matrix.CreateRotationZ(lean)
                * Matrix.CreateRotationY(spin)
                * Matrix.CreateTranslation(position);

            _renderers[_tier].Draw(camera, world, _materials[_tier]);
        }

        public void Dispose()
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i]?.Dispose();

            _plainMesh?.Dispose();
            _handledMesh?.Dispose();
        }
    }
}
