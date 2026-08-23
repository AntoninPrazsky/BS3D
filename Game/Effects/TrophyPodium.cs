using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// The cup the player just won (#183): a trophy presented close to the lens on the result screen, turning
    /// and swaying, in one of four tiers taken from the level's star rating — a plain Bronze and Silver, then
    /// a Gold and, at the top, a Diamond cup, both <b>with handles</b> (Gold's own since #232), Diamond alone
    /// <b>crystal</b> rather than a fourth metal (#228). It reflects the actual dome the level is played
    /// under, the same as the cannon (<see cref="Renderers"/>, #232's second half) — the owner's own call,
    /// on the argument that the cup stands over the very scene it was won in and ought to match it, unlike
    /// the front end's wordmark, which stands over whichever of the fourteen scenes happens to be rolling.
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
/// <para>
/// <b>And the top tier is see-through</b> (#228). The layer is composited over the resolved frame through
/// premultiplied alpha, so a cup that writes an alpha below one lets the frame <i>underneath</i> through —
/// and what is underneath, on this page, is the defocused arena with its fireworks and its confetti. The
/// crystal therefore costs nothing beyond its own draw: the bokeh the result page was already building
/// shows through the bowl, moving. It is also why a translucent tier had to wait for #225's layer to
/// exist — inside the HDR pass the cup would have been transparent to a scene that was about to be
/// blurred <i>with</i> it.
/// </para>
    /// </summary>
    public sealed class TrophyPodium : IDisposable
    {
        /// <summary>The rating that earns each cup. Index 0 is unused — a level with no stars was not cleared.</summary>
        public const int TIERS = 4;

        //WHERE IT SITS IN THE FRAME, in normalised device coordinates: -1 is the left edge and the bottom, +1
        //the right and the top. Left of centre, because the heading, the stars, the breakdown panel and the
        //three buttons are all centred, and since #226 the cup is TALL ENOUGH TO REACH INTO THEM — a reward
        //does not apologise for its size, and the one that runs right up to the page it sits on reads as
        //handed over rather than exhibited in a case beside it.
        //
        //RAISED IN #233, and that reverses #226's own deliberate move: it went -0.22 to -0.30 there, low on
        //purpose. The owner played it and asked for it higher and centred on the left, so the ruling stands over
        //the reasoning. Note what this anchor MEANS before changing it: the world matrix below pre-translates by
        //half the cup's own height so it turns on the spot, so this is the cup's CENTRE, and 0 would be literally
        //"vertically centred in the frame".
        //
        //IT IS NOT 0, AND THAT IS MEASURED RATHER THAN TASTE. At 0 the bowl is cut by the top edge at the
        //dolly's NEAR end — photographed, against a dark sky so the cup could not be confused with the
        //backdrop. The cup is simply too tall to be centred at that end: SIZE 2.0 against the frame's half
        //height at DISTANCE - DOLLY_DEPTH leaves nothing over, and the LEAN_ANGLE swings the rim higher still.
        //So the old -0.30 was not arbitrary, it was protecting the bowl by cutting the PLINTH instead — and a
        //cut bowl is worse, the bowl being what makes a cup a cup.
        //
        //-0.15 is the highest that survives the whole dolly period: verified over six captures across its
        //7.85 s, in space (a dark sky) so a clipped rim could not hide in a bright one. Half the way to true
        //centre, and as centred as the cup's own size allows without shrinking it or shortening the dolly —
        //which are the two things full centring would cost, and the owner's to spend, not mine.
        //
        //Vertically this is aspect-independent by construction: the anchor is scaled by the frame's half
        //HEIGHT, so an ultrawide adds width and cannot re-open the question above. The bowl running off the
        //LEFT edge at the near end is #226's own choice and predates this.
        private const float NDC_X = -0.60f, NDC_Y = -0.15f;

        //How far in front of the lens the dolly is CENTRED. The old clearance reason (the gun still in frame
        //on the first seconds) died with #225's own layer: the cup is composited over everything, so there
        //is no depth it can intersect. What the distance does now is centre the approach below.
        private const float DISTANCE = 3.1f;

        //THE DOLLY (#226): the cup genuinely approaches and recedes — the DISTANCE moves, the world matrix
        //is not scaled to fake it — and because the NDC anchor below is worked out from the live distance,
        //the cup holds its place in the composition while its perspective changes, which is exactly what a
        //real dolly-in does. Three quarters of a unit either way: at the near end the cup is most of the
        //frame's height and its handles swing wide of the score panel's edge, at the far end it is still
        //nearly half of it, and the swing between them is the "here it is" of the presentation repeating
        //for as long as the page is up. Slow against the dance, so the two motions read as one object
        //doing two things rather than a camera shake.
        private const float DOLLY_RATE = 0.8f, DOLLY_DEPTH = 0.75f;

        //World height of the cup at rest. Read against DISTANCE and the NDC placement rather than on its own:
        //at the result page's released field of view this stands about half the frame's height at the
        //dolly's centre, and the dolly carries it from nearly half to three quarters and back.
        private const float SIZE = 2.0f;

        //The reveal. Shorter than the camera's release (ORBIT_EASE_SECONDS, 2.5) so the cup has arrived by the
        //time the lens stops moving, and far shorter than the defocus delay, so it is fully formed and holding
        //the frame to itself for a good two seconds before the arena starts going soft behind it.
        private const float REVEAL_SECONDS = 0.9f;

        //THE DANCE. A turn so every side of the cup is seen and the handles read as handles, a bob, and
        //a lean that PRECESSES rather than swinging in one plane — the lean is applied about the cup's own Z and
        //the turn about its Y afterwards, so the tilt travels around the axis instead of rocking like a
        //metronome. A cup that merely spins reads as a menu prop; the wobble is what makes it look held up.
        //#226 turned all of it up: this is a game's reward moment, not a catalogue, and the lean that was
        //5 degrees is 17, the turn is half again as fast, and the dolly above is the fourth motion. Extreme
        //is the brief.
        private const float SPIN_RATE = 1.3f;          //radians a second
        private const float BOB_RATE = 1.7f, BOB_DEPTH = 0.11f;
        private const float LEAN_RATE = 0.9f, LEAN_ANGLE = 0.30f;

        //How hard the cup lands. The scale overshoots and settles rather than easing flatly to one: an object
        //presented to a player is thrown up rather than faded in, and over a third extra is the difference
        //between "it appeared" and "HERE".
        private const float OVERSHOOT = 0.35f;

        private readonly GraphicsDevice _device;
        private readonly TrophyMesh _plainMesh, _handledMesh;
        private readonly InstancedModelRenderer[] _renderers = new InstancedModelRenderer[TIERS + 1];
        private readonly BasicEffectParams[] _materials = new BasicEffectParams[TIERS + 1];

        //Which tiers are glass, so Draw can state the states each wants. Filled by AddTier off the alpha it
        //was given, rather than being a second list of the same fact.
        private readonly bool[] _translucent = new bool[TIERS + 1];

        private int _tier;              //0 = nothing to show
        private float _reveal;          //0..1
        private float _clock;           //the dance's own clock, free-running while a cup is up

        /// <summary>True while a cup is being shown, so a caller can skip the draw entirely.</summary>
        public bool Active => _tier > 0;

        /// <summary>
        /// The four tiers' renderers, all four whether the cup is presenting or not — for
        /// <see cref="BS3DGame"/>'s <c>SkyLitRenderers</c> enrolment (#232's second half). The cup is built
        /// well before a level is ever won, so this is not "only the one showing": every tier has to carry the
        /// current dome's light rig for the moment its own turn comes, the same reason the balls' whole
        /// palette is enrolled rather than only the ones a map happens to use. Index 0 is skipped — see
        /// <see cref="TIERS"/>.
        /// </summary>
        public IEnumerable<InstancedModelRenderer> Renderers
        {
            get
            {
                for (int tier = 1; tier <= TIERS; tier++) yield return _renderers[tier];
            }
        }

        /// <param name="ambientIntensity">
        /// The engine's flat ambient fill figure (<c>SCENE_AMBIENT_INTENSITY</c>), the same constant the rest
        /// of the setting is drawn with. Handed in rather than assumed, so a retune of that figure reaches the
        /// cup too.
        /// </param>
        public TrophyPodium(GraphicsDevice device, Effect instancingEffect, float ambientIntensity)
        {
            _device = device;
            _plainMesh = new TrophyMesh(device, handles: false);
            _handledMesh = new TrophyMesh(device, handles: true);

            Vector3 ambient = Vector3.One * ambientIntensity;

            //THE FOUR TIERS. Every one is drawn on the METAL path (Metalness = 1), which is the funnel's gold
            //rims' setup and the reason a cup here looks like metal rather than like coloured plastic: a
            //metal's reflectance IS its specular colour, so bronze reflects its environment in bronze and
            //silver in white. The diffuse is what holds the tier apart under a dark reflection, the specular
            //is what does it under a bright one, and both are stated per tier because either alone is wrong
            //somewhere in the range between them.
            //
            //THE ENVIRONMENT IS THE LEVEL'S OWN DOME (#232's second half), the same way the cannon's is: the
            //cup is enrolled in BS3DGame.SkyLitRenderers through Renderers, so it reflects and is ambient-lit
            //by whichever of the fourteen scenes the result page actually stands over, refreshed on every
            //scene or dome change like everything else on that list. It was NOT enrolled at first — a
            //presented object was reasoned to want one controlled finish rather than fourteen different ones,
            //the argument TitleWordmark's own doc still makes the OPPOSITE call from — but nothing had ever
            //authored that controlled finish either: SkyColor/GroundColor sat at their compiled default of
            //flat white, so every metal here was reflecting nothing at all rather than something consistent.
            //The owner's call, once that was found and fixed with a hand-authored fixed environment first: the
            //cup already stands over the exact scene the level was played in, unlike the wordmark's rolling
            //preview backdrop, so matching THAT scene is the more fitting "consistent" than a fixed one is.
            //
            //The specular POWER climbs with the tier as well, which is most of what says "better": a bronze
            //cup is a cast, slightly rough thing with a broad highlight, and a diamond one is polished to a
            //point. Nothing about the geometry changes between the first three — only the finish (and, since
            //#232, Gold's own — see AddTier's handled-mesh notes below).

            //A METAL'S DIFFUSE IS DARK, and the first version of these got that wrong in a way worth recording:
            //authored at the diffuse a painted surface would take (0.66 for the gold) and reflecting the sky at
            //full strength on top, every cup came out of the tonemap as flat pale plastic — a bright, even,
            //shadowless shape with no highlight anywhere on it, because the diffuse alone was already near the
            //top of the curve and the reflection pushed it over. Metals have almost no diffuse; what colours
            //them is their REFLECTANCE. So the diffuse is roughly a third of what it was and carries only
            //enough to hold the tier apart under a dark reflection, the specular carries the hue, and the
            //reflection strength is dialled back off full so there is somewhere left for a highlight to be
            //brighter than.
            //
            //The specular POWER climbs with the tier, which is most of what says "better": a bronze cup is a
            //cast, slightly rough thing with a broad highlight, and a diamond one is polished to a point.

            //Bronze: a cast, warm metal — the ROUGHEST finish of the four, and still a polish no prop gets.
            AddTier(device, instancingEffect, 1, _plainMesh,
                diffuse: new Vector3(0.330f, 0.170f, 0.070f),
                specular: new Vector3(0.85f, 0.52f, 0.28f), power: 80f,
                specularAmbient: 0.30f, emissive: Vector3.Zero, ambient);

            //Silver: neutral — and #232 is why its diffuse is this dark rather than the pale grey it shipped
            //with. A neutral diffuse has no HUE to hold it apart from a neutral specular the way bronze's warm
            //body and near-white highlight hold each other apart under identical lighting; the first cut
            //(0.40, 0.415, 0.45) was closer to the specular's own (0.88, 0.90, 0.95) than a dark metal's
            //reflectance ever gets, and against the flat placeholder environment below it that read as one
            //undifferentiated pale wash — "flat white and matte", verbatim the owner's report. Dropped to a
            //genuine near-black, the body now sits far enough under the highlight and the reflection for both
            //to read as light ARRIVING on the cup rather than the cup's own paint.
            AddTier(device, instancingEffect, 2, _plainMesh,
                diffuse: new Vector3(0.075f, 0.080f, 0.090f),
                specular: new Vector3(0.88f, 0.90f, 0.95f), power: 160f,
                specularAmbient: 0.42f, emissive: Vector3.Zero, ambient);

            //Gold: the funnel rims' hue, which is the one metal this game has already tuned against every
            //dome — no reason to invent a second one — but at a metal's diffuse rather than a band's. Takes
            //the HANDLED mesh since #232: the owner's report was that gold reads as a plain bowl where a
            //trophy is expected to have them, and Diamond having sole claim on the shape was a #183 decision
            //made before a player had actually won one and looked. The shape difference now marks the top TWO
            //tiers rather than one — a bowl at Bronze and Silver, a cup at Gold and Diamond — which still
            //reads before any colour has, just one rung lower than it used to.
            AddTier(device, instancingEffect, 3, _handledMesh,
                diffuse: new Vector3(0.470f, 0.320f, 0.080f),
                specular: new Vector3(0.98f, 0.78f, 0.40f), power: 140f,
                specularAmbient: 0.36f, emissive: Vector3.Zero, ambient);

            //Diamond: the top tier, and the only one that is not a metal at all. It takes the HANDLED mesh, so
            //it is told apart by its SHAPE before any colour has been read — which matters because the three
            //below it differ only in hue, and hue is the first thing a dark scene takes away. It carries a
            //small emissive term as well, so it is the one cup that is faintly a light source rather than only
            //a reflector; small, because the first pass had it at three times this and the cup came out of the
            //glare pass as a white blob with no shape left in it at all.
            //
            //SINCE #228 IT IS CRYSTAL: a little blue and genuinely transparent, so the defocused arena goes on
            //moving through the bowl while the cup holds the frame. Three figures make that read as cut glass
            //rather than as a faded decal, and none of them would work alone:
            //
            // * the ALPHA. Just over a third, per surface — and the cup is a closed solid drawn with its depth
            //   WRITE off (see Draw), so a look through the bowl crosses its near wall and its far inside and
            //   lands at about 60 % while the stem, one wall thick, stays at a third. Glass that gets denser
            //   where there is more of it is most of what says glass, and it falls straight out of the
            //   geometry rather than being authored.
            // * the METALNESS, which is not "a bit metallic": the shader lerps normal-incidence reflectance
            //   from its 4 % dielectric F0 towards the specular colour, and this lands it near 0.10 — between
            //   window glass at 0.04 and lead crystal's own 0.055, leaning towards the diamond it is named
            //   for (0.17, n = 2.42) without going there, because a flat white environment reflected at
            //   diamond's F0 is a veil. So the face is nearly clear and the edges mirror, which is Fresnel
            //   and is the entire shape language of glass; a metal reflects the same at every angle and
            //   would be a mirror with a hole in it.
            // * and the reflection NOT being attenuated by the transparency (SpecularAlphaWeight, set in
            //   AddTier below). At 38 % alpha the metal path would have kept 38 % of its sparkle, which is a
            //   coloured film. Unattenuated it is light added over a background that still shows through.
            //
            //WHERE THE BLUE HAS TO COME FROM, which is not where it looks like it should. The diffuse is
            //premultiplied by the alpha and then sRGB-DECODED by the shader, and both of those crush it: a
            //diffuse of 0.34 at 38 % alpha reaches the light as 0.13, which decodes to 0.015 of linear
            //radiance — a body colour that is, to the eye, black. The first crystal was authored at a
            //sensible-looking blue and came out a white ghost for exactly that reason. So the diffuse is
            //authored HIGH (it is a colour at 38 % of itself, not a colour), and a small EMISSIVE TINT
            //carries the rest: that one is added in linear radiance, is not premultiplied and is not
            //decoded, so it is the only term on this material that survives at full strength — and a tint
            //that glows faintly from inside the glass rather than only appearing where a lamp hits it is
            //what the cup wants anyway.
            //
            //The specular stays near white, because a highlight on clear glass is the colour of the lamp and
            //not of the glass, and the reflected environment is turned WAY down from the metals' (0.30
            //against 0.30–0.42, but at a dielectric's F0, not a metal's): at a metal's strength even the real
            //dome's own image would be a milky veil over everything the crystal is supposed to be showing
            //through, rather than the glint the low strength keeps it as.
            AddTier(device, instancingEffect, 4, _handledMesh,
                diffuse: new Vector3(0.620f, 0.820f, 1.000f),
                specular: new Vector3(0.78f, 0.90f, 1.00f), power: 320f,
                specularAmbient: 0.30f, emissive: new Vector3(0.020f, 0.045f, 0.060f), ambient,
                metalness: 0.06f, alpha: 0.38f, emissiveTint: new Vector3(0.016f, 0.055f, 0.110f));
        }

        private void AddTier(GraphicsDevice device, Effect effect, int tier, TrophyMesh mesh,
            Vector3 diffuse, Vector3 specular, float power, float specularAmbient, Vector3 emissive, Vector3 ambient,
            float metalness = 1f, float alpha = 1f, Vector3 emissiveTint = default)
        {
            _renderers[tier] = new InstancedModelRenderer(device, mesh, diffuse, effect, alpha)
            {
                Metalness = metalness,
                SpecularAmbientStrength = specularAmbient,
                EmissiveTint = emissiveTint,

                //A translucent tier's reflection is not something its transparency takes away — see
                //InstancedModelRenderer.SpecularAlphaWeight. Derived from the alpha rather than passed
                //separately: an opaque tier is at alpha 1 and cannot tell the difference, so there is no
                //second dial here that could disagree with the first.
                SpecularAlphaWeight = alpha < 1f ? 0f : 1f
            };

            _translucent[tier] = alpha < 1f;
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
        /// <para>
        /// <b>The draw states are stated here rather than by the caller</b>, because this class is the only
        /// thing that knows whether the tier being presented is a solid metal or a pane of crystal, and the
        /// two want different ones. The crystal's depth WRITE is off on purpose: with it on, only the nearest
        /// surface of each pixel survives and the cup is one flat film, where off, every front face blends —
        /// so a look through the bowl finds its far inside, and the far handle shows through the body, which
        /// is what looking through crystal is. Nothing else is ever drawn into this layer, so there is
        /// nothing for the cup to sort against but itself.
        /// </para>
        /// </remarks>
        public void Draw(ICamera camera)
        {
            if (_tier <= 0) return;

            bool glass = _translucent[_tier];

            _device.BlendState = glass ? BlendState.AlphaBlend : BlendState.Opaque;
            _device.DepthStencilState = glass ? DepthStencilState.DepthRead : DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;

            Matrix view = camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);
            Vector3 forward = -new Vector3(view.M13, view.M23, view.M33);

            Matrix projection = camera.Projection;

            //Smoothstep on the reveal, then the overshoot: the scale passes one and comes back, which is what
            //makes it land rather than arrive. Sin(pi*t) is zero at both ends, so nothing has to be clamped
            //and the cup is exactly SIZE once the reveal is done. Computed first because the dolly below is
            //eased in with it.
            float eased = MathHelper.SmoothStep(0f, 1f, _reveal);
            float scale = SIZE * eased * (1f + OVERSHOOT * MathF.Sin(MathF.PI * eased));

            //The dolly (#226), and everything below is derived from where it has got to: a real approach
            //and recede along the lens's own forward axis, eased in with the reveal so the cup ARRIVES at
            //the centre distance rather than appearing somewhere along the swing.
            float distance = DISTANCE + MathF.Sin(_clock * DOLLY_RATE) * DOLLY_DEPTH * eased;

            float halfHeight = distance / projection.M22;
            float halfWidth = distance / projection.M11;

            //And it rises into its place, from a little under it. Tied to the same eased value, so there is
            //one motion rather than two that can disagree about when they finished.
            float rise = (1f - eased) * -0.45f;
            float bob = MathF.Sin(_clock * BOB_RATE) * BOB_DEPTH * eased;

            Vector3 position = camera.Position
                + forward * distance
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
