using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.BS3D.GameObjects;
using System;

namespace Prazsky.BS3D
{
    /// <summary>
    /// Aiming the gun from a captured cursor, and from the pad's right stick. It stood in both executables with
    /// the same two dials and the same arithmetic until #76.
    /// <para>
    /// While the cursor is held, it is hidden and re-centred every frame, so what the aim reads is a
    /// <b>delta</b> from the viewport's centre rather than a position. <b>When</b> it is held is the caller's
    /// question, not this class's: the Testbed holds it for as long as a session runs, while the Game takes it
    /// on the player's first click in the picture and gives it back on every focus loss (#99), so that the
    /// window can still be moved and resized. Nothing here changes either way — a caller that does not want
    /// the cursor simply does not call <see cref="Recentre"/>. Three things about the delta are load-bearing:
    /// </para>
    /// <list type="bullet">
    /// <item><description>The delta is divided by the frame time, which cancels exactly against the frame time
    /// <see cref="Cannon.Aim"/> multiplies back in — so the aim moves a fixed amount per <i>pixel</i> at any
    /// frame rate, rather than a fixed amount per second.</description></item>
    /// <item><description>The centre is read from the <b>live</b> viewport every frame, not cached, so a resize
    /// or a fullscreen switch cannot leave the delta measured against a centre that has moved. That one is a
    /// recorded trap: the first frame after such a switch otherwise reads a delta of half the screen and slams
    /// the barrel into its elevation clamp, leaving the gun pointing at the sky.</description></item>
    /// <item><description>The first captured frame is <b>skipped</b> (see <see cref="Initialized"/>), so
    /// acquiring the cursor never jumps the aim.</description></item>
    /// </list>
    /// <para>
    /// The steps are exposed separately rather than as one routine, because the two callers interleave their own
    /// work between them and always did: the Game reads its shot edge and handles a running drop cinematic
    /// between applying the delta and re-centring, while the Testbed applies the delta, re-centres and then adds
    /// the pad. A single method would have forced one of those orders onto the other.
    /// </para>
    /// </summary>
    public sealed class MouseAim
    {
        /// <summary>
        /// Aim per pixel, after the frame-time cancellation: 0.001 × this, in radians — so 0.115° of aim per
        /// pixel of cursor travel, at any frame rate.
        /// <para>
        /// <b>This is the shipped feel and not the whole answer</b> (#384): it is an angle per <i>pixel</i>, so
        /// on its own it hands a 4K player half the sweep per hand movement that it hands a 1600×900 one, and a
        /// high-DPI mouse a different one again. What the player's own dial and the lens's lean both do is
        /// multiply it — see <c>rateScale</c> on <see cref="ApplyCursor"/>.
        /// </para>
        /// </summary>
        public const float SENSITIVITY = 2.0f;

        /// <summary>
        /// Right-stick aim rate. No <c>1/dt</c> here, unlike the cursor: a stick deflection is already a rate,
        /// so the frame time <see cref="Cannon.Aim"/> applies is exactly what it wants.
        /// <para>
        /// <b>Deliberately left a constant by #384, which put the cursor's on a settings row.</b> It is not the
        /// same quantity wearing a different name — a rate has no pixels in it, so none of the argument for the
        /// dial (a screen's resolution, a mouse's DPI) reaches this at all, and one row driving both would let a
        /// player fixing their mouse break their pad. If it ever earns a dial, it earns its own.
        /// </para>
        /// </summary>
        public const float PAD_RATE = 1.0f;

        /// <summary>
        /// The right stick's response (#819): its push raised to this power along the push's own direction, so the first
        /// part of the travel is a fine aim and a full push is still <see cref="PAD_RATE"/>. The left stick's walk took the
        /// same 1.5 for the same reason (#802): a square leaves the first third of the travel all but dead.
        /// </summary>
        public const float PAD_STICK_EXPONENT = 1.5f;

        /// <summary>
        /// What the right stick asks of <see cref="Cannon.Aim"/>: pitch and yaw, from the stick's push shaped by
        /// <see cref="PAD_STICK_EXPONENT"/> (radially, so a diagonal keeps its direction) and scaled by
        /// <paramref name="rateScale"/>. The Game hands the lens's own ratio there (<c>PreciseAim.CursorRateScale</c>),
        /// 1 in the overview, so leaning in slows the stick by exactly what the lens magnifies, as it does the mouse.
        /// The owner found the pad's precise aim far too quick (#819): the magnified view swung as many degrees a
        /// second as the overview, several times faster on screen. No player's dial reaches it, for PAD_RATE's reason.
        /// </summary>
        public static Vector2 PadAimRate(Vector2 stick, float rateScale)
        {
            float push = stick.Length();
            if (push <= 0f) return Vector2.Zero;

            Vector2 shaped = stick * (MathF.Pow(MathF.Min(push, 1f), PAD_STICK_EXPONENT) / push);
            return new Vector2(shaped.Y, -shaped.X) * (PAD_RATE * rateScale);
        }

        /// <summary>
        /// Whether a captured frame has been seen yet. False means the next delta is thrown away — which is what
        /// makes grabbing the cursor, returning focus, or a viewport change a no-op for the aim rather than a
        /// lurch. Cleared through <see cref="Invalidate"/>.
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Drops the aim's baseline, so the next frame re-centres and applies nothing. Call it whenever the
        /// cursor is about to arrive somewhere unrelated to where the player left it: focus returning, the
        /// viewport changing, a mode switch, a session being installed.
        /// </summary>
        public void Invalidate()
        {
            Initialized = false;
            _pending = Vector2.Zero;
        }

        /// <summary>
        /// The cursor travel read but not yet handed to the gun, in pixels (#644) — see <c>smoothingSeconds</c> on
        /// <see cref="ApplyCursor"/>. Zero whenever there is no smoothing, and dropped with the baseline.
        /// </summary>
        private Vector2 _pending;

        /// <summary>
        /// Turns this frame's cursor offset from the viewport centre into an aim movement. Does nothing until a
        /// captured frame has been seen, and nothing on a zero-length frame.
        /// </summary>
        /// <param name="mouse">This frame's snapshot — taken once by the caller, never polled here.</param>
        /// <param name="centreX">Live viewport centre, per the class remarks. Do not cache it.</param>
        /// <param name="rateScale">Everything the caller wants multiplied into <see cref="SENSITIVITY"/> this
        /// frame, as one number (#384). Two things ride it and they have deliberately different lifetimes: the
        /// player's own dial off the settings page, which is a <i>setting</i>, and
        /// <see cref="PreciseAim.CursorRateScale"/>, which is a <i>frame</i>. They are multiplied by the caller
        /// rather than stored here because this class cannot own either — the dial belongs to a settings file
        /// the Testbed does not have, and a copy held here would be one more thing to keep in step with a page
        /// the player can open in the middle of a level. <b>1 is the shipped feel</b>, and is what a caller with
        /// neither passes.</param>
        /// <param name="smoothingSeconds">
        /// How long the read travel takes to reach the gun, as the time constant of an exponential hand-off (#644);
        /// zero hands it over on the frame it was read, which is the overview's feel and the Testbed's. <b>It keeps
        /// every pixel</b> — the gun turns exactly as far as it always did, only spread over a few frames — so no
        /// sensitivity changes with it. What it is for: the cursor arrives in whole pixels, after Windows' pointer
        /// ballistics, and a slow, steady drag measured in the Game with the lens leaned in came through as 0 on
        /// three frames in four and 1 on the fourth (600 mouse counts in 3 s read as 80 pixels), so the magnified
        /// view stood still and then jumped a step. The owner's words: a mouse on a coarse wire mat.
        /// </param>
        public void ApplyCursor(Cannon cannon, in MouseState mouse, int centreX, int centreY, GameTime gameTime,
            float rateScale, float smoothingSeconds = 0f)
        {
            if (!Initialized) return;

            float dtMillis = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            if (dtMillis <= 0f) return;

            //What this frame hands over: all of it without smoothing, else the share an exponential approach
            //would have covered in this frame's time — frame-rate independent, and never more than was read
            _pending += new Vector2(mouse.X - centreX, mouse.Y - centreY);
            float share = smoothingSeconds > 0f ? 1f - MathF.Exp(-dtMillis * 0.001f / smoothingSeconds) : 1f;
            Vector2 delta = _pending * share;
            _pending -= delta;

            //The last sliver is not left to creep for seconds: under a hundredth of a pixel it goes now
            if (_pending.LengthSquared() < 1e-4f)
            {
                delta += _pending;
                _pending = Vector2.Zero;
            }

            float rate = SENSITIVITY * rateScale * (1f / dtMillis);
            float pitch = -delta.Y * rate;   //mouse up -> aim up
            float yaw = -delta.X * rate;     //mouse left -> yaw left

            if (pitch != 0f || yaw != 0f) cannon.Aim(new Vector2(pitch, yaw), gameTime);
        }

        /// <summary>
        /// The pad's right stick, fed in as a rate (<see cref="PadAimRate"/>). <paramref name="rateScale"/> is the lens's
        /// ratio while leaning in, 1 otherwise (the Testbed's).
        /// </summary>
        public static void ApplyPad(Cannon cannon, in GamePadState pad, GameTime gameTime, float rateScale = 1f)
        {
            if (!pad.IsConnected || pad.ThumbSticks.Right.LengthSquared() <= 0f) return;

            cannon.Aim(PadAimRate(pad.ThumbSticks.Right, rateScale), gameTime);
        }

        /// <summary>
        /// Puts the cursor back in the middle of the viewport and marks the baseline good, so the next frame's
        /// offset is a true delta. Runs even on a frame whose delta was discarded or whose aim was frozen — that
        /// is what stops the aim being handed the distance the cursor drifted while it was not looking.
        /// </summary>
        public void Recentre(int centreX, int centreY)
        {
            Mouse.SetPosition(centreX, centreY);

            Initialized = true;
        }
    }
}
