using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// <b>Where a level's field meets the world, and where that world stops forgiving it</b> — the hang
    /// height, the death line, and the rule that decides whether a ball under the line has lost the level.
    /// <para>
    /// It stood in <c>GameplayScreen</c> alone until #301/#302, and it had to come out for one reason: the
    /// level generator now <i>hangs a level in a real simulation</i> to find out whether the remainder sags
    /// after a group is shot off (<c>Tools/LevelGen</c>'s sag gate). A gate that hangs a cluster somewhere
    /// else, or forgives a dip differently, answers a question about a different game — which is exactly the
    /// fault #288 was closed on, an arithmetic that was correct about the wrong moment. So the two callers
    /// share the figures rather than agreeing to keep two copies in step.
    /// </para>
    /// <para>
    /// It lives beside <see cref="Level"/> rather than in <c>Prazsky.Core</c> because it is about <i>this</i>
    /// game's field: a <see cref="BallsMap"/> hung over the arena's island. It reaches into
    /// <see cref="ArenaIsland"/> for the one figure the line is stated against, which is a reference
    /// <c>Prazsky.BS3D</c> already carries.
    /// </para>
    /// </summary>
    public static class ClusterHang
    {
        /// <summary>
        /// The height the field's topmost level hangs at, in lattice levels — for every field shallow enough
        /// that hanging it here keeps its bottom level clear of the death line; a deeper one is raised past it
        /// (see <see cref="FIELD_FLOOR_MARGIN"/>). It is where the game's previously hard-coded field put its
        /// top, kept exactly so the camera, the gun and the ceiling frame a loaded level the way they framed
        /// that one.
        /// </summary>
        public const int FIELD_TOP_LEVELS = 8;

        /// <inheritdoc cref="FIELD_TOP_LEVELS"/>
        public static readonly float FIELD_TOP_Y = FIELD_TOP_LEVELS / Constants.SQRT_TWO;

        /// <summary>
        /// The least the field's <b>bottom</b> level clears the death line by, and what raises a deep field
        /// past <see cref="FIELD_TOP_Y"/>: a ball's radius, so a ball hung in the field's lowest cell rests
        /// its surface exactly on the line — alive, one descent from loss. It makes the whole field playable
        /// by construction however deep it is, and turns the empty levels an author leaves under a layout
        /// into the level's starting clearance instead of dead space past the line.
        /// <para>
        /// Against the line's present seat the two branches of the max meet at a top level of ~17.9, so
        /// <b>every field up to 18 levels is pinned</b> at <see cref="FIELD_TOP_Y"/> — the whole shipped pack
        /// but the tall ones — and 19 is the first raised.
        /// </para>
        /// </summary>
        public const float FIELD_FLOOR_MARGIN = Constants.HALF;

        /// <summary>
        /// <b>The death line.</b> A ball below this has lost the level — stated against the island rather than
        /// as a number of its own: one unit above the drain's rim, which is the island's top surface, so the
        /// line sits just clear of the funnel a lost cluster falls into.
        /// <para>
        /// It was −5.5 until the owner reported the fault that moved it — two units higher, well above the
        /// gun's barrel, where a cluster that merely <i>swung</i> dipped under it a few shots into a level and
        /// ended it. That lever is now spent: it cannot go below <c>ArenaIsland.TOP_Y + 1</c> without the
        /// laser net (half a unit lower again) drawing inside the island cap it is meant to hover over.
        /// </para>
        /// </summary>
        public const float DEATH_Y = ArenaIsland.TOP_Y + 1f;

        /// <summary>
        /// <b>A cluster that merely swings has not lost</b> (#239) — how far past <see cref="DEATH_Y"/> a
        /// swing is allowed to reach, and for how long it may stay there.
        /// <para>
        /// Both are measured, on Chest — the level it was reported on, and the second-heaviest cluster in the
        /// pack — by a probe that fired a shot every 0.7 s and stepped the ceiling every 2 s, then detrended
        /// the lowest ball against a centred moving average (the baseline descends all level, so a raw minimum
        /// reads the whole descent as one dip). 35 swings over 67 s: deepest 0.82 units below the trend,
        /// longest 0.76 s, median 0.40 s, 90th percentile 0.71 s. A dip shallower than a unit AND shorter than
        /// a second is therefore forgiven; anything deeper or longer is the cluster genuinely arriving.
        /// </para>
        /// <para>
        /// <b>⚠ What this pair cannot forgive, and #301 is the report of it:</b> shooting a group off removes
        /// structure from one side, so the remainder rotates into a <i>new rest pose and stays there</i> — a
        /// lower equilibrium rather than a passing dip. The grace then expires and the level ends, correctly.
        /// The lever on that is the layout, not these two numbers.
        /// </para>
        /// </summary>
        public const float SWING_ALLOWANCE = 1f;

        /// <inheritdoc cref="SWING_ALLOWANCE"/>
        public const float BELOW_LINE_GRACE = 1f;

        /// <summary>
        /// The lattice-to-world offset a field is hung by, and the Y its topmost level ends up at.
        /// <para>
        /// Y hangs the top of the field at <see cref="FIELD_TOP_Y"/> — or higher, when the field is deep
        /// enough that its bottom level would start past the death line: a cell's height is its level index
        /// over √2, so without an offset every map would hang at its own depth rather than in one frame. The
        /// depth that matters is the <b>field's</b>, not the layout's: every cell has to be reachable without
        /// ending the level, or the empty levels an author left as growth room are a trap instead of a
        /// clearance.
        /// </para>
        /// <para>
        /// X and Z correct the residual half-unit <see cref="BallsMap.Center"/> can leave behind. An
        /// <b>odd</b> field tops out on an unshifted level, whose cells run 0…N-1 rather than 0.5…N-0.5, and
        /// the whole cluster would then hang half a unit off the axis the gun orbits and the camera looks
        /// down. The residual is measured off the centred top level rather than assumed away.
        /// </para>
        /// </summary>
        public static Vector3 FitWorldOffset(BallsMap map, out float fieldTopY)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            XZLevel size = map.GetStaticBallsArraySize();
            byte topLevel = (byte)(size.Level - 1);

            //The residual the centring leaves: the midpoint of the top level's own cells, measured through
            //the map's public centred-position accessor rather than re-deriving its arithmetic here
            Vector3 nearCorner = map.GetRealCenteredPosition(new XZLevel(0, 0, topLevel));
            Vector3 farCorner = map.GetRealCenteredPosition(new XZLevel(size.X - 1, size.Z - 1, topLevel));

            fieldTopY = MathF.Max(FIELD_TOP_Y, DEATH_Y + FIELD_FLOOR_MARGIN + topLevel / Constants.SQRT_TWO);

            return new Vector3(
                -(nearCorner.X + farCorner.X) * Constants.HALF,
                fieldTopY - topLevel / Constants.SQRT_TWO,
                -(nearCorner.Z + farCorner.Z) * Constants.HALF);
        }
    }

    /// <summary>
    /// What the death line has to say about one frame's lowest ball. See
    /// <see cref="ClusterHang.SWING_ALLOWANCE"/> for why a crossing is not immediately a loss.
    /// </summary>
    public enum ClusterLineVerdict
    {
        /// <summary>Above the line, or under it for less than the grace — the level goes on.</summary>
        Alive,

        /// <summary>
        /// Deeper past the line than any measured swing reaches, so there is nothing to wait for: the cluster
        /// has genuinely arrived and holding the verdict would only be a second of watching a lost level.
        /// </summary>
        PastAllowance,

        /// <summary>Under the line, held there for the whole grace without once coming back up.</summary>
        HeldTooLong,
    }

    /// <summary>
    /// The death line's verdict as a <b>rule over time</b> rather than over one frame — the state the
    /// <see cref="ClusterHang.BELOW_LINE_GRACE"/> half of the pair needs, in the one copy the game and the
    /// level generator's sag gate both run.
    /// <para>
    /// A struct with one float in it: the game holds it in a field beside the rest of its per-level state,
    /// and the gate holds one per simulated run. Nothing here allocates or formats — the caller decides what
    /// a verdict is worth saying, which is what keeps this callable from a per-frame path.
    /// </para>
    /// </summary>
    public struct ClusterLineWatch
    {
        /// <summary>
        /// How long the cluster's lowest ball has been at or below the death line without once coming back
        /// up. Reset by the rule itself the moment it does, so it measures a <b>held</b> crossing rather than
        /// a total.
        /// </summary>
        public float BelowLineSeconds { get; private set; }

        /// <summary>
        /// Reads this frame's lowest ball against the line and advances the grace.
        /// </summary>
        /// <param name="lowestBallY">World Y of the lowest ball still in the structure.</param>
        /// <param name="elapsed">
        /// <b>Real</b> seconds this frame — not a scaled simulation step. A swing takes the time it takes
        /// whatever the world is doing, so the grace it is measured against has to be wall time.
        /// </param>
        public ClusterLineVerdict Update(float lowestBallY, float elapsed)
        {
            if (lowestBallY <= ClusterHang.DEATH_Y - ClusterHang.SWING_ALLOWANCE) return ClusterLineVerdict.PastAllowance;

            //Otherwise the line has to be HELD rather than merely touched. No reset is needed when a level
            //starts: every level begins with its cluster far above the line, so the first frame zeroes this.
            BelowLineSeconds = lowestBallY <= ClusterHang.DEATH_Y ? BelowLineSeconds + elapsed : 0f;

            return BelowLineSeconds >= ClusterHang.BELOW_LINE_GRACE
                ? ClusterLineVerdict.HeldTooLong
                : ClusterLineVerdict.Alive;
        }
    }
}
