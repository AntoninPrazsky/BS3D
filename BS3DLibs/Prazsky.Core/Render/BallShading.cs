namespace Prazsky.Core.Render
{
    /// <summary>
    /// Which of <c>InstancedModel.fx</c>'s <b>ball techniques</b> shades the patterned mesh parts of the next
    /// draw — what the surface does with the light that arrives at it, and nothing else.
    /// <para>
    /// This is a <b>rendering</b> concept and it lives here rather than beside the game's own
    /// <c>Prazsky.BS3D.GameStructure.BallStyle</c> because of the layering: the styles are what a <i>map</i>
    /// names and are read off a level file, while these are the programs the shader was compiled with. They
    /// correspond one for one today and there is no reason they must — a style could be given a technique the
    /// game already had, and this library cannot see the enum that would say so.
    /// </para>
    /// <para>
    /// <b>It replaced a <c>bool</c> (#304), and that is the whole reason it exists.</b> Two shadings behind
    /// <c>GlassBubble</c> was one flag; the eight styles split out of #272 behind eight flags would be eight
    /// ways to ask for two shadings at once. A renderer is in exactly one of these at a time, which is the fact
    /// an enum states and a set of bools cannot.
    /// </para>
    /// </summary>
    public enum BallShading
    {
        /// <summary>
        /// The moulded vinyl skin: gores, polar discs, welded seams and cast relief, drawn by
        /// <c>InstancedModelPattern</c>. The shading a renderer that has said nothing gets, and the one every
        /// ball in this game had before #258.
        /// </summary>
        Vinyl = 0,

        /// <summary>
        /// The hollow glass bubble (#258): a dyed soap film, transparent through the middle and iridescent
        /// along the rim, drawn by <c>InstancedModelBubble</c>.
        /// <para>
        /// <b>Selecting it is not enough to draw one.</b> A film is transparent, so the shell has to be put out
        /// as two passes with opposite cull modes and the right depth states between them — an order this
        /// property cannot express, because it says how a pixel is shaded and not in what order the pixels
        /// arrive. <c>Prazsky.BS3D.BallRenderSet.Draw</c> is the one caller that does both, and it asks
        /// <c>BallStyles.IsTransparent</c> rather than naming this member, so a second transparent shading gets
        /// the same treatment without that method being edited again.
        /// </para>
        /// </summary>
        Bubble = 1
    }
}
