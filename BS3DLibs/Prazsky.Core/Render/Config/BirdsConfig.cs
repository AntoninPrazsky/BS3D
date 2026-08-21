namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flock of birds circling overhead, shared by the savanna, desert and outback scenes (one mesh and
    /// one shader in <see cref="SceneRenderer"/>). Per-bird orbit radius, altitude, speed and phase — and
    /// since #235 the cycle of wingbeat bursts and glides each one flies — are randomised inline at load and
    /// are not exposed here; these are the named, tunable flock parameters.
    /// </summary>
    public sealed class BirdsConfig
    {
        /// <summary>Number of birds in the flock (a look dial, like the snowflake count).</summary>
        public int Count { get; set; } = 9;

        /// <summary>
        /// Wingspan of a bird, which is its whole size: <see cref="BirdMesh"/> is modelled to a span of
        /// exactly 1, so this is the only scale a bird takes.
        /// </summary>
        public float Wingspan { get; set; } = 6f;

        /// <summary>How far a bird drifts up and down over its circle. It also sets how far a bird tips its
        /// nose, since the pitch is taken from the climb this drift is actually doing.</summary>
        public float Bob { get; set; } = 2.5f;

        /// <summary>
        /// The plumage's dark <b>albedo</b> (linear) — not a finished radiance. It was the latter until #235,
        /// when the birds became real geometry lit by the scene's own sun and dome: seen from below against a
        /// bright sky the ambient term alone brings this out near black, which is what a backlit bird is, and
        /// the sun is what puts a flash on a bird's back as it banks over.
        /// </summary>
        public Rgb Color { get; set; } = new(0.055f, 0.048f, 0.038f);

        /// <summary>Fixed point the flock circles, well above the cluster so the birds sit against the sky.</summary>
        public Vec3 FlockCenter { get; set; } = new(-20f, 34f, -75f);
    }
}
