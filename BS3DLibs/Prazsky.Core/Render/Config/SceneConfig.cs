using System.Text.Json.Serialization;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The designer-facing configuration of a scene backdrop: one concrete subclass per
    /// <see cref="SceneKind"/>, one instance per scene inside <see cref="SceneRenderer"/>, and its values
    /// are <b>fixed in code</b> — a level names its scene and carries no config (level format 2), and
    /// nothing edits one at runtime (the map editor's live PropertyGrid over it, #33/#44/#45, went in
    /// #522). It was once both the persisted level data (issue #32, format 1) and the object that grid
    /// reflected over, which is why it is shaped as clean POCOs with public get/set properties; the shape
    /// stays because it reads well. Every default reproduces the current hard-coded look byte-for-byte, so
    /// a fresh config renders exactly as today.
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
        /// <see cref="WeatherPreset.Clear"/> here and mean it — and because a dial on the base is one every
        /// scene states in the same place.
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
        /// the base is one all twenty state in the same place — and while these three lived on
        /// <c>SavannaSceneConfig</c> the renderer's gate had to name that one scene.
        /// <para>
        /// <b>The default is off</b> (<see cref="ShadowConfig.Strength"/> 0, no target allocated and every
        /// receiver handed 0), so a scene opts in where the rest of its look is stated.
        /// </para>
        /// </summary>
        public ShadowConfig Shadows { get; set; } = new();
    }
}
