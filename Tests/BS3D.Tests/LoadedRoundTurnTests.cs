using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using System;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// A loaded round lies turned in the barrel so the slot shows its colour cue's figure (#939, <see cref="BorePose.LOADED_TURN"/>):
    /// the owner saw "only a red ball" in the gun on the Savanna, the wool's red wave running round the round's own Y while the
    /// slot showed its Y pole. Held two ways: the round's frame is the barrel's turned by one constant and nothing else, and every
    /// figure of every set puts ink in the cap the slot shows.
    /// </summary>
    public class LoadedRoundTurnTests
    {
        private static readonly GameTime Frame = new(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));

        //Even directions over the sphere (a Fibonacci lattice), once for the class
        private static readonly Vector3[] Sphere = BuildSphere(40000);

        [Fact]
        public void ARoundsFrameIsTheBarrelsTurnedByOneConstantAndTheCuttersIsTheBarrelsOwn()
        {
            Cannon cannon = new(new Vector3(0f, 5f, 0f));
            Magazine magazine = new(() => BallType.Type1);

            //At rest, then aimed up and round, so the barrel's basis is not the identity's
            for (int step = 0; step < 3; step++)
            {
                BorePose pose = magazine.Pose(cannon, 1f);
                Matrix barrel = cannon.BarrelOrientation();
                Matrix turned = BorePose.LOADED_TURN * barrel;

                for (int slot = 0; slot < Magazine.SIZE; slot++)
                {
                    AssertSameBasis(turned, pose.SlotWorld(slot, out _));
                    AssertSameBasis(barrel, pose.SlotWorld(slot, out _, barrelFrame: true));
                }

                for (int frame = 0; frame < 30; frame++)
                {
                    cannon.Aim(new Vector2(0.6f, 0.4f), Frame);
                    cannon.Update(Frame);
                }
            }
        }

        [Fact]
        public void EveryFigurePutsInkInTheCapTheSlotShows()
        {
            //The cap the slot shows: directions within the slot's half angle of the barrel's up (CannonRig.SLOT_HALF_ANGLE)
            List<string> faults = new();

            for (int set = 1; set <= ColourCues.SET_COUNT; set++)
                foreach (BallType type in Enum.GetValues<BallType>())
                {
                    ColourCues.Figure figure = ColourCues.FigureOf(set, type);
                    float share = InkInCap(figure, BorePose.LOADED_TURN);
                    if (share < MIN_INK_IN_CAP) faults.Add($"set {set} {type} ({figure.Name}): {share:P0} of the cap");
                }

            Assert.True(faults.Count == 0, "Figures the slot hides: " + string.Join("; ", faults));
        }

        [Fact]
        public void UnturnedTheWoolsRedWaveWasHidden()
        {
            //The owner's case (#939), measured in the orientation the round had: the band runs round the round's own Y and
            //the cap is its Y pole. Kept as the reason for the turn, so a later change that undoes it fails here too
            ColourCues.Figure wave = ColourCues.FigureOf(ColourCues.SetOf(BallStyle.Wool), BallType.Type1);
            Assert.True(InkInCap(wave, Matrix.Identity) < MIN_INK_IN_CAP);
            Assert.True(InkInCap(wave, BorePose.LOADED_TURN) >= MIN_INK_IN_CAP);
        }

        //The least share of the cap a figure must cover to be told from a plain ball: under the sparsest figure's coverage
        //of a whole ball (the dotted line's 0.079), so a figure shown only as it is everywhere else passes
        private const float MIN_INK_IN_CAP = 0.05f;

        private static float InkInCap(ColourCues.Figure figure, Matrix turn)
        {
            Matrix frame = ColourCues.FrameOf(figure);
            Matrix back = Matrix.Transpose(turn);
            float cos = MathF.Cos(CannonRig.SLOT_HALF_ANGLE);
            int inCap = 0, inked = 0;

            foreach (Vector3 shown in Sphere)
            {
                if (shown.Y < cos) continue;

                inCap++;
                Vector3 own = Vector3.TransformNormal(shown, back);
                if (ColourCueField.Field(figure, ColourCueField.ToFrame(frame, own)) > 0f) inked++;
            }

            return inked / (float)inCap;
        }

        private static void AssertSameBasis(Matrix expected, Matrix actual)
        {
            float[] e = { expected.M11, expected.M12, expected.M13, expected.M21, expected.M22, expected.M23, expected.M31, expected.M32, expected.M33 };
            float[] a = { actual.M11, actual.M12, actual.M13, actual.M21, actual.M22, actual.M23, actual.M31, actual.M32, actual.M33 };
            for (int i = 0; i < e.Length; i++) Assert.Equal(e[i], a[i], 5);
        }

        private static Vector3[] BuildSphere(int count)
        {
            Vector3[] points = new Vector3[count];
            float golden = MathF.PI * (3f - MathF.Sqrt(5f));

            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float r = MathF.Sqrt(1f - y * y);
                float a = golden * i;
                points[i] = new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r);
            }

            return points;
        }
    }
}
