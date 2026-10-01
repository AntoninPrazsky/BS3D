using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// One crate a level stands in its play space (#257): where its centre is and how big it is, in world units. The
    /// centre is measured from the <b>field's own floor, on its axis</b> — the middle of the lattice's lowest level, the
    /// point a level is hung by — so a crate keeps its place against the cluster whatever height the field is hung at.
    /// Axis-aligned: a bounce off a crate is meant to be read, and the six faces of an upright box are the planes a
    /// player reads best. See <c>Crates</c> in the physics library for the bounce itself.
    /// </summary>
    public sealed class CrateSpec
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        /// <summary>Full size along X.</summary>
        [JsonPropertyName("w")]
        public float Width { get; set; } = 2f;

        /// <summary>Full size along Y.</summary>
        [JsonPropertyName("h")]
        public float Height { get; set; } = 2f;

        /// <summary>Full size along Z.</summary>
        [JsonPropertyName("d")]
        public float Depth { get; set; } = 2f;
    }
}
