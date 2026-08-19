using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Screens;
using System;
using System.IO;

namespace BS3D.Screens
{
    /// <summary>
    /// The setting the front end stands in, at the <b>bottom of the stack</b> for the whole life of the
    /// program. Every menu page sits over it with <see cref="Screen.DrawsUnderlying"/>, so "a menu with no
    /// session still shows the world" is the stack working rather than a special case in the host's
    /// <c>Draw</c>; while a level is being played the <see cref="GameplayScreen"/> above it draws the
    /// setting itself (its gameplay is <i>interleaved</i> with it), and this screen lies dormant underneath.
    /// <para>
    /// Its update is the front end's motion: the slow orbit around the scene, and the adaptive quality probe
    /// that watches the frame rate while the menu is what is being drawn. Both stop the moment a session is
    /// on the stack, because the pages above only let this screen update while no game stands over it.
    /// </para>
    /// <para>
    /// Since #249 the setting it draws is not empty of the game: a random level's map hangs over the island
    /// — no cannon, no physics, just the cluster and the glass over it — so the player sees at launch what a
    /// juicy map awaits them to shoot apart. The map is drawn again at random on every return to the front
    /// end (<see cref="BS3DGame.ReturnToMainMenu"/>).
    /// </para>
    /// </summary>
    internal sealed class BackdropScreen : Screen
    {
        private readonly BS3DGame Game;

        //The menu camera orbits the scene's origin in XZ. The angle is advanced by elapsed seconds, never by
        //a frame count, so the turn takes the same time on any machine.
        private float _angle;

        //The preview the front end hangs: pure data, rolled at random out of the level set. The offset is
        //the very one a session would derive for the same map, so the cluster sits where it will sit when
        //played — and the glass is a pose rather than a body, because nothing ever steps this ceiling down.
        private BallsMap _previewMap;
        private Vector3 _previewOffset = Vector3.Zero;
        private Matrix _menuCeilingWorld = Matrix.Identity;

        //Its own generator, in the manner of the host's own private RANDOM: the menu's scene pick and its
        //map pick have no reason to share a stream.
        private static readonly Random RANDOM = new();

        //The orbit sits well outside the island (radius 26) so the whole platform reads with the backdrop
        //around it, and close to level with its target so the frame is a look across the scene rather than
        //down onto it — which is what shows most of a city, a sea or a mountain range at once.
        private const float CAM_RADIUS = 44f;
        private const float CAM_HEIGHT = 3f;
        private const float TARGET_Y = 5f;

        //About a full turn every 90 s: slow enough to read as ambience rather than as a turntable.
        private const float ROTATION_SPEED = MathHelper.TwoPi / 90f;

        private static readonly float FOV = MathF.PI / 3f;  //60°: wide, to take in the scene behind the panel

        public BackdropScreen(BS3DGame game)
        {
            Game = game;

            //Rolled here rather than on first draw: the level set is loaded and the sky is up by the time the
            //host builds this screen, so the very first frame the front end ever shows already carries the
            //promise.
            RollPreviewMap();
        }

        /// <summary>
        /// The front end's slow orbit around the scene's origin, on the game's own <see cref="RecoilCamera"/>
        /// with its shake at rest — nothing kicks it here. Runs whenever no <see cref="GameplayScreen"/> is on
        /// the stack (the menu pages pass updates down exactly then), which is also when the adaptive quality
        /// probe is a fair measurement: the menu draws the same city, clouds, glare and tonemap the game does
        /// — and, since #249, shades a real cluster, so the verdict the probe reaches already includes the
        /// balls.
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

            Game.TuneQualityToFrameRate(elapsed, "menu");
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
        /// reads as the camera being yanked sideways rather than being released.
        /// </summary>
        internal void AlignOrbitTo(Vector3 lens) => _angle = MathF.Atan2(lens.Z, lens.X);

        /// <summary>
        /// Draws a level at random and hangs it over the island: any entry of the set, played or not — the
        /// front end's promise is <i>what awaits</i>, not what is next, which is why progress has no say
        /// here. The scene and its dome are not the map's: the backdrop keeps whatever sky it stands under,
        /// the note the feature answers being explicit that they do not matter (#249).
        /// <para>
        /// The map is data and nothing more — no physics is built for it, no cannon stands under it. It
        /// hangs by the very offset a session derives for the same map
        /// (<see cref="GameplayScreen.FitClusterWorldOffset"/>), under the menu's own glass
        /// (<see cref="BS3DGame.MenuCeilingRenderer"/>) fitted to its footprint.
        /// </para>
        /// </summary>
        internal void RollPreviewMap()
        {
            _previewMap = null;
            _previewOffset = Vector3.Zero;
            _menuCeilingWorld = Matrix.Identity;

            LevelSet set = Game.LevelSet;
            if (set == null || set.Count == 0) return;

            //Every entry tried at most once, from a random start: one unreadable file skips that map, not
            //the feature — and if the whole set is unreadable, the front end falls back to the bare setting
            //it always was.
            int start = RANDOM.Next(set.Count);
            for (int tried = 0; tried < set.Count; tried++)
            {
                int index = (start + tried) % set.Count;

                string path = set.ResolvePath(index);
                BallsMap map = null;
                string name = null;
                try
                {
                    //The same tolerance InstallLevel reads a level with: a level file carries its map
                    //inside, anything else is tried as a plain map outright.
                    if (Level.IsLevelFile(path))
                    {
                        map = new BallsMap(Level.Load(path).Map);
                        name = set.DisplayName(index);
                    }
                    else
                    {
                        map = new BallsMap(path);
                        name = Path.GetFileNameWithoutExtension(path);
                    }
                }
                catch (Exception)
                {
                    //Unreadable or unparseable — the next entry is the whole recovery.
                }

                if (map == null) continue;

                map.Center();
                _previewMap = map;
                _previewOffset = GameplayScreen.FitClusterWorldOffset(map, out float fieldTopY);

                //The menu's glass over what was just hung, and the sky palette the fresh renderer starts
                //without — the same re-run the session makes after its own refit.
                Game.RebuildMenuCeilingRenderer(map.StageSizeX, map.StageSizeZ);
                Game.ApplySkyLighting();
                _menuCeilingWorld = Matrix.CreateTranslation(0f, CeilingPlate.CentreYAbove(fieldTopY), 0f);

                Console.WriteLine($"[menu] preview map {name} — {map.GetBallsCount()} balls");
                return;
            }

            Console.WriteLine("[menu] preview map: no readable level in the set");
        }

        /// <summary>
        /// The setting with the preview in its gameplay slot: the host's pipeline sliced open, the map's
        /// cluster and the menu's glass drawn where the session draws its own. The collection happens
        /// <b>before</b> <see cref="BS3DGame.BeginSceneDraw"/> because the LOD ladder solves against the back
        /// buffer's height — the same slot <see cref="GameplayScreen.Draw"/> collects in — and the balls
        /// draw after it, in the states it binds. No gun, no trails, no warning grid: only the map hangs
        /// here, breathing on the wall clock.
        /// </summary>
        public override void Draw(GameTime gameTime)
        {
            BallDrawFrame ballFrame = Game.Balls.BeginFrame(Game.Camera);
            ballFrame.AddMap(_previewMap, _previewOffset);

            SceneFrame sceneFrame = Game.BeginSceneDraw();

            Game.Balls.Draw(Game.WallClock);

            Game.DrawSettingGlass();

            //Over the glass drain, as in play — and hung from the pose RollPreviewMap wrote, at rest above
            //the preview field's top level. Null only through the no-readable-level fallback above.
            Game.MenuCeilingRenderer?.Draw(Game.Camera, _menuCeilingWorld, Game.SceneEffectParams);

            Game.FinishSceneDraw(sceneFrame);
        }
    }
}
