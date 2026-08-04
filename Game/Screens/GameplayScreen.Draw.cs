using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.Physics;
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
        #region The landing preview

        /// <summary>
        /// How much of the ghost is clipped away. It is the shader's <b>dissolve</b> — a noise cut over 7³ cells of
        /// the ball's own surface, not a transparency — which is why the preview needs no blend state, no sorting
        /// and no shader change: it is ordinary opaque geometry with most of itself missing, and it reads as "not
        /// there yet" rather than as a ball that is somehow faint.
        /// <para>
        /// Tuned against the <b>overview</b> and not against precise aim. Leaning in over the barrel the ghost is
        /// large and unmissable at almost any value; from the overview stand-off it is a few dozen pixels, and at
        /// 0.62 it was present but easy to miss. Going much lower is the opposite failure — a ghost with most of
        /// itself intact reads as a ball that is already there, which would have the player aiming somewhere else.
        /// </para>
        /// </summary>
        private const float PREVIEW_DISSOLVE = 0.5f;

        /// <summary>Red, for a crosshair over a shot that will not stick. Display space — the overlay is after the resolve.</summary>
        private static readonly Color PREVIEW_REFUSED = new(236, 74, 74);

        /// <summary>
        /// Solves where a shot fired this instant would land, from the barrel's own line. Called once a frame,
        /// after the step, so it reads the poses the player is actually looking at.
        /// </summary>
        /// <remarks>
        /// The origin and direction are taken exactly as <see cref="Shoot"/> takes them, from the same two
        /// properties — if the preview and the shot ever disagreed about where the bore points, everything below is
        /// worthless. The cell then comes from <see cref="ShotPlacement"/>, which is the same call the contact
        /// handler makes when a ball really lands.
        /// <para>
        /// Silent while a drop cinematic runs, because the gun does not answer at all then, and a ghost sitting in
        /// the cluster while the player cannot fire would read as a promise that is not being kept.
        /// </para>
        /// </remarks>
        private void UpdateShotPreview()
        {
            _previewHasCell = false;
            _previewReachesCluster = false;

            if (_cinematic.Engaged || _physicsBalls == null || _map == null) return;

            //Both radii, because the shot has one: the grown sphere is what the moving ball's surface sweeps
            if (!ShotPlacement.TryFindFirstHit(_physicsBalls, _cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall),
                    _cannon.AimDirection, 2f * BallsConstraintsBuilder.BALL_RADIUS,
                    out PhysicsBall hit, out Vector3 contact))
                return;

            _previewReachesCluster = true;
            _previewHasCell = ShotPlacement.TrySolveAgainstBall(_map, hit, contact, _clusterWorldOffset, out _previewCell);
        }

        /// <summary>
        /// Adds the ghost to the frame's collection: the colour actually loaded at the muzzle, in the cell it would
        /// land in, mostly dissolved away.
        /// </summary>
        /// <remarks>
        /// The colour is the magazine's front ball rather than a neutral grey on purpose — the useful question is
        /// not only "does this stick" but "does it stick <i>next to two more of its own</i>", and a grey ghost
        /// answers the first while hiding the second.
        /// <para>
        /// Drawn at the cell's <b>ideal</b> lattice position, not offset by the local sway the solve took out. The
        /// ghost is where the ball will come to rest once its constraints have settled, which is the cell, and
        /// chasing the sway would make it jitter against a cluster that is still moving.
        /// </para>
        /// </remarks>
        private void CollectShotPreview(in BallDrawFrame frame)
        {
            if (!_previewHasCell) return;

            Vector3 position = _map.GetRealCenteredPosition(_previewCell) + _clusterWorldOffset;

            frame.Add(_magazine.Peek(0), position, Matrix.CreateTranslation(position),
                BallRenderSet.UNOCCLUDED, PREVIEW_DISSOLVE);
        }

        #endregion

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
