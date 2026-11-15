using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// <b>The rover's traverse on Mars</b> (#745, #1115): the one copy of the line <c>Mars.fx</c> presses the ruts along
    /// (<see cref="RoverTracks"/>, pushed by <c>MarsBackdrop</c>), the clearing's stones keep off and the chapter intro's tracks
    /// shot runs up (<c>MarsIntroShots</c>). The Moon's is <see cref="MoonPlanting.TRAVERSE"/>.
    /// <para>
    /// The owner (#745): "Mars could also get the tracks of a rover ... a small easter egg". The references (745-klein,
    /// 745-zimage) run a twin chevron track off across the plain to the dusty horizon. It comes in from the mesas to the
    /// north-west down a straight arm, swings round the arena's west side in one even arc (a hundred units' radius, its
    /// nearest about 79 from the axis, inside the clearing), and runs off down a second straight arm towards the far
    /// side's mesas; both ends fade under dust (<c>RoverTracks.fxh</c>'s <c>TRACK_END_FADE</c>). The menu's orbit and the drop
    /// look across it from wherever they stand; the intro looks along it.
    /// </para>
    /// <para>
    /// ⚠ <b>The shader's track is <see cref="RoverTracks.POINTS"/> points spread evenly over the SPANS between these
    /// controls</b> (the Moon's lesson, #1114), so the arc is nine short spans and each arm two long ones. The first traverse
    /// had a span a control round its corners, and its swing met at up to 18 degrees between two of the shader's straight
    /// segments: a polygon in the intro's close-up. Now every bend is under 10 (<c>MarsIntroShotsTests</c>).
    /// </para>
    /// </summary>
    public static class MarsTracks
    {
        /// <summary>The traverse as designed: its control points, world XZ, from the north-west end.</summary>
        public static readonly Vector2[] TRAVERSE =
        {
            new(-260f, 120f), new(-209f, 108f), new(-158f, 98f), new(-133f, 90f), new(-114f, 78f), new(-97f, 59f), new(-86f, 37f),
            new(-80f, 13f), new(-79f, -10f), new(-84f, -34f), new(-94f, -54f), new(-109f, -72f), new(-171f, -130f), new(-232f, -186f),
        };

        /// <summary>The traverse as the shader draws it (<see cref="RoverTracks.Resample"/>), and as the stones keep off it.</summary>
        public static readonly Vector2[] TRAVERSE_PATH = RoverTracks.Resample(TRAVERSE);
    }
}
