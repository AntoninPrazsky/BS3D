using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Where one planted thing stands and how much room it takes, in world space: the root on the ground, the
    /// centre of its crown and how far the crown reaches from it, and how thick the stem between them is. What
    /// a host that has to keep a camera <i>out</i> of a planting reads (the chapter intro's prologue shots,
    /// #559) — the instance arrays say where each thing is, but not how big its mesh variant is, and that is
    /// the half a lens needs.
    /// <para>
    /// The solid it describes is two pieces: a capsule of <see cref="Stem"/> radius from <see cref="Root"/> to
    /// <see cref="Crown"/>, and a sphere of <see cref="Reach"/> round <see cref="Crown"/>. Conservative by
    /// construction — both are taken from the variant's own bounding figures at the instance's scale.
    /// </para>
    /// </summary>
    public readonly struct PlantFigure(Vector3 root, Vector3 crown, float reach, float stem)
    {
        /// <summary>Where it stands on the ground.</summary>
        public readonly Vector3 Root = root;

        /// <summary>The centre of the crown (or of the whole mass, for a thing with no crown).</summary>
        public readonly Vector3 Crown = crown;

        /// <summary>How far the crown reaches from <see cref="Crown"/> in any direction.</summary>
        public readonly float Reach = reach;

        /// <summary>The radius of the stem between the root and the crown.</summary>
        public readonly float Stem = stem;

        /// <summary>
        /// A figure out of a mesh's own bounding sphere under an instance's world matrix — the sphere's centre
        /// moved with the instance, its radius scaled by the matrix's (uniform) scale.
        /// </summary>
        public static PlantFigure Of(BoundingSphere local, Matrix world, float stem)
        {
            float scale = new Vector3(world.M11, world.M12, world.M13).Length();
            return new PlantFigure(world.Translation, Vector3.Transform(local.Center, world), local.Radius * scale, stem * scale);
        }
    }
}
