using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// What the fingers ask of play on a touch screen (#1050, #1051), read once a frame from MonoGame's touch state: the
    /// owner's scheme. The screen is two sides, the aiming side (left unless <see cref="AimOnRight"/>) and the firing side.
    /// <list type="bullet">
    /// <item><b>The aiming side is a touchpad</b>: a finger's drag turns the gun relatively, as the mouse does
    /// (<see cref="AimDelta"/>), so the target stays visible above the finger rather than under it.</item>
    /// <item><b>On the firing side a tap fires</b> (<see cref="Fire"/>) and <b>a drag moves the gun</b>, traversing round the
    /// field and walking in and out, as a floating stick that stands where the thumb landed (<see cref="Stick"/>). A touch
    /// is a tap until it travels <see cref="TAP_SLOP"/>, so a tap that slips a little still fires; a press held past
    /// <see cref="TAP_SECONDS"/> without moving is the thumb resting, and lifting it fires nothing.</item>
    /// <item><b>Buttons</b> drawn over either side (the precise-aim toggle, pause, the power-up chips) take a touch that
    /// lands on them, answered on the press (<see cref="Pressed"/>), and that touch is theirs until it lifts: a finger
    /// belongs to one control at a time.</item>
    /// </list>
    /// <para>
    /// Every distance is a fraction of the screen's height: a phone held landscape is about the same height in the hand
    /// whatever its pixels, so a drag means the same turn on a 1080-row screen and a 1440-row one.
    /// </para>
    /// </summary>
    internal sealed class TouchControls
    {
        /// <summary>How far a touch on the firing side travels, in screen heights, before it is a drag and not a tap.</summary>
        internal const float TAP_SLOP = 0.045f;

        /// <summary>How long a touch on the firing side may rest without moving and still fire when it lifts.</summary>
        internal const float TAP_SECONDS = 0.45f;

        /// <summary>The floating stick's full push, in screen heights past <see cref="TAP_SLOP"/>.</summary>
        internal const float STICK_RADIUS = 0.11f;

        /// <summary>
        /// The aim a drag of one screen height turns the gun by at a touch sensitivity of 1, in radians: about 120°, so
        /// half the screen's height sweeps the elevation's range and a short stroke of the thumb is a fine correction.
        /// </summary>
        internal const float AIM_RADIANS_PER_HEIGHT = 2.1f;

        /// <summary>PromptFont's pointing hand: a tap, on a tutorial card and on a power-up chip a finger presses.</summary>
        internal const string TAP_GLYPH = "👆";

        /// <summary>PromptFont's magnifier: the precise-aim button, and the cards that name it.</summary>
        internal const string PRECISE_GLYPH = "🔍";

        /// <summary>A control a touch can land on; <see cref="None"/> when it lands on a side.</summary>
        internal enum Button { None, PreciseAim, Pause, Swap, Brake, Cut }

        private enum Role { Aim, Tap, Stick, Button, Ignored }

        private struct Track
        {
            public int Id;
            public Role Role;
            public Vector2 Start, Last, StickOrigin;
            public float Age;
        }

        //Ten fingers is more than a phone reports
        private readonly Track[] _tracks = new Track[10];
        private int _count;

        /// <summary>Whether the aiming side is the right one (a Settings row, #1050).</summary>
        public bool AimOnRight { get; set; }

        /// <summary>This frame's drag on the aiming side, in pixels, x right and y down.</summary>
        public Vector2 AimDelta { get; private set; }

        /// <summary>A tap on the firing side ended this frame.</summary>
        public bool Fire { get; private set; }

        /// <summary>The firing side's drag as a stick, each axis -1 to 1, x right and y UP; zero with no drag.</summary>
        public Vector2 Stick { get; private set; }

        /// <summary>Where the stick's base and the thumb stand, in pixels, while <see cref="StickActive"/>.</summary>
        public Vector2 StickOrigin { get; private set; }

        /// <inheritdoc cref="StickOrigin"/>
        public Vector2 StickThumb { get; private set; }

        /// <summary>Whether a drag on the firing side is moving the gun.</summary>
        public bool StickActive { get; private set; }

        /// <summary>The button a touch landed on this frame, or <see cref="Button.None"/>.</summary>
        public Button Pressed { get; private set; }

        /// <summary>A finger went down this frame, anywhere: what skips a camera takeover and says the hand is on the screen.</summary>
        public bool AnyPress { get; private set; }

        /// <summary>Whether any finger is on the screen.</summary>
        public bool Touching => _count > 0;

        /// <summary>
        /// Reads this frame's touches. <paramref name="hit"/> says which button, if any, stands under a point; it is asked
        /// only of a touch's first frame.
        /// </summary>
        public void Update(TouchCollection touches, int width, int height, float elapsed, Func<Vector2, Button> hit)
        {
            AimDelta = Vector2.Zero;
            Fire = false;
            Pressed = Button.None;
            AnyPress = false;

            float slop = TAP_SLOP * height;
            float radius = STICK_RADIUS * height;

            //A track whose finger is no longer reported at all (a cancelled gesture) is dropped
            for (int i = _count - 1; i >= 0; i--)
                if (!Reported(touches, _tracks[i].Id)) Remove(i);

            foreach (TouchLocation touch in touches)
            {
                int index = Find(touch.Id);

                if (index < 0)
                {
                    if (touch.State == TouchLocationState.Invalid) continue;
                    AnyPress = true;

                    Track track = new() { Id = touch.Id, Start = touch.Position, Last = touch.Position };
                    Button button = hit?.Invoke(touch.Position) ?? Button.None;

                    if (button != Button.None)
                    {
                        Pressed = button;
                        track.Role = Role.Button;
                    }
                    else if (OnAimingSide(touch.Position, width))
                        track.Role = Has(Role.Aim) ? Role.Ignored : Role.Aim;
                    else
                        track.Role = Role.Tap;

                    //A touch first seen released went down and up between two frames: a tap where it landed
                    if (touch.State == TouchLocationState.Released)
                    {
                        if (track.Role == Role.Tap) Fire = true;
                        continue;
                    }

                    if (_count < _tracks.Length) _tracks[_count++] = track;
                    continue;
                }

                ref Track held = ref _tracks[index];
                held.Age += elapsed;

                switch (held.Role)
                {
                    case Role.Aim:
                        AimDelta += touch.Position - held.Last;
                        break;

                    case Role.Tap:
                        //Past the slop it moves the gun, unless another drag already does
                        if (Vector2.Distance(touch.Position, held.Start) > slop)
                        {
                            held.Role = Has(Role.Stick) ? Role.Ignored : Role.Stick;
                            held.StickOrigin = held.Start;
                        }
                        break;
                }

                if (held.Role == Role.Stick)
                {
                    //A floating base: dragged past the full push, it follows the thumb, so turning back answers at once
                    Vector2 offset = touch.Position - held.StickOrigin;
                    float reach = slop + radius;
                    if (offset.Length() > reach) held.StickOrigin = touch.Position - Vector2.Normalize(offset) * reach;
                }

                held.Last = touch.Position;

                if (touch.State == TouchLocationState.Released)
                {
                    if (held.Role == Role.Tap && held.Age <= TAP_SECONDS) Fire = true;
                    Remove(index);
                }
            }

            //The stick from whichever track holds it this frame
            StickActive = false;
            Stick = Vector2.Zero;
            for (int i = 0; i < _count; i++)
            {
                if (_tracks[i].Role != Role.Stick) continue;

                Vector2 offset = _tracks[i].Last - _tracks[i].StickOrigin;
                float distance = offset.Length();
                float push = MathHelper.Clamp((distance - slop) / radius, 0f, 1f);
                if (distance > 0f) Stick = new Vector2(offset.X, -offset.Y) / distance * push;

                StickActive = true;
                StickOrigin = _tracks[i].StickOrigin;
                StickThumb = _tracks[i].Last;
                break;
            }
        }

        /// <summary>
        /// Takes this frame's touches as already spoken for, in place of <see cref="Update"/>, on the first frame of play
        /// after anything else had the screen (a menu, the pause, another app): a finger still down is ignored until it lifts,
        /// and one lifting now is not a tap. The finger that pressed Resume must not fire the shot it lifts on.
        /// </summary>
        public void Adopt(TouchCollection touches)
        {
            _count = 0;
            foreach (TouchLocation touch in touches)
            {
                if (touch.State is TouchLocationState.Released or TouchLocationState.Invalid || _count >= _tracks.Length) continue;
                _tracks[_count++] = new Track { Id = touch.Id, Role = Role.Ignored, Start = touch.Position, Last = touch.Position };
            }

            AimDelta = Vector2.Zero;
            Fire = false;
            Stick = Vector2.Zero;
            StickActive = false;
            Pressed = Button.None;
            AnyPress = false;
        }

        /// <summary>
        /// Takes every finger on the screen as spoken for until it lifts (#1072): the finger that skipped a camera takeover
        /// must not fire the shot it lifts on. The tap had skipped the level's opening move and shot at once, because its
        /// track stayed a tap and the lift came within a tap's time. <see cref="Fire"/> and <see cref="Pressed"/> of this
        /// frame are withdrawn too, for a finger that went down and up between two frames.
        /// </summary>
        public void Consume()
        {
            for (int i = 0; i < _count; i++) _tracks[i].Role = Role.Ignored;
            Fire = false;
            Pressed = Button.None;
        }

        /// <summary>Whether a point is on the aiming side.</summary>
        public bool OnAimingSide(Vector2 point, int width) => (point.X < width * 0.5f) != AimOnRight;

        private bool Has(Role role)
        {
            for (int i = 0; i < _count; i++)
                if (_tracks[i].Role == role) return true;
            return false;
        }

        private int Find(int id)
        {
            for (int i = 0; i < _count; i++)
                if (_tracks[i].Id == id) return i;
            return -1;
        }

        private static bool Reported(TouchCollection touches, int id)
        {
            foreach (TouchLocation touch in touches)
                if (touch.Id == id) return true;
            return false;
        }

        private void Remove(int index)
        {
            _tracks[index] = _tracks[--_count];
        }
    }
}
