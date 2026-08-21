using Microsoft.Xna.Framework;

namespace Prazsky.Core.Screens
{
    /// <summary>
    /// One thing the player can be looking at: a splash, a menu, a level being played, a pause over the top of
    /// it. Screens live on a <see cref="ScreenManager"/>'s stack, and a screen is only ever concerned with
    /// itself — what is underneath it is the manager's business.
    /// <para>
    /// <b>The two flags are the whole design.</b> Which screens draw and which update are separate questions,
    /// and every screen system that conflates them gets the same case wrong: a pause menu has to <i>draw</i>
    /// over the game it froze (or there is nothing behind it to have frozen) while stopping it
    /// <i>updating</i> — and a modal settings panel over that pause has to let both of them draw and neither
    /// of them update. Two independent flags say that; a single "is popup" flag cannot.
    /// </para>
    /// </summary>
    public abstract class Screen
    {
        /// <summary>The stack this screen is on. Set by <see cref="ScreenManager"/> when it is pushed.</summary>
        public ScreenManager Manager { get; internal set; }

        /// <summary>
        /// Whether the screens beneath this one are still drawn. False for a screen that fills the frame on its
        /// own — a splash, a main menu that owns the whole picture — and true for anything the player is meant
        /// to see through or around.
        /// </summary>
        public virtual bool DrawsUnderlying => false;

        /// <summary>
        /// Whether the screens beneath this one still update. Usually false: a screen that lets what is under
        /// it go on running is usually a screen that is not really in front of anything. The exceptions are a
        /// purely decorative overlay, and a screen that is <i>reporting</i> on what is underneath rather than
        /// interrupting it — an end-of-level page over the arena that has just been played, where freezing
        /// the world would turn the thing being reported on into a photograph of itself.
        /// </summary>
        public virtual bool UpdatesUnderlying => false;

        /// <summary>
        /// Whether this screen is the top of the stack, i.e. the one the player's input belongs to. A screen
        /// that reads input should check this rather than assume, since it goes on being updated while it is
        /// merely visible under a translucent screen above it.
        /// </summary>
        public bool IsActive => Manager != null && Manager.Active == this;

        /// <summary>
        /// Called once when the screen is put on the stack. The place for anything that has to agree with the
        /// state of the world <i>at the moment the player arrives</i> — which is most of what a menu shows.
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// Called once when the screen leaves the stack, whether it was popped, replaced or cleared away. Not
        /// called when another screen is merely pushed on top: the screen is still there, just covered.
        /// </summary>
        public virtual void Leave() { }

        /// <summary>Called when a screen above this one is pushed or popped, so it can re-read what changed.</summary>
        public virtual void CoveredChanged() { }

        public virtual void Update(GameTime gameTime) { }

        public virtual void Draw(GameTime gameTime) { }
    }
}
