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

            //The ending is the result page's own figures (a page the `result` argument stages too), or, before that page, the
            //record a level that has ended keeps (the campaign's finale stands in front of the page). Play is the level itself on
            //top: a pause or the note page over it is the menus' board
            if (_screens.Contains<ResultPage>())
            {
                LevelResult result = _resultPage.Result;
                mode = result.Cleared ? KeyLightMode.Won : KeyLightMode.Lost;
                input.Stars = result.Stars;
            }
            else if (_gameplayScreen is { IsBuilt: true } play && _screens.Contains<GameplayScreen>() && !_screens.Contains<PausePage>())
            {
                if (play.KeyLightEnding(out bool cleared, out int stars))
                {
                    mode = cleared ? KeyLightMode.Won : KeyLightMode.Lost;
                    input.Stars = stars;
                }
                else if (_screens.Active is GameplayScreen)
                {
                    mode = KeyLightMode.Playing;
                    play.FillKeyLights(ref input, _lightQueue);
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
