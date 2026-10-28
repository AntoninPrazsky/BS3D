using BS3D.Lighting;
using Microsoft.Xna.Framework;

namespace BS3D.Platform
{
    /// <summary>
    /// GamePi's <c>Game/Platform/KeyboardLights.cs</c>: the keyboard's lamps go through Windows' LampArray (#987), which Linux does
    /// not have, so here no keyboard is ever found and the game composes nothing for one. Same names and members, so the game calls
    /// it unchanged.
    /// </summary>
    internal static class KeyboardLights
    {
        /// <summary>Never: there is no keyboard to compose for.</summary>
        internal static KeyLightLayout Layout => null;

        /// <summary>Always: there is no keyboard to find, and the Settings row says so.</summary>
        internal static bool NoneFound => true;

        /// <summary>Nothing to start.</summary>
        internal static void Start() { }

        /// <summary>Nothing to open or close.</summary>
        internal static void SetWanted(bool wanted) { }

        /// <summary>Never called: there is no <see cref="Layout"/> to compose for.</summary>
        internal static void Submit(Vector3[] lamps, bool resend) { }
    }
}
