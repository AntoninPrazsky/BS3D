namespace Prazsky.Core.Render
{
    /// <summary>
    /// The flock of birds circling overhead, shared by the savanna and desert scenes (one buffer and
    /// shader in <see cref="SceneRenderer"/>). Per-bird orbit radius/altitude/speed/phase are randomised
    /// inline at load and are not exposed here; these are the named, tunable flock parameters.
    /// </summary>
    public sealed class BirdsConfig
    {
        /// <summary>Number of birds in the flock (a look dial, like the snowflake count).</summary>
        public int Count { get; set; } = 9;

        /// <summary>Billboard width (wingspan) of each bird.</summary>
        public float Wingspan { get; set; } = 6f;

        /// <summary>Billboard height as a fraction of its width.</summary>
        public float Aspect { get; set; } = 0.55f;

        /// <summary>How far a bird drifts up and down over its circle.</summary>
        public float Bob { get; set; } = 2.5f;

        /// <summary>Near-black bird silhouette colour (linear).</summary>
        public Rgb Color { get; set; } = new(0.02f, 0.017f, 0.014f);

        /// <summary>Fixed point the flock circles, well above the cluster so the birds sit against the sky.</summary>
        public Vec3 FlockCenter { get; set; } = new(-20f, 34f, -75f);
    }
}
