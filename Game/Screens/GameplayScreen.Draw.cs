using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;

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

        /// <summary>
        /// How far the ghost's dissolve swings either side of <see cref="PREVIEW_DISSOLVE"/>, and how often —
        /// the <b>blink</b>, and it is the ghost telling the truth about itself.
        /// <para>
        /// <b>Measured, because the honest answer turned out to be worse than the docs assumed.</b> Fired with
        /// Space, which leaves along the bit-identical pose the ghost was solved from — so with input staleness
        /// excluded entirely and only the physics left — 26 shots produced 10 landings of which
        /// <b>3 landed in the cell the ghost showed</b>. Five of the seven misses were one cell or one level away,
        /// which still points at the right pocket; two were three levels and two levels out, which does not. The
        /// causes are structural and none of them is a bug to fix: the shot crosses 1.667 units per physics step
        /// at <see cref="SHOOT_SPEED"/>, so the ball can have slid around the struck surface by the time the
        /// touch is reported; a glancing pass the analytic sweep counts can leave a <i>different ball</i> to
        /// anchor the solve and therefore a different ring of candidate cells; and once the first ring is full
        /// the second ring scores some thirty cells two levels wide by plain distance, which multiplies every
        /// one of those perturbations.
        /// </para>
        /// <para>
        /// These 26 shots predate the shot collidable becoming <b>swept</b> (see <see cref="PhysicsWorld"/>'s
        /// constructor), which places the contact at the solved time of impact rather than at the first
        /// overlapping step and so removes part of the first cause above. They have not been re-measured
        /// against it — the blink below is set from the worst case, which is the safe direction to be wrong in.
        /// </para>
        /// <para>
        /// So the ghost may not stand still and be read as a promise. A <b>smooth swing rather than a hard
        /// on/off</b>: the ghost has a second job — its colour is what answers "does it stick next to two more of
        /// its own" — and a ball that is absent half the time cannot be read for that. It also has to live beside
        /// a heartbeat: the cluster breathes at <c>BallRenderSet</c>'s 1.1 Hz, so this is deliberately faster,
        /// enough to read as unsettled rather than as alive. On the <b>wall clock</b>, like the beam's crawl and
        /// that heartbeat, because it is what the ghost <i>is</i> and not something the session is doing.
        /// </para>
        /// <para>
        /// The depth is what makes it unmissable at the overview stand-off, where the ghost is a few dozen pixels
        /// — the same argument that set <see cref="PREVIEW_DISSOLVE"/> itself. At ±0.22 the ball swings between
        /// mostly-there and mostly-gone; much less and the swing is lost in the dither's own noise at that size.
        /// </para>
        /// </summary>
        private const float PREVIEW_BLINK_DEPTH = 0.22f;

        /// <inheritdoc cref="PREVIEW_BLINK_DEPTH"/>
        private const float PREVIEW_BLINK_HZ = 2.2f;

        /// <summary>Red, for a crosshair over a shot that will not stick. Display space — the overlay is after the resolve.</summary>
        private static readonly Color PREVIEW_REFUSED = new(236, 74, 74);

        /// <summary>
        /// How far the beam is drawn when the aim reaches nothing at all. Only roughly meaningful: the dashes are
        /// faded out over the tail of it, so what the number decides is where the line has finished dying rather
        /// than where it stops. Comfortably past the cluster from the gun's orbit, so the beam never looks cut off
        /// short of something the player can see.
        /// </summary>
        private const float PREVIEW_OPEN_REACH = 24f;

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
        /// <b>Shown exactly when a shot would actually leave the barrel</b>, which is the rule that keeps it from
        /// promising anything: silent while a drop cinematic runs, because the gun does not answer at all then, and
        /// silent once <c>_score.OutOfShots</c> or once the level is decided — the <i>same</i> two tests
        /// <see cref="Shoot"/> refuses on, so the two cannot drift apart. A ghost sitting in the cluster over a
        /// spent budget, or over a level already won or lost, points at a landing the player can no longer buy.
        /// </para>
        /// <para>
        /// The decided level was deliberately <b>not</b> in that list while firing through the collapse was
        /// allowed. #177 stopped allowing it, so this followed in the same change — and it is what takes the
        /// beam and the ghost out of the frozen frame the result screen is read over, rather than leaving a
        /// line of light aimed at a level that is over.
        /// </para>
        /// </remarks>
        private void UpdateShotPreview()
        {
            _previewHasCell = false;
            _previewReachesCluster = false;
            _previewBeamVisible = false;

            if (_cinematic.Engaged || _score.OutOfShots || LevelDecided || _physicsBalls == null || _map == null) return;

            Vector3 muzzle = _cannon.MuzzlePosition(Game.CannonRig.PivotToFrontBall);
            Vector3 aim = _cannon.AimDirection;

            _previewMuzzle = muzzle;
            _previewBeamVisible = true;

            //Both radii, because the shot has one: the grown sphere is what the moving ball's surface sweeps
            if (!ShotPlacement.TryFindFirstHit(_physicsBalls, muzzle, aim,
                    2f * BallsConstraintsBuilder.BALL_RADIUS, out PhysicsBall hit, out Vector3 contact))
            {
                //Nothing out there. The beam still goes up, because in the overview it is the ONLY thing saying
                //where the gun points — but open-ended, so it thins away instead of ending at a phantom.
                _previewBeamEnd = muzzle + aim * PREVIEW_OPEN_REACH;
                return;
            }

            //Ended at the touch and not at the cell: that is where the ball actually stops, and it puts the
            //line's tip on the surface it strikes rather than pushing it through into the cluster
            _previewBeamEnd = contact;
            _previewReachesCluster = true;

            //The drift comes back with the cell because the cell alone does not say where anything is: the lattice
            //is where the level HUNG the field, and the cluster is wherever the glass has since dragged it. See
            //_previewDrift, and ShotPlacement.CellWorldPosition for the two things that separate the two.
            _previewHasCell = ShotPlacement.TrySolveAgainstBall(_map, hit, contact, _clusterWorldOffset,
                out _previewCell, out _previewDrift);
        }

        /// <summary>
        /// Draws the aim beam. Coloured by what the far end means — the loaded ball's own tint where the shot
        /// sticks, <see cref="PREVIEW_REFUSED"/> where it certainly will not.
        /// </summary>
        /// <remarks>
        /// This is what gives the <b>overview</b> the refusal signal the crosshair cannot: the crosshair is only
        /// drawn while precise aim is leaning in, so without the beam a player at the overview stand-off would see
        /// the ghost vanish and be told nothing about why. On the wall clock, so the crawl keeps going while a
        /// pause holds the session — the beam is a thing that is there rather than something the session is doing.
        /// </remarks>
        private void DrawShotPreviewBeam()
        {
            if (!_previewBeamVisible) return;

            //sRGB in 0…1 either way, which is what the beam decodes: Color.ToVector3 divides by 255, and the
            //type tints are already in that form — they are what LaunchSmears is handed for the same reason.
            Vector3 tint = _previewReachesCluster && !_previewHasCell
                ? PREVIEW_REFUSED.ToVector3()
                : BasicEffectParamsProvider.GetDiffuseTintByType(_magazine.Peek(0));

            //Faded out as precise aim leans in, which is the inverse of the crosshair's own opacity: the two are
            //one signal handed between the modes rather than two competing for the same pixels. Measured need
            //rather than taste — foreshortened along the bore the dashes pile up over the exact cell they point
            //at, and in that mode the crosshair on a lens aimed down the shot ray already IS the trajectory.
            _aimBeam.Draw(Camera, _previewMuzzle, _previewBeamEnd, tint, WallClock,
                openEnded: !_previewReachesCluster, opacity: 1f - _preciseAim.Blend);
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
        /// Drawn in the cluster's <b>live</b> frame (<see cref="ShotPlacement.CellWorldPosition"/>) and not at the
        /// cell's ideal lattice position, which it was until the descending ceiling proved that those are not the
        /// same thing. The lattice is where <see cref="FitFieldToMap"/> hung the field once; the cluster is
        /// wherever the glass has since dragged it, <see cref="CEILING_DESCENT_PER_STEP"/> at a time — so a ghost
        /// pinned to the lattice climbed away from the cluster as a level went on, until on
        /// <c>Colossus.json</c>'s eleven descents it was floating some nine levels above the pocket it claimed to be
        /// in. It also takes out the stretch the structure hangs with at rest, which is over a level's worth at
        /// the top of the cluster and was already putting the ghost beside its pocket rather than in it.
        /// </para>
        /// <para>
        /// The local drift is taken <i>whole</i> rather than only its vertical part, so the ghost sways with the
        /// pocket it sits in instead of standing still while the balls around it move. That is the point of
        /// measuring it at the ball that was hit: it is the same drift the cell was chosen with, so the ghost
        /// cannot be somewhere the decision did not mean.
        /// </para>
        /// </remarks>
        private void CollectShotPreview(in BallDrawFrame frame)
        {
            if (!_previewHasCell) return;

            Vector3 position = ShotPlacement.CellWorldPosition(_map, _previewCell, _clusterWorldOffset, _previewDrift);

            //And the blink: the ghost swings between mostly-there and mostly-gone rather than standing still,
            //because standing still would be a promise it cannot keep — 3 of 10 measured landings hit the cell it
            //showed. See PREVIEW_BLINK_DEPTH for the measurement and for why this is a swing and not an on/off.
            float dissolve = PREVIEW_DISSOLVE
                + PREVIEW_BLINK_DEPTH * MathF.Sin(MathHelper.TwoPi * PREVIEW_BLINK_HZ * WallClock);

            frame.Add(_magazine.Peek(0), position, Matrix.CreateTranslation(position),
                BallRenderSet.UNOCCLUDED, dissolve);
        }

        #endregion

        #region The round that fires next

        //THE ONE THE PLAYER IS ABOUT TO FIRE HAS TO BE THE ONE THEY CAN SEE, and it was the hardest of the five
        //(#175): two testers, independently, read a colour off the wrong ball and fired expecting a match that
        //could not happen. The cause is geometry and it cannot be fixed by geometry. From the stance the game is
        //actually played from — and from precise aim, which is the same stance leant in — the barrel HIDES ITS
        //OWN MUZZLE END: the open round shows only as the small ellipse of its cap at the top of the window,
        //while the ball behind it, under glass, is the large clearly-coloured thing that dominates the opening.
        //Verified by eye before anything was changed, from precise aim on a built level: a thin crescent of the
        //firing round above a full dome of the next one.
        //
        //Nothing could be handed to the notch to fix that. Its reach is already a ball radius, which lands
        //exactly on the seam where the front ball parts from the next (see CannonGlassMesh), and reaching
        //FURTHER back would begin uncovering the second ball — the opposite of the signal wanted. The balls
        //touch by construction (Magazine.SPACING is one diameter, so the queue reads as a full magazine rather
        //than one with gaps in it), so there is no strip of open bore between them to widen either.
        //
        //So the round is MARKED instead, and the channel for it was already wired end to end: ModelInstance's
        //ripple, which the cluster's landing wave rides and which the Game alone switches on (BallRenderSet's
        //`ripples`). A positive ripple adds mostly-white light to a ball — see the shader's own note on why
        //white and not the ball's hue — so slot 0 breathes brighter than its neighbours and the sliver, however
        //small, is the thing in the window that MOVES. A static cue of any kind is what failed here.

        //#236 CHANGED WHAT THE MARK IS MADE OF, and the reason is in the paragraph above: the ripple's positive
        //branch adds mostly-WHITE light, so the round whose colour most needs reading was the one continuously
        //washed towards white while the player aimed. It said "this one" by spending the very thing it was
        //there to reveal. The owner's words: the thing pulses, but the pulse itself is what blurs the colour.
        //
        //So the breath moved OFF the ball and into a halo AROUND it, in the round's own colour — BallGlow, one
        //additive camera-facing billboard whose middle the depth buffer removes for free (the ball is nearer
        //the lens than the quad behind it), so it is a ring outside the silhouette rather than a wash over it.
        //From this camera the barrel rejects most of it too, and what comes out of the notch reads as the gun
        //lit from inside by the colour it is about to fire.
        //
        //Two mechanisms were refused before that one and both are MEASURED, so neither is worth retrying: a
        //same-hue flare through this same ripple channel was tried at full strength (0.97) and could not be
        //seen on screen at all, because it piles energy into a channel already at the top of the ACES curve;
        //and the negative-ripple branch that REPLACES a ball's shading with a flat colour is a single uniform
        //per draw call, so it cannot carry one slot's own hue without per-instance data.
        //
        //What did not change is the CADENCE. The rate and the gate below are the ones #175 tuned, deliberately
        //kept: what was wrong with the mark was the channel, not its timing.

        /// <summary>
        /// The halo's strength at rest, 0…1. Where the ripple it replaced had to stay under the glare threshold
        /// to avoid bleaching the ball, this is <b>meant</b> to bloom — the colour is the signal, and a halo
        /// under the threshold is a faint ring nobody reads. <see cref="BallGlow"/> holds the radiance boost.
        /// </summary>
        private const float MUZZLE_GLOW_BASE = 0.62f;

        /// <summary>How far the breath swings either side of <see cref="MUZZLE_GLOW_BASE"/> — never to zero, so
        /// the round is never briefly unmarked, and never far enough to read as a fault.</summary>
        private const float MUZZLE_GLOW_SWING = 0.38f;

        /// <summary>
        /// How often it breathes. Deliberately its own rate: the balls' own heartbeat is at
        /// <c>BallRenderSet</c>'s 1.1 Hz and the landing ghost blinks at 2.2, so this sits between them and on
        /// neither's harmonic — a marker that beat with the cluster would read as part of it.
        /// </summary>
        private const float MUZZLE_MARK_HZ = 1.6f;

        /// <summary>
        /// The halo the muzzle round carries this frame, or zero when no shot would leave the barrel at all —
        /// which <see cref="BallGlow.Draw"/> reads as "draw nothing", so the gate lives in one place.
        /// <para>
        /// The gate is <see cref="_previewBeamVisible"/>, which is exactly the question "would a shot leave the
        /// barrel this instant" — the ghost and the aim beam are drawn on the same answer, and it is refused on
        /// the same three things <see cref="Shoot"/> refuses on (a drop cinematic, a spent budget, a decided
        /// level). Marking a round the player can no longer fire would be the same broken promise the ghost was
        /// taken out of the frozen frame for.
        /// </para>
        /// <para>
        /// On the <b>wall</b> clock, like the ghost's blink and the balls' own breath: it is a thing the round
        /// <i>is</i>, not something the session is doing, so it keeps breathing while a pause holds the frame.
        /// </para>
        /// </summary>
        private float MuzzleGlowStrength() =>
            _previewBeamVisible
                ? MUZZLE_GLOW_BASE + MUZZLE_GLOW_SWING * MathF.Sin(MathHelper.TwoPi * MUZZLE_MARK_HZ * WallClock)
                : 0f;

        /// <summary>
        /// Draws the muzzle round's halo. Called from the frame's additive slot — after the balls, so the depth
        /// buffer already holds the round and the barrel and can carve the ring out of this by itself, and
        /// before the smears, so a shot's own flare sits over it rather than under.
        /// </summary>
        private void DrawMuzzleGlow() =>
            _ballGlow.Draw(Camera, _muzzleBallPosition, Constants.HALF,
                BasicEffectParamsProvider.GetDiffuseTintByType(_magazine.Peek(0)), MuzzleGlowStrength());

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
            //rides the barrel, recoil included: it sits in the bore, so it goes back with it — off the gun's own
            //stroke since #115 (Pose reads Cannon.DrawnMuzzlePosition), so the balls and the tube cannot
            //disagree. The pose also carries the barrel's own basis, and each slot's matrix takes it with the
            //translation written straight into its fourth row rather than multiplied in (see BorePose.SlotWorld).
            BorePose pose = _magazine.Pose(_cannon, Game.CannonRig.PivotToFrontBall);

            for (int i = 0; i < Magazine.SIZE; i++)
            {
                Matrix world = pose.SlotWorld(i, out Vector3 position);

                //NO ball in the barrel carries a ripple any more (#236). Slot 0 used to breathe one — see the
                //region above for what that was for and why the channel was wrong — and the owner's ruling is
                //that no loaded round should pulse at all: whatever says "this one, right now" belongs beside
                //the ball, not on its shading. The halo does it, and the strip in the corner says it again in
                //2D. What is left here is the transmute's dither, which is a different thing entirely.
                const float mark = 0f;

                //And they do not breathe with the cluster either, which is the other half of that ruling and the
                //half the ripple could not carry (#252): the emissive heartbeat is a per-RENDERER uniform, so
                //"this ball is still" has to be a different DRAW rather than a value on the instance. That is
                //what `still` asks BallDrawFrame for. The owner's words, once the halo was in: it is enough that
                //the cannon's tip glows — so the loaded rounds radiate their colour steadily and the pulsing is
                //the gun's, in one place.
                const bool still = true;

                //Slot 0's world position, kept for the halo drawn later in the frame — stored rather than
                //recomputed from a second Pose() call, for the reason _previewMuzzle is: the ring and the ball
                //it rings cannot be allowed to disagree about where the bore is.
                if (i == 0) _muzzleBallPosition = position;

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
                //Both halves of a transmute take the mark, or the muzzle round would flicker between marked and
                //plain across the dither's two complementary cuts while it re-coloured itself
                if (remaining > 0f)
                {
                    float progress = 1f - remaining;

                    frame.Add(_magazine.Peek(i), position, world, BallRenderSet.UNOCCLUDED, -progress, mark, still);
                    frame.Add(_magazineFrom[i], position, world, BallRenderSet.UNOCCLUDED, progress, mark, still);
                }
                else frame.Add(_magazine.Peek(i), position, world, BallRenderSet.UNOCCLUDED, 0f, mark, still);
            }
        }

        #endregion

        #region The cluster profile

        /// <summary>
        /// Slots kept in <c>_profileBalls</c> beyond the field's own cell count, for the balls in the air that
        /// no cell holds. Only the shots can exceed the cells (a released group has already left the cluster
        /// array), and the kill plane culls a missed shot a beat after it passes — so this is far past what any
        /// fire rate can hold in flight at once. <see cref="AddBallsInFlight"/> bounds its write regardless.
        /// </summary>
        private const int PROFILE_FLIGHT_HEADROOM = 64;

        /// <summary>
        /// Builds the cluster profile the HUD draws as a side cut, from the live body poses and the ceiling's
        /// current state. Walks <see cref="_physicsBalls"/> exactly the way the loss check and the instance
        /// collection do — same null-check, same XZLevel sizing — so the cut's balls are the cluster the frame
        /// draws, this frame. Writes into the reused <see cref="_profileBalls"/> backing array and returns its
        /// occupied length through <paramref name="count"/>, so the caller can hand the HUD a span of exactly
        /// that — no per-frame allocation on the gameplay path.
        /// <para>
        /// The balls <b>in the air</b> are walked too, off <see cref="_shotBalls"/> and <see cref="_fallingBalls"/>
        /// — the two lists the cluster array by definition does not hold. Without them (#89) a shot was invisible
        /// on the panel for its whole flight and appeared only once it had attached, which is precisely the
        /// stretch the panel is worth watching: the panel's subject is where the glass stands against the
        /// cluster, and a ball on its way to becoming part of that is part of the answer.
        /// </para>
        /// </summary>
        private PlayHud.ClusterProfile BuildClusterProfile(out int count)
        {
            count = 0;

            //No session standing (the one frame the HUD draws over the fallback setting). count stays 0, so the
            //span built at the call site is empty by construction — and `new ReadOnlySpan<T>(null, 0, 0)` is
            //defined to return an empty span, so _profileBalls being null here is safe.
            if (_physicsBalls == null)
                return new PlayHud.ClusterProfile
                {
                    CeilingY = CeilingPlate.CentreYAbove(FIELD_TOP_Y),
                    TopY = CeilingPlate.CentreYAbove(FIELD_TOP_Y),
                    DeathY = CEILING_DEATH_Y,
                    HalfDepth = FieldHalfDiagonal(),
                    CameraRight = _gameplayCameraRight,
                };

            XZLevel size = XZLevel.FromArray(_physicsBalls);

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        PhysicsBall ball = _physicsBalls[x, z, level];
                        if (ball == null) continue;

                        //The drawn position, render-offset included — the same position the instance collection
                        //draws, so a ball gliding into its cell glides in the cut too.
                        System.Numerics.Vector3 pose = ball.BallReference.Pose.Position + ball.RenderOffset;

                        _profileBalls[count++] = new PlayHud.BallMarker
                        {
                            World = new Vector3(pose.X, pose.Y, pose.Z),
                            Type = ball.Type,
                        };
                    }

            AddBallsInFlight(_shotBalls, ref count);
            AddBallsInFlight(_fallingBalls, ref count);

            return new PlayHud.ClusterProfile
            {
                CeilingY = _ceilingY,
                CeilingFlash = _ceilingFlash,
                CeilingFeeding = _ceilingFlashIsFeed,
                TopY = _ceilingRestY,
                DeathY = CEILING_DEATH_Y,
                HalfDepth = FieldHalfDiagonal(),
                CameraRight = _gameplayCameraRight,
            };
        }

        /// <summary>
        /// Appends one list of loose balls to the profile: the shots still climbing at the cluster, or the balls
        /// the cluster has let go of on their way to the drain. Marked <see cref="PlayHud.BallMarker.InFlight"/>,
        /// which is what has the HUD draw them as rings rather than as cluster.
        /// <para>
        /// The bound is a guard and not an expectation: <see cref="_profileBalls"/> is sized at load with room
        /// for these (see <c>PROFILE_FLIGHT_HEADROOM</c>), so the check never fires — but the two lists are the
        /// one input to this walk whose length is not fixed by the field, and dropping a marker off the end of
        /// the panel is a far better failure than writing past the array.
        /// </para>
        /// </summary>
        private void AddBallsInFlight(List<PhysicsBall> balls, ref int count)
        {
            for (int i = 0; i < balls.Count && count < _profileBalls.Length; i++)
            {
                PhysicsBall ball = balls[i];
                if (ball == null) continue;

                System.Numerics.Vector3 pose = ball.BallReference.Pose.Position + ball.RenderOffset;

                _profileBalls[count++] = new PlayHud.BallMarker
                {
                    World = new Vector3(pose.X, pose.Y, pose.Z),
                    Type = ball.Type,
                    InFlight = true,

                    //Which way it is going, not which list it came from: a shot that missed is still in
                    //_shotBalls on the way back down, so the list cannot answer this. The panel's floor cull
                    //turns on it (#134).
                    Falling = ball.BallReference.Velocity.Linear.Y < 0f,
                };
            }
        }

        /// <summary>
        /// Half the field's diagonal in world units — the furthest a ball's projection onto the camera's right
        /// axis can reach from the centre, and so the span the HUD's horizontal axis maps across. The same
        /// footprint arithmetic <see cref="FitCannonAndGameCameraToLevel"/> uses to frame the field's corners.
        /// </summary>
        private float FieldHalfDiagonal()
        {
            if (_map == null) return 1f;

            float halfX = CeilingPlate.FootprintFor(_map.StageSizeX) * Constants.HALF;
            float halfZ = CeilingPlate.FootprintFor(_map.StageSizeZ) * Constants.HALF;
            return MathF.Sqrt(halfX * halfX + halfZ * halfZ);
        }

        #endregion
    }
}
