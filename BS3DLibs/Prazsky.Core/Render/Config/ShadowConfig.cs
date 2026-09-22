using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// A scene's sun shadow map (#469, carried to every backdrop by #471): the three designer-facing dials
    /// that decide whether the map exists at all, how far round the camera it reaches and how finely it is
    /// rasterised. It sits on the base <see cref="SceneConfig"/> rather than on one subclass because every
    /// backdrop with a sun over it can have shadows — the savanna was merely the first, and while these three
    /// lived on <c>SavannaSceneConfig</c> the renderer's gate had to name that scene.
    /// <para>
    /// <b><see cref="Strength"/> 0 is the default and it means "no map at all"</b>, not "a map nobody can
    /// see": the renderer allocates no target, draws no casters and hands every receiver a strength of 0,
    /// which is the uniform their <c>[branch]</c> skips their nine taps on. So a scene opts in by saying so
    /// in its own initializer, and a scene that never mentions shadows renders exactly what it rendered
    /// before there was anything to mention — the same argument <see cref="SceneConfig.Weather"/> makes for
    /// <see cref="WeatherPreset.Scattered"/>.
    /// </para>
    /// <para>
    /// <b>The fit's height range is deliberately not here.</b> It is not a designer's number: the savanna's
    /// runs from half a hill below the plain to a baobab and a half above it, which is to say it is derived
    /// from <c>HillHeight</c> and <c>BaobabHeight</c> — dials that are tuned. Authored as a figure beside
    /// them it would be a second copy of the terrain's own proportions, and a copy that drifts silently: the
    /// map keeps rendering, it simply stops covering what stands in it, and nothing in a frame says why.
    /// <c>SceneRenderer.ShadowFit</c> computes it per scene from that scene's own terrain figures instead.
    /// </para>
    /// </summary>
    public sealed class ShadowConfig
    {
        /// <summary>
        /// How dark a full shadow is: 1 takes the sun term away entirely, a little under it keeps a shadow
        /// from reading as a hole in the ground (the dome's ambient is untouched either way, which is what a
        /// shadow is). <b>0 means the scene has no shadow map</b> — see the type's remarks.
        /// </summary>
        public float Strength { get; set; }

        /// <summary>How far round the camera the map reaches, in world units square. Shadows exist inside it
        /// and fade out over the last few per cent of its width; the map's texel is this over
        /// <see cref="MapSize"/>.</summary>
        public float Extent { get; set; } = 260f;

        /// <summary>The map's size in texels a side, at High: 4096 over 260 units is 0.063 units a texel, and the
        /// nine-tap box's penumbra about a fifth of a unit — a sunlit edge. It was 2048 (0.13 a texel, a third of a
        /// unit) until #484, whose play-camera captures read that as blocky, and 2048 is still what the Game builds
        /// below High through <c>SceneRenderer.ShadowMapSizeCap</c>. Eight bytes a texel: 134 MB here, 33.5 at
        /// 2048, and 537 at the 8192 the Testbed's <c>shadowmap=</c> can ask for — which is why 8192 is a dial and
        /// not a default.</summary>
        public int MapSize { get; set; } = 4096;

        /// <summary>A scene's shadows, stated where the rest of its look is. The parameterless constructor is
        /// the <c>= new()</c> default every scene starts from (shadows off); the other states a scene's own.</summary>
        public ShadowConfig() { }

        public ShadowConfig(float strength, float extent = 260f, int mapSize = 4096)
        {
            Strength = strength;
            Extent = extent;
            MapSize = mapSize;
        }

        /// <summary>Whether this scene asks for a map at all — the renderer's first gate, before the tier and
        /// the sun's height.</summary>
        [JsonIgnore]
        public bool Enabled => Strength > 0f;
    }
}
