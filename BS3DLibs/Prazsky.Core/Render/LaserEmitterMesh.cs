using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// One of the four laser emitters on the funnel's rim (#874), in four parts so that each material is a draw of its
    /// own: the flared <see cref="Part.Pedestal"/> that stands on the gold band, the swivel <see cref="Part.Yoke"/> (two
    /// arms and a pin through the housing, the gun's trunnion language), the short ribbed <see cref="Part.Housing"/> and
    /// the glass <see cref="Part.Lens"/> set into its muzzle. The drawn references were a stubby cast-iron barrel with a
    /// thick rimmed lens, a ribbed collar behind it and a swivel on a round foot; every figure below is read off that.
    /// <para>
    /// <b>Two frames.</b> The pedestal and the yoke are modelled in the <i>stand</i> frame: the origin on the foot
    /// (the point on the band the pedestal sits on), +Y up, -Z towards the funnel's centre. The housing and the lens are
    /// modelled in the <i>housing</i> frame: the emission axis is +Y, the rear face at 0, the lens at
    /// <see cref="LENS_APEX_Y"/>; the layout (<c>LaserEmitterRing</c> in the game library) pitches that axis about the
    /// pin at <see cref="PIVOT_Y"/> above the foot. Everything is smaller than the gun's tube: about 1.3 units tall at
    /// most, which is what keeps the barrel passing over it at the lowest elevation.
    /// </para>
    /// <para>
    /// Wound clockwise from outside, like every procedural mesh: the lathes by their profile direction (a closed
    /// cross-section traced front, outside, back), the yoke through <see cref="MeshBuilder"/>, which corrects each face
    /// against the normal it is meant to show. <c>WindingCheck</c> judges the four.
    /// </para>
    /// </summary>
    public sealed class LaserEmitterMesh : IProceduralMesh, IDisposable
    {
        public enum Part { Pedestal, Yoke, Housing, Lens }

        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }
        public int PrimitiveCount { get; }
        public BoundingSphere BoundingSphere { get; }

        /// <summary>The pedestal's outer radius at its flange: the footprint the emitter takes of the band it stands on.</summary>
        public const float FOOT_RADIUS = 0.5f;

        /// <summary>How far above the foot the pin runs through the housing.</summary>
        public const float PIVOT_Y = 0.72f;

        /// <summary>Where along the housing's axis (from its rear face) the pin crosses it.</summary>
        public const float PIVOT_ALONG = 0.55f;

        /// <summary>The housing's body radius.</summary>
        public const float BODY_RADIUS = 0.38f;

        /// <summary>The collar's radius at the muzzle end: the widest the housing gets.</summary>
        public const float COLLAR_RADIUS = 0.46f;

        /// <summary>Where the lens's convex face crests on the housing's axis, from its rear face: the point the beam leaves from.</summary>
        public const float LENS_APEX_Y = 1.42f;

        /// <summary>Radius of the glass.</summary>
        public const float LENS_RADIUS = 0.29f;

        private const int SEGMENTS = 24;

        //The yoke: two arms from the pedestal's top up to the pin, standing just outside the housing's collar-free
        //body, and the pin through the housing with a stub proud of each arm
        private const float ARM_X = 0.48f;
        private const float ARM_HALF_WIDTH = 0.045f;
        private const float PIN_RADIUS = 0.085f;
        private const float PIN_HALF_LENGTH = ARM_X + ARM_HALF_WIDTH + 0.05f;
        private const float PEDESTAL_TOP = 0.30f;

        public LaserEmitterMesh(GraphicsDevice device, Part part)
        {
            switch (part)
            {
                case Part.Pedestal:
                    //Down the lathe's axis from the top, so the normals face outwards: top disc, the cup's rim, the neck, the
                    //flare to the flange and the flange's edge; sunk a hand's breadth into the band
                    Adopt(new LatheMesh(device, new[]
                    {
                        new LathePoint(0f, PEDESTAL_TOP, crease: true),
                        new LathePoint(0.30f, PEDESTAL_TOP, crease: true),
                        new LathePoint(0.30f, PEDESTAL_TOP - 0.06f, crease: true),
                        new LathePoint(0.21f, PEDESTAL_TOP - 0.10f, crease: true),
                        new LathePoint(0.21f, 0.14f),
                        new LathePoint(0.38f, 0.10f, crease: true),
                        new LathePoint(FOOT_RADIUS, 0.07f, crease: true),
                        new LathePoint(FOOT_RADIUS, -0.05f, crease: true),
                        new LathePoint(0f, -0.05f, crease: true),
                    }, SEGMENTS));
                    break;

                case Part.Housing:
                    //Front to back: the lens seat, the bore wall and the lip, the collar's step and the rib, the body, a
                    //second rib, then the taper to the rear face. The seat sits just behind the lens's back
                    Adopt(new LatheMesh(device, new[]
                    {
                        new LathePoint(0f, 1.28f, crease: true),
                        new LathePoint(LENS_RADIUS + 0.01f, 1.28f, crease: true),
                        new LathePoint(LENS_RADIUS + 0.01f, 1.40f, crease: true),
                        new LathePoint(0.36f, 1.45f, crease: true),
                        new LathePoint(COLLAR_RADIUS, 1.45f, crease: true),
                        new LathePoint(COLLAR_RADIUS, 1.36f, crease: true),
                        new LathePoint(BODY_RADIUS + 0.02f, 1.33f, crease: true),
                        new LathePoint(BODY_RADIUS + 0.02f, 1.22f, crease: true),
                        new LathePoint(BODY_RADIUS, 1.20f, crease: true),
                        new LathePoint(BODY_RADIUS, 0.98f, crease: true),
                        new LathePoint(0.43f, 0.96f, crease: true),
                        new LathePoint(0.43f, 0.86f, crease: true),
                        new LathePoint(BODY_RADIUS, 0.84f, crease: true),
                        new LathePoint(BODY_RADIUS, 0.42f, crease: true),
                        new LathePoint(0.29f, 0.10f, crease: true),
                        new LathePoint(0.29f, 0.0f, crease: true),
                        new LathePoint(0f, 0.0f, crease: true),
                    }, SEGMENTS));
                    break;

                case Part.Lens:
                    //A plano-convex disc: the dome cresting at LENS_APEX_Y, a short edge, and the flat back inside the seat
                    Adopt(new LatheMesh(device, new[]
                    {
                        new LathePoint(0f, LENS_APEX_Y),
                        new LathePoint(0.14f, LENS_APEX_Y - 0.01f),
                        new LathePoint(0.23f, LENS_APEX_Y - 0.04f),
                        new LathePoint(LENS_RADIUS, LENS_APEX_Y - 0.09f, crease: true),
                        new LathePoint(LENS_RADIUS, 1.26f, crease: true),
                        new LathePoint(0f, 1.26f, crease: true),
                    }, SEGMENTS));
                    break;

                case Part.Yoke:
                {
                    MeshBuilder builder = new();

                    //Two arms from the pedestal's cup up to the pin, and the pin through the housing across them
                    float armTop = PIVOT_Y + PIN_RADIUS * 1.6f;
                    float armBottom = PEDESTAL_TOP - 0.12f;

                    for (int side = -1; side <= 1; side += 2)
                        builder.AddBox(new Vector3(side * ARM_X, (armTop + armBottom) * 0.5f, 0f),
                            new Vector3(ARM_HALF_WIDTH, 0f, 0f), new Vector3(0f, (armTop - armBottom) * 0.5f, 0f),
                            new Vector3(0f, 0f, 0.13f));

                    //A saddle across the pedestal's top that the arms spring from, so they read as one casting
                    builder.AddBox(new Vector3(0f, armBottom + 0.05f, 0f), new Vector3(ARM_X + ARM_HALF_WIDTH, 0f, 0f),
                        new Vector3(0f, 0.05f, 0f), new Vector3(0f, 0f, 0.13f));

                    builder.AddTubeX(new Vector3(0f, PIVOT_Y, 0f), PIN_HALF_LENGTH, PIN_RADIUS, 16);

                    (VertexBuffer vertices, IndexBuffer indices, int primitives) = builder.Build(device);
                    VertexBuffer = vertices;
                    IndexBuffer = indices;
                    PrimitiveCount = primitives;
                    BoundingSphere = new BoundingSphere(new Vector3(0f, PIVOT_Y * 0.6f, 0f), 0.9f);
                    return;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(part));
            }

            //Adopt filled the buffers of the lathe that was just built
            void Adopt(LatheMesh lathe)
            {
                VertexBuffer = lathe.VertexBuffer;
                IndexBuffer = lathe.IndexBuffer;
                _primitives = lathe.PrimitiveCount;
                _bounds = lathe.BoundingSphere;
            }

            PrimitiveCount = _primitives;
            BoundingSphere = _bounds;
        }

        private int _primitives;
        private BoundingSphere _bounds;

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            VertexBuffer = null;
            IndexBuffer = null;
        }
    }
}
