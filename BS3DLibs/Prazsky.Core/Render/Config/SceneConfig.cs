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
    public abstract class SceneConfig
    {
        /// <summary>Which backdrop this config drives. Derived from the concrete type; not serialized.</summary>
        [JsonIgnore]
        public abstract SceneKind Kind { get; }
    }
}
