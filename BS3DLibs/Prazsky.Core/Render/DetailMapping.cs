namespace Prazsky.Core.Render
{
    /// <summary>
    /// How <see cref="InstancedModelRenderer.DetailTexture"/> is placed onto a surface.
    /// </summary>
    public enum DetailMapping
    {
        /// <summary>
        /// Projected along the three world axes and blended by the surface normal. Needs no UVs,
        /// but the texture is fixed in world space, so it only suits objects that never move.
        /// </summary>
        Triplanar,

        /// <summary>
        /// Sampled through the model's own texture coordinates, so it stays put on a moving
        /// or rotating object. Requires the model to carry UVs.
        /// </summary>
        ModelUVs
    }
}
