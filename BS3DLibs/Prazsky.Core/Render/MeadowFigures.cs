using System.Collections.Generic;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// What the meadow's planting looks like to a camera (#609's third round, #893): the figures of the things a
    /// chapter intro's shots point at or must keep out of, in world space, as the planting placed them. The meadow
    /// is planted on a device (<see cref="MeadowScatter"/> uploads its meshes), so a host reads this off the live
    /// scene rather than rebuilding the planting; and a test that has no device builds the figures itself.
    /// </summary>
    /// <param name="Trees">The old trees, the one on the knoll first. A camera high over the hills keeps off each as its
    /// stem and its crown; a camera at a walker's height keeps off the <b>stem alone</b> (<see cref="PlantFigure.Root"/>
    /// to <see cref="PlantFigure.Crown"/>, <see cref="PlantFigure.Stem"/> thick): the crown's sphere is the mesh's
    /// bounding sphere, which on an oak reaches below the ground, so a lens anywhere under the tree is inside it.</param>
    /// <param name="Bench">The bench under the knoll's oak, as the sphere round its mesh at its instance
    /// (<see cref="PlantFigure.Crown"/> its centre, 1 unit over the ground at the scene's metre of two).</param>
    /// <param name="Bales">Every round hay bale, each as the sphere round its mesh at its instance. They stand in loose
    /// groups of one to three, and which are in one group is not recorded: a host groups them by how near they stand.</param>
    /// <param name="Solids">Everything else standing low that a lens near the ground must keep clear of: the shrubs and the
    /// hedgerows' shrubs and the boulders as spheres round their meshes, the fence's spans as capsules post to post
    /// (<see cref="PlantFigure.Root"/> to <see cref="PlantFigure.Crown"/>, <see cref="PlantFigure.Stem"/> thick, no
    /// sphere), and the bales and the bench again. The old trees are not in it, being <paramref name="Trees"/>.</param>
    public sealed record MeadowFigures(IReadOnlyList<PlantFigure> Trees, PlantFigure Bench,
        IReadOnlyList<PlantFigure> Bales, IReadOnlyList<PlantFigure> Solids);
}
