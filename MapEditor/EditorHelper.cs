using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.Core.Camera;

namespace MapEditor
{
	public class CameraInputHelper
	{
		private const float CAMERA_OFFSETT = 15f;

		private BasicCamera3D _editorCamera;
		private Game _game;

		#region Ovládání

		private GamePadState _currentGamePadState = new GamePadState();
		private KeyboardState _currentKeyboardState = new KeyboardState();
		private MouseState _currentMouseState = new MouseState();

		private GamePadState _previousGamePadState = new GamePadState();
		private KeyboardState _previousKeyboardState = new KeyboardState();
		private MouseState _previousMouseState = new MouseState();

		private const float _mouseMovementDenominator = 50f;

		private int _heightHalf;
		private int _widthHalf;
		private bool _mousePanMode = false;
		private bool _mouseRotationMode = false;

		#endregion Ovládání

		public CameraInputHelper(BasicCamera3D camera, Game game)
		{
			_editorCamera = camera;
			_game = game;

			_widthHalf = _game.Window.ClientBounds.Width / 2;
			_heightHalf = _game.Window.ClientBounds.Height / 2;
		}

		public void RegisterCurrentInputState()
		{
			_currentKeyboardState = Keyboard.GetState();
			_currentGamePadState = GamePad.GetState(PlayerIndex.One);
			_currentMouseState = Mouse.GetState();
		}

		public void RegisterPreviousInputState()
		{
			_previousKeyboardState = _currentKeyboardState;
			_previousGamePadState = _currentGamePadState;
			_previousMouseState = _currentMouseState;
		}

		public void CameraMovement(GameTime gameTime)
		{
			#region kompletní ovládání kamery gamepadem

			if (_currentGamePadState.IsConnected)
			{
				float Z = 0f;
				if (_currentGamePadState.Triggers.Right > 0) Z = -_currentGamePadState.Triggers.Right;
				if (_currentGamePadState.Triggers.Left > 0) Z = _currentGamePadState.Triggers.Left;

				_editorCamera.Move(
						_currentGamePadState.ThumbSticks.Left.X,
						_currentGamePadState.ThumbSticks.Left.Y,
						Z, gameTime);
				_editorCamera.Rotate(
						_currentGamePadState.ThumbSticks.Right.Y,
						-_currentGamePadState.ThumbSticks.Right.X,
						gameTime);
			}

			#endregion kompletní ovládání kamery gamepadem

			#region ovládání kamery klávesnicí

			float speed = 1f;
			if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)) speed = 3f;

			if (Keyboard.GetState().IsKeyDown(Keys.W))
				_editorCamera.Move(0, 0f, -speed, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.S))
				_editorCamera.Move(0, 0f, speed, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.A))
				_editorCamera.Move(-speed, 0f, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.D))
				_editorCamera.Move(speed, 0f, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.E))
				_editorCamera.Move(0f, speed, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.Q))
				_editorCamera.Move(0f, -speed, 0f, gameTime);

			#endregion ovládání kamery klávesnicí

			#region ovládání kamery myší

			if (PressedOnceMouse(leftButton: false, middleButton: false, rightButton: true))
			{
				CenterMouse();
				_mouseRotationMode = !_mouseRotationMode;
				return;
			}

			if (_currentMouseState.RightButton == ButtonState.Pressed)
				_mousePanMode = true;

			_game.IsMouseVisible = !_mousePanMode && !_mouseRotationMode;

			if (_mouseRotationMode || _mousePanMode)
			{
				float mDeltaA = 0f;
				float mDeltaB = 0f;

				if (_currentMouseState.X != _widthHalf)
					mDeltaB = -(_currentMouseState.X - _widthHalf) / _mouseMovementDenominator;

				if (_currentMouseState.Y != _heightHalf)
					mDeltaA = -(_currentMouseState.Y - _heightHalf) / _mouseMovementDenominator;

				CenterMouse();

				if (_mouseRotationMode && !_mousePanMode)
					_editorCamera.Rotate(mDeltaA, mDeltaB, gameTime);

				if (_currentMouseState.RightButton == ButtonState.Pressed)
				{
					_mousePanMode = true;
					_editorCamera.Move(-mDeltaB, mDeltaA, 0f, gameTime);
				}
				else
					_mousePanMode = false;
			}

			#endregion ovládání kamery myší
		}

		public bool PressedOnce(Keys key, Buttons button)
		{
			return InputHelper.PressedOnce(
					key,
					button,
					_currentKeyboardState,
					_currentGamePadState,
					_previousKeyboardState,
					_previousGamePadState);
		}

		public bool PressedOnce(Keys key)
		{
			return InputHelper.PressedOnce(
					key,
					_currentKeyboardState,
					_previousKeyboardState);
		}

		public bool PressedOnceMouse(bool leftButton, bool middleButton, bool rightButton)
		{
			return InputHelper.PressedOnce(
					leftButton,
					middleButton,
					rightButton,
					_currentMouseState,
					_previousMouseState);
		}

		public void CenterMouse()
		{
			Mouse.SetPosition(_widthHalf, _heightHalf);
		}

		public void CenterCameraToMapCenter(Vector3 mapCenter, Vector3 lookDirection)
		{
			_editorCamera.Position = mapCenter + (lookDirection * CAMERA_OFFSETT);
			_editorCamera.Target = mapCenter;
			_editorCamera.Recalculate();
		}

		public void RestartCamera()
		{
			_editorCamera.Position = new Vector3(0f, 0f, CAMERA_OFFSETT);
			_editorCamera.Target = Vector3.Zero;
			_editorCamera.Recalculate();
		}
	}
}