using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core;
using Prazsky.Core.Render;
using System.Collections.Generic;

namespace BS3D
{
    /// <summary>
    /// The host's half of <b>the setting</b> — the sky, the fifteen backdrops, the city, the island and its
    /// drain, the forest scatter, the clouds, the light rig and the scene's own lamps. All of it outlives a
    /// session, which is why it is the host's and not <see cref="Screens.GameplayScreen"/>'s (#65), and since
    /// #75 the drawn things themselves live in <c>Prazsky.Core</c> in one copy — what is here is the wiring:
    /// which scene is standing, what the rig derives from the dome, and the frame's slices.
    /// </summary>
    /// <remarks>
    /// <see cref="BeginSceneDraw"/>, <see cref="DrawSettingGlass"/> and <see cref="FinishSceneDraw"/> are
    /// deliberately three calls rather than one <c>DrawScene</c>: gameplay draws <i>inside</i> that sequence
    /// (the cluster and the gun go between the opaque setting and its glass), so each bottom screen runs the
    /// sequence with its own work in the gaps. Split out of <c>BS3DGame.cs</c> in #71.
    /// </remarks>
    public partial class BS3DGame
    {
        #region Scene

        //Dome 13 is the violet/teal dusk. The neon city reads best under a dark sky — the facades stay dark
        //under any dome, so a bright one only fights the neon it is meant to set off. It is the default the
        //game starts on; the sky setting cycles the whole set, and two scenes bring a dome of their own.
        internal const byte SKY_DOME_COUNT = SkyDome.Count;
        private const byte DEFAULT_SKY_DOME = 13;

        //The sea mirrors the sky, so its whole mood follows the dome and a bright one gives a breezy sea
        //rather than a moody one; the savanna wants the set's warmest gold horizon; the tropical beach
        //wants the brightest blue in the set (dome 1, a clear sunny sky over a warm horizon — white
        //sand and turquoise water are the postcard, and they read as one under it). The Testbed's own
        //figures, except the beach's, chosen for it.
        private const byte SEA_SKY_DOME = 13;
        private const byte SAVANNA_SKY_DOME = 14;
        private const byte TROPICAL_SKY_DOME = 1;

        //The volcano wants a sky that stays out of the way, because its ground is the light. Dome 9 is a dim
        //mauve-and-slate dusk with no bright band and no sun disc beside the cone — picked by looking, over
        //the darker-zenithed 16 whose cream horizon and sun both compete with the crater. The Testbed's
        //figure, chosen there.
        private const byte VOLCANO_SKY_DOME = 9;

        //Mars has a dome built for it (#277) rather than a pick among the eighteen general-purpose ones —
        //dome 19 IS the Martian sky.
        private const byte MARS_SKY_DOME = 19;

        //Space deliberately forces NO dome, unlike those two. Its dome is neither drawn (Space.fx covers the
        //whole frame) nor read (SpaceLightingConfig states the light rig instead, for the reasons set out
        //there) — so it is completely inert in that scene, and changing the player's dome behind their back to
        //no visible effect would be a silent side effect rather than a setting. Whatever is up stays up.

        private byte _skyDome = DEFAULT_SKY_DOME;

        /// <summary>
        /// Which of the fifteen settings the frame stands in — the backdrop the menu's camera orbits and the
        /// one the game is then played in, since the player picks it from the menu and it stays picked. The
        /// city and the neon city are the procedural <see cref="City"/> under two lightings; the other thirteen
        /// are the shared <see cref="SceneRenderer"/>'s self-lit backdrops, the same ones the Testbed and the
        /// map editor draw. The count is <see cref="SceneRenderer.SceneCount"/>, which is where to read it.
        /// </summary>
        private SceneKind _scene = SceneKind.NeonCity;

        //How many scenes there are, and what each is called, are SceneRenderer's since #75:
        //SceneRenderer.SceneCount and SceneRenderer.SceneName, both in the declared order of SceneKind, so the
        //scene page still indexes its labels by the enum's own value. The count stood here as a literal 11 and
        //the name list existed twice, here and in the Testbed.

        private SceneRenderer _sceneRenderer;

        private SkyDome _sky;
        private Effect _skyEffect;

        /// <summary>
        /// The whole scene's lighting derived from the dome it stands under — the palette decoded to linear
        /// radiance, the hemisphere ambient and the tinted three-light rig — shared with the Testbed and the
        /// map editor since #75, and with it the sun's direction and radiance and the sky tint strength that
        /// used to stand here. Built in <see cref="LoadContent"/>, after the scene renderer it consults for the
        /// scenes that state a rig of their own.
        /// <para>
        /// <b>The game deliberately never steps the overcast lerp</b> (<see cref="SkyLightRig.StepOvercast"/>),
        /// which the Testbed does: that overcast palette is authored for a daylight sky and is brighter than
        /// this dusk dome's own, so lerping towards it would <i>lighten</i> a night city as the weather
        /// thickened. The half that matters is the shader's, which takes the sun away per pixel where cloud
        /// covers it. So <see cref="SkyLightRig.Overcast"/> stays 0 here — and <c>Lerp(a, b, 0)</c> is
        /// bit-exactly <c>a</c>, so this gets the un-lerped ambient rather than an approximation of it.
        /// </para>
        /// </summary>
        private SkyLightRig _rig;

        //The weather. Clouds live on a flat plane at a finite altitude rather than as a texture on the dome,
        //which is what lets the same field be both the cloud you look at and the shadow it throws: the sky
        //shader crosses the plane with the view ray, the ball/city/island shader with the sun ray. One field,
        //handed to both shaders from here, so the two cannot be tuned apart by accident.
        private readonly CloudField _clouds = new();

        //The cloud look values that used to stand here — the shadowed colour, the detail strength, the opacity,
        //the horizon fade, the sun step, both absorptions, both silver-lining figures and the harder tint the
        //shadowed side takes — are CloudField's own since #75. They were identical to the Testbed's to the last
        //digit, which is the drift a shared field should never have been able to have. The weather's shape
        //(plane, scale, wind, coverage) was already the field's, and the one direction both shaders are
        //shadowed along is SkyLightRig.SunDirection — the dome's own since #220, so it goes out with the
        //colours rather than once at load. The LIT side's radiance went to the rig rather than to CloudField,
        //because it is the sun's and not the cloud's: SkyLightRig.SUN_RADIANCE, handed over as ApplyDome's
        //argument so that it is bit-for-bit the number SceneFrame carries.

        private static readonly float SCENE_AMBIENT_INTENSITY = 0.25f;

        //Zero specular here keeps each mesh's own material specular; the ambient is the scene's flat fill.
        private readonly BasicEffectParams _sceneEffectParams =
            new(Vector3.One * SCENE_AMBIENT_INTENSITY, Vector3.Zero, 0f, Vector3.Zero);

        internal BasicEffectParams SceneEffectParams => _sceneEffectParams;

        private Effect _instancingEffect;

        private readonly CitySceneConfig _cityConfig = new();

        //Fixed, so the skyline is the same city every launch — and so a quality tier that rebuilds it at a
        //smaller radius produces the same towers, minus the outer rings, rather than a different city
        private const int CITY_SEED = 20260720;

        private City _city;

        //How many of the city's buildings the last frame actually drew, for the logfps line. A frame's worth of
        //diagnostics, not state anything renders from.
        private int _cityVisible;
        private BoxMesh _unitBox;
        private InstancedModelRenderer _cityRenderer;

        //The whole arena the gun stands on: the round island's stone cap and concrete drum, the glass drain
        //bored through its middle, the two gold beads that ring the drain's circles, and the dark pit shaft
        //that backs the glass in the solid-terrain scenes. Every mesh, procedural texture, renderer, world
        //matrix and figure of it is ArenaIsland's since #75 — the Testbed had a copy of the lot, value for
        //value, under ARENA_*/FUNNEL_*/PIT_* names against these ISLAND_*/FUNNEL_*/PIT_* ones. The figures the
        //rest of this executable still reads are constants on that type: ArenaIsland.TOP_Y (the old ISLAND_Y,
        //which the session's precise-aim floor and the drop cinematic derive from in constant expressions),
        //RADIUS, EDGE_HEIGHT, TERRAIN_HOLE_RADIUS and the drain's four figures the session's collider wants.
        //
        //The frame sequence stays here: the three DrawIsland/DrawPit/DrawGlass slices are placed by hand
        //below, which is what lets the session put its gun, balls and shot trails between the pit and the glass.
        private ArenaIsland _island;

        //The forest scene's scattered trees, rocks and stumps: every procedural texture, mesh variant,
        //renderer, matte material and encoded tint of them is ForestScatterRenderer's since #75 — the piece
        //stood in this file alone, which is the whole reason the Testbed and the map editor drew the forest as a
        //bare clearing on the very same shared terrain. Built once (a fixed default forest, like the city is a
        //fixed default city), and it needs no stone texture handed to it: ArenaIsland's copy is that
        //component's private business and SurfaceTexture.Stone is deterministic, so the one the scatter builds
        //for its boulders is the very same 512² tile — one more copy in memory and not a pixel of difference.
        //
        //What stays this file's: where the draw sits in the frame, the if (_scene == SceneKind.Forest) gate on
        //it, and the sky-lit enrolment below.
        private ForestScatterRenderer _forestScatter;

        //The scene's own point lights (the neon city's ring of magenta and cyan around the island, the
        //savanna's campfire, space's planetshine) pushed onto the shared instanced effect each frame, so the
        //balls, the island, the gun and the city all take them on top of the sun and the dome. The slots, the
        //three arrays and the change gate are SceneLights' own since #75 — this and the Testbed held a copy
        //each. The neon ring's figures live in _cityConfig.NeonLook.
        private SceneLights _sceneLights;

        //What the cluster hangs from: a translucent glass plate over the play field, and CeilingPlate's since
        //#75 (it stood line for line here and in the Testbed). The MESH AND RENDERER are the host's — the glass
        //is lit with the rest of the scene, and SkyLitRenderers has to reach it — while the kinematic BODY it is
        //drawn from belongs to the session (GameplayScreen), which is the split the issue warned about. Fitted
        //per level at the field's footprint, so before the first level its Renderer is null and SkyLitRenderers
        //tolerates that.
        private CeilingPlate _ceilingPlate;

        //The menu's own plate, over the backdrop's PREVIEW map instead of a played field. A second instance
        //rather than a refit of the one above, because a kept session (Continue) never reinstalls its level:
        //refitting the shared plate for a preview would leave that session drawing glass cut to another
        //field's footprint. Fitted at whatever the backdrop last rolled, so like the session's it starts
        //without a renderer and only the backdrop's own draw ever asks for it (#249).
        private CeilingPlate _menuCeilingPlate;

        //The menu plate's opacity: clearly present glass rather than the played field's whisper. The menu
        //camera looks at the field almost level and from well outside the island, so a 0.4 slab against a
        //bright sky all but disappears — and the owner's ask was that this ceiling BE seen. (The stand-off
        //was a fixed 44 when this was set and is solved per map since #254, which moves it but does not
        //change the argument: the wide leg is still a look across the scene from outside it.)
        private const float MENU_CEILING_ALPHA = 0.7f;

        /// <summary>
        /// The glass plate's renderer, or null before the first level is installed: the session reaches through
        /// it for <see cref="InstancedModelRenderer.EmissiveTint"/> — how the glass flashes on the frame the
        /// ceiling steps down — and draws it from its own kinematic body's pose.
        /// </summary>
        internal InstancedModelRenderer CeilingRenderer => _ceilingPlate.Renderer;

        /// <summary>
        /// The menu plate's renderer, or null before the backdrop has rolled a map. The backdrop is also its
        /// only drawer — the session never sees this plate, it has the one its own level fitted.
        /// </summary>
        internal InstancedModelRenderer MenuCeilingRenderer => _menuCeilingPlate.Renderer;

        #endregion

        /// <summary>
        /// The city and the island it leaves a clearing for, plus everything the island carries: the glass
        /// drain, its gold beads, the dark pit shaft behind it and the glass plate the cluster will hang
        /// from. One unit box under a different instance matrix per building, so the whole skyline is a
        /// single instanced draw call.
        /// <para>
        /// All of it is scene, not session: it stands whether a game is being played or the menu's camera is
        /// merely orbiting it, which is why the ceiling's <i>mesh and renderer</i> live on the host while its
        /// kinematic <i>body</i> belongs to the session and is built with the rest of the world.
        /// </para>
        /// </summary>
        private void BuildScene()
        {
            _unitBox = new BoxMesh(GraphicsDevice, 1f, 1f, 1f);
            _city = new City(seed: CITY_SEED, arenaHalfExtent: ArenaIsland.RADIUS, config: _cityConfig);

            //The neon flags are what SetScene switches between the two city lightings; these are only the
            //values they start at, and are overwritten before the first frame is drawn.
            _cityRenderer = new InstancedModelRenderer(GraphicsDevice, _unitBox, Vector3.One, _instancingEffect)
            {
                CityConfig = _cityConfig,
                CityNeon = 1f,
                CityWindowBrightness = _cityConfig.NeonLook.WindowBrightness,

                //The specular ambient is not multiplied by albedo, and almost every facade of a city seen
                //from inside it is at a grazing angle where Fresnel is near 1 — left alone it bleaches the
                //whole skyline into a white cliff with the windows lost in it.
                SpecularAmbientStrength = 0.07f
            };

            //The arena the gun stands on, all of it: the island's stone cap and concrete drum, the glass drain
            //bored through the middle, its two gold beads and the dark pit shaft that backs the glass where the
            //terrain has the island's footprint cut out of it. Meshes, procedural textures, renderers and the
            //one world matrix are the component's; the ambient is the scene's, so it is handed in.
            _island = new ArenaIsland(GraphicsDevice, _instancingEffect, SCENE_AMBIENT_INTENSITY)
            {
                //Seeded here as well as written by ApplyQuality, for the reason SceneDetail beside it is: the
                //tier is applied once in LoadContent BEFORE this exists, so a startup at anything but High
                //would otherwise draw the full-price cap until the next tier change — which on a pinned tier
                //never comes.
                SurfaceDetail = _quality == QualityLevel.High ? 1f : 0f
            };

            //The forest's scattered trees, rocks and stumps, all of it: both procedural textures, the fifteen
            //mesh variants, the twenty-five renderers with their bark, foliage, stone and sawn-wood dressing,
            //the matte materials and the tints encoded once from the config. The config is the SceneRenderer's
            //own instance and is read at build time only, which is all this executable ever needs — nothing
            //here edits a scene config at runtime, so there is no Replant call in the Game (the map editor's
            //live grid is the one caller that has to make it). No stone texture handed in: the component builds
            //its own, since ArenaIsland's is that component's private business. The ambient is the scene's, so
            //it is handed over as it is to the island.
            _forestScatter = new ForestScatterRenderer(GraphicsDevice, _instancingEffect,
                (ForestSceneConfig)_sceneRenderer.GetSceneConfig(SceneKind.Forest), SCENE_AMBIENT_INTENSITY);

            //Note the glass the cluster hangs from is NOT built here: its footprint is the loaded level's
            //field, so RebuildCeilingRenderer fits it (and refits it on every level) — which is why the
            //glass push tolerates a null renderer and ApplySkyLighting runs again after a load.
        }

        /// <summary>
        /// Refits the drawn glass plate to the loaded field's footprint, called by the session as it installs a
        /// level. The renderer is recreated, so it starts without the sky palette —
        /// <see cref="ApplySkyLighting"/> has to run after this, exactly as it does after the Testbed's
        /// <c>FitCeilingToMap</c>. The margin, the thickness and the glass itself are
        /// <see cref="CeilingPlate"/>'s; the plate is drawn from the session's kinematic body's own pose, so
        /// the glass and the collidable cannot disagree.
        /// </summary>
        internal void RebuildCeilingRenderer(float stageSizeX, float stageSizeZ) =>
            _ceilingPlate.Fit(stageSizeX, stageSizeZ);

        /// <summary>
        /// <see cref="RebuildCeilingRenderer"/> for the menu's plate: refits the backdrop's glass to the preview
        /// map's footprint, at the menu's own opacity — the played field's 0.4 whispers under a nearly level
        /// camera against a bright sky, and this plate is a display piece, not furniture (#249). The same
        /// sky-lighting caveat applies — <see cref="ApplySkyLighting"/> has to run after this, and the backdrop
        /// does so as it rolls.
        /// </summary>
        internal void RebuildMenuCeilingRenderer(float stageSizeX, float stageSizeZ) =>
            _menuCeilingPlate.Fit(stageSizeX, stageSizeZ, MENU_CEILING_ALPHA);

        /// <summary>
        /// Every renderer that takes its lighting from the sky dome <b>at full strength, unadjusted</b>. Two
        /// are deliberately not among them, each taking the rig through its own push in
        /// <see cref="ApplySkyLighting"/> instead: the ceiling glass, which stands against the sky itself
        /// (<see cref="SkyLightRig.ApplyToGlass"/>), and the trophy cup, which must stay legible even under a
        /// dome whose own rig is near-black by design (<see cref="SkyLightRig.ApplyToPresented"/>, #232).
        /// </summary>
        private IEnumerable<InstancedModelRenderer> SkyLitRenderers()
        {
            //Sky-lit enrolment is the one thing BallRenderSet exposes its renderers for, and deliberately so:
            //which renderers take part is each executable's own list with its own reasons, which is why this
            //walk is here and not in the component.
            foreach (InstancedModelRenderer ballRenderer in _balls.Renderers) yield return ballRenderer;

            yield return _cannonRig.Renderer;
            yield return _cannonRig.GlassRenderer;
            yield return _cannonRig.CarriageRenderer;
            yield return _cannonRig.WheelRenderer;
            yield return _cannonRig.RollerRenderer;
            yield return _cityRenderer;

            //The island's stone cap and concrete drum, the drain's glass and its two gold beads — but
            //deliberately not its pit shaft, which is a hole in the ground no dome may bleach. Dereferenced
            //unconditionally, unlike the ceiling below: BuildScene makes the island and runs in LoadContent
            //BEFORE the startup SetScene, which is what first calls ApplySkyLighting, and every other way here
            //(the scene page, the sky setting, a level's own dome, the session after a refit) is later still.
            //A guard would be reassurance about something that cannot happen.
            foreach (InstancedModelRenderer renderer in _island.SkyLitRenderers) yield return renderer;

            //The forest scatter, present only in the forest scene but always built. Every variant of every kind
            //takes part — the one flat array the component exposes for exactly this — or a spruce of the variant
            //this missed would stand under the light rig of whatever dome was up when it was made. Dereferenced
            //unconditionally like the island's, and for the same reason: BuildScene makes it well before the
            //startup SetScene, which is what first calls ApplySkyLighting.
            foreach (InstancedModelRenderer renderer in _forestScatter.Renderers) yield return renderer;

            //The 3D title over the front end (#248), letters and keylines both. It takes the dome's light like
            //everything else in the frame ON PURPOSE — argued on TitleWordmark.Renderers, a wordmark stands
            //over all fifteen backdrops under all eighteen domes and has to come out right at both ends of
            //that range. Dereferenced unconditionally like the two above — it is built in LoadContent
            //immediately before the startup SetScene, and for this very reason.
            foreach (InstancedModelRenderer renderer in _titleWordmark.Renderers) yield return renderer;

            //The trophy cup is deliberately NOT here (#232's second half) — it takes the rig through
            //SkyLightRig.ApplyToPresented instead, in ApplySkyLighting below, the same way the ceiling glass
            //takes ApplyToGlass rather than the plain push this walk feeds everything else.
        }

        /// <summary>
        /// Derives the whole scene's lighting from the dome: hemisphere ambient plus a tinted key light, in
        /// linear radiance. The derivation itself is <see cref="SkyLightRig"/>'s since #75 — palette decode,
        /// scale factors, tints and the sky-replacing scenes' own rig alike — and it stood here, in the Testbed
        /// and in the map editor. Which renderers take part stays this file's business, which is why the walk
        /// is here. Internal because the session re-runs it after rebuilding the ceiling's renderer, which
        /// starts without the palette.
        /// </summary>
        internal void ApplySkyLighting()
        {
            _rig.SetSky(_sky, _scene);

            foreach (InstancedModelRenderer renderer in SkyLitRenderers()) _rig.ApplyTo(renderer);

            //The glass push (SkyLightRig.ApplyToGlass holds the why — the plate stands against the sky,
            //#156). Null through a level load's refit window; the push tolerates it, and the session
            //re-runs this once the plate is refitted.
            _rig.ApplyToGlass(_ceilingPlate.Renderer);

            //The menu's glass with it (#249) — the same sky it stands against, the same refit-window null,
            //the same tolerance on the push.
            _rig.ApplyToGlass(_menuCeilingPlate.Renderer);

            //The trophy's own push (#232's second half — ApplyToPresented holds the why): it reflects the
            //level's dome like everything else, floored so a dome whose own rig runs near-black (the Moon's
            //airless sky, the cavern's dim one) cannot take the reward down with it.
            foreach (InstancedModelRenderer renderer in _trophy.Renderers) _rig.ApplyToPresented(renderer);

            //And the wood's own pigments, which the rig above cannot reach — see ForestScatterRenderer.
            //ShiftTowardsSky (#108). Guarded inside on the tint, so it is free every frame but a dome switch.
            _forestScatter?.ApplySkyTint(_rig.KeyTint);

            //The clouds' own colours follow the dome as well, and the lit side is handed the very radiance the
            //rig gives the scene — one sun, one number (see SkyLightRig.SunRadianceTinted). Since #220 the
            //sun's DIRECTION rides along, the dome having its own: the drawn disc, the deck's silver lining
            //and the shadow the instanced shader throws are all re-aimed in this one call.
            _clouds.ApplyDome(_skyEffect, _instancingEffect, _rig.SunDirection,
                _rig.SunRadianceTinted, _rig.ZenithLinear, _rig.HorizonLinear);
        }

        /// <summary>
        /// Puts the scene into the frame: the backdrop's own lighting defaults, the dome that suits it and
        /// the city's day-or-neon switch. The one place a scene change happens, shared by the random pick at
        /// startup, the scene menu and a loaded level that carries a scene of its own.
        /// </summary>
        internal void SetScene(SceneKind scene)
        {
            _scene = scene;

            //The scene's own sound follows the scene, on the one writer's rule. Null-conditional because the
            //startup pick runs before LoadContent has built the audio; that first pick is handed over there.
            _ambience?.SetScene(scene);

            //Neither city is drawn by the SceneRenderer — the city is one instanced box mesh under the shared
            //shader's city technique, and its two lightings are a flag and a brightness on the renderer.
            bool neon = scene == SceneKind.NeonCity;
            _cityRenderer.CityNeon = neon ? 1f : 0f;
            _cityRenderer.CityWindowBrightness = neon ? _cityConfig.NeonLook.WindowBrightness : _cityConfig.WindowBrightness;

            //The sea mirrors the sky, so a bright dome would give it a breezy mood rather than the moody one
            //it is built for; the savanna wants the warmest gold horizon of the set; the tropical beach
            //wants the brightest blue — sand and turquoise water are a postcard, and only read as one under
            //a sunny sky; and the volcano wants the darkest, because its ground is what lights it. Every
            //other scene keeps whatever dome is up — including the neon city, whose default IS the dusk.
            //The Testbed's rule, so a scene looks the same in both.
            if (scene == SceneKind.Sea) _skyDome = SEA_SKY_DOME;
            else if (scene == SceneKind.Savanna) _skyDome = SAVANNA_SKY_DOME;
            else if (scene == SceneKind.Tropical) _skyDome = TROPICAL_SKY_DOME;
            else if (scene == SceneKind.Volcano) _skyDome = VOLCANO_SKY_DOME;
            else if (scene == SceneKind.Mars) _skyDome = MARS_SKY_DOME;

            //And the sky the scene stands under (#221). It is the scene's own default here; a level says
            //what it is like TODAY and overrides this a moment later, in BuildLevel, which is the same
            //order the dome and the music arrive in. SetWeather fades, so walking the scene picker leaves
            //one sky closing over into the next rather than cutting between them.
            ApplySceneWeather();

            //Re-derives the whole light rig from the dome, which every scene needs whether its dome changed
            //or not: the renderers were told nothing until now. Re-selecting the dome that is already up
            //costs a 92-vertex recolour and a palette re-read.
            SetSkyDome(_skyDome);
        }

        /// <summary>
        /// Puts the sky the current scene asks for over it (#221), unless a level has overridden it. The two
        /// arrive in the order the dome and the music do: <see cref="SetScene"/> states the scene's own
        /// weather, and a level built a moment later says what it is like today instead.
        /// <para>
        /// <paramref name="levelWeather"/> is the level file's word, or null for a level that names none —
        /// and an unrecognised word is null too, which is the leniency the scene and the music take: a typo
        /// is a level under its scene's usual sky rather than a level that will not open.
        /// </para>
        /// </summary>
        internal void ApplySceneWeather(string levelWeather = null)
        {
            //The city's own config is the HOST's and not the scene renderer's — the two cities are drawn
            //through the shared instanced technique rather than by a scene shader, which is why
            //GetSceneConfig answers null for both of them. Reading it from the right place is what stops the
            //city being the one scene with no authored sky (it asks for overcast, and it was getting
            //scattered by falling through this).
            SceneConfig config = _scene is SceneKind.City or SceneKind.NeonCity
                ? _cityConfig
                : _sceneRenderer.GetSceneConfig(_scene);

            WeatherPreset preset = WeatherLooks.TryParse(levelWeather)
                ?? config?.Weather
                ?? WeatherPreset.Scattered;

            _clouds.SetWeather(preset);
        }

        /// <summary>
        /// Loads a sky dome and re-derives the whole scene's lighting from it — the one place a dome change
        /// happens, shared by <see cref="SetScene"/>, the sky setting and a loaded level's own dome.
        /// </summary>
        internal void SetSkyDome(byte number)
        {
            _skyDome = number;
            _sky.DomeNumber = number;

            ApplySkyLighting();
        }

        /// <summary>
        /// The per-frame inputs the shared <see cref="SceneRenderer"/> needs. The rig holds five of the six and
        /// its cloud hook was captured once at load, so this allocates nothing — it used to build a fresh
        /// delegate and re-derive the sun's tint on every frame of the draw path.
        /// </summary>
        private SceneFrame BuildSceneFrame() => _rig.BuildSceneFrame(_camera, _wallClock);

        #region The setting's slices (the pipeline the bottom screens run)

        /// <summary>
        /// The setting on its own — the whole pipeline with both gameplay slots empty. Since #249 it is only
        /// what the <see cref="GameplayScreen"/> falls back to on the one frame it is still on the stack with
        /// no session left to draw: the front end itself has outgrown it, the backdrop slicing the pipeline
        /// open to hang a preview cluster in the slot.
        /// </summary>
        internal void DrawSetting()
        {
            SceneFrame sceneFrame = BeginSceneDraw();

            DrawSettingGlass();
            FinishSceneDraw(sceneFrame);
        }

        /// <summary>
        /// The sharp foreground layer this frame filled, waiting to be composited — or null when nothing
        /// presented itself. <see cref="FinishSceneDraw"/> records it and <see cref="CompositeForegroundLast"/>
        /// spends and clears it, which is why it can never carry a stale target into the next frame.
        /// <para>
        /// It exists because #242 moved the composite to the very end of the host's <c>Draw</c>, past the Myra
        /// desktop: the owner wanted the confetti falling over the UI and the cup allowed to cover a panel, and
        /// "last" then means later than the frame's own close.
        /// </para>
        /// </summary>
        private RenderTarget2D _foregroundToComposite;

        /// <summary>
        /// Puts the frame's sharp foreground layer over everything, the UI included — the last picture the frame
        /// draws (#242). Called from the host's <c>Draw</c> after the desktop has rendered and <b>before</b> the
        /// screenshot writer reads the back buffer, or a capture would miss the thing the page is about.
        /// </summary>
        internal void CompositeForegroundLast()
        {
            if (_foregroundToComposite == null) return;

            _pipeline.CompositeForeground(_foregroundToComposite);
            _foregroundToComposite = null;
        }

        /// <summary>
        /// Everything up to the frame's first gameplay slot: binds the HDR scene target, clears it to the
        /// dome's horizon, hands the clouds and the camera to the shaders, draws the sky, the backdrop and
        /// the island with its pit, and returns the <see cref="SceneFrame"/> the closing slices need. The
        /// gameplay screen draws its gun, cluster and trails after this; the backdrop goes straight on to
        /// <see cref="DrawSettingGlass"/>.
        /// </summary>
        internal SceneFrame BeginSceneDraw()
        {
            //The won cup (#183), drawn since #225 into the pipeline's SHARP FOREGROUND target rather than
            // the scene, and FIRST — before the scene target is bound — for a reason that cost a black
            // result screen to learn: binding a target whose usage is DiscardContents CLEARS it (that is
            // MonoGame's discard), so re-binding the scene mid-frame to route around the cup wiped
            // everything already drawn into it. Drawing the cup before the scene's own bind keeps every
            // target on the one bind-draw-unbind lifecycle the framework promises: the foreground is
            // resolved the moment the scene takes its place, the scene is cleared only ahead of the sky
            // the way it always was, and nothing is ever bound twice in one frame.
            //
            //Why a separate target at all: the result page's defocus takes everything the HDR pass holds,
            // and the one object it must not take is the cup being presented. Out here the blur is built
            // from a scene the cup is not in — so no blurred copy of it stands behind the sharp one — and
            // the cup is composited back over the resolved frame in FinishSceneDraw, through the same
            // exposure, curve and grain, losing nothing but the softening; its glints still glare, because
            // the resolve feeds the foreground layer's own bright pass into the bloom pyramid. One thing
            // the layer does flip: the weather and the celebrations stay in the HDR scene and are
            // therefore BEHIND the cup, where the cup's old place in the pass had snow and confetti cross
            // in front of it — the presented object reads as the nearest thing either way, which is what
            // #225 asked for. States are stated rather than inherited, for the reason the Testbed states
            // its rasterizer before the scene — what ran last here is last frame's resolve and it does not
            // promise anything (see the winding note in CLAUDE.md). WHICH states is the cup's own call
            // since #228: only it knows whether the tier up is a solid metal or a pane of crystal, and the
            // two want different blending and different depth (see TrophyPodium.Draw). What stays here is
            // the save and restore around it, because the frame that follows is this file's.
            //THE CONFETTI IS IN THIS LAYER TOO SINCE #242, for the cup's own reason: it fell inside the HDR
            // pass, which is exactly what the result page's defocus reads, so the campaign's last celebration
            // dissolved into bokeh along with the arena — the owner's words, it should stay sharp like the
            // trophy. Three consequences, all accepted rather than missed:
            //
            //  - IT LOSES SCENE OCCLUSION. Out here the depth buffer is not the scene's, so the island and
            //    the cluster no longer hide a chip falling behind them. Cheap at the moment it plays: the
            //    camera has been released onto its orbit and the page is going out of focus, so what the
            //    paper would have been occluded BY is soft and receding anyway.
            //  - it is drawn BEFORE the cup, so the cup still covers it. That is #225's ruling that the
            //    presented object reads as the nearest thing, and this issue did not ask to change it.
            //  - it had to become PREMULTIPLIED to live here (Confetti.fx and Confetti.cs both), because
            //    this target is cleared transparent and composited by coverage rather than drawn over an
            //    opaque scene.
            //
            //What it does NOT lose is being tonemapped with the frame — the composite runs it through the
            // same exposure, curve and grain — so the paper still reads as lit rather than luminous, which
            // was the reason it sat in the HDR pass in the first place (#215).
            bool trophyUp = _trophy != null && _trophy.Active;
            bool confettiUp = _confetti != null && _confetti.Active;

            if (trophyUp || confettiUp)
            {
                BlendState blend = GraphicsDevice.BlendState;
                DepthStencilState depth = GraphicsDevice.DepthStencilState;
                RasterizerState raster = GraphicsDevice.RasterizerState;

                GraphicsDevice.SetRenderTarget(_pipeline.ForegroundTarget);

                //Transparent, not black: the target's alpha is the coverage the composite blends by, and
                //it has to say "nothing presented here" everywhere the cup is not — and, for the crystal
                //tier, how much of the cup is there everywhere it is
                GraphicsDevice.Clear(Color.Transparent);

                if (confettiUp) _confetti.Draw(_camera);
                if (trophyUp) _trophy.Draw(_camera);

                GraphicsDevice.BlendState = blend;
                GraphicsDevice.DepthStencilState = depth;
                GraphicsDevice.RasterizerState = raster;
            }

            GraphicsDevice.SetRenderTarget(_pipeline.SceneTarget);

            //Cleared to the dome's horizon colour rather than a fixed one: at a wide aspect the bottom
            //corners can look below the horizon past both the dome and the island, and there any other
            //colour shows up as a band instead of blending into the hazed skyline.
            //The sky-replacing scenes (space, the dream, the cavern, the Moon) have no dome, so they clear to black
            //instead: their pass covers every pixel of the frame, and black is what would show if it ever did not.
            GraphicsDevice.Clear(SceneRenderer.ReplacesSky(_scene) ? Color.Black : new Color(_rig.HorizonLinear));

            //The weather runs off the same wall clock the balls pulse to, so it keeps drifting whatever the
            //game does. Handed to both shaders from the one field, which is what keeps the cloud the player
            //looks at and the shadow it throws across the cluster the same cloud.
            //
            //Space is the one scene with no weather at all: its dome is not drawn (Space.fx covers the frame),
            //and the cloud coverage is zeroed on the instanced effect so the cluster, island and gun are not
            //crossed by the shadows of a deck nobody can see — InstancedModel.fx calls CloudSunlight
            //unconditionally, and a gain left standing from the scene before would go on shadowing this one.
            _clouds.Time = _wallClock;

            if (SceneRenderer.ReplacesSky(_scene)) _clouds.SuppressOn(_instancingEffect);
            else
            {
                _clouds.ApplyTo(_skyEffect);
                _clouds.ApplyTo(_instancingEffect);

                _skyCameraPositionParam.SetValue(_camera.Position);

                //Stated before the frame's first draw rather than only after it. SkyDome.Draw sets the sampler
                //and the depth state it needs but neither the blend nor the cull mode, and what ran last is the
                //overlay's SpriteBatch (AlphaBlend, CullCounterClockwise) over the tonemap's full-screen quad
                //(Opaque, CullNone) — so the dome would be drawn under whichever of them finished the frame.
                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;

                _sky.Draw(_camera);
            }

            //The sea's submerge fade for missed balls — a no-op off the sea scene (see SceneRenderer.ApplySeaSubmerge).
            //It takes how far the LENS is under the water, because since #159 the fade is released by exactly what
            //the tonemap's murk takes over (the same call answers both, at the resolve below).
            _sceneRenderer.ApplySeaSubmerge(_instancingEffect, _scene,
                _sceneRenderer.LensSubmergedAmount(_scene, _camera.Position));

            //The kill plane's own fade for a ball about to be culled (#192) — scene-independent and pushed every
            //frame, including on the front end, which has no ball anywhere near it: see
            //SceneRenderer.ApplyKillPlaneFade and GameplayScreen.KILL_PLANE_Y's own remarks on why this asks the
            //screen for the value rather than keeping a second copy of it here.
            _sceneRenderer.ApplyKillPlaneFade(_instancingEffect, Screens.GameplayScreen.KILL_PLANE_Y);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //Stated rather than inherited, for the same reason: the scene's cull mode must not depend on
            //what the previous frame's last pass happened to leave behind.
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            //The setting: the backdrop, then the island standing in it. The two cities are the procedural
            //skyline under the shared shader's city technique; the other five are the SceneRenderer's
            //self-lit terrain and water, which need this frame's camera, sun and sky handed over.
            SceneFrame sceneFrame = BuildSceneFrame();

            //The scene's own point lights (the neon ring, the savanna's campfire, space's planetshine) onto the
            //shared instanced effect, so the island, the gun and the balls take them as well as the towers. The
            //clock is the balls' own, so the campfire's light and its flame billboard cannot drift.
            _sceneLights.Apply(_scene, _sceneRenderer, _cityConfig.NeonLook, _wallClock);

            if (_scene == SceneKind.City || _scene == SceneKind.NeonCity)
            {
                //The city's windows keep their own rhythm off the wall clock — a city's lamps do not stop
                //because the game is paused
                _cityRenderer.CityWindowTime = _wallClock;

                //Culled to the frustum and ordered near to far first. The ordering is what pays: the city's
                //pixel shader is the most expensive in the frame, and in generator order most of what it
                //shades is a facade another tower is standing in front of. See City.PrepareVisible.
                _cityVisible = _city.PrepareVisible(_camera);
                _cityRenderer.Draw(_camera, _city.Visible, _cityVisible, _sceneEffectParams);
            }
            //The target goes in so the cavern and the dream can be shaded at the back buffer's size and scaled
            //up (#155). Passed rather than remembered, so it cannot be the one the pipeline held before a
            //resize, a fullscreen switch or a quality step replaced it.
            else _sceneRenderer.DrawEnvironment(_scene, sceneFrame, _pipeline.SceneTarget);

            //The forest's scattered trees, rocks and stumps, drawn after the forest terrain they stand on and
            //before the island: one draw per kind per mesh variant, and per material within a tree. The scene
            //gate stays here because the component draws the wood whenever it is called — where it sits in the
            //frame and whether this frame wants it at all are this file's business, not its.
            if (_scene == SceneKind.Forest) _forestScatter.Draw(_camera);

            //The round island, opaque: its stone cap and concrete drum. Then the dark well behind the glass
            //drain, which is drawn in the solid-terrain scenes only — it fills the hole those shaders cut in
            //the ground, so the drain reads as a deep shaft rather than as a glass ring over bright sky haze —
            //and which brings the culling its open cone needs with it. Each slice owns the states its own
            //geometry wants; where they sit in the frame is this file's decision, which is the whole reason
            //ArenaIsland hands them over separately.
            _island.DrawIsland(_camera, _sceneEffectParams);
            _island.DrawPit(_camera, _sceneEffectParams, _scene);

            return sceneFrame;
        }

        /// <summary>
        /// The drain's gold beads and its glass, after the frame's opaque work: the beads are opaque but the
        /// funnel composites over everything drawn so far — including the gameplay screen's balls, which is
        /// why this is a separate slice the session calls after its own 3D.
        /// </summary>
        internal void DrawSettingGlass() => _island.DrawGlass(_camera, _sceneEffectParams);

        /// <summary>
        /// The frame's close: the scene's foreground weather — the mountain's snow, the sea's spray, the
        /// savanna's flame — settles over everything, and the resolve takes the HDR target to the back
        /// buffer. Display space from here on.
        /// </summary>
        internal void FinishSceneDraw(SceneFrame sceneFrame)
        {
            //The cup's layer, filled by BeginSceneDraw if a cup is up this frame. Asked for here rather
            // than carried in a field: the pair of slices is one pipeline, and what the frame's close
            // consumes is what its opening produced.
            //The same two tests BeginSceneDraw opened the layer on, and they have to stay the same two: a
            //layer filled and not composited is a celebration drawn into a target nobody reads, and one
            //composited without being filled is last frame's contents blended over this one.
            RenderTarget2D foreground = (_trophy != null && _trophy.Active) || (_confetti != null && _confetti.Active)
                ? _pipeline.ForegroundTarget
                : null;

            //A no-op in the two cities and the desert, which carry no overlay weather
            _sceneRenderer.DrawOverlays(_scene, sceneFrame);

            //The victory display goes last of the scene's own draws and before the resolve, so it is inside the
            //HDR pass and blooms through the glare like everything else that emits — which is the entire point
            //of a firework. Drawn from here rather than from a screen so it keeps running once the result page
            //covers the session (see Fireworks).
            _fireworks?.Draw(_camera);

            //The confetti used to be drawn HERE, after the shells and inside the same HDR pass — #242 moved it
            //out to the sharp foreground layer, where BeginSceneDraw now draws it and where the argument for the
            //move is written down. What it gave up in leaving: it no longer covers a firework it crosses (the
            //shells stay in the scene, so they are behind the whole layer), and the island no longer occludes
            //it. What it kept: the tonemap, which is what made the paper read as lit rather than luminous and
            //was the reason it sat in this pass at all (#215).

            //How far under the sea the lens is. Only the sea has water to get under, and only the drop cinematic
            //ever takes the camera down there — the play camera stands on the island. Zero everywhere else, which
            //is a no-op in the shader.
            //
            //It is SceneRenderer's answer since #159, not this file's arithmetic: the ball shader's submerge fade
            //is released by the same figure the murk arrives with (see ApplySeaSubmerge above), and two effects
            //that have to hand over cannot be reading two copies of one expression — this one and the Testbed's
            //were already two.
            float underwater = _sceneRenderer.LensSubmergedAmount(_scene, _camera.Position);

            //And how far the frame has gone out of focus — the ACTIVE screen's answer and nobody else's,
            //amount and shape together (Screens.IFrameBlurSource: a page blurs the whole frame, the session
            //blurs precise aim's periphery only, #214). Asked here rather than tracked in a field of this
            //class: the ramp belongs to the screen whose moment it is, and that screen is also the only one
            //still being updated while it runs. The foreground layer goes in with it — not for the defocus,
            //which never sees it, but for the glare, whose pyramid it still feeds.
            float defocus = 0f, defocusFocus = 0f;
            if (_screens.Active is Screens.IFrameBlurSource focusSource)
            {
                defocus = focusSource.FrameBlur;
                defocusFocus = focusSource.FrameBlurFocus;
            }

            _pipeline.Resolve(_wallClock, underwater, defocus, defocusFocus, foreground);

            //And the layer back on top of the resolved frame, in display space now but through the same curve,
            //so the only difference from its old life inside the HDR pass is that the defocus stopped
            //reaching it.
            //
            //IT IS NOT COMPOSITED HERE ANY MORE (#242). This used to be the first thing after the resolve, and
            //the reason written down was that "the HUD, the page and its panels belong over the cup". The owner
            //ruled the other way on both halves of it — the sparkles should fall over the UI, and the cup may
            //cover a panel — so the composite is deferred to the END of the host's Draw, after the Myra desktop
            //has rendered and before the screenshot writer reads the back buffer. What is recorded here is only
            //THAT there is a layer to composite: the frame's close cannot do it itself once "last" means later
            //than the frame's close.
            //
            //Which does introduce the field the comment above deliberately avoided, and honestly so: while both
            //halves lived in this file, "what the frame's close consumes is what its opening produced" held
            //locally. The composite now happens in a third place, so the state has to be somewhere both can
            //see. It is cleared by the composite itself, so a frame that never reaches here cannot leave a
            //stale target for the next one to blend.
            _foregroundToComposite = foreground;
        }

        #endregion
    }
}
