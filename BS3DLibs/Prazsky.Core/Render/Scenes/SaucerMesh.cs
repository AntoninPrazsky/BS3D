using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flying saucer's two lathes (#955, drawn by <c>SaucerFlight</c> through <c>Saucer.fx</c>): the hull from the middle of
    /// its top plate over its slope to the rim and under to its belly, and the dome with the antenna over its crown, closed
    /// underneath on the hull's plate. Both shut solids, wound as every procedural mesh is (WindingCheck builds this on its
    /// own, without the effect).
    /// </summary>
    public sealed class SaucerMesh : IDisposable
    {
        //The hull, in its own frame: the rim's radius, its band of lights (the rim's face) and where the belly's glow starts
        public const float RIM_RADIUS = 7f;
        public static readonly Vector2 RIM_BAND = new(-0.24f, 0.02f);
        public static readonly Vector2 TOP_RING = new(5.6f, 0.45f);   //the ring of bulbs round the top: radius, height
        public const float UNDER_Y = -1.0f;

        //The dome: its base on the hull's top plate, its radius and its height
        public const float DOME_BASE = 0.9f, DOME_RADIUS = 2.3f, DOME_HEIGHT = 1.9f;

        public const int SEGMENTS = 48;

        //The hull from the middle of its top plate (under the dome) out over its slope to the rim and back under to the middle
        //of its belly: one closed solid
        private static readonly LathePoint[] HULL =
        {
            new(0f, DOME_BASE),
            new(DOME_RADIUS + 0.15f, DOME_BASE, crease: true),
            new(DOME_RADIUS + 0.35f, DOME_BASE - 0.05f),
            new(5.0f, 0.55f),
            new(6.6f, 0.22f),
            new(RIM_RADIUS, 0.02f, crease: true),
            new(RIM_RADIUS, -0.24f, crease: true),
            new(6.5f, -0.45f),
            new(4.5f, -0.80f),
            new(2.0f, -1.05f, crease: true),
            new(1.2f, -1.20f),
            new(0f, -1.25f)
        };

        /// <summary>The hull.</summary>
        public LatheMesh Hull { get; }

        /// <summary>The dome and its antenna.</summary>
        public LatheMesh Dome { get; }

        public SaucerMesh(GraphicsDevice graphicsDevice)
        {
            Hull = new LatheMesh(graphicsDevice, HULL, SEGMENTS);

            //The dome: the antenna over its crown (a ball on a thin rod), then a squashed hemisphere from the crown down to its
            //rim on the hull, closed underneath by a disc on the hull's top plate, so it is a shut solid
            const int DOME_RINGS = 8;
            float crown = DOME_BASE + DOME_HEIGHT;
            var dome = new LathePoint[DOME_RINGS + 5];
            dome[0] = new LathePoint(0f, crown + 0.95f);
            dome[1] = new LathePoint(0.13f, crown + 0.82f);
            dome[2] = new LathePoint(0.05f, crown + 0.70f, crease: true);
            dome[3] = new LathePoint(0.05f, crown + 0.03f, crease: true);
            for (int i = 1; i <= DOME_RINGS; i++)
            {
                float a = MathHelper.PiOver2 * i / DOME_RINGS;   //a little off the crown, a right angle at the rim
                dome[3 + i] = new LathePoint(MathF.Sin(a) * DOME_RADIUS, DOME_BASE + MathF.Cos(a) * DOME_HEIGHT, crease: i == DOME_RINGS);
        }
        dome[DOME_RINGS + 4] = new LathePoint(0f, DOME_BASE);
        Dome = new LatheMesh(graphicsDevice, dome, SEGMENTS);
        }

        public void Dispose()
        {
            Hull?.Dispose();
            Dome?.Dispose();
        }
    }
}
