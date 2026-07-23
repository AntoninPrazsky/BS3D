using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Serializable linear RGB colour. Values are linear radiance/reflectance and may exceed 1 for
    /// emissive or light colours, so a byte <see cref="Color"/> (0–255) is unsuitable — the three channels
    /// are edited individually.
    ///
    /// A <b>reference type</b> on purpose: Myra's PropertyGrid edits a nested value type's sub-properties on
    /// a boxed copy that is never written back, so a struct colour could not be edited live; a class is edited
    /// in place, so R/G/B changes reach the config the SceneRenderer reads. Kept a clean POCO with a
    /// parameterless constructor so System.Text.Json round-trips it through the property setters.
    /// </summary>
    public sealed class Rgb
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }

        public Rgb() { }
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public Vector3 ToVector3() => new(R, G, B);
        public static Rgb FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
    }

    /// <summary>Serializable 2D vector (e.g. a wind direction in the XZ plane). A reference type for the same
    /// reason as <see cref="Rgb"/> — so its components edit live in the PropertyGrid.</summary>
    public sealed class Vec2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vec2() { }
        public Vec2(float x, float y) { X = x; Y = y; }

        public Vector2 ToVector2() => new(X, Y);
        public static Vec2 FromVector2(Vector2 v) => new(v.X, v.Y);
    }

    /// <summary>Serializable 3D vector (e.g. a box size). A reference type for the same reason as
    /// <see cref="Rgb"/> — so its components edit live in the PropertyGrid.</summary>
    public sealed class Vec3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vec3() { }
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public Vector3 ToVector3() => new(X, Y, Z);
        public static Vec3 FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
