using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The whole scene's lighting, derived from the sky dome it stands under (issue #39): hemisphere ambient —
    /// the zenith colour from above, the horizon's bounce from below — plus the tinted three-light rig, all in
    /// <b>linear radiance</b>. It existed value-for-value in all three executables until #75, and with it the
    /// four figures below and the sun tint, which was being re-derived at up to three separate sites per
    /// executable (one of them twice per frame).
    /// <para>
    /// The palette comes off the dome's own vertex colours, so it arrives sRGB-encoded and is decoded here.
    /// Everything after the decode scales, tints and lerps it, and none of that means anything until it is
    /// radiance: scaling an sRGB value by <see cref="ZENITH_AMBIENT_SCALE"/> does not make that much more
    /// light. Doing it in display space is what once had the ambient running some 38 % brighter than the rig
    /// asked for — see "Color management" in docs/rendering.md, the one area where a wrong claim about where a
    /// colour is decoded has already caused bugs twice.
    /// </para>
    /// <para>
    /// <b>Which renderers take part is deliberately not this class's business.</b> Each executable keeps its
    /// own enrolment list and its own reasons — the Testbed's ceiling is always there while the Game's is
    /// rebuilt per level and may be null, the map editor lights only the balls and the city, and all of them
    /// exclude the drain's pit shaft on purpose so no dome bleaches the inside of a hole in the ground. The
    /// rig is handed a list and never learns what a ceiling or a pit is.
    /// </para>
    /// </summary>
    public sealed class SkyLightRig
    {
        /// <summary>
        /// How far the key/fill lights are carried towards the horizon colour, and the back light towards the
        /// zenith, so the whole rig follows the mood of the sky rather than only the ambient term. The clouds
        /// borrow the same figure — deliberately, so the deck is lit by literally the same light as everything
        /// under it (see <see cref="CloudField"/> and "The weather" in docs/rendering.md). Public because it is
        /// the figure the record reaches for by name to explain a look: why every neutral vertical surface in
        /// this world reads warm (docs/scenes.md), and why a sky-replacing scene has to state its own rig rather
        /// than borrow the darkest dome — that would halve the sun through this very lerp
        /// (<see cref="SpaceLightingConfig"/>). No code outside this assembly reads it, and being a
        /// <c>const</c> it could not be re-pointed at runtime if any did.
        /// </summary>
        public const float SKY_TINT_STRENGTH = 0.5f;

        //The sky above is the stronger half of the hemisphere; bounce light from below is dimmer than the sky
        //it bounced from. Both were unnamed inline literals in all three executables.
        private const float ZENITH_AMBIENT_SCALE = 1.3f;
        private const float GROUND_BOUNCE_SCALE = 0.75f;

        //The "sun" is a POINT light, and this is how far out it stands: close enough for its direction to
        //visibly differ from object to object across the arena, which is what a light forty units away does
        //and a directional light at infinity cannot. Not to be confused with SUN_DIRECTION, which is the one
        //direction the cloud shadow and the scene shaders are told about.
        private const float KEY_LIGHT_DISTANCE = 40f;

        /// <summary>
        /// The sun's direction as the shaders want it — a single direction for the whole scene, unlike
        /// <see cref="KeyLightPosition"/>, whose direction fans out from a point forty units away. The cloud
        /// shadow and every scene shader read this one.
        /// </summary>
        public static readonly Vector3 SUN_DIRECTION = -DefaultLighting.Light0Direction;

        /// <summary>
        /// The sun's own radiance before the dome tints it, in linear radiance and deliberately over 1 — it is
        /// a sun. Two things read it and they must agree: the lit side of a cloud, and the sun colour the
        /// self-lit scene shaders shade their terrain and water with. See
        /// <see cref="SunRadianceTinted"/>, which is that agreement made into one value.
        /// </summary>
        public static readonly Vector3 SUN_RADIANCE = new(1.7f, 1.66f, 1.55f);

        /// <summary>Where the key light stands. Cached: it never moves, and it was recomputed per renderer per
        /// call in every executable.</summary>
        public static readonly Vector3 KeyLightPosition = -DefaultLighting.Light0Direction * KEY_LIGHT_DISTANCE;

        /// <summary>
        /// Fully overcast ambient, in linear radiance: grey, and no dimmer than the clear sky it replaces. A
        /// cloud deck is a big lit diffuse source, so losing the sun does not darken what arrives from above so
        /// much as spread it out and take the colour out of it.
        /// </summary>
        private static readonly Vector3 OVERCAST_SKY = new(0.62f, 0.64f, 0.68f);

        private static readonly Vector3 OVERCAST_GROUND = new(0.34f, 0.35f, 0.37f);

        /// <summary>Seconds the overcast reading takes to catch up — about how long a sky takes to close over.
        /// It belongs with the palette it drives rather than with the caller that samples the cloud.</summary>
        private const float OVERCAST_RESPONSE_SECONDS = 2.5f;

        //May be null: a caller with no scene renderer simply has no scene that could state its own rig.
        private readonly SceneRenderer _sceneRenderer;

        private SceneKind _scene;

        /// <param name="sceneRenderer">The scene renderer whose <c>TryGetLightRig</c> is consulted, or null for
        /// a caller with no scenes. All three executables have one, the map editor included — a level sets its
        /// scene from its own config, and previewing a sky-replacing level under the wrong sun is the one thing
        /// that editor exists to prevent.</param>
        public SkyLightRig(SceneRenderer sceneRenderer = null) => _sceneRenderer = sceneRenderer;

        /// <summary>The dome's zenith colour, decoded to linear. Never overridden by a scene — see
        /// <see cref="SetSky"/> — and handed to the scene shaders through <see cref="BuildSceneFrame"/>.</summary>
        public Vector3 ZenithLinear { get; private set; }

        /// <summary>The dome's horizon colour, decoded to linear. Never overridden by a scene.</summary>
        public Vector3 HorizonLinear { get; private set; }

        /// <summary>Hemisphere ambient from above, after the zenith scale, the overcast lerp and any scene
        /// override.</summary>
        public Vector3 SkyAmbient { get; private set; }

        /// <summary>Hemisphere ambient from below — the ground's bounce — after the same three.</summary>
        public Vector3 GroundAmbient { get; private set; }

        /// <summary>The key/fill tint, or the scene's own if it states one.</summary>
        public Vector3 KeyTint { get; private set; }

        /// <summary>The back light's tint, or the scene's own if it states one.</summary>
        public Vector3 BackTint { get; private set; }

        /// <summary>
        /// <see cref="SUN_RADIANCE"/> already tinted by the dome — the one value the lit side of a cloud
        /// (<c>CloudSunColor</c>) and <see cref="SceneFrame.SunColor"/> both want, and which every executable
        /// used to spell out twice.
        /// <para>
        /// It is derived from the <b>dome's</b> horizon and never from <see cref="KeyTint"/>, even though the
        /// two are the same expression whenever no scene overrides the rig. That is not an oversight and must
        /// not be "unified": a sky-replacing scene states its own key tint precisely because a dome-derived one
        /// would be a lie, and folding that override into the sun's radiance would change what the terrain
        /// shaders are told the sun is. Nothing reads this in those scenes today — they draw no cloud deck and
        /// no terrain — which is exactly why the error would go unnoticed.
        /// </para>
        /// </summary>
        public Vector3 SunRadianceTinted { get; private set; }

        /// <summary>
        /// How much cloud is overhead, 0 clear … 1 solid, smoothed by <see cref="StepOvercast"/>. Stays 0 for
        /// any caller that never steps it, and <c>Lerp(a, b, 0)</c> is bit-exactly <c>a</c>, so an executable
        /// with no weather gets the un-lerped ambient rather than an approximation of it.
        /// </summary>
        public float Overcast { get; private set; }

        /// <summary>
        /// The hook that hands the shared cloud field to an effect, carried into every
        /// <see cref="BuildSceneFrame"/>. Set it <b>once</b>, at load — a caller that assigns
        /// <c>clouds.ApplyTo</c> per frame allocates a fresh delegate per frame, which is what all three
        /// executables were doing on the draw path. Left null when there is no weather to apply (the map
        /// editor draws no clouds, so the cloud uniforms stay zero and <c>CloudSunlight</c> returns a flat 1).
        /// </summary>
        public Action<Effect> CloudHook { get; set; }

        /// <summary>
        /// Reads the dome's palette, decodes it and re-derives the whole rig for the scene it is standing in.
        /// Call it wherever the executables called their own <c>ApplySkyLighting</c>: after every dome switch,
        /// after every scene change, and after any enrolled renderer has been recreated — a fresh renderer
        /// starts with the library's default rig and has never been told the dome's palette, which is what once
        /// left the Game's rebuilt ceiling glass unlit. Then <see cref="ApplyTo(List{InstancedModelRenderer})"/>.
        /// <para>
        /// A scene that states its own rig replaces the ambient and both tints, and <b>bypasses the overcast
        /// lerp with them</b>, which is right: there is no weather out in deep space to go overcast. The reason
        /// it states one at all is not purity — reaching for the darkest dome to get a dark sky halves the sun
        /// through the key tint and takes the drain's metallic gold beads with it. Note the scenes that do this
        /// are the sky-replacing ones as a group, not space alone; several comments and docs still say space.
        /// </para>
        /// </summary>
        public void SetSky(SkyDome sky, SceneKind scene)
        {
            _scene = scene;

            ZenithLinear = ColorSpace.SrgbToLinear(sky.ZenithColor);
            HorizonLinear = ColorSpace.SrgbToLinear(sky.HorizonColor);

            Derive();
        }

        /// <summary>
        /// Follows the cloud straight over the arena and flattens the ambient towards
        /// <see cref="OVERCAST_SKY"/>/<see cref="OVERCAST_GROUND"/> as it thickens. The caller samples its own
        /// cloud field and steps this every frame, then re-applies.
        /// <para>
        /// <b>Overhead, not along the sun ray, and the difference matters:</b> the sun ray is what decides
        /// whether you are standing in a shadow, which the shader already answers per pixel, while what is over
        /// your head is what decides how much of the sky is still blue air. Sun behind a cloud with the rest of
        /// the sky open should take the shadows away and leave the ambient where it was, and reading both off
        /// one number would not let it. Only the <b>ambient</b> is touched: the key light is dimmed per pixel by
        /// the shader and dimming it here too would count the same cloud twice and darken everything uniformly,
        /// which is precisely the look this exists to avoid. Sun goes, sky stays — overcast reads <i>flat</i>,
        /// not dark.
        /// </para>
        /// <para>
        /// Deliberately not every executable's business: the Game leaves it alone because this palette is
        /// authored for a daylight sky and is brighter than its dusk dome's own, so lerping towards it would
        /// <i>lighten</i> a night city as the weather thickened (docs/game-session.md).
        /// </para>
        /// </summary>
        /// <param name="coverOverhead">How much cloud is over the arena right now, 0–1, averaged over a patch
        /// about as wide as a cloud — what lights the scene from above is how much of the sky is covered, not
        /// what happens to sit over one point of it.</param>
        /// <param name="elapsedSeconds">The frame's own elapsed time. The response is exponential and framed in
        /// seconds rather than as a per-frame constant, so it does not change with the frame rate.</param>
        public void StepOvercast(float coverOverhead, float elapsedSeconds)
        {
            Overcast = MathHelper.Lerp(Overcast, coverOverhead, 1f - MathF.Exp(-elapsedSeconds / OVERCAST_RESPONSE_SECONDS));

            Derive();
        }

        private void Derive()
        {
            //The key/fill lights (the "sun" side) take on the horizon colour, the back light the zenith
            Vector3 domeKeyTint = Vector3.Lerp(Vector3.One, HorizonLinear, SKY_TINT_STRENGTH);

            //Taken off the dome before any override, and the property's own doc says why that is load-bearing
            SunRadianceTinted = SUN_RADIANCE * domeKeyTint;

            KeyTint = domeKeyTint;
            BackTint = Vector3.Lerp(Vector3.One, ZenithLinear, SKY_TINT_STRENGTH);
            SkyAmbient = Vector3.Lerp(ZenithLinear * ZENITH_AMBIENT_SCALE, OVERCAST_SKY, Overcast);
            GroundAmbient = Vector3.Lerp(HorizonLinear * GROUND_BOUNCE_SCALE, OVERCAST_GROUND, Overcast);

            if (_sceneRenderer != null && _sceneRenderer.TryGetLightRig(_scene, out SceneLightRig rig))
            {
                SkyAmbient = rig.SkyAmbient;
                GroundAmbient = rig.GroundAmbient;
                KeyTint = rig.KeyTint;
                BackTint = rig.BackTint;
            }
        }

        /// <summary>Pushes the rig onto one renderer, and nothing else — at full strength, which it states
        /// explicitly so a renderer moved between this and <see cref="ApplyToGlass"/> carries no leftover.</summary>
        public void ApplyTo(InstancedModelRenderer renderer)
        {
            if (renderer == null) return;

            renderer.LinearLightRig = true;
            renderer.SkyColor = SkyAmbient;
            renderer.GroundColor = GroundAmbient;
            renderer.KeyLightPosition = KeyLightPosition;
            renderer.DirLightStrength = 1f;
            renderer.SetLightTint(KeyTint, BackTint);
        }

        /// <summary>
        /// Sky-ambient luminance (<see cref="ColorSpace.Luminance"/>, in linear radiance) at and above which
        /// glass standing against the sky keeps the full three-light rig — about what a daylight dome's
        /// zenith derives to, so the day scenes are untouched by <see cref="ApplyToGlass"/>. The
        /// sky-replacing scenes' stated rigs sit near a fifth of it and the Moon's near a seventh, which is
        /// what actually dims their glass.
        /// </summary>
        private const float GLASS_FULL_RIG_LUMINANCE = 0.25f;

        /// <summary>
        /// Pushes the rig onto a <b>translucent renderer that stands against the sky itself</b> — the ceiling
        /// glass plate — with the three directional lights scaled by how bright that sky is
        /// (<see cref="SkyAmbient"/>'s luminance over <see cref="GLASS_FULL_RIG_LUMINANCE"/>, saturated).
        /// <para>
        /// What hides the plate's straight edges in a dome scene is not a treatment on the plate but the dome
        /// itself: the glass is lit from the very palette that shows through it, so its 40 % term tracks the
        /// 60 % show-through in hue and brightness. The sky-replacing scenes broke that pact from the other
        /// side — their rigs deliberately keep the sun at full strength so a dark scene does not take the
        /// balls and the drain's gold beads with it (see <see cref="SetSky"/>), and that undimmed key on the
        /// glass made the plate a bright slab over a near-black backdrop, hard box edges and all (#156). This
        /// push restores the pact for the one surface whose backdrop <i>is</i> the sky: opaque objects keep
        /// their authored sun, the glass follows the sky it is seen against. A dusk dome dims its glass the
        /// same way, by the same measure, with no scene knowledge in here.
        /// </para>
        /// <para>
        /// Only the three-light rig is scaled, and through the renderer's own
        /// <see cref="InstancedModelRenderer.DirLightStrength"/> — the tints themselves are the shared
        /// effect's DirLight* colors, one set for the whole scene, so scaling them here would dim every
        /// renderer drawn after the glass (which is exactly what the first cut of this did). The hemisphere
        /// ambient and the Fresnel sky reflection already follow the rig's own colours, the scene point
        /// lights stay at full strength (a cave's own glow still reaches the pane), and the emissive
        /// step-down flash is deliberately untouched — it is added after lighting (see <c>EmissiveTint</c> in
        /// InstancedModel.fx) and reads better, not worse, on a dark pane. Which renderer is "the glass
        /// against the sky" stays the caller's decision, like the enrolment lists themselves: the rig still
        /// never learns what a ceiling is.
        /// </para>
        /// </summary>
        public void ApplyToGlass(InstancedModelRenderer renderer)
        {
            if (renderer == null) return;

            ApplyTo(renderer);

            renderer.DirLightStrength =
                MathF.Min(1f, ColorSpace.Luminance(SkyAmbient) / GLASS_FULL_RIG_LUMINANCE);
        }

        /// <summary>
        /// Pushes the rig onto every renderer in the caller's own enrolment list. Walked by index, so a reused
        /// <see cref="List{T}"/> costs no enumerator even where this runs every frame — which it does wherever
        /// the overcast lerp is being stepped.
        /// </summary>
        public void ApplyTo(List<InstancedModelRenderer> renderers)
        {
            for (int i = 0; i < renderers.Count; i++) ApplyTo(renderers[i]);
        }

        /// <summary>
        /// This frame's <see cref="SceneFrame"/> — the per-frame inputs the self-lit scenes need. Four of its
        /// six values are the rig's own, so the caller hands over only what the rig cannot know: which camera
        /// is looking and what time it is. Allocates nothing: <see cref="SceneFrame"/> is a readonly struct and
        /// <see cref="CloudHook"/> was captured once at load.
        /// </summary>
        /// <param name="camera">This frame's camera.</param>
        /// <param name="time">Wall-clock seconds, not simulation time — the sea, the wind and the campfire keep
        /// moving while the simulation is paused. It must be the same clock the scene's point lights are given,
        /// or the campfire's light and its flame billboard fall out of step.</param>
        public SceneFrame BuildSceneFrame(ICamera camera, float time) =>
            new(camera, SUN_DIRECTION, ZenithLinear, HorizonLinear, SunRadianceTinted, time, CloudHook);
    }
}
