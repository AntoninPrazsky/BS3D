namespace Prazsky.BS3D.Input
{
    /// <summary>The inputs the main menu's code is spelled in: a direction, or one of the two face buttons.</summary>
    public enum KonamiKey
    {
        Up,
        Down,
        Left,
        Right,
        B,
        A,
    }

    /// <summary>
    /// <b>The Konami code on the main menu</b> — #230's "menu edition": up, up, down, down, left, right, left,
    /// right, B, A, on the arrow keys and the letter keys or on the pad's D-pad and face buttons. Never explained
    /// anywhere; it is there for the players who try it on every game they own. The Game answers it with the
    /// title's own dance (<c>TitleWordmark.Celebrate</c>) and a short rising chime, and nothing else.
    /// </summary>
    public sealed class KonamiCode : SecretCode<KonamiKey>
    {
        public KonamiCode() : base(
            KonamiKey.Up, KonamiKey.Up, KonamiKey.Down, KonamiKey.Down,
            KonamiKey.Left, KonamiKey.Right, KonamiKey.Left, KonamiKey.Right,
            KonamiKey.B, KonamiKey.A)
        { }
    }
}
