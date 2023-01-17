using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.Core.Camera;
using System;

namespace Prazsky.BS3D.Input
{
	public class CameraInputHelper : IUpdateable, IGameComponent
	{
		private BasicCamera3D _camera;
		private Game _game;

		#region Controls

		private GamePadState _currentGamePadState = new GamePadState();
		private KeyboardState _currentKeyboardState = new KeyboardState();
		private MouseState _currentMouseState = new MouseState();

		private GamePadState _previousGamePadState = new GamePadState();
		private KeyboardState _previousKeyboardState = new KeyboardState();
		private MouseState _previousMouseState = new MouseState();

		private int _heightHalf;
		private int _widthHalf;
		private bool _mousePanMode = false;
		private bool _mouseRotationMode = false;

		#endregion Controls

		#region Animation

		private const float PI_COUNT_STEP = 0.0015f;
		private float _piCount = -MathHelper.Pi;

		private bool _animateCamera = false;
		private bool _animationStarted = false;
		private Vector3 _startCameraPos, _endCameraPos, _startCameraTarget, _endCameraTarget;

		#endregion Animation

		public event EventHandler<EventArgs> EnabledChanged;

		public event EventHandler<EventArgs> UpdateOrderChanged;

		public float CameraOffset { get; set; } = 15f;
		public float MouseMovementDenominator { get; set; } = 50f;

		public bool Enabled => true;

		public int UpdateOrder => 0;

		public CameraInputHelper(BasicCamera3D camera, Game game)
		{
			_camera = camera;
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
			#region Gamepad

			if (_currentGamePadState.IsConnected)
			{
				float Z = 0f;
				if (_currentGamePadState.Triggers.Right > 0) Z = -_currentGamePadState.Triggers.Right;
				if (_currentGamePadState.Triggers.Left > 0) Z = _currentGamePadState.Triggers.Left;

				_camera.Move(
						_currentGamePadState.ThumbSticks.Left.X,
						_currentGamePadState.ThumbSticks.Left.Y,
						Z, gameTime);
				_camera.Rotate(
						_currentGamePadState.ThumbSticks.Right.Y,
						-_currentGamePadState.ThumbSticks.Right.X,
						gameTime);
			}

			#endregion Gamepad

			#region Keyboard

			float speed = 1f;
			if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)) speed = 3f;

			if (Keyboard.GetState().IsKeyDown(Keys.W))
				_camera.Move(0, 0f, -speed, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.S))
				_camera.Move(0, 0f, speed, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.A))
				_camera.Move(-speed, 0f, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.D))
				_camera.Move(speed, 0f, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.E))
				_camera.Move(0f, speed, 0f, gameTime);
			if (Keyboard.GetState().IsKeyDown(Keys.Q))
				_camera.Move(0f, -speed, 0f, gameTime);

			#endregion Keyboard

			#region Mouse

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
					mDeltaB = -(_currentMouseState.X - _widthHalf) / MouseMovementDenominator;

				if (_currentMouseState.Y != _heightHalf)
					mDeltaA = -(_currentMouseState.Y - _heightHalf) / MouseMovementDenominator;

				CenterMouse();

				if (_mouseRotationMode && !_mousePanMode)
					_camera.Rotate(mDeltaA, mDeltaB, gameTime);

				if (_currentMouseState.RightButton == ButtonState.Pressed)
				{
					_mousePanMode = true;
					_camera.Move(-mDeltaB, mDeltaA, 0f, gameTime);
				}
				else
					_mousePanMode = false;
			}

			#endregion Mouse
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

		public void CenterCameraToMapCenter(Vector3 mapCenter, Vector3 lookDirection, bool animate = false)
		{
			Vector3 finalCameraPos = mapCenter + lookDirection * CameraOffset;
			Vector3 finalCameraTarget = mapCenter;

			if (!animate)
			{
				_camera.Position = finalCameraPos;
				_camera.Target = finalCameraTarget;
				_camera.Recalculate();

				_animateCamera = false;
				return;
			}

			_startCameraPos = _camera.Position;
			_endCameraPos = finalCameraPos;

			_startCameraTarget = _camera.Target;
			_endCameraTarget = finalCameraTarget;

			_animateCamera = true;
		}

		public void RestartCamera()
		{
			_camera.Position = new Vector3(0f, 0f, CameraOffset);
			_camera.Target = Vector3.Zero;
			_camera.Recalculate();
		}

		public void Update(GameTime gameTime)
		{
			if (!_animateCamera) return;

			float elapsedTime = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
			if (_piCount <= MathHelper.Pi) _piCount += PI_COUNT_STEP * elapsedTime;
			else _piCount = -MathHelper.Pi;

			if (!_animationStarted && _animateCamera)
			{
				_piCount = -MathHelper.Pi;
				_animationStarted = true;
			}


			float step = (float)(Math.Cos(_piCount) + 1) / 2f;

#if DEBUG
			Console.WriteLine(step);
#endif

			//TODO: Camera rotation around sphere
			_camera.Position = Vector3.Lerp(_startCameraPos, _endCameraPos, step);
			_camera.Target = Vector3.Lerp(_startCameraTarget, _endCameraTarget, step);
			_camera.Recalculate();

			if (step > 0.9999f)
			{
				_camera.Position = _endCameraPos;
				_camera.Target = _endCameraTarget;
				_camera.Recalculate();

				_animateCamera = false;
				_animationStarted = false;
			}
		}

		public void Initialize()
		{
		}
	}
}