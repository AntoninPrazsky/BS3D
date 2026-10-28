using BS3D.Lighting;
using BS3D.Platform;
using BS3D.Screens;
using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using System;

namespace BS3D
{
    public partial class BS3DGame
    {
        //The keyboard's lights (#987): what was composed last, the queue's colours, and the mode the board is in and since when
        private Vector3[] _lightLamps = Array.Empty<Vector3>();
        private readonly Vector3[] _lightQueue = new Vector3[Magazine.SIZE - 1];
        private KeyLightMode _lightMode = KeyLightMode.Idle;
        private float _lightModeSince;
        private bool _lightsWereShown;

        /// <summary>
        /// The backlit keyboard's frame (#987): what the stack is showing, composed into one colour a lamp
        /// (<see cref="KeyLightModel.Compose"/>, no allocation) and handed to the backend, which sends it off the frame at the
        /// device's pace. Started the first time it is wanted, so a player who has turned the lights off never has the keyboard
        /// opened at all, and never in a capture run, which films the game and has no hands on the keyboard. Only while the window
        /// is active: Windows gives the lamps to the app in the foreground and drops anyone else's writes, and the board is sent
        /// whole again the frame the window comes back.
        /// </summary>
        private void UpdateKeyboardLights()
        {
            bool wanted = _effective.KeyboardLighting && _capture == null;
            if (wanted) KeyboardLights.Start();
            KeyboardLights.SetWanted(wanted);

            KeyLightLayout layout = KeyboardLights.Layout;
            if (!wanted || layout == null || !IsActive)
            {
                _lightsWereShown = false;
                return;
            }

            //A new array only when a keyboard with another number of lamps has been found
            if (_lightLamps.Length != layout.LampCount) _lightLamps = new Vector3[layout.LampCount];

            KeyLightInput input = new() { Clock = _wallClock };
            KeyLightMode mode = KeyLightMode.Idle;

            //Play is the level on the stack with no pause over it; the result page over it is the ending, read off the record
            //the page was built from
            if (_gameplayScreen is { IsBuilt: true } play && _screens.Contains<GameplayScreen>() && !_screens.Contains<PausePage>())
            {
                if (!_screens.Contains<ResultPage>())
                {
                    mode = KeyLightMode.Playing;
                    play.FillKeyLights(ref input, _lightQueue);
                }
                else if (play.KeyLightEnding(out bool cleared, out int stars))
                {
                    mode = cleared ? KeyLightMode.Won : KeyLightMode.Lost;
                    input.Stars = stars;
                }
            }

            if (mode != _lightMode)
            {
                _lightMode = mode;
                _lightModeSince = _wallClock;
            }
            input.Mode = mode;
            input.ModeSeconds = _wallClock - _lightModeSince;

            KeyLightModel.Compose(input, _lightQueue, layout, _lightLamps);
            KeyboardLights.Submit(_lightLamps, resend: !_lightsWereShown);
            _lightsWereShown = true;
        }
    }
}
