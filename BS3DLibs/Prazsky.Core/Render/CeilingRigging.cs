using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What the glass plate hangs from (#896): a steel cable at each corner, rising straight up out of the picture to a
    /// crane nobody sees, and the winch at its foot that pays it out. The owner's ruling on the four concepts drawn for
    /// it (2026-10-18) was this one, the cables, because nothing of it stands in the space the gun walks or the lens
    /// orbits: the plate never rotates and the cables leave it upwards, so from any camera under them they run off the
    /// top of the frame and end in empty sky. It answers the playtest note that the plate "levitates there magically":
    /// the plate steps down because the four drums let out cable, and lifts because they wind it in.
    /// <para>
    /// <b>The winch.</b> At each corner of the plate's top face, inside the crown's grind (<see cref="CeilingPlate.CROWN"/>),
    /// a drum lies on its side across the corner's diagonal on two pillow blocks and an axle, the cable wound round its
    /// core between two flanges (<see cref="DrumProfile"/>) and leaving from its outer side, so it rises clear of the
    /// glass. The drum turns with the plate's height and nothing else (<see cref="DrumTurn"/>): lower by one unit, and the
    /// core has let out one unit of cable. That is a pure function of the pose, so it cannot drift, needs no clock, and
    /// the brake's lift (#766) winds it back by the same law.
    /// </para>
    /// <para>
    /// <b>One class for every host</b>, beside <see cref="CeilingPlate"/>: the Game draws one under the session's plate
    /// and one under the front end's preview plate, the Testbed one under its own. Built once (nothing about it depends on
    /// the footprint but where it stands, which <see cref="Fit"/> keeps as four numbers), so its renderers are the same
    /// objects for the life of the host and a sky-lit enrolment can list them once (<see cref="Renderers"/>).
    /// <b>Opaque, and drawn with the setting</b>, before the host takes the copy the plate's glass bends: from under the
    /// plate, the drums are behind the glass and have to be in that copy to be seen through it (#541). It casts no sun
    /// shadow: a cable a few hundredths of a unit thick casts nothing a shadow map resolves.
    /// </para>
    /// </summary>
    public sealed class CeilingRigging : IDisposable
    {
        /// <summary>
        /// The cables' and the drums' steel: a galvanised grey a shade lighter and colder than the gun's barrel, so the
        /// rigging reads as wire rope and not as part of the gun.
        /// </summary>
        public static readonly Vector3 STEEL_COLOR = new(0.50f, 0.52f, 0.55f);

        /// <summary>The pillow blocks' cast iron, darker, the way a winch's frame is under its drum.</summary>
        public static readonly Vector3 FRAME_COLOR = new(0.24f, 0.24f, 0.26f);

        //How much of the sky the steel mirrors: the gun's own figures (CannonRig.SPECULAR_AMBIENT_STRENGTH for the barrel,
        //FRAME_SPECULAR_AMBIENT for the carriage), restated because this library cannot see the gun's
        private const float STEEL_SPECULAR_AMBIENT = 0.5f;
        private const float FRAME_SPECULAR_AMBIENT = 0.22f;

        /// <summary>
        /// The cable's radius, in world units (a ball is one across). Thin enough to read as wire against a pane a dozen
        /// balls wide, thick enough to stay two or three pixels wide from the play camera at 1600 x 900, where a thinner
        /// line breaks into a dotted crawl as the plate moves.
        /// </summary>
        public const float CABLE_RADIUS = 0.035f;

        /// <summary>
        /// How far above the plate's top face each cable runs: past anything a camera under the plate can frame looking up
        /// at it, so its end is never in the picture and the crane it hangs from never has to exist.
        /// </summary>
        public const float CABLE_RISE = 600f;

        /// <summary>
        /// How far in from both of the footprint's edges a winch stands: its corner's flanges and blocks clear the crown's
        /// grind (<see cref="CeilingPlate.CROWN"/>) and the corner's cut (<see cref="CeilingPlate.CORNER_RADIUS"/>) and
        /// stand on the flat top, over the border of flutes.
        /// </summary>
        public const float CORNER_INSET = 1.2f;

        //The drum, in its own frame (DrumProfile): the flanges' radius, the wound core's, the width between the flanges'
        //outer faces, a flange's thickness, how many wraps of cable lie on the core and how far each stands proud of it
        internal const float FLANGE_RADIUS = 0.32f;
        internal const float CORE_RADIUS = 0.24f;
        internal const float DRUM_WIDTH = 0.56f;
        internal const float FLANGE_THICKNESS = 0.05f;
        private const int WRAPS = 8;
        private const float WRAP_BULGE = 0.02f;

        //How unevenly the cable lies on the core, in world units: a drum that is round all the way round turns without a
        //sign of it, and this is what the eye catches turning when the plate steps
        private const float WINDING_WANDER = 0.012f;

        //The axle's radius, how far it stands proud of each block, and the blocks: their thickness along the axle, depth
        //across it, and how far their top stands over the axle
        private const float AXLE_RADIUS = 0.06f;
        private const float AXLE_PROUD = 0.03f;
        private const float BLOCK_WIDTH = 0.09f;
        private const float BLOCK_DEPTH = 0.24f;
        private const float BLOCK_CAP = 0.07f;

        //The axle's height over the top face: the flanges clear the glass by a few hundredths
        private const float AXIS_HEIGHT = FLANGE_RADIUS + 0.04f;

        private const int CORNERS = 4;

        private readonly LatheMesh _rod;
        private readonly LatheMesh _drumMesh;
        private readonly BoxMesh _blockMesh;
        private readonly InstancedModelRenderer _cable, _drum, _block, _axle;
        private readonly InstancedModelRenderer[] _renderers;

        //Rewritten in place every draw (the plate moves): nothing is allocated per frame
        private readonly ModelInstance[] _cables = new ModelInstance[CORNERS];
        private readonly ModelInstance[] _drums = new ModelInstance[CORNERS];
        private readonly ModelInstance[] _axles = new ModelInstance[CORNERS];
        private readonly ModelInstance[] _blocks = new ModelInstance[CORNERS * 2];

        //Where the winches stand, off the plate's centre: Fit's
        private float _cornerX, _cornerZ;
        private bool _fitted;

        /// <param name="device">Graphics device the meshes and the instance buffers live on.</param>
        /// <param name="instancingEffect">The shared instancing effect, the caller's to load (see <see cref="CeilingPlate"/>).</param>
        public CeilingRigging(GraphicsDevice device, Effect instancingEffect)
        {
            _rod = BuildRod(device);
            _drumMesh = BuildDrum(device);
            _blockMesh = new BoxMesh(device, BLOCK_WIDTH, AXIS_HEIGHT + BLOCK_CAP, BLOCK_DEPTH);

            _cable = new InstancedModelRenderer(device, _rod, STEEL_COLOR, instancingEffect) { SpecularAmbientStrength = STEEL_SPECULAR_AMBIENT };
            _drum = new InstancedModelRenderer(device, _drumMesh, STEEL_COLOR, instancingEffect) { SpecularAmbientStrength = STEEL_SPECULAR_AMBIENT };
            _axle = new InstancedModelRenderer(device, _rod, FRAME_COLOR, instancingEffect) { SpecularAmbientStrength = FRAME_SPECULAR_AMBIENT };
            _block = new InstancedModelRenderer(device, _blockMesh, FRAME_COLOR, instancingEffect) { SpecularAmbientStrength = FRAME_SPECULAR_AMBIENT };

            _renderers = new[] { _cable, _drum, _axle, _block };
        }

        /// <summary>
        /// Every renderer, for the host's sky-lit enrolment: the same four objects for the life of this rigging. The array
        /// itself, as <c>BallRenderSet.Renderers</c> hands back its own, so a list refilled every frame (the Testbed's)
        /// walks it without boxing an enumerator.
        /// </summary>
        public InstancedModelRenderer[] Renderers => _renderers;

        /// <summary>
        /// Stands the winches at the corners of a plate fitted to this field (<see cref="CeilingPlate.FootprintFor"/> of each
        /// cell count, the figures the caller hands <see cref="CeilingPlate.Fit"/>). Per level, never per frame; nothing is
        /// built.
        /// </summary>
        public void Fit(float stageSizeX, float stageSizeZ)
        {
            _cornerX = CeilingPlate.FootprintFor(stageSizeX) * Constants.HALF - CORNER_INSET;
            _cornerZ = CeilingPlate.FootprintFor(stageSizeZ) * Constants.HALF - CORNER_INSET;
            _fitted = true;
        }

        /// <summary>
        /// How far a drum has turned, in radians, with its plate's centre at <paramref name="plateY"/>: the cable it has let
        /// out is the height the plate has come down, so the turn is the height over the core's radius, the sign the one
        /// that brings the cable's side of the drum up as the plate goes down. Pure, so a test can hold the law.
        /// </summary>
        public static float DrumTurn(float plateY) => -plateY / CORE_RADIUS;

        /// <summary>
        /// The cables, the drums, their axles and their blocks, four instanced draws, opaque, hung from the plate's own pose
        /// (the kinematic body's world, the one the plate is drawn from). Nothing before the first <see cref="Fit"/>.
        /// </summary>
        public void Draw(ICamera camera, in Matrix plateWorld, BasicEffectParams effectParams)
        {
            if (!_fitted) return;

            Vector3 centre = plateWorld.Translation;
            Vector4 custom = new(0f, 0f, 0f, 1f);

            float axleLength = DRUM_WIDTH + 2f * (BLOCK_WIDTH + AXLE_PROUD);
            float blockOffset = (DRUM_WIDTH + BLOCK_WIDTH) * Constants.HALF;
            float blockY = centre.Y + CeilingPlate.THICKNESS * Constants.HALF + (AXIS_HEIGHT + BLOCK_CAP) * Constants.HALF;

            for (int k = 0; k < CORNERS; k++)
            {
                Corner(k, _cornerX, _cornerZ, centre, out Vector3 outward, out Vector3 axis, out Vector3 drumAt);
                Matrix frame = DrumFrame(outward, axis, drumAt);
                _drums[k] = new ModelInstance(Matrix.CreateRotationY(DrumTurn(centre.Y)) * frame, custom);

                //The axle through it, from block to block
                Matrix axleFrame = frame;
                axleFrame.Translation = drumAt - axis * (axleLength * Constants.HALF);
                _axles[k] = new ModelInstance(Matrix.CreateScale(AXLE_RADIUS, axleLength, AXLE_RADIUS) * axleFrame, custom);

                //The two blocks, standing on the glass at each end of the drum
                Matrix blockFrame = AxisFrame(axis, Vector3.Up, Vector3.Cross(axis, Vector3.Up), Vector3.Zero);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 at = drumAt + axis * (side == 0 ? -blockOffset : blockOffset);
                    blockFrame.M41 = at.X;
                    blockFrame.M42 = blockY;
                    blockFrame.M43 = at.Z;
                    _blocks[k * 2 + side] = new ModelInstance(blockFrame, custom);
                }

                //The cable off the core's outer side, straight up
                Vector3 foot = drumAt + outward * CORE_RADIUS;
                Matrix cable = Matrix.CreateScale(CABLE_RADIUS, CABLE_RISE, CABLE_RADIUS);
                cable.M41 = foot.X;
                cable.M42 = foot.Y;
                cable.M43 = foot.Z;
                _cables[k] = new ModelInstance(cable, custom);
            }

            _block.Draw(camera, _blocks, _blocks.Length, effectParams);
            _axle.Draw(camera, _axles, _axles.Length, effectParams);
            _drum.Draw(camera, _drums, _drums.Length, effectParams);
            _cable.Draw(camera, _cables, _cables.Length, effectParams);
        }

        /// <summary>
        /// One unit-radius, unit-tall cylinder for the cable and the axle both, scaled per instance: its side normals are
        /// radial and its caps' are along the axis, so a scale along the axis and an equal one across leave them true. Public,
        /// as <see cref="BuildDrum"/> is, for <c>Tools/WindingCheck</c>'s catalogue.
        /// </summary>
        public static LatheMesh BuildRod(GraphicsDevice device) => new(device, new[]
        {
            new LathePoint(0f, 1f),
            new LathePoint(1f, 1f, crease: true),
            new LathePoint(1f, 0f, crease: true),
            new LathePoint(0f, 0f),
        }, 8);

        /// <summary>The drum, wound (<see cref="DrumProfile"/>), its wraps a little uneven so its turn shows.</summary>
        public static LatheMesh BuildDrum(GraphicsDevice device) =>
            new(device, DrumProfile(), 20, irregularityAmplitude: WINDING_WANDER);

        /// <summary>
        /// The drum in section, about its own axle (<see cref="LatheMesh"/>'s Y): a flange at each end, and between them the
        /// core with the cable wound on it, a ridge a wrap, so the light rakes across the turns. Traced from the axle out
        /// along the top flange, down the outside and back in along the bottom, so the solid faces out. Only the wraps take
        /// the lathe's irregularity: the flanges stay true discs.
        /// </summary>
        internal static LathePoint[] DrumProfile()
        {
            float h = DRUM_WIDTH * Constants.HALF, inner = h - FLANGE_THICKNESS;
            var profile = new List<LathePoint>
            {
                new(0f, h),
                new(FLANGE_RADIUS, h, crease: true),
                new(FLANGE_RADIUS, inner, crease: true),
                new(CORE_RADIUS, inner, crease: true),
            };

            //Each wrap a bump, from the top flange down: its crest half a wrap in, the valley between two at the core
            float pitch = 2f * inner / WRAPS;
            for (int w = 0; w < WRAPS; w++)
            {
                float top = inner - w * pitch;
                profile.Add(new LathePoint(CORE_RADIUS + WRAP_BULGE, top - pitch * Constants.HALF, wobble: 1f));
                if (w < WRAPS - 1) profile.Add(new LathePoint(CORE_RADIUS, top - pitch, wobble: 1f));
            }

            profile.Add(new LathePoint(CORE_RADIUS, -inner, crease: true));
            profile.Add(new LathePoint(FLANGE_RADIUS, -inner, crease: true));
            profile.Add(new LathePoint(FLANGE_RADIUS, -h, crease: true));
            profile.Add(new LathePoint(0f, -h));
            return profile.ToArray();
        }

        /// <summary>
        /// Where winch <paramref name="corner"/> stands over a plate centred at <paramref name="plateCentre"/>: the corners in
        /// turn, (+,+), (-,+), (-,-), (+,-), <paramref name="cornerX"/> and <paramref name="cornerZ"/> off the centre (what
        /// <see cref="Fit"/> keeps). The drum's axle lies across the corner's diagonal, so its outer side, where the cable
        /// leaves, faces out along it; <paramref name="drumAt"/> is the middle of the axle.
        /// </summary>
        internal static void Corner(int corner, float cornerX, float cornerZ, Vector3 plateCentre, out Vector3 outward,
            out Vector3 axis, out Vector3 drumAt)
        {
            float sx = corner == 0 || corner == 3 ? 1f : -1f, sz = corner < 2 ? 1f : -1f;
            outward = Vector3.Normalize(new Vector3(sx, 0f, sz));
            axis = new Vector3(-outward.Z, 0f, outward.X);
            drumAt = new Vector3(plateCentre.X + sx * cornerX,
                plateCentre.Y + CeilingPlate.THICKNESS * Constants.HALF + AXIS_HEIGHT, plateCentre.Z + sz * cornerZ);
        }

        /// <summary>
        /// The drum's frame before its turn: the lathe's Y along the axle, its X outward. The rows of a right-handed frame,
        /// so a turn about the lathe's Y is one about the axle in the same sense, and <see cref="DrumTurn"/>'s sign holds.
        /// </summary>
        internal static Matrix DrumFrame(Vector3 outward, Vector3 axis, Vector3 drumAt) =>
            AxisFrame(outward, axis, Vector3.Cross(outward, axis), drumAt);

        //A frame whose local X, Y and Z run along the three given directions (rows, as the framework multiplies), at a point
        private static Matrix AxisFrame(Vector3 x, Vector3 y, Vector3 z, Vector3 at) => new(
            x.X, x.Y, x.Z, 0f,
            y.X, y.Y, y.Z, 0f,
            z.X, z.Y, z.Z, 0f,
            at.X, at.Y, at.Z, 1f);

        public void Dispose()
        {
            foreach (InstancedModelRenderer renderer in _renderers) renderer.Dispose();
            _rod.Dispose();
            _drumMesh.Dispose();
            _blockMesh.Dispose();
        }
    }
}
