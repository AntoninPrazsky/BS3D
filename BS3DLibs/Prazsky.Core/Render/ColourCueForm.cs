namespace Prazsky.Core.Render
{
    /// <summary>
    /// How a ball's colour carries its <b>second cue</b> (#737), the prototypes put in front of the owner before
    /// all thirteen colours are given one: a figure <see cref="Motif">painted</see> on the round ball, or a figure
    /// <see cref="Shape">carved</see> into it. Which figure a colour carries is a per-draw slot
    /// (<see cref="InstancedModelRenderer.ColourCue"/>), and which colours carry one is
    /// <c>Prazsky.BS3D.ColourCues</c>'s.
    /// <para>
    /// <b>A prototype switch, off by default everywhere</b>: only the Testbed's and the Game's <c>colourcue=</c>
    /// argument turn it on, so nothing a player can reach draws it. With it off the shipped ball programs draw,
    /// and their compiled code is the code they had before the prototypes existed ("The colour cue prototypes" in
    /// <c>docs/rendering/ball-styles.md</c>).
    /// </para>
    /// </summary>
    public enum ColourCueForm
    {
        /// <summary>No cue: colour is the only thing that tells the balls apart, as shipped.</summary>
        None = 0,

        /// <summary>
        /// The cue painted in the pixel shader: stripes, dots, checks or stars in a paler or darker shade of the
        /// ball's own colour (the vinyl's gores' white on the vinyl), and the silhouette stays a circle.
        /// </summary>
        Motif = 1,

        /// <summary>
        /// The cue carved into the ball: ridges, dimples, facets or twisted grooves, the mesh moved inward in the
        /// vertex shader (the silhouette changes) and shaded per pixel. Physics, the lattice and the match rule
        /// still see a sphere.
        /// </summary>
        Shape = 2,
    }
}
