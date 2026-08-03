using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.Core.Render;

namespace BS3D.Screens
{
    /// <summary>
    /// <b>The balls in the frame</b> — the loaded magazine drawn as real balls inside the bore, and the notes
    /// on the one walk that turns a simulated cluster into instances.
    /// </summary>
    /// <remarks>
    /// The walk itself is <see cref="Prazsky.BS3D.Physics.ClusterCollector"/>'s since #76, and the invariant
    /// it exists to hold is that <b>every ball is visited exactly once a frame</b>: the occlusion ease, the
    /// attach glide and the ripple advance all mutate state that lives on the ball, so a second walk would
    /// double-step all three. Nothing here may add one. Split out of <c>GameplayScreen.cs</c> in #72.
    /// </remarks>
    internal sealed partial class GameplayScreen
    {
        #region The balls in the frame

        //The walk that gathers the structure, the shots in flight and the balls falling is ClusterCollector's
        //(see the field), and the neighbour-based ambient occlusion it shades them with — a ball buried in the
        //mass is darker than one on the outside, which is what makes the cluster read as one body rather than a
        //heap of spheres — is derived by BallRenderSet.OcclusionTarget, the only thing that can build that
        //vector at all. That is where this game's worst ball bug was: the direction is a SUM of unit vectors,
        //one per occupied neighbour, and this file handed it over undivided, so it was up to twelve times too
        //long, the shader's dot against it saturated over most of the ball and every surface ball wore a hard
        //black crescent instead of the soft inward shading. The division cannot be forgotten now, and it must
        //not be done a second time here.

        /// <summary>
        /// The loaded queue, drawn as real balls inside the bore so they show through the barrel's slot —
        /// the player reads the next colour off them. They take the barrel's own basis: drawn unrotated they
        /// would hold a fixed world orientation while the barrel tilts around them, which reads as each
        /// ball skewing in its slot.
        /// <para>
        /// Into the same open frame the cluster went into, through the same
        /// <see cref="BallDrawFrame.Add"/> — but the loop is this screen's own, because which colours are
        /// loaded, where the bore puts them and the cross-fade below are all questions about this game rather
        /// than about drawing a ball. Taken as <c>in</c> since the frame is a ref struct.
        /// </para>
        /// </summary>
        private void CollectMagazineBalls(in BallDrawFrame frame)
        {
            //Taken once per frame rather than per ball, so each slot's place is a multiply and an add. The queue
            //rides the barrel, recoil included: it sits in the bore, so it goes back with it — the same stroke
            //the barrel itself was drawn with, or the balls float out of it. The pose also carries the barrel's
            //own basis, and each slot's matrix takes it with the translation written straight into its fourth
            //row rather than multiplied in (see BorePose.SlotWorld).
            BorePose pose = _magazine.Pose(_cannon, Game.CannonRig.PivotToFrontBall, CannonRecoilBack());

            for (int i = 0; i < Magazine.SIZE; i++)
            {
                Matrix world = pose.SlotWorld(i, out Vector3 position);

                //A ball whose colour was eliminated from the cluster is re-coloured where it sits, and the two
                //colours cross-fade by dithering against each other: the new one arrives (negative) while the
                //old one goes (positive), and the two cuts are exact complements, so every pixel of the sphere
                //is written by exactly one of the two draws. Both stay in the opaque path — no sorting, no
                //muddy overlap. A settled ball is a single draw at zero, which clips nothing.
                //
                //_magazineTransmute counts DOWN from 1 (just swapped) to 0 (settled), so the dissolve's own
                //progress is its complement. Feeding the countdown straight in runs the effect backwards: the
                //new colour arrives complete on the frame of the swap and the old one is never seen at all.
                float remaining = _magazineTransmute[i];

                //A ball in the barrel has nothing packed around it, so it carries the same unoccluded vector a
                //shot in flight does — off the one constant, rather than four literals written out here
                if (remaining > 0f)
                {
                    float progress = 1f - remaining;

                    frame.Add(_magazine.Peek(i), position, world, BallRenderSet.UNOCCLUDED, -progress);
                    frame.Add(_magazineFrom[i], position, world, BallRenderSet.UNOCCLUDED, progress);
                }
                else frame.Add(_magazine.Peek(i), position, world, BallRenderSet.UNOCCLUDED);
            }
        }

        #endregion
    }
}
