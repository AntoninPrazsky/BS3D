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

        //How much faster the camera flies while LeftShift is held
        private const float SPRINT_MULTIPLIER = 3f;

        #endregion Controls

        #region Animation

        private float _animationStep = 0;
        private static readonly float ANIMATION_SPEED = Constants.THOUSANDTH;

        //Above this much of the vertical axis in an offset, the vertical axis is no longer usable to turn around
        private const float VERTICAL_LIMIT = 0.99f;
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
            if (Keyboard.GetState().IsKeyDown(Keys.LeftShift)) _cameraSpeed = Constants.ONE * SPRINT_MULTIPLIER;

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

			float step = MathHelper.SmoothStep(0f, Constants.ONE, _animationStep);

			//Swinging the camera around the point it ends up looking at keeps it on an arc around the map, instead
			//of taking the short cut straight through the middle of it. Both ends of the arc are measured from that
			//same point; the target of the camera is no use as a pivot, as it always sits just in front of it
			_camera.Position = _endCameraTarget +
					SlerpOffset(_startCameraPos - _endCameraTarget, _endCameraPos - _endCameraTarget, step);

			//The camera keeps looking at the map for the whole of the arc. Easing the direction in from wherever it
			//was pointing instead would leave it lagging behind the arc, with the map sliding off the screen halfway
			//through; only a camera that did not start out facing the map turns abruptly, and just on the first frame
			_camera.Target = _endCameraTarget;
			_camera.Recalculate();

			_animationStep += ANIMATION_SPEED * (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_animationStep > Constants.ONE)
            {
                _animateCamera = false;
                _animationStarted = false;
            }
        }

        /// <summary>
        /// Interpolates between two camera offsets along the arc between them: the direction turns at a steady
        /// rate around the target and the distance from it is interpolated separately.
        /// </summary>
        private static Vector3 SlerpOffset(Vector3 from, Vector3 to, float step)
        {
            float fromLength = from.Length();
            float toLength = to.Length();

            //Without a direction on one of the ends there is no arc to follow
            if (fromLength < Constants.THOUSANDTH || toLength < Constants.THOUSANDTH) return Vector3.Lerp(from, to, step);

            Vector3 fromDirection = from / fromLength;
            Vector3 toDirection = to / toLength;
            float length = MathHelper.Lerp(fromLength, toLength, step);

            float dot = MathHelper.Clamp(Vector3.Dot(fromDirection, toDirection), -Constants.ONE, Constants.ONE);
            float angle = (float)Math.Acos(dot);

            //Almost the same direction: there is hardly an arc left and the division by its sine would blow up
            if (angle < Constants.THOUSANDTH) return Vector3.Normalize(Vector3.Lerp(fromDirection, toDirection, step)) * length;

            //Opposite directions (a view swapped for the one facing it) leave the arc undefined, as every way
            //round is equally short. Turning around the vertical axis takes the camera around the side, which
            //reads better than over the top; only a view that is already vertical has to go over the top
            if (angle > MathHelper.Pi - Constants.THOUSANDTH)
            {
                Vector3 axis = Math.Abs(Vector3.Dot(fromDirection, Vector3.Up)) < VERTICAL_LIMIT ? Vector3.Up : Vector3.Right;

                //Only the part of the axis perpendicular to the offset turns it, and half a turn around
                //a perpendicular axis is exactly what lands on the opposite direction
                axis = Vector3.Normalize(axis - fromDirection * Vector3.Dot(axis, fromDirection));

                return Vector3.Transform(fromDirection, Matrix.CreateFromAxisAngle(axis, step * MathHelper.Pi)) * length;
            }

            float sine = (float)Math.Sin(angle);
            Vector3 direction =
                    ((float)Math.Sin((Constants.ONE - step) * angle) * fromDirection +
                    (float)Math.Sin(step * angle) * toDirection) / sine;

            return direction * length;
        }

        public void Initialize()
        {
        }
    }
}