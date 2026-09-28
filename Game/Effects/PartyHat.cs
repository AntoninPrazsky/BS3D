using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Effects
{
    /// <summary>
    /// <b>The gun's party hat</b>, worn on the game's birthday (#230's calendar surprise): a striped paper cone with
    /// a white trim and a pompom, sat on the crown of the barrel's base ring (<see cref="CannonRig.BreechCrown"/>)
    /// and carried by the barrel's own pose — so it recoils with every shot, walks with the gun and nods off with
    /// it when it sleeps.
    /// <para>
    /// <b>Cosmetic and nothing else</b>, by the line #230's design comment drew: a calendar egg that touched the
    /// physics or the rules would make one day a year play differently from the one the gates measured. It casts
    /// no shadow and takes no motion blur — it is drawn in the scene pass alone, and only on the day.
    /// </para>
    /// </summary>
    public sealed class PartyHat : IDisposable
    {
        //THE CONE, in barrel units (a ball is 0.5 across the radius, the base ring 0.845): a head-sized hat on a
        //head-sized breech. Tall for its width, the way a party hat is, so it reads as a hat from behind the gun
        //rather than as a traffic cone.
        private const float RADIUS = 0.26f;
        private const float HEIGHT = 0.85f;
        private const int BANDS = 5;

        //How far it is pushed down into the steel: the ring it stands on is only 0.16 long and the tube curves
        //away across it, so a base resting on the crown alone floats at its edges. The trim hides the rest.
        private const float SINK = 0.06f;

        //A jaunty lean to one side, in radians: a hat worn upright on the dead centre of a head reads as
        //placed there, not worn.
        private const float TILT = 0.2f;

        private const float TRIM_TUBE = 0.045f;
        private const float POMPOM_RADIUS = 0.1f;
        private const int SEGMENTS = 24;

        //Paper and card: two stripe colours across the wheel from each other, white trim and pompom. sRGB, like
        //every material colour the instanced renderer takes.
        private static readonly Vector3 STRIPE_A = new(0.93f, 0.16f, 0.52f);
        private static readonly Vector3 STRIPE_B = new(1.00f, 0.80f, 0.14f);
        private static readonly Vector3 TRIM = new(0.96f, 0.96f, 0.94f);

        //Matte: card barely reflects the sky, and a pompom does not at all
        private const float PAPER_SHEEN = 0.12f;

        private readonly List<IDisposable> _meshes = new();
        private readonly List<InstancedModelRenderer> _renderers = new();
        private readonly Matrix _onBarrel;

        /// <param name="rig">The gun it is worn on: where its crown is.</param>
        public PartyHat(GraphicsDevice device, Effect instancingEffect, CannonRig rig)
        {
            //The cone in BANDS stripes, alternating colour, each its own lathe: a lathe is one material. Traced
            //from the top of each band down its outside, which faces it outwards (LatheMesh's rule); the last
            //band closes its underside back to the axis.
            for (int b = 0; b < BANDS; b++)
            {
                float top = HEIGHT * (1f - b / (float)BANDS);
                float bottom = HEIGHT * (1f - (b + 1) / (float)BANDS);

                List<LathePoint> profile = new()
                {
                    new LathePoint(RadiusAt(top), top, crease: true),
                    new LathePoint(RadiusAt(bottom), bottom, crease: true),
                };

                if (b == BANDS - 1) profile.Add(new LathePoint(0f, bottom, crease: true));

                Add(device, instancingEffect, new LatheMesh(device, profile, SEGMENTS), b % 2 == 0 ? STRIPE_A : STRIPE_B, PAPER_SHEEN);
            }

            //The trim: a round tube about the base, traced clockwise in (radius, height) so it faces outwards
            List<LathePoint> trim = new();
            const int TRIM_STEPS = 12;
            for (int i = 0; i <= TRIM_STEPS; i++)
            {
                float angle = MathHelper.PiOver2 - MathHelper.TwoPi * i / TRIM_STEPS;
                trim.Add(new LathePoint(RADIUS + TRIM_TUBE * MathF.Cos(angle), TRIM_TUBE * MathF.Sin(angle)));
            }

            Add(device, instancingEffect, new LatheMesh(device, trim, SEGMENTS), TRIM, PAPER_SHEEN);

            SphereMesh pompom = new(device, POMPOM_RADIUS, 16, 10);
            Add(device, instancingEffect, pompom, TRIM, 0f);
            _pompomAt = Matrix.CreateTranslation(0f, HEIGHT, 0f);

            //From the hat's own frame (base on the origin, up +Y) onto the crown, leaning, and a little sunk
            _onBarrel = Matrix.CreateRotationZ(TILT)
                * Matrix.CreateTranslation(rig.BreechCrown - new Vector3(0f, SINK, 0f));
        }

        private readonly Matrix _pompomAt;

        private static float RadiusAt(float height) => RADIUS * (1f - height / HEIGHT);

        private void Add(GraphicsDevice device, Effect effect, IProceduralMesh mesh, Vector3 color, float sheen)
        {
            _meshes.Add((IDisposable)mesh);
            _renderers.Add(new InstancedModelRenderer(device, mesh, color, effect) { SpecularAmbientStrength = sheen });
        }

        /// <summary>Every renderer, for the host's sky-lighting enrolment: the hat sits in the scene's light like the gun.</summary>
        public IEnumerable<InstancedModelRenderer> Renderers => _renderers;

        /// <summary>
        /// Draws the hat on the gun.
        /// </summary>
        /// <param name="barrelWorld">This frame's barrel pose — the one the barrel itself was drawn with.</param>
        public void Draw(ICamera camera, Matrix barrelWorld, BasicEffectParams effectParams)
        {
            Matrix world = _onBarrel * barrelWorld;

            //The ground-darkening anchor, off the stone under the gun, as CannonRig.Draw sets the barrel's
            float ground = ArenaIsland.FloorHeightAt(MathF.Sqrt(world.M41 * world.M41 + world.M43 * world.M43));

            for (int i = 0; i < _renderers.Count; i++)
            {
                InstancedModelRenderer renderer = _renderers[i];
                renderer.GroundHeight = ground;

                //The pompom is the last renderer and the only one not at the base
                renderer.Draw(camera, i == _renderers.Count - 1 ? _pompomAt * world : world, effectParams);
            }
        }

        public void Dispose()
        {
            foreach (InstancedModelRenderer renderer in _renderers) renderer.Dispose();
            foreach (IDisposable mesh in _meshes) mesh.Dispose();
        }
    }
}
