namespace Prazsky.Core.Render
{
    /// <summary>
    /// What a mesh of a model is made of, as far as the procedural surface detail is concerned.
    /// A model is rarely one material all over — a castle is stone walls, a timber door and glazing —
    /// and coursed stonework drawn across the lot gives the door brick joints it should never have.
    /// </summary>
    public enum SurfaceStyle
    {
        /// <summary>Only the plain micro-relief, with no construction pattern on top.</summary>
        Plain = 0,

        /// <summary>Coursed stone blocks: darkened mortar joints, cut in as real recesses.</summary>
        Masonry = 1,

        /// <summary>Sawn timber: boards with a groove between them and the long grain running along them.</summary>
        Wood = 2
    }
}
