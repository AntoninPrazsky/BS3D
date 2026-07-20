using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The standard XNA three-light rig set up by <see cref="Microsoft.Xna.Framework.Graphics.BasicEffect"/>.EnableDefaultLighting().
    /// Shared by <see cref="InstancedModelRenderer"/> and by code that tints the rig (e.g. by the sky dome palette)
    /// for models rendered through <see cref="ModelRenderer"/>.
    /// </summary>
    public static class DefaultLighting
    {
        public static readonly Vector3 AmbientLightColor = new(0.05333332f, 0.09882354f, 0.1819608f);

        /// <summary>Key light (the "sun").</summary>
        public static readonly Vector3 Light0Direction = new(-0.5265408f, -0.5735765f, -0.6275069f);
        public static readonly Vector3 Light0Diffuse = new(1f, 0.9607844f, 0.8078432f);
        public static readonly Vector3 Light0Specular = new(1f, 0.9607844f, 0.8078432f);

        /// <summary>Fill light.</summary>
        public static readonly Vector3 Light1Direction = new(0.7198464f, 0.3420201f, 0.4293262f);
        public static readonly Vector3 Light1Diffuse = new(0.9647059f, 0.7607844f, 0.4078432f);
        public static readonly Vector3 Light1Specular = Vector3.Zero;

        /// <summary>Back light.</summary>
        public static readonly Vector3 Light2Direction = new(0.4545195f, -0.7660444f, 0.4545195f);
        public static readonly Vector3 Light2Diffuse = new(0.3231373f, 0.3607844f, 0.3937255f);
        public static readonly Vector3 Light2Specular = new(0.3231373f, 0.3607844f, 0.3937255f);
    }
}
