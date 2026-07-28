using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The setting on its own — the scene the front end stands in, at the <b>bottom of the stack</b> for the
    /// whole life of the program. Every menu page sits over it with <see cref="Screen.DrawsUnderlying"/>, so
    /// "a menu with no session still shows the world" is the stack working rather than a special case in the
    /// host's <c>Draw</c>; while a level is being played the <see cref="GameplayScreen"/> above it draws the
    /// setting itself (its gameplay is <i>interleaved</i> with it), and this screen lies dormant underneath.
    /// <para>
    /// Its update is the front end's motion: the slow orbit around the scene, and the adaptive quality probe
    /// that watches the frame rate while the menu is what is being drawn. Both stop the moment a session is
    /// on the stack, because the pages above only let this screen update while no game stands over it.
    /// </para>
    /// </summary>
    internal sealed class BackdropScreen : Screen
    {
        private readonly BS3DGame Game;

        //The menu camera orbits the scene's origin in XZ. The angle is advanced by elapsed seconds, never by
        //a frame count, so the turn takes the same time on any machine.
        private float _angle;

        //The orbit sits well outside the island (radius 26) so the whole platform reads with the backdrop
        //around it, and close to level with its target so the frame is a look across the scene rather than
        //down onto it — which is what shows most of a city, a sea or a mountain range at once.
        private const float CAM_RADIUS = 44f;
        private const float CAM_HEIGHT = 3f;
        private const float TARGET_Y = 5f;

        //About a full turn every 90 s: slow enough to read as ambience rather than as a turntable.
        private const float ROTATION_SPEED = MathHelper.TwoPi / 90f;

        private static readonly float FOV = MathF.PI / 3f;  //60°: wide, to take in the scene behind the panel

        public BackdropScreen(BS3DGame game) => Game = game;

        /// <summary>
        /// The front end's slow orbit around the scene's origin, on the game's own <see cref="RecoilCamera"/>
        /// with its shake at rest — nothing kicks it here. Runs whenever no <see cref="GameplayScreen"/> is on
        /// the stack (the menu pages pass updates down exactly then), which is also when the adaptive quality
        /// probe is a fair measurement: the menu draws the same city, clouds, glare and tonemap the game does.
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            AdvanceOrbit(elapsed, out Vector3 position, out Vector3 target, out float fieldOfView);

            RecoilCamera camera = Game.Camera;

            camera.BasePosition = position;
            camera.BaseTarget = target;
            camera.FieldOfView = fieldOfView;

            camera.Update(elapsed);

            Game.TuneQualityToFrameRate(elapsed);
        }

        /// <summary>
        /// Advances the orbit and hands back the pose it has reached, without touching the camera.
        /// <para>
        /// Shared with <see cref="ResultPage"/>, which flies the camera out onto this very orbit when a level
        /// ends. <b>One orbit and one angle</b>, deliberately: a second orbit of its own would leave the front
        /// end at some unrelated bearing, so pressing "Main Menu" off the result screen would cut to a
        /// different view of the same arena. Sharing it makes that a continuation — the angle the result
        /// screen leaves is the angle this screen picks up, and the pose at the end of its ease is the pose
        /// this screen would have set on its own next frame.
        /// </para>
        /// </summary>
        internal void AdvanceOrbit(float elapsed, out Vector3 position, out Vector3 target, out float fieldOfView)
        {
            _angle += ROTATION_SPEED * elapsed;
            if (_angle >= MathHelper.TwoPi) _angle -= MathHelper.TwoPi;

            position = new Vector3(MathF.Cos(_angle) * CAM_RADIUS, CAM_HEIGHT, MathF.Sin(_angle) * CAM_RADIUS);
            target = new Vector3(0f, TARGET_Y, 0f);
            fieldOfView = FOV;
        }

        /// <summary>
        /// Puts the orbit at the bearing <paramref name="lens"/> already stands on, so a camera flown onto it
        /// moves straight <i>out</i> from the arena rather than swinging around it. Without this the ease
        /// would be a chord to wherever the front end was last left — which crosses the island on the way and
        /// reads as the camera being yanked sideways rather than released.
        /// </summary>
        internal void AlignOrbitTo(Vector3 lens) => _angle = MathF.Atan2(lens.Z, lens.X);

        /// <summary>
        /// The setting-only frame: the host's pipeline with nothing in the gameplay slots — sky, backdrop,
        /// island and pit; the drain's gold and glass; the weather; then the resolve. The same sequence
        /// <see cref="GameplayScreen.Draw"/> runs, minus the session, so the menu shows exactly the world the
        /// game is then played in.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            Game.DrawSetting();
        }
    }
}
