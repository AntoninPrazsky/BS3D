using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Prazsky.Core.Camera;
using Prazsky.Core.Tools;
using System;

namespace Prazsky.BS3D.Input
{
    public class CameraInputHelper : IUpdateable, IGameComponent
    {
        private readonly BasicCamera3D _camera;
        private readonly Game _game;

        #region Controls

        private GamePadState _currentGamePadState = new();
        private KeyboardState _currentKeyboardState = new();
        private MouseState _currentMouseState = new();

        private GamePadState _previousGamePadState = new();
        private KeyboardState _previousKeyboardState = new();
        private MouseState _previousMouseState = new();

        private readonly int _heightHalf;
        private readonly int _widthHalf;
        private bool _mousePanMode = false;
        private bool _mouseRotationMode = false;
        private float _cameraSpeed = Constants.ONE;

        #endregion Controls

        #region Animation

        private float _animationStep = 0;
        private static readonly float ANIMATION_SPEED = Constants.THOUSANDTH;
        private bool _animateCamera = false;
        private bool _animationStarted = false;
        private Vector3 _startCameraPos, _endCameraPos, _startCameraTarget, _endCameraTarget;

        #endregion Animation

        private readonly Vector3 _initialPosition;
        private readonly Vector3 _initialTarget;

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

            _initialPosition = camera.Position;
            _initialTarget = camera.Target;
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

        public void CameraMovement(GameTime gameTime, bool allowCircularMovement = true)
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

            _cameraSpeed = Constants.ONE;
            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)) _cameraSpeed = Constants.ONE * 3f;

            if (Keyboard.GetState().IsKeyDown(Keys.W)) _camera.Move(0, 0f, -_cameraSpeed, gameTime);
            if (Keyboard.GetState().IsKeyDown(Keys.S)) _camera.Move(0, 0f, _cameraSpeed, gameTime);
            if (Keyboard.GetState().IsKeyDown(Keys.A)) _camera.Move(-_cameraSpeed, 0f, 0f, gameTime);
            if (Keyboard.GetState().IsKeyDown(Keys.D)) _camera.Move(_cameraSpeed, 0f, 0f, gameTime);
            if (Keyboard.GetState().IsKeyDown(Keys.E)) _camera.Move(0f, _cameraSpeed, 0f, gameTime);
            if (Keyboard.GetState().IsKeyDown(Keys.Q)) _camera.Move(0f, -_cameraSpeed, 0f, gameTime);

            if (allowCircularMovement)
            {
                if (Keyboard.GetState().IsKeyDown(Keys.NumPad9))
                {
                    _mouseRotationMode = false;
                    _mousePanMode = false;

                    _camera.MoveCircular(-_cameraSpeed, gameTime);
                }

                if (Keyboard.GetState().IsKeyDown(Keys.NumPad7))
                {
                    _mouseRotationMode = false;
                    _mousePanMode = false;

                    _camera.MoveCircular(_cameraSpeed, gameTime);
                }
            }

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
            _camera.Position = _initialPosition;
            _camera.Target = _initialTarget;
            _camera.ResetCircularMovementProperties();

            _camera.Recalculate();
        }

        public void Update(GameTime gameTime)
        {
            if (!_animateCamera) return;

			if (!_animationStarted)
			{
				_animationStep = 0f;
				_animationStarted = true;
			}

			_camera.Position = Vector3.SmoothStep(_startCameraPos, _endCameraPos, _animationStep);
			_camera.Target = Vector3.SmoothStep(_startCameraTarget, _endCameraTarget, _animationStep);
			_camera.Recalculate();

			_animationStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_animationStep > Constants.ONE)
            {
                _animateCamera = false;
                _animationStarted = false;
            }
        }

        public void Initialize()
        {
        }
    }
}