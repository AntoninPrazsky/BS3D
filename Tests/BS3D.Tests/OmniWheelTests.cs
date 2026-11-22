using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.BS3D;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The cannon's omni wheel (#129, its hub face #485 and #1212), held with no device: the body (plates, hub, hub face) is
    /// built from <see cref="CannonRig"/>'s own figures (read off the rig, so a retuned wheel is the one tested) and stays
    /// inside the plates' radius, so only a roller touches the ground; it is shut; its published reach is its own; the laser
    /// emitters' clearance test models it whole; and the rollers are a rubber darker than the plates and the barrel, not black.
    /// The openness and winding of every mesh are also run by <c>Tools/WindingCheck</c>.
    /// </summary>
    public class OmniWheelTests
    {
        private static float Rig(string name) =>
            Convert.ToSingle(typeof(CannonRig).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null));

        private static Vector3 RigColour(string name) =>
            (Vector3)typeof(CannonRig).GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);

        private static readonly float WheelRadius = Rig("WHEEL_RADIUS");
        private static readonly float RollerRadius = Rig("ROLLER_RADIUS");
        private static readonly float PlateRadius = WheelRadius - RollerRadius;
        private static readonly float PlateOuterX = Rig("ROLLER_ROW_OFFSET") + RollerRadius + Rig("PLATE_THICKNESS");

        private static (List<VertexPositionNormalTexture> Vertices, List<int> Indices) Body() =>
            OmniWheelMesh.Compose(WheelRadius, RollerRadius, Rig("ROLLER_ROW_OFFSET"), Rig("HUB_RADIUS"), Rig("PLATE_THICKNESS")).Geometry;

        private static float Radial(Vector3 p) => MathF.Sqrt(p.Y * p.Y + p.Z * p.Z);

        [Fact]
        public void Nothing_of_the_body_reaches_past_the_plates_radius_so_only_a_roller_touches_the_ground()
        {
            float widest = Body().Vertices.Max(v => Radial(v.Position));

            Assert.True(widest <= PlateRadius + 1e-4f, $"the body reaches {widest} from the axle, past the plates' {PlateRadius}");
            Assert.True(PlateRadius < WheelRadius, "the plates stop inside the rollers' envelope");
        }

        [Fact]
        public void The_body_is_shut()
        {
            (List<VertexPositionNormalTexture> vertices, List<int> indices) = Body();

            Assert.Empty(MeshOpenness.ExposedLoops(vertices, indices));
        }

        [Fact]
        public void The_hub_faces_published_reach_is_the_meshs_own()
        {
            List<VertexPositionNormalTexture> vertices = Body().Vertices;

            //At its furthest exactly HUB_FACE_PROUD past a plate's outer face, on both sides
            float outermost = vertices.Max(v => v.Position.X), innermost = vertices.Min(v => v.Position.X);
            Assert.Equal(PlateOuterX + OmniWheelMesh.HUB_FACE_PROUD, outermost, 4);
            Assert.Equal(-(PlateOuterX + OmniWheelMesh.HUB_FACE_PROUD), innermost, 4);

            //And past the lip, nothing wider than the flange's edge
            float flange = OmniWheelMesh.HUB_FACE_RADIUS_FRACTION * PlateRadius;
            foreach (VertexPositionNormalTexture v in vertices)
                if (MathF.Abs(v.Position.X) > PlateOuterX + OmniWheelMesh.LIP_PROUD + 1e-4f)
                    Assert.True(Radial(v.Position) <= flange + 1e-4f, $"{v.Position} stands past the lip outside the flange's {flange}");
        }

        [Fact]
        public void The_laser_clearance_tests_model_of_a_wheel_holds_the_whole_wheel()
        {
            //The rollers reach the envelope, no further, and no wider than the plates' outer faces
            Assert.True(WheelRadius <= LaserEmitterClearanceTests.WHEEL_RADIUS);
            Assert.True(PlateOuterX + OmniWheelMesh.LIP_PROUD <= LaserEmitterClearanceTests.WHEEL_HALF_WIDTH);

            foreach (VertexPositionNormalTexture v in Body().Vertices)
            {
                float across = MathF.Abs(v.Position.X), r = Radial(v.Position);
                bool inWheel = across <= LaserEmitterClearanceTests.WHEEL_HALF_WIDTH && r <= LaserEmitterClearanceTests.WHEEL_RADIUS;
                bool inHubFace = across <= LaserEmitterClearanceTests.HUB_FACE_HALF_WIDTH && r <= LaserEmitterClearanceTests.HUB_FACE_RADIUS;

                Assert.True(inWheel || inHubFace, $"{v.Position} lies outside both discs the clearance test models the wheel as");
            }
        }

        [Fact]
        public void The_rollers_are_a_rubber_darker_than_the_plates_and_the_steel_but_not_black()
        {
            float roller = Luminance(RigColour("ROLLER_COLOR"));
            float plate = Luminance(RigColour("WHEEL_COLOR"));

            //Darker than everything else on the gun they used to match, so the rubber is a material of its own and the plates
            //carrying it stand apart from it standing still
            Assert.True(roller < plate, "the rollers are darker than the plates that carry them");
            Assert.True(roller < Luminance(CannonRig.STEEL_COLOR), "the rollers are darker than the barrel's steel");
            Assert.True(roller < Luminance(CannonRig.FRAME_COLOR), "the rollers are darker than the carriage's iron");

            //Not black: the flutes have to shade for the turning to show. At sRGB 0.18 the rollers measured 17 of 255 in the
            //meadow's daylight and read as black; the floor is the linear luminance of a 0.2 grey.
            Assert.True(roller >= ColorSpace.SrgbToLinear(0.2f), $"the rollers' luminance {roller} is black");

            //And a rubber's highlight, under the renderer's default white at 16 that makes a dark albedo glossy plastic. That
            //the rig hands it to the rollers' renderer needs a device to see: Tools/WindingCheck --export-cannon refuses a rig
            //that does not.
            Assert.True(Luminance(RigColour("ROLLER_SPECULAR")) < 1f && Rig("ROLLER_SPECULAR_POWER") < 16f);
        }

        private static float Luminance(Vector3 srgb) => ColorSpace.Luminance(ColorSpace.SrgbToLinear(srgb));
    }
}
