using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Serializable, designer-facing configuration of a scene backdrop. There is one concrete subclass
    /// per <see cref="SceneKind"/>, and a level stores exactly one — the scene it plays in — serialized
    /// polymorphically with a "kind" discriminator.
    ///
    /// This type is both (a) the persisted level data (see issue #32) and (b) the object a Myra
    /// PropertyGrid reflects over to build the Level Editor UI (see issue #45), so it is kept as clean
    /// POCOs with public get/set properties. Every default reproduces the current hard-coded look
    /// byte-for-byte, so a fresh config renders exactly as today.
    ///
    /// Only <b>designer-meaningful</b> parameters live here (wave height, snowiness, tree/building counts,
    /// colours, light intensity, …). Rendering-<b>quality</b> knobs (mesh grid density and similar) belong
    /// to a future graphics-settings system, and hard limits stay as constants in <see cref="SceneRenderer"/>.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(SeaSceneConfig), "sea")]
    [JsonDerivedType(typeof(DesertSceneConfig), "desert")]
    [JsonDerivedType(typeof(SavannaSceneConfig), "savanna")]
    [JsonDerivedType(typeof(MountainSceneConfig), "mountain")]
    [JsonDerivedType(typeof(MeadowSceneConfig), "meadow")]
    [JsonDerivedType(typeof(CitySceneConfig), "city")]
    [JsonDerivedType(typeof(ForestSceneConfig), "forest")]
    [JsonDerivedType(typeof(SpaceSceneConfig), "space")]
    [JsonDerivedType(typeof(DreamSceneConfig), "dream")]
    [JsonDerivedType(typeof(CavernSceneConfig), "cavern")]
    [JsonDerivedType(typeof(MoonSceneConfig), "moon")]
    [JsonDerivedType(typeof(OutbackSceneConfig), "outback")]
    [JsonDerivedType(typeof(TropicalSceneConfig), "tropical")]
    [JsonDerivedType(typeof(VolcanoSceneConfig), "volcano")]
    [JsonDerivedType(typeof(MarsSceneConfig), "mars")]
    [JsonDerivedType(typeof(StormSceneConfig), "storm")]
    [JsonDerivedType(typeof(PolarSceneConfig), "polar")]
    [JsonDerivedType(typeof(AuroraSceneConfig), "aurora")]
    [JsonDerivedType(typeof(GridSceneConfig), "grid")]
    public abstract class SceneConfig
    {
        /// <summary>Which backdrop this config drives. Derived from the concrete type; not serialized.</summary>
        [JsonIgnore]
        public abstract SceneKind Kind { get; }

        /// <summary>
        /// What sky stands over this backdrop (#221). It is on the base class rather than on each subclass
        /// because every scene has weather — even the four that suppress the deck entirely, which say
        /// <see cref="WeatherPreset.Clear"/> here and mean it — and because a designer-facing dial on the
        /// base is one the map editor's PropertyGrid picks up for every scene at once.
        /// <para>
        /// <b>Scattered is the default and that is load-bearing</b>: it is the weather the game had before
        /// #221, to the last digit, so a scene that never states one renders exactly what it rendered
        /// before there was anything to state. A scene overrides it in its own initializer, where the
        /// argument for the choice belongs beside the rest of that scene's look.
        /// </para>
        /// <para>
        /// A level may override it in turn (<c>Level.Weather</c>), the way it already overrides the dome and
        /// the music — the scene says what the place is usually like and the level says what it is like
        /// today.
        /// </para>
        /// </summary>
        public WeatherPreset Weather { get; set; } = WeatherPreset.Scattered;

        /// <summary>
        /// The sun's cast shadows over this backdrop (#469, on the base class since #471). Here for the same
        /// reason as <see cref="Weather"/>: every scene with a sun over it can throw shadows, so a dial on
        /// the base is one the map editor's PropertyGrid picks up for all twenty at once — and while these
        /// three lived on <c>SavannaSceneConfig</c> the renderer's gate had to name that one scene.
        /// <para>
        /// <b>The default is off</b> (<see cref="ShadowConfig.Strength"/> 0, no target allocated and every
        /// receiver handed 0), so a scene opts in where the rest of its look is stated.
        /// </para>
        /// </summary>
        public ShadowConfig Shadows { get; set; } = new();
    }
}
