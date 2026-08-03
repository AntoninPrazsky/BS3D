using Microsoft.Xna.Framework;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// The boundary between the two vector types this project unavoidably has: <c>System.Numerics</c> on the
    /// physics side (Bepu speaks nothing else) and <c>Microsoft.Xna.Framework</c> on the game and render side.
    /// MonoGame ships the outbound half as <c>Vector3.ToNumerics()</c>; these are the inbound half, so the
    /// crossing reads the same in both directions and looks like what it is.
    /// <para>
    /// <b>Why a named call and not the implicit conversion.</b> MonoGame 3.8.5 also declares
    /// <c>implicit operator Vector3(System.Numerics.Vector3)</c>, so assigning one to the other compiles
    /// silently today. That is precisely the objection: with two vector types in one file and only their
    /// namespaces telling them apart, a crossing that is invisible at the call site is how a value ends up in
    /// the wrong frame with nothing on the line to say so. CLAUDE.md states the convention as conversions at
    /// the boundary; these make the boundary legible.
    /// </para>
    /// <para>
    /// It stood as three hand-rolled copies until #76 — a private <c>ToXna</c> in the Game's contact handler,
    /// and <c>new Vector3(v.X, v.Y, v.Z)</c> written out inline in the gameplay screen and in the cluster
    /// walk. Lives in <c>Prazsky.Core.Tools</c> beside <c>Constants</c>, <c>ColorSpace</c> and
    /// <c>Geometry</c>, which is where the framework-level helpers with no game knowledge already are: every
    /// library and every executable references <c>Prazsky.Core</c> already, and this needs nothing but the
    /// BCL and the MonoGame reference that assembly has at compile time — so no consumer takes on a
    /// dependency and no layer learns anything about the one above it.
    /// </para>
    /// </summary>
    public static class NumericsExtensions
    {
        /// <summary>A Bepu position, velocity or offset as the render side's own vector type.</summary>
        public static Vector3 ToXna(this System.Numerics.Vector3 vector) => new(vector.X, vector.Y, vector.Z);

        /// <summary>
        /// A body's orientation as the render side's own quaternion. Component for component in both
        /// libraries, XYZW, so this is a copy and not a change of convention.
        /// </summary>
        public static Quaternion ToXna(this System.Numerics.Quaternion quaternion) =>
            new(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
    }
}
