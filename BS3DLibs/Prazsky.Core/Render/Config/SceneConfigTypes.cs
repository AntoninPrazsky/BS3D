using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Linear RGB colour in a scene config. Values are linear radiance/reflectance and may exceed 1 for
    /// emissive or light colours, so a byte <see cref="Color"/> (0–255) is unsuitable.
    /// <para>
    /// A <b>value type</b> since #522. It was a class for one reason only: Myra's <c>PropertyGrid</c> in the
    /// map editor edited a nested value type's sub-properties on a boxed copy it never wrote back, so a
    /// struct colour could not be tuned live there. That panel is gone, nothing edits a config at runtime,
    /// and a struct is what a three-float colour is — no heap object behind every colour field of every
    /// scene config. Nothing serializes these any more either: a level names its scene (format 2) and the
    /// format-1 reader takes only the name off an old file's config object.
    /// </para>
    /// </summary>
    public readonly struct Rgb
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public Vector3 ToVector3() => new(R, G, B);
        public static Rgb FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
    }

    /// <summary>2D vector in a scene config (e.g. a wind direction in the XZ plane). A value type for the
    /// same reason as <see cref="Rgb"/>.</summary>
    public readonly struct Vec2
    {
        public float X { get; }
        public float Y { get; }

        public Vec2(float x, float y) { X = x; Y = y; }

        public Vector2 ToVector2() => new(X, Y);
        public static Vec2 FromVector2(Vector2 v) => new(v.X, v.Y);
    }

    /// <summary>3D vector in a scene config (e.g. a box size). A value type for the same reason as
    /// <see cref="Rgb"/>.</summary>
    public readonly struct Vec3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public Vector3 ToVector3() => new(X, Y, Z);
        public static Vec3 FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
