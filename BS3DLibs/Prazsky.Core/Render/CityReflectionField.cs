using Microsoft.Xna.Framework;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The windows' reflection map on the CPU, and <c>CityReflection.fxh</c>'s march over it line for line (#1119): whether a
    /// pane's mirror ray meets a tower <b>the way the shader follows it</b>, in the steps the renderer gives it, or runs out
    /// of them and keeps the sky. What frames a shot round the reflections asks this and not a ray through the towers'
    /// boxes: the boxes answer whether a tower stands along the ray, the march whether the game draws it there, and a ray
    /// grazing down a street that a box test calls a hit can spend every step the shader has before it gets across.
    /// <para>
    /// Of each texel only the two figures the march reads, the roof's height and the distance to the nearest tower (the
    /// shader's <c>.r</c> and <c>.a</c>); the id and the facade kind only shade what is met. A quarter of the map's size, so
    /// <see cref="CityReflectionMap"/> keeps one beside its texture (<see cref="CityReflectionMap.Field"/>) and the tests
    /// build one without a device.
    /// </para>
    /// </summary>
    public sealed class CityReflectionField
    {
        /// <summary>
        /// The march's own bound, <c>CITY_REFLECT_MAX_STEPS</c> in <c>CityReflection.fxh</c>: the loop stops there whatever
        /// the step budget asks.
        /// </summary>
        public const int MAX_STEPS = 64;

        private readonly float[] _roof;
        private readonly byte[] _distance;
        private readonly int _width, _height;
        private readonly Vector2 _origin;

        /// <summary>The highest roof in the city: a ray climbing past it meets nothing more.</summary>
        public float Top { get; }

        /// <summary>The street level, which every texel no tower stands on holds.</summary>
        public float Ground { get; }

        /// <summary>The field of <paramref name="city"/>, rasterised as <see cref="CityReflectionMap"/> rasterises its map.</summary>
        public CityReflectionField(City city)
            : this(CityReflectionMap.Rasterise(city, out int width, out int height, out Vector2 origin, out float top),
                width, height, origin, top, city.GroundY)
        {
        }

        internal CityReflectionField(Vector4[] texels, int width, int height, Vector2 origin, float top, float ground)
        {
            _roof = new float[texels.Length];
            _distance = new byte[texels.Length];
            for (int i = 0; i < texels.Length; i++)
            {
                _roof[i] = texels[i].X;
                _distance[i] = (byte)texels[i].W;   //a whole number of texels, at most CityReflectionMap.DISTANCE_CAP
            }

            _width = width;
            _height = height;
            _origin = origin;
            Top = top;
            Ground = ground;
        }

        /// <summary>
        /// Whether the mirror ray <paramref name="direction"/> (a unit vector) off the pane at <paramref name="origin"/>, on a
        /// face of normal <paramref name="faceNormal"/>, meets a tower's side or roof within <paramref name="steps"/> steps
        /// (the renderer's <c>CityReflectionSteps</c>), as <c>CityReflectMarch</c> marches it; false where the shader keeps
        /// the sky: a ray along the facade, one that climbs over the roofs, leaves the map, comes down to the street or runs
        /// out of steps. <paramref name="taken"/> is how many steps it took, the one it met the tower on included.
        /// </summary>
        public bool March(Vector3 origin, Vector3 faceNormal, Vector3 direction, int steps, out int taken)
        {
            taken = 0;
            const float texel = CityReflectionMap.TEXEL;

            //A texel and a half out from the pane along its normal, so the march starts outside the pane's own tower
            float away = direction.X * faceNormal.X + direction.Z * faceNormal.Z;
            if (away < 0.02f) return false;
            Vector3 start = origin + new Vector3(faceNormal.X, 0f, faceNormal.Z) * (texel * 1.5f);

            Vector2 perUnit = new Vector2(direction.X, direction.Z) / texel;
            float across = MathF.Max(MathF.Max(MathF.Abs(perUnit.X), MathF.Abs(perUnit.Y)), 1e-5f);
            Vector2 tPerTexel = new(1f / MathF.Max(MathF.Abs(perUnit.X), 1e-5f), 1f / MathF.Max(MathF.Abs(perUnit.Y), 1e-5f));
            Vector2 stepDir = new(perUnit.X >= 0f ? 1f : -1f, perUnit.Y >= 0f ? 1f : -1f);
            float t = 0f;

            for (int i = 0; i < MAX_STEPS; i++)
            {
                if (i >= steps) return false;
                taken = i + 1;

                Vector3 p = start + direction * t;
                if (p.Y < Ground || (direction.Y >= 0f && p.Y > Top)) return false;

                Vector2 g = (new Vector2(p.X, p.Z) - _origin) / texel;
                float cellX = MathF.Floor(g.X), cellZ = MathF.Floor(g.Y);
                if (cellX < 0f || cellZ < 0f || cellX >= _width || cellZ >= _height) return false;

                int index = (int)cellZ * _width + (int)cellX;
                float roof = _roof[index], distance = _distance[index];

                //Far from every tower: jump, a texel short of the nearest
                if (distance >= 2f)
                {
                    t += (distance - 1f) / across;
                    continue;
                }

                //Into a tower's side
                if (roof > p.Y && roof > Ground) return true;

                //A texel step: to where the ray leaves this texel
                Vector2 toEdge = new Vector2(stepDir.X > 0f ? cellX + 1f - g.X : g.X - cellX,
                    stepDir.Y > 0f ? cellZ + 1f - g.Y : g.Y - cellZ) * tPerTexel;
                float tStep = MathF.Min(toEdge.X, toEdge.Y);
                float yExit = p.Y + direction.Y * tStep;

                //Down onto the roof inside the texel
                if (direction.Y < 0f && roof > yExit && roof > Ground) return true;

                t += tStep + 1e-3f;
            }

            return false;
        }
    }
}
