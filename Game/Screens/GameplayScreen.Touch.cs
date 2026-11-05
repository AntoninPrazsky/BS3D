using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// Play on a touch screen (#1050, #1051): the owner's scheme, read by <see cref="TouchControls"/> and applied here
    /// through the very calls the mouse, the keys and the pad make. The aiming side's drag aims as the mouse does; the
    /// firing side's tap is <see cref="Shoot"/> and its drag the left stick's traverse and walk; the precise-aim button is a
    /// toggle; pause and the power-up chips are buttons; a finger put down during a camera takeover skips it.
    /// </summary>
    internal sealed partial class GameplayScreen
    {
        /// <summary>The precise-aim toggle's radius, in screen heights: about 8 mm on a phone held landscape.</summary>
        internal const float TOUCH_PRECISE_RADIUS = 0.065f;

        /// <summary>Its centre's height down the screen, between the score above and the magazine and chips below.</summary>
        internal const float TOUCH_PRECISE_Y = 0.42f;

        /// <summary>The pause button's radius, in screen heights: smaller, in a corner, where a thumb does not rest.</summary>
        internal const float TOUCH_PAUSE_RADIUS = 0.045f;

        /// <summary>How much further than its drawn edge a button takes a touch, as a share of its radius.</summary>
        private const float TOUCH_BUTTON_REACH = 1.3f;

        //The fingers' reading of the frame
        private readonly TouchControls _touch = new();
        private Func<Vector2, TouchControls.Button> _touchButtonAt;

        //Precise aim toggled on (#1050: "a tap to turn it on and off, nothing held"): by the touch screen's button, and since
        //#1054 by the right button and the left trigger when Settings says Toggle
        private bool _preciseAimToggled;

        //Whether the right button or the left trigger was down last frame, so a press is told from a hold (#1054)
        private bool _leanButtonWasDown = true;

        //Whether the last frame read the touches as play. False after anything else had the screen - a menu, the pause,
        //another app - and the next frame then adopts what is down rather than reading it (TouchControls.Adopt)
        private bool _touchLive;

        /// <summary>The precise-aim toggle: on the firing side's outer edge, between the score and the magazine.</summary>
        internal static Vector2 TouchPreciseCentre(int width, int height, bool aimOnRight)
        {
            float inset = height * (TOUCH_PRECISE_RADIUS + 0.05f);
            return new Vector2(aimOnRight ? inset : width - inset, height * TOUCH_PRECISE_Y);
        }

        /// <summary>The pause button: the top left corner, under nothing the HUD draws there in play.</summary>
        internal static Vector2 TouchPauseCentre(int width, int height)
        {
            float inset = height * (TOUCH_PAUSE_RADIUS + 0.035f);
            return new Vector2(inset, inset);
        }

        /// <summary>
        /// Reads this frame's touches, once, as play (or adopts them, on the first frame back), and notes the screen as the
        /// hand for the tutorial and the rumble when a finger goes down.
        /// </summary>
        private void ReadTouch(float elapsed)
        {
            TouchCollection touches = TouchPanel.GetState();
            Viewport viewport = GraphicsDevice.Viewport;
            _touch.AimOnRight = Game.TouchAimOnRight;
            _tutorial.TouchAimOnRight = Game.TouchAimOnRight;
            _tutorial.LeanToggles = Game.PreciseAimToggles;

            if (!_touchLive)
            {
                _touch.Adopt(touches);
                _touchLive = true;
                return;
            }

            _touchButtonAt ??= TouchButtonAt;
            _touch.Update(touches, viewport.Width, viewport.Height, elapsed, _touchButtonAt);

            if (_touch.AnyPress) NoteDevice(Tutorial.Device.Touch);
        }

        /// <summary>Which button, if any, a touch landing at <paramref name="point"/> is on.</summary>
        private TouchControls.Button TouchButtonAt(Vector2 point)
        {
            Viewport viewport = GraphicsDevice.Viewport;
            int height = viewport.Height;

            if (Vector2.Distance(point, TouchPreciseCentre(viewport.Width, height, Game.TouchAimOnRight))
                <= TOUCH_PRECISE_RADIUS * TOUCH_BUTTON_REACH * height)
                return TouchControls.Button.PreciseAim;

            if (Vector2.Distance(point, TouchPauseCentre(viewport.Width, height)) <= TOUCH_PAUSE_RADIUS * TOUCH_BUTTON_REACH * height)
                return TouchControls.Button.Pause;

            //The chips where the HUD drew them last frame, each only while its power-up is on offer
            Point at = new((int)point.X, (int)point.Y);
            if (_hud.ChipBounds(PowerupKind.Swap).Contains(at)) return TouchControls.Button.Swap;
            if (_hud.ChipBounds(PowerupKind.Brake).Contains(at)) return TouchControls.Button.Brake;
            if (_hud.ChipBounds(PowerupKind.Cut).Contains(at)) return TouchControls.Button.Cut;

            return TouchControls.Button.None;
        }

        /// <summary>
        /// The aiming finger's drag turned into the gun's aim, as <c>MouseAim.ApplyCursor</c> turns the cursor's: up the
        /// screen is up, left is left, and the lean slows it by the lens's own ratio and the player's lean dial, as it does
        /// the mouse. The rate is per screen height (<see cref="TouchControls.AIM_RADIANS_PER_HEIGHT"/>) times the touch row.
        /// </summary>
        private void ApplyTouchAim(GameTime gameTime)
        {
            Vector2 drag = _touch.AimDelta;
            if (drag == Vector2.Zero) return;

            float dtMillis = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
            if (dtMillis <= 0f) return;

            float radiansPerPixel = TouchControls.AIM_RADIANS_PER_HEIGHT / GraphicsDevice.Viewport.Height * Game.TouchSensitivity
                * MathHelper.Lerp(1f, Game.AimSensitivity, _preciseAim.Blend) * _preciseAim.CursorRateScale(GAME_FOV);

            //Cannon.Aim multiplies by its rotation speed and the frame's milliseconds; the drag is already this frame's angle
            float scale = radiansPerPixel / (_cannon.RotationSpeed * dtMillis);
            _cannon.Aim(new Vector2(-drag.Y, -drag.X) * scale, gameTime);
        }

        /// <summary>The touch controls over the HUD, while the hand is on the screen.</summary>
        private void DrawTouchControls()
        {
            if (!_tutorial.OnTouch) return;

            Viewport viewport = GraphicsDevice.Viewport;
            _hud.DrawTouchControls(_touch,
                TouchPreciseCentre(viewport.Width, viewport.Height, Game.TouchAimOnRight), TOUCH_PRECISE_RADIUS * viewport.Height,
                _preciseAimToggled,
                TouchPauseCentre(viewport.Width, viewport.Height), TOUCH_PAUSE_RADIUS * viewport.Height);
        }
    }
}
