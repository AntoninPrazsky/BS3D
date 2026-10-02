using System.Text.Json.Serialization;

namespace Prazsky.BS3D.Levels
{
    /// <summary>
    /// <b>How soft this level's lattice is</b> (#690): the spring every socket between two balls is given after the
    /// cluster is built and after every landing, so a level drawn as a chain, a bridge or a hammock sags, bounces and
    /// ripples like rope or cloth. Absent — every level written before the field existed — is the builder's own stiff
    /// spring (15 Hz, critically damped), under which a dense lattice hangs as one rigid body.
    /// <para>
    /// <b>Per level and not per chapter</b>, because #617 and #690's prototype measured the same thing twice: one
    /// softening cannot serve different shapes, since a strand is a chain of springs in series and sags many times as
    /// far as a block at the same figure. Each level states what its own shape was probed at.
    /// </para>
    /// <para>
    /// <b>Only the sockets between balls.</b> The glass's anchors keep the builder's spring whatever this says; the
    /// start-of-level swing (#617) eases them back to it, and a softer anchor would fight that. See
    /// <c>LatticeSoftness</c> in the physics library, which is what applies this.
    /// </para>
    /// <para>
    /// No format bump, on <see cref="Level.Crates"/>' argument: an older build ignores the property and hangs the level
    /// stiff. That is a different level, which is why <c>LevelIdentity</c> hashes the whole file and gives it a board of
    /// its own, but it is not a broken file.
    /// </para>
    /// </summary>
    public sealed class SoftnessSpec
    {
        /// <summary>
        /// The spring's frequency in hertz: lower is softer. The builder's own is 15. The prototype's two measured
        /// settings were 6 (the spans belly 5–7.6 units deep at each bounce) and 3 (they bungee 11–15 units and
        /// ripple along their length).
        /// </summary>
        [JsonPropertyName("hz")]
        public float Frequency { get; set; }

        /// <summary>
        /// The damping ratio: 1 is critically damped, below it the lattice rings before it settles. 1 when the file
        /// does not say, which is the builder's own — the conservative reading of an unstated figure.
        /// </summary>
        [JsonPropertyName("damping")]
        public float Damping { get; set; } = 1f;

        /// <summary>
        /// The stiffest spring a level may state: half the simulation's 120 Hz step, past which Bepu's own guidance is
        /// that a spring is no longer resolved (and far enough past it, its springiness arithmetic overflows). The
        /// builder's own is 15, so nothing softening a lattice comes near it; a typo of a hundred does.
        /// </summary>
        public const float MAX_FREQUENCY = 60f;

        /// <summary>The heaviest damping a level may state: far past critical (1), a figure no softening needs, and the
        /// bound that keeps the product of the two from underflowing in the solver's arithmetic.</summary>
        public const float MAX_DAMPING = 20f;

        /// <summary>The smallest figure of either: a spring this weak or this undamped is no spring the solver can hold.</summary>
        public const float MIN_FIGURE = 0.01f;

        /// <summary>Both figures finite and inside what the solver can hold (<see cref="MIN_FIGURE"/> up to
        /// <see cref="MAX_FREQUENCY"/> and <see cref="MAX_DAMPING"/>).</summary>
        [JsonIgnore]
        public bool IsValid => float.IsFinite(Frequency) && Frequency >= MIN_FIGURE && Frequency <= MAX_FREQUENCY
            && float.IsFinite(Damping) && Damping >= MIN_FIGURE && Damping <= MAX_DAMPING;
    }
}
