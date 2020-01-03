using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MapEditor
{
	public static class InputHelper
	{
		/// <summary>
		/// Výchozí pozice kamery.
		/// </summary>
		public class Positions
		{
			public static Vector3 DefaultCameraPosition => new Vector3(0f, 5f, 25f);
		}

		/// <summary>
		/// Zjištění, zda byla daná klávesa/tlačítko stisknuta pouze jednou.
		/// </summary>
		/// <param name="key">Klávesa klávesnice.</param>
		/// <param name="button">Tlačítko herního ovladače.</param>
		/// <param name="keyboardState">Aktuální stav klávesnice.</param>
		/// <param name="gamePadState">Aktuální stav herního ovladače.</param>
		/// <param name="previousKeyboardState">Předchozí stav klávesnice.</param>
		/// <param name="previousGamePadState">Předchozí stav herního ovladače.</param>
		/// <returns>Vrací <code>true</code>, pokud klávesa nebo tlačítko bylo stisknuto pouze jednou a
		/// <code>false</code>, pokud nebylo.</returns>
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
		/// Zjištění, zda byla daná klávesa stisknuta pouze jednou.
		/// </summary>
		/// <param name="key">Klávesa klávesnice.</param>
		/// <param name="keyboardState">Aktuální stav klávesnice.</param>
		/// <param name="previousKeyboardState">Předchozí stav klávesnice.</param>
		/// <returns>Vrací <code>true</code>, pokud klávesa byla stisknuta pouze jednou a <code>false</code>, pokud
		/// nebyla.</returns>
		public static bool PressedOnce(
				Keys key,
				KeyboardState keyboardState,
				KeyboardState previousKeyboardState)
		{
			bool keyboardPressed = keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);

			return keyboardPressed;
		}

		/// <summary>
		/// Zjištění, zda bylo dané tlačítko myši stisknuto pouze jednou.
		/// </summary>
		/// <param name="leftMouseButton">Levé tlačítko myši.</param>
		/// <param name="middleMouseButton">Prostřední tlačítko myši.</param>
		/// <param name="rightMouseButton">Pravé tlačítko myši.</param>
		/// <param name="mouseState">Aktuální stav myši.</param>
		/// <param name="previsousMouseState">Předchozí stav myši.</param>
		/// <returns>Vrací <code>true</code>, pokud dané tlačítko bylo stisknuto pouze jednou a <code>false</code>,
		/// pokud nebylo.</returns>
		public static bool PressedOnce(
				bool leftMouseButton,
				bool middleMouseButton,
				bool rightMouseButton,
				MouseState mouseState,
				MouseState previsousMouseState)
		{
			bool mousePressed = leftMouseButton &&
					(mouseState.LeftButton == ButtonState.Pressed)
					&& (previsousMouseState.LeftButton == ButtonState.Released);

			mousePressed = middleMouseButton &&
					(mouseState.MiddleButton == ButtonState.Pressed)
					&& (previsousMouseState.MiddleButton == ButtonState.Released);

			mousePressed = rightMouseButton &&
					(mouseState.RightButton == ButtonState.Pressed)
					&& (previsousMouseState.RightButton == ButtonState.Released);

			return mousePressed;
		}
	}
}