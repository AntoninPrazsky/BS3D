namespace Prazsky.Core.Render
{
    /// <summary>
    /// The trailer's detail (#1178): every distance rule that trades a far thing's detail for frame time, drawn at its finest
    /// from every lens, for a run whose frames are written to disk rather than shown in time. The owner saw the aurora's trees
    /// at the back pop from their far look to their near one as a tour's lens flew in. A run that films is not held to a frame
    /// budget, and a pop is what a film must not have.
    /// <para>
    /// What it moves:
    /// <list type="bullet">
    /// <item>the aurora stand's spires become spruces from <see cref="StandSpruces.MAXIMUM_SWAP_RADIUS"/>, not <see cref="StandSpruces.SWAP_RADIUS"/>;</item>
    /// <item>every planted tree wears the near crown in both passes (<see cref="ForestScatterRenderer.WearsNearCrown"/>);</item>
    /// <item>every broadleaf of a wood is dressed as a near one (<c>ForestDressing</c>);</item>
    /// <item>every city rooftop's clutter and equipment is drawn at any distance (<see cref="CityRooftops"/>);</item>
    /// <item>the sea's oil platforms wear their finer mesh at any distance (<c>OilRigs.DrawsFine</c>, #1169);</item>
    /// <item>every ball is the finest sphere (the Game's <c>BallRenderSet.LodBias</c>).</item>
    /// </list>
    /// What it leaves: the tier's own choices (a filming run asks for Ultra, which already maxes every one), the frustum
    /// culls (exact), and the fades that are part of a scene's look (the sea's swell, the clouds, the particles' near fade).
    /// </para>
    /// Set once, by the Game's <c>maxdetail</c> argument, before any scene is built (the wood's dressing is split when it
    /// is built), and never cleared within a run. Nothing reads it per instance in a hot loop beyond one branch.
    /// </summary>
    public static class RenderDetail
    {
        /// <summary>Whether every distance rule draws its finest (see the class).</summary>
        public static bool Maximum { get; private set; }

        /// <summary>Turns <see cref="Maximum"/> on for the rest of the run. Call before the first scene is built.</summary>
        public static void EngageMaximum() => Maximum = true;
    }
}
