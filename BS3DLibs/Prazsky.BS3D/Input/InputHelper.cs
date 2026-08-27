using Microsoft.Xna.Framework.Input;

namespace Prazsky.BS3D.Input
{
    public static class InputHelper
    {
        /// <summary>
        /// Detects if a given key/button has been pressed only once.
        /// </summary>
        /// <param name="key">Keyboard key.</param>
        /// <param name="button">Gamepad button.</param>
        /// <param name="keyboardState">Current keyboard state.</param>
        /// <param name="gamePadState">Current gamepad state.</param>
        /// <param name="previousKeyboardState">Previous keyboard state.</param>
        /// <param name="previousGamePadState">Previous gamepad state.</param>
        /// <returns>Returns <code>true</code> if the key or button was pressed only once and <code>false</code> if it was not.</returns>
        public static bool PressedOnce(
                Keys key,
                Buttons button,
                KeyboardState keyboardState,
                GamePadState gamePadState,
                KeyboardState previousKeyboardState,
                GamePadState previousGamePadState)
        {
            bool keyboardPressed = keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
            bool gamePadPressed = gamePadState.IsButtonDown(button) && !previousGamePadState.IsButtonDown(button);
            return keyboardPressed || gamePadPressed;
        }

        /// <summary>
        /// Detects if a given key has been pressed only once.
        /// </summary>
        /// <param name="key">Keyboard key.</param>
        /// <param name="keyboardState">Current keyboard state.</param>
        /// <param name="previousKeyboardState">Previous keyboard state.</param>
        /// <returns>Returns <code>true</code> if the key was pressed only once and <code>false</code> if it was not.</returns>
        public static bool PressedOnce(
                Keys key,
                KeyboardState keyboardState,
                KeyboardState previousKeyboardState)
        {
            bool keyboardPressed = keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);

            return keyboardPressed;
        }

        /// <summary>
        /// Detects if any of the given mouse buttons was pressed only once.
        /// </summary>
        /// <param name="leftMouseButton">Left mouse button.</param>
        /// <param name="middleMouseButton">Middle mouse button.</param>
        /// <param name="rightMouseButton">Right mouse button.</param>
        /// <param name="mouseState">Current mouse state.</param>
        /// <param name="previousMouseState">Previous mouse state.</param>
        /// <returns>Returns <code>true</code> if any of the given buttons was pressed only once and <code>false</code> if none were pressed.</returns>
        public static bool PressedOnce(
                bool leftMouseButton,
                bool middleMouseButton,
                bool rightMouseButton,
                MouseState mouseState,
                MouseState previousMouseState)
        {
            return
            (leftMouseButton && mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
            ||(middleMouseButton && mouseState.MiddleButton == ButtonState.Pressed && previousMouseState.MiddleButton == ButtonState.Released)
            ||(rightMouseButton && mouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released);
        }
    }
}