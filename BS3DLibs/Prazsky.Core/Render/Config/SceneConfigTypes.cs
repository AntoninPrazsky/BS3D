using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Serializable linear RGB colour. Values are linear radiance/reflectance and may exceed 1 for
    /// emissive or light colours. Kept as a plain POCO (rather than <see cref="Color"/> or
    /// <see cref="Vector3"/>) so it serializes cleanly and a Myra PropertyGrid can give it a colour editor.
    /// </summary>
    public struct Rgb
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }

        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public readonly Vector3 ToVector3() => new(R, G, B);
        public static Rgb FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
    }

    /// <summary>Serializable 2D vector (e.g. a wind direction in the XZ plane).</summary>
    public struct Vec2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vec2(float x, float y) { X = x; Y = y; }

        public readonly Vector2 ToVector2() => new(X, Y);
        public static Vec2 FromVector2(Vector2 v) => new(v.X, v.Y);
    }

    /// <summary>Serializable 3D vector (e.g. a box size).</summary>
    public struct Vec3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public readonly Vector3 ToVector3() => new(X, Y, Z);
        public static Vec3 FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
    }
}
